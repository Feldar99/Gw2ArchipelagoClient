using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Blish_HUD.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gw2Sharp.WebApi.V2.Models;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

using Archipelago.MultiClient.Net;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Gw2Archipelago;
using System.Text.Json;

using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaColor = Microsoft.Xna.Framework.Color;
using SystemFile = System.IO.File;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Enums;
using System.Threading;

using SlotData = System.Collections.Generic.Dictionary<string, object>;

namespace Gw2Archipelago
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {

        private static readonly Logger logger = Logger.GetLogger<Module>();

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        private SettingEntry<string> apServerUrl;
        private SettingEntry<string> slotName;
        private SettingEntry<string> characterName;

        private ArchipelagoSession         apSession;
        private SlotData                   slotData;
        private string                     filePrefix;

        private LocationChecker locationChecker;

        private CornerIcon                     cornerIcon;
        private ArchipelagoWindow              mainWindow;
        private Dictionary<long, int>          itemCounts = new Dictionary<long, int>();
        private int                            mistFragments = 0;

        private AchievementData achievementData;
        private SavedData savedData;

        private Random random = new Random();

        // State Info
        bool unlockingItems = false;

        private Timer reconnectTimer;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            logger.Debug("Define Settings");
            apServerUrl = settings.DefineSetting("apServerUrl", "archipelago.gg:<port>", () => "Archipelago Server URL");
            slotName    = settings.DefineSetting("apSlotName", "", () => "Slot Name");
            characterName = settings.DefineSetting("characterName", "", () => "Character Name", () => "The name of the character you will be using for this Archipelago slot");
        }

        protected override void Initialize()
        {
            logger.Debug("Initialize");
            locationChecker = new LocationChecker(Gw2ApiManager, characterName);
            locationChecker.AchievementProgressed += UpdateAchievementProgress;
            locationChecker.AchievementCompleted += SendLocationCompletion;
            locationChecker.QuestCompleted += SendLocationCompletion;
            locationChecker.ItemAcquired += SendLocationCompletion;
            locationChecker.PoiDiscovered += SendLocationCompletion;
        }

        protected override async Task LoadAsync()
        {
            logger.Debug("Load");
            await ConnectToArchipelago();
        }

        internal void StartReconnectTimer(object sender, MouseEventArgs e)
        {
            if (reconnectTimer == null)
            {
                reconnectTimer = new Timer(ConnectToArchipelago, null, 5000, 60000);
            }
        }

        private async void ConnectToArchipelago(Object stateInfo)
        {
            await ConnectToArchipelago();
        }

        private void DisconnectFromArchipelago()
        {
            locationChecker.ClearLocations();
            mainWindow.ClearLocations();

            apSession = null;

        }

        private async Task ConnectToArchipelago()
        {
            if (apSession != null)
            {
                DisconnectFromArchipelago();
            }
            logger.Debug("Connecting to " + apServerUrl.Value);
            bool success = false;
            LoginResult result = null;
            try
            {
                apSession = ArchipelagoSessionFactory.CreateSession(apServerUrl.Value);
                result = apSession.TryConnectAndLogin("Guild Wars 2", slotName.Value, Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems);
                success = result.Successful;
            }
            catch (UriFormatException) {
                logger.Error("Could not parse URI {}", apServerUrl.Value);

            }
            if (success)
            {

                if (reconnectTimer != null)
                {
                    reconnectTimer = null;
                }

                LoginSuccessful loginSuccess = (LoginSuccessful)result;
                slotData = loginSuccess.SlotData;
                logger.Debug("Connetion Successful");


                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(LowerCaseNamingConvention.Instance)
                    .Build();

                Dictionary<string, List<int>> itemIdsByName = new Dictionary<string, List<int>>();
                {
                    var reader = new StreamReader(ContentsManager.GetFileStream("Items.yaml"));
                    var itemData = deserializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, List<int>>>>>(reader);

                    foreach (var storylineData in itemData)
                    {
                        foreach (var mapData in storylineData.Value)
                        {
                            foreach (var itemIds in mapData.Value)
                            {
                                logger.Debug(itemIds.Key);
                                itemIdsByName.Add(itemIds.Key, itemIds.Value);
                            }
                        }
                    }
                }
                
                Dictionary<string, PointOfInterest> PoisByName = new Dictionary<string, PointOfInterest>();
                {
                    var reader = new StreamReader(ContentsManager.GetFileStream("pois.yaml"));
                    var poiData = deserializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, PointOfInterest>>>>(reader);

                    foreach (var storylineData in poiData)
                    {
                        foreach (var mapData in storylineData.Value)
                        {
                            foreach (var poi in mapData.Value)
                            {
                                PoisByName.Add(poi.Key.Trim(), poi.Value);
                            }
                        }
                    }
                }

                foreach (long location_id in apSession.Locations.AllMissingLocations)
                {
                    string locationName = apSession.Locations.GetLocationNameFromId(location_id);
                    string[] splitName = locationName.Split(' ');
                    if (splitName[0].Equals("Item:"))
                    {
                        success = true;
                        locationChecker.AddItemLocation(false, locationName, itemIdsByName[locationName.Substring(6)]);
                    }
                    else if (splitName[0].Equals("POI:"))
                    {
                        success = true;
                        locationChecker.AddPoiLocation(false, locationName, PoisByName[locationName.Substring(5).Trim()]);
                    }
                }
                filePrefix = apSession.RoomState.Seed + "_" + slotName.Value + "_";

                {
                    string fileName = "ArchipelagoData\\" + filePrefix + "savedData.json";
                    if (SystemFile.Exists(fileName))
                    {
                        var file = SystemFile.OpenRead(fileName);
                        savedData = await JsonSerializer.DeserializeAsync<SavedData>(file);
                    }
                    else
                    {
                        savedData = new SavedData();
                        savedData.CharacterName = (string)slotData["Character"];
                    }
                    characterName.Value = savedData.CharacterName;
                }

                var checkedLocations = new HashSet<long>(apSession.Locations.AllLocationsChecked);

                var tasks = new List<Task>();
                {
                    string fileName = "ArchipelagoData\\" + filePrefix + "AchievementLocations.json";
                    if (SystemFile.Exists(fileName))
                    {
                        var file = SystemFile.OpenRead(fileName);
                        var locations = await JsonSerializer.DeserializeAsync<List<AchievementLocation>>(file);
                        foreach (var location in locations)
                        {
                            logger.Debug("Loaded Location - BitCount: {}, Tier: {}, Repeated: {}", location.BitCount, location.Tier, location.Repeated);
                            if (location.CateoryId != 0)
                            {
                                location.Category = await Gw2ApiManager.Gw2ApiClient.V2.Achievements.Categories.GetAsync(location.CateoryId);
                            }
                            var locationId = apSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName);
                            if (checkedLocations.Contains(locationId))
                            {
                                location.LocationComplete = true;
                            }
                            if (!location.LocationComplete)
                            {
                                
                                tasks.Add(locationChecker.AddAchievmentLocation(location));
                            }
                        }
                    }
                }
                await Task.WhenAll(tasks);

                {
                    string fileName = "ArchipelagoData\\" + filePrefix + "QuestStatus.json";
                    if (SystemFile.Exists(fileName))
                    {
                        var file = SystemFile.OpenRead(fileName);
                        var status = await JsonSerializer.DeserializeAsync<QuestStatus>(file);
                        locationChecker.SetQuestStatus(status);

                    }
                }


                locationChecker.StartTimer();
                logger.Debug("Locations Loaded");

                RefreshItems();
                apSession.Items.ItemReceived += UnlockItem;
            }
            else
            {
                apSession = null;
                logger.Debug("Connetion Attempted Failed");
            }
        }

        private void RefreshItems()
        {
            if (apSession == null)
            {
                return;
            }
            logger.Debug("Refreshing Items list");
            itemCounts.Clear();
            mistFragments = 0;
            foreach (var item in apSession.Items.AllItemsReceived)
            {
                UnlockItem(item);
            }

            logger.Debug("{}", slotData["MistFragmentsRequired"]);
            var mistFragmentsRequired = (Int64)slotData["MistFragmentsRequired"];
            if (mainWindow != null)
            {
                mainWindow.UpdateMistFragments(mistFragments);
            }

            foreach (var data in slotData)
            {
                logger.Debug("slotData[{}]: {} ({})", data.Key, data.Value, data.Value.GetType());
            }

            if (mistFragments >= mistFragmentsRequired)
            {
                var statusUpdatePacket = new StatusUpdatePacket();
                statusUpdatePacket.Status = ArchipelagoClientState.ClientGoal;
                apSession.Socket.SendPacket(statusUpdatePacket);
            }

        }

        private void UnlockItem(Archipelago.MultiClient.Net.Models.ItemInfo itemInfo)
        {
            var itemName = itemInfo.ItemName;
            var itemId = itemInfo.ItemId;
            logger.Debug("Unlock Item {}", itemName);
            if (itemName.Equals("Mist Fragment")) {
                mistFragments++;
                return;
            }
            if (itemCounts.ContainsKey(itemId))
            {
                itemCounts[itemId]++;
            }
            else
            {
                itemCounts[itemId] = 1;
            }

            if (mainWindow != null)
            {
                mainWindow.UpdateItemCount(itemName, itemCounts[itemId]);
            }

        }

        private void UnlockItem(ReceivedItemsHelper helper)
        {
            //prevent simultaneous or recursive entry
            if (!unlockingItems)
            {
                unlockingItems = true;
                RefreshItems();
                unlockingItems = false;
            }
        }

        private async Task<bool> randomAchievement(List<Guid> groups, 
            Dictionary<int, AccountAchievement> 
            achievementProgress, 
            Region region,
            Storyline? storyline,
            string locationName,
            HashSet<int> visitedCategories, 
            HashSet<int> visitedAchievements
        ) {
            while (groups.Count > 0)
            {
                var groupIndex = random.Next(groups.Count);
                var groupGuid = groups[groupIndex];
                var groupReq = achievementData.groups[groupGuid];

                var groupRegion = RegionExtensions.FromName(groupReq);
                var groupStoryline = StorylineExtensions.FromName(groupReq);
                var storylineMatch = (storyline != null && groupStoryline == storyline);

                if (groupRegion != region
                    && !storylineMatch
                    && !groupReq.Equals("Any"))
                {
                    groups.Remove(groupGuid);
                    logger.Debug("Ignoring Group: {}, {}", groupGuid, groupReq);
                    continue;
                }

                var group = await Gw2ApiManager.Gw2ApiClient.V2.Achievements.Groups.GetAsync(groupGuid);
                var categoryList = new List<int>(group.Categories);

                while (categoryList.Count > 0)
                {
                    var categoryIndex = random.Next(categoryList.Count);
                    var categoryId = categoryList[categoryIndex];
                    categoryList.RemoveAt(categoryIndex);

                    if (visitedCategories.Contains(categoryId))
                    {
                        logger.Debug("Ignoring category: {}, already visited", categoryId);
                        continue;
                    }

                    Region? categoryRegion = null;
                    Storyline? categoryStoryline = null;
                    if (achievementData.categories.ContainsKey(categoryId))
                    {
                        var categoryReq = achievementData.categories[categoryId];

                        if (categoryReq.Equals("None"))
                        {
                            visitedCategories.Add(categoryId);
                            logger.Debug("Ignoring Category: {}, {}", categoryId, categoryReq);
                            continue;
                        }

                        categoryRegion = RegionExtensions.FromName(categoryReq);
                        categoryStoryline = StorylineExtensions.FromName(categoryReq);
                        storylineMatch = (storyline != null && categoryStoryline == storyline);

                        if (categoryRegion != region
                            && !storylineMatch
                            && !categoryReq.Equals("Any"))
                        {
                            visitedCategories.Add(categoryId);
                            logger.Debug("Ignoring Category: {}, {}", categoryId, categoryReq);
                            continue;
                        }
                    }

                    var category = await Gw2ApiManager.Gw2ApiClient.V2.Achievements.Categories.GetAsync(categoryId);
                    logger.Debug("Category: {} ({})", category.Name, categoryId);
                    var achievementList = new List<int>(category.Achievements);
                    while (achievementList.Count > 0)
                    {
                        var achievementIndex = random.Next(achievementList.Count);
                        var achievementId = achievementList[achievementIndex];

                        achievementList.RemoveAt(achievementIndex);

                        if (visitedAchievements.Contains(achievementId))
                        {
                            logger.Debug("Ignoring achievment: {}, already visited", achievementId);
                            continue;
                        }
                        visitedAchievements.Add(achievementId);

                        if (locationChecker.HasAchievementLocation(achievementId))
                        {
                            logger.Debug("Ignoring achievment: {}, already selected", achievementId);
                            continue;
                        }


                        Region? achievementRegion = null;
                        Storyline? achievementStoryline = null;
                        if (achievementData.achievments.ContainsKey(achievementId))
                        {
                            var achievementReq = achievementData.achievments[achievementId];

                            achievementRegion = RegionExtensions.FromName(achievementReq);
                            achievementStoryline = StorylineExtensions.FromName(achievementReq);
                            storylineMatch = (storyline != null && achievementStoryline == storyline);

                            if (achievementRegion != region
                                && !storylineMatch
                                && !achievementReq.Equals("Any"))
                            {
                                logger.Debug("Ignoring achievement: {}, {}", achievementId, achievementReq);
                                continue;
                            }
                        }


                        var requiredRegion = groupRegion ?? categoryRegion ?? achievementRegion ?? Region.OPEN_WORLD;
                        if (requiredRegion != region)
                        {
                            logger.Debug("Ignoring achievement: {}, {}", achievementId, requiredRegion.GetName());
                            continue;
                        }

                        var requiredStoryline = groupStoryline ?? categoryStoryline ?? achievementStoryline;
                        if (requiredStoryline != null && storyline != null && requiredStoryline != storyline)
                        {
                            logger.Debug("Ignoring achievement: {}, {}", achievementId, requiredStoryline.Value.GetName());
                            continue;
                        }

                        AccountAchievement progress = null;
                        Achievement achievement = await Gw2ApiManager.Gw2ApiClient.V2.Achievements.GetAsync(achievementId);
                        bool requiresUnlock = false;
                        bool repeatable = false;
                        bool metaAchievement = false;
                        foreach (var flag in achievement.Flags)
                        {
                            if (flag == AchievementFlag.RequiresUnlock)
                            {
                                requiresUnlock = true;
                            }
                            if (flag == AchievementFlag.Repeatable)
                            {
                                repeatable = true;
                            }
                            if (flag == AchievementFlag.CategoryDisplay)
                            {
                                metaAchievement = true;
                            }
                        }
                        if (metaAchievement)
                        {
                            logger.Debug("Ignoring achievment: {}, meta achievement", achievementId);
                            continue;
                        }

                        if (achievementProgress.ContainsKey(achievementId))
                        {

                            progress = achievementProgress[achievementId];
                            if (requiresUnlock && progress.Unlocked != true)
                            {
                                visitedAchievements.Add(achievementId);
                                logger.Debug("Ignoring achievment: {}, locked", achievementId);
                                continue;
                            }

                            if (progress.Done)
                            {
                                


                                if (!repeatable)
                                {
                                    visitedAchievements.Add(achievementId);
                                    logger.Debug("Ignoring achievment: {}, already done", achievementId);
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            if (requiresUnlock)
                            {
                                logger.Debug("Ignoring achievment: {}, locked, no progress", achievementId);
                                continue;
                            }

                            if (achievement.Prerequisites != null)
                            {
                                bool missingPrereq = false;
                                foreach (var prereq in achievement.Prerequisites)
                                {
                                    if (!achievementProgress.ContainsKey(prereq) || !achievementProgress[prereq].Done) {
                                        missingPrereq = true;
                                        logger.Debug("Ignoring achievment: {}, missing prereq: {}", achievementId, prereq);
                                        break;
                                    }
                                }

                                if (missingPrereq)
                                {
                                    continue;
                                }
                            }


                        }
                        
                        await locationChecker.AddAchievmentLocation(achievementId, achievement, category, progress, locationName);
                        return true;

                    }

                    //if a category has no achievements left, ignore it
                    if (achievementList.Count == 0)
                    {
                        visitedCategories.Add(categoryId);
                    }
                }

                //if a group has no cateories left, ignore it
                if (categoryList.Count == 0)
                {
                    groups.Remove(groupGuid);
                }
            }

            logger.Error("No achievement found for {}", locationName);
            return false;
        }

        internal async void GenerateLocations(object sender, MouseEventArgs e)
        {

            if (apSession == null)
            {
                logger.Debug("Cannot generate locations without a connection to the AP Server");
                return;
            }

            logger.Debug("Map Type: {}", GameService.Gw2Mumble.CurrentMap.Type);
            if (GameService.Gw2Mumble.CurrentMap.Type == Gw2Sharp.Models.MapType.CharacterCreate || GameService.Gw2Mumble.CurrentMap.Type == Gw2Sharp.Models.MapType.Redirect)
            {
                logger.Debug("Cannot generate locations from the character select screen.");
                return;
            }

            locationChecker.ClearLocations();
            mainWindow.ClearLocations();

            logger.Debug("Generating Locations");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var reader = new StreamReader(ContentsManager.GetFileStream("achievements.yaml"));
            achievementData = deserializer.Deserialize<AchievementData>(reader);

            foreach (var item in slotData)
            {
                logger.Debug("{key}: {value}", item.Key, item.Value);
            }
            var storyline = (Storyline)Enum.ToObject(typeof(Storyline), slotData["Storyline"]);
            var storylineName = storyline.GetName();
            var regionToGroups = new Dictionary<string, List<Guid>>();
            var regionToCategories = new Dictionary<string, HashSet<int>>();
            var achievementProgress = new Dictionary<int, AccountAchievement>();


            if (Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression }))
            {
                var progress = await Gw2ApiManager.Gw2ApiClient.V2.Account.Achievements.GetAsync();
                 
                foreach (var achievement in progress)
                {
                    achievementProgress[achievement.Id] = achievement;
                }
            }
            else
            {
                logger.Error("No permission for achievements");
            }

            var questIds = new HashSet<int>();

            if (Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Progression } ))
            {
                var season = await Gw2ApiManager.Gw2ApiClient.V2.Stories.Seasons.GetAsync(storyline.GetSeasonId());
                var storyIds = new HashSet<int>(season.Stories);
                var quest_progress = new HashSet<int>(await Gw2ApiManager.Gw2ApiClient.V2.Characters[characterName.Value].Quests.GetAsync());


                foreach (var quest_id in quest_progress)
                {
                    logger.Debug("Quest Entry: {}", quest_id);
                }

                var all_quests = await Gw2ApiManager.Gw2ApiClient.V2.Quests.AllAsync();
                foreach (var quest in all_quests)
                {
                    if (!storyIds.Contains(quest.Story))
                    {
                        logger.Debug("Quest not in story: {} ({})", quest.Name, quest.Id);
                        continue;
                    }

                    if (quest_progress.Contains(quest.Id))
                    {
                        logger.Debug("Quest already done: {}", quest.Name);
                        continue;
                    }

                    

                    logger.Debug("Quest added to list: {} ({})", quest.Name, quest.Id);
                    questIds.Add(quest.Id);
                }

            }
            else
            {
                logger.Error("No permission for quests");
            }
            locationChecker.AddQuestIds(questIds);

            var visitedAchievements = new Dictionary<string, HashSet<int>>();
            var visitedCategories = new Dictionary<string, HashSet<int>>();
            var unsuccessfulLocations = new List<string>();
            var incompleteQuestLocations = 0;
            foreach (long location_id in apSession.Locations.AllMissingLocations) {
                string locationName = apSession.Locations.GetLocationNameFromId(location_id);
                string[] splitName = locationName.Split(' ');
                string regionName = splitName[0];
                string locationType = splitName[1];
                logger.Debug("Generating Location: {}, type: {}, region: {}", locationName, locationType, regionName);
                bool success = false;

                var region = RegionExtensions.FromName(regionName);

                if (region == null)
                {
                    continue;
                }

                if (!regionToGroups.ContainsKey(regionName))
                {
                    regionToGroups[regionName] = new List<Guid>(achievementData.groups.Keys);
                    visitedAchievements[regionName] = new HashSet<int>();
                    visitedCategories[regionName] = new HashSet<int>();
                }
                if (locationType.Equals("ACHIEVEMENT"))
                {
                    success = await randomAchievement(regionToGroups[regionName], achievementProgress, region.Value, storyline, locationName, visitedCategories[regionName], visitedAchievements[regionName]);
                }
                else if (locationType.Equals("QUEST"))
                {
                    success = true;
                    locationChecker.AddQuestLocation(false, locationName);
                    incompleteQuestLocations++;
                }

                if (!success)
                {
                    logger.Debug("Failed to resolve location: {}", locationName);
                    unsuccessfulLocations.Add(locationName);
                }
            }

            foreach (var locationName in unsuccessfulLocations)
            {
                string[] splitName = locationName.Split(' ');
                string locationType = splitName[1];
                logger.Debug("Generating Location: {}, type: {}, region: {}", locationName, locationType, "OPEN_WORLD");
                bool success = false;
                if (locationType.Equals("ACHIEVEMENT"))
                {
                    success = await randomAchievement(regionToGroups["OPEN_WORLD"], achievementProgress, Region.OPEN_WORLD, storyline, locationName, visitedCategories["OPEN_WORLD"], visitedAchievements["OPEN_WORLD"]);
                }

                if (!success)
                {
                    logger.Warn("Sending location {} because it couldn't be filled", locationName);
                    apSession.Locations.CompleteLocationChecks(new long[] { apSession.Locations.GetLocationIdFromName("Guild Wars 2", locationName) });
                }
            }


            logger.Debug("Finished Generating Locations");

            var totalQuestLocation = incompleteQuestLocations;
            foreach (long locationId in apSession.Locations.AllLocationsChecked)
            {
                string locationName = apSession.Locations.GetLocationNameFromId(locationId);
                string[] splitName = locationName.Split(' ');
                string locationType = splitName[1];


                if (locationType.Equals("QUEST"))
                {
                    totalQuestLocation++;
                }
            }
            mainWindow.CreateQuestButton(storyline, incompleteQuestLocations, totalQuestLocation);

            foreach (var location in locationChecker.GetAchievementLocations())
            {
                mainWindow.AddAchievementButton(location);
            }

            var itemLocations = locationChecker.GetItemLocations();
            mainWindow.CreateItemLocationButtons(itemLocations);

            var poiLocations = locationChecker.GetPoiLocations();
            mainWindow.CreatePoiButtons(poiLocations);

            await serialize();

            locationChecker.StartTimer();
        }

        private async Task serialize()
        {
            Directory.CreateDirectory("archipelagoData");
            {
                var stream = SystemFile.Create("archipelagoData\\" + filePrefix + "AchievementLocations.json");

                await JsonSerializer.SerializeAsync(stream, locationChecker.GetAchievementLocations());
            }
            {
                var stream = SystemFile.Create("archipelagoData\\" + filePrefix + "QuestStatus.json");

                await JsonSerializer.SerializeAsync(stream, locationChecker.GetQuestStatus());
            }
            {
                var stream = SystemFile.Create("archipelagoData\\" + filePrefix + "savedData.json");
                savedData.CharacterName = characterName.Value;

                await JsonSerializer.SerializeAsync(stream, savedData);
            }
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            logger.Debug("Load Complete");

            // Base handler must be called
            base.OnModuleLoaded(e);


            mainWindow = new ArchipelagoWindow(new XnaRectangle(40, 26, 913, 691), new XnaRectangle(70, 71, 839, 605), locationChecker, slotData, apSession, this);


            

            logger.Debug("Loading icon");
            cornerIcon = new CornerIcon
            {
                IconName = this.Name,
                Icon = ContentsManager.GetTexture("archipelago.png")
            };
            cornerIcon.Click += ToggleWindow;


            RefreshItems();
        }

        private void UpdateAchievementProgress (AchievementLocation location)
        {
            logger.Debug("Updating UI for {name}", location.Achievement.Name);
        }

        private void SendLocationCompletion (AchievementLocation location)
        {
            logger.Debug("Sending location {} for achievement {}", location.LocationName, location.Achievement.Name);
            apSession.Locations.CompleteLocationChecks(new long[] { apSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName) });
            serialize();
        }

        private void SendLocationCompletion(Location location, int questId)
        {
            logger.Debug("Sending location {} for quest {}", location.LocationName, questId);
            apSession.Locations.CompleteLocationChecks(new long[] { apSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName) });
            serialize();
        }

        private void SendLocationCompletion(Location location)
        {
            logger.Debug("Sending location for {}", location.LocationName);
            apSession.Locations.CompleteLocationChecks(new long[] { apSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName) });
            serialize();
        }


        protected void ToggleWindow(object sender, MouseEventArgs e)
        {
            mainWindow.ToggleWindow();
        }

        protected override void Update(GameTime gameTime)
        {

        }

        /// <inheritdoc />
        protected override void Unload()
        {
            // Unload here

            // All static members must be manually unset
        }

    }

}
