using Jotunn.Configs;
using Jotunn.Entities;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ChebsValheimLibrary.Items.Tools
{
    public class SkeletonPickaxe : Item
    {
        public override string ItemName => "ChebGonaz_SkeletonPickaxe";
        public override string PrefabName => "ChebGonaz_SkeletonPickaxe.prefab";
        public override string NameLocalization => "$item_chebgonaz_skeletonpickaxe_name";
        public override string DescriptionLocalization => "$item_chebgonaz_skeletonpickaxe_desc";
    }
}