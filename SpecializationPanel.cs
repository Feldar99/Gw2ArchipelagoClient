using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    class SpecializationPanel
    {
        private static readonly Logger logger = Logger.GetLogger<SpecializationPanel>();

        private static AsyncTexture2D lockIcon;

        private AsyncTexture2D icon;
        private AsyncTexture2D background;
        private Specialization specialization;
        private ItemIcon[] traits = new ItemIcon[9];
        private ContentsManager contentsManager;

        public int UnlockedCount { get; set; }
        public Specialization Specialization { get { return specialization; } }
        public Trait this[int i] {
            get { return traits[i].Trait; }
            set
            {
                traits[i] = new ItemIcon(contentsManager, value);
                traits[i].Locked = true;
                traits[i].Size = new Point(32, 32);
            }
        }

        public void Unlock(string itemName)
        {
            foreach (var trait in traits)
            {
                if (itemName.StartsWith(trait.Trait.Name))
                {
                    trait.Locked = false;
                    break;
                }
            }
        }

        public SpecializationPanel(ContentsManager contentsManager, Specialization specialization)
        {
            logger.Debug("Creating Panel for {}", specialization.Name);
            if (lockIcon == null)
            {
                lockIcon = contentsManager.GetTexture("archipelago64.png");
                //lockIcon = GameService.Content.GetTexture("733264");
            }

            this.specialization = specialization;
            {
                var relativePath = specialization.Icon.Url.LocalPath.Split(new char[] { '/' }, 3)[2].Split('.')[0];
                logger.Debug("iconPath: {}, relativePath: {}", specialization.Icon, relativePath);

                icon = GameService.Content.GetRenderServiceTexture(relativePath);
            }

            {
                var relativePath = specialization.Background.Url.LocalPath.Split(new char[] { '/' }, 3)[2].Split('.')[0];
                logger.Debug("iconPath: {}, relativePath: {}", specialization.Icon, relativePath);

                background = GameService.Content.GetRenderServiceTexture(relativePath);
            }
        }

        public void Build(Container container, Point pos, ItemTracker itemTracker)
        {
            var panel = new Panel()
            {
                ShowBorder = false,
                Size = new Point(650, 200),
                Location = pos,
                Parent = container
            };

            var backgroundImage = new Image(background)
            {
                Size = new Point(650, 200),
                Location = new Point(0,0),
                Parent = panel,
            };

            var iconImage = new Image(icon)
            {
                Size = new Point(64, 64),
                Location = new Point(10, 68),
                Parent = panel,
                BasicTooltipText = Specialization.Name,
            };

            for (int i = 0; i < traits.Length; ++i)
            {
                var traitIcon = traits[i];
                var x = i / 3;
                var y = i % 3;
                var traitPos = new Point(100 + x * 50, 10 + y * 50);
                traitIcon.Locked = itemTracker.GetUnlockedItemCount(traitIcon.Trait.Name + " " + specialization.Name + " Trait") == 0;
                traitIcon.Build(panel, traitPos);
            }

        }
    }
}
