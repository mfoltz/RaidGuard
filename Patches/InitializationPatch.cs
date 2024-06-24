using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.WarEvents;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class InitializationPatch
{
    [HarmonyPatch(typeof(WarEventRegistrySystem), nameof(WarEventRegistrySystem.RegisterWarEventEntities))]
    [HarmonyPostfix]
    static void Postfix()
    {
        if (!Core.hasInitialized) Core.Initialize();
        if (Core.hasInitialized)
        {
            Core.Log.LogInfo($"|{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] initialized|");
        }
    }
}