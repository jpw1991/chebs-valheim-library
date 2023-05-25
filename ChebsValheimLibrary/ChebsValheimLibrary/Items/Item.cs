using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using ChebsValheimLibrary.Common;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Items
{
    /// <summary>
    /// This enum has a readable name eg. Workbench combined with an internal name. The internal name is the only thing
    /// Valheim cares about. Intended use is for config files; where the user can write a human-readable name for a
    /// recipe.
    /// <example>
    /// You can retrieve the internal name like this: 
    /// <code>
    /// var pieceName = InternalName.GetName(CraftingTable.Workbench);
    /// </code>
    /// Which can then be used for setting up recipes with.
    /// </example>
    /// </summary>
    public enum CraftingTable
    {
        None,
        [InternalName("piece_workbench")] Workbench,
        [InternalName("piece_cauldron")] Cauldron,
        [InternalName("forge")] Forge,
        [InternalName("piece_artisanstation")] ArtisanTable,
        [InternalName("piece_stonecutter")] StoneCutter
    }
    
    /// <summary>
    /// The Item class represents a single in-game item eg. a sword and has support for recipes and frame-by-frame
    /// actions. The class is not a Monobehaviour but has a virtual function <c>DoOnUpdate</c> that can be called to
    /// behave similarly to a Monobehaviour. It is indended to be subclassed and have its methods overridden.
    /// </summary>
    public class Item
    {
        /// <summary>
        /// Whether the item is allowed to be crafted or not.
        /// </summary>
        public ConfigEntry<bool> Allowed;

        /// <summary>
        /// The ItemName is the same as the prefab name, but without the .prefab extension. It refers to the item after
        /// it has been loaded by Jotunn's ItemManager, basically.
        /// <example>ChebGonaz_SkeletonWand</example>
        /// </summary>
        public virtual string ItemName => "";
        /// <summary>
        /// The full name of the item as it exists in the asset bundle.
        /// <example>ChebGonaz_SkeletonWand.prefab</example>
        /// </summary>
        public virtual string PrefabName => "";
        /// <summary>
        /// The item's localization string for name.
        /// <example>$item_friendlyskeletonwand</example>
        /// </summary>
        public virtual string NameLocalization => "";
        /// <summary>
        /// The item's localization string for description.
        /// <example>$item_friendlyskeletonwand_desc</example>
        /// </summary>
        public virtual string DescriptionLocalization => "";
        /// <summary>
        /// Create your item's config stuff here.
        /// <example>
        /// Example of overridden method:
        /// <code>
        /// public override void CreateConfigs(BaseUnityPlugin plugin)
        /// {
        ///     base.CreateConfigs(plugin);
        ///
        ///     Allowed = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "OrbOfBeckoningAllowed",
        ///         true, new ConfigDescription("Whether crafting an Orb of Beckoning is allowed or not.", null,
        ///             new ConfigurationManagerAttributes { IsAdminOnly = true }));
        ///
        ///     CraftingStationRequired = plugin.Config.Bind($"{GetType().Name} (Server Synced)", "OrbOfBeckoningCraftingStation",
        ///         CraftingTable.Workbench, new ConfigDescription("Crafting station where it's available", null,
        ///             new ConfigurationManagerAttributes { IsAdminOnly = true }));
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="plugin"></param>
        public virtual void CreateConfigs(BaseUnityPlugin plugin) {}
        /// <summary>
        /// The default recipe with syntax: <code><![CDATA[<Prefab1>:<quantity>[[,<PreFab2>:<quantity>], ...]]]></code>
        /// <example>
        /// <code>Crystal:5,SurtlingCore:5,Tar:25</code>
        /// </example>
        /// </summary>
        protected virtual string DefaultRecipe => "";
        /// <summary>
        /// An open ended function for updating a recipe.
        /// <example>
        /// <code>
        /// public override void UpdateRecipe()
        /// {
        ///     UpdateRecipe(CraftingStationRequired, CraftingCost, CraftingStationLevel);
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public virtual void UpdateRecipe()
        {
            
        }
        /// <summary>
        /// What to do when updating a recipe, but receives config entries as arguments.
        /// </summary>
        /// <param name="craftingStationRequired">The ConfigEntry containing the CraftingTable</param>
        /// <param name="craftingCost">The ConfigEntry containing recipe string</param>
        /// <param name="craftingStationLevel">The crafting station level required to craft the item.</param>
        public virtual void UpdateRecipe(ConfigEntry<CraftingTable> craftingStationRequired, ConfigEntry<string> craftingCost, ConfigEntry<int> craftingStationLevel)
        {
            var recipe = ItemManager.Instance.GetItem(ItemName).Recipe;

            var craftingStationName =
                ((InternalName)typeof(CraftingTable).GetMember(craftingStationRequired.Value.ToString())[0]
                    .GetCustomAttributes(typeof(InternalName)).First()).Name;

            recipe.Recipe.m_minStationLevel = craftingStationLevel.Value;
            
            var sub = ZNetScene.instance.GetPrefab(craftingStationName);
            recipe.Recipe.m_craftingStation = sub.GetComponent<CraftingStation>();
            var newRequirements = new List<Piece.Requirement>();
            foreach (string material in craftingCost.Value.Split(','))
            {
                var materialSplit = material.Split(':');
                var materialName = materialSplit[0];
                var materialAmount = int.Parse(materialSplit[1]);
                newRequirements.Add(new Piece.Requirement()
                {
                    m_amount = materialAmount,
                    m_amountPerLevel = materialAmount * 2,
                    m_resItem = ZNetScene.instance.GetPrefab(materialName).GetComponent<ItemDrop>(),
                });
            }

            recipe.Recipe.m_resources = newRequirements.ToArray();
        }
        
        /// <summary>
        /// Method SetRecipeReqs sets the material requirements needed to craft the item via a recipe.
        /// </summary>
        /// <param name="recipeConfig">The Jotunn ItemConfig</param>
        /// <param name="craftingCost">The ConfigEntry containing recipe string</param>
        /// <param name="craftingStationRequired">The ConfigEntry containing the CraftingTable</param>
        /// <param name="craftingStationLevel">The crafting station level required to craft the item.</param>
        protected void SetRecipeReqs(
            ItemConfig recipeConfig,
            ConfigEntry<string> craftingCost, 
            ConfigEntry<CraftingTable> craftingStationRequired,
            ConfigEntry<int> craftingStationLevel
            )
        {

            // function to add a single material to the recipe
            void AddMaterial(string material)
            {
                string[] materialSplit = material.Split(':');
                string materialName = materialSplit[0];
                int materialAmount = int.Parse(materialSplit[1]);
                recipeConfig.AddRequirement(new RequirementConfig(materialName, materialAmount, materialAmount * 2));
            }

            // set the crafting station to craft it on
            recipeConfig.CraftingStation = ((InternalName)typeof(CraftingTable).GetMember(craftingStationRequired.Value.ToString())[0].GetCustomAttributes(typeof(InternalName)).First()).Name;

            // build the recipe. material config format ex: Wood:5,Stone:1,Resin:1
            if (craftingCost.Value.Contains(','))
            {
                string[] materialList = craftingCost.Value.Split(',');
                foreach (string material in materialList)
                {
                    AddMaterial(material);
                }
            }
            else
            {
                AddMaterial(craftingCost.Value);
            }

            // Set the minimum required crafting station level to craft
            recipeConfig.MinStationLevel = craftingStationLevel.Value;
        }


        /// <summary>
        /// The update delay in seconds between DoOnUpdate executions.
        /// </summary>
        protected float DoOnUpdateDelay;
        /// <summary>
        /// Although the Item class is not a Monobehaviour, we may want similar functionality for the item. You can
        /// achieve this here.
        /// <example>
        /// A good example is the Sprectral Shroud which periodically checks for enemies and summons a wraith if one
        /// is nearby.
        /// <code>
        /// public override void DoOnUpdate()
        /// {
        ///     if (SpawnWraith.Value
        ///         && ZInput.instance != null
        ///         && global::Player.m_localPlayer != null)
        ///     {
        ///         if (Time.time > DoOnUpdateDelay)
        ///         {
        ///             GuardianWraithStuff();
        ///
        ///             DoOnUpdateDelay = Time.time + .5f;
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        /// </summary>
        public virtual void DoOnUpdate()
        {
            // Coroutines cause problems and this is not a Monobehaviour, but we may still want some stuff to
            // happen during update.
        }
        /// <summary>
        /// To be used when loading the item's prefab from an asset bundle. It will generate and return the Jotunn
        /// CustomItem object or null on error.
        /// </summary>
        /// <param name="prefab">The prefab from the bundle.</param>
        /// <returns></returns>
        public virtual CustomItem GetCustomItemFromPrefab(GameObject prefab)
        {
            var config = new ItemConfig();
            config.Name = NameLocalization;
            config.Description = DescriptionLocalization;

            var customItem = new CustomItem(prefab, false, config);
            if (customItem.ItemPrefab == null)
            {
                Logger.LogError($"GetCustomItemFromPrefab: {PrefabName}'s ItemPrefab is null!");
                return null;
            }

            return customItem;
        }
    }
}
