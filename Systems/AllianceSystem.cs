using ProjectM;
using ProjectM.Network;
using RaidGuard.Services;
using Unity.Entities;
using VampireCommandFramework;

namespace RaidGuard.Systems;
public class AllianceSystem
{

    public static bool CheckClanLeadership(ChatCommandContext ctx, Entity ownerClanEntity)
    {
        if (ownerClanEntity.Equals(Entity.Null))
        {
            return true;
        }

        Entity userEntity = ctx.Event.SenderUserEntity;
        return userEntity.TryGetComponent(out ClanRole clanRole) && !clanRole.Value.Equals(ClanRoleEnum.Leader);
    }

    public static void HandleClanAlliance(ChatCommandContext ctx, ulong ownerId, string name)
    {
        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId))
        {
            Core.DataStructures.PlayerAlliances[ownerId] = [];
        }

        HashSet<string> alliance = Core.DataStructures.PlayerAlliances[ownerId];
        HashSet<string> members = [];
        Entity clanEntity = PlayerService.GetClanByName(name);

        if (clanEntity.Equals(Entity.Null))
        {
            HandleReply(ctx, "Clan/leader not found...");
            return;
        }

        if (!TryAddClanMembers(ctx, ownerId, clanEntity, members))
        {
            return;
        }

        AddMembersToAlliance(ctx, alliance, members);
    }
    public static bool TryAddClanMembers(ChatCommandContext ctx, ulong ownerId, Entity clanEntity, HashSet<string> members)
    {
        var clanBuffer = clanEntity.ReadBuffer<ClanMemberStatus>();
        int leaderIndex = GetClanLeaderIndex(clanBuffer);

        if (leaderIndex == -1)
        {
            HandleReply(ctx, "Couldn't find clan leader to verify consent.");
            return false;
        }

        var userBuffer = clanEntity.ReadBuffer<SyncToUserBuffer>();
        for (int i = 0; i < userBuffer.Length; i++)
        {
            var users = userBuffer[i];
            User user = users.UserEntity.Read<User>();

            if (i == leaderIndex && !IsClanLeaderInvitesEnabled(user))
            {
                HandleReply(ctx, "Clan leader does not have alliances invites enabled.");
                return false;
            }
            members.Add(user.CharacterName.Value);
        }
        return true;
    }
    public static void RemoveClanFromAlliance(ChatCommandContext ctx, HashSet<string> alliance, string clanName)
    {
        List<string> removed = [];
        Entity clanEntity = PlayerService.GetClanByName(clanName);

        if (clanEntity.Equals(Entity.Null))
        {
            HandleReply(ctx, "Clan not found...");
            return;
        }

        foreach (string memberName in alliance)
        {
            string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(playerKey) && PlayerService.playerCache.TryGetValue(playerKey, out var player))
            {
                Entity playerClanEntity = player.Read<User>().ClanEntity._Entity;
                ClanTeam clanTeam = playerClanEntity.Read<ClanTeam>();
                if (clanTeam.Name.Value.Equals(clanName, StringComparison.OrdinalIgnoreCase))
                {
                    alliance.Remove(memberName);
                    removed.Add(memberName);
                }
            }
        }
        string replyMessage = removed.Count > 0 ? string.Join(", ", removed.Select(member => $"<color=green>{member}</color>")) : "No members from clan found to remove.";
        if (removed.Count > 0) replyMessage += " removed from alliance.";
        HandleReply(ctx, replyMessage);
        Core.DataStructures.SavePlayerAlliances();
    }
    public static int GetClanLeaderIndex(DynamicBuffer<ClanMemberStatus> clanBuffer)
    {
        for (int i = 0; i < clanBuffer.Length; i++)
        {
            if (clanBuffer[i].ClanRole.Equals(ClanRoleEnum.Leader))
            {
                return i;
            }
        }
        return -1;
    }
    public static bool IsClanLeaderInvitesEnabled(User user)
    {
        return Core.DataStructures.PlayerBools.TryGetValue(user.PlatformId, out var bools) && bools["Grouping"];
    }
    public static void AddMembersToAlliance(ChatCommandContext ctx, HashSet<string> alliance, HashSet<string> members)
    {
        if (members.Count > 0 && alliance.Count + members.Count < Plugin.MaxAllianceSize.Value)
        {
            string membersAdded = string.Join(", ", members.Select(member => $"<color=green>{member}</color>"));
            alliance.UnionWith(members);
            HandleReply(ctx, $"{membersAdded} were added to the alliance.");
            Core.DataStructures.SavePlayerAlliances();
        }
        else if (members.Count == 0)
        {
            HandleReply(ctx, "Couldn't find any clan members to add.");
        }
        else
        {
            HandleReply(ctx, "Alliance would exceed max size by adding found clan members.");
        }
    }
    public static void HandlePlayerAlliance(ChatCommandContext ctx, ulong ownerId, string name)
    {
        string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(playerKey) && PlayerService.playerCache.TryGetValue(playerKey, out Entity player))
        {
            if (player.Equals(Entity.Null))
            {
                HandleReply(ctx, "Player not found...");
                return;
            }

            User foundUser = player.Read<User>();
            if (foundUser.PlatformId == ownerId)
            {
                HandleReply(ctx, "Player not found...");
                return;
            }

            string playerName = foundUser.CharacterName.Value;
            if (IsPlayerEligibleForAlliance(foundUser, ownerId, playerName))
            {
                AddPlayerToAlliance(ctx, ownerId, playerName);
            }
            else
            {
                HandleReply(ctx, $"<color=green>{playerName}</color> does not have alliances enabled or is already in an alliance.");
            }
        }
        else
        {
            HandleReply(ctx, "Player not found...");
        }
    }
    public static bool IsPlayerEligibleForAlliance(User foundUser, ulong ownerId, string playerName)
    {
        if (Core.DataStructures.PlayerBools.TryGetValue(foundUser.PlatformId, out var bools) && bools["Grouping"])
        {
            if (!Core.DataStructures.PlayerAlliances.ContainsKey(foundUser.PlatformId) && (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId) || !Core.DataStructures.PlayerAlliances[ownerId].Contains(playerName)))
            {
                bools["Grouping"] = false;
                Core.DataStructures.SavePlayerBools();
                return true;
            }
        }
        return false;
    }
    public static void AddPlayerToAlliance(ChatCommandContext ctx, ulong ownerId, string playerName)
    {
        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId))
        {
            Core.DataStructures.PlayerAlliances[ownerId] = [];
        }

        string ownerName = ctx.Event.User.CharacterName.Value;
        HashSet<string> alliance = Core.DataStructures.PlayerAlliances[ownerId];

        if (alliance.Count < Plugin.MaxAllianceSize.Value && !alliance.Contains(playerName))
        {
            alliance.Add(playerName);

            if (!alliance.Contains(ownerName)) // add owner to alliance for simplified processing elsewhere
            {
                alliance.Add(ownerName);
            }

            Core.DataStructures.SavePlayerAlliances();
            HandleReply(ctx, $"<color=green>{playerName}</color> added to alliance.");
        }
        else
        {
            HandleReply(ctx, $"Alliance is full or <color=green>{playerName}</color> is already in the alliance.");
        }
    }
    public static void RemovePlayerFromAlliance(ChatCommandContext ctx, HashSet<string> alliance, string playerName)
    {
        string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(playerKey) && alliance.FirstOrDefault(n => n.Equals(playerKey)) != null)
        {
            alliance.Remove(playerKey);
            Core.DataStructures.SavePlayerAlliances();
            HandleReply(ctx, $"<color=green>{char.ToUpper(playerName[0]) + playerName[1..].ToLower()}</color> removed from alliance.");
        }
        else
        {
            HandleReply(ctx, $"<color=green>{char.ToUpper(playerName[0]) + playerName[1..].ToLower()}</color> not found in alliance.");
        }
    }
    public static void ListPersonalAllianceMembers(ChatCommandContext ctx, Dictionary<ulong, HashSet<string>> playerAlliances)
    {
        ulong ownerId = ctx.Event.User.PlatformId;
        string playerName = ctx.Event.User.CharacterName.Value;
        HashSet<string> members = playerAlliances.ContainsKey(ownerId) ? playerAlliances[ownerId] : playerAlliances.Where(groupEntry => groupEntry.Value.Contains(playerName)).SelectMany(groupEntry => groupEntry.Value).ToHashSet();
        string replyMessage = members.Count > 0 ? string.Join(", ", members.Select(member => $"<color=green>{member}</color>")) : "No members in alliance.";
        HandleReply(ctx, replyMessage);
    }
    public static void ListAllianceMembersByName(ChatCommandContext ctx, string name, Dictionary<ulong, HashSet<string>> playerAlliances)
    {
        string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(playerKey) && PlayerService.playerCache.TryGetValue(playerKey, out var player))
        {
            ulong steamId = player.Read<User>().PlatformId;
            string playerName = player.Read<User>().CharacterName.Value;
            HashSet<string> members = playerAlliances.ContainsKey(steamId) ? playerAlliances[steamId] : playerAlliances.Where(groupEntry => groupEntry.Value.Contains(playerName)).SelectMany(groupEntry => groupEntry.Value).ToHashSet();
            string replyMessage = members.Count > 0 ? string.Join(", ", members.Select(member => $"<color=green>{member}</color>")) : "No members in alliance.";
            HandleReply(ctx, replyMessage);
        }
        else
        {
            foreach (var groupEntry in playerAlliances)
            {
                playerKey = groupEntry.Value.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(playerKey))
                {
                    string replyMessage = groupEntry.Value.Count > 0 ? string.Join(", ", groupEntry.Value.Select(member => $"<color=green>{member}</color>")) : "No members in alliance.";
                    HandleReply(ctx, replyMessage);
                    return;
                }
            }
        }
    }
    
}