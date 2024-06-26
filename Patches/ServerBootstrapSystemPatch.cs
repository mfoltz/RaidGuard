using HarmonyLib;
using ProjectM;
using Stunlock.Network;
using Unity.Entities;
using User = ProjectM.Network.User;

namespace RaidGuard.Patches;

[HarmonyPatch]
internal static class ServerBootstrapSystemPatch
{
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    [HarmonyPostfix]
    static void OnUserConnectedPostfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        int userIndex = __instance._NetEndPointToApprovedUserIndex[netConnectionId];
        ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];
        Entity userEntity = serverClient.UserEntity;
        User user = __instance.EntityManager.GetComponentData<User>(userEntity);
        ulong steamId = user.PlatformId;

        if (Plugin.Alliances.Value && !Core.DataStructures.PlayerBools.ContainsKey(steamId))
        {
            Core.DataStructures.PlayerBools.Add(steamId, new Dictionary<string, bool>
            {
                { "AllianceInvites", false }
            });
            Core.DataStructures.SavePlayerBools();
        }
    }
}