
public enum Region
{
    OPEN_WORLD,
    STORY,
    FRACTAL,
    STRIKE_MISSION,
    DUNGEON,
    RAID,
    WVW,
    PVP,
}

public static class RegionExtensions
{

    public static string GetName(this Region region)
    {

        switch (region)
        {
            case Region.OPEN_WORLD: return "OpenWorld";
            case Region.STORY: return "Story";
            case Region.FRACTAL: return "Fractal";
            case Region.DUNGEON: return "Dungeon";
            case Region.RAID: return "Raid";
            case Region.STRIKE_MISSION: return "StrikeMission";
            case Region.WVW: return "WvW";
            case Region.PVP: return "PvP";
            default:
                throw new System.ArgumentException("Invalid Region");

        }
    }

    public static Region? FromName(string name)
    {
        var lowerName = name.ToLower();
        if (lowerName.Equals("story")) return Region.STORY;
        if (lowerName.Equals("fractal")) return Region.FRACTAL;
        if (lowerName.Equals("dungeon")) return Region.DUNGEON;
        if (lowerName.Equals("raid")) return Region.RAID;
        if (lowerName.Equals("wvw")) return Region.WVW;
        if (lowerName.Equals("pvp")) return Region.PVP;
        if (lowerName.Equals("openworld") || lowerName.Equals("open_world") || lowerName.Equals("open world")) return Region.OPEN_WORLD;
        if (lowerName.Equals("strikemission") || lowerName.Equals("strike_mission") || lowerName.Equals("strike mission") || lowerName.Equals("strike")) return Region.STRIKE_MISSION;
        return null;
    }
}