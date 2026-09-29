// <copyright file="QuestMasterConstants.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.QuestMaster;

/// <summary>
/// Constants of the quest chain which is driven by the quest master NPC instead of the client's quest dialogs.
/// </summary>
public static class QuestMasterConstants
{
    /// <summary>
    /// The quest group of the chain. The F6 quest handlers refuse it, so the chain can only advance by talking to the NPC.
    /// </summary>
    public const short QuestGroup = 100;

    /// <summary>
    /// The NPC number of the quest master ('Leo the Helper').
    /// </summary>
    public const short NpcNumber = 371;
}
