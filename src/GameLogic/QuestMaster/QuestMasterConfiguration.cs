// <copyright file="QuestMasterConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.QuestMaster;

/// <summary>
/// Configuration of the <see cref="QuestMasterNpcPlugIn"/>. All values are <see cref="string.Format(string, object[])"/>
/// templates; the placeholders are documented per property.
/// </summary>
public class QuestMasterConfiguration
{
    /// <summary>
    /// Gets or sets the quest title shown in the quest window. {0} = quest number, {1} = monster.
    /// </summary>
    [Display(Name = "Quest title")]
    public string QuestTitle { get; set; } = "Quest {0}: {1}";

    /// <summary>
    /// Gets or sets the sentence per kill requirement, joined into the quest summary. {0} = count, {1} = monster, {2} = map.
    /// </summary>
    [Display(Name = "Quest summary")]
    public string QuestSummary { get; set; } = "Kill {0} {1} in {2}.";

    /// <summary>
    /// Gets or sets the NPC speech bubble when a quest starts. {0} = count, {1} = monster, {2} = map.
    /// </summary>
    [Display(Name = "Start: NPC message")]
    public string StartBubble { get; set; } = "Kill {0} {1} in {2}.";

    /// <summary>
    /// Gets or sets the chat line when a quest starts, one per kill requirement. {0} = quest number, {1} = count, {2} = monster, {3} = map.
    /// </summary>
    [Display(Name = "Start: chat line")]
    public string StartChat { get; set; } = "[Quest {0}] Kill {1} {2} — {3}";

    /// <summary>
    /// Gets or sets the NPC speech bubble while the quest is unfinished. No placeholders.
    /// </summary>
    [Display(Name = "In progress: NPC message")]
    public string ProgressBubble { get; set; } = "Come back when you're done.";

    /// <summary>
    /// Gets or sets the chat line while the quest is unfinished, one per kill requirement.
    /// {0} = quest number, {1} = monster, {2} = current kills, {3} = required kills, {4} = map.
    /// </summary>
    [Display(Name = "In progress: chat line")]
    public string ProgressChat { get; set; } = "[Quest {0}] {1} {2}/{3} — {4}";

    /// <summary>
    /// Gets or sets the NPC speech bubble when a finished quest is turned in. No placeholders.
    /// </summary>
    [Display(Name = "Turn-in: NPC message")]
    public string TurnInBubble { get; set; } = "Well done! Talk to me again for your next quest.";

    /// <summary>
    /// Gets or sets the chat line per reward when a quest is turned in. {0} = quest number, {1} = reward.
    /// </summary>
    [Display(Name = "Turn-in: reward chat line")]
    public string RewardChat { get; set; } = "[Quest {0}] Reward: {1}";

    /// <summary>
    /// Gets or sets the chat line on every kill once all kill requirements are met. {0} = quest number.
    /// </summary>
    [Display(Name = "Kills met: chat line")]
    public string KillsMetChat { get; set; } = "[Quest {0}] Complete! Return to Leo the Helper for your reward.";

    /// <summary>
    /// Gets or sets the NPC speech bubble when no quest is left. No placeholders.
    /// </summary>
    [Display(Name = "Chain complete: NPC message")]
    public string ChainCompleteBubble { get; set; } = "You have completed every quest.";
}
