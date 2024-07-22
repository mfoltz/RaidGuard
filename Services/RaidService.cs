using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using ProjectM.Scripting;
using Stunlock.Core;
using System.Collections;
using Unity.Entities;

namespace RaidGuard.Services;
internal class RaidService
{
    static readonly bool PlayerAlliances = Plugin.Alliances.Value;
    static readonly bool LimitAssists = Plugin.LimitAssists.Value;
    static readonly bool LockParticipants = Plugin.LockParticipants.Value;
    static readonly int Assists = Plugin.AllianceAssists.Value;
    static EntityManager EntityManager => Core.EntityManager;
    static DebugEventsSystem DebugEventsSystem => Core.DebugEventsSystem;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;

    static readonly PrefabGUID debuff = new(-1572696947);

    static Dictionary<Entity, (HashSet<Entity> Allowed, List<Entity> AllowedAllies, List<Entity> AllowedRaiders, List<Entity> ActiveAllies, List<Entity> ActiveRaiders)> Participants = []; // castleHeart entity and players allowed in territory for the raid (owner clan, raiding clan, alliance members if applicable)
    static Dictionary<ulong, HashSet<string>> Alliances => Core.DataStructures.PlayerAlliances;

    static bool active = false;
    static DateTime lastMessage = DateTime.MinValue;
    public static void StartRaidMonitor(Entity raider, Entity breached)
    {
        Entity heartEntity = breached.Has<CastleHeartConnection>() ? breached.Read<CastleHeartConnection>().CastleHeartEntity._Entity : Entity.Null;

        if (!active) // if not active start monitor loop after clearing caches
        {
            Core.Log.LogInfo("Starting raid monitor...");
            Participants.Clear(); // clear previous raid participants, this should be empty here anyway but just incase
            AddRaidParticipants(heartEntity, raider, breached);
            Core.StartCoroutine(RaidMonitor());
        }
        else if (active) // if active update onlinePlayers and add new territory participants
        {
            AddRaidParticipants(heartEntity, raider, breached);
        }
    }
    static void AddRaidParticipants(Entity heartEntity, Entity raider, Entity breached)
    {
        if (!heartEntity.Equals(Entity.Null))
        {
            Participants.TryAdd(heartEntity, (
                GetAllowedParticipants(raider, breached),
                GetAllowedAllies(breached),
                GetAllowedRaiders(raider),
                new List<Entity>(),
                new List<Entity>()
            ));
        }
    }
    static List<Entity> GetAllowedAllies(Entity breached) => GetEntities(breached.Read<CastleHeartConnection>().CastleHeartEntity._Entity.Read<UserOwner>().Owner._Entity);
    static List<Entity> GetAllowedRaiders(Entity raider) => GetEntities(raider.Read<PlayerCharacter>().UserEntity);
    static HashSet<Entity> GetAllowedParticipants(Entity raider, Entity breached)
    {
        var allowedParticipants = new HashSet<Entity>();
        allowedParticipants.UnionWith(GetEntities(breached.Read<UserOwner>().Owner._Entity));
        allowedParticipants.UnionWith(GetEntities(raider.Read<PlayerCharacter>().UserEntity));

        return allowedParticipants;
    }
    static List<Entity> GetEntities(Entity userEntity)
    {
        HashSet<Entity> entities = [];
        User playerUser = userEntity.Read<User>();
        string playerName = playerUser.CharacterName.Value;

        Entity clanEntity = EntityManager.Exists(playerUser.ClanEntity._Entity) ? playerUser.ClanEntity._Entity : Entity.Null;

        if (!clanEntity.Equals(Entity.Null))
        {
            var userBuffer = clanEntity.ReadBuffer<SyncToUserBuffer>();
            for (int i = 0; i < userBuffer.Length; i++)
            {
                //Core.Log.LogInfo($"Adding {userBuffer[i].UserEntity.Read<User>().CharacterName.Value} to allowed participants...");
                entities.Add(userBuffer[i].UserEntity);
            }
        }
        else
        {
            entities.Add(userEntity);
        }

        if (PlayerAlliances && Alliances.Values.Any(set => set.Contains(playerName)))
        {
            var members = Alliances
                .Where(groupEntry => groupEntry.Value.Contains(playerName))
                .SelectMany(groupEntry => groupEntry.Value)
                .Where(name => PlayerService.playerCache.TryGetValue(name, out var _))
                .Select(name => PlayerService.playerCache[name])
                .ToHashSet();
            entities.UnionWith(members);
        }
        return [.. entities];
    }
    static IEnumerator RaidMonitor()
    {
        active = true;
        yield return null;
        while (true)
        {
            if (Participants.Keys.Count == 0)
            {
                active = false;
                Core.Log.LogInfo("Stopping raid monitor...");
                yield break;
            }
            bool sendMessage = (DateTime.Now - lastMessage).TotalSeconds >= 10;
            List<Entity> heartEntities = [.. Participants.Keys];
            foreach (KeyValuePair<string, Entity> player in PlayerService.playerCache) // validate player presence in raided territories
            {
                Entity userEntity = player.Value;
                User user = userEntity.Read<User>();
                if (!user.IsConnected) continue;
                Entity character = user.LocalCharacter._Entity;
                if (character.TryGetComponent(out TilePosition pos))
                {
                    heartEntities.ForEach(heartEntity =>
                    {
                        CastleHeart castleHeart = heartEntity.Read<CastleHeart>();
                        if (!castleHeart.IsSieged())
                        {
                            Participants.Remove(heartEntity);
                            return;
                        }

                        bool territoryCheck = CastleTerritoryExtensions.IsTileInTerritory(EntityManager, pos.Tile, ref castleHeart.CastleTerritoryEntity, out CastleTerritory _);

                        if (territoryCheck && !Participants[heartEntity].Allowed.Contains(userEntity)) // if not allowed and in territory, debuff
                        {
                            ApplyDebuff(character, userEntity, sendMessage, "You are not allowed in this territory during a raid.");
                        }
                        else if (territoryCheck && LimitAssists && Participants[heartEntity].Allowed.Contains(userEntity)) // if allowed and in territory and LimitAssists and online, add to actives  ADD THIS BACK AFTER TESTING
                        {
                            if (Participants[heartEntity].AllowedAllies.Contains(userEntity))
                            {
                                if (!Participants[heartEntity].ActiveAllies.Contains(userEntity))
                                {
                                    Participants[heartEntity].ActiveAllies.Add(userEntity);
                                    if (Participants[heartEntity].ActiveAllies.IndexOf(userEntity) > Assists - 1) // if latest arrival is greater than allowed assists, debuff them
                                    {
                                        ApplyDebuff(character, userEntity, sendMessage, "You are not allowed in this territory during a raid (maximum allied assists reached).");
                                    }
                                }
                                else if (Participants[heartEntity].ActiveAllies.IndexOf(userEntity) > Assists - 1)
                                {
                                    ApplyDebuff(character, userEntity, sendMessage, "You are not allowed in this territory during a raid (maximum allied assists reached).");
                                }
                            }
                            else if (Participants[heartEntity].AllowedRaiders.Contains(userEntity))
                            {
                                if (!Participants[heartEntity].ActiveRaiders.Contains(userEntity))
                                {
                                    Participants[heartEntity].ActiveRaiders.Add(userEntity);
                                    if (Participants[heartEntity].ActiveRaiders.IndexOf(userEntity) > Assists - 1) // if latest arrival is greater than allowed assists, debuff them
                                    {
                                        ApplyDebuff(character, userEntity, sendMessage, "You are not allowed in this territory during a raid (maximum raider assists reached).");
                                    }
                                }
                                else if (Participants[heartEntity].ActiveRaiders.IndexOf(userEntity) > Assists - 1)
                                {
                                    ApplyDebuff(character, userEntity, sendMessage, "You are not allowed in this territory during a raid (maximum raider assists reached).");
                                }
                            }
                        }
                        else if (!territoryCheck && LimitAssists && Participants[heartEntity].Allowed.Contains(userEntity))
                        {
                            if (Participants[heartEntity].ActiveAllies.Contains(userEntity) && !LockParticipants)
                            {
                                Participants[heartEntity].ActiveAllies.Remove(userEntity);
                            }
                            else if (Participants[heartEntity].ActiveRaiders.Contains(userEntity) && !LockParticipants)
                            {
                                Participants[heartEntity].ActiveRaiders.Remove(userEntity);
                            }
                        }
                    });
                }
                yield return null;
            }
            if (sendMessage) lastMessage = DateTime.Now;
            yield return null;
        }
    }
    static void ApplyDebuff(Entity character, Entity userEntity, bool sendMessage, string message)
    {
        if (!ServerGameManager.TryGetBuff(character, debuff.ToIdentifier(), out Entity _))
        {
            ApplyBuffDebugEvent applyBuffDebugEvent = new()
            {
                BuffPrefabGUID = debuff,
            };
            FromCharacter fromCharacter = new()
            {
                Character = character,
                User = userEntity,
            };

            DebugEventsSystem.ApplyBuff(fromCharacter, applyBuffDebugEvent); // apply green fire to interlopers and block healing

            if (ServerGameManager.TryGetBuff(character, debuff.ToIdentifier(), out Entity debuffEntity))
            {
                debuffEntity.Add<BlockHealBuff>();
                debuffEntity.Write(new BlockHealBuff { PercentageBlocked = 1f });
                if (debuffEntity.TryGetComponent(out LifeTime lifeTime))
                {
                    lifeTime.Duration = 60f;
                    debuffEntity.Write(lifeTime);
                }
                var tickBuffer = debuffEntity.ReadBuffer<CreateGameplayEventsOnTick>();
                CreateGameplayEventsOnTick tickBufferEntry = tickBuffer[0];
                tickBufferEntry.MaxTicks = 60;
                tickBuffer[0] = tickBufferEntry;
                var damageBuffer = debuffEntity.ReadBuffer<DealDamageOnGameplayEvent>();
                DealDamageOnGameplayEvent damageBufferEntry = damageBuffer[0];
                damageBufferEntry.DamageModifierPerHit *= 3f;
                damageBuffer[0] = damageBufferEntry;
            }
        }
        if (sendMessage) ServerChatUtils.SendSystemMessageToClient(EntityManager, userEntity.Read<User>(), message);
    }
}