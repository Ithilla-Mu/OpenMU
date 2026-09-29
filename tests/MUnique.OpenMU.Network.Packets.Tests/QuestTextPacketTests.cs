// <copyright file="QuestTextPacketTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Tests the byte layout of <see cref="QuestTextRef"/>, which the client parses at fixed offsets.
/// </summary>
[TestFixture]
public class QuestTextPacketTests
{
    /// <summary>
    /// Tests the header, the field offsets and the NUL padding.
    /// </summary>
    [Test]
    public void QuestText_Layout()
    {
        Assert.That(QuestTextRef.Length, Is.EqualTo(586));

        var data = new byte[QuestTextRef.Length];
        Array.Fill(data, (byte)0xFF);
        var packet = new QuestTextRef(data)
        {
            QuestNumber = 0x0102,
            QuestGroup = 0x0304,
            Title = "Ab",
            Summary = "Cd",
        };

        Assert.That(data[0], Is.EqualTo(0xC2));
        Assert.That((data[1] << 8) | data[2], Is.EqualTo(586));
        Assert.That(data[3], Is.EqualTo(0xF6));
        Assert.That(data[4], Is.EqualTo(0xF0));
        Assert.That(data[6], Is.EqualTo(0x02));
        Assert.That(data[7], Is.EqualTo(0x01));
        Assert.That(data[8], Is.EqualTo(0x04));
        Assert.That(data[9], Is.EqualTo(0x03));
        Assert.That(data[10], Is.EqualTo((byte)'A'));
        Assert.That(data[11], Is.EqualTo((byte)'b'));
        Assert.That(data[12], Is.EqualTo(0));
        Assert.That(data[73], Is.EqualTo(0));
        Assert.That(data[74], Is.EqualTo((byte)'C'));
        Assert.That(data[75], Is.EqualTo((byte)'d'));
        Assert.That(data[76], Is.EqualTo(0));
        Assert.That(data[585], Is.EqualTo(0));
        Assert.That(packet.Title, Is.EqualTo("Ab"));
    }
}
