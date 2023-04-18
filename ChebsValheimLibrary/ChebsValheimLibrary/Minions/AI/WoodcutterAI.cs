using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChebsValheimLibrary.Minions.AI
{
    public class WoodcutterAI : MonoBehaviour
    {
        public static float RoamRange = 0f;
        public static float LookRadius = 0f;
        public static float UpdateDelay = 0f;
        
        private float nextCheck;

        private MonsterAI _monsterAI;
        private Humanoid _humanoid;

        private readonly int defaultMask = LayerMask.GetMask("Default");
        
        private static List<Transform> _transforms = new();

        private string _status;
        private bool _inContact;

        private bool _tree;

        private void Awake()
        {
            _monsterAI = GetComponent<MonsterAI>();
            _humanoid = GetComponent<Humanoid>();
            _monsterAI.m_alertRange = 1f; // don't attack unless something comes super close - focus on the wood
            _monsterAI.m_randomMoveRange = RoamRange;
        }

        public void LookForCuttableObjects()
        {
            if (_monsterAI.GetFollowTarget() != null) return;

            _transforms.RemoveAll(a => a == null); // clean trash
            
            // Trees: TreeBase
            // Stumps: Destructible with type Tree
            // Logs: TreeLog
            var closest =
                ChebGonazMinion.FindClosest<Transform>(transform, LookRadius, defaultMask, 
                    a => !_transforms.Contains(a), false);
            
            // if closest turns up nothing, pick the closest from the claimed transforms list (if there's nothing else
            // to whack, may as well whack a log right next to you, even if another skeleton is already whacking it)
            if (closest == null)
            {
                closest = _transforms
                    .OrderBy(t => Vector3.Distance(t.position, transform.position))
                    .FirstOrDefault();
            }
            
            if (closest != null)
            {
                _tree = false;
                
                // prioritize stumps, then logs, then trees
                Destructible destructible = closest.GetComponentInParent<Destructible>();
                if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(destructible.gameObject);
                    _status = "Moving to stump.";
                    return;
                }

                TreeLog treeLog = closest.GetComponentInParent<TreeLog>();
                if (treeLog != null)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(treeLog.gameObject);
                    _status = "Moving to log.";
                    return;
                }

                TreeBase tree = closest.GetComponentInParent<TreeBase>();
                if (tree != null)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(tree.gameObject);
                    _status = "Moving to tree.";
                    _tree = true;
                }
            }
        }

        private void Update()
        {
            var followTarget = _monsterAI.GetFollowTarget();
            if (followTarget != null)
            {
                transform.LookAt(followTarget.transform.position + (_tree ? Vector3.up : Vector3.down));
                
                TryAttack();
            }
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + UpdateDelay
                                      + Random.value; // add a fraction of a second so that multiple
                                                      // workers don't all simultaneously scan
                
                LookForCuttableObjects();

                if (_monsterAI.GetFollowTarget() == null) _status = "Can't find tree.";

                _humanoid.m_name = _status;
            }
        }
        
        private void TryAttack()
        {
            if (_monsterAI.GetFollowTarget() != null 
                && (_inContact 
                    || Vector3.Distance(_monsterAI.GetFollowTarget().transform.position, transform.position) < 1f))
            {
                _monsterAI.DoAttack(null, false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            _inContact = Hittable(collision.gameObject);
        }

        private void OnCollisionExit(Collision other)
        {
            _inContact = Hittable(other.gameObject);
        }

        private bool Hittable(GameObject go)
        {
            var destructible = go.GetComponentInParent<Destructible>();
            if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree)
            {
                return true;
            }

            var treeLog = go.GetComponentInParent<TreeLog>();
            if (treeLog != null)
            {
                return true;
            }

            var tree = go.GetComponentInParent<TreeBase>();
            if (tree != null)
            {
                return true;
            }

            return false;
        }
    }
}
