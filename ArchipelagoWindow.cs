using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
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

namespace Gw2Archipelago
{
    class ArchipelagoWindow : StandardWindow
    {

        private static readonly Logger logger = Logger.GetLogger<Module>();

        private ContentsManager contentsManager;
        private Panel locationPanel;
        private Scrollbar locationScrollbar;
        private int locationPanelY = 0;
        private StandardButton connectButton;
        private StandardButton generateButton;
        private Dictionary<int, DetailsButton> achievementButtons = new Dictionary<int, DetailsButton>();
        private DetailsButton questButton;
        private Dictionary<string, DetailsButton> genericLocationButtons = new Dictionary<string, DetailsButton>();
        private Panel itemPanel;
        private Scrollbar itemScrollbar;
        private Dictionary<long, Label> itemLabels = new Dictionary<long, Label>();
        private int itemPanelY = 0;
        private Label mistFragmentLabel;

        public event EventHandler<Blish_HUD.Input.MouseEventArgs> ConnectButtonClick
        {
            add { connectButton.Click += value; }
            remove { connectButton.Click -= value; }
        }

        public event EventHandler<Blish_HUD.Input.MouseEventArgs> GenerateButtonClick
        {
            add { generateButton.Click += value; }
            remove { generateButton.Click -= value; }
        }

        internal ArchipelagoWindow (XnaRectangle windowRegion, XnaRectangle contentRegion, ContentsManager contentsManager) : base(AsyncTexture2D.FromAssetId(155985), windowRegion, contentRegion)
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = "Archipelago";
            Emblem = contentsManager.GetTexture("archipelago64.png");
            //Subtitle = "Example Subtitle",
            SavesPosition = true;
            Id = $"Archipelago 22694e1c-69df-43e7-a926-f8b1a5319a47";
            this.contentsManager = contentsManager;


            locationPanel = new Panel()
            {
                Location = new Point(10, 35),
                Size = new Point(350, 520),
                Parent = this,
            };

            locationScrollbar = new Scrollbar(locationPanel)
            {
                Location = new Point(0, 35),
                Size = new Point(10, 570),
                Parent = this,
            };

            itemPanel = new Panel()
            {
                Location = new Point(370, 35),
                Size = new Point(300, 520),
                Parent = this,
            };

            itemScrollbar = new Scrollbar(itemPanel)
            {
                Location = new Point(360, 35),
                Size = new Point(10, 570),
                Parent = this,
            };


            connectButton = new StandardButton()
            {
                Text = "Connect",
                Size = new Point(140, 30),
                Location = new Point(0, 0),
                Parent = this,
            };

            generateButton = new StandardButton()
            {
                Text = "Generate Locations",
                Size = new Point(140, 30),
                Location = new Point(150, 0),
                Parent = this,
            };
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

            locationPanelY = 0;
        }

        internal void ClearItems()
        {

            foreach (var label in itemLabels)
            {
                if (itemPanel != null)
                {
                    itemPanel.RemoveChild(label.Value);
                }
            }
            itemPanelY = 0;
            itemLabels.Clear();
            if (mistFragmentLabel != null)
            {
                RemoveChild(mistFragmentLabel);
            }
        }

        internal void UpdateMistFragments(int mistFragments, long mistFragmentsRequired)
        {

            mistFragmentLabel = new Label()
            {
                Text = "Mist Fragments: " + mistFragments + " / " + mistFragmentsRequired,
                Size = new Point(140, 30),
                Location = new Point(300, 0),
                Parent = this,
            };
        }

        internal void UpdateItemCount(string itemName, long itemId, int itemCount)
        {
            Label label;
            if (itemLabels.ContainsKey(itemId)) {
                label = itemLabels[itemId];
            }
            else
            {
                label = new Label()
                {
                    Size = new Point(300, 30),
                    Location = new Point(0, itemPanelY),
                    Parent = itemPanel,
                };
                itemPanelY += label.Height + 5;
                itemLabels.Add(itemId, label);
            }

            if (itemCount > 1)
            {
                label.Text = itemName + " x" + itemCount;
            }
            else
            {
                label.Text = itemName;
            }
        }

        internal void CreateItemLocationButtons(IEnumerable<ItemLocation> itemLocations)
        {

            Texture2D icon = contentsManager.GetTexture("archipelago64.png");

            foreach (var location in itemLocations) {

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
                button.CurrentFill = 0;
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
                button.CurrentFill = 0;
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

        internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        {
            var button = achievementButtons[achievement.Id];
            logger.Debug("Updating Progress Button: {}/{}", progress.Current, progress.Max);
            button.CurrentFill = progress.Current;
        }
    }
}
