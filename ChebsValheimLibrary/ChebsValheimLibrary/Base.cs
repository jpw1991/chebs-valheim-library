using System.Collections.Generic;
using ChebsValheimLibrary.Items;
using ChebsValheimLibrary.Items.Armor.BlackMetal;
using ChebsValheimLibrary.Items.Armor.Bronze;
using ChebsValheimLibrary.Items.Armor.Iron;
using ChebsValheimLibrary.Items.Armor.Leather;
using ChebsValheimLibrary.Items.Armor.Leather.Lox;
using ChebsValheimLibrary.Items.Armor.Leather.Troll;
using ChebsValheimLibrary.Items.Armor.Leather.Wolf;
using ChebsValheimLibrary.Items.Armor.Mage;
using ChebsValheimLibrary.Items.Tools;
using ChebsValheimLibrary.Items.Weapons.BlackMetal;
using ChebsValheimLibrary.Items.Weapons.Bows;
using ChebsValheimLibrary.Items.Weapons.Bronze;
using ChebsValheimLibrary.Items.Weapons.Iron;
using ChebsValheimLibrary.Items.Weapons.Mage;
using ChebsValheimLibrary.Items.Weapons.Needle;
using ChebsValheimLibrary.Items.Weapons.Poison;
using ChebsValheimLibrary.Items.Weapons.Wood;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary
{
    public class Base
    {
        public static readonly System.Version CurrentVersion = new("1.2.1");

        public static bool VersionCheck(System.Version version, out string message)
        {
            if (version.Major != CurrentVersion.Major)
            {
                message = "Major version difference detected! Please check your ChebsValheimLibrary.dll version! " +
                          $"Mod expected {version}, but library version is {CurrentVersion}";
                return false;
            }

            if (version.Minor != CurrentVersion.Minor)
            {
                message = "Minor version difference detected! Please check your ChebsValheimLibrary.dll version! " +
                          $"Mod expected {version}, but library version is {CurrentVersion}";
                return false;
            }

            if (version.Build < CurrentVersion.Build)
            {
                message = "Patch version difference detected. The mod expects an older ChebsValheimLibrary.dll " +
                          "version. This probably won't cause problems." +
                          $"Mod expected {version}, but library version is {CurrentVersion}";
                return false;
            }

            message = "";
            return true;
        }

        
        public static GameObject LoadPrefabFromBundle(string prefabName, AssetBundle bundle, bool radeonFriendly)
        {
            var prefab = bundle.LoadAsset<GameObject>(prefabName);
            if (prefab == null)
            {
                Logger.LogFatal($"LoadPrefabFromBundle: {prefabName} is null!");
            }

            if (radeonFriendly)
            {
                foreach (var child in prefab.GetComponentsInChildren<ParticleSystem>())
                {
                    GameObject.Destroy(child);
                }

                if (prefab.TryGetComponent(out Humanoid humanoid))
                {
                    humanoid.m_deathEffects = new EffectList();
                    humanoid.m_dropEffects = new EffectList();
                    humanoid.m_equipEffects = new EffectList();
                    humanoid.m_pickupEffects = new EffectList();
                    humanoid.m_consumeItemEffects = new EffectList();
                    humanoid.m_hitEffects = new EffectList();
                    humanoid.m_jumpEffects = new EffectList();
                    humanoid.m_slideEffects = new EffectList();
                    humanoid.m_perfectBlockEffect = new EffectList();
                    humanoid.m_tarEffects = new EffectList();
                    humanoid.m_waterEffects = new EffectList();
                    humanoid.m_flyingContinuousEffect = new EffectList();
                }
            }
                    
            return prefab;
        }
        
        public static void LoadMinionItems(AssetBundle bundle, bool radeonFriendly)
        {
            // minion worn items
            List<Item> minionWornItems = new()
            {
                new SkeletonClub(),
                new SkeletonBow(),
                new SkeletonBow2(),
                new SkeletonBow3(),
                new SkeletonHelmetLeather(),
                new SkeletonHelmetBronze(),
                new SkeletonHelmetIron(),
                new SkeletonFireballLevel1(),
                new SkeletonFireballLevel2(),
                new SkeletonFireballLevel3(),
                new SkeletonMageCirclet(),
                new SkeletonAxe(),
                new BlackIronChest(),
                new BlackIronHelmet(),
                new BlackIronLegs(),
                new SkeletonHelmetBlackIron(),
                new SkeletonMace(),
                new SkeletonMace2(),
                new SkeletonMace3(),
                new SkeletonHelmetIronPoison(),
                new SkeletonHelmetBlackIronPoison(),
                new SkeletonHelmetLeatherPoison(),
                new SkeletonHelmetBronzePoison(),
                new SkeletonWoodAxe(),
                new SkeletonPickaxe(),
                new SkeletonAxeBlackMetal(),
                new SkeletonAxeBronze(),
                new SkeletonMaceBlackMetal(),
                new SkeletonMaceBronze(),
                new SkeletonMaceIron(),
                new SkeletonSwordBlackMetal(),
                new SkeletonSwordBronze(),
                new SkeletonSwordIron(),
                new SkeletonBowFire(),
                new SkeletonBowPoison(),
                new SkeletonBowFrost(),
                new SkeletonBowSilver(),
                new SkeletonMaceNeedle(),
                new HelmetLeatherTroll(),
                new HelmetLeatherWolf(),
                new HelmetLeatherLox(),
                new SkeletonHelmetLeatherTroll(),
                new SkeletonHelmetLeatherPoisonTroll(),
                new SkeletonArmorLeatherChestTroll(),
                new SkeletonArmorLeatherLegsTroll(),
                new SkeletonHelmetLeatherWolf(),
                new SkeletonHelmetLeatherPoisonWolf(),
                new SkeletonArmorLeatherChestWolf(),
                new SkeletonArmorLeatherLegsWolf(),
                new SkeletonHelmetLeatherLox(),
                new SkeletonHelmetLeatherPoisonLox(),
                new SkeletonArmorLeatherChestLox(),
                new SkeletonArmorLeatherLegsLox()
            };
            minionWornItems.ForEach(minionItem =>
            {
                var customItem = ItemManager.Instance.GetItem(minionItem.ItemName);
                if (customItem == null)
                {
                    GameObject minionItemPrefab = LoadPrefabFromBundle(minionItem.PrefabName, bundle, radeonFriendly);
                    ItemManager.Instance.AddItem(minionItem.GetCustomItemFromPrefab(minionItemPrefab));
                }
            });
        }
    }
}