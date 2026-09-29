// <copyright file="QuestTextPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Quest;

using System.Runtime.InteropServices;
using System.Text;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Quest;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IQuestTextPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.QuestTextPlugIn_Name), Description = nameof(PlugInResources.QuestTextPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3C4A6620-C40E-495E-9297-B22D223D469F")]
public class QuestTextPlugIn : IQuestTextPlugIn
{
    private const int TitleFieldSize = 64;
    private const int SummaryFieldSize = 512;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestTextPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public QuestTextPlugIn(RemotePlayer player)
    {
        this._player = player;
    }

    /// <inheritdoc/>
    public async ValueTask ShowQuestTextAsync(short group, short number, string title, string summary)
    {
        if (this._player.Connection is null)
        {
            return;
        }

        await this._player.Connection.SendQuestTextAsync(
            (ushort)number,
            (ushort)group,
            Truncate(title, TitleFieldSize),
            Truncate(summary, SummaryFieldSize)).ConfigureAwait(false);
    }

    /// <summary>
    /// The generated string setter throws when the UTF-8 bytes exceed the field, and does not
    /// reserve a terminating NUL, so the text is cut to fieldSize - 1 bytes on a character boundary.
    /// </summary>
    private static string Truncate(string text, int fieldSize)
    {
        var maxBytes = fieldSize - 1;
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var end = text.Length;
        while (end > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, end)) > maxBytes)
        {
            end--;
        }

        // Do not end on the first half of a surrogate pair.
        if (end > 0 && char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        return text[..end];
    }
}
