using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;
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
        private bool _chopping;

        private void Awake()
        {
            _monsterAI = GetComponent<MonsterAI>();
            _humanoid = GetComponent<Humanoid>();
            _monsterAI.m_alertRange = 1f; // don't attack unless something comes super close - focus on the wood
            _monsterAI.m_randomMoveRange = RoamRange;
        }

        private int EquippedToolsTier()
        {
            var result = 2; // 2 = bronze
            if (_humanoid != null)
            {
                var axeItemToolTier = _humanoid.m_randomWeapon?.FirstOrDefault()?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_toolTier;
                result = axeItemToolTier ?? result;
            }
            return result;
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
                var toolTier = EquippedToolsTier();
                
                closest = _transforms
                    .Where(t =>
                    {
                        var destructible = t.GetComponentInParent<Destructible>();
                        if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree
                                                 && destructible.m_minToolTier <= toolTier)
                            return true;
                        var treeLog = t.GetComponentInParent<TreeLog>();
                        if (treeLog != null && treeLog.m_minToolTier <= toolTier)
                            return true;
                        var tree = t.GetComponentInParent<TreeBase>();
                        if (tree != null && tree.m_minToolTier <= toolTier)
                            return true;
                        return false;
                    })
                    .OrderBy(t => Vector3.Distance(t.position, transform.position))
                    .FirstOrDefault();
            }
            
            if (closest != null)
            {
                // prioritize stumps, then logs, then trees
                var destructible = closest.GetComponentInParent<Destructible>();
                if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(destructible.gameObject);
                    _status = "Moving to stump.";
                    if (!closest.TryGetComponent(out NukeTree _)) closest.gameObject.AddComponent<NukeTree>();
                    return;
                }

                var treeLog = closest.GetComponentInParent<TreeLog>();
                if (treeLog != null)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(treeLog.gameObject);
                    _status = "Moving to log.";
                    if (!closest.TryGetComponent(out NukeTree _)) closest.gameObject.AddComponent<NukeTree>();
                    return;
                }

                var tree = closest.GetComponentInParent<TreeBase>();
                if (tree != null)
                {
                    _transforms.Add(closest);
                    _monsterAI.SetFollowTarget(tree.gameObject);
                    _status = "Moving to tree.";
                    if (!closest.TryGetComponent(out NukeTree _)) closest.gameObject.AddComponent<NukeTree>();
                }
            }
        }

        private void FixedUpdate()
        {
            var followTarget = _monsterAI.GetFollowTarget();

            // if following player, suspend all worker logic
            if (followTarget != null)
            {
                if(followTarget.TryGetComponent(out Player player))
                {
                    _status = $"Following {player.GetPlayerName()}";
                    return;   
                }
                
                var followTargetPos = followTarget.transform.position;
                var lookAtPos = new Vector3(followTargetPos.x, transform.position.y, followTargetPos.z);
                transform.LookAt(lookAtPos);
                
                TryAttack(lookAtPos);
            }
            
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + UpdateDelay
                                      + Random.value; // add a fraction of a second so that multiple
                                                      // workers don't all simultaneously scan
                
                LookForCuttableObjects();

                // do not use followTarget variable from above here -> we want to check if it changed
                if (_monsterAI.GetFollowTarget() == null) _status = "Can't find tree.";

                _humanoid.m_name = _status;
            }
        }

        private void TryAttack(Vector3 lookAtPos)
        {
            // a bunch of dumb stuff can be null as the game is loading, so check before proceeding
            if (_humanoid == null || _monsterAI == null) return;
            
            // if already chopping, abort
            if (_chopping) return;
            
            var followTarget = _monsterAI.GetFollowTarget();
            if (followTarget != null// && _inContact)
                 && (_inContact 
                     || Vector3.Distance(lookAtPos, transform.position) < 1.5f))
            {
                var destructible = followTarget.GetComponentInParent<Destructible>();
                if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree)
                {
                    StartCoroutine(Chop(destructible.m_health, destructible,
                        () => destructible.m_health));
                    return;
                }

                var treeLog = followTarget.GetComponentInParent<TreeLog>();
                if (treeLog != null)
                {
                    StartCoroutine(Chop(treeLog.m_health, treeLog,
                        () => treeLog.m_health));
                    return;
                }

                var tree = followTarget.GetComponentInParent<TreeBase>();
                if (tree != null)
                {
                    StartCoroutine(Chop(tree.m_health, treeLog,
                        () => tree.m_health));
                    return;
                }
            }
        }
        
        IEnumerator Chop(float healthBeforeAttack, IDestructible destructible, Func<float> healthAfterDamaged)
        {
            // because of how difficult it is to get a stupid axe to connect with a tree, let's just make sure the
            // damage gets done

            _chopping = true;
            _monsterAI.DoAttack(null, false);
            yield return new WaitForSeconds(2);

            if (healthAfterDamaged.Invoke() < healthBeforeAttack)
            {
                // it worked, great
                _chopping = false;
                yield break;
            }

            // it didn't work - make it work
            var axeItem = _humanoid.m_randomWeapon?.FirstOrDefault()?.GetComponent<ItemDrop>();
            if (axeItem == null)
            {
                _chopping = false;
                Logger.LogError("WoodcutterAI.Chop: Woodcutter has no axe?");
                yield break;
            }

            var hitData = new HitData();
            hitData.m_damage.m_chop = axeItem.m_itemData.m_shared.m_damages.m_chop;
            hitData.m_toolTier = axeItem.m_itemData.m_shared.m_toolTier;
            destructible?.Damage(hitData);

            _chopping = false;
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
