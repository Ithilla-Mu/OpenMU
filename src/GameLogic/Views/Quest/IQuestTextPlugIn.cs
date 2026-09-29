// <copyright file="IQuestTextPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Quest;

/// <summary>
/// Interface of a view whose implementation sends the title and summary of a quest.
/// </summary>
/// <remarks>
/// Sends C2F6F0.
/// </remarks>
public interface IQuestTextPlugIn : IViewPlugIn
{
    /// <summary>
    /// Sends the title and summary of the specified quest to the client.
    /// </summary>
    /// <param name="group">The quest group.</param>
    /// <param name="number">The quest number.</param>
    /// <param name="title">The quest title.</param>
    /// <param name="summary">The quest summary.</param>
    ValueTask ShowQuestTextAsync(short group, short number, string title, string summary);
}
