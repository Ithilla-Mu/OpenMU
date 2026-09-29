// <copyright file="ResetAvailableNotificationConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using MUnique.OpenMU.DataModel.Composition;

/// <summary>
/// Configuration of the <see cref="ResetAvailableNotificationPlugIn"/>.
/// </summary>
public class ResetAvailableNotificationConfiguration
{
    /// <summary>
    /// Gets or sets the reset limit. No notification is sent once the character's reset count
    /// reaches this value, matching the cap enforced by <see cref="ResetConfiguration.ResetLimit"/>.
    /// </summary>
    [Display(Name = "Reset limit")]
    public int ResetLimit { get; set; } = 100;

    /// <summary>
    /// Gets or sets the required level per reset-count range. Resolved by taking the tier with the
    /// highest <see cref="ResetLevelTier.MinimumResetCount"/> that is less than or equal to the
    /// character's current reset count; a character below every tier's minimum gets no notification.
    /// </summary>
    [Display(Name = "Level tiers")]
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    public ICollection<ResetLevelTier> LevelTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets the message shown to the player when they become eligible to reset.
    /// </summary>
    [Display(Name = "Message")]
    public string Message { get; set; } = "You can now reset your character.";

    /// <summary>
    /// Tier definition for the level required to reset, keyed on the character's current reset count.
    /// </summary>
    public class ResetLevelTier
    {
        /// <summary>
        /// Gets or sets the minimum reset count at which this tier applies.
        /// </summary>
        [Display(Name = "Minimum reset count")]
        public int MinimumResetCount { get; set; }

        /// <summary>
        /// Gets or sets the level required to reset for this tier.
        /// </summary>
        [Display(Name = "Required level")]
        public int RequiredLevel { get; set; }
    }
}
