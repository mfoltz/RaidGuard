using HarmonyLib;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Gameplay.Systems;
using ProjectM.Network;
using RaidGuard.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class StatChangeSystemPatches
{
    static readonly PrefabGUID siegeGolem = new(914043867);

    static readonly bool PlayerAlliances = Plugin.Alliances.Value;
    static readonly bool PreventFriendlyFire = Plugin.PreventFriendlyFire.Value;
    static readonly bool DamageLock = Plugin.BlockOutsideDamage.Value;
    static readonly bool GolemLock = Plugin.GolemGuard.Value;

    static readonly int GolemAttackProtect = Plugin.GolemHitAttackProtection.Value;
    static readonly int GolemBreachedProtect = Plugin.GolemHitBreachedProtection.Value;

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

                // Each time the golem hits castle, refresh his Golem Guard
                if (GolemLock && Core.ServerGameManager.TryGetBuff(dealDamageEvent.SpellSource, siegeGolem.ToIdentifier(), out Entity buff))
                {
                    if (dealDamageEvent.SpellSource.TryGetComponent(out EntityOwner entityOwner) && entityOwner.Owner.TryGetComponent(out PlayerCharacter source))
                    {
                        if (RaidService.IsInRaidCheck(entityOwner.Owner))
                        {
                            RaidService.ProtectRaidGolem(entityOwner.Owner, buff, GolemBreachedProtect); // Breached Per Hit Time
                        }
                        else
                        {
                            if (dealDamageEvent.Target.TryGetComponent(out CastleHeartConnection heartConnection))
                            {
                                GolemService.ProtectGolem(buff, heartConnection.CastleHeartEntity._Entity.Read<UserOwner>().Owner._Entity, GolemAttackProtect); // Pre-Breached Per Hit Time
                            }
                        }
                    }
                }

                // Check to see if a golem can be hit by this person
                if (GolemLock && dealDamageEvent.SpellSource.TryGetComponent(out EntityOwner attacker) && dealDamageEvent.Target.TryGetComponent(out PlayerCharacter t))
                {
                    if (Core.ServerGameManager.TryGetBuff(dealDamageEvent.Target, siegeGolem.ToIdentifier(), out Entity buf))
                    {
                        if (RaidService.IsRaidGolemProtected(buf)) // Does this golem have active protection as a member of a raid?
                        {
                            if (RaidService.ThirdPartyCheck(t.UserEntity, attacker.Owner))
                            {
                                Core.EntityManager.DestroyEntity(entity); // this is a third party attack on a protected golem, destroy it.
                                continue;
                            }
                        }
                        else if (GolemService.IsProtected(buf, attacker)) // does this golem have protected time outside of raid?
                        {
                            Core.EntityManager.DestroyEntity(entity);
                            continue;
                        }
                    }
                }

                // Prevents third party damage to members of an active raid
                if (DamageLock && dealDamageEvent.Target.TryGetComponent(out PlayerCharacter tar) && dealDamageEvent.SpellSource.TryGetComponent(out EntityOwner owner))
                {
                    if (RaidService.ThirdPartyCheck(tar.UserEntity, owner.Owner))
                    {
                        Core.EntityManager.DestroyEntity(entity);
                        continue;
                    }
                }

                // Prevents friendly fire between alliance/clan members
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
