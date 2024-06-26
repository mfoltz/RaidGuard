using ProjectM;
using ProjectM.Network;
using RaidGuard.Services;
using Unity.Entities;
using VampireCommandFramework;

namespace RaidGuard;
internal static class AllianceUtilities
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
        string ownerName = ctx.Event.User.CharacterName.Value;
        HashSet<string> ownerClanMembers = GetOwnerClanMembers(ctx.Event.User.ClanEntity._Entity);
        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId)) // when first creating also add owner and clan members
        {
            Core.DataStructures.PlayerAlliances[ownerId] = [];
            HashSet<string> newAlliance = Core.DataStructures.PlayerAlliances[ownerId];
            newAlliance.Add(ownerName);
            if (ownerClanMembers.Count > 0)
            {
                newAlliance.UnionWith(ownerClanMembers);
            }
            Core.DataStructures.SavePlayerAlliances();
        }

        HashSet<string> alliance = Core.DataStructures.PlayerAlliances[ownerId];
        HashSet<string> members = [];
        Entity clanEntity = PlayerService.GetClanByName(name);

        if (clanEntity.Equals(Entity.Null))
        {
            ctx.Reply("Clan/leader not found...");
            return;
        }

        if (!TryAddClanMembers(ctx, ownerId, clanEntity, members))
        {
            return;
        }
        
        AddMembersToAlliance(ctx, alliance, members, ownerClanMembers);
    }
    public static bool TryAddClanMembers(ChatCommandContext ctx, ulong ownerId, Entity clanEntity, HashSet<string> members)
    {
        var clanBuffer = clanEntity.ReadBuffer<ClanMemberStatus>();
        int leaderIndex = GetClanLeaderIndex(clanBuffer);

        if (leaderIndex == -1)
        {
            ctx.Reply("Couldn't find clan leader to verify consent.");
            return false;
        }

        var userBuffer = clanEntity.ReadBuffer<SyncToUserBuffer>();
        for (int i = 0; i < userBuffer.Length; i++)
        {
            var users = userBuffer[i];
            User user = users.UserEntity.Read<User>();

            if (i == leaderIndex && !IsLeaderEligibleForAlliance(user, user.CharacterName.Value))
            {
                ctx.Reply("Clan leader does not have alliances invites enabled.");
                return false;
            }
            members.Add(user.CharacterName.Value);
        }
        return true;
    }
    public static HashSet<string> GetOwnerClanMembers(Entity ownerClanEntity)
    {
        HashSet<string> ownerClanMembers = [];
        if (!ownerClanEntity.Equals(Entity.Null))
        {
            var userBuffer = ownerClanEntity.ReadBuffer<SyncToUserBuffer>();
            for (int i = 0; i < userBuffer.Length; i++)
            {
                var users = userBuffer[i];
                User user = users.UserEntity.Read<User>();
                ownerClanMembers.Add(user.CharacterName.Value);
            }
        }
        return ownerClanMembers;
    }
    public static void RemoveClanFromAlliance(ChatCommandContext ctx, HashSet<string> alliance, string clanName)
    {
        List<string> removed = [];
        Entity clanEntity = PlayerService.GetClanByName(clanName);

        if (clanEntity.Equals(Entity.Null))
        {
            ctx.Reply("Clan not found...");
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
        ctx.Reply(replyMessage);
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
    public static bool IsLeaderEligibleForAlliance(User leaderUser, string leaderName)
    {
        if (Core.DataStructures.PlayerBools.TryGetValue(leaderUser.PlatformId, out var bools) && bools["Grouping"])
        {
            if (!Core.DataStructures.PlayerAlliances.ContainsKey(leaderUser.PlatformId) && !Core.DataStructures.PlayerAlliances.Values.Any(alliance => alliance.Equals(leaderName)))
            {
                bools["Grouping"] = false;
                Core.DataStructures.SavePlayerBools();
                return true;
            }
        }
        return false;
    }
    public static void AddMembersToAlliance(ChatCommandContext ctx, HashSet<string> alliance, HashSet<string> members, HashSet<string> ownerClanMembers)
    {
        int currentAllianceSize = alliance.Count - ownerClanMembers.Count;

        if (members.Count > 0 && currentAllianceSize + members.Count < Plugin.MaxAllianceSize.Value)
        {
            string membersAdded = string.Join(", ", members.Select(member => $"<color=green>{member}</color>"));
            alliance.UnionWith(members);
            ctx.Reply($"{membersAdded} were added to the alliance.");
            Core.DataStructures.SavePlayerAlliances();
        }
        else if (members.Count == 0)
        {
            ctx.Reply("Couldn't find any clan members to add.");
        }
        else
        {
            ctx.Reply("Alliance would exceed max size by adding found clan members.");
        }
    }
    public static void HandlePlayerAlliance(ChatCommandContext ctx, ulong ownerId, string name)
    {
        string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(playerKey) && PlayerService.playerCache.TryGetValue(playerKey, out Entity player))
        {
            if (player.Equals(Entity.Null))
            {
                ctx.Reply("Player not found...");
                return;
            }

            User foundUser = player.Read<User>();
            if (foundUser.PlatformId == ownerId)
            {
                ctx.Reply("Player not found...");
                return;
            }

            string playerName = foundUser.CharacterName.Value;
            if (IsPlayerEligibleForAlliance(foundUser, playerName))
            {
                HashSet<string> ownerClanMembers = GetOwnerClanMembers(ctx.Event.User.ClanEntity._Entity);
                AddPlayerToAlliance(ctx, ownerId, playerName, ownerClanMembers);
            }
            else
            {
                ctx.Reply($"<color=green>{playerName}</color> does not have alliances enabled or is already in an alliance.");
            }
        }
        else
        {
            ctx.Reply("Player not found...");
        }
    }
    public static bool IsPlayerEligibleForAlliance(User foundUser, string playerName)
    {
        if (Core.DataStructures.PlayerBools.TryGetValue(foundUser.PlatformId, out var bools) && bools["Grouping"])
        {
            if (!Core.DataStructures.PlayerAlliances.ContainsKey(foundUser.PlatformId) && !Core.DataStructures.PlayerAlliances.Values.Any(alliance => alliance.Equals(playerName)))
            {
                bools["Grouping"] = false;
                Core.DataStructures.SavePlayerBools();
                return true;
            }
        }
        return false;
    }
    public static void AddPlayerToAlliance(ChatCommandContext ctx, ulong ownerId, string playerName, HashSet<string> ownerClanMembers)
    {
        string ownerName = ctx.Event.User.CharacterName.Value;

        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId)) // when first creating also add owner and clan members
        {
            Core.DataStructures.PlayerAlliances[ownerId] = []; 
            HashSet<string> newAlliance = Core.DataStructures.PlayerAlliances[ownerId];
            newAlliance.Add(ownerName);
            if (ownerClanMembers.Count > 0)
            {
                newAlliance.UnionWith(ownerClanMembers);
            }
            Core.DataStructures.SavePlayerAlliances();
        }

        HashSet<string> alliance = Core.DataStructures.PlayerAlliances[ownerId];
        int currentAllianceSize = alliance.Count - ownerClanMembers.Count;

        if (currentAllianceSize < Plugin.MaxAllianceSize.Value && !alliance.Contains(playerName))
        {
            alliance.Add(playerName);
            Core.DataStructures.SavePlayerAlliances();
            ctx.Reply($"<color=green>{playerName}</color> added to alliance.");
        }
        else
        {
            ctx.Reply($"Alliance is full or <color=green>{playerName}</color> is already in the alliance.");
        }
    }
    public static void RemovePlayerFromAlliance(ChatCommandContext ctx, HashSet<string> alliance, string playerName)
    {
        string playerKey = PlayerService.playerCache.Keys.FirstOrDefault(key => key.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(playerKey) && alliance.FirstOrDefault(n => n.Equals(playerKey)) != null)
        {
            alliance.Remove(playerKey);
            Core.DataStructures.SavePlayerAlliances();
            ctx.Reply($"<color=green>{char.ToUpper(playerName[0]) + playerName[1..].ToLower()}</color> removed from alliance.");
        }
        else
        {
            ctx.Reply($"<color=green>{char.ToUpper(playerName[0]) + playerName[1..].ToLower()}</color> not found in alliance.");
        }
    }
    public static void ListPersonalAllianceMembers(ChatCommandContext ctx, Dictionary<ulong, HashSet<string>> playerAlliances)
    {
        ulong ownerId = ctx.Event.User.PlatformId;
        string playerName = ctx.Event.User.CharacterName.Value;
        HashSet<string> members = playerAlliances.ContainsKey(ownerId) ? playerAlliances[ownerId] : playerAlliances.Where(groupEntry => groupEntry.Value.Contains(playerName)).SelectMany(groupEntry => groupEntry.Value).ToHashSet();
        string replyMessage = members.Count > 0 ? string.Join(", ", members.Select(member => $"<color=green>{member}</color>")) : "No members in alliance.";
        ctx.Reply(replyMessage);
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
            ctx.Reply(replyMessage);
        }
        else
        {
            foreach (var groupEntry in playerAlliances)
            {
                playerKey = groupEntry.Value.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(playerKey))
                {
                    string replyMessage = groupEntry.Value.Count > 0 ? string.Join(", ", groupEntry.Value.Select(member => $"<color=green>{member}</color>")) : "No members in alliance.";
                    ctx.Reply(replyMessage);
                    return;
                }
            }
        }
    }
}