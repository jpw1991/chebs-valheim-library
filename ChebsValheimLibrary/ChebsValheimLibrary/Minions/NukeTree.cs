using System.Collections;
using UnityEngine;

namespace ChebsValheimLibrary.Minions
{
    public class NukeTree : MonoBehaviour
    {
        public const float NukeAfter = 20f;
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