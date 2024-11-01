
using System;

namespace Gw2Archipelago
{
    public enum Race
    {
        Asura = 1,
        Charr,
        Human,
        Norn,
        Sylvari
    }
    public static class RaceExtensions
    {

        public static string GetName(this Race race)
        {

            switch (race)
            {
                case Race.Asura:   return "ASURA";
                case Race.Charr:   return "CHARR";
                case Race.Human:   return "HUMAN";
                case Race.Norn:    return "NORN";
                case Race.Sylvari: return "SYLVARI";
                default:
                    throw new System.ArgumentException("Invalid Race");
            }
        }

        public static Race FromName(string name)
        {
            name = name.ToUpper();
            if (name.Equals("ASURA"))   { return Race.Asura;       }
            if (name.Equals("CHARR"))   { return Race.Charr;       }
            if (name.Equals("HUMAN"))   { return Race.Human;       }
            if (name.Equals("NORN"))    { return Race.Norn;        }
            if (name.Equals("SYLVARI")) { return Race.Sylvari;     }

            throw new System.ArgumentException("Invalid Race");
        }
    }
}