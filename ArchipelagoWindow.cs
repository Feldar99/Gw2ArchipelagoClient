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
                    locationView = new LocationView(module);
                    itemView = null;
                    locationView.ConnectButtonClick += module.StartReconnectTimer;
                    locationView.GenerateButtonClick += module.GenerateLocations;
                    return locationView;
                }, "Locations"));
            Tabs.Add(new Tab(GameService.Content.GetTexture("155052"), () => {
                locationView = null;
                itemView = new ItemView(module);
                return itemView;
            }, "Item"));

        }

        internal void SetProfession(Profession profession)
        {
            //itemView.Initialize(profession);
        }

        internal void Refresh()
        {
            if (locationView != null)
            {
                locationView.Refresh();
            }
        }

        //internal void UpdateAchievementProgress(Achievement achievement, AccountAchievement progress)
        //{
        //    if (locationView != null)
        //    {
        //        locationView.UpdateAchievementProgress(achievement, progress);
        //    }
        //}
    }
}
