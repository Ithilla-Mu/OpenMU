// <copyright file="DefaultDropGenerator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Attributes;
using Nito.AsyncEx;

/// <summary>
/// The default drop generator.
/// </summary>
public class DefaultDropGenerator : IDropGenerator
{
    /// <summary>
    /// The amount of money which is dropped at least, and added to the gained experience.
    /// </summary>
    private const int BaseMoneyDrop = 7;
    private const int DropLevelMaxGap = 12;
    private const int SkillDropChancePercent = 50;

    private const byte DefaultMaxItemOptionLevelDrop = 3;
    private const byte MinItemOptionLevelDrop = 1;
    private const byte MaxItemOptionLevelDrop = 4;

    /// <summary>
    /// A re-usable list of drop item groups.
    /// </summary>
    private readonly List<DropItemGroup> _chanceDropGroups = new(64);
    private readonly List<DropItemGroup> _guaranteedDropGroups = new(16);

    private readonly AsyncLock _lock = new();
    private readonly IRandomizer _randomizer;
    private readonly IList<ItemDefinition> _ancientItems;
    private readonly IList<ItemDefinition> _droppableItems;
    private readonly IList<ItemDefinition>?[] _droppableItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];
    private readonly IList<ItemDefinition>?[] _droppableSocketItemsPerMonsterLevel = new IList<ItemDefinition>?[byte.MaxValue + 1];

    private readonly byte _maxItemOptionLevelDrop;
    private readonly byte _excellentItemDropLevelDelta;
    private readonly ClassAwareDropMode _classAwareDropMode;
    private readonly float _classAwareDropWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDropGenerator" /> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="randomizer">The randomizer.</param>
    public DefaultDropGenerator(GameConfiguration config, IRandomizer randomizer)
    {
        this._excellentItemDropLevelDelta = config.ExcellentItemDropLevelDelta;
        this._randomizer = randomizer;
        this._maxItemOptionLevelDrop = IsValidOptionLevelDrop(config.MaximumItemOptionLevelDrop)
            ? config.MaximumItemOptionLevelDrop
            : DefaultMaxItemOptionLevelDrop;
        this._classAwareDropMode = config.ClassAwareDropMode;

        // Below 1 there is no preference to express; the weighted pick treats an off-class item as weight 1.
        this._classAwareDropWeight = Math.Max(1f, config.ClassAwareDropWeight);
        this._droppableItems = config.Items.Where(i => i.DropsFromMonsters).ToList();
        this._ancientItems = this._droppableItems.Where(
            i => i.PossibleItemSetGroups.Any(
                g => g.Options?.PossibleOptions.Any(
                    o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false))
            .ToList();
    }

    /// <inheritdoc/>
    public async ValueTask<(IEnumerable<Item> Items, uint? Money)> GenerateItemDropsAsync(MonsterDefinition monster, int gainedExperience, Player player)
    {
        var character = player.SelectedCharacter;
        var map = player.CurrentMap?.Definition;
        if (map is null || character is null)
        {
            return ([], null);
        }

        // Built once per drop, outside the group partitioning, since it only reads the killer's party and never mutates shared state.
        var candidateClasses = this._classAwareDropMode == ClassAwareDropMode.Off ? null : BuildPartyCandidateClasses(player);

        using var l = await this._lock.LockAsync();
        this._guaranteedDropGroups.Clear();
        this._chanceDropGroups.Clear();

        if (monster.ObjectKind == NpcObjectKind.Destructible)
        {
            this.PartitionDropGroups(monster.DropItemGroups ?? []);
        }
        else
        {
            this.PartitionDropGroups(monster.DropItemGroups ?? []);
            this.PartitionDropGroups(character.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(map.DropItemGroups ?? [], monster);
            this.PartitionDropGroups(await GetQuestItemGroupsAsync(player).ConfigureAwait(false) ?? [], monster);
        }

        uint money = 0;
        var (droppedItems, moneyResult) = this.GenerateDrops(monster, gainedExperience, candidateClasses);
        if (moneyResult > 0)
        {
            money = moneyResult;
        }

        this._guaranteedDropGroups.Clear();
        this._chanceDropGroups.Clear();
        return (droppedItems ?? Enumerable.Empty<Item>(), money > 0 ? money : null);
    }

    /// <inheritdoc/>
    public Item? GenerateItemDrop(DropItemGroup selectedGroup, Player? player = null)
    {
        return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems, BuildCandidateClasses(player));
    }

    /// <inheritdoc/>
    public (Item? Item, uint? Money, ItemDropEffect DropEffect) GenerateItemDrop(IEnumerable<DropItemGroup> groups, Player? player = null)
    {
        var group = this.SelectRandomGroup(groups.OrderBy(group => group.Chance), 1.0);
        if (group is null)
        {
            return (null, null, ItemDropEffect.Undefined);
        }

        var dropEffect = ItemDropEffect.Undefined;
        if (group is ItemDropItemGroup itemDropItemGroup)
        {
            dropEffect = itemDropItemGroup.DropEffect;

            if (group.ItemType == SpecialItemType.Money)
            {
                return (null, (uint)itemDropItemGroup.MoneyAmount, dropEffect);
            }
        }

        return (this.GenerateItemDrop(group, group.PossibleItems, BuildCandidateClasses(player)), null, dropEffect);
    }

    /// <summary>
    /// Gets a random item.
    /// </summary>
    /// <param name="monsterLevel">The monster level.</param>
    /// <param name="isSocketItem">If set to <c>true</c>, it selects only socket items.</param>
    /// <param name="candidateClasses">The classes eligible to receive the drop, or <see langword="null"/> for a class-blind pick.</param>
    /// <returns>A random item.</returns>
    protected Item? GenerateRandomItem(int monsterLevel, bool isSocketItem, ISet<CharacterClass>? candidateClasses = null)
    {
        var possible = this.GetPossibleList(monsterLevel, isSocketItem);
        var item = this.GenerateRandomItem(possible, candidateClasses);
        if (item is null)
        {
            return null;
        }

        item.Level = GetItemLevelByMonsterLevel(item.Definition!, monsterLevel);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Applies random options to the item.
    /// </summary>
    /// <param name="item">The item.</param>
    protected void ApplyRandomOptions(Item item)
    {
        foreach (var option in item.Definition!.PossibleItemOptions.Where(o =>
            o.AddsRandomly &&
            !o.PossibleOptions.Any(po => object.Equals(po.OptionType, ItemOptionTypes.Excellent))))
        {
            this.ApplyOption(item, option);
        }

        if (item.Definition.MaximumSockets > 0)
        {
            item.SocketCount = this._randomizer.NextInt(1, item.Definition.MaximumSockets + 1);
        }

        if (item.CanHaveSkill())
        {
            item.HasSkill = this._randomizer.NextRandomBool(SkillDropChancePercent);
        }
    }

    /// <summary>
    /// Gets a random excellent item.
    /// </summary>
    /// <param name="monsterLevel">The monster level, if it's a monster drop.</param>
    /// <param name="possibleItems">The possible items, if the drop is from an item box (e.g. box of kundun).</param>
    /// <param name="candidateClasses">The classes eligible to receive the drop, or <see langword="null"/> for a class-blind pick.</param>
    /// <returns>A random excellent item.</returns>
    protected Item? GenerateRandomExcellentItem(int monsterLevel = 0, ICollection<ItemDefinition>? possibleItems = null, ISet<CharacterClass>? candidateClasses = null)
    {
        if (monsterLevel < this._excellentItemDropLevelDelta && possibleItems is null)
        {
            return null;
        }

        var possible = possibleItems ?? this.GetPossibleList(monsterLevel - this._excellentItemDropLevelDelta);
        var item = this.GenerateRandomItem(possible, candidateClasses);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every excellent item got skill

        this.AddRandomExcOptions(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    /// <summary>
    /// Gets a random ancient item.
    /// </summary>
    /// <param name="candidateClasses">The classes eligible to receive the drop, or <see langword="null"/> for a class-blind pick.</param>
    /// <returns>A random ancient item.</returns>
    protected Item? GenerateRandomAncient(ISet<CharacterClass>? candidateClasses = null)
    {
        var item = this.GenerateRandomItem(this._ancientItems, candidateClasses);
        if (item is null)
        {
            return null;
        }

        item.HasSkill = item.CanHaveSkill(); // every ancient item got skill

        this.ApplyRandomAncientOption(item);
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    private static byte GetItemLevelByMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        return Math.Min((byte)((monsterLevel - itemDefinition.DropLevel) / 3), itemDefinition.MaximumItemLevel);
    }

    private static async ValueTask<IEnumerable<DropItemGroup>> GetQuestItemGroupsAsync(Player player)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return [];
        }

        if (player.Party is { } party)
        {
            return await party.GetQuestDropItemGroupsAsync(player).ConfigureAwait(false);
        }

        return character.GetQuestDropItemGroups();
    }

    private static bool IsGroupRelevant(MonsterDefinition monsterDefinition, DropItemGroup group)
    {
        if (group.MinimumMonsterLevel.HasValue && monsterDefinition[Stats.Level] < group.MinimumMonsterLevel)
        {
            return false;
        }

        if (group.MaximumMonsterLevel.HasValue && monsterDefinition[Stats.Level] > group.MaximumMonsterLevel)
        {
            return false;
        }

        if (group.Monster is { } monster && !monster.Equals(monsterDefinition))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidOptionLevelDrop(byte value)
        => value is >= MinItemOptionLevelDrop and <= MaxItemOptionLevelDrop;

    private static bool CanDropAtMonsterLevel(ItemDefinition itemDefinition, int monsterLevel)
    {
        if (itemDefinition.DropLevel > monsterLevel)
        {
            return false;
        }

        return itemDefinition.MaximumDropLevel is not { } maxDropLevel || monsterLevel <= maxDropLevel;
    }

    private (IList<Item>? Items, uint Money) GenerateDrops(MonsterDefinition monster, int gainedExperience, ISet<CharacterClass>? candidateClasses)
    {
        uint money = 0;
        List<Item>? droppedItems = null;
        var remainingDrops = monster.NumberOfMaximumItemDrops;

        // Guaranteed groups.
        foreach (var group in this._guaranteedDropGroups)
        {
            if (remainingDrops <= 0)
            {
                break;
            }

            var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, candidateClasses, out var droppedMoney);
            if (item is not null)
            {
                droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                droppedItems.Add(item);
            }

            if (droppedMoney is not null)
            {
                money += droppedMoney.Value;
            }

            remainingDrops--;
        }

        // Chance based groups.
        if (remainingDrops > 0 && this._chanceDropGroups.Count > 0)
        {
            double totalChance = 0;
            foreach (var group in this._chanceDropGroups)
            {
                totalChance += group.Chance;
            }

            for (int i = 0; i < remainingDrops; i++)
            {
                var group = this.SelectRandomGroup(this._chanceDropGroups, totalChance);
                if (group is null)
                {
                    continue;
                }

                var item = this.GenerateItemDropOrMoney(monster, group, gainedExperience, candidateClasses, out var droppedMoney);
                if (item is not null)
                {
                    droppedItems ??= new List<Item>(monster.NumberOfMaximumItemDrops);
                    droppedItems.Add(item);
                }

                if (droppedMoney is not null)
                {
                    money += droppedMoney.Value;
                }
            }
        }

        return (droppedItems, money);
    }

    private void PartitionDropGroups(IEnumerable<DropItemGroup> groups, MonsterDefinition? monster = null)
    {
        foreach (var group in groups)
        {
            if (monster is not null && !IsGroupRelevant(monster, group))
            {
                continue;
            }

            if (group.Chance >= 1.0)
            {
                this._guaranteedDropGroups.Add(group);
            }
            else
            {
                this._chanceDropGroups.Add(group);
            }
        }
    }

    private Item? GenerateItemDrop(DropItemGroup selectedGroup, ICollection<ItemDefinition> possibleItems, ISet<CharacterClass>? candidateClasses)
    {
        var item = selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(candidateClasses),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(possibleItems: possibleItems, candidateClasses: candidateClasses),
            _ => this.GenerateRandomItem(possibleItems, candidateClasses),
        };

        if (item is null)
        {
            return null;
        }

        if (item.Durability == 0)
        {
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
        }

        if (selectedGroup is ItemDropItemGroup itemDropItemGroup)
        {
            item.Level = (byte)this._randomizer.NextInt(itemDropItemGroup.MinimumLevel, itemDropItemGroup.MaximumLevel + 1);
        }
        else if (selectedGroup.ItemLevel is { } itemLevel)
        {
            item.Level = itemLevel;
        }
        else
        {
            // no level defined, so it stays at 0.
        }

        item.Level = Math.Min(item.Level, item.Definition!.MaximumItemLevel);

        return item;
    }

    private void ApplyOption(Item item, ItemOptionDefinition option)
    {
        for (int i = 0; i < option.MaximumOptionsPerItem; i++)
        {
            if (this._randomizer.NextRandomBool(option.AddChance))
            {
                var remainingOptions = option.PossibleOptions.Where(possibleOption => item.ItemOptions.All(link => link.ItemOption != possibleOption));
                var newOption = remainingOptions.SelectRandom(this._randomizer);
                if (newOption is null)
                {
                    break;
                }

                var itemOptionLink = new ItemOptionLink
                {
                    ItemOption = newOption,
                    Level = newOption.LevelDependentOptions
                        .Select(ldo => ldo.Level)
                        .Concat(newOption.LevelDependentOptions.Count > 0 ? [1] : []) // For base def/dmg opts level 1 is not an ItemOptionOfLevel entry
                        .Distinct()
                        .Where(l => l <= this._maxItemOptionLevelDrop)
                        .DefaultIfEmpty(0)
                        .SelectRandom(),
                };
                item.ItemOptions.Add(itemOptionLink);
            }
        }
    }

    private Item? GenerateRandomItem(ICollection<ItemDefinition>? possibleItems, ISet<CharacterClass>? candidateClasses = null)
    {
        if (possibleItems is null || possibleItems.Count == 0)
        {
            return null;
        }

        var item = new TemporaryItem
        {
            Definition = this.SelectItemDefinition(possibleItems, candidateClasses),
        };

        this.ApplyRandomOptions(item);

        return item;
    }

    /// <summary>
    /// Picks one definition from <paramref name="possibleItems"/>. With <see cref="ClassAwareDropMode.Off"/>,
    /// or without a class set, this is a uniform pick identical to the pre-existing behavior. Neither list
    /// passed in nor the cached lists it may originate from (<see cref="GetPossibleList"/>, <see cref="_ancientItems"/>)
    /// are mutated; a filtered subset is always a new list.
    /// </summary>
    private ItemDefinition SelectItemDefinition(ICollection<ItemDefinition> possibleItems, ISet<CharacterClass>? candidateClasses)
    {
        if (this._classAwareDropMode == ClassAwareDropMode.Off || candidateClasses is not { Count: > 0 })
        {
            return possibleItems.ElementAt(this._randomizer.NextInt(0, possibleItems.Count));
        }

        if (this._classAwareDropMode == ClassAwareDropMode.Only)
        {
            var usableItems = possibleItems.Where(item => IsUsableByAny(item, candidateClasses)).ToList();
            var usableFrom = usableItems.Count > 0 ? usableItems : possibleItems;
            return usableFrom.ElementAt(this._randomizer.NextInt(0, usableFrom.Count));
        }

        return this.SelectWeightedItemDefinition(possibleItems, candidateClasses);
    }

    /// <summary>
    /// Weighted pick for <see cref="ClassAwareDropMode.Prefer"/>: a usable item weighs <see cref="_classAwareDropWeight"/>,
    /// an off-class item weighs 1. Mirrors the threshold-walk of <see cref="SelectRandomGroup"/>, drawing a single
    /// <see cref="IRandomizer.NextDouble"/> value against the summed weight.
    /// </summary>
    private ItemDefinition SelectWeightedItemDefinition(ICollection<ItemDefinition> possibleItems, ISet<CharacterClass> candidateClasses)
    {
        double totalWeight = 0;
        foreach (var item in possibleItems)
        {
            totalWeight += IsUsableByAny(item, candidateClasses) ? this._classAwareDropWeight : 1.0;
        }

        var remainingWeight = this._randomizer.NextDouble() * totalWeight;
        ItemDefinition? lastItem = null;
        foreach (var item in possibleItems)
        {
            lastItem = item;
            remainingWeight -= IsUsableByAny(item, candidateClasses) ? this._classAwareDropWeight : 1.0;
            if (remainingWeight <= 0)
            {
                return item;
            }
        }

        // Floating-point rounding may leave a small remainder; fall back to the last item instead of throwing.
        return lastItem!;
    }

    /// <summary>
    /// An item is usable by the candidate set when it has no <see cref="ItemDefinition.QualifiedCharacters"/>
    /// (a class-neutral item, e.g. rings or potions) or when that list contains one of the candidate classes.
    /// </summary>
    private static bool IsUsableByAny(ItemDefinition item, ISet<CharacterClass> candidateClasses)
    {
        var qualifiedCharacters = item.QualifiedCharacters;
        if (qualifiedCharacters is null || qualifiedCharacters.Count == 0)
        {
            return true;
        }

        foreach (var characterClass in qualifiedCharacters)
        {
            if (candidateClasses.Contains(characterClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the candidate class set for a kill: every class among the killer's party (the drop is owned by
    /// the whole party list, see <see cref="NPC.AttackableNpcBase"/>), or the killer's own class solo.
    /// </summary>
    private static ISet<CharacterClass>? BuildPartyCandidateClasses(Player player)
    {
        if (player.SelectedCharacter?.CharacterClass is not { } ownClass)
        {
            return null;
        }

        if (player.Party is not { } party)
        {
            return new HashSet<CharacterClass> { ownClass };
        }

        var classes = new HashSet<CharacterClass>();
        foreach (var member in party.PartyList.OfType<Player>())
        {
            if (member.SelectedCharacter?.CharacterClass is { } memberClass)
            {
                classes.Add(memberClass);
            }
        }

        return classes.Count > 0 ? classes : new HashSet<CharacterClass> { ownClass };
    }

    /// <summary>
    /// Builds the candidate class set for a box, event reward or item registration drop: the receiving
    /// player's own class only, since that player alone owns the item.
    /// </summary>
    private static ISet<CharacterClass>? BuildCandidateClasses(Player? player)
    {
        if (player?.SelectedCharacter?.CharacterClass is not { } characterClass)
        {
            return null;
        }

        return new HashSet<CharacterClass> { characterClass };
    }

    private void ApplyRandomAncientOption(Item item)
    {
        var ancientSet = item.Definition?.PossibleItemSetGroups
            .Where(g => g!.Options?.PossibleOptions.Any(o => object.Equals(o.OptionType, ItemOptionTypes.AncientOption)) ?? false)
            .SelectRandom(this._randomizer);
        if (ancientSet is null)
        {
            return;
        }

        var itemOfSet = ancientSet.Items.First(i => object.Equals(i.ItemDefinition, item.Definition));
        item.ItemSetGroups.Add(itemOfSet);

        // For example: +5str or +10str.
        if (itemOfSet.BonusOption is { } bonusOption)
        {
            var bonusOptionLink = new ItemOptionLink();
            bonusOptionLink.ItemOption = bonusOption;
            bonusOptionLink.Level = bonusOption.LevelDependentOptions.Select(o => o.Level).SelectRandom();
            item.ItemOptions.Add(bonusOptionLink);
        }
    }

    private void AddRandomExcOptions(Item item)
    {
        var excellentOptions = item.Definition!.PossibleItemOptions.FirstOrDefault(
            o => o.PossibleOptions.Any(p => object.Equals(p.OptionType, ItemOptionTypes.Excellent)));

        if (excellentOptions is null)
        {
            return;
        }

        var existingOptionCount = item.ItemOptions.Count(o => object.Equals(o.ItemOption?.OptionType, ItemOptionTypes.Excellent));

        for (int i = existingOptionCount; i < excellentOptions.MaximumOptionsPerItem; i++)
        {
            if (i == 0)
            {
                // The first option is always added without a chance
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                    existingOptionCount++;
                }

                continue;
            }

            if (this._randomizer.NextRandomBool(excellentOptions.AddChance))
            {
                var newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                while (item.ItemOptions.Any(o => object.Equals(o.ItemOption, newOption)))
                {
                    newOption = excellentOptions.PossibleOptions.SelectRandom(this._randomizer);
                }

                if (newOption is not null)
                {
                    item.ItemOptions.Add(new ItemOptionLink { ItemOption = newOption });
                }
            }
        }
    }

    private Item? GenerateItemDropOrMoney(MonsterDefinition monster, DropItemGroup selectedGroup, int gainedExperience, ISet<CharacterClass>? candidateClasses, out uint? droppedMoney)
    {
        droppedMoney = null;

        if (selectedGroup.PossibleItems?.Count > 0)
        {
            return this.GenerateItemFromGroup(monster, selectedGroup, candidateClasses);
        }

        var item = this.GenerateSpecialItem(monster, selectedGroup, candidateClasses);
        if (item is null && selectedGroup.ItemType == SpecialItemType.Money)
        {
            droppedMoney = (uint)(gainedExperience + BaseMoneyDrop);
        }

        return item;
    }

    private Item? GenerateItemFromGroup(MonsterDefinition monster, DropItemGroup selectedGroup, ISet<CharacterClass>? candidateClasses)
    {
        var isDropSpecificForMonster = monster.DropItemGroups.Contains(selectedGroup);
        if (isDropSpecificForMonster)
        {
            return this.GenerateItemDrop(selectedGroup, selectedGroup.PossibleItems!, candidateClasses);
        }

        var monsterLevel = (int)monster[Stats.Level];
        var isJewel = selectedGroup.ItemType == SpecialItemType.Jewel;

        var filteredPossibleItems = selectedGroup.PossibleItems!
            .Where(it => CanDropAtMonsterLevel(it, monsterLevel)
                         && (isJewel || it.DropLevel == 0 || it.DropLevel > monsterLevel - DropLevelMaxGap))
            .ToList();

        return this.GenerateItemDrop(selectedGroup, filteredPossibleItems, candidateClasses);
    }

    private Item? GenerateSpecialItem(MonsterDefinition monster, DropItemGroup selectedGroup, ISet<CharacterClass>? candidateClasses)
    {
        var monsterLevel = (int)monster[Stats.Level];
        return selectedGroup.ItemType switch
        {
            SpecialItemType.Ancient => this.GenerateRandomAncient(candidateClasses),
            SpecialItemType.Excellent => this.GenerateRandomExcellentItem(monsterLevel, candidateClasses: candidateClasses),
            SpecialItemType.RandomItem => this.GenerateRandomItem(monsterLevel, false, candidateClasses),
            SpecialItemType.SocketItem => this.GenerateRandomItem(monsterLevel, true, candidateClasses),
            _ => null,
        };
    }

    private DropItemGroup? SelectRandomGroup(IEnumerable<DropItemGroup> groups, double totalChance)
    {
        var remainingThreshold = this._randomizer.NextDouble();
        if (totalChance > 1.0)
        {
            remainingThreshold *= totalChance;
        }

        foreach (var group in groups)
        {
            if (remainingThreshold > group.Chance)
            {
                remainingThreshold -= group.Chance;
            }
            else
            {
                return group;
            }
        }

        return null;
    }

    private IList<ItemDefinition>? GetPossibleList(int monsterLevel, bool isSocketItem = false)
    {
        if (monsterLevel is < byte.MinValue or > byte.MaxValue)
        {
            return null;
        }

        var cache = isSocketItem ? this._droppableSocketItemsPerMonsterLevel : this._droppableItemsPerMonsterLevel;
        return cache[monsterLevel]
            ??= (from it in this._droppableItems
                 where CanDropAtMonsterLevel(it, monsterLevel)
                       && (it.DropLevel > monsterLevel - DropLevelMaxGap)
                       && (!isSocketItem || it.MaximumSockets > 0)
                 select it).ToList();
    }
}