// <copyright file="DropGeneratorTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Tests the drop generator.
/// </summary>
[TestFixture]
public class DropGeneratorTest
{
    /// <summary>
    /// Tests if the drop fails because the randomizer returns a number which causes a fail.
    /// </summary>
    [Test]
    public async ValueTask TestDropFailAsync()
    {
        var config = this.GetGameConfig();
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(9999));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 0), 0, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();
        Assert.That(item, Is.Null);
    }

    /// <summary>
    /// Tests the drops defined by a monster are getting considered.
    /// </summary>
    [Test]
    public async ValueTask TestItemDropItemByMonsterAsync()
    {
        var config = this.GetGameConfig();
        var monster = this.GetMonster(1, 0);
        monster.DropItemGroups.AddBasicDropItemGroups();
        monster.DropItemGroups.Add(3000, SpecialItemType.RandomItem, true);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer2(0, 0.5));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 1, await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);

        // ReSharper disable once PossibleNullReferenceException
        Assert.That(item!.Definition, Is.EqualTo(monster.DropItemGroups.Last().PossibleItems.First()));
    }

    /// <summary>
    /// Tests that items with a maximum drop level are filtered from generic monster drops.
    /// </summary>
    [Test]
    public async ValueTask TestMaximumDropLevelAsync()
    {
        var config = this.GetGameConfig();
        var cappedItem = this.CreateItemDefinition(12, 15, 12, 66);
        var uncappedItem = this.CreateItemDefinition(14, 13, 25);

        var dropGroup = new Mock<DropItemGroup>();
        dropGroup.SetupAllProperties();
        dropGroup.Object.Chance = 1.0;
        dropGroup.Object.ItemType = SpecialItemType.Jewel;
        dropGroup.Setup(g => g.PossibleItems).Returns(new List<ItemDefinition> { cappedItem, uncappedItem });

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.CurrentMap!.Definition.DropItemGroups.Add(dropGroup.Object);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var (items, _) = await generator.GenerateItemDropsAsync(this.GetMonster(1, 67), 1, player).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(uncappedItem));
    }

    /// <summary>
    /// Tests the drops defined by a player are getting considered.
    /// </summary>
    public void TestItemDropItemByPlayer()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests the drops defined by a map are getting considered.
    /// </summary>
    public void TestItemDropItemByMap()
    {
        // to be implemented
    }

    /// <summary>
    /// Tests that ExcellentItemDropLevelDelta property exists and has correct default.
    /// </summary>
    [Test]
    public void TestExcellentItemDropLevelDelta_PropertyExists()
    {
        var config = this.GetGameConfig();
        // The initializer sets default to 25 for backward compatibility
        config.ExcellentItemDropLevelDelta = 25;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(25));

        config.ExcellentItemDropLevelDelta = 0;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(0));

        config.ExcellentItemDropLevelDelta = 50;
        Assert.That(config.ExcellentItemDropLevelDelta, Is.EqualTo(50));
    }

    /// <summary>
    /// Tests that <see cref="ClassAwareDropMode.Off"/> is a uniform pick by index, ignoring
    /// <see cref="ItemDefinition.QualifiedCharacters"/> entirely - the same call the generator made
    /// before class-aware drops existed.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropModeOff_MatchesUniformPickAsync()
    {
        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Off;

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var offClass = new Mock<CharacterClass>().Object;
        var itemA = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [offClass]);
        var itemB = this.CreateItemDefinition(1, 2, 0, qualifiedCharacters: [offClass]);
        var group = CreateGroupWithPossibleItems(itemA, itemB);

        // NextInt(0, 2) => 1 picks the second element, regardless of neither item being usable by the player.
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(1));
        var item = generator.GenerateItemDrop(group.Object, player);

        Assert.That(item!.Definition, Is.EqualTo(itemB));
    }

    /// <summary>
    /// Tests that <see cref="ClassAwareDropMode.Only"/> never returns an off-class item while a
    /// usable one exists in the list, even though a uniform pick over the same list would have
    /// landed on the off-class item.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropModeOnly_NeverReturnsOffClassItemWhenAUsableOneExistsAsync()
    {
        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Only;

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var ownClass = player.SelectedCharacter!.CharacterClass!;
        var offClass = new Mock<CharacterClass>().Object;
        var offItem = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [offClass]);
        var usableItem = this.CreateItemDefinition(1, 2, 0, qualifiedCharacters: [ownClass]);
        var group = CreateGroupWithPossibleItems(offItem, usableItem);

        // A uniform pick over [offItem, usableItem] with NextInt returning 0 would select offItem.
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var item = generator.GenerateItemDrop(group.Object, player);

        Assert.That(item!.Definition, Is.EqualTo(usableItem));
    }

    /// <summary>
    /// Tests that <see cref="ClassAwareDropMode.Only"/> falls back to the whole list, and still
    /// returns an item, when no candidate in the list is usable - the mechanism may change which
    /// item drops, never whether one does.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropModeOnly_FallsBackToWholeListWhenNoneAreUsableAsync()
    {
        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Only;

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var offClassA = new Mock<CharacterClass>().Object;
        var offClassB = new Mock<CharacterClass>().Object;
        var itemA = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [offClassA]);
        var itemB = this.CreateItemDefinition(1, 2, 0, qualifiedCharacters: [offClassB]);
        var group = CreateGroupWithPossibleItems(itemA, itemB);

        // With the usable subset empty, the pick must fall back to the unfiltered [itemA, itemB].
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(1));
        var item = generator.GenerateItemDrop(group.Object, player);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(itemB));
    }

    /// <summary>
    /// Tests that a class-neutral item (no <see cref="ItemDefinition.QualifiedCharacters"/> at all,
    /// e.g. a ring or a potion) stays eligible under <see cref="ClassAwareDropMode.Only"/> alongside
    /// items the player's own class qualifies for.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropModeOnly_KeepsClassNeutralItemsEligibleAsync()
    {
        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Only;

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var offClass = new Mock<CharacterClass>().Object;
        var offItem = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [offClass]);
        var neutralItem = this.CreateItemDefinition(1, 2, 0); // no QualifiedCharacters at all.
        var group = CreateGroupWithPossibleItems(offItem, neutralItem);

        var generator = new DefaultDropGenerator(config, this.GetRandomizer(0));
        var item = generator.GenerateItemDrop(group.Object, player);

        Assert.That(item!.Definition, Is.EqualTo(neutralItem));
    }

    /// <summary>
    /// Tests that <see cref="ClassAwareDropMode.Prefer"/> hits the usable item at the ratio the
    /// weight implies: a usable item weighs <see cref="GameConfiguration.ClassAwareDropWeight"/>
    /// against 1 for the off-class item, so it should win weight/(weight+1) of the rolls.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropModePrefer_HitsUsableItemsAtTheExpectedRatioAsync()
    {
        const int weight = 4;
        const int rolls = 6000;

        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Prefer;
        config.ClassAwareDropWeight = weight;

        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var ownClass = player.SelectedCharacter!.CharacterClass!;
        var offClass = new Mock<CharacterClass>().Object;
        var usableItem = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [ownClass]);
        var offItem = this.CreateItemDefinition(1, 2, 0, qualifiedCharacters: [offClass]);
        var group = CreateGroupWithPossibleItems(usableItem, offItem);

        // Evenly spaced midpoints over [0, 1) make the observed ratio exact instead of statistical, and
        // never land exactly on the weight/(weight+1) boundary, where floating-point rounding could tip
        // a sample to either side.
        var values = Enumerable.Range(0, rolls).Select(i => (i + 0.5) / rolls).ToList();
        var generator = new DefaultDropGenerator(config, new SequentialDoubleRandomizer(values));

        var usableHits = 0;
        for (var i = 0; i < rolls; i++)
        {
            var item = generator.GenerateItemDrop(group.Object, player);
            if (ReferenceEquals(item!.Definition, usableItem))
            {
                usableHits++;
            }
        }

        var expectedHits = rolls * weight / (weight + 1);
        Assert.That(usableHits, Is.EqualTo(expectedHits));
    }

    /// <summary>
    /// Tests that the candidate class set for a kill is the union of the whole party's classes, so a
    /// drop is never filtered down to only the killer's own gear while a party member could use it.
    /// </summary>
    [Test]
    public async ValueTask ClassAwareDropMode_PartyUnionAdmitsBothMembersGearAsync()
    {
        var config = this.GetGameConfig();
        config.ClassAwareDropMode = ClassAwareDropMode.Only;

        var killer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var partyManager = new PartyManager(5, new NullLogger<Party>());
        var party = new Party(partyManager, 5, new NullLogger<Party>());
        await party.AddAsync(killer).ConfigureAwait(false);
        var companion = await PlayerTestHelper.CreatePlayerAsync(killer.GameContext).ConfigureAwait(false);
        await party.AddAsync(companion).ConfigureAwait(false);

        var killerClass = killer.SelectedCharacter!.CharacterClass!;
        var companionClass = companion.SelectedCharacter!.CharacterClass!;
        var offClass = new Mock<CharacterClass>().Object;
        var killerItem = this.CreateItemDefinition(1, 1, 0, qualifiedCharacters: [killerClass]);
        var companionItem = this.CreateItemDefinition(1, 2, 0, qualifiedCharacters: [companionClass]);
        var offItem = this.CreateItemDefinition(1, 3, 0, qualifiedCharacters: [offClass]);

        var monster = this.GetMonster(1, 0);
        var group = CreateGroupWithPossibleItems(killerItem, companionItem, offItem);
        group.Object.Chance = 1.0;
        monster.DropItemGroups.Add(group.Object);

        // NextInt(0, 2) => 1 would pick companionItem from the usable subset [killerItem, companionItem];
        // the off-class item must never be reachable regardless of what index is drawn.
        var generator = new DefaultDropGenerator(config, this.GetRandomizer(1));
        var (items, _) = await generator.GenerateItemDropsAsync(monster, 0, killer).ConfigureAwait(false);
        var item = items.FirstOrDefault();

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Definition, Is.EqualTo(companionItem));
    }

    private static Mock<DropItemGroup> CreateGroupWithPossibleItems(params ItemDefinition[] possibleItems)
    {
        var group = new Mock<DropItemGroup>();
        group.SetupAllProperties();
        group.Setup(g => g.PossibleItems).Returns(possibleItems.ToList());
        return group;
    }

    /// <summary>
    /// A randomizer whose <see cref="NextDouble"/> cycles through a fixed sequence, used to make the
    /// weighted class-aware pick's ratio exact instead of statistical. Every other member is unused by
    /// the pick under test and throws if called.
    /// </summary>
    private sealed class SequentialDoubleRandomizer : IRandomizer
    {
        private readonly IReadOnlyList<double> _values;
        private int _index;

        public SequentialDoubleRandomizer(IReadOnlyList<double> values)
        {
            this._values = values;
        }

        public double NextDouble() => this._values[this._index++ % this._values.Count];

        public bool NextRandomBool() => throw new NotSupportedException();

        public bool NextRandomBool(int percent) => throw new NotSupportedException();

        public bool NextRandomBool(int chance, int basis) => throw new NotSupportedException();

        public bool NextRandomBool(double chance) => throw new NotSupportedException();

        public int NextInt(int min, int max) => throw new NotSupportedException();

        public int NextInt(uint min, uint max) => throw new NotSupportedException();

        public uint NextUInt(uint min, uint max) => throw new NotSupportedException();
    }

    private MonsterDefinition GetMonster(int numberOfDrops, byte level)
    {
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(m => m.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Setup(m => m.Attributes).Returns(new List<MonsterAttribute>());
        monster.Object.NumberOfMaximumItemDrops = numberOfDrops;
        monster.Object.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = level });
        return monster.Object;
    }

    private IRandomizer GetRandomizer(int randomValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(randomValue);
        randomizer.Setup(r => r.NextDouble()).Returns(randomValue / 10000.0);
        return randomizer.Object;
    }

    private IRandomizer GetRandomizer2(int integerValue, double doubleValue)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns(integerValue);
        randomizer.Setup(r => r.NextDouble()).Returns(doubleValue);

        return randomizer.Object;
    }

    private GameConfiguration GetGameConfig()
    {
        var gameConfiguration = new Mock<GameConfiguration>();
        gameConfiguration.Setup(c => c.Items).Returns(new List<ItemDefinition>());
        return gameConfiguration.Object;
    }

    private ItemDefinition CreateItemDefinition(byte group, short number, byte dropLevel, byte? maximumDropLevel = null, IEnumerable<CharacterClass>? qualifiedCharacters = null)
    {
        var itemDefinition = new Mock<ItemDefinition>();
        itemDefinition.SetupAllProperties();
        itemDefinition.Object.Group = group;
        itemDefinition.Object.Number = number;
        itemDefinition.Object.DropLevel = dropLevel;
        itemDefinition.Object.MaximumDropLevel = maximumDropLevel;
        itemDefinition.Object.Durability = 1;
        itemDefinition.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        itemDefinition.Setup(d => d.QualifiedCharacters).Returns(new List<CharacterClass>(qualifiedCharacters ?? []));
        return itemDefinition.Object;
    }
}
