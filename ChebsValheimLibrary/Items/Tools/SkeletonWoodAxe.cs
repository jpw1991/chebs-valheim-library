using Jotunn.Configs;
using Jotunn.Entities;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Items.Tools
{
    public class SkeletonWoodAxe : Item
    {
        public override string ItemName => "ChebGonaz_SkeletonWoodAxe";
        public override string PrefabName => "ChebGonaz_SkeletonWoodAxe.prefab";
        public override string NameLocalization => "$item_chebgonaz_skeletonaxe_name";
        public override string DescriptionLocalization => "$item_chebgonaz_skeletonaxe_desc";
    }
}