// <copyright file="ResetAvailableNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies a player in-game when their character reaches the level at which it can be reset,
/// per the ladder in <see cref="ResetAvailableNotificationConfiguration.LevelTiers"/>.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ResetAvailableNotificationPlugIn_Name), Description = nameof(PlugInResources.ResetAvailableNotificationPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("4B1212D5-8A38-4ADE-B3ED-5133F0995219")]
public class ResetAvailableNotificationPlugIn : ICharacterLevelUpPlugIn, IPlayerStateChangedPlugIn, ISupportCustomConfiguration<ResetAvailableNotificationConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public ResetAvailableNotificationConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public void CharacterLeveledUp(Player player)
    {
        this.Configuration ??= CreateDefaultConfiguration();

        if (player.Attributes is not { } attributes
            || !this.TryGetRequiredLevel((int)attributes[Stats.Resets], out var requiredLevel)
            || player.Level != requiredLevel)
        {
            return;
        }

        // CharacterLeveledUp (ICharacterLevelUpPlugIn.cs:21) is invoked synchronously from inside the
        // experience-gain loop (PlayerExperience.cs:233), so the notification is dispatched without
        // being awaited, mirroring the fire-and-forget respawn dispatch in
        // NPC/AttackableNpcBase.cs:332. Exceptions are caught here so a faulted task is never left
        // unobserved.
        _ = Task.Run(async () =>
        {
            try
            {
                await this.SendNotificationAsync(player).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                player.Logger.LogError(ex, "Failed to send reset-available notification to {player}", player);
            }
        });
    }

    /// <inheritdoc/>
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld || player.SelectedCharacter is null)
        {
            return;
        }

        this.Configuration ??= CreateDefaultConfiguration();

        if (player.Attributes is not { } attributes
            || !this.TryGetRequiredLevel((int)attributes[Stats.Resets], out var requiredLevel)
            || player.Level < requiredLevel)
        {
            return;
        }

        await this.SendNotificationAsync(player).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public object CreateDefaultConfig()
    {
        return CreateDefaultConfiguration();
    }

    private static ResetAvailableNotificationConfiguration CreateDefaultConfiguration()
    {
        return new()
        {
            ResetLimit = 100,
            LevelTiers =
            [
                new() { MinimumResetCount = 0, RequiredLevel = 300 },
                new() { MinimumResetCount = 6, RequiredLevel = 320 },
                new() { MinimumResetCount = 11, RequiredLevel = 340 },
                new() { MinimumResetCount = 16, RequiredLevel = 360 },
                new() { MinimumResetCount = 21, RequiredLevel = 380 },
                new() { MinimumResetCount = 26, RequiredLevel = 400 },
            ],
        };
    }

    private static async ValueTask SendBothAsync(Player player, string message)
    {
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, MessageType.GoldenCenter)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, MessageType.BlueNormal)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the level required to reset for the given reset count: the tier with the highest
    /// <see cref="ResetAvailableNotificationConfiguration.ResetLevelTier.MinimumResetCount"/> that is
    /// less than or equal to <paramref name="resets"/>. Returns <see langword="false"/> if the reset
    /// count is at or above <see cref="ResetAvailableNotificationConfiguration.ResetLimit"/>, or below
    /// every configured tier.
    /// </summary>
    private bool TryGetRequiredLevel(int resets, out int requiredLevel)
    {
        requiredLevel = 0;
        if (resets >= this.Configuration!.ResetLimit)
        {
            return false;
        }

        var tier = this.Configuration.LevelTiers
            .Where(t => t.MinimumResetCount <= resets)
            .OrderByDescending(t => t.MinimumResetCount)
            .FirstOrDefault();
        if (tier is null)
        {
            return false;
        }

        requiredLevel = tier.RequiredLevel;
        return true;
    }

    private ValueTask SendNotificationAsync(Player player)
    {
        var message = this.Configuration!.Message;
        return SendBothAsync(player, message);
    }
}
