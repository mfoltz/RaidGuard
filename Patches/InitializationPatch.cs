using HarmonyLib;
using ProjectM.Gameplay.WarEvents;
using Unity.Scenes;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class InitializationPatch
{
    [HarmonyPatch(typeof(SceneSystem), nameof(SceneSystem.ShutdownStreamingSupport))]
    [HarmonyPostfix]
    static void ShutdownStreamingSupportPostfix()
    {
        Core.Initialize();
        if (Core.hasInitialized)
        {
            Core.Log.LogInfo($"|{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] initialized|");
            Plugin.Harmony.Unpatch(typeof(SceneSystem).GetMethod("ShutdownStreamingSupport"), typeof(InitializationPatch).GetMethod("OneShot_AfterLoad_InitializationPatch"));
        }
    }
}