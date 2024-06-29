using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class StatChangeSystemPatches
{
    static readonly bool PlayerAlliances = Plugin.Alliances.Value;
    static readonly bool PreventFriendlyFire = Plugin.PreventFriendlyFire.Value;

    [HarmonyPatch(typeof(DealDamageSystem), nameof(DealDamageSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(DealDamageSystem __instance)
    {
        NativeArray<Entity> entities = __instance._Query.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (Entity entity in entities)
            {
                if (!Core.hasInitialized) continue;

                DealDamageEvent dealDamageEvent = entity.Read<DealDamageEvent>();

                if (dealDamageEvent.MainType != MainDamageType.Physical && dealDamageEvent.MainType != MainDamageType.Spell) continue;

                if (PlayerAlliances && PreventFriendlyFire && dealDamageEvent.Target.TryGetComponent(out PlayerCharacter target))
                {
                    if (dealDamageEvent.SpellSource.TryGetComponent(out EntityOwner entityOwner) && entityOwner.Owner.TryGetComponent(out PlayerCharacter source))
                    {
                        Dictionary<ulong, HashSet<string>> playerAlliances = Core.DataStructures.PlayerAlliances;
                        string targetName = target.Name.Value;
                        string sourceName = source.Name.Value;
                        ulong steamId = source.UserEntity.Read<User>().PlatformId;
                        if (playerAlliances.Values.Any(set => set.Contains(targetName) && set.Contains(sourceName)))
                        {
                            Core.EntityManager.DestroyEntity(entity);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogInfo(ex);
        }
        finally
        {
            entities.Dispose();
        }
    }
}
