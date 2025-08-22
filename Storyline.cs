
using Blish_HUD;
using Gw2Archipelago;
using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Gw2Archipelago
{
    public enum Storyline
    {
        CORE,
        SEASON_1,
        SEASON_2,
        HEART_OF_THORNS,
        SEASON_3,
        PATH_OF_FIRE,
        SEASON_4,
        ICEBROOD_SAGA,
        END_OF_DRAGONS,
        SECRETS_OF_THE_OBSCURE,
        JANTHIR_WILDS,
    }

    public static class StorylineExtensions
    {

        public static string GetName(this Storyline storyline)
        {
            
            switch (storyline)
            {
                case Storyline.CORE:                    return "Core";
                case Storyline.SEASON_1:                return "Season1";
                case Storyline.SEASON_2:                return "Season2";
                case Storyline.HEART_OF_THORNS:         return "HeartOfThorns";
                case Storyline.SEASON_3:                return "Season3";
                case Storyline.PATH_OF_FIRE:            return "PathOfFire";
                case Storyline.SEASON_4:                return "Season4";
                case Storyline.ICEBROOD_SAGA:           return "IcebroodSaga";
                case Storyline.END_OF_DRAGONS:          return "EndOfDragons";
                case Storyline.SECRETS_OF_THE_OBSCURE:  return "SecretsOfTheObscure";
                case Storyline.JANTHIR_WILDS:           return "JanthirWilds";
                default:
                    throw new System.ArgumentException("Invalid Storyline");
            }
        }

        public static Guid GetSeasonId(this Storyline storyline)
        {
            switch (storyline) { 
                case Storyline.CORE:                    return new Guid("215AAA0F-CDAC-4F93-86DA-C155A99B5784");
                case Storyline.SEASON_1:                return new Guid("A49D0CD7-E725-4141-8E10-180F1CED7CAF");
                case Storyline.SEASON_2:                return new Guid("A515A1D3-4BD7-4594-AE30-2C5D05FF5960");
                case Storyline.HEART_OF_THORNS:         return new Guid("B8901E58-DC9D-4525-ADB2-79C93593291E");
                case Storyline.SEASON_3:                return new Guid("09766A86-D88D-4DF2-9385-259E9A8CA583");
                case Storyline.PATH_OF_FIRE:            return new Guid("EAB597C0-C484-4FD3-9430-31433BAC81B6");
                case Storyline.SEASON_4:                return new Guid("C22AFD21-667A-4AA8-8210-AC74EAEE58BB");
                case Storyline.ICEBROOD_SAGA:           return new Guid("EDCAE800-302A-4D9B-8331-3CC769ADA0B3");
                case Storyline.END_OF_DRAGONS:          return new Guid("D1B709AB-92B6-4EE9-8B40-2B7C628E5022");
                case Storyline.SECRETS_OF_THE_OBSCURE:  return new Guid("AEE99452-D323-4ABB-8F49-D7C0A752CBD1");
                case Storyline.JANTHIR_WILDS:           return new Guid("5EFFBB71-7C6D-4594-A87D-88B8CF38FDA3");

                default:
                    throw new System.ArgumentException("Invalid Storyline");
            }
        }

        public static Storyline? FromName(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Equals("core")) return Storyline.CORE;
            if (lowerName.Equals("season1") || lowerName.Equals("season_1") || lowerName.Equals("season 1") || lowerName.Equals("s1")) return Storyline.SEASON_1;
            if (lowerName.Equals("season2") || lowerName.Equals("season_2") || lowerName.Equals("season 2") || lowerName.Equals("s2")) return Storyline.SEASON_2;
            if (lowerName.Equals("heartofthorns") || lowerName.Equals("heart_of_thorns") || lowerName.Equals("heart of thorns") || lowerName.Equals("hot")) return Storyline.HEART_OF_THORNS;
            if (lowerName.Equals("season3") || lowerName.Equals("season_3") || lowerName.Equals("season 3") || lowerName.Equals("s3")) return Storyline.SEASON_3;
            if (lowerName.Equals("pathoffire") || lowerName.Equals("path_of_fire") || lowerName.Equals("path of fire") || lowerName.Equals("pof")) return Storyline.PATH_OF_FIRE;
            if (lowerName.Equals("season4") || lowerName.Equals("season_4") || lowerName.Equals("season 4") || lowerName.Equals("s4")) return Storyline.SEASON_4;
            if (lowerName.Equals("icebroodsaga") || lowerName.Equals("icebrood_saga") || lowerName.Equals("icebrood saga") || lowerName.Equals("ibs")) return Storyline.ICEBROOD_SAGA;
            if (lowerName.Equals("endofdragons") || lowerName.Equals("end_of_dragons") || lowerName.Equals("end of dragons") || lowerName.Equals("eod")) return Storyline.END_OF_DRAGONS;
            if (lowerName.Equals("secretsoftheobscure") || lowerName.Equals("secrets_of_the_obscure") || lowerName.Equals("secrets of the obscure") || lowerName.Equals("soto")) return Storyline.SECRETS_OF_THE_OBSCURE;
            if (lowerName.Equals("janthirwilds") || lowerName.Equals("janthir_wilds") || lowerName.Equals("janthir wilds") || lowerName.Equals("jw")) return Storyline.JANTHIR_WILDS;
            return null;
        }

        public static Storyline? GetFallback(this Storyline storyline)
        {
            switch (storyline)
            {
                case Storyline.CORE:
                    return null;
                case Storyline.SEASON_3:
                    return Storyline.HEART_OF_THORNS;
                case Storyline.SEASON_4:
                case Storyline.ICEBROOD_SAGA:
                    return Storyline.PATH_OF_FIRE;
                default:
                    return Storyline.CORE;
            }
        }
    }

    internal class YamlStorylineConverter : IYamlTypeConverter
    {
        private static readonly Logger logger = Logger.GetLogger<YamlStorylineConverter>();
        public bool Accepts(Type type)
        {
            //logger.Debug(type.FullName);
            return type == typeof(Storyline);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            Scalar value = parser.Consume<Scalar>();
            //logger.Debug(value.Value);
            return StorylineExtensions.FromName(value.Value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            Storyline storyline = (Storyline)value;
            emitter.Emit(new Scalar(storyline.GetName()));
        }
    }
}