using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using RaidGuard.Services;
using ProjectM;
using ProjectM.Network;
using ProjectM.Physics;
using ProjectM.Scripting;
using ProjectM.Shared.Systems;
using RaidGuard.Systems;
using Stunlock.Core;
using System.Collections;
using System.Text.Json;
using Unity.Entities;
using UnityEngine;
using static RaidGuard.Core.DataStructures;

namespace RaidGuard;
internal static class Core
{
    public static World Server { get; } = GetWorld("Server") ?? throw new Exception("There is no Server world (yet)...");
    public static EntityManager EntityManager { get; } = Server.EntityManager;
    public static PrefabCollectionSystem PrefabCollectionSystem { get; internal set; }
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; internal set; }
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static DebugEventsSystem DebugEventsSystem { get; internal set; }
    public static ModifyUnitStatBuffSystem_Spawn ModifyUnitStatBuffSystem_Spawn { get; internal set; }
    public static ReplaceAbilityOnSlotSystem ReplaceAbilityOnSlotSystem { get; internal set; }
    public static EntityCommandBufferSystem EntityCommandBufferSystem { get; internal set; }
    public static ClaimAchievementSystem ClaimAchievementSystem { get; internal set; }
    public static GameDataSystem GameDataSystem { get; internal set; }
    public static PlayerService Players { get; } = new();
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static ScriptSpawnServer ScriptSpawnServer { get; internal set;}
    public static ServerGameSettings ServerGameSettings { get; internal set; }
    public static double ServerTime => ServerGameManager.ServerTime;
    public static ManualLogSource Log => Plugin.LogInstance;

    static MonoBehaviour monoBehaviour;

    public static bool hasInitialized;
    public static void Initialize()
    {
        if (hasInitialized) return;

        // Initialize utility services
        PrefabCollectionSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
        ServerGameSettingsSystem = Server.GetExistingSystemManaged<ServerGameSettingsSystem>();
        DebugEventsSystem = Server.GetExistingSystemManaged<DebugEventsSystem>();
        ServerScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();
        ModifyUnitStatBuffSystem_Spawn = Server.GetExistingSystemManaged<ModifyUnitStatBuffSystem_Spawn>();
        ReplaceAbilityOnSlotSystem = Server.GetExistingSystemManaged<ReplaceAbilityOnSlotSystem>();
        ClaimAchievementSystem = Server.GetExistingSystemManaged<ClaimAchievementSystem>();
        EntityCommandBufferSystem = Server.GetExistingSystemManaged<EntityCommandBufferSystem>();
        GameDataSystem = Server.GetExistingSystemManaged<GameDataSystem>();
        ServerGameSettings = Server.GetExistingSystemManaged<ServerGameSettingsSystem>()._Settings;
        ScriptSpawnServer = Server.GetExistingSystemManaged<ScriptSpawnServer>();

        hasInitialized = true;
    }
    static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name)
            {
                return world;
            }
        }
        return null;
    }
    public static void StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("RaidGuard");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }
    public class DataStructures
    {
        static readonly JsonSerializerOptions prettyJsonOptions = new()
        {
            WriteIndented = true,
            IncludeFields = true
        };

        // structures to write to json for permanence

        static Dictionary<ulong, Dictionary<string, bool>> playerBools = [];

        static Dictionary<ulong, HashSet<string>> playerAlliances = []; // userEntities of players in the same alliance
        public static Dictionary<ulong, Dictionary<string, bool>> PlayerBools
        {
            get => playerBools;
            set => playerBools = value;
        }
        public static Dictionary<ulong, HashSet<string>> PlayerAlliances
        {
            get => playerAlliances;
            set => playerAlliances = value;
        }

        // file paths dictionary

        private static readonly Dictionary<string, string> filePaths = new()
        {
            {"PlayerBools", JsonFiles.PlayerBoolsJson},
            {"PlayerAlliances", JsonFiles.PlayerAlliancesJson},

        };

        // Generic methods to save/load dictionaries
        public static void LoadData<T>(ref Dictionary<ulong, T> dataStructure, string key)
        {
            string path = filePaths[key];
            if (!File.Exists(path))
            {
                // If the file does not exist, create a new empty file to avoid errors on initial load.
                File.Create(path).Dispose();
                dataStructure = []; // Initialize as empty if file does not exist.
                Log.LogInfo($"{key} file created as it did not exist.");
                return;
            }
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    // Handle the empty file case
                    //Log.LogWarning($"{key} data file is empty or contains only whitespace.");
                    dataStructure = []; // Provide default empty dictionary
                }
                else
                {
                    var data = JsonSerializer.Deserialize<Dictionary<ulong, T>>(json, prettyJsonOptions);
                    dataStructure = data ?? []; // Ensure non-null assignment
                }
            }
            catch (IOException ex)
            {
                Log.LogError($"Error reading {key} data from file: {ex.Message}");
                dataStructure = []; // Provide default empty dictionary on error.
            }
            catch (System.Text.Json.JsonException ex)
            {
                Log.LogError($"JSON deserialization error when loading {key} data: {ex.Message}");
                dataStructure = []; // Provide default empty dictionary on error.
            }
        }
        public static void SaveData<T>(Dictionary<ulong, T> data, string key)
        {
            string path = filePaths[key];
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(data, prettyJsonOptions);
                File.WriteAllText(path, json);
                //Core.Log.LogInfo($"{key} data saved successfully.");
            }
            catch (IOException ex)
            {
                Log.LogInfo($"Failed to write {key} data to file: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Log.LogInfo($"JSON serialization error when saving {key} data: {ex.Message}");
            }
        }
        public static void LoadPlayerAlliances() => LoadData(ref playerAlliances, "PlayerAlliances");
        public static void LoadPlayerBools() => LoadData(ref playerBools, "PlayerBools");
        public static void SavePlayerAlliances() => SaveData(PlayerAlliances, "PlayerAlliances");
        public static void SavePlayerBools() => SaveData(PlayerBools, "PlayerBools");
    }
    public static class JsonFiles
    {
        public static readonly string PlayerAlliancesJson = Path.Combine(Plugin.PlayerAlliancesPath, "player_alliances.json");
        public static readonly string PlayerBoolsJson = Path.Combine(Plugin.PlayerAlliancesPath, "player_bools.json");
    }
}




