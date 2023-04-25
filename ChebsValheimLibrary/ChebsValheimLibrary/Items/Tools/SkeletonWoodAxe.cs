using Jotunn.Configs;
using Jotunn.Entities;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Items.Tools
{
    public class SkeletonWoodAxe : Item
    {
        public static int ToolTier = 2;
        public override string ItemName => "ChebGonaz_SkeletonWoodAxe";
        public override string PrefabName => "ChebGonaz_SkeletonWoodAxe.prefab";
        public override string NameLocalization => "$item_chebgonaz_skeletonaxe_name";
        public override string DescriptionLocalization => "$item_chebgonaz_skeletonaxe_desc";

        public void SetPrefab(GameObject prefab)
        {
            _prefab = prefab;
        }
        private static GameObject _prefab;
        
        public override CustomItem GetCustomItemFromPrefab(GameObject prefab)
        {
            var config = new ItemConfig();
            config.Name = NameLocalization;
            config.Description = DescriptionLocalization;

            if (prefab.TryGetComponent(out ItemDrop itemDrop))
                itemDrop.m_itemData.m_shared.m_toolTier = ToolTier;
            
            // keep a reference to prefab for later use
            _prefab = prefab;

            var customItem = new CustomItem(prefab, false, config);
            if (customItem.ItemPrefab == null)
            {
                Logger.LogError($"GetCustomItemFromPrefab: {PrefabName}'s ItemPrefab is null!");
                return null;
            }

            return customItem;
        }
        
        public static void SyncInternalsWithConfigs(int toolTier)
        {
            // This is used for when the config file in one of the mods is updated to sync the static values with
            // whatever's in the config.
            //
            // For example: User sets SkeletonPickaxe's ToolTier in Cheb's Necromancy config to 2. This needs to be
            // reflected into SkeletonPickaxe.ToolTier. Ideas welcome for how to improve this because I'm not really
            // sure if this is the right way to go about it. But it works.
            ToolTier = toolTier;
            if (_prefab == null)
            {
                Logger.LogError("SkeletonWoodAxe.SyncInternalsWithConfigs: _prefab is null!");
                return;
            }
            _prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_toolTier = toolTier;
        }
    }
}