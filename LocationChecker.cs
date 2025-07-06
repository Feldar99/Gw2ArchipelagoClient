using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using System.Threading;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2;
using Gw2Sharp.WebApi.V2.Models;
using Gw2Archipelago;
using YamlDotNet.Serialization;
using System.IO;
using ApiMap = Gw2Sharp.WebApi.V2.Models.Map;

namespace Gw2Archipelago
{
    class LocationChecker
    {
        public const int POI_DISCOVERY_DISTANCE = 25;
        public const int POI_DISCOVERY_DISTANCE_SQ = POI_DISCOVERY_DISTANCE * POI_DISCOVERY_DISTANCE;

        private Module module;
        private SettingEntry<string> characterName;

        private Timer apiUpdateTimer;
        private Timer mumbleUpdateTimer;
        private static readonly Logger logger = Logger.GetLogger<LocationChecker>();

        private Dictionary<int, AchievementLocation> achievementLocations = new Dictionary<int, AchievementLocation>();
        private QuestStatus questStatus = new QuestStatus();
        private Dictionary<int, ItemLocation> itemLocations = new Dictionary<int, ItemLocation>();
        private Dictionary<int, List<PoiLocation>> pointsOfInterest = new Dictionary<int, List<PoiLocation>>(); // mapId -> pois
        public HashSet<string> Regions = new HashSet<string>();

        private ApiMap currentMap;

        private Mutex poiMutex = new Mutex();

        public delegate void AchievementEvent(AchievementLocation achievementLocation);
        public delegate void QuestEvent(Location questLocation, Quest quest);
        public delegate void ItemEvent(ItemLocation itemLocation);
        public delegate void PoiEvent(PoiLocation poiLocation);

        public event AchievementEvent AchievementProgressed;
        public event AchievementEvent AchievementCompleted;
        public event QuestEvent QuestCompleted;
        public event ItemEvent ItemAcquired;
        public event PoiEvent PoiDiscovered;

        public LocationChecker(Module module, SettingEntry<string> characterName)
        {
            this.module = module;
            this.characterName = characterName;
        }

        public void StartTimer()
        {
            apiUpdateTimer = new Timer(Update, null, 5000, 60000);
            mumbleUpdateTimer = new Timer(MumbleUpdate, null, 1000, 1000);
        }

        public ICollection<Location> GetLocationsInRegion(string region)
        {
            List<Location> locations = new List<Location>();
            locations = locations.Union(itemLocations.Values)
                .Union(achievementLocations.Values)
                .Union(pointsOfInterest.Values.SelectMany(poi => poi))
                .Union(questStatus.QuestLocations)
                .Where(loc => loc.Region.Equals(region))
                .ToList();
            return locations;
        }

        public IReadOnlyCollection<AchievementLocation> GetAchievementLocations()
        {
            return achievementLocations.Values;
        }

        public Queue<Location> GetQuestLocations()
        {
            return questStatus.QuestLocations;
        }

        public async Task AddAchievmentLocation(int achievementId, Achievement achievement, AchievementCategory category, AccountAchievement progress, string name, string region)
        {
            if (achievement == null)
            {
                achievement = await module.Gw2ApiManager.Gw2ApiClient.V2.Achievements.GetAsync(achievementId);
            }
            logger.Debug("Added achievement: {} ({}) -> {}", achievement.Name, achievementId, name);
            AchievementLocation location = new AchievementLocation();
            location.Achievement = achievement;
            location.Category = category;
            location.LocationName = name;
            location.Region = region;
            Regions.Add(region);
            // Don't override serialized data on load
            location.Progress = progress;
            location.Id = achievementId;
            achievementLocations.Add(achievementId, location);

        }

        public async Task AddAchievmentLocation(AchievementLocation location)
        {
            var id = location.Id;
            location.Achievement = await module.Gw2ApiManager.Gw2ApiClient.V2.Achievements.GetAsync(id);
            Regions.Add(location.Region);
            achievementLocations.Add(id, location);
        }

        public void AddQuestLocation(bool complete, string name, string region)
        {
            Location location = new Location();
            location.LocationName = name;
            location.LocationComplete = complete;
            location.Region = region;
            Regions.Add(region);
            questStatus.QuestLocations.Enqueue(location);
        }

        public void AddItemLocation(bool complete, string name, List<int> itemIds, string region)
        {
            var location = new ItemLocation();
            location.LocationName = name;
            location.LocationComplete = complete;
            location.Region = region;
            Regions.Add(region);

            foreach (var id in itemIds)
            {
                location.targetQuantities[id] = -1;
                itemLocations[id] = location;
            }
        }

        public async Task AddPoiLocation(bool complete, string name, PointOfInterest poi)
        {
            var map = await module.Gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(poi.mapId);

            var location = new PoiLocation(poi);
            location.LocationName = name;
            location.LocationComplete = complete;
            location.Region = map.Name;
            Regions.Add(location.Region);
            poiMutex.WaitOne(10_000);
            if (!pointsOfInterest.ContainsKey(poi.mapId))
            {
                pointsOfInterest[poi.mapId] = new List<PoiLocation>();
            }
            pointsOfInterest[poi.mapId].Add(location);
            poiMutex.ReleaseMutex();
        }

        public void AddQuestLocation(Location location)
        {
            questStatus.QuestLocations.Enqueue(location);
        }

        public void AddQuests(HashSet<Quest> quests)
        {
            foreach (var quest in quests)
            {
                if (!questStatus.Quests.ContainsKey(quest.Id))
                {
                    questStatus.Quests.Add(quest.Id, quest);
                }
            }
        }

        public QuestStatus GetQuestStatus()
        {
            return questStatus;
        }

        public void SetQuestStatus(QuestStatus status)
        {
            questStatus = status;
        }

        public void ClearLocations()
        {
            achievementLocations.Clear();
            questStatus.QuestLocations.Clear();
        }

        public bool HasAchievementLocation(int id)
        {
            return achievementLocations.Keys.Contains(id);
        }

        private async void Update(Object stateInfo)
        {
            var mapId = GameService.Gw2Mumble.CurrentMap.Id;

            logger.Debug("Character Name: {name}", characterName.Value);
            var achievements = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Progression                             }, module.Gw2ApiManager.Gw2ApiClient.V2.Account.Achievements.GetAsync(),                        "achievements", HandleAchievements);
            var quests       = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Progression }, module.Gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Quests.GetAsync(),      "quests",       HandleQuests);
            var training     = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Builds      }, module.Gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Training.GetAsync(),    "training",     HandleTraining);
            var worldBosses  = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Progression                             }, module.Gw2ApiManager.Gw2ApiClient.V2.Account.WorldBosses.GetAsync(),                         "world bosses", HandleWorldBosses);
            var items        = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Inventories }, module.Gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Inventory.GetAsync(),   "items",    HandleItems);

            var tasks = new List<Task> { achievements, quests, training, worldBosses };
            await Task.WhenAll(tasks);
            logger.Debug("API Update complete");
        }

        private async void MumbleUpdate(Object stateInfo)
        {
            //logger.Debug("Updating locations from MumbleAPI");
            var mapId = GameService.Gw2Mumble.CurrentMap.Id;
            if (mapId == 0)
            {
                return;
            }

            if (currentMap == null || mapId != currentMap.Id)
            {
                currentMap = await module.Gw2ApiManager.Gw2ApiClient.V2.Maps.GetAsync(mapId);
            }

            if (module.MapAccessTracker.IsLocked(currentMap.Name))
            {
                return;
            }
            var currentCharacter = GameService.Gw2Mumble.PlayerCharacter;
            //logger.Debug("Mumble Character: {}, AP Character: {}", currentCharacter.Name, characterName.Value);
            if (pointsOfInterest.ContainsKey(mapId) && currentCharacter.Name.Equals(characterName.Value))
            {
                
                var pos = currentCharacter.Position;
                foreach (var poi in pointsOfInterest[mapId])
                {
                    if (poi.LocationComplete)
                    {
                        continue;
                    }
                    //logger.Debug("Current Position: {} POI Position: {}", pos, poi.Position);
                    var distanceSq = (pos - poi.Position).LengthSquared();
                    //logger.Debug("Distance to {} = {}", poi.LocationName, distanceSq);
                    if (distanceSq <= POI_DISCOVERY_DISTANCE_SQ)
                    {
                        PoiDiscovered.Invoke(poi);
                    }
                }
            }
            //logger.Debug("Mumble Update Complete");
            
        }

        private async Task UpdateCategory<T> (IEnumerable<TokenPermission> permissions, Task<T> task, string name, Action<T> callback)
        {
            try
            {
                if (module.Gw2ApiManager.HasPermissions(permissions))
                {
                    logger.Debug("Getting {name} from the API.", name);

                    var obj = await task;

                    callback(obj);

                    logger.Debug("Loaded {name} from the API.", name);
                }
                else
                {
                    logger.Debug("Skipping {name} from the API - API key does not give us permission", name);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to update {name}", name);
            }
        }



        private void HandleItems(CharactersInventory inventory)
        {
            logger.Debug("Updating Items");
            var itemQuantities = new Dictionary<int, int>(); //id -> quantity
            foreach (var bag in inventory.Bags)
            {
                if (bag == null)
                {
                    continue;
                }
                foreach (var item in bag.Inventory)
                {
                    if (item == null || !itemLocations.ContainsKey(item.Id))
                    {
                        continue;
                    }
                    if (itemQuantities.ContainsKey(item.Id))
                    {
                        itemQuantities[item.Id] += item.Count;
                    }
                    else
                    {
                        itemQuantities[item.Id] = item.Count;
                    }
                }
            }

            foreach (var itemLocationPair in itemLocations)
            {
                var itemId = itemLocationPair.Key;
                var itemLocation = itemLocationPair.Value;
                var targetQuantity = itemLocation.targetQuantities[itemId];
                var quantity = 0;
                itemQuantities.TryGetValue(itemId, out quantity);
                logger.Debug("location: {}, quantity: {}, targetQuantity: {}", itemLocation.LocationName, quantity, targetQuantity);
                if (quantity >= targetQuantity && targetQuantity != -1)
                {

                    ItemAcquired.Invoke(itemLocation);
                }
                else
                {
                    itemLocation.targetQuantities[itemId] = quantity + 1;
                }
            }
        }

        private void HandleAchievements(IApiV2ObjectList<AccountAchievement> achievements) 
        {
            logger.Debug("Updating Achievements");
            foreach (var progress in achievements) {
                if (achievementLocations.TryGetValue(progress.Id, out AchievementLocation location))
                {
                    var achievement = location.Achievement;
                    var oldProgress = location.Progress;
                    logger.Debug("Updating {name}", achievement.Name);

                    if (oldProgress != null)
                    {
                        if (progress.Current == oldProgress.Current)
                        {
                            logger.Debug("No Change: {oldProgress} {progress}");
                            continue;
                        }
                    }

                    var oldDone = location.Done;
                    int? oldRepeated = location.Repeated;
                    var oldBitCount = location.BitCount;
                    var oldTier = location.Tier;

                    location.Progress = progress;
                    achievementLocations[progress.Id] = location;

                    if (AchievementProgressed != null)
                    {
                        AchievementProgressed.Invoke(location);
                    }

                    if (AchievementCompleted != null)
                    {
                        var newBitCount = 0;
                        if (progress.Bits != null)
                        {
                            newBitCount = progress.Bits.Count;
                        }

                        if (oldDone != location.Done 
                            || oldRepeated != location.Repeated
                            || oldBitCount != location.BitCount
                            || oldTier != location.Tier
                        ) {
                            if (oldDone != location.Done)
                            {
                                logger.Debug("Done");
                            }
                            if (oldRepeated != location.Repeated)
                            {
                                logger.Debug("Repeated - old: {}, new: {}", oldRepeated, location.Repeated);
                            }
                            if (oldBitCount != location.BitCount)
                            {
                                logger.Debug("BitCount - old: {}, new: {}", oldBitCount, location.BitCount);
                            }
                            if (oldTier != location.Tier)
                            {
                                logger.Debug("Tier - old: {}, new: {}", oldTier, location.Tier);
                            }
                            location.LocationComplete = true;
                            AchievementCompleted.Invoke(location);
                        }
                    }
                }
            }
        }
        private void HandleWorldBosses(IApiV2ObjectList<string> worldBosses) {
            logger.Debug("World Bosses not yet supported");
        }


        private void HandleQuests(IApiV2ObjectList<int> quests)
        {
            logger.Debug("Updating Quests");
            foreach (var questId in quests)
            {
                logger.Debug("questLocations: {}", questStatus.QuestLocations);
                if (questStatus.Quests.ContainsKey(questId) && questStatus.QuestLocations.Count > 0)
                {
                    var location = questStatus.QuestLocations.Dequeue();
                    Quest quest = questStatus.Quests[questId];
                    questStatus.Quests.Remove(questId);
                    questStatus.CompleteQuestCount++;
                    QuestCompleted.Invoke(location, quest);
                }
            }
        }

        public int GetCompleteQuestCount()
        {
            return questStatus.CompleteQuestCount;
        }
        public IEnumerable<ItemLocation> GetItemLocations()
        {
            return itemLocations.Values.Distinct();
        }

        public List<PoiLocation> GetPoiLocations()
        {
            var result = new List<PoiLocation>();
            foreach (var poiEntry in pointsOfInterest)
            {
                result.AddRange(poiEntry.Value);
            }

            return result;
        }


        private void HandleTraining(CharactersTraining training)
        {
            logger.Debug("Training not yet supported");
        }
    }

}
