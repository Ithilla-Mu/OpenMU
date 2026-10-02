// <copyright file="MoneyPayResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// The outcome of handing money over to a player, see <see cref="MoneyDistribution.TryPay"/>.
/// </summary>
internal enum MoneyPayResult
{
    /// <summary>
    /// Nothing was paid and nothing may be consumed: the amount is zero, the scaled amount is not positive,
    /// the money doesn't fit and the pick up clamp is off, or the money could not be added.
    /// </summary>
    NotPaid,

    /// <summary>
    /// Money was added to the player, including an amount which was reduced by the pick up clamp.
    /// </summary>
    Paid,

    /// <summary>
    /// The pick up clamp is on and the player is already at the maximum inventory money, so nothing was
    /// added. The source of the money is still used up.
    /// </summary>
    Capped,
}
