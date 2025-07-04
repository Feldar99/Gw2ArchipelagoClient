
using Blish_HUD;
using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Gw2Archipelago
{
    public enum Profession
    {
        Warrior = 1,
        Guardian,
        Revenant,
        Thief,
        Engineer,
        Ranger,
        Mesmer,
        Elementalist,
        Necromancer
    }
    public static class ProfessionExtensions
    {

        public static string GetName(this Profession profession)
        {

            switch (profession)
            {
                case Profession.Warrior:        return "WARRIOR";
                case Profession.Guardian:       return "GUARDIAN";
                case Profession.Revenant:       return "REVENANT";
                case Profession.Thief:          return "THIEF";
                case Profession.Engineer:       return "ENGINEER";
                case Profession.Ranger:         return "RANGER";
                case Profession.Mesmer:         return "MESMER";
                case Profession.Elementalist:   return "ELEMENTALIST";
                case Profession.Necromancer:    return "NECROMANCER";
                default:
                    throw new System.ArgumentException("Invalid Profession");
            }
        }

        public static Profession FromName(string name)
        {
            name = name.ToUpper();
            if (name.Equals("WARRIOR"))      { return Profession.Warrior;      }
            if (name.Equals("GUARDIAN"))     { return Profession.Guardian;     }
            if (name.Equals("REVENANT"))     { return Profession.Revenant;     }
            if (name.Equals("THIEF"))        { return Profession.Thief;        }
            if (name.Equals("ENGINEER"))     { return Profession.Engineer;     }
            if (name.Equals("RANGER"))       { return Profession.Ranger;       }
            if (name.Equals("MESMER"))       { return Profession.Mesmer;       }
            if (name.Equals("ELEMENTALIST")) { return Profession.Elementalist; }
            if (name.Equals("NECROMANCER"))  { return Profession.Necromancer;  }

            throw new System.ArgumentException("Invalid Profession");
        }
    }

    internal class YamlProfessionConverter : IYamlTypeConverter
    {
        private static readonly Logger logger = Logger.GetLogger<YamlProfessionConverter>();
        public bool Accepts(Type type)
        {
            //logger.Debug(type.FullName);
            return type == typeof(Profession);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            Scalar value = parser.Consume<Scalar>();
            //logger.Debug(value.Value);
            return ProfessionExtensions.FromName(value.Value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            Profession profession = (Profession)value;
            emitter.Emit(new Scalar(profession.GetName()));
        }
    }
}