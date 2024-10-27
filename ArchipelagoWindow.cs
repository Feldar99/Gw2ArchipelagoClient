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
using SlotData = System.Collections.Generic.Dictionary<string, object>;
using Archipelago.MultiClient.Net;

namespace Gw2Archipelago
{
    class ArchipelagoWindow : TabbedWindow2
    {
        private ContentsManager contentsManager;

        private static readonly Logger logger = Logger.GetLogger<ArchipelagoWindow>();

        private LocationView locationView;
        private ItemView itemView;

        internal ArchipelagoWindow 
        (
            XnaRectangle windowRegion, 
            XnaRectangle contentRegion, 
            LocationChecker locationChecker, 
            SlotData slotData, 
            ArchipelagoSession apSession, 
            Module module
        ) : base(AsyncTexture2D.FromAssetId(155985), windowRegion, contentRegion)
        {
            contentsManager = module.ContentsManager;
            Parent = GameService.Graphics.SpriteScreen;
            Title = "Archipelago";
            Emblem = contentsManager.GetTexture("archipelago64.png");
            //Subtitle = "Example Subtitle",
            SavesPosition = true;
            Id = $"Archipelago 22694e1c-69df-43e7-a926-f8b1a5319a47";

            Tabs.Add(
                new Tab(GameService.Content.GetTexture("155052"), () => {
                    locationView = new LocationView(contentsManager, locationChecker, slotData);
                    itemView = null;
                    locationView.ConnectButtonClick += module.StartReconnectTimer;
                    locationView.GenerateButtonClick += module.GenerateLocations;
                    return locationView;
                }, "Locations"));
            Tabs.Add(new Tab(GameService.Content.GetTexture("155052"), () => {
                locationView = null;
                itemView = new ItemView(contentsManager, slotData, apSession, module.Gw2ApiManager);
                return itemView;
            }, "Item"));

        }

        internal void SetSlotData(SlotData slotData)
        {
            if (locationView != null)
            {
                locationView.Initialize(slotData);
            }
        }

        internal void SetProfession(Profession profession)
        {
            //itemView.Initialize(profession);
        }

        internal void ClearLocations()
        {
            if (locationView != null)
            {
                locationView.ClearLocations();
            }
        }

        internal void ClearItems()
        {
            if (locationView != null)
            {
                locationView.ClearItems();
            }
        }

        internal void UpdateMistFragments(int mistFragments)
        {
            if (locationView != null)
            {
                itemView.UpdateMistFragments(mistFragments);
            }
        }
        internal void UpdateItemCount(string itemName, int itemCount)
        {
            if (locationView != null)
            {
                itemView.UpdateItemCount(itemName, itemCount);
            }
        }

        internal void CreateItemLocationButtons(IEnumerable<ItemLocation> itemLocations)
        {
            if (locationView != null)
            {
                locationView.CreateItemLocationButtons(itemLocations);
            }
        }

        internal void CreatePoiButtons(IEnumerable<PoiLocation> poiLocations)
        {
            if (locationView != null)
            {
                locationView.CreatePoiButtons(poiLocations);
            }
        }

        internal void CreateQuestButton(Storyline storyline, int incompleteQuestCount, int completeQuestCount)
        {
            if (locationView != null)
            {
                locationView.CreateQuestButton(storyline, incompleteQuestCount, completeQuestCount);
            }
        }

        internal void AddAchievementButton(AchievementLocation achievementLocation)
        {
            if (locationView != null)
            {
                locationView.AddAchievementButton(achievementLocation);
            }
        }

        internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        {
            if (locationView != null)
            {
                locationView.UpdateAchievementProgress(achievement, progress);
            }
        }
    }
}
