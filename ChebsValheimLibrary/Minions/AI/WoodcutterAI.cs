using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;
using Random = UnityEngine.Random;

namespace ChebsValheimLibrary.Minions.AI
{
    /// <summary>
    /// Adding this to a minion turns it into a Woodcutter. It will seek out trees and whack them - but only in roaming
    /// mode. If set to wait/follow, it will behave like any other minion.
    /// </summary>
    public class WoodcutterAI : MonoBehaviour
    {
        /// <summary>
        /// The minion's roam range -> how far it wanders when set to roaming mode in search of trees.
        /// </summary>
        public virtual float RoamRange => 0f;

        /// <summary>
        /// How far it is able to spot trees from.
        /// </summary>
        public virtual float LookRadius => 0f;

        /// <summary>
        /// How often it performs searches. Low values are worse for performance.
        /// </summary>
        public virtual float UpdateDelay => 0f;

        /// <summary>
        /// How much damage the woodcutter deals to trees.
        /// </summary>
        public virtual float ToolDamage => 6f;

        /// <summary>
        /// The tool tier of the axe eg. 1 = stone/flint, 2 = bronze, 3 = iron, etc.
        /// </summary>
        public virtual short ToolTier => 2;

        /// <summary>
        /// How often the worker provides updates on what it's doing. Set to 0 for no chatting.
        /// </summary>
        public virtual float ChatInterval => 5f;

        /// <summary>
        /// How close the player must be for the NPC to talk.
        /// </summary>
        public virtual float ChatDistance => 5f;
        
        /// <summary>
        /// The minion's displayable status. You can display it, log it, whatever you want.
        /// </summary>
        public string Status
        {
            get => _status;
            protected set => _status = value;
        }

        #region PrivateVariables

        private float _nextCheck, _lastChat;
        private MonsterAI _monsterAI;
        private Humanoid _humanoid;
        private readonly int _defaultMask = LayerMask.GetMask("Default");
        private static List<Transform> _transforms = new();
        private string _status;
        private bool _inContact;
        private bool _chopping;

        #endregion

        #region PublicMethods

        /// <summary>
        /// Look for any nearby trees and if one is spotted, set it as follow target. You shouldn't need to call this
        /// because it is already called during the FixedUpdate.
        ///
        /// Getting woodcutters to properly reach things is a challenge. They get stuck on all kinds of stuff. To remedy this
        /// the found object has the NukeTree component added to it. This will blow the entire tree up after an elapsed
        /// period of time to ensure that even if the woodcuter can't figure out how to get to something, it will still
        /// destroy it.
        /// </summary>
        public void LookForCuttableObjects()
        {
            if (_monsterAI.GetFollowTarget() != null) return;

            _transforms.RemoveAll(a => a == null); // clean trash

            // Trees: TreeBase
            // Stumps: Destructible with type Tree
            // Logs: TreeLog
            var closest =
                ChebGonazMinion.FindClosest<Transform>(transform, LookRadius, _defaultMask,
                    a => !_transforms.Contains(a), false);

            // if closest turns up nothing, pick the closest from the claimed transforms list (if there's nothing else
            // to whack, may as well whack a log right next to you, even if another skeleton is already whacking it)
            if (closest == null)
            {
                closest = _transforms
                    .Where(t =>
                    {
                        var destructible = t.GetComponentInParent<Destructible>();
                        if (destructible != null && destructible.GetDestructibleType() == DestructibleType.Tree
                                                 && destructible.m_minToolTier <= ToolTier)
                            return true;
                        var treeLog = t.GetComponentInParent<TreeLog>();
                        if (treeLog != null && treeLog.m_minToolTier <= ToolTier)
                            return true;
                        var tree = t.GetComponentInParent<TreeBase>();
                        if (tree != null && tree.m_minToolTier <= ToolTier)
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

        public void UpdateToolProperties()
        {
            var tool = _humanoid.m_randomWeapon?.FirstOrDefault()?.GetComponent<ItemDrop>();
            if (tool == null)
            {
                Logger.LogError("Failed to update tool properties: tool is null");
                return;
            }

            tool.m_itemData.m_shared.m_damages.m_chop = ToolDamage;
            tool.m_itemData.m_shared.m_toolTier = ToolTier;
        }

        #endregion

        protected virtual void Awake()
        {
            _monsterAI = GetComponent<MonsterAI>();
            _humanoid = GetComponent<Humanoid>();
            _monsterAI.m_alertRange = 1f; // don't attack unless something comes super close - focus on the wood
            _monsterAI.m_randomMoveRange = RoamRange;

            UpdateToolProperties();
        }

        protected virtual void FixedUpdate()
        {
            var followTarget = _monsterAI.GetFollowTarget();

            // if following player, suspend all worker logic
            if (followTarget != null)
            {
                if (followTarget.TryGetComponent(out Player player))
                {
                    var followingMessage = Localization.instance.Localize("$chebgonaz_minionstatus_following");
                    _status = $"{followingMessage} {player.GetPlayerName()}";
                    return;
                }

                if (ChatInterval != 0 && Time.time > _lastChat + ChatInterval)
                {
                    _lastChat = Time.time;
                    var playersInRange = new List<Player>();
                    Player.GetPlayersInRange(transform.position, ChatDistance, playersInRange);
                    if (playersInRange.Count > 0)
                    {
                        var targetMessage = Localization.instance.Localize("$chebgonaz_worker_target");
                        Chat.instance.SetNpcText(gameObject, Vector3.up, 5f, 10f, "",
                            targetMessage + $": {followTarget.gameObject.name}",
                            false);
                    }
                }

                var followTargetPos = followTarget.transform.position;
                var lookAtPos = new Vector3(followTargetPos.x, transform.position.y, followTargetPos.z);
                transform.LookAt(lookAtPos);

                TryAttack(lookAtPos);
            }

            if (Time.time > _nextCheck)
            {
                _nextCheck = Time.time + UpdateDelay
                                       + Random.value; // add a fraction of a second so that multiple
                // workers don't all simultaneously scan

                LookForCuttableObjects();

                // do not use followTarget variable from above here -> we want to check if it changed
                if (_monsterAI.GetFollowTarget() == null)
                    _status = Localization.instance.Localize("$chebgonaz_worker_cantfindtarget");

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
            if (followTarget != null // && _inContact)
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
            var hitData = new HitData();
            hitData.m_damage.m_chop = ToolDamage;
            hitData.m_toolTier = ToolTier;
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