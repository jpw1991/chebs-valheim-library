using System.Collections.Generic;
using System.Linq;
using ChebsValheimLibrary.Common;
using UnityEngine;

namespace ChebsValheimLibrary.Structures
{
    /// <summary>
    /// The basis for a structure eg. a Pylon.
    /// </summary>
    public class Structure : MonoBehaviour
    {
        /// <summary>
        /// The distance from which players are seen by structures.
        /// </summary>
        public const float PlayerDetectionDistance = 150f;
        /// <summary>
        /// The ChebsRecipe object associated with the structure.
        /// </summary>
        public static ChebsRecipe ChebsRecipeConfig;
        /// <summary>
        /// What to do when updating a recipe.
        /// <example>
        /// Example taken from the Farming Pylon.
        /// <code>
        /// public new static void UpdateRecipe()
        /// {
        ///     ChebsRecipeConfig.UpdateRecipe(ChebsRecipeConfig.CraftingCost);
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public static void UpdateRecipe()
        {
            
        }
        /// <summary>
        /// EnemiesNearby returns true if an enemy is within the radius when it is called. It also spits the closest
        /// enemy to the structure so it may be targeted or otherwise acted upon.
        /// </summary>
        /// <param name="characterInRange">The closest enemy in range.</param>
        /// <param name="radius">The radius to consider.</param>
        /// <returns>True if enemy is nearby, false if no enemy is nearby.</returns>
        protected bool EnemiesNearby(out Character characterInRange, float radius)
        {
            List<Character> charactersInRange = new();
            Character.GetCharactersInRange(
                transform.position,
                radius,
                charactersInRange
            );
            foreach (var character in charactersInRange.Where(
                         character => 
                             character != null
                             && (character.m_faction != Character.Faction.Players && !character.m_tamed)))
            {
                characterInRange = character;
                return true;
            }
            characterInRange = null;
            return false;
        }
    }
}