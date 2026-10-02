// <copyright file="SellItemToNpcActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using NUnit.Framework;

/// <summary>
/// Tests for the <see cref="SellItemToNpcAction"/> and its handling of the maximum inventory money.
/// </summary>
[TestFixture]
public class SellItemToNpcActionTest
{
    private const byte Slot = 20;

    private const int SellingPrice = 1000;

    /// <summary>
    /// Tests that with the clamp on, a sale at the maximum completes and gives no money.
    /// </summary>
    [Test]
    public async Task SaleAtCapCompletesWhenClampedAsync()
    {
        var (player, item) = await this.PrepareAsync(true, 5000).ConfigureAwait(false);

        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, Slot).ConfigureAwait(false), Is.True);
        Assert.That(player.Inventory!.GetItem(Slot), Is.Null);
        Assert.That(player.Money, Is.EqualTo(5000));
    }

    /// <summary>
    /// Tests that with the clamp on, a sale near the maximum caps the price.
    /// </summary>
    [Test]
    public async Task SaleNearCapIsCappedWhenClampedAsync()
    {
        var (player, item) = await this.PrepareAsync(true, 4500).ConfigureAwait(false);

        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, Slot).ConfigureAwait(false), Is.True);
        Assert.That(player.Inventory!.GetItem(Slot), Is.Null);
        Assert.That(player.Money, Is.EqualTo(5000));
    }

    /// <summary>
    /// Tests that with the clamp off, a sale which doesn't fit is refused and the item stays.
    /// </summary>
    [Test]
    public async Task SaleAtCapIsRefusedWhenNotClampedAsync()
    {
        var (player, item) = await this.PrepareAsync(false, 5000).ConfigureAwait(false);

        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, Slot).ConfigureAwait(false), Is.False);
        Assert.That(player.Inventory!.GetItem(Slot), Is.SameAs(item));
        Assert.That(player.Money, Is.EqualTo(5000));
    }

    private async Task<(Player Player, Item Item)> PrepareAsync(bool clamp, int money)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = 5000;
        player.GameContext.Configuration.ClampMoneyOnPickup = clamp;
        player.Money = money;
        player.OpenedNpc = new NonPlayerCharacter(null!, new MonsterDefinition { NpcWindow = NpcWindow.Merchant, MerchantStore = new ItemStorage() }, null!);

        // Group 15 items are priced by their fixed value; the selling price is a third of it.
        var definition = new ItemDefinition { Width = 1, Height = 1, Durability = 1, Group = 15, Value = SellingPrice * 3 };
        var mock = new Mock<Item>();
        mock.SetupAllProperties();
        mock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        mock.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        mock.Object.Definition = definition;
        mock.Object.Durability = 1;
        await player.Inventory!.AddItemAsync(Slot, mock.Object).ConfigureAwait(false);
        return (player, mock.Object);
    }
}
