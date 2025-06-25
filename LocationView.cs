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
using System.Runtime.Remoting.Channels;

namespace Gw2Archipelago
{
    public class LocationView : View
    {

        private static readonly Logger logger = Logger.GetLogger<LocationView>();

        private Panel regionPanel;
        private Scrollbar regionScrollbar;
        private Panel locationPanel;
        private Scrollbar locationScrollbar;
        private int locationPanelY = 0;
        private StandardButton connectButton;
        private StandardButton generateButton;
        private Dictionary<int, DetailsButton> achievementButtons = new Dictionary<int, DetailsButton>();
        private Dictionary<string, DetailsButton> genericLocationButtons = new Dictionary<string, DetailsButton>();
        private Container container;
        private string selectedRegion;
        private Module module;


        public event EventHandler<Blish_HUD.Input.MouseEventArgs> ConnectButtonClick;
        public event EventHandler<Blish_HUD.Input.MouseEventArgs> GenerateButtonClick;

        internal LocationView(Module module)
        {
            this.module = module;
            module.MapAccessTracker.MapAccessChanged += OnMapAccessChange;
        }

        private void OnMapAccessChange(string mapName, MapAccessTracker.MapAccessState state)
        {
            RefreshRegions();
        }

        protected override void Build(Container container) { 
            this.container = container;

            regionPanel = new Panel()
            {
                Location = new Point(40, 35),
                Size = new Point(350, 520),
                Parent = container,
            };

            regionScrollbar = new Scrollbar(regionPanel)
            {
                Location = new Point(30, 35),
                Size = new Point(10, 570),
                Parent = container,
            };

            locationPanel = new Panel()
            {
                Location = new Point(410, 35),
                Size = new Point(350, 520),
                Parent = container,
            };

            locationScrollbar = new Scrollbar(locationPanel)
            {
                Location = new Point(400, 35),
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


            module.LocationChecker.AchievementProgressed += UpdateAchievementProgress;
            module.LocationChecker.QuestCompleted += OnQuestComplete;
            module.LocationChecker.ItemAcquired += MarkGenericLocationComplete;
            module.LocationChecker.PoiDiscovered += MarkGenericLocationComplete;

            Refresh();
        }

        internal void Refresh()
        {
            if (module.ApSession == null)
            {
                return;
            }
            
            RefreshRegions();
            ClearLocations();
            

            var itemLocations = module.LocationChecker.GetItemLocations();
            CreateItemLocationButtons(itemLocations);

            var poiLocations = module.LocationChecker.GetPoiLocations();
            CreatePoiButtons(poiLocations);

            var achievementLocations = module.LocationChecker.GetAchievementLocations();
            foreach (var achievementLocation in achievementLocations)
            {
                if (selectedRegion == null || selectedRegion.Equals(achievementLocation.Region))
                {
                    AddAchievementButton(achievementLocation);
                }
            }

            if ("Quests".Equals(selectedRegion))
            {
                Texture2D icon = module.ContentsManager.GetTexture("archipelago64.png");

                var questStatus = module.LocationChecker.GetQuestStatus();
                var quests = new List<Quest>(questStatus.Quests.Values);
                quests.Sort(CompareQuestsById);
                foreach (var quest in  questStatus.Quests.Values)
                {

                    var button = new DetailsButton()
                    {
                        Text = quest.Name,
                        Icon = icon,
                        MaxFill = 1,
                        ShowFillFraction = true,
                        FillColor = XnaColor.White
                    };
                    button.Parent = locationPanel;
                    button.Location = new Point(0, locationPanelY);
                    locationPanelY += button.Height + 5;
                    button.CurrentFill = 0;
                    genericLocationButtons.Add(quest.Name, button);

                }
            }
        }

        private static int CompareQuestsById(Quest questA, Quest questB)
        {
            return questA.Id - questB.Id;
        }

        internal void ClearLocations()
        {
            foreach (var button in achievementButtons)
            {
                locationPanel.RemoveChild(button.Value);
            }
            achievementButtons.Clear();
            foreach (var button in genericLocationButtons)
            {
                locationPanel.RemoveChild(button.Value);
            }

            genericLocationButtons.Clear();

            locationPanelY = 0;
        }

        internal void RefreshRegions()
        {
            regionPanel.ClearChildren();

            Texture2D icon = module.ContentsManager.GetTexture("archipelago64.png");

            int y = 0;

            var storyline = (Storyline)Enum.ToObject(typeof(Storyline), module.SlotData["Storyline"]);
            var incompleteQuestCount = module.LocationChecker.GetQuestLocations().Count;
            var completeQuestCount = module.LocationChecker.GetCompleteQuestCount();

            CreateRegionButton(icon, ref y, storyline.GetName() + " Quests", "Quests", completeQuestCount, completeQuestCount + incompleteQuestCount);

            foreach (var region in module.LocationChecker.Regions)
            {
                var locations = module.LocationChecker.GetLocationsInRegion(region);
                var completeCount = locations.Count(loc => loc.LocationComplete);
                CreateRegionButton(icon, ref y, region, region, completeCount, locations.Count);
            }
        }

        private void CreateRegionButton(Texture2D icon, ref int y, string text, string region, int currentFill, int maxFill)
            {
            if (module.MapAccessTracker.IsLocked(region))
            {
                text += " (Locked)";
            }
            else if (!module.MapAccessTracker.HasAccess(region))
            {
                text += " (Out of Logic)";
            }
                var button = new DetailsButton()
                {
                Text = text,
                    Icon = icon,
                ShowFillFraction = (maxFill > 0),
                    Parent = regionPanel,
                    Location = new Point(0, y),
                FillColor = XnaColor.White
                };
            if (maxFill > 0)
            {
                button.MaxFill = maxFill;
                button.CurrentFill = currentFill;
            }
                y += button.Height + 5;
                button.Click += (sender, e) =>
                {
                    if (selectedRegion != null && selectedRegion.Equals(region))
                    {
                        selectedRegion = null;
                    }
                    else
                    {
                        selectedRegion = region;
                    }
                    Refresh();
                };
        }

        internal void CreateItemLocationButtons(IEnumerable<ItemLocation> itemLocations)
        {

            Texture2D icon = module.ContentsManager.GetTexture("archipelago64.png");

            foreach (var location in itemLocations)
            {
                if (selectedRegion != null && !location.Region.Equals(selectedRegion))
                {
                    continue;
                }

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
                locationPanelY += button.Height + 5;
                button.CurrentFill = location.LocationComplete ? 1 : 0;
                genericLocationButtons.Add(location.LocationName, button);
            }

        }

        internal void CreatePoiButtons(IEnumerable<PoiLocation> poiLocations)
        {

            Texture2D icon = module.ContentsManager.GetTexture("archipelago64.png");

            foreach (var location in poiLocations)
            {
                if (selectedRegion != null && !location.Region.Equals(selectedRegion))
                {
                    continue;
                }

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
                locationPanelY += button.Height + 5;
                button.CurrentFill = location.LocationComplete ? 1 : 0;
                genericLocationButtons.Add(location.LocationName, button);
            }
        }

        internal void AddAchievementButton(AchievementLocation achievementLocation)
        {

            var achievement = achievementLocation.Achievement;
            var category = achievementLocation.Category;
            var progress = achievementLocation.Progress;

            logger.Debug("Create button for Achievement: {}", achievement.Name);

            var maxFill = 0;
            if (achievementLocation.Done) {
                maxFill = achievement.Tiers[achievement.Tiers.Count - 1].Count;
            }
            else
            {
                maxFill = achievement.Tiers[achievementLocation.Tier].Count;
            }


            var button = new DetailsButton()
            {
                Text = achievement.Name + " (" + achievementLocation.LocationName + ")",
                MaxFill = maxFill,
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
                button.Icon = module.ContentsManager.GetTexture("archipelago64.png");
            }

            achievementButtons.Add(achievement.Id, button);

            if (progress != null)
            {
                UpdateAchievementProgress(achievementLocation);
            }
        }

        internal void UpdateAchievementProgress(AchievementLocation location)
        {
            var achievement = location.Achievement;
            var progress    = location.Progress;

            var button = achievementButtons[achievement.Id];
            if (location.LocationComplete)
            {
                button.CurrentFill = button.MaxFill;
            }
            else
            {
                button.CurrentFill = progress.Current;
            }
            //UpdateAchievementProgress(achievement, location.Progress);
        }

        //internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        //{
        //    var button = achievementButtons[achievement.Id];
        //    logger.Debug("Updating Progress Button: {}/{}", progress.Current, progress.Max);
        //    if (progress.Done)
        //    {
        //        button.CurrentFill = button.MaxFill;
        //    }
        //    else
        //    {
        //        button.CurrentFill = progress.Current;
        //    }
        //}

        internal void MarkGenericLocationComplete(string locationName)
        {
            DetailsButton button;
            if (genericLocationButtons.TryGetValue(locationName, out button))
            {
                button.CurrentFill = button.MaxFill;
            }
            RefreshRegions();
        }

        internal void MarkGenericLocationComplete(Location location)
        {
            MarkGenericLocationComplete(location.LocationName);
        }

        internal void OnQuestComplete(Location location, Quest quest)
        {
            MarkGenericLocationComplete(quest.Name);
            RefreshRegions();
        }

        private void connect(object sender, MouseEventArgs e)
        {
            ConnectButtonClick.Invoke(sender, e);
            Refresh();
        }

        private void generateLocations(object sender, MouseEventArgs e)
        {
            GenerateButtonClick.Invoke(sender, e);
            Refresh();
        }
    }
}
