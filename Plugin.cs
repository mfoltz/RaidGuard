using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using VampireCommandFramework;

namespace RaidGuard;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
    Harmony _harmony;
    internal static Plugin Instance { get; private set; }
    public static Harmony Harmony => Instance._harmony;
    public static ManualLogSource LogInstance => Instance.Log;

    public static readonly string ConfigFiles = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);

    // current paths
    public static readonly string PlayerAlliancesPath = Path.Combine(ConfigFiles, "Alliances");

    // config entries
    private static ConfigEntry<bool> _raidGuard;
    private static ConfigEntry<bool> _alliances;
    private static ConfigEntry<bool> _clanBasedAlliances;
    private static ConfigEntry<int> _maxAllianceSize;
    private static ConfigEntry<bool> _preventFriendlyFire;
    private static ConfigEntry<bool> _limitAssists;
    private static ConfigEntry<int> _allianceAssists;

    // public getters, kinda verbose might just get rid of these
    public static ConfigEntry<bool> RaidGuard => _raidGuard;
    public static ConfigEntry<bool> Alliances => _alliances;
    public static ConfigEntry<int> MaxAllianceSize => _maxAllianceSize;
    public static ConfigEntry<bool> ClanBasedAlliances => _clanBasedAlliances;
    public static ConfigEntry<bool> PreventFriendlyFire => _preventFriendlyFire;
    public static ConfigEntry<bool> LimitAssists => _limitAssists;
    public static ConfigEntry<int> AllianceAssists => _allianceAssists;

    public override void Load()
    {
        Instance = this;
        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        InitConfig();
        CommandRegistry.RegisterAll();
        LoadAllData();
        Core.Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] loaded!");
    }
    static void InitConfig()
    {
        foreach (string path in directoryPaths) // make sure directories exist
        {
            CreateDirectories(path);
        }

        _raidGuard = InitConfigEntry("Config", "RaidGuard", false, "Enable or disable the prevention of raid interference (only territory clan members and raiding clan members are allowed in territory for duration of the raid once breach by raiders is detected).");
        _alliances = InitConfigEntry("Config", "Alliances", false, "Enable or disable the ability to form alliances.");
        _clanBasedAlliances = InitConfigEntry("Config", "ClanBasedAlliances", false, "If true, clan leaders will decide if the entire clan participates in alliances. If false, it will be player-based. (Alliances must be enabled as well).");
        _preventFriendlyFire = InitConfigEntry("Config", "PreventFriendlyFire", false, "True to prevent damage between players in alliances, false to allow.");
        _maxAllianceSize = InitConfigEntry("Config", "MaxAllianceSize", 4, "The maximum number of players allowed in an alliance (clan members of founding alliance member are included automatically regardless if using clan-based alliances or not and do not count towards this number).");
        _limitAssists = InitConfigEntry("Config", "LimitAssists", false, "True to limit the number of assists during a raid (see below config option for how many allowed if this is set to true).");
        _allianceAssists = InitConfigEntry("Config", "AllianceAssists", 4, "The maximum number of alliance members that can enter a raided territory to assist (includes owning clan members. if this is 4 and 2 owning clan members are offline, then the first 2 alliance members to enter the territory can assist without taking damage from RaidGuard).");
    }
    static ConfigEntry<T> InitConfigEntry<T>(string section, string key, T defaultValue, string description)
    {
        // Bind the configuration entry and get its value
        var entry = Instance.Config.Bind(section, key, defaultValue, description);

        // Check if the key exists in the configuration file and retrieve its current value
        var newFile = Path.Combine(Paths.ConfigPath, $"{MyPluginInfo.PLUGIN_GUID}.cfg");

        if (File.Exists(newFile))
        {
            var config = new ConfigFile(newFile, true);
            if (config.TryGetEntry(section, key, out ConfigEntry<T> existingEntry))
            {
                // If the entry exists, update the value to the existing value
                entry.Value = existingEntry.Value;
            }
        }
        return entry;
    }

    static void CreateDirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public override bool Unload()
    {
        Config.Clear();
        _harmony.UnpatchSelf();
        return true;
    }

    static void LoadAllData()
    {
        Core.DataStructures.LoadPlayerBools();
        if (Alliances.Value)
        {
            Core.DataStructures.LoadPlayerAlliances();
        }
    }

    static readonly List<string> directoryPaths =
        [
        ConfigFiles,
        PlayerAlliancesPath
        ];
}