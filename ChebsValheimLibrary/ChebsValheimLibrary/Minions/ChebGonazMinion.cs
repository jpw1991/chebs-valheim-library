﻿using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ChebsValheimLibrary.Common;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Minions
{
    public class ChebGonazMinion : MonoBehaviour
    {
        // we add this component to the creatures we create in the mod
        // so that we can use .GetComponent<ChebGonazMinion>()
        // to determine whether a creature was created by the mod, or
        // whether it was created by something else.
        //
        // This allows us to only call wait/follow/whatever on minions
        // that the mod has created. The component is lost between sessions
        // so it must be checked for in Awake and re-added (see harmony patching).

        public enum CleanupType
        {
            None,
            Time,
            Logout,
        }

        public enum DropType
        {
            Nothing,
            JustResources,
            Everything,
        }

        public enum State
        {
            Waiting,
            Roaming,
            Following,
        }

        public enum ArmorType
        {
            None,
            Leather,
            LeatherTroll,
            LeatherWolf,
            LeatherLox,
            Bronze,
            Iron,
            BlackMetal,
        }

        public bool canBeCommanded = true;

        public const string MinionOwnershipZdoKey = "UndeadMinionMaster";
        public const string MinionDropsZdoKey = "UndeadMinionDrops";
        public const string MinionWaitPosZdoKey = "UndeadMinionWaitPosition";
        public const string MinionWaitObjectName = "UndeadMinionWaitPositionObject";

        public int createdOrder;

        private static readonly int VehicleLayer = LayerMask.NameToLayer("vehicle");

        public bool ItemsDropped { get; private set; }

        #region DeathCrates
        private static List<Transform> _deathCrates = new();

        public bool DepositIntoNearbyDeathCrate(CharacterDrop characterDrop, float range=15f)
        {
            // return:
            // true = successful - items all loaded into a box
            // false = unsuccessful - items remain
            
            // cleanup
            _deathCrates.RemoveAll(t => t == null);

            // try depositing everything into existing containers
            var deathCrates = _deathCrates
                .OrderBy(t => Vector3.Distance(t.position, transform.position) < range);
            foreach (var t in deathCrates)
            {
                if (characterDrop.m_drops.Count < 1) break;
                if (!t.TryGetComponent(out Container container)) continue;

                var inv = container.GetInventory();
                if (inv is null) continue;

                var dropsRemaining = new List<CharacterDrop.Drop>();
                foreach (var drop in characterDrop.m_drops)
                {
                    if (inv.CanAddItem(drop.m_prefab))
                    {
                        inv.AddItem(drop.m_prefab, drop.m_amountMax);
                    }
                    else
                    {
                        dropsRemaining.Add(drop);
                    }
                }

                characterDrop.m_drops = dropsRemaining;
            }
            
            // if items remain undeposited, create a new crate for them
            if (characterDrop.m_drops.Count < 1)
            {
                ItemsDropped = true;
                return ItemsDropped;
            }
            var crate = CreateDeathCrate();
            if (crate != null)
            {
                // warning: we mustn't ever exceed the maximum storage capacity
                // of the crate. Not a problem right now, but could be in the future
                // if the ingredients exceed 4. Right now, can only be 3, so it's fine.
                // eg. bones, meat, ingot (draugr) OR bones, ingot, surtling core (skele)
                var inv = crate.GetInventory();
                var unsuccessful = new List<CharacterDrop.Drop>();
                characterDrop.m_drops.ForEach(drop =>
                {
                    if (!inv.AddItem(drop.m_prefab, drop.m_amountMax))
                    {
                        unsuccessful.Add(drop);
                    }
                });
                characterDrop.m_drops = unsuccessful;

                ItemsDropped = unsuccessful.Count == 0;
            }

            return ItemsDropped;
        }
        
        private Container CreateDeathCrate()
        {
            // use vanilla cargo crate -> same as a karve/longboat drops
            var cratePrefab = ZNetScene.instance.GetPrefab("CargoCrate");
            var result = Instantiate(cratePrefab, transform.position + Vector3.up, Quaternion.identity);
            _deathCrates.Add(result.transform);
            return result.GetComponent<Container>();
        }
        #endregion

        private Vector3 StatusRoaming => Vector3.negativeInfinity;
        private Vector3 StatusFollowing => Vector3.positiveInfinity;
        
        public static ArmorType DetermineArmorType(Inventory inventory, int armorBlackIronRequired, int armorIronRequired, int armorBronzeRequired, int armorLeatherRequired)
        {
            int blackMetalInInventory = inventory.CountItems("$item_blackmetal");
            if (blackMetalInInventory >= armorBlackIronRequired)
            {
                return ArmorType.BlackMetal;
            }
            
            int ironInInventory = inventory.CountItems("$item_iron");
            if (ironInInventory >= armorIronRequired)
            {
                return ArmorType.Iron;
            }
            
            int bronzeInInventory = inventory.CountItems("$item_bronze");
            if (bronzeInInventory >= armorBronzeRequired)
            {
                return ArmorType.Bronze;
            }
            
            int trollHideInInventory = inventory.CountItems("$item_trollhide");
            if (trollHideInInventory >= armorLeatherRequired)
            {
                return ArmorType.LeatherTroll;
            }
            
            int wolfHideInInventory = inventory.CountItems("$item_wolfpelt");
            if (wolfHideInInventory >= armorLeatherRequired)
            {
                return ArmorType.LeatherWolf;
            }
            
            int loxHideInInventory = inventory.CountItems("$item_loxpelt");
            if (loxHideInInventory >= armorLeatherRequired)
            {
                return ArmorType.LeatherLox;
            }
            
            // todo: expose these options to config
            var leatherItemTypes = new List<string>()
            {
                "$item_leatherscraps",
                "$item_deerhide",
                "$item_scalehide"
            };
            
            foreach (var leatherItem in leatherItemTypes)
            {
                var leatherItemsInInventory = inventory.CountItems(leatherItem);
                if (leatherItemsInInventory >= armorLeatherRequired)
                {
                    return ArmorType.Leather;
                }
            }

            return ArmorType.None;
        }
        
        public static bool CanSpawn(MemoryConfigEntry<string, List<string>> itemsCost, Inventory inventory,
            out string message)
        {
            var itemCostsList = itemsCost.Value;
            return CanSpawn(itemCostsList, inventory, out message);
        }

        public static bool CanSpawn(string itemsCost, Inventory inventory,
            out string message)
        {
            var itemCostsList = itemsCost?.Split(',').ToList();
            return CanSpawn(itemCostsList, inventory, out message);
        }
        
        public static bool CanSpawn(List<string> itemsCost, Inventory inventory,
            out string message)
        {
            message = "";
            var requirementsSatisfied = new List<Tuple<bool, string>>();
            foreach (var fuel in itemsCost)
            {
                var splut = fuel.Split(':');
                if (splut.Length != 2)
                {
                    message = $"[1] Error in config for ItemsCost - please revise: ({fuel})";
                    Logger.LogError(message);
                    return false;
                }

                var itemRequired = splut[0];
                if (!int.TryParse(splut[1], out int itemAmountRequired))
                {
                    message = $"[2] Error in config for ItemsCost - please revise: ({fuel})";
                    Logger.LogError(message);
                    return false;
                }

                var result = CountItems(itemRequired, inventory);
                var canSpawn = result.Item1 >= itemAmountRequired;
                message = canSpawn ? "" : $"Not enough {string.Join("/", result.Item3)}";
                requirementsSatisfied.Add(new Tuple<bool, string>(canSpawn, message));
            }

            var cantSpawn = requirementsSatisfied.Find(t => !t.Item1);
            if (cantSpawn != null)
            {
                message = cantSpawn.Item2;
                return false;
            }
            
            return true;
        }

        public static void ConsumeRequirements(MemoryConfigEntry<string, List<string>> itemsCost, Inventory inventory)
        {
            var itemCostsList = itemsCost.Value;
            ConsumeRequirements(itemCostsList, inventory);
        }

        public static void ConsumeRequirements(string itemsCost, Inventory inventory)
        {
            var itemCostsList = itemsCost?.Split(',').ToList();
            ConsumeRequirements(itemCostsList, inventory);
        }
        
        protected static void ConsumeRequirements(List<string> itemsCost, Inventory inventory)
        {
            foreach (var fuel in itemsCost)
            {
                var splut = fuel.Split(':');
                if (splut.Length != 2)
                {
                    Logger.LogError("Error in config for ItemsCost - please revise.");
                    return;
                }

                var itemRequired = splut[0];
                if (!int.TryParse(splut[1], out int itemAmountRequired))
                {
                    Logger.LogError("Error in config for ItemsCost - please revise.");
                    return;
                }

                var acceptedItemAccumulator = 0;
                var acceptedItems = itemRequired.Split('|');
                foreach (var acceptedItem in acceptedItems)
                {
                    if (acceptedItemAccumulator >= itemAmountRequired) break;
                    
                    var requiredItemPrefab = ZNetScene.instance.GetPrefab(acceptedItem);
                    if (requiredItemPrefab == null)
                    {
                        Logger.LogError($"Error processing config for ItemsCost: {itemRequired} doesn't exist.");
                        return;
                    }
                    
                    var requiredItemName = requiredItemPrefab.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name;
                    var itemsInInv = inventory.CountItems(requiredItemName);

                    for (var i = 0; i < itemsInInv; i++)
                    {
                        if (acceptedItemAccumulator >= itemAmountRequired) break;

                        inventory.RemoveItem(requiredItemName, 1);
                        acceptedItemAccumulator++;
                    }
                }
            }
        }

        protected CharacterDrop GenerateDeathDrops(ConfigEntry<DropType> dropOnDeath,
            MemoryConfigEntry<string, List<string>> itemsCost)
        {
            if (dropOnDeath.Value == DropType.Nothing) return null;

            var characterDrop = gameObject.AddComponent<CharacterDrop>();

            foreach (var fuel in itemsCost.Value)
            {
                var splut = fuel.Split(':');
                if (splut.Length != 2)
                {
                    Logger.LogError("Error in config for ItemsCost - please revise.");
                    return null;
                }

                var itemRequired = splut[0];
                if (!int.TryParse(splut[1], out int itemAmountRequired))
                {
                    Logger.LogError("Error in config for ItemsCost - please revise.");
                    return null;
                }
                
                var acceptedItems = itemRequired.Split('|');
                foreach (var acceptedItem in acceptedItems)
                {
                    var requiredItemPrefab = ZNetScene.instance.GetPrefab(acceptedItem);
                    if (requiredItemPrefab == null)
                    {
                        Logger.LogError($"Error processing config for ItemsCost: {acceptedItem} doesn't exist.");
                        return null;
                    }

                    characterDrop.m_drops.Add(new CharacterDrop.Drop
                    {
                        m_prefab = requiredItemPrefab,
                        m_onePerPlayer = true,
                        m_amountMin = itemAmountRequired,
                        m_amountMax = itemAmountRequired,
                        m_chance = 1f
                    });

                    break; // just take the first one in the list for now
                }
            }

            return characterDrop;
        }
        
        private static Tuple<int, string, List<string>> CountItems(string item, Inventory inventory)
        {
            var acceptedItemsFound = 0;
            var acceptedItemsLocalizedNames = new List<string>();
            var acceptedItems = item.Split('|');
            foreach (var acceptedItem in acceptedItems)
            {
                var requiredItemPrefab = ZNetScene.instance.GetPrefab(acceptedItem);
                if (requiredItemPrefab == null)
                {
                    var message = $"Error processing config for ItemsCost: {acceptedItem} doesn't exist.";
                    Logger.LogError(message);
                    return new Tuple<int, string, List<string>>(0, message, acceptedItemsLocalizedNames);
                }

                var requiredItemName = requiredItemPrefab.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name;
                acceptedItemsLocalizedNames.Add(requiredItemName);
                var amountInInv = inventory.CountItems(requiredItemName);
                acceptedItemsFound += amountInInv;
            }

            return new Tuple<int, string, List<string>>(acceptedItemsFound, "", acceptedItemsLocalizedNames);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // ignore collision with player
            var character = collision.gameObject.GetComponent<Character>();
            if (character != null
                && character.m_faction == Character.Faction.Players
                && character.GetComponent<ChebGonazMinion>() == null) // allow collision between minions
            {
                Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
                return;
            }
            
            // ignore collision with cart
            //
            // can't find a good way to detect collision with cart except for via its name. So... there it is.
            // Code review & a better solution most welcome
            if (collision.gameObject.GetComponentInParent<Vagon>() != null)
            {
                Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
                //Physics.IgnoreCollision(collision.gameObject.GetComponent<Collider>(), GetComponent<Collider>());
                return;
            }
            // below = failed cart collision detection attempts
            // Jotunn.Logger.LogInfo($"{collision.gameObject.name}");
            // if (collision.gameObject.layer == LayerMask.NameToLayer("vehicle")
            //     ||  (collision.gameObject.layer == LayerMask.NameToLayer("item") && collision.gameObject.GetComponentInParent<Vagon>() != null))
            // // if (collision.gameObject.GetComponentInParent<Vagon>() != null)
            // {
            //     Physics.IgnoreCollision(collision.gameObject.GetComponent<Collider>(), GetComponent<Collider>());
            //     return;
            // }
        }

        public void Kill()
        {
            if (TryGetComponent(out Character character))
            {
                if (!character.IsDead()) character.SetHealth(0);
            }
            else
            {
                Logger.LogError($"Cannot kill {name} because it has no Character component.");
            }
        }

        #region MinionMasterZDO
        public string UndeadMinionMaster
        {
            get => TryGetComponent(out ZNetView zNetView) ? zNetView.GetZDO().GetString(MinionOwnershipZdoKey) : "";
            set
            {
                if (TryGetComponent(out ZNetView zNetView))
                {
                    zNetView.GetZDO().Set(MinionOwnershipZdoKey, value);
                }
                else
                {
                    Logger.LogError($"Cannot SetUndeadMinionMaster to {value} because it has no ZNetView component.");
                }
            }
        }

        public bool BelongsToPlayer(string playerName)
        {
            return TryGetComponent(out ZNetView zNetView) 
                   && zNetView.GetZDO().GetString(MinionOwnershipZdoKey, "")
                       .ToLower()
                       .Trim()
                       .Equals(playerName.ToLower().Trim());
        }
        #endregion
        #region DropsZDO
        public void RecordDrops(CharacterDrop characterDrop)
        {
            // the component won't be remembered by the game on logout because
            // only what is on the prefab is remembered. Even changes to the prefab
            // aren't remembered. So we must write what we're dropping into
            // the ZDO as well and then read & restore this on Awake
            if (TryGetComponent(out ZNetView zNetView))
            {
                var dropsList = "";
                var drops = new List<string>();
                characterDrop.m_drops.ForEach(drop => drops.Add($"{drop.m_prefab.name}:{drop.m_amountMax}"));
                dropsList = string.Join(",", drops);
                zNetView.GetZDO().Set(MinionDropsZdoKey, string.Join(",", dropsList));
            }
            else
            {
                Logger.LogError($"Cannot record drops because {name} has no ZNetView component.");
            }
        }

        public void RestoreDrops()
        {
            // the component won't be remembered by the game on logout because
            // only what is on the prefab is remembered. Even changes to the prefab
            // aren't remembered. So we must write what we're dropping into
            // the ZDO as well and then read & restore this on Awake
            if (TryGetComponent(out ZNetView zNetView))
            {
                var characterDrop = gameObject.GetComponent<CharacterDrop>(); 
                if (characterDrop == null)
                {
                    characterDrop = gameObject.AddComponent<CharacterDrop>();
                }

                var minionDropsZdoValue = zNetView.GetZDO().GetString(MinionDropsZdoKey, "");
                if (minionDropsZdoValue == "")
                {
                    // abort - there's no drops recorded -> naked minion
                    return;
                }
                
                var dropsList = new List<string>(minionDropsZdoValue.Split(','));
                dropsList.ForEach(dropString =>
                {
                    string[] splut = dropString.Split(':');

                    string prefabName = splut[0];
                    int amount = int.Parse(splut[1]);
                    
                    AddOrUpdateDrop(characterDrop, prefabName, amount);
                });
            }
            else
            {
                Logger.LogError($"Cannot record drops because {name} has no ZNetView component.");
            }
        }
        #endregion
        #region WaitPositionZDO

        public State Status
        {
            get
            {
                var waitPos = GetWaitPosition();
                if (waitPos.Equals(StatusFollowing)) return State.Following;
                return waitPos.Equals(StatusRoaming) ? State.Roaming : State.Waiting;
            }
        }
        protected void RecordWaitPosition(Vector3 waitPos)
        {
            // waitPos == some position = wait at that position
            // waitPos == StatusFollow = follow owner
            // waitPos == StatusRoam = roam
            if (TryGetComponent(out ZNetView zNetView))
            {
                if (!zNetView.IsOwner())
                {
                    zNetView.ClaimOwnership();
                }
                
                zNetView.GetZDO().Set(MinionWaitPosZdoKey, waitPos);
            }
            else
            {
                Logger.LogError($"Cannot RecordWaitPosition {waitPos} because it has no ZNetView component.");
            }
        }

        protected Vector3 GetWaitPosition()
        {
            if (TryGetComponent(out ZNetView zNetView))
            {
                return zNetView.GetZDO().GetVec3(MinionWaitPosZdoKey, StatusRoaming);
            }

            Logger.LogError($"Cannot GetWaitPosition because it has no ZNetView component.");
            return StatusRoaming;
        }

        public void RoamFollowOrWait()
        {
            Vector3 waitPos = GetWaitPosition();
            // we cant compare negative infinity with == because unity's == returns true for vectors that are almost
            // equal.
            if (waitPos.Equals(StatusFollowing))
            {
                // Try to find player that minion belongs to. If found, follow. Otherwise roam
                Player player = Player.GetAllPlayers().Find(p => BelongsToPlayer(p.GetPlayerName()));
                if (player == null)
                {
                    Logger.LogError($"{name} should be following but has no associated player. Roaming instead.");
                    Roam();
                    return;
                }
                Follow(player.gameObject);
                return;
            }
            
            if (waitPos.Equals(StatusRoaming))
            {
                Roam();
                return;
            }

            if (!TryGetComponent(out MonsterAI monsterAI))
            {
                Logger.LogError($"{name} cannot WaitAtRecordedPosition because it has no MonsterAI component.");
                return;
            }

            // create a temporary object. This has no ZDO so will be cleaned up
            // after the session ends
            var waitObject = new GameObject(MinionWaitObjectName);
            waitObject.transform.position = waitPos;
            monsterAI.SetFollowTarget(waitObject);
        }
        #endregion

        public void Follow(GameObject followObject)
        {
            if (!TryGetComponent(out MonsterAI monsterAI))
            {
                Logger.LogError($"Cannot Follow because it has no MonsterAI component.");
                return;
            }
            // clear out current wait object if it exists
            var currentFollowTarget = monsterAI.GetFollowTarget();
            if (currentFollowTarget != null && currentFollowTarget.name == MinionWaitObjectName)
            {
                Destroy(currentFollowTarget);
            }
            // follow
            RecordWaitPosition(StatusFollowing);
            monsterAI.SetFollowTarget(followObject);
        }

        public void Wait(Vector3 waitPosition)
        {
            RecordWaitPosition(waitPosition);
            RoamFollowOrWait();
        }

        public void Roam()
        {
            RecordWaitPosition(StatusRoaming);
            if (!TryGetComponent(out MonsterAI monsterAI))
            {
                Logger.LogError($"Cannot Roam because {name} has no MonsterAI component!");
                return;
            }
            // clear out current wait object if it exists
            GameObject currentFollowTarget = monsterAI.GetFollowTarget();
            if (currentFollowTarget != null && currentFollowTarget.name == MinionWaitObjectName)
            {
                Destroy(currentFollowTarget);
            }
            monsterAI.SetFollowTarget(null);
        }

        public static T FindClosest<T>(Transform targetTransform, float radius, int mask, Func<T, bool> where, bool interactable) where T : Component
        {
            return Physics.OverlapSphere(targetTransform.position, radius, mask)
                .Where(c => c.GetComponentInParent<T>() != null) // check if desired component exists
                .Select(c => c.GetComponentInParent<T>()) // get the component we want (e.g. ItemDrop)
                .Where(c => !interactable || (c.TryGetComponent(out ZNetView znv) && znv.IsValid())) // only interactable objects
                .Where(where) // allow the caller to specify additional constraints (e.g. drop => drop.GetTimeSinceSpawned() > 4)
                .OrderBy(t => Vector3.Distance(t.transform.position, targetTransform.position)) // sort to find closest
                .FirstOrDefault(); // return closest
        }
        
        public static List<T> FindNearby<T>(Transform targetTransform, float radius, int mask, Func<T, bool> where, bool interactable) where T : Component
        {
            return Physics.OverlapSphere(targetTransform.position, radius, mask)
                .Where(c => c.GetComponentInParent<T>() != null) // check if desired component exists
                .Select(c => c.GetComponentInParent<T>()) // get the component we want (e.g. ItemDrop)
                .Where(c => !interactable ||
                            (c.TryGetComponent(out ZNetView znv) && znv.IsValid())) // only interactable objects
                .Where(where) // allow the caller to specify additional constraints (e.g. drop => drop.GetTimeSinceSpawned() > 4)
                .ToList();
        }

        public static void AddOrUpdateDrop(CharacterDrop characterDrop, string prefabName, int amount)
        {
            var existing = characterDrop.m_drops.FirstOrDefault(drop => drop.m_prefab.name.Equals(prefabName));
            if (existing != null)
            {
                existing.m_amountMin = amount;
                existing.m_amountMax = amount;
                existing.m_chance = 1f;
            }
            else
            {
                characterDrop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab = ZNetScene.instance.GetPrefab(prefabName),
                    m_onePerPlayer = true,
                    m_amountMin = amount,
                    m_amountMax = amount,
                    m_chance = 1f
                });
            }
        }
    }
}
