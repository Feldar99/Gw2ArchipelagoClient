using Archipelago.MultiClient.Net.MessageLog.Parts;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Gw2Archipelago
{
    internal class MapAccessTracker
    {

        private struct MapEntry
        {
            public int Id;
            public string Name;
            public string Type;
            public List<string> Entrances;
            public List<Storyline> Storylines;
            public Dictionary<Storyline, HashSet<string>> Start;
        }

        public enum MapAccessState
        {
            Locked,
            Inaccessible,
            Accessible
        }

        private struct MapAccessData
        {
            public Map map;
            public MapAccessState state;
        }

        private static readonly Logger logger = Logger.GetLogger<MapAccessTracker>();

        public delegate void MapAccessEvent(string mapName, MapAccessState state);

        public event MapAccessEvent MapAccessChanged;

        private Dictionary<string, MapAccessData> maps                   = new Dictionary<string, MapAccessData>();
        private Dictionary<Storyline, List<Map>> mapsByStoryline         = new Dictionary<Storyline, List<Map>>();
        private Dictionary<Storyline, Dictionary<string, Map>> startMaps = new Dictionary<Storyline, Dictionary<string, Map>>();
        private Storyline storyline;
        
        public void LoadMaps(ContentsManager contentsManager)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(LowerCaseNamingConvention.Instance)
                .WithTypeConverter(new YamlStorylineConverter())
                .Build();

            var reader = new StreamReader(contentsManager.GetFileStream("Maps.yaml"));
            var mapData = deserializer.Deserialize<List<MapEntry>>(reader);

            var connections = new List<(string, string)>(); // (from, to)
            foreach (var mapEntry in mapData)
            {
                //logger.Debug(mapEntry.Name);
                maps[mapEntry.Name] = new MapAccessData()
                {
                    map   = new Map(mapEntry.Id, mapEntry.Name, MapTypeExtensions.FromName(mapEntry.Type).Value),
                    state = MapAccessState.Locked,
                };
                var map = maps[mapEntry.Name].map;
                foreach (var entrance in mapEntry.Entrances)
                {
                    connections.Add((entrance, mapEntry.Name));
                }
                foreach (var storyline in mapEntry.Storylines)
                {
                    map.Storylines.Add(storyline);
                    if (mapsByStoryline.ContainsKey(storyline))
                    {
                        mapsByStoryline[storyline].Add(map);
                    }
                    else
                    {
                        var storylineMap = new List<Map>();
                        storylineMap.Add(map);
                        mapsByStoryline.Add(storyline, storylineMap);
                    }
                }
                if (mapEntry.Start != null)
                {
                    foreach(var StartPair in mapEntry.Start)
                    {
                        //logger.Debug("({},{})", StartPair.Key, StartPair.Value);
                        Storyline storyline = StartPair.Key;
                        HashSet<string> starts = StartPair.Value;
                        Dictionary<string, Map> startMapsForStoryline;
                        if (!startMaps.TryGetValue(storyline, out startMapsForStoryline))
                        {
                            startMapsForStoryline = new Dictionary<string, Map>();
                            startMaps.Add(storyline, startMapsForStoryline);
                        }
                        foreach (string start in starts)
                        {
                            startMapsForStoryline[start.ToUpper()] = map;
                        }

                    }
                }
            }
            foreach (var connection in connections)
            {
                maps[connection.Item2].map.Entrances.Add(maps[connection.Item1].map);
                maps[connection.Item1].map.Exits.Add(maps[connection.Item2].map);
            }
        }

        public void Initialize(Storyline storyline, Race race, Profession profession, ItemTracker itemTracker)
        {
            this.storyline = storyline;
            //logger.Debug(storyline.ToString());

            Map startMap;
            if (!startMaps[storyline].TryGetValue(race.GetName(), out startMap))
            {
                if (!startMaps[storyline].TryGetValue(profession.GetName(), out startMap)) {
                    startMap = startMaps[storyline]["ANY"];
                }
            }
            GiveAccess(startMap.Name);
            foreach (var itemCount in itemTracker.ItemCounts)
            {
                if (maps.ContainsKey(itemCount.Key))
                {
                    UnlockMap(itemCount.Key);
                }
            }
            itemTracker.ItemUnlocked += OnItemUnlocked;
        }

        private void UnlockMap(string mapName)
        {
            MapAccessData mapAccess = maps[mapName];
            if (mapAccess.state == MapAccessState.Locked)
            {
                var accessible = false;
                foreach (var entrance in mapAccess.map.Entrances)
                {
                    if (maps[entrance.Name].state == MapAccessState.Accessible)
                    {
                        GiveAccess(mapName);
                        accessible = true;
                        break;
                    }
                }
                if (!accessible)
                {
                    mapAccess.state = MapAccessState.Inaccessible;
                    maps[mapName] = mapAccess;
                    if (MapAccessChanged != null) {
                        MapAccessChanged.Invoke(mapName, MapAccessState.Inaccessible);
                    }
                }
            }
        }

        private void GiveAccess(string mapName)
        {
            MapAccessData mapAccess = maps[mapName];
            if (mapAccess.state != MapAccessState.Accessible)
            {
                mapAccess.state = MapAccessState.Accessible;
                maps[mapName] = mapAccess;
                if (MapAccessChanged != null)
                {
                    MapAccessChanged.Invoke(mapName, MapAccessState.Accessible);
                }
                foreach (var exit in mapAccess.map.Exits)
                {
                    if (maps[exit.Name].state == MapAccessState.Inaccessible)
                    {
                        GiveAccess(exit.Name);
                    }
                }
            }
        }

        private void OnItemUnlocked(string itemName, int unlockCount)
        {
            if (maps.ContainsKey(itemName))
            {
                UnlockMap(itemName);
            }
        }

        internal bool IsLocked(string region)
        {
            if (maps.ContainsKey(region))
            {
                return maps[region].state == MapAccessState.Locked;
            }

            return false;
        }

        internal bool HasAccess(string region)
        {
            if (maps.ContainsKey(region))
            {
                return maps[region].state == MapAccessState.Accessible;
            }

            MapType? type = MapTypeExtensions.FromName(region);
            if (!type.HasValue)
            {
                return false;
            }

            if (type.Value == MapType.STORY)
            {
                type = MapType.OPEN_WORLD;
            }

            if (!mapsByStoryline.ContainsKey(storyline))
            {
                return false;
            }

            foreach (var map in mapsByStoryline[storyline])
            {
                if (type.Value == map.Type)
                {
                    if (maps[map.Name].state != MapAccessState.Accessible)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal bool IsMapName(string itemName)
        {
            return maps.ContainsKey(itemName);
        }
    }
}
