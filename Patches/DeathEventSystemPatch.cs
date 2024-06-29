using HarmonyLib;
using ProjectM;
using RaidGuard.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class DeathEventListenerSystemPatch
{
    static readonly PrefabGUID siegeGolem = new(914043867);
    static readonly bool RaidGuard = Plugin.RaidGuard.Value;

    [HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostfix(DeathEventListenerSystem __instance)
    {
        NativeArray<DeathEvent> deathEvents = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
        try
        {
            foreach (DeathEvent deathEvent in deathEvents)
            {
                if (!Core.hasInitialized) continue;
                
                if (RaidGuard && deathEvent.Died.Has<AnnounceCastleBreached>() && deathEvent.StatChangeReason.Equals(StatChangeReason.StatChangeSystem_0))
                {
                    if (Core.ServerGameManager.TryGetBuff(deathEvent.Killer, siegeGolem.ToIdentifier(), out Entity buff)) // if this was done by a player with a siege golem buff, start raid service
                    {
                        RaidService.StartRaidMonitor(deathEvent.Killer, deathEvent.Died);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogInfo($"Exited DeathEventListenerSystem hook early: {e}");
        }
        finally
        {
            deathEvents.Dispose();
        }
    } 
}