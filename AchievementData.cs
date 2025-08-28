using Blish_HUD;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Core.Tokens;
using YamlDotNet.Serialization;
using YamlScalar = YamlDotNet.Core.Events.Scalar;

namespace Gw2Archipelago
{

    class GenericAchievementData
    {
        public Dictionary<int, string> categories;
        public Dictionary<int, string> achievments;
        public Dictionary<Guid, string> groups;
    }

    abstract class LogicGroup
    {
        public HashSet<string> entries = new HashSet<string>();

        protected abstract IEnumerable<LogicGroup> GetSubgroups();

        public HashSet<string> GetEntriesRecursive()
        {
            HashSet<string> results = new HashSet<string>(entries);
            foreach (var group in GetSubgroups())
            {
                results.UnionWith(group.GetEntriesRecursive());
            }

            return results;
        }

    }

    class AnyGroup : LogicGroup
    {
        public List<AllGroup> allGroups = new List<AllGroup>();

        protected override IEnumerable<LogicGroup> GetSubgroups()
        {
            return allGroups;
        }
    }

    class AllGroup : LogicGroup
    {
        public List<AnyGroup> anyGroups = new List<AnyGroup>();

        protected override IEnumerable<LogicGroup> GetSubgroups()
        {
            return anyGroups;
        }
    }

    enum ProgressBitType
    {
        Text,
        Item,
        Minipet,
        Skin,
    }

    enum Order
    {
        Vigil,
        OrderOfWhispers,
        DurmandPriory,
    }

    class ProgressBit
    {
        public ProgressBitType Type;
        public string Text;
        public int? Id;
        public string Name;

        public HashSet<string> Tags;

        // regions
        public AnyGroup Disciplines;
        public AnyGroup Maps;
        public AnyGroup Quests;
        public AnyGroup RewardTracks;
        public AnyGroup ContainedIn;
        public AnyGroup SoldBy;
        public MapType? MapType;
        public string Activity;

        // filters
        public AnyGroup RequiredBits;
        public AnyGroup RequiredItems;
        public AnyGroup RequiredRaces;
        public AnyGroup RequiredAchievements;
        public Profession RequiredProfession;
        public Festival Festival;
        public Storyline Storyline;
        public Order RequiredOrder;

        public HashSet<string> GetRegions()
        {
            HashSet<string> regions = new HashSet<string>();

            if (Disciplines != null)
            {
                regions.UnionWith(Disciplines.GetEntriesRecursive());
            }
            if (Maps != null)
            {
                regions.UnionWith(Maps.GetEntriesRecursive());
            }
            if (Quests != null)
            {
                regions.UnionWith(Quests.GetEntriesRecursive());
            }
            if (MapType.HasValue)
            {
                regions.Add(MapType.Value.ToString());
            }
            if (!string.IsNullOrEmpty(Activity))
            {
                regions.Add(Activity);
            }

            return regions;
        }

    }

    class AchievementData
    {
        public string Name;

        public HashSet<string> Tags;
        public int? MaxAmount;

        // regions
        public AnyGroup Disciplines;
        public AnyGroup Maps;
        public AnyGroup Quests;
        public AnyGroup RewardTracks;
        public AnyGroup ContainedIn;
        public AnyGroup SoldBy;
        public MapType? MapType;
        public string Activity;

        // filters
        public AnyGroup RequiredItems;
        public AnyGroup RequiredRaces;
        public AnyGroup RequiredAchievements;
        public Profession RequiredProfession;
        public Festival Festival;
        public Storyline Storyline;
        public Order RequiredOrder;

        public List<ProgressBit> bits;

        public HashSet<string> GetRegions()
        {
            HashSet<string> regions = new HashSet<string>();

            if (Disciplines != null)
            {
                regions.UnionWith(Disciplines.GetEntriesRecursive());
            }
            if (Maps != null)
            {
                regions.UnionWith(Maps.GetEntriesRecursive());
            }
            if (Quests != null)
            {
                regions.UnionWith(Quests.GetEntriesRecursive());
            }
            if (MapType.HasValue)
            {
                regions.Add(MapType.Value.ToString());
            }
            if (!string.IsNullOrEmpty(Activity))
            {
                regions.Add(Activity);
            }

            return regions;
        }
    }

    internal class YamLogicGroupConverter<T> : IYamlTypeConverter
    {
        private static readonly Logger logger = Logger.GetLogger<YamLogicGroupConverter<T>>();
        public bool Accepts(Type type)
        {
            //logger.Debug(type.FullName);
            return type == typeof(AnyGroup);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            if (parser.TryConsume<SequenceStart>(out _))
            {
                return ParseAnyGroup(parser);
            }
            else if (parser.Accept<YamlScalar>(out _))
            {
                var scalar = parser.Consume<YamlScalar>();
                var group = new AnyGroup();
                group.entries.Add(scalar.Value);
                return group;
            }
             
            throw new InvalidOperationException(BuildErrorMessage("AnyGroups must be ordered lists or single values", parser));
        }

        private static string BuildErrorMessage(string errorMessage, IParser parser)
        {
            return String.Format("{0}: row {1}, col {2}", errorMessage, parser.Current.Start.Line, parser.Current.Start.Column);
        }


        public static AnyGroup ParseAnyGroup(IParser parser)
        {
            var group = new AnyGroup();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (parser.TryConsume<MappingStart>(out _))
                {
                    var key = parser.Consume<YamlScalar>();
                    if (key.Value.Equals("all"))
                    {
                        parser.Consume<SequenceStart>();
                        var allGroup = ParseAllGroup(parser);
                        group.allGroups.Add(allGroup);
                    }
                    else
                    {
                        logger.Error(key.Value);
                        throw new InvalidOperationException(BuildErrorMessage("Only maps with the key of 'all' may be in AnyGroup lists", parser));
                    }

                    if (!parser.Accept<MappingEnd>(out _))
                    {
                        throw new InvalidOperationException(BuildErrorMessage("Invalid Token, expected MappingEnd", parser));
                    }
                    parser.Consume<MappingEnd>();
                }
                else if (parser.Accept<YamlScalar>(out _))
                {
                    var scalar = parser.Consume<YamlScalar>();
                    group.entries.Add(scalar.Value);
                }
                else
                {
                    throw new InvalidOperationException(BuildErrorMessage("AnyGroup lists must contain strings and AllGroups", parser));
                }
            }

            if (!parser.Accept<SequenceEnd>(out _))
            {
                throw new InvalidOperationException(BuildErrorMessage("Invalid Token, expected SequenceEnd", parser));
            }
            parser.Consume<SequenceEnd>();
            return group;
        }


        public static AllGroup ParseAllGroup(IParser parser)
        {
            var group = new AllGroup();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                if (parser.TryConsume<MappingStart>(out _))
                {
                    var key = parser.Consume<YamlScalar>();
                    if (key.Value.Equals("any"))
                    {
                        parser.Consume<SequenceStart>();
                        var anyGroup = ParseAnyGroup(parser);
                        group.anyGroups.Add(anyGroup);
                    }
                    else
                    {
                        logger.Error(key.Value);
                        throw new InvalidOperationException(BuildErrorMessage("Only maps with the key of 'any' may be in AllGroup lists", parser));
                    }

                    if (!parser.Accept<MappingEnd>(out _))
                    {
                        throw new InvalidOperationException(BuildErrorMessage("Invalid Token, expected MappingEnd", parser));
                    }
                    parser.Consume<MappingEnd>();
                }
                else if (parser.Accept<YamlScalar>(out _))
                {
                    var scalar = parser.Consume<YamlScalar>();
                    group.entries.Add(scalar.Value);
                }
                else
                {
                    throw new InvalidOperationException(BuildErrorMessage("AllGroup lists must contain strings and AnyGroups", parser));
                }
            }

            if (!parser.Accept<SequenceEnd>(out _))
            {
                throw new InvalidOperationException(BuildErrorMessage("Invalid Token, expected SequenceEnd", parser));
            }
            parser.Consume<SequenceEnd>();
            return group;
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new InvalidOperationException("Not Yet Implemented");
        }
    }
}