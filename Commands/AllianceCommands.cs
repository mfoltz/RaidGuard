using ProjectM;
using Unity.Entities;
using VampireCommandFramework;
using static RaidGuard.Systems.AllianceSystem.AllianceUtilities;

namespace RaidGuard.Commands;
internal static class AllianceCommands
{

    static readonly bool PlayerAlliances = Plugin.PlayerAlliances.Value;
    static readonly bool ClanAlliances = Plugin.ClanBasedAlliances.Value;
    

    [Command(name: "toggleAllianceInvites", shortHand: "invites", adminOnly: false, usage: ".invites", description: "Toggles being able to be invited to an alliance. Allowed in raids of allied players and share exp if applicable.")]
    public static void ToggleAllianceInvitesCommand(ChatCommandContext ctx)
    {
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }

        ulong SteamID = ctx.Event.User.PlatformId;
        Entity ownerClanEntity = ctx.Event.User.ClanEntity._Entity;
        string name = ctx.Event.User.CharacterName.Value;

        if (ClanAlliances && ownerClanEntity.Equals(Entity.Null) || !Core.EntityManager.Exists(ownerClanEntity))
        {
            LocalizationService.HandleReply(ctx, "You must be the leader of a clan to toggle alliance invites.");
            return;
        }
        else if (ClanAlliances)
        {
            Entity userEntity = ctx.Event.SenderUserEntity;
            if (userEntity.TryGetComponent(out ClanRole clanRole) && !clanRole.Value.Equals(ClanRoleEnum.Leader))
            {
                LocalizationService.HandleReply(ctx, "You must be the leader of a clan to toggle alliance invites.");
                return;
            }
        }

        if (Core.DataStructures.PlayerAlliances.Any(kvp => kvp.Value.Contains(name)))
        {
            LocalizationService.HandleReply(ctx, "You are already in an alliance. Leave or disband if owned before enabling invites.");
            return;
        }

        if (Core.DataStructures.PlayerBools.TryGetValue(SteamID, out var bools))
        {
            bools["Grouping"] = !bools["Grouping"];
        }
        Core.DataStructures.SavePlayerBools();
        LocalizationService.HandleReply(ctx, $"Alliance invites {(bools["Grouping"] ? "<color=green>enabled</color>" : "<color=red>disabled</color>")}.");
    }

    [Command(name: "allianceAdd", shortHand: "aa", adminOnly: false, usage: ".aa [Player/Clan]", description: "Adds player/clan to alliance if invites are toggled (if clan based owner of clan must toggle).")]
    public static void AllianceAddCommand(ChatCommandContext ctx, string name)
    {  
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }

        ulong ownerId = ctx.Event.User.PlatformId;
        Entity ownerClanEntity = ctx.Event.User.ClanEntity._Entity;

        if (ClanAlliances)
        {
            if (CheckClanLeadership(ctx, ownerClanEntity))
            {
                LocalizationService.HandleReply(ctx, "You must be the leader of a clan to form an alliance.");
                return;
            }

            HandleClanAlliance(ctx, ownerId, name);
        }
        else
        {
            HandlePlayerAlliance(ctx, ownerId, name);
        }
    }

    [Command(name: "allianceRemove", shortHand: "ar", adminOnly: false, usage: ".ar [Player/Clan]", description: "Removes player or clan from alliance.")]
    public static void AllianceRemoveCommand(ChatCommandContext ctx, string name)
    {
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }

        ulong ownerId = ctx.Event.User.PlatformId;
        Entity ownerClanEntity = ctx.Event.User.ClanEntity._Entity;

        if (ClanAlliances && CheckClanLeadership(ctx, ownerClanEntity))
        {
            LocalizationService.HandleReply(ctx, "You must be the leader of a clan to remove clans from an alliance.");
            return;
        }

        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId))
        {
            LocalizationService.HandleReply(ctx, "You don't have an alliance.");
            return;
        }

        HashSet<string> alliance = Core.DataStructures.PlayerAlliances[ownerId]; // check size and if player is already present in group before adding

        if (ClanAlliances)
        {
            RemoveClanFromAlliance(ctx, alliance, name);
        }
        else
        {
            RemovePlayerFromAlliance(ctx, alliance, name);
        }
    }

    [Command(name: "listAllianceMembers", shortHand: "lam", adminOnly: false, usage: ".lam [Player]", description: "Lists alliance members of your alliance or the alliance you are in or the members in the alliance of the player entered if found.")]
    public static void AllianceMembersCommand(ChatCommandContext ctx, string name = "")
    {
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }        

        Dictionary<ulong, HashSet<string>> playerAlliances = Core.DataStructures.PlayerAlliances;

        if (string.IsNullOrEmpty(name))
        {
            ListPersonalAllianceMembers(ctx, playerAlliances);
        }
        else
        {
            ListAllianceMembersByName(ctx, name, playerAlliances);
        }
    }

    [Command(name: "allianceDisband", shortHand: "disband", adminOnly: false, usage: ".disband", description: "Disbands alliance.")]
    public static void DisbandAllianceCommand(ChatCommandContext ctx)
    {
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }

        ulong ownerId = ctx.Event.User.PlatformId;
        Entity ownerClanEntity = ctx.Event.User.ClanEntity._Entity;

        if (ClanAlliances && CheckClanLeadership(ctx, ownerClanEntity))
        {
            LocalizationService.HandleReply(ctx, "You must be the leader of your clan to disband the alliance.");
            return;
        }

        if (!Core.DataStructures.PlayerAlliances.ContainsKey(ownerId)) 
        {
            LocalizationService.HandleReply(ctx, "You don't have an alliance to disband.");
            return;
        }
       
        Core.DataStructures.PlayerAlliances.Remove(ownerId);
        LocalizationService.HandleReply(ctx, "Alliance disbanded.");
        Core.DataStructures.SavePlayerAlliances();   
    }

    [Command(name: "leaveAlliance", shortHand: "leave", adminOnly: false, usage: ".leave", description: "Leaves alliance if in one.")]
    public static void LeaveAllianceCommand(ChatCommandContext ctx)
    {
        if (!PlayerAlliances)
        {
            LocalizationService.HandleReply(ctx, "Alliances are not enabled.");
            return;
        }

        ulong ownerId = ctx.Event.User.PlatformId;
        Entity ownerClanEntity = ctx.Event.User.ClanEntity._Entity;
        string playerName = ctx.Event.User.CharacterName.Value;

        if (ClanAlliances && CheckClanLeadership(ctx, ownerClanEntity))
        {
            LocalizationService.HandleReply(ctx, "You must be the leader of a clan to leave an alliance.");
            return;
        }

        if (Core.DataStructures.PlayerAlliances.ContainsKey(ownerId))
        {
            LocalizationService.HandleReply(ctx, "You can't leave your own alliance. Disband it instead.");
            return;
        }

        if (ClanAlliances)
        {
            var alliance = Core.DataStructures.PlayerAlliances.Values.FirstOrDefault(set => set.Contains(playerName));
            if (alliance != null)
            {
                RemoveClanFromAlliance(ctx, alliance, ownerClanEntity.Read<ClanTeam>().Name.Value);
            }
            else
            {
                LocalizationService.HandleReply(ctx, "Your clan is not in an alliance.");
            }
        }
        else
        {
            var alliance = Core.DataStructures.PlayerAlliances.Values.FirstOrDefault(set => set.Contains(playerName));
            if (alliance != null)
            {
                RemovePlayerFromAlliance(ctx, alliance, playerName);
            }
            else
            {
                LocalizationService.HandleReply(ctx, "You're not in an alliance.");
            }    
        }
    }
}