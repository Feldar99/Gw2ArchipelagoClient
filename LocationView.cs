using Archipelago.MultiClient.Net;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using SlotData = System.Collections.Generic.Dictionary<string, object>;
using Blish_HUD.Input;

namespace Gw2Archipelago
{
    public class LocationView : View
    {

        private static readonly Logger logger = Logger.GetLogger<LocationView>();

        private ContentsManager contentsManager;
        private Panel locationPanel;
        private Scrollbar locationScrollbar;
        private int locationPanelY = 0;
        private StandardButton connectButton;
        private StandardButton generateButton;
        private Dictionary<int, DetailsButton> achievementButtons = new Dictionary<int, DetailsButton>();
        private DetailsButton questButton;
        private Dictionary<string, DetailsButton> genericLocationButtons = new Dictionary<string, DetailsButton>();
        private Container container;
        private SlotData slotData;
        private LocationChecker locationChecker;


        public event EventHandler<Blish_HUD.Input.MouseEventArgs> ConnectButtonClick;
        public event EventHandler<Blish_HUD.Input.MouseEventArgs> GenerateButtonClick;

        internal LocationView(ContentsManager contentsManager, LocationChecker locationChecker, SlotData slotData)
        {
            this.contentsManager = contentsManager;
            this.locationChecker = locationChecker;
            this.slotData = slotData;
        }

        protected override void Build(Container container) { 
            this.container = container;
            locationPanel = new Panel()
            {
                Location = new Point(20, 35),
                Size = new Point(350, 520),
                Parent = container,
            };

            locationScrollbar = new Scrollbar(locationPanel)
            {
                Location = new Point(10, 35),
                Size = new Point(10, 570),
                Parent = container,
            };


            connectButton = new StandardButton()
            {
                Text = "Connect",
                Size = new Point(150, 30),
                Location = new Point(0, 0),
                Parent = container,
            };
            connectButton.Click += connect;
            logger.Debug("Connect Button: {}, Parent: {}", connectButton, container);

            generateButton = new StandardButton()
            {
                Text = "Generate Locations",
                Size = new Point(150, 30),
                Location = new Point(150, 0),
                Parent = container,
            };
            generateButton.Click += generateLocations;


            locationChecker.AchievementProgressed += UpdateAchievementProgress;
            locationChecker.QuestCompleted += IncrementQuestCounter;
            locationChecker.ItemAcquired += MarkGenericLocationComplete;
            locationChecker.PoiDiscovered += MarkGenericLocationComplete;

            if (slotData != null)
            {
                Initialize(slotData);
            }
        }

        public void Initialize(SlotData slotData) {
            this.slotData = slotData;

            Refresh();
        }

        private void Refresh()
        {
            ClearLocations();

            var storyline = (Storyline)Enum.ToObject(typeof(Storyline), slotData["Storyline"]);
            var incompleteQuests = locationChecker.GetQuestLocations().Count;
            var completeQuests = locationChecker.GetCompleteQuestCount();
            CreateQuestButton(storyline, incompleteQuests, completeQuests);

            var itemLocations = locationChecker.GetItemLocations();
            CreateItemLocationButtons(itemLocations);

            var poiLocations = locationChecker.GetPoiLocations();
            CreatePoiButtons(poiLocations);

            var achievementLocations = locationChecker.GetAchievementLocations();
            foreach (var achievementLocation in achievementLocations)
            {
                AddAchievementButton(achievementLocation);
            }
        }

        internal void ClearLocations()
        {
            foreach (var button in achievementButtons)
            {
                locationPanel.RemoveChild(button.Value);
            }
            achievementButtons.Clear();

            if (questButton != null)
            {
                locationPanel.RemoveChild(questButton);
                questButton = null;
            }
            foreach (var button in genericLocationButtons)
            {
                locationPanel.RemoveChild(button.Value);
            }

            genericLocationButtons.Clear();

            locationPanelY = 0;
        }

        internal void CreateItemLocationButtons(IEnumerable<ItemLocation> itemLocations)
        {

            Texture2D icon = contentsManager.GetTexture("archipelago64.png");

            foreach (var location in itemLocations)
            {

                var button = new DetailsButton()
                {
                    Text = location.LocationName,
                    Icon = icon,
                    MaxFill = 1,
                    ShowFillFraction = true,
                    FillColor = XnaColor.White
                };
                button.Parent = locationPanel;
                button.Location = new Point(0, locationPanelY);
                locationPanelY += questButton.Height + 5;
                button.CurrentFill = location.LocationComplete ? 1 : 0;
                genericLocationButtons.Add(location.LocationName, button);
            }

        }

        internal void CreatePoiButtons(IEnumerable<PoiLocation> poiLocations)
        {

            Texture2D icon = contentsManager.GetTexture("archipelago64.png");

            foreach (var location in poiLocations)
            {

                var button = new DetailsButton()
                {
                    Text = location.LocationName,
                    Icon = icon,
                    MaxFill = 1,
                    ShowFillFraction = true,
                    FillColor = XnaColor.White
                };
                button.Parent = locationPanel;
                button.Location = new Point(0, locationPanelY);
                locationPanelY += questButton.Height + 5;
                button.CurrentFill = location.LocationComplete ? 1 : 0;
                genericLocationButtons.Add(location.LocationName, button);
            }
        }

        internal void CreateQuestButton(Storyline storyline, int incompleteQuestCount, int completeQuestCount)
        {

            Texture2D icon = contentsManager.GetTexture("archipelago64.png");

            questButton = new DetailsButton()
            {
                Text = storyline.GetName() + " Quests",
                Icon = icon,
                MaxFill = completeQuestCount + incompleteQuestCount,
                ShowFillFraction = true,
                FillColor = XnaColor.White
            };
            questButton.Parent = locationPanel;
            questButton.Location = new Point(0, locationPanelY);
            locationPanelY += questButton.Height + 5;
            questButton.CurrentFill = completeQuestCount;
        }

        internal void AddAchievementButton(AchievementLocation achievementLocation)
        {

            var achievement = achievementLocation.Achievement;
            var category = achievementLocation.Category;
            var progress = achievementLocation.Progress;

            logger.Debug("Create button for Achievement: {}", achievement.Name);


            var button = new DetailsButton()
            {
                Text = achievement.Name + " (" + achievementLocation.LocationName + ")",
                MaxFill = achievement.Tiers[achievementLocation.Tier].Count,
                ShowFillFraction = true,
                FillColor = XnaColor.White
            };
            button.Parent = locationPanel;
            button.Location = new Point(0, locationPanelY);
            locationPanelY += button.Height + 5;

            var iconPath = achievement.Icon.Url;
            if (iconPath == null && category != null)
            {
                iconPath = category.Icon.Url;
            }

            if (iconPath != null)
            {
                var relativePath = iconPath.LocalPath.Split(new char[] { '/' }, 3)[2].Split('.')[0];
                logger.Debug("iconPath: {}, relativePath: {}", iconPath, relativePath);
                button.Icon = GameService.Content.GetRenderServiceTexture(relativePath);
            }
            else
            {
                button.Icon = contentsManager.GetTexture("archipelago64.png");
            }

            achievementButtons.Add(achievement.Id, button);

            if (progress != null)
            {
                UpdateAchievementProgress(achievement, progress);
            }
        }

        internal void UpdateAchievementProgress(AchievementLocation location)
        {
            UpdateAchievementProgress(location.Achievement, location.Progress);
        }

        internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        {
            var button = achievementButtons[achievement.Id];
            logger.Debug("Updating Progress Button: {}/{}", progress.Current, progress.Max);
            button.CurrentFill = progress.Current;
        }

        internal void MarkGenericLocationComplete(Location location)
        {
            DetailsButton button;
            if (genericLocationButtons.TryGetValue(location.LocationName, out button))
            {
                button.CurrentFill = button.MaxFill;
            }
        }

        internal void IncrementQuestCounter(Location location, int questId)
        {
            questButton.CurrentFill++;
        }

        private void connect(object sender, MouseEventArgs e)
        {
            ConnectButtonClick.Invoke(sender, e);
        }

        private void generateLocations(object sender, MouseEventArgs e)
        {
            GenerateButtonClick.Invoke(sender, e);
        }
    }
}
