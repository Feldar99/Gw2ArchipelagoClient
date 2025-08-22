using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Archipelago;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.ServiceModel.Channels;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SlotData = System.Collections.Generic.Dictionary<string, object>;
using SystemFile = System.IO.File;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

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
        private SettingEntry<Profession> profession;
        private SettingEntry<Race> race;

        private string  filePrefix;

        internal LocationChecker LocationChecker { private set; get; }
        internal SlotData SlotData { private set; get; }
        internal ArchipelagoSession ApSession { private set; get; }
        internal ItemTracker ItemTracker { private set; get; } = new ItemTracker();
        internal MapAccessTracker MapAccessTracker { private set; get; } = new MapAccessTracker();

        private CornerIcon                     cornerIcon;
        private ArchipelagoWindow              mainWindow;

        private AchievementData genericAchievementData;
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
            apServerUrl   = settings.DefineSetting("apServerUrl", "archipelago.gg:<port>", () => "Archipelago Server URL");
            slotName      = settings.DefineSetting("apSlotName", "", () => "Slot Name");
            profession    = settings.DefineSetting("profession", Profession.Warrior, () => "Profession", () => "The profession of the character you will be using for this Archipelago slot");
            race          = settings.DefineSetting("race", Race.Human, () => "Race", () => "The race of the character you will be using for this Archipelago slot");
            characterName = settings.DefineSetting("characterName", "", () => "Character Name", () => "The name of the character you will be using for this Archipelago slot");
            characterName.PropertyChanged += OnCharacterNameChange;

            profession.SetDisabled(true);
            race.SetDisabled(true);
        }

        private void OnCharacterNameChange(object sender, PropertyChangedEventArgs e)
        {
            if (savedData != null)
            {
                savedData.CharacterName = characterName.Value;
                serialize();
            }
        }

        protected override void Initialize()
        {
            logger.Debug("Initialize");
            LocationChecker = new LocationChecker(this, characterName);
            LocationChecker.AchievementCompleted += SendLocationCompletion;
            LocationChecker.QuestCompleted += SendLocationCompletion;
            LocationChecker.ItemAcquired += SendLocationCompletion;
            LocationChecker.PoiDiscovered += SendLocationCompletion;
        }

        protected override async Task LoadAsync()
        {
            logger.Debug("Load");
            MapAccessTracker.LoadMaps(ContentsManager);
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
            LocationChecker.ClearLocations();

            ApSession = null;

        }

        private struct ItemData
        {
            public string Name;
            public List<int> ItemIds;
            public string Region;
        }

        private async Task ConnectToArchipelago()
        {
            if (ApSession != null)
            {
                DisconnectFromArchipelago();
            }
            logger.Debug("Connecting to " + apServerUrl.Value);
            bool success = false;
            LoginResult result = null;
            try
            {
                ApSession = ArchipelagoSessionFactory.CreateSession(apServerUrl.Value);
                result = ApSession.TryConnectAndLogin("Guild Wars 2", slotName.Value, Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems);
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
                SlotData = loginSuccess.SlotData;
                logger.Debug("Connetion Successful");

                Profession characterProfession = (Profession)Enum.ToObject(typeof(Profession), SlotData["CharacterProfession"]);
                Race characterRace = (Race)Enum.ToObject(typeof(Race), SlotData["CharacterRace"]);
                var storyline = (Storyline)Enum.ToObject(typeof(Storyline), SlotData["Storyline"]);

                profession.Value = characterProfession;
                race.Value = characterRace;

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(LowerCaseNamingConvention.Instance)
                    .Build();

                await AddNonGenericLocations();

                filePrefix = DirectoriesManager.GetFullDirectoryPath("ArchipelagoData") + "\\" + ApSession.RoomState.Seed + "_" + slotName.Value + "_";
                {
                    var file = waitForFile("savedData.json", 5, true /* readonly */);
                    if (file != null)
                    {
                        savedData = await JsonSerializer.DeserializeAsync<SavedData>(file);
                    }
                    else if (savedData == null)
                    {
                        savedData = new SavedData();
                        savedData.CharacterName = (string)SlotData["Character"];
                    }

                    if (!savedData.CharacterName.Equals("New Character"))
                    {
                        characterName.Value = savedData.CharacterName;
                    }
                }

                var checkedLocations = new HashSet<long>(ApSession.Locations.AllLocationsChecked);

                var tasks = new List<Task>();
                {
                    var file = waitForFile("AchievementLocations.json", 5, true);
                    if (file != null)
                    {
                        var locations = await JsonSerializer.DeserializeAsync<List<AchievementLocation>>(file);
                        foreach (var location in locations)
                        {
                            logger.Debug("Loaded Location - BitCount: {}, Tier: {}, Repeated: {}", location.BitCount, location.Tier, location.Repeated);
                            if (location.CateoryId != 0)
                            {
                                location.Category = await Gw2ApiManager.Gw2ApiClient.V2.Achievements.Categories.GetAsync(location.CateoryId);
                            }
                            var locationId = ApSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName);
                            if (checkedLocations.Contains(locationId))
                            {
                                location.LocationComplete = true;
                            }
                            if (!location.LocationComplete)
                            {

                                tasks.Add(LocationChecker.AddAchievmentLocation(location));
                            }
                        }
                    }
                }
                await Task.WhenAll(tasks);

                {
                    var file = waitForFile("QuestStatus.json", 5, true);
                    string fileName = filePrefix + "QuestStatus.json";
                    if (SystemFile.Exists(fileName))
                    {
                        var status = await JsonSerializer.DeserializeAsync<QuestStatus>(file);
                        LocationChecker.SetQuestStatus(status);

                    }
                }


                LocationChecker.StartTimer();
                logger.Debug("Locations Loaded");

                ItemTracker.Initialize(ApSession);
                ItemTracker.ItemUnlocked += OnItemUnlocked;
                MapAccessTracker.Initialize(storyline, characterRace, ItemTracker);
            }
            else
            {
                ApSession = null;
                logger.Debug("Connetion Attempted Failed");
            }
        }

        private async Task AddNonGenericLocations()
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(LowerCaseNamingConvention.Instance)
                .Build();

            logger.Debug("Reading Items");
            Dictionary<string, ItemData> itemDataByName = new Dictionary<string, ItemData>();
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
                            itemDataByName.Add(itemIds.Key, new ItemData { ItemIds = itemIds.Value, Name = itemIds.Key, Region = mapData.Key });
                        }
                    }
                }
            }

            logger.Debug("Reading POIs");
            Dictionary<string, PointOfInterest> PoisByName = new Dictionary<string, PointOfInterest>();
            {
                var reader = new StreamReader(ContentsManager.GetFileStream("pois.yaml"));
                var poiData = deserializer.Deserialize<Dictionary<string, Dictionary<string, PointOfInterest>>>(reader);

                foreach (var mapData in poiData)
                {
                    foreach (var poi in mapData.Value)
                    {
                        PoisByName.Add(poi.Key.Trim(), poi.Value);
                    }
                }
            }

            var tasks = new List<Task>();
            foreach (long location_id in ApSession.Locations.AllMissingLocations)
            {
                logger.Debug("location_id: {}", location_id);
                string locationName = ApSession.Locations.GetLocationNameFromId(location_id);
                string[] splitName = locationName.Split(' ');
                if (splitName[0].Equals("Item:"))
                {
                    ItemData itemData = itemDataByName[locationName.Substring(6)];
                    LocationChecker.AddItemLocation(false, locationName, itemData.ItemIds, itemData.Region);
                }
                else if (splitName[0].Equals("POI:"))
                {
                    tasks.Add(LocationChecker.AddPoiLocation(false, locationName, PoisByName[locationName.Substring(5).Trim()]));
                }
            }
            await Task.WhenAll(tasks);
        }

        private void OnItemUnlocked(string itemName, int unlockCount)
        {
            if (itemName.Equals("Mist Fragment"))
            {
                if (unlockCount >= (Int64)SlotData["MistFragmentsRequired"])
                {
                    var statusUpdatePacket = new StatusUpdatePacket();
                    statusUpdatePacket.Status = ArchipelagoClientState.ClientGoal;
                    ApSession.Socket.SendPacket(statusUpdatePacket);
                }
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
                var groupReq = genericAchievementData.groups[groupGuid];

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
                    if (genericAchievementData.categories.ContainsKey(categoryId))
                    {
                        var categoryReq = genericAchievementData.categories[categoryId];

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

                        if (LocationChecker.HasAchievementLocation(achievementId))
                        {
                            logger.Debug("Ignoring achievment: {}, already selected", achievementId);
                            continue;
                        }


                        Region? achievementRegion = null;
                        Storyline? achievementStoryline = null;
                        if (genericAchievementData.achievments.ContainsKey(achievementId))
                        {
                            var achievementReq = genericAchievementData.achievments[achievementId];

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
                        
                        await LocationChecker.AddAchievmentLocation(achievementId, achievement, category, progress, locationName, region.GetName());
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

            if (ApSession == null)
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

            LocationChecker.ClearLocations();

            logger.Debug("Generating Locations");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            await AddNonGenericLocations();

            var reader = new StreamReader(ContentsManager.GetFileStream("achievements_generic.yaml"));
            genericAchievementData = deserializer.Deserialize<AchievementData>(reader);

            foreach (var item in SlotData)
            {
                logger.Debug("{key}: {value}", item.Key, item.Value);
            }
            var storyline = (Storyline)Enum.ToObject(typeof(Storyline), SlotData["Storyline"]);
            var storylineName = storyline.GetName();
            var groups = new Dictionary<Storyline, Dictionary<string, List<Guid>>>();
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

            var quests = new HashSet<Quest>();

            if (Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Characters, TokenPermission.Progression } ))
            {
                var season = await Gw2ApiManager.Gw2ApiClient.V2.Stories.Seasons.GetAsync(storyline.GetSeasonId());
                var storyIds = new HashSet<int>(season.Stories);
                try
                {
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
                        quests.Add(quest);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn("Could not generate quest locations for {}", characterName.Value);
                }

            }
            else
            {
                logger.Error("No permission for quests");
            }
            LocationChecker.AddQuests(quests);

            var visitedAchievements = new Dictionary<Storyline, Dictionary<string, HashSet<int>>>();
            var visitedCategories = new Dictionary<Storyline, Dictionary<string, HashSet<int>>>();
            var unsuccessfulLocations = new List<string>();
            var incompleteQuestLocations = 0;
            foreach (long location_id in ApSession.Locations.AllMissingLocations) {
                string locationName = ApSession.Locations.GetLocationNameFromId(location_id);
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

                if (!groups.ContainsKey(storyline))
                {
                    groups[storyline] = new Dictionary<string, List<Guid>>();
                    visitedAchievements[storyline] = new Dictionary<string, HashSet<int>>();
                    visitedCategories[storyline] = new Dictionary<string, HashSet<int>>();
                }
                if (!groups[storyline].ContainsKey(regionName))
                {
                    groups[storyline][regionName] = new List<Guid>(genericAchievementData.groups.Keys);
                    visitedAchievements[storyline][regionName] = new HashSet<int>();
                    visitedCategories[storyline][regionName] = new HashSet<int>();
                }
                if (locationType.Equals("ACHIEVEMENT"))
                {
                    success = await randomAchievement(groups[storyline][regionName], achievementProgress, region.Value, storyline, locationName, visitedCategories[storyline][regionName], visitedAchievements[storyline][regionName]);
                }
                else if (locationType.Equals("QUEST"))
                {
                    success = true;
                    LocationChecker.AddQuestLocation(false, locationName, "Story");
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
                    success = await randomAchievement(groups[storyline]["OPEN_WORLD"], achievementProgress, Region.OPEN_WORLD, storyline, locationName, visitedCategories[storyline]["OPEN_WORLD"], visitedAchievements[storyline]["OPEN_WORLD"]);
                    var fallbackStoryline = storyline.GetFallback();
                    while (!success && fallbackStoryline != null)
                    {

                        if (!groups.ContainsKey(fallbackStoryline.Value))
                        {
                            groups[fallbackStoryline.Value] = new Dictionary<string, List<Guid>>();
                            visitedAchievements[fallbackStoryline.Value] = new Dictionary<string, HashSet<int>>();
                            visitedCategories[fallbackStoryline.Value] = new Dictionary<string, HashSet<int>>();
                        }
                        if (!groups[fallbackStoryline.Value].ContainsKey("OPEN_WORLD"))
                        {
                            groups[fallbackStoryline.Value]["OPEN_WORLD"] = new List<Guid>(genericAchievementData.groups.Keys);
                            visitedAchievements[fallbackStoryline.Value]["OPEN_WORLD"] = new HashSet<int>();
                            visitedCategories[fallbackStoryline.Value]["OPEN_WORLD"] = new HashSet<int>();
                        }


                        success = await randomAchievement(groups[fallbackStoryline.Value]["OPEN_WORLD"], achievementProgress, Region.OPEN_WORLD, fallbackStoryline, locationName, visitedCategories[fallbackStoryline.Value]["OPEN_WORLD"], visitedAchievements[fallbackStoryline.Value]["OPEN_WORLD"]);
                        fallbackStoryline = fallbackStoryline.Value.GetFallback();
                    }
                }

                if (!success)
                {
                    logger.Warn("Sending location {} because it couldn't be filled", locationName);
                    ApSession.Locations.CompleteLocationChecks(new long[] { ApSession.Locations.GetLocationIdFromName("Guild Wars 2", locationName) });
                }
            }


            logger.Debug("Finished Generating Locations");

            var totalQuestLocation = incompleteQuestLocations;
            foreach (long locationId in ApSession.Locations.AllLocationsChecked)
            {
                string locationName = ApSession.Locations.GetLocationNameFromId(locationId);
                string[] splitName = locationName.Split(' ');
                string locationType = splitName[1];


                if (locationType.Equals("QUEST"))
                {
                    totalQuestLocation++;
                }
            }
            mainWindow.Refresh();

            await serialize();

            LocationChecker.StartTimer();
        }


        private FileStream waitForFile(string filename, int timeout, bool readOnly)
        {
            for (int i = 0; i < timeout; ++i)
            {
                try
                {
                    if (readOnly)
                    {
                        if (!SystemFile.Exists(filePrefix + filename))
                        {
                            return null;
                        }
                        return new FileStream(filePrefix + filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    else
                    {
                        return new FileStream(filePrefix + filename, FileMode.Create, FileAccess.Write, FileShare.None);
                    }
                }
                catch (Exception)
                {

                }
                System.Threading.Thread.Sleep(1000);
            }
            return null;
        }

        private async Task serialize()
        {
            {
                FileStream stream = waitForFile("AchievementLocations.json", 10, false);
                if (stream == null)
                {
                    return;
                }
                await JsonSerializer.SerializeAsync(stream, LocationChecker.GetAchievementLocations());
            }
            {
                FileStream stream = waitForFile("QuestStatus.json", 10, false);
                if (stream == null)
                {
                    return;
                }
                await JsonSerializer.SerializeAsync(stream, LocationChecker.GetQuestStatus());
            }
            {
                FileStream stream = waitForFile("savedData.json", 10, false);
                if (stream == null)
                {
                    return;
                }
                savedData.CharacterName = characterName.Value;

                await JsonSerializer.SerializeAsync(stream, savedData);
            }
        }

        protected override void OnModuleLoaded(EventArgs e)
        {
            logger.Debug("Load Complete");

            // Base handler must be called
            base.OnModuleLoaded(e);

            if (mainWindow == null)
            {
                mainWindow = new ArchipelagoWindow(new XnaRectangle(40, 26, 913, 691), new XnaRectangle(70, 71, 839, 605), this);

                logger.Debug("Loading icon");
                cornerIcon = new CornerIcon
                {
                    IconName = this.Name,
                    Icon = ContentsManager.GetTexture("archipelago.png")
                };
                cornerIcon.Click += ToggleWindow;
            }
        }

        private async void SendLocationCompletion (AchievementLocation location)
        {
            logger.Debug("Sending location {} for achievement {}", location.LocationName, location.Achievement.Name);
            ApSession.Locations.CompleteLocationChecks(new long[] { ApSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName) });
            location.LocationComplete = true;
            await serialize();
        }

        private async void SendLocationCompletion(Location location, Quest quest)
        {
            logger.Debug("Sending location {} for quest {}", location.LocationName, quest.Id);
            ApSession.Locations.CompleteLocationChecks(new long[] { ApSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName) });
            location.LocationComplete = true;
            await serialize();
        }

        private async void SendLocationCompletion(Location location)
        {
            logger.Debug("Sending location for {}", location.LocationName);
            long locationId = ApSession.Locations.GetLocationIdFromName("Guild Wars 2", location.LocationName);
            ApSession.Locations.CompleteLocationChecks(locationId);
            location.LocationComplete = true;
            await serialize();
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
