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

namespace Gw2Archipelago
{
    class LocationChecker
    {

        private Gw2ApiManager gw2ApiManager;
        private SettingEntry<string> characterName;

        private Timer updateTimer;
        private static readonly Logger logger = Logger.GetLogger<LocationChecker>();

        private Dictionary<int, AchievementLocation> achievementLocations = new Dictionary<int, AchievementLocation>();
        private QuestStatus questStatus = new QuestStatus();
        private Dictionary<int, ItemLocation> itemLocations = new Dictionary<int, ItemLocation>();

        public delegate void AchievementEvent(AchievementLocation achievementLocation);
        public delegate void QuestEvent(Location questLocation, int questId);
        public delegate void ItemEvent(ItemLocation itemLocation);

        public event AchievementEvent AchievementProgressed;
        public event AchievementEvent AchievementCompleted;
        public event QuestEvent QuestCompleted;
        public event ItemEvent ItemAcquired;

        public LocationChecker(Gw2ApiManager gw2ApiManager, SettingEntry<string> characterName)
        {
            this.gw2ApiManager = gw2ApiManager;
            this.characterName = characterName;
        }

        public void StartTimer()
        {
            updateTimer = new Timer(Update, null, 5000, 60000);
        }

        public IReadOnlyCollection<AchievementLocation> GetAchievementLocations()
        {
            return achievementLocations.Values;
        }

        public Queue<Location> GetQuestLocations()
        {
            return questStatus.QuestLocations;
        }

        public async Task AddAchievmentLocation(int achievementId, Achievement achievement, AchievementCategory category, AccountAchievement progress, string name)
        {
            if (achievement == null)
            {
                achievement = await gw2ApiManager.Gw2ApiClient.V2.Achievements.GetAsync(achievementId);
            }
            logger.Debug("Added achievement: {} ({}) -> {}", achievement.Name, achievementId, name);
            AchievementLocation location = new AchievementLocation();
            location.Achievement = achievement;
            location.Category = category;
            location.LocationName = name;
            // Don't override serialized data on load
            location.Progress = progress;
            location.Id = achievementId;
            achievementLocations.Add(achievementId, location);

        }

        public async Task AddAchievmentLocation(AchievementLocation location)
        {
            var id = location.Id;
            location.Achievement = await gw2ApiManager.Gw2ApiClient.V2.Achievements.GetAsync(id);
            achievementLocations.Add(id, location);
        }

        public void AddQuestLocation(bool complete, string name)
        {
            Location location = new Location();
            location.LocationName = name;
            location.LocationComplete = complete;
            questStatus.QuestLocations.Enqueue(location);
        }

        public void AddItemLocation(bool complete, string name, List<int> itemIds)
        {
            ItemLocation location = new ItemLocation();
            location.LocationName = name;
            location.LocationComplete = complete;

            foreach (var id in itemIds)
            {
                location.targetQuantities[id] = -1;
                itemLocations[id] = location;
            }
        }

        public void AddQuestLocation(Location location)
        {
            questStatus.QuestLocations.Enqueue(location);
        }

        public void AddQuestIds(HashSet<int> questIds)
        {
            questStatus.QuestIds.UnionWith(questIds);
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
            logger.Debug("Character Name: {name}", characterName.Value);
            var achievements = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Progression                             }, gw2ApiManager.Gw2ApiClient.V2.Account.Achievements.GetAsync(),                        "achievements", HandleAchievements);
            var quests       = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Progression }, gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Quests.GetAsync(),      "quests",       HandleQuests);
            var training     = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Builds      }, gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Training.GetAsync(),    "training",     HandleTraining);
            var worldBosses  = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Progression                             }, gw2ApiManager.Gw2ApiClient.V2.Account.WorldBosses.GetAsync(),                         "world bosses", HandleWorldBosses);
            var items        = UpdateCategory(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Inventories }, gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Inventory.GetAsync(),   "items",    HandleItems);

            var tasks = new List<Task> { achievements, quests, training, worldBosses };
            await Task.WhenAll(tasks);
            logger.Debug("API Update complete");
            
        }

        private async Task UpdateCategory<T> (IEnumerable<TokenPermission> permissions, Task<T> task, string name, Action<T> callback)
        {
            try
            {
                if (gw2ApiManager.HasPermissions(permissions))
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
                logger.Warn(ex, "Failed to load {name}", name);
            }
        }



        private void HandleItems(CharactersInventory inventory)
        {
            logger.Debug("Updating Items");
            var itemQuantities = new Dictionary<int, int>(); //id -> quantity
            foreach (var bag in inventory.Bags)
            {
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
                if (questStatus.QuestIds.Contains(questId) && questStatus.QuestLocations.Count > 0)
                {
                    var location = questStatus.QuestLocations.Dequeue();
                    QuestCompleted(location, questId);
                    questStatus.QuestIds.Remove(questId);
                    questStatus.CompleteQuestCount++;
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


        private void HandleTraining(CharactersTraining training)
        {
            logger.Debug("Training not yet supported");
        }
    }

}
