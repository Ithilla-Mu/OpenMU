// <copyright file="QuestMasterNpcPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.QuestMaster;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Drives the linear kill quest chain of <see cref="QuestMasterConstants.QuestGroup"/> from the quest master NPC.
/// Talking to the NPC starts the next quest, reports progress, or turns in a finished quest; the quests appear in
/// the client's quest window through <see cref="IQuestTextPlugIn"/> and never open a client dialog.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestMasterNpcPlugIn_Name), Description = nameof(PlugInResources.QuestMasterNpcPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6C1E7A53-3B0D-4F2E-9A8B-5D2C41F7E9A1")]
public class QuestMasterNpcPlugIn : IPlayerTalkToNpcPlugIn, ISupportCustomConfiguration<QuestMasterConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public QuestMasterConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (npc.Definition.Number != QuestMasterConstants.NpcNumber)
        {
            return;
        }

        // TalkNpcAction.cs does not await this plug-in point and reads HasBeenHandled right after the call,
        // so it has to be set before the first await.
        eventArgs.HasBeenHandled = true;
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        var configuration = this.Configuration ??= CreateDefaultConfiguration();
        var state = character.QuestStates.FirstOrDefault(s => s.Group == QuestMasterConstants.QuestGroup);
        if (state?.ActiveQuest is { } active)
        {
            await this.TalkWithActiveQuestAsync(player, npc, state, active, configuration).ConfigureAwait(false);
            return;
        }

        var lastNumber = state?.LastFinishedQuest?.Number ?? 0;
        var next = npc.Definition.Quests
            .Where(q => q.Group == QuestMasterConstants.QuestGroup && q.Number > lastNumber)
            .Where(q => q.QualifiedCharacter is null || object.Equals(q.QualifiedCharacter, character.CharacterClass))
            .OrderBy(q => q.Number)
            .FirstOrDefault();
        if (next is null)
        {
            await SayAsync(player, npc, configuration.ChainCompleteBubble).ConfigureAwait(false);
            return;
        }

        await this.StartQuestAsync(player, npc, state, next, configuration).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public object CreateDefaultConfig()
    {
        return CreateDefaultConfiguration();
    }

    private static QuestMasterConfiguration CreateDefaultConfiguration()
    {
        return new QuestMasterConfiguration();
    }

    private static ValueTask SayAsync(Player player, NonPlayerCharacter npc, string text)
    {
        return player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(text, npc));
    }

    /// <summary>
    /// Mirrors <see cref="QuestCompletionAction.CompleteQuestAsync"/> for a quest whose kills are met, except that
    /// item rewards always drop at the player's feet, owned by the player, instead of going to the inventory.
    /// </summary>
    private static async ValueTask CompleteQuestAsync(Player player, CharacterQuestState state, QuestDefinition quest)
    {
        state.LastFinishedQuest = quest;
        foreach (var reward in quest.Rewards)
        {
            switch (reward.RewardType)
            {
                case QuestRewardType.Item:
                    if (reward.ItemReward is null || player.CurrentMap is not { } map)
                    {
                        player.Logger.LogWarning("Quest {0} reward {1} could not be dropped.", quest.Number, reward.GetId());
                        break;
                    }

                    var item = player.PersistenceContext.CreateNew<Item>();
                    item.AssignValues(reward.ItemReward);
                    await map.AddAsync(new DroppedItem(item, player.Position, map, player, player.GetAsEnumerable())).ConfigureAwait(false);
                    break;
                case QuestRewardType.Money:
                    player.TryAddMoney(reward.Value);
                    await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                    break;
                case QuestRewardType.Experience:
                    await player.AddExperienceAsync(reward.Value, null).ConfigureAwait(false);
                    break;
                default:
                    player.Logger.LogWarning("Quest {0} reward type {1} is not supported by the quest master.", quest.Number, reward.RewardType);
                    break;
            }
        }

        await state.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IQuestCompletionResponsePlugIn>(p => p.QuestCompletedAsync(quest)).ConfigureAwait(false);
    }

    private async ValueTask TalkWithActiveQuestAsync(Player player, NonPlayerCharacter npc, CharacterQuestState state, QuestDefinition quest, QuestMasterConfiguration configuration)
    {
        if (!QuestMasterText.AreKillsMet(state, quest))
        {
            await this.ReportProgressAsync(player, npc, state, quest, configuration).ConfigureAwait(false);
            return;
        }

        await CompleteQuestAsync(player, state, quest).ConfigureAwait(false);
        await SayAsync(player, npc, configuration.TurnInBubble).ConfigureAwait(false);
        foreach (var reward in quest.Rewards)
        {
            await player.ShowBlueMessageAsync(string.Format(configuration.RewardChat, quest.Number, QuestMasterText.DescribeReward(reward))).ConfigureAwait(false);
        }
    }

    private async ValueTask ReportProgressAsync(Player player, NonPlayerCharacter npc, CharacterQuestState state, QuestDefinition quest, QuestMasterConfiguration configuration)
    {
        await SayAsync(player, npc, configuration.ProgressBubble).ConfigureAwait(false);
        foreach (var kill in quest.RequiredMonsterKills)
        {
            var line = string.Format(
                configuration.ProgressChat,
                quest.Number,
                QuestMasterText.MonsterName(player, kill),
                QuestMasterText.KillCount(state, kill),
                kill.MinimumNumber,
                QuestMasterText.MapName(player, kill));
            await player.ShowBlueMessageAsync(line).ConfigureAwait(false);
        }
    }

    private async ValueTask StartQuestAsync(Player player, NonPlayerCharacter npc, CharacterQuestState? state, QuestDefinition quest, QuestMasterConfiguration configuration)
    {
        // Not QuestStartAction: it resolves the quest through player.OpenedNpc and raises the client's quest dialog.
        if (state is null)
        {
            state = player.PersistenceContext.CreateNew<CharacterQuestState>();
            state.Group = QuestMasterConstants.QuestGroup;
            player.SelectedCharacter!.QuestStates.Add(state);
        }

        await state.ClearAsync(player.PersistenceContext).ConfigureAwait(false);
        state.ActiveQuest = quest;

        await QuestMasterText.SendQuestTextAsync(player, quest).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<ICurrentlyActiveQuestsPlugIn>(p => p.ShowActiveQuestsAsync()).ConfigureAwait(false);

        var firstKill = quest.RequiredMonsterKills.FirstOrDefault();
        if (firstKill is not null)
        {
            await SayAsync(
                player,
                npc,
                string.Format(configuration.StartBubble, firstKill.MinimumNumber, QuestMasterText.MonsterName(player, firstKill), QuestMasterText.MapName(player, firstKill))).ConfigureAwait(false);
        }

        foreach (var kill in quest.RequiredMonsterKills)
        {
            var line = string.Format(configuration.StartChat, quest.Number, kill.MinimumNumber, QuestMasterText.MonsterName(player, kill), QuestMasterText.MapName(player, kill));
            await player.ShowBlueMessageAsync(line).ConfigureAwait(false);
        }
    }
}
