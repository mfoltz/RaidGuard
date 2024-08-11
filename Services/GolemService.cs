using ProjectM.CastleBuilding;
using Unity.Entities;

namespace RaidGuard.Services;

class GolemService
{ 
    public static List<GolemTrack> TempProtectedGolems = new List<GolemTrack>();

    public static void ProtectGolem(Entity entity, Entity heartOwner, int length)
    {
        GolemTrack track = TempProtectedGolems.FirstOrDefault(x => x.entity == entity);

        if (track == null)
        {
            track = new GolemTrack();
            track.entity = entity;
            track.protectionUntil = DateTime.Now.AddSeconds(length);
            track.defenders = RaidService.GetEntities(heartOwner);

            TempProtectedGolems.Add(track);
        }
        else
        {
            track.protectionUntil = DateTime.Now.AddSeconds(length);
        }
    }

    public static bool IsProtected(Entity entity, Entity attacker)
    {
        GolemTrack track = TempProtectedGolems.FirstOrDefault( x=> x.entity == entity);

        if (track != null)
        { 
            int i = track.protectionUntil.CompareTo(DateTime.Now);

            if (i > 0) // protection time is active
            {
                if (track.defenders.Contains(attacker))
                {
                    return false;
                }

                return true;
            }
        }

        return false;
    }
}

public class GolemTrack
{
    public Entity entity;
    public DateTime protectionUntil;
    public List<Entity> defenders;
}
