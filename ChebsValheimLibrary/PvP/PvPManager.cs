using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;

namespace ChebsValheimLibrary.PvP
{
    /// <summary>
    /// Clients send their PvP wishes to the server and the server receives them, stores them, and sends the information
    /// back (including the other player's settings). This way all clients are informed by the server of each others'
    /// PvP settings.
    ///
    /// <code>
    ///                   ┌───────────┐
    ///                   │ Client 1  │
    ///                   └──────┬────┘
    ///                       ▲  │
    ///                       │  ▼
    /// ┌────┐     ┌──────┐  ┌┴───┐
    /// │Ally├────►│Server│  │RPC ├───►┌───────────┐
    /// │File│     │      ├──┤    │    │ Client 2  │
    /// │    │◄────┤      │  │    │◄───┴───────────┘
    /// └────┘     └──────┘  └────┘
    /// </code>
    /// </summary>
    public class PvPManager
    {
        public static bool HeavyLogging = false;

        private static CustomRPC _pvPrpc;
        private const string GetDictString = "CG_PvP_1";
        private const string UpdateDictString = "CG_PvP_2";
        private const string PvPrpcName = "PvPrpc";

        private static string AllyFileName => $"{ZNet.instance.GetWorldName()}.ChebsValheimLibrary.PvP.json";

        private static Tuple<string, Dictionary<string, List<string>>> _playerFriends;

        private static Dictionary<string, List<string>> PlayerFriends
        {
            // getter logic reloads the file if the world has changed. That way if a player switches worlds the file
            // will be read again. It also ensures no mismatch can be made
            get
            {
                _playerFriends ??= new Tuple<string, Dictionary<string, List<string>>>(ZNet.instance.GetWorldName(),
                    ReadAllyFile());

                return _playerFriends.Item2;
            }
            set => _playerFriends =
                new Tuple<string, Dictionary<string, List<string>>>(ZNet.instance.GetWorldName(), value);
        }

        /// <summary>
        /// Get a list of the local player's friends.
        /// </summary>
        public static List<string> GetPlayerFriends()
        {
            return PlayerFriends.TryGetValue(Player.m_localPlayer.GetPlayerName(), out List<string> friends)
                ? friends
                : new List<string>();
        }

        /// <summary>
        /// Test whether two entities are considered friends in PvP by comparing their minion master strings.
        /// <example>
        /// Example snippet of implementation:
        /// <code>
        /// var minionAFriendlyToMinionB = PvPManager.Friendly(minionA.UndeadMinionMaster, minionB.UndeadMinionMaster);
        /// var minionBFriendlyToMinionA = PvPManager.Friendly(minionA.UndeadMinionMaster, minionB.UndeadMinionMaster);
        /// var isHostile = !(minionAFriendlyToMinionB &amp;&amp; minionBFriendlyToMinionA);
        /// </code>
        /// </example>
        /// For a real working example, please look at the Cheb's Mercenaries or Cheb's Necromancy source code.
        /// </summary>
        public static bool Friendly(string minionMasterA, string minionMasterB)
        {
            return PlayerFriends.TryGetValue(minionMasterA, out List<string> friends)
                   && friends.Contains(minionMasterB);
        }

        /// <summary>
        /// Call once during your mod's Awake phase.
        /// </summary>
        public static void ConfigureRPC()
        {
            // can be called multiple times - Jotunn only makes it once and returns the existing one if it's
            // already there
            _pvPrpc = NetworkManager.Instance.AddRPC(PvPrpcName, PvP_RPCServerReceive, PvP_RPCClientReceive);
        }

        private static void UpdateAllyFile(string content)
        {
            // only used by server, clients just use their in-memory dictionary.
            var filePath = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), AllyFileName);

            if (!File.Exists(filePath))
            {
                try
                {
                    using var fs = File.Create(filePath);
                    fs.Close();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error creating {filePath}: {ex.Message}");
                }
            }

            try
            {
                using var writer = new StreamWriter(filePath, false);
                writer.Write(content);
                writer.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error writing to {filePath}: {ex.Message}");
            }
        }

        private static Dictionary<string, List<string>> ReadAllyFile()
        {
            // only used by server, clients just use their in-memory dictionary.
            var filePath = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), AllyFileName);

            if (!File.Exists(filePath))
            {
                try
                {
                    using var fs = File.Create(filePath);
                    fs.Close();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error creating {filePath}: {ex.Message}");
                }
            }

            string content = null;
            try
            {
                using var reader = new StreamReader(filePath);
                content = reader.ReadToEnd();
                reader.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading from {filePath}: {ex.Message}");
            }

            if (content == null)
            {
                Logger.LogError($"Error reading {filePath}: content is null!");
                return new Dictionary<string, List<string>>();
            }

            return content == ""
                ? new Dictionary<string, List<string>>()
                : SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, List<string>>>(content);
        }

        /// <summary>
        /// Update the server's dictionary of PvP friends for the local player.
        /// <example>
        /// Snippet taken from Cheb's Mercenaries command for adding a friend via console.
        /// <code>
        /// var playerNames = args.Select(s =&gt; s.Trim()).ToList();
        /// var friends = PvPManager.GetPlayerFriends();
        /// foreach (var playerName in playerNames)
        /// {
        ///     if (!friends.Contains(playerName))
        ///     {
        ///         friends.Add(playerName);
        ///     }
        /// } 
        /// PvPManager.UpdatePlayerFriendsDict(friends);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="list"></param>
        public static void UpdatePlayerFriendsDict(List<string> list)
        {
            UpdatePlayerFriendsDict(string.Join(",", list.Select(s => s.Trim())));
        }

        private static void UpdatePlayerFriendsDict(string list)
        {
            if (Player.m_localPlayer == null)
            {
                Logger.LogWarning($"UpdatePlayerFriendsDict m_localPlayer is null");
                return;
            }
            var content = $"{UpdateDictString};{Player.m_localPlayer.GetPlayerName()};{list}";
            var package = new ZPackage(Encoding.UTF8.GetBytes(content));
            _pvPrpc.SendPackage(ZRoutedRpc.instance.GetServerPeerID(), package);
        }

        private static IEnumerator PvP_RPCServerReceive(long sender, ZPackage package)
        {
            if (ZNet.instance == null) yield return null;
            if (ZNet.instance.IsServerInstance() || ZNet.instance.IsLocalInstance())
            {
                var payload = package.GetArray();
                var payloadDecoded = Encoding.UTF8.GetString(payload);
                if (payloadDecoded.StartsWith(GetDictString))
                {
                    if (HeavyLogging) Logger.LogInfo($"PvP_RPCServerReceive {GetDictString}");
                    var serializedDict = SimpleJson.SimpleJson.SerializeObject(PlayerFriends.ToDictionary(
                        kvp => kvp.Key, kvp => (object)kvp.Value));
                    _pvPrpc.SendPackage(sender,
                        new ZPackage(Encoding.UTF8.GetBytes(GetDictString + ";" + serializedDict)));
                }
                else if (payloadDecoded.StartsWith(UpdateDictString))
                {
                    if (HeavyLogging) Logger.LogInfo($"PvP_RPCServerReceive {UpdateDictString}");
                    var split = payloadDecoded.Split(';');
                    if (split.Length != 3)
                    {
                        Logger.LogError($"Failed to parse payload ({split.Length})");
                    }

                    var senderNameString = split[1];
                    var friendsString = split[2];

                    var friendsList = friendsString.Split(',');
                    PlayerFriends[senderNameString] = friendsList.ToList();

                    // update all connected peers with the new dictionary
                    var serializedDict = SimpleJson.SimpleJson.SerializeObject(PlayerFriends.ToDictionary(
                        kvp => kvp.Key, kvp => (object)kvp.Value));
                    var returnPayload = GetDictString + ";" + serializedDict;
                    if (HeavyLogging)
                        Logger.LogMessage(
                            $"PvP_RPCServerReceive {UpdateDictString} sending to all peers: {returnPayload}");
                    _pvPrpc.SendPackage(ZNet.instance.m_peers, new ZPackage(Encoding.UTF8.GetBytes(returnPayload)));

                    UpdateAllyFile(serializedDict);
                }
            }

            yield return null;
        }

        private static IEnumerator PvP_RPCClientReceive(long sender, ZPackage package)
        {
            var payload = package.GetArray();
            if (payload.Length > 0)
            {
                var decoded = Encoding.UTF8.GetString(payload);
                if (decoded.StartsWith(GetDictString))
                {
                    if (HeavyLogging) Logger.LogInfo($"PvP_RPCClientReceive decoded: {decoded}");
                    var split = decoded.Split(';');
                    if (split.Length != 2)
                    {
                        Logger.LogError($"Failed to parse payload ({split.Length})");
                    }

                    var data = split[1];
                    var serialized = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, List<string>>>(data);
                    PlayerFriends = serialized;
                }
            }
            else if (HeavyLogging) Logger.LogInfo($"PvP_RPCClientReceive received no data");

            yield return null;
        }
    }
}