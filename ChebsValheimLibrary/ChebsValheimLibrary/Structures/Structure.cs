using System.Collections.Generic;
using System.Linq;
using ChebsValheimLibrary.Common;
using UnityEngine;

namespace ChebsValheimLibrary.Structures
{
    public class Structure : MonoBehaviour
    {
        public static ChebsRecipe ChebsRecipeConfig;
        
        public static void UpdateRecipe()
        {
            
        }
        
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