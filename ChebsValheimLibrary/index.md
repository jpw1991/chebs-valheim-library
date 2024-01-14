
# Cheb's Valheim Library

A library which contains shared classes and things required by all my mods. You're welcome to use it for your own minions too. I hope that you do so that we can create some kind of awesome minions framework.

## Features

- Minion ownership
	+ Minion will only follow and obey its master.
- Minion states
	+ Follow
	+ Wait
	+ Roam
- Creation cost support
- Death drops
- Worker minion AIs for:
	+ Woodcutting
	+ Mining
- PvP
	+ Via the PvP manager, friends/allies are remembered server-side and sent to clients when needed.

## Requirements

- Jotunn

## Quickstart

**Attention:** Assumed prior knowledge of basic Valheim modding, C#, etc.

1. Add the DLL reference to your project.
2. Add a constant to your plugin to check the version against. I recommend a warning or error if the player's version doesn't match:

```cs
    public class BasePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.chebgonaz.chebsmercenaries";
        public const string PluginName = "ChebsMercenaries";
        public const string PluginVersion = "1.3.2";

        public readonly System.Version ChebsValheimLibraryVersion = new("1.2.3");
        
        ...
        
        private void Awake()
        {
            if (!Base.VersionCheck(ChebsValheimLibraryVersion, out string message))
            {
                Jotunn.Logger.LogWarning(message);
            }
            
            ...
```

3. Create a minion class and inherit ChebGonazMinion:

```cs
    public class YourMinion : ChebGonazMinion
    {
       ...
    }
```

4. Apply the component to the prefab after you load it from the asset bundle:

```cs
var prefab = Base.LoadPrefabFromBundle(prefabName, chebgonazAssetBundle, RadeonFriendly.Value);
prefab.AddComponent<YourMinion>();
CreatureManager.Instance.AddCreature(new CustomCreature(prefab, true));
```

5. Or create a harmony patch to apply the component to your minions when the game is loading up:

```cs
    [HarmonyPatch(typeof(MonsterAI))]
    class MonsterAIPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MonsterAI.Awake))]
        static void AwakePostfix(ref Character __instance)
        {
            if (__instance.name.StartsWith("YourMinion"))
            {
                if (!__instance.TryGetComponent(out ChebGonazMinion _))
                {
                    __instance.gameObject.AddComponent<YourMinion>();
                }
            }
        }
    }
```

6. If you fire up the game and go to your minions, it should now be possible to command them to follow, wait, or roam with E.

## Special Thanks

Some people really helped me out with coding this stuff. I'd just like to thank them:

- Developers
	+ [**Dracbjorn**](https://github.com/Dracbjorn) - Extensive work on the configuration files & parsing (including server-sync); general help & testing.
	+ [**CW-Jesse**](https://github.com/CW-Jesse) - Refinements and improvements on networking code and minion AI; general help & testing.
	+ [**WalterWillis**](https://github.com/WalterWillis) - Improvements to Treasure Pylon & Testing.
- Advice & Generic help
    + **Hugo the Dwarf** and **Pfhoenix** for advice and help for fixing the [frozen minions problem](https://github.com/jpw1991/chebs-necromancy/issues/75).
	+ **redseiko** for helpful advice on the Valheim modding Discord.
