// <copyright file="QuestMasterTextTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.QuestMaster;
using BasicItem = MUnique.OpenMU.Persistence.BasicModel.Item;
using BasicItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;
using BasicMap = MUnique.OpenMU.Persistence.BasicModel.GameMapDefinition;
using BasicMonster = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;
using BasicQuestReward = MUnique.OpenMU.Persistence.BasicModel.QuestReward;
using BasicSpawnArea = MUnique.OpenMU.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Tests for <see cref="QuestMasterText"/> and the default templates of <see cref="QuestMasterConfiguration"/>.
/// </summary>
[TestFixture]
public class QuestMasterTextTests
{
    /// <summary>
    /// The map with the highest summed quantity of automatic spawns wins.
    /// </summary>
    [Test]
    public void FindMainMapPicksHighestQuantity()
    {
        var monster = new BasicMonster { Id = Guid.NewGuid() };
        var maps = new[] { CreateMap(0, monster, 3), CreateMap(1, monster, 5, 4) };

        Assert.That(QuestMasterText.FindMainMap(maps, monster)?.Number, Is.EqualTo(1));
    }

    /// <summary>
    /// A tie goes to the lowest map number.
    /// </summary>
    [Test]
    public void FindMainMapTieGoesToLowestNumber()
    {
        var monster = new BasicMonster { Id = Guid.NewGuid() };
        var maps = new[] { CreateMap(4, monster, 5), CreateMap(2, monster, 5) };

        Assert.That(QuestMasterText.FindMainMap(maps, monster)?.Number, Is.EqualTo(2));
    }

    /// <summary>
    /// Non-automatic spawns and other monsters are ignored.
    /// </summary>
    [Test]
    public void FindMainMapIgnoresEventSpawnsAndOtherMonsters()
    {
        var monster = new BasicMonster { Id = Guid.NewGuid() };
        var map = CreateMap(0, new BasicMonster { Id = Guid.NewGuid() }, 9);
        var eventArea = new BasicSpawnArea { MonsterDefinition = monster, Quantity = 9, SpawnTrigger = SpawnTrigger.OnceAtEventStart };
        map.MonsterSpawns.Add(eventArea);

        Assert.That(QuestMasterText.FindMainMap(new[] { map }, monster), Is.Null);
    }

    /// <summary>
    /// The default templates substitute their documented placeholders.
    /// </summary>
    [Test]
    public void DefaultTemplatesSubstitutePlaceholders()
    {
        var configuration = new QuestMasterConfiguration();

        Assert.That(string.Format(configuration.QuestTitle, 2, "Spider"), Is.EqualTo("Quest 2: Spider"));
        Assert.That(string.Format(configuration.QuestSummary, 10, "Spider", "Lorencia"), Is.EqualTo("Kill 10 Spider in Lorencia."));
        Assert.That(string.Format(configuration.ProgressChat, 1, "Spider", 3, 10, "Lorencia"), Is.EqualTo("[Quest 1] Spider 3/10 — Lorencia"));
        Assert.That(string.Format(configuration.RewardChat, 1, "Box of Luck"), Is.EqualTo("[Quest 1] Reward: Box of Luck"));
    }

    /// <summary>
    /// Item rewards show the level only when it is above zero; money and experience are labelled.
    /// </summary>
    [Test]
    public void DescribeRewardFormatsByType()
    {
        var definition = new BasicItemDefinition { Name = "Box of Luck" };
        var level0 = new BasicQuestReward { RewardType = QuestRewardType.Item, ItemReward = new BasicItem { Definition = definition, Level = 0 } };
        var level2 = new BasicQuestReward { RewardType = QuestRewardType.Item, ItemReward = new BasicItem { Definition = definition, Level = 2 } };

        Assert.That(QuestMasterText.DescribeReward(level0), Is.EqualTo("Box of Luck"));
        Assert.That(QuestMasterText.DescribeReward(level2), Is.EqualTo("Box of Luck +2"));
        Assert.That(QuestMasterText.DescribeReward(new BasicQuestReward { RewardType = QuestRewardType.Money, Value = 500 }), Is.EqualTo("500 Zen"));
        Assert.That(QuestMasterText.DescribeReward(new BasicQuestReward { RewardType = QuestRewardType.Experience, Value = 7 }), Is.EqualTo("7 Experience"));
    }

    private static BasicMap CreateMap(short number, MonsterDefinition monster, params short[] quantities)
    {
        var map = new BasicMap { Number = number };
        foreach (var quantity in quantities)
        {
            map.MonsterSpawns.Add(new BasicSpawnArea { MonsterDefinition = monster, Quantity = quantity, SpawnTrigger = SpawnTrigger.Automatic });
        }

        return map;
    }
}
