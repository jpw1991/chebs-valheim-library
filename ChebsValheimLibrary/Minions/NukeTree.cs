using System.Collections;
using UnityEngine;

namespace ChebsValheimLibrary.Minions
{
    /// <summary>
    /// A component that can be added to a tree in order to destroy it after a period.
    /// </summary>
    public class NukeTree : MonoBehaviour
    {
        /// <summary>
        /// How long to wait before nuking the tree.
        /// </summary>
        public const float NukeAfter = 120f;
        private IEnumerator Start()
        {
            yield return new WaitForSeconds(NukeAfter);
            
            Nuke();
            
            Destroy(this);
        }

        protected virtual void Nuke()
        {
            //Logger.LogInfo($"Nuking {gameObject.name}");

            var hitData = new HitData();
            hitData.m_damage.m_chop = 999;
            hitData.m_toolTier = 4;

            var destructible = GetComponentInParent<Destructible>();
            if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree)
            {
                destructible.Damage(hitData);
                return;
            }

            var treeLog = GetComponentInParent<TreeLog>();
            if (treeLog != null)
            {
                treeLog.Damage(hitData);
                return;
            }

            var tree = GetComponentInParent<TreeBase>();
            if (tree != null)
            {
                tree.Damage(hitData);
            }
        }
    }
}