using Gw2Sharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    enum MapType
    {
        OPEN_WORLD,
        STORY,
        FRACTAL,
        STRIKE_MISSION,
        DUNGEON,
        RAID,
        WVW,
        PVP,
    };

    public static class MapTypeExtensions
    {

        internal static string GetName(this MapType region)
        {

            switch (region)
            {
                case MapType.OPEN_WORLD: return "Open World";
                case MapType.STORY: return "Story";
                case MapType.FRACTAL: return "Fractal";
                case MapType.STRIKE_MISSION: return "Strike Mission";
                case MapType.DUNGEON: return "Dungeon";
                case MapType.RAID: return "Raid";
                case MapType.WVW: return "WvW";
                case MapType.PVP: return "PvP";
                default:
                    throw new System.ArgumentException("Invalid Region");

            }
        }

        internal static MapType? FromName(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Equals("story") || lowerName.Equals("dragon response mission") || lowerName.Equals("quests")) return MapType.STORY;
            if (lowerName.Equals("fractal")) return MapType.FRACTAL;
            if (lowerName.Equals("dungeon")) return MapType.DUNGEON;
            if (lowerName.Equals("raid")) return MapType.RAID;
            if (lowerName.Equals("wvw")) return MapType.WVW;
            if (lowerName.Equals("pvp")) return MapType.PVP;
            if (lowerName.Equals("open world") || lowerName.Equals("openworld") || lowerName.Equals("open_world") || lowerName.Equals("city") || lowerName.Equals("convergence")) return MapType.OPEN_WORLD;
            if (lowerName.Equals("strikemission") || lowerName.Equals("strike_mission") || lowerName.Equals("strike mission") || lowerName.Equals("strike")) return MapType.STRIKE_MISSION;
            return null;
        }
    }
    internal class Map
    {
        public int Id;
        public string Name;
        public MapType Type;
        public HashSet<Map> Entrances = new HashSet<Map>();
        public HashSet<Map> Exits = new HashSet<Map>();
        public HashSet<Storyline> Storylines = new HashSet<Storyline>();

        public Map(int id, string name, MapType type)
        {
            Id = id;
            Name = name;
            Type = type;
        }

    }
}
