// <copyright file="ClassAwareDropMode.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Defines how the drop generator weighs an item's <see cref="Items.ItemDefinition.QualifiedCharacters"/>
/// against the classes of the player (and, for a kill, their party) who will receive the drop.
/// </summary>
public enum ClassAwareDropMode
{
    /// <summary>
    /// Every candidate item is picked with equal probability, regardless of class. This is the
    /// pre-existing behavior and the default for a fresh configuration.
    /// </summary>
    Off = 0,

    /// <summary>
    /// A usable item (one with no <see cref="Items.ItemDefinition.QualifiedCharacters"/>, or whose
    /// list contains one of the candidate classes) is weighted by <see cref="GameConfiguration.ClassAwareDropWeight"/>
    /// against off-class items, which keep weight 1.
    /// </summary>
    Prefer = 1,

    /// <summary>
    /// Off-class items are excluded from the pick. If none of the candidates are usable, the pick
    /// falls back to the whole list, so the drop still happens.
    /// </summary>
    Only = 2,
}
