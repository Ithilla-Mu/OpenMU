// <copyright file="QuestMasterText.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.QuestMaster;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Quest;

/// <summary>
/// Builds the texts of the quest master chain from <see cref="QuestMasterConfiguration"/>. Shared by the
/// NPC plugin and by the view which lists the active quests, so the T window and the NPC agree.
/// </summary>
public static class QuestMasterText
{
    /// <summary>
    /// Gets the configuration of the active <see cref="QuestMasterNpcPlugIn"/>, or the defaults when it is inactive or unconfigured.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The configuration.</returns>
    public static QuestMasterConfiguration GetConfiguration(Player player)
    {
        return player.GameContext.PlugInManager.GetActivePlugInsOf<IPlayerTalkToNpcPlugIn>()
                   .OfType<QuestMasterNpcPlugIn>()
                   .FirstOrDefault()?.Configuration
               ?? new QuestMasterConfiguration();
    }

    /// <summary>
    /// Finds the map on which the monster has the most automatically spawning monsters (sum of quantities);
    /// ties go to the lowest map number. Mini-game maps (the map of a mini game's entrance gate) are ignored,
    /// so the text never names an event map such as a Devil Square.
    /// </summary>
    /// <param name="maps">The maps.</param>
    /// <param name="monster">The monster.</param>
    /// <param name="miniGames">The mini games whose maps are ignored; <c>null</c> ignores none.</param>
    /// <returns>The map, or <c>null</c> if the monster does not spawn automatically anywhere else.</returns>
    public static GameMapDefinition? FindMainMap(IEnumerable<GameMapDefinition> maps, MonsterDefinition monster, IEnumerable<MiniGameDefinition>? miniGames = null)
    {
        var miniGameMaps = (miniGames ?? Enumerable.Empty<MiniGameDefinition>()).Select(g => g.Entrance?.Map).OfType<GameMapDefinition>().ToList();
        return maps
            .Where(map => !miniGameMaps.Contains(map))
            .Select(map => (Map: map, Count: map.MonsterSpawns
                .Where(s => s.SpawnTrigger == SpawnTrigger.Automatic && object.Equals(s.MonsterDefinition, monster))
                .Sum(s => (int)s.Quantity)))
            .Where(t => t.Count > 0)
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Map.Number)
            .Select(t => t.Map)
            .FirstOrDefault();
    }

    /// <summary>
    /// Builds the title of the quest for the quest window.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    /// <returns>The title.</returns>
    public static string BuildTitle(Player player, QuestDefinition quest)
    {
        var monster = quest.RequiredMonsterKills.FirstOrDefault() is { } first ? MonsterName(player, first) : string.Empty;
        return string.Format(GetConfiguration(player).QuestTitle, quest.Number, monster);
    }

    /// <summary>
    /// Builds the summary of the quest for the quest window.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    /// <returns>The summary.</returns>
    public static string BuildSummary(Player player, QuestDefinition quest)
    {
        var template = GetConfiguration(player).QuestSummary;
        return string.Join(' ', quest.RequiredMonsterKills.Select(k => string.Format(template, k.MinimumNumber, MonsterName(player, k), MapName(player, k))));
    }

    /// <summary>
    /// Sends the title and summary of the quest to the client, which it needs before it can list the quest.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="quest">The quest.</param>
    /// <returns>The task.</returns>
    public static ValueTask SendQuestTextAsync(Player player, QuestDefinition quest)
    {
        var title = BuildTitle(player, quest);
        var summary = BuildSummary(player, quest);
        return player.InvokeViewPlugInAsync<IQuestTextPlugIn>(p => p.ShowQuestTextAsync(quest.Group, quest.Number, title, summary));
    }

    /// <summary>
    /// Gets the designation of the monster of the requirement.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The monster name.</returns>
    public static string MonsterName(Player player, QuestMonsterKillRequirement requirement)
    {
        return requirement.Monster?.Designation.GetTranslation(player.Culture) ?? string.Empty;
    }

    /// <summary>
    /// Gets the name of the map where the monster of the requirement is mainly found.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requirement">The requirement.</param>
    /// <returns>The map name.</returns>
    public static string MapName(Player player, QuestMonsterKillRequirement requirement)
    {
        if (requirement.Monster is not { } monster)
        {
            return string.Empty;
        }

        return FindMainMap(player.GameContext.Configuration.Maps, monster, player.GameContext.Configuration.MiniGameDefinitions)?.Name.GetTranslation(player.Culture) ?? string.Empty;
    }

    /// <summary>
    /// Gets the kill count of a requirement in the quest state; 0 before the first kill.
    /// </summary>
    /// <param name="state">The quest state.</param>
    /// <param name="requirement">The kill requirement.</param>
    /// <returns>The kill count.</returns>
    public static int KillCount(CharacterQuestState state, QuestMonsterKillRequirement requirement)
    {
        return state.RequirementStates.FirstOrDefault(r => object.Equals(r.Requirement, requirement))?.KillCount ?? 0;
    }

    /// <summary>
    /// Determines whether every kill requirement of the quest is met in the quest state.
    /// </summary>
    /// <param name="state">The quest state.</param>
    /// <param name="quest">The quest.</param>
    /// <returns><c>true</c> when all kill requirements are met.</returns>
    public static bool AreKillsMet(CharacterQuestState state, QuestDefinition quest)
    {
        return quest.RequiredMonsterKills.All(k => KillCount(state, k) >= k.MinimumNumber);
    }

    /// <summary>
    /// Formats a reward for a chat line.
    /// </summary>
    /// <param name="reward">The reward.</param>
    /// <returns>The description.</returns>
    public static string DescribeReward(QuestReward reward)
    {
        return reward.RewardType switch
        {
            QuestRewardType.Item when reward.ItemReward is { Definition: { } definition } item
                => item.Level > 0 ? $"{definition.GetNameForLevel(item.Level)} +{item.Level}" : definition.GetNameForLevel(item.Level),
            QuestRewardType.Money => $"{reward.Value} Zen",
            QuestRewardType.Experience => $"{reward.Value} Experience",
            _ => $"{reward.RewardType} {reward.Value}",
        };
    }
}
