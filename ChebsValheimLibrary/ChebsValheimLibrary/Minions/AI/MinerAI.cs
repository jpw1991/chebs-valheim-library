using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChebsValheimLibrary.Minions.AI
{
    /// <summary>
    /// Adding this to a minion turns it into a Miner. It will seek out rocks and whack them - but only in roaming
    /// mode. If set to wait/follow, it will behave like any other minion.
    /// </summary>
    public class MinerAI : MonoBehaviour
    {
        /// <summary>
        /// This is the list of rocks for the minion to seek out. Usually specified via config file. It should be a
        /// comma delimited list of prefab names.
        /// <example>
        /// Here is the list I use for Cheb's Necromancy and Cheb's Mercenaries:
        /// <code>rock1_mistlands,rock1_mountain,rock1_mountain_frac,rock2_heath,rock2_heath_frac,rock2_mountain,rock2_mountain_frac,Rock_3,Rock_3_frac,rock3_mountain,rock3_mountain_frac,rock3_silver,rock3_silver_frac,Rock_4,Rock_4_plains,rock4_coast,rock4_coast_frac,rock4_copper,rock4_copper_frac,rock4_forest,rock4_forest_frac,rock4_heath,rock4_heath_frac,Rock_7,Rock_destructible,rock_mistlands1,rock_mistlands1_frac,rock_mistlands2,RockDolmen_1,RockDolmen_2,RockDolmen_3,silvervein,silvervein_frac,MineRock_Tin,MineRock_Obsidian</code>
        /// </example>
        /// </summary>
        public static string RockInternalIDsList = "";
        /// <summary>
        /// The minion's roam range -> how far it wanders when set to roaming mode in search of rocks.
        /// </summary>
        public static float RoamRange = 0f;
        /// <summary>
        /// How far it is able to spot rocks from.
        /// </summary>
        public static float LookRadius = 0f;
        /// <summary>
        /// How often it performs searches. Low values are worse for performance.
        /// </summary>
        public static float UpdateDelay = 0f;
        
        private float nextCheck;

        private MonsterAI _monsterAI;
        private Humanoid _humanoid;
        private List<string> _rocksList;

        private string _status;
        
        private bool _inContact;

        private void Awake()
        {
            _rocksList = RockInternalIDsList.Split(',').ToList();
            _monsterAI = GetComponent<MonsterAI>();
            _humanoid = GetComponent<Humanoid>();
            _monsterAI.m_alertRange = 1f; // don't attack unless something comes super close - focus on the rocks
            _monsterAI.m_randomMoveRange = RoamRange;
        }

        /// <summary>
        /// Look for any nearby rocks and if one is spotted, set it as follow target. You shouldn't need to call this
        /// because it is already called during the FixedUpdate.
        ///
        /// Getting miners to properly reach things is a challenge. They get stuck on all kinds of stuff. To remedy this
        /// the found object has the NukeRock component added to it. This will blow the entire rock up after an elapsed
        /// period of time to ensure that even if the miner can't figure out how to get to something, it will still
        /// destroy it.
        /// </summary>
        public void LookForMineableObjects()
        {
            if (_monsterAI.GetFollowTarget() != null) return;

            // All rocks are in the static_solid layer and have a Destructible component with type Default.
            // We can just match names as the rock names are pretty unique
            var layerMask = 1 << LayerMask.NameToLayer("static_solid") | 1 << LayerMask.NameToLayer("Default_small");
            // get all nearby rocks/ores
            var nearby = ChebGonazMinion.FindNearby<Transform>(transform,
                LookRadius,
                layerMask,
                Hittable,
                false);
            // assign a priority to the rocks/ores -> eg. copper takes precedence over simple rocks
            var priorities = nearby
                .Select(c => (c, c.name.Contains("_Tin")
                                 || c.name.Contains("silver")
                                 || c.name.Contains("copper")
                    ? 1
                    : 2));
            
            // Order the list of tuples by priority first, then by distance
            var orderedPriorities = priorities.OrderBy(t => t.Item2)
                .ThenBy(t => Vector3.Distance(transform.position, t.Item1.position));

            // Get the first item from the ordered list
            var closest = orderedPriorities.FirstOrDefault().ToTuple()?.Item1;
            if (closest != null)
            {
                _monsterAI.SetFollowTarget(closest.gameObject);
                if (!closest.TryGetComponent(out NukeRock _)) closest.gameObject.AddComponent<NukeRock>();
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
                
                TryAttack();
            }
            
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + UpdateDelay
                                      + Random.value; // add a fraction of a second so that multiple
                                                      // workers don't all simultaneously scan
                
                LookForMineableObjects();

                _status = _monsterAI.GetFollowTarget() != null
                    ? $"Moving to rock ({_monsterAI.GetFollowTarget().name})."
                    : "Can't find rocks.";

                _humanoid.m_name = _status;
            }
        }

        private void TryAttack()
        {
            var followTarget = _monsterAI.GetFollowTarget();
            if (followTarget != null && _inContact)
            {
                _monsterAI.DoAttack(null, false);

                var destructible = followTarget.GetComponentInParent<Destructible>();
                if (destructible != null)
                {
                    var hitData = new HitData();
                    hitData.m_damage.m_pickaxe = 500;
                    destructible.Damage(hitData);
                    return;
                }

                var mineRock5 = followTarget.GetComponentInParent<MineRock5>();
                if (mineRock5 != null)
                {
                    // destroy all fragments
                    for (int i = 0; i < mineRock5.m_hitAreas.Count; i++)
                    {
                        var hitArea = mineRock5.m_hitAreas[i];
                        if (hitArea.m_health > 0f)
                        {
                            var hitData = new HitData();
                            hitData.m_damage.m_damage = hitArea.m_health;
                            hitData.m_point = hitArea.m_collider.bounds.center;
                            hitData.m_toolTier = 100;
                            mineRock5.DamageArea(i, hitData);
                        }
                    }
                    return;
                }

                var mineRock = followTarget.GetComponentInParent<MineRock>();
                if (mineRock != null)
                {
                    // destroy all fragments
                    for (int i = 0; i < mineRock.m_hitAreas.Length; i++)
                    {
                        var col = mineRock.m_hitAreas[i];
                        if (col.TryGetComponent(out HitArea hitArea) && hitArea.m_health > 0f)
                        {
                            var hitData = new HitData();
                            hitData.m_damage.m_damage = hitArea.m_health;
                            hitData.m_point = col.bounds.center;
                            hitData.m_toolTier = 100;
                            mineRock5.DamageArea(i, hitData);
                        }
                    }
                }
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

        private bool Hittable(Transform t)
        {
            return Hittable(t.gameObject);
        }
        
        private bool Hittable(GameObject go)
        {
            // Getting miners to hit the right stuff has been a big challenge. This is the closest thing I've been able
            // to come up with. For some reason, checking layers isn't so reliable.
            // History of most of it can be seen here: https://github.com/jpw1991/chebs-necromancy/issues/109
            var destructible = go.GetComponentInParent<Destructible>();
            return _rocksList.FirstOrDefault(rocksListName =>
                   {
                       var parent = go.transform.parent;
                       return parent != null && rocksListName.Contains(parent.name);
                   }) != null
                   || (destructible != null
                       && destructible.m_destructibleType == DestructibleType.Default
                       && destructible.GetComponent<Container>() == null // don't attack containers
                       && destructible.GetComponent<Pickable>() == null // don't attack useful bushes
                    )
                   || go.GetComponentInParent<MineRock5>() != null
                   || go.GetComponentInParent<MineRock>() != null;
        }
    }
}
