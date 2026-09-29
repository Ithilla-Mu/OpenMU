# C2 F6 F0 - QuestText (by server)

## Is sent when

Before a quest is announced to the client by its quest number and group, so the client can look up the text of that quest.

## Causes the following actions on the client side

The client caches the title and summary of the quest, and shows them wherever it displays this quest.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |   586   | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF6  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0xF0  | Packet header - sub packet type identifier |
| 6 | 2 | ShortLittleEndian |  | QuestNumber |
| 8 | 2 | ShortLittleEndian |  | QuestGroup |
| 10 | 64 | String |  | Title; The quest title, UTF-8, padded with NUL bytes. |
| 74 | 512 | String |  | Summary; The quest summary, UTF-8, padded with NUL bytes. |