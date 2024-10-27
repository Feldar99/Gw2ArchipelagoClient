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

namespace Gw2Archipelago
{
    class ItemIcon
    {
        private static readonly Logger logger = Logger.GetLogger<ItemIcon>();

        private static AsyncTexture2D lockIcon;

        private AsyncTexture2D icon;
        private Skill skill;
        private Trait trait;

        public bool Locked { get; set; }
        public Skill Skill { get { return skill; } }
        public Trait Trait { get { return Trait; } }
        public Point Size { get; set; }
        public string TooltipText { get; private set; }

        public ItemIcon (ContentsManager contentsManager, Skill skill)
        {
            this.skill = skill;
            TooltipText = skill.Name + "\n\n" + skill.Description;
            var relativePath = skill.Icon.Value.Url.LocalPath.Split(new char[] { '/' }, 3)[2].Split('.')[0];
            LoadIcons(contentsManager, relativePath);
        }

        public ItemIcon (ContentsManager contentsManager, Trait trait)
        {
            this.trait = trait;
            TooltipText = trait.Name + "\n\n" + trait.Description;
            var relativePath = trait.Icon.Url.LocalPath.Split(new char[] { '/' }, 3)[2].Split('.')[0];
            LoadIcons(contentsManager, relativePath);

        }

        public ItemIcon (ContentsManager contentsManager, int assetId, string tooltipText)
        {
            LoadIcons(contentsManager, assetId);
            TooltipText = tooltipText;
        }
        private void LoadIcons(ContentsManager contentsManager, int assetId)
        {
            logger.Debug("Constructing Icon for {}", assetId);
            if (lockIcon == null)
            {
                lockIcon = contentsManager.GetTexture("archipelago64.png");
                //lockIcon = GameService.Content.GetTexture("733264");
            }

            icon = GameService.Content.DatAssetCache.GetTextureFromAssetId(assetId);
            logger.Debug("icon {}:", icon);
            Size = new Point(32, 32);

        }

        private void LoadIcons(ContentsManager contentsManager, string iconPath)
        {
            logger.Debug("Constructing Icon for {}", iconPath);
            if (lockIcon == null)
            {
                lockIcon = contentsManager.GetTexture("archipelago64.png");
                //lockIcon = GameService.Content.GetTexture("733264");
            }

            icon = GameService.Content.GetRenderServiceTexture(iconPath);
            Size = new Point(64, 64);

        }

        public static ItemIcon FromWeapon(ContentsManager contentsManager, string weaponSlot, string weaponName)
        {
            logger.Debug("Constructing Icon for {} {}", weaponSlot, weaponName);
            int assetId;
            if (weaponName.Equals("Axe"))
            {
                assetId = 1770024;
            }
            else if (weaponName.Equals("Dagger"))
            {
                assetId = 1770025;
            }
            else if (weaponName.Equals("Mace"))
            {
                assetId = 2192625;
            }
            else if (weaponName.Equals("Pistol"))
            {
                assetId = 2192626;
            }
            else if (weaponName.Equals("Sword"))
            {
                assetId = 1770031;
            }
            else if (weaponName.Equals("Scepter"))
            {
                assetId = 2192627;
            }
            else if (weaponName.Equals("Focus"))
            {
                assetId = 2192623;
            }
            else if (weaponName.Equals("Shield"))
            {
                assetId = 1770028;
            }
            else if (weaponName.Equals("Torch"))
            {
                assetId = 1770032;
            }
            else if (weaponName.Equals("Warhorn"))
            {
                assetId = 2010286;
            }
            else if (weaponName.Equals("Greatsword"))
            {
                assetId = 2010284;
            }
            else if (weaponName.Equals("Hammer"))
            {
                assetId = 1770026;
            }
            else if (weaponName.Equals("Longbow"))
            {
                assetId = 2010285;
            }
            else if (weaponName.Equals("Rifle"))
            {
                assetId = 1770027;
            }
            else if (weaponName.Equals("ShortBow"))
            {
                assetId = 1770029;
            }
            else if (weaponName.Equals("Staff"))
            {
                assetId = 1770030;
            }
            else if (weaponName.Equals("HarpoonGun"))
            {
                assetId = 2192628;
            }
            else if (weaponName.Equals("Spear"))
            {
                assetId = 2192624;
            }
            else if (weaponName.Equals("Trident"))
            {
                assetId = 2192629;
            }
            else
            {
                return null;
            }
            logger.Debug("assetId: {}", assetId);

            var icon = new ItemIcon(contentsManager, assetId, weaponSlot + " " + weaponName);

            return icon;
        }
        

        public void Build(Container container, Point pos)
        {
            var skillImage = new Image(icon)
            {
                Size = Size,
                Location = pos,
                Parent = container,
                BasicTooltipText = TooltipText,
            };

            if (Locked)
            {
                var lockImage = new Image(lockIcon)
                {
                    Size = Size,
                    Location = pos,
                    Parent = container,
                    BasicTooltipText = TooltipText,
                };
                lockImage.Opacity = 0.9f;
            }
        }
    }
}
