using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ChebsValheimLibrary.Common;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Minions
{
    /// <summary>
    /// The ChebGonazMinion class is the basis for all minions. It serves multiple purposes:
    /// <list type="bullet">
    /// <item>
    /// <description>Identification: To determine whether a creature was created by the mod.</description>
    /// </item>
    /// <item>
    /// <description>Wait/Follow/Roam functionality</description>
    /// </item>
    /// <item>
    /// <description>Ownership functionality</description>
    /// </item>
    /// <item>
    /// <description>Helper methods/functions eg. FindClosest, FindNearby</description>
    /// </item>
    /// </list>
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
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

        /// <summary>
        /// Cleanup enum to determine what happens to a minion.
        /// <list type="bullet">
        /// <item>
        /// <description>None: Default - take no special action.</description>
        /// </item>
        /// <item>
        /// <description>Time: Minions destroyed after an elapsed time.</description>
        /// </item>
        /// <item>
        /// <description>Logout: Minions owned by a player are destroyed when the player logs out.</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum CleanupType
        {
            None,   // no cleanup -> minions remain forever
            Time,   // minions destroyed after a time
            Logout, // minions destroyed on player log out
        }

        /// <summary>
        /// Action to take regarding refunding of resources used to create a minion when it dies.
        /// <list type="bullet">
        /// <item>
        /// <description>Nothing: Generate no CharacterDrop at all.</description>
        /// </item>
        /// <item>
        /// <description>JustResources: Just save the armor type eg. iron ingots.</description>
        /// </item>
        /// <item>
        /// <description>Everything: Drop everything that was used to create the minion eg. bones, meat, iron ingots.</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum DropType
        {
            Nothing,       // generate no CharacterDrop at all
            JustResources, // just save the armor type eg. iron ingots
            Everything,    // drop everything that was used to create the minion eg. bones, meat, iron ingots
        }
        /// <summary>
        /// <list type="bullet">
        /// <item>
        /// <description>Waiting: Minion follows an empty gameobject and thus remains rooted in place.</description>
        /// </item>
        /// <item>
        /// <description>Roaming: Default Valheim MonsterAI behaviour for when no follow object is set.</description>
        /// </item>
        /// <item>
        /// <description>Following: Default valheim MonsterAI behaviour for when a follow object is set.</description>
        /// </item>
        /// </list>
        /// </summary>
        public enum State
        {
            Waiting,   // minion follows an empty gameobject and thus remains rooted in place
            Roaming,   // default valheim MonsterAI behaviour for when no follow is set
            Following, // default valheim MonsterAI behaviour for when follow is set
        }

        /// <summary>
        /// The armor type the minion is equipped with.
        /// </summary>
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

        /// <summary>
        /// Whether the minion can be commanded with E.
        /// </summary>
        public bool canBeCommanded = true;
        /// <summary>
        /// This ZDO entry contains the owner player's name as a string. It won't respond to the commands of other
        /// players.
        /// </summary>
        public const string MinionOwnershipZdoKey = "UndeadMinionMaster";
        /// <summary>
        /// This ZDO entry contains a list of items and their amounts that the minion will drop on death. This is
        /// parsed on the minion's Awake and added to its CharacterDrop component.
        /// </summary>
        public const string MinionDropsZdoKey = "UndeadMinionDrops";
        /// <summary>
        /// This ZDO entry contains the location of where the minion has been told to wait.
        /// </summary>
        public const string MinionWaitPosZdoKey = "UndeadMinionWaitPosition";
        /// <summary>
        /// The name given to wait objects.
        /// </summary>
        public const string MinionWaitObjectName = "UndeadMinionWaitPositionObject";

        /// <summary>
        /// The order in which minions have been created. This is useful if a limit is set, so that when the limit is
        /// reached the oldest minions are killed first.
        /// </summary>
        public int createdOrder;

        private static readonly int VehicleLayer = LayerMask.NameToLayer("vehicle");

        /// <summary>
        /// Whether this minion has already dropped its items or not.
        /// </summary>
        public bool ItemsDropped { get; private set; }

        #region DeathCrates
        private static List<Transform> _deathCrates = new();

        /// <summary>
        /// Attempt to deposit the minion's character drop into any nearby crates within range.
        /// </summary>
        /// <param name="characterDrop">The drop to read from.</param>
        /// <param name="range">The range within which to search.</param>
        /// <returns>Returns true if a nearby crate has been found and the items successfully deposited. False if
        /// items remain in the inventory.</returns>
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
        
        /// <summary>
        /// Creates a death crate to deposit items into.
        /// </summary>
        /// <returns>Return the container.</returns>
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
        
        /// <summary>
        /// Determines whether the inventory provided has enough materials for an armor type.
        /// </summary>
        /// <param name="inventory">The inventory to read from.</param>
        /// <param name="armorBlackIronRequired"></param>
        /// <param name="armorIronRequired"></param>
        /// <param name="armorBronzeRequired"></param>
        /// <param name="armorLeatherRequired"></param>
        /// <returns>Returns the armor type.</returns>
        public static ArmorType DetermineArmorType(Inventory inventory, int armorBlackIronRequired, int armorIronRequired, int armorBronzeRequired, int armorLeatherRequired)
        {
            var blackMetalInInventory = inventory.CountItems("$item_blackmetal");
            if (blackMetalInInventory >= armorBlackIronRequired)
            {
                return ArmorType.BlackMetal;
            }
            
            var ironInInventory = inventory.CountItems("$item_iron");
            if (ironInInventory >= armorIronRequired)
            {
                return ArmorType.Iron;
            }
            
            var bronzeInInventory = inventory.CountItems("$item_bronze");
            if (bronzeInInventory >= armorBronzeRequired)
            {
                return ArmorType.Bronze;
            }
            
            var trollHideInInventory = inventory.CountItems("$item_trollhide");
            if (trollHideInInventory >= armorLeatherRequired)
            {
                return ArmorType.LeatherTroll;
            }
            
            var wolfHideInInventory = inventory.CountItems("$item_wolfpelt");
            if (wolfHideInInventory >= armorLeatherRequired)
            {
                return ArmorType.LeatherWolf;
            }
            
            var loxHideInInventory = inventory.CountItems("$item_loxpelt");
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
        
        #region CanSpawn
        /// <summary>
        /// Checks if inventory has enough requirements to create the minion.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
        /// <param name="message">Message containing a reason for failure eg. not enough this, or that.</param>
        /// <returns>Returns true if minion can be spawned, otherwise false.</returns>
        public static bool CanSpawn(MemoryConfigEntry<string, List<string>> itemsCost, Inventory inventory,
            out string message)
        {
            var itemCostsList = itemsCost.Value;
            return CanSpawn(itemCostsList, inventory, out message);
        }

        /// <summary>
        /// Checks if inventory has enough requirements to create the minion.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
        /// <param name="message">Message containing a reason for failure eg. not enough this, or that.</param>
        /// <returns>Returns true if minion can be spawned, otherwise false.</returns>
        public static bool CanSpawn(string itemsCost, Inventory inventory,
            out string message)
        {
            var itemCostsList = itemsCost?.Split(',').ToList();
            return CanSpawn(itemCostsList, inventory, out message);
        }
        
        /// <summary>
        /// Checks if inventory has enough requirements to create the minion.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
        /// <param name="message">Message containing a reason for failure eg. not enough this, or that.</param>
        /// <returns>Returns true if minion can be spawned, otherwise false.</returns>
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
                if (!int.TryParse(splut[1], out var itemAmountRequired))
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
        #endregion
        
        #region ConsumeRequirements
        /// <summary>
        /// Consume the requirements from the inventory provided.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
        public static void ConsumeRequirements(MemoryConfigEntry<string, List<string>> itemsCost, Inventory inventory)
        {
            var itemCostsList = itemsCost.Value;
            ConsumeRequirements(itemCostsList, inventory);
        }

        /// <summary>
        /// Consume the requirements from the inventory provided.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
        public static void ConsumeRequirements(string itemsCost, Inventory inventory)
        {
            var itemCostsList = itemsCost?.Split(',').ToList();
            ConsumeRequirements(itemCostsList, inventory);
        }
        
        /// <summary>
        /// Consume the requirements from the inventory provided.
        /// </summary>
        /// <param name="itemsCost"></param>
        /// <param name="inventory"></param>
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
                if (!int.TryParse(splut[1], out var itemAmountRequired))
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
        #endregion
        
        #region GenerateDeathDrops
        /// <summary>
        /// Generate death drops.
        /// </summary>
        /// <param name="characterDrop"></param>
        /// <param name="itemsCost"></param>
        protected static void GenerateDeathDrops(CharacterDrop characterDrop, MemoryConfigEntry<string, List<string>> itemsCost)
        {
            var itemCostsList = itemsCost.Value;
            GenerateDeathDrops(characterDrop, itemCostsList);
        }

        /// <summary>
        /// Generate death drops.
        /// </summary>
        /// <param name="characterDrop"></param>
        /// <param name="itemsCost"></param>
        protected static void GenerateDeathDrops(CharacterDrop characterDrop, string itemsCost)
        {
            var itemCostsList = itemsCost?.Split(',').ToList();
            GenerateDeathDrops(characterDrop, itemCostsList);
        }
        
        /// <summary>
        /// Generate death drops.
        /// </summary>
        /// <param name="characterDrop"></param>
        /// <param name="itemsCost"></param>
        protected static void GenerateDeathDrops(CharacterDrop characterDrop, List<string> itemsCost)
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
                if (!int.TryParse(splut[1], out var itemAmountRequired))
                {
                    Logger.LogError("Error in config for ItemsCost - please revise.");
                    return;
                }
                
                var acceptedItems = itemRequired.Split('|');
                foreach (var acceptedItem in acceptedItems)
                {
                    var requiredItemPrefab = ZNetScene.instance.GetPrefab(acceptedItem);
                    if (requiredItemPrefab == null)
                    {
                        Logger.LogError($"Error processing config for ItemsCost: {acceptedItem} doesn't exist.");
                        return;
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
        }
        
        #endregion
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

        /// <summary>
        /// Kill the minion.
        /// </summary>
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
        /// <summary>
        /// Set/get the minion's master. Takes/returns a player's name.
        /// <example>
        /// <code>if (humanMinion.UndeadMinionMaster == "") humanMinion.UndeadMinionMaster = player.GetPlayerName();</code>
        /// </example>
        /// </summary>
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

        /// <summary>
        /// Check if minion belongs to player.
        /// </summary>
        /// <param name="playerName"></param>
        /// <returns>True if belongs to player, otherwise false.</returns>
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
        /// <summary>
        /// Save the contents of a CharacterDrop to the ZDO.
        /// </summary>
        /// <param name="characterDrop"></param>
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

        /// <summary>
        /// Reads drops from ZDO and add them back to the CharacterDrop.
        /// </summary>
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
                    var splut = dropString.Split(':');

                    var prefabName = splut[0];
                    var amount = int.Parse(splut[1]);
                    
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

        /// <summary>
        /// Get/set the minion's waiting position.
        /// </summary>
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

        /// <summary>
        /// Read the minion's state from the ZDO and take the correct action based off what's written there: either roam,
        /// follow, or wait.
        /// </summary>
        public void RoamFollowOrWait()
        {
            var waitPos = GetWaitPosition();
            // we cant compare negative infinity with == because unity's == returns true for vectors that are almost
            // equal.
            if (waitPos.Equals(StatusFollowing))
            {
                // Try to find player that minion belongs to. If found, follow. Otherwise roam
                var player = Player.GetAllPlayers().Find(p => BelongsToPlayer(p.GetPlayerName()));
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

        /// <summary>
        /// Instruct the minion to follow an object. Records state as following.
        /// </summary>
        /// <param name="followObject"></param>
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

        /// <summary>
        /// Instruct the minion to wait at a position. Records state as waiting and stores wait position.
        /// </summary>
        /// <param name="waitPosition"></param>
        public void Wait(Vector3 waitPosition)
        {
            RecordWaitPosition(waitPosition);
            RoamFollowOrWait();
        }

        /// <summary>
        /// Instruct minion to roam. Records state as roaming.
        /// </summary>
        public void Roam()
        {
            RecordWaitPosition(StatusRoaming);
            if (!TryGetComponent(out MonsterAI monsterAI))
            {
                Logger.LogError($"Cannot Roam because {name} has no MonsterAI component!");
                return;
            }
            // clear out current wait object if it exists
            var currentFollowTarget = monsterAI.GetFollowTarget();
            if (currentFollowTarget != null && currentFollowTarget.name == MinionWaitObjectName)
            {
                Destroy(currentFollowTarget);
            }
            monsterAI.SetFollowTarget(null);
        }

        /// <summary>
        /// Find the closest object of a type and return it
        /// <example>
        /// An example of a Neckro Gatherer minion finding nearby containers.
        /// <code>
        /// var closestContainer = FindClosest&lt;Container&gt;(transform, DropoffPointRadius.Value, pieceMask,
        ///     c => c.m_piece != null
        ///     && c.m_piece.IsPlacedByPlayer() 
        ///     && allowedContainers.Contains(c.m_piece.m_nview.GetPrefabName())
        ///     && c.GetInventory() != null
        ///     && c.GetInventory().GetEmptySlots() > 0, true);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="radius"></param>
        /// <param name="mask"></param>
        /// <param name="where"></param>
        /// <param name="interactable"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
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
        
        /// <summary>
        /// Similar to FindNearby, but returns all objects of a type as a list.
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="radius"></param>
        /// <param name="mask"></param>
        /// <param name="where"></param>
        /// <param name="interactable"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
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

        /// <summary>
        /// Update an existing character drop to add an item and amount.
        /// </summary>
        /// <param name="characterDrop"></param>
        /// <param name="prefabName"></param>
        /// <param name="amount"></param>
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
