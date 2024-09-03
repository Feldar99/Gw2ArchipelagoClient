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
    class ArchipelagoWindow : TabbedWindow2
    {
        private ContentsManager contentsManager;

        public LocationView locationView;

        private static readonly Logger logger = Logger.GetLogger<Module>();

        public event EventHandler<Blish_HUD.Input.MouseEventArgs> ConnectButtonClick
        {
            add { locationView.ConnectButtonClick += value; }
            remove { locationView.ConnectButtonClick -= value; }
        }

        public event EventHandler<Blish_HUD.Input.MouseEventArgs> GenerateButtonClick
        {
            add { locationView.GenerateButtonClick += value; }
            remove { locationView.GenerateButtonClick -= value; }
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
            locationView = new LocationView(contentsManager, this);
            this.Tabs.Add(new Tab(GameService.Content.GetTexture("155052"), () => locationView, "Locations"));

            locationView.Initialize();


        }

        internal void ClearLocations()
        {
            locationView.ClearLocations();
        }

        internal void ClearItems()
        {
            locationView.ClearItems();
        }

        internal void UpdateMistFragments(int mistFragments, long mistFragmentsRequired)
        {

            locationView.UpdateMistFragments(mistFragments, mistFragmentsRequired);
        }
        internal void UpdateItemCount(string itemName, long itemId, int itemCount)
        {
            locationView.UpdateItemCount(itemName, itemId, itemCount);
        }

        internal void CreateItemLocationButtons(IEnumerable<ItemLocation> itemLocations)
        {
            locationView.CreateItemLocationButtons(itemLocations);
        }

        internal void CreatePoiButtons(IEnumerable<PoiLocation> poiLocations)
        {
            locationView.CreatePoiButtons(poiLocations);
        }

        internal void CreateQuestButton(Storyline storyline, int incompleteQuestCount, int completeQuestCount)
        {
            locationView.CreateQuestButton(storyline, incompleteQuestCount, completeQuestCount);
        }

        internal void AddAchievementButton(AchievementLocation achievementLocation)
        {
            locationView.AddAchievementButton(achievementLocation);
        }

        internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        {
            locationView.UpdateAchievementProgress(achievement, progress);
        }
    }
}
