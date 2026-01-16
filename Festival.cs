using Blish_HUD;
using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Gw2Archipelago
{
    public enum Festival
    {
        Wintersday,
        LunarNewYear,
        SuperAdventureBox,
        DraonBash,
        FestivalOfTheFourWinds,
        Halloween,
    }
    public static class FestivalExtensions
    {

        public static string GetName(this Festival festival)
        {

            switch (festival)
            {
                case Festival.Wintersday: return "Wintersday";
                case Festival.LunarNewYear: return "Lunar New Year";
                case Festival.SuperAdventureBox: return "Super Adventure Box";
                case Festival.DraonBash: return "Dragon Bash";
                case Festival.FestivalOfTheFourWinds: return "Festival of the Four Winds";
                case Festival.Halloween: return "Halloween";
                default:
                    throw new System.ArgumentException("Invalid Festival");
            }
        }

        public static Festival? FromName(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Equals("wintersday")) return Festival.Wintersday;
            if (lowerName.Equals("lunar new year") || lowerName.Equals("lny") || lowerName.Equals("lunarnewyear")) return Festival.LunarNewYear;
            if (lowerName.Equals("super adventure box") || lowerName.Equals("sab") || lowerName.Equals("superadventurebox")) return Festival.SuperAdventureBox;
            if (lowerName.Equals("dragon bash") || lowerName.Equals("dragonbash")) return Festival.DraonBash;
            if (lowerName.Equals("festival of the four winds") || lowerName.Equals("four winds") || lowerName.Equals("festivalofthefourwinds") || lowerName.Equals("fourwinds")) return Festival.FestivalOfTheFourWinds;
            if (lowerName.Equals("halloween")) return Festival.Halloween;
            return null;
        }
    }

    internal class YamlFestivalConverter : IYamlTypeConverter
    {
        private static readonly Logger logger = Logger.GetLogger<YamlFestivalConverter>();
        public bool Accepts(Type type)
        {
            //logger.Debug(type.FullName);
            return type == typeof(Festival) || type == typeof(Festival?);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            Scalar value = parser.Consume<Scalar>();
            //logger.Debug(value.Value);
            return FestivalExtensions.FromName(value.Value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            Festival festival = (Festival)value;
            emitter.Emit(new Scalar(festival.GetName()));
        }
    }
}