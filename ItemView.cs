using Archipelago.MultiClient.Net;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules.Managers;
using Gw2Sharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using SlotData = System.Collections.Generic.Dictionary<string, object>;

namespace Gw2Archipelago
{
    public class ItemView : View
    {
        private struct SpecializationData
        {
            public int Id;
            public string[] Traits;
            public string Storyline;
        }

        private static readonly Logger logger = Logger.GetLogger<ItemView>();

        private Container container;
        private Panel contentPanel;
        private Scrollbar scrollbar;

        private Dictionary<string, ItemIcon> skillIcons = new Dictionary<string, ItemIcon>();
        private Dictionary<string, SpecializationPanel> specializationPanels = new Dictionary<string, SpecializationPanel>();
        private Dictionary<string, List<ItemIcon>> weaponIcons = new Dictionary<string, List<ItemIcon>>();
        private Dictionary<string, ItemIcon> equipSlotIcons = new Dictionary<string, ItemIcon>();
        private Label mistFragmentsLabel;

        private Module module;

        public ItemView(Module module)
        {
            this.module = module;
            module.ItemTracker.ItemUnlocked += OnItemUnlocked;
        }


        private void addSkillIdsForEntry(string entry, Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>> skillData, Dictionary<string, int> skillIds)
        {
            foreach (var skillsBySpec in skillData[entry])
            {
                //logger.Debug("{}", skillsBySpec.Key);
                foreach (var skillsByType in skillsBySpec.Value)
                {
                    //logger.Debug("  {}", skillsByType.Key);
                    foreach (var iconsByName in skillsByType.Value)
                    {
                        //logger.Debug("    {}: {}", iconsByName.Key, iconsByName.Value);
                        skillIds[iconsByName.Key] = iconsByName.Value;
                    }
                }
            }

        }

        protected override async Task<bool> Load(IProgress<string> progress)
        {
            Profession characterProfession = (Profession)Enum.ToObject(typeof(Profession), module.SlotData["CharacterProfession"]);
            Race       characterRace       = (Race)Enum.ToObject(typeof(Race), module.SlotData["CharacterRace"]);

            var deserializer = new DeserializerBuilder()
            .WithNamingConvention(LowerCaseNamingConvention.Instance)
            .WithTypeConverter(new YamlProfessionConverter())
            .Build();

            progress.Report("Reading Skill Ids");
            logger.Debug("Reading Skill Ids");
            var skillIds = new Dictionary<string, int>();
            {
                var reader = new StreamReader(module.ContentsManager.GetFileStream("Skills.yaml"));
                var skillData = deserializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>>(reader);

                addSkillIdsForEntry(characterProfession.GetName(), skillData, skillIds);
                addSkillIdsForEntry(characterRace.GetName(), skillData, skillIds);

            }

            progress.Report("Reading Specializations from File");
            logger.Debug("Reading Specializations from File");
            Dictionary<string, SpecializationData> specData;
            var specialiationIds = new List<int>();
            {
                var reader = new StreamReader(module.ContentsManager.GetFileStream("Traits.yaml"));
                var data = deserializer.Deserialize<Dictionary<Profession, Dictionary<string, SpecializationData>>>(reader);

                specData = data[characterProfession];
                foreach (var spec in specData)
                {
                    specialiationIds.Add(spec.Value.Id);
                }
            }

            var skillTask = module.Gw2ApiManager.Gw2ApiClient.V2.Skills.ManyAsync(skillIds.Values);
            var specializationTask = module.Gw2ApiManager.Gw2ApiClient.V2.Specializations.ManyAsync(specialiationIds);

            progress.Report("Loading Specializaiton Data");
            logger.Debug("Loading Specializaiton Data");
            var specializations = await specializationTask;

            var traitIds = new List<int>();
            var specNames = new Dictionary<int, string>();
            specializationPanels.Clear();
            foreach (var specialization in specializations)
            {
                var panel = new SpecializationPanel(module.ContentsManager, specialization);
                specNames[specialization.Id] = specialization.Name;
                specializationPanels[specialization.Name] = panel;

                traitIds.AddRange(specialization.MajorTraits);
            }
            var traitTask = module.Gw2ApiManager.Gw2ApiClient.V2.Traits.ManyAsync(traitIds);


            progress.Report("Reading Weapons from File");
            logger.Debug("Reading Weapons from File");
            weaponIcons.Clear();
            weaponIcons.Add("Mainhand", new List<ItemIcon>());
            weaponIcons.Add("Offhand", new List<ItemIcon>());
            weaponIcons.Add("TwoHanded", new List<ItemIcon>());
            weaponIcons.Add("Aquatic", new List<ItemIcon>());
            {
                var reader = new StreamReader(module.ContentsManager.GetFileStream("Weapons.yaml"));
                var data = deserializer.Deserialize<Dictionary<string, Dictionary<string, List<object>>>>(reader);

                foreach (var weapon in data)
                {
                    //logger.Debug(weapon.Key);
                    foreach (var weaponSlot in weapon.Value)
                    {
                        //logger.Debug(weaponSlot.Key);
                        foreach (var professionData in weaponSlot.Value)
                        {
                            string professionName = "";
                            if (professionData is string str)
                            {
                                professionName = str;
                            }
                            else if (professionData is Dictionary<object, object> dict)
                            {
                                foreach (var entry in dict)
                                {
                                    professionName = (string)entry.Key;
                                    break;
                                }
                            }
                            else
                            {
                                //logger.Debug("{}", professionData.GetType());
                                throw new InvalidDataException("Weapons.yaml profession entries must either be strings, or dictionaries of professions to requirements, represented as strings");
                            }
                            //logger.Debug(professionName);
                            var profession = ProfessionExtensions.FromName(professionName);
                            //logger.Debug(profession.GetName());

                            if (profession == characterProfession)
                            {
                                var icon = ItemIcon.FromWeapon(module.ContentsManager, weaponSlot.Key, weapon.Key);
                                var weaponName = weaponSlot.Key + " " + weapon.Key;
                                if (module.ItemTracker.GetUnlockedItemCount(weaponName) == 0)
                                {
                                    icon.Locked = true;
                                }
                                weaponIcons[weaponSlot.Key].Add(icon);
                            }
                        }
                    }
                }
            }

            logger.Debug("Creating EquipSlot Icons");
            equipSlotIcons.Clear();
            {
                equipSlotIcons.Add("Head",          new ItemIcon(module.ContentsManager, 699210,  "Head"));
                equipSlotIcons.Add("Shoulders",     new ItemIcon(module.ContentsManager, 699208,  "Shoulders"));
                equipSlotIcons.Add("Chest",         new ItemIcon(module.ContentsManager, 699212,  "Chest"));
                equipSlotIcons.Add("Gloves",        new ItemIcon(module.ContentsManager, 699211,  "Gloves"));
                equipSlotIcons.Add("Legs",          new ItemIcon(module.ContentsManager, 699209,  "Legs"));
                equipSlotIcons.Add("Boots",         new ItemIcon(module.ContentsManager, 699213,  "Boots"));
                equipSlotIcons.Add("Back",          new ItemIcon(module.ContentsManager, 61004,   "Back"));
                equipSlotIcons.Add("Accessory 1",   new ItemIcon(module.ContentsManager, 1203063, "Accessory 1"));
                equipSlotIcons.Add("Accessory 2",   new ItemIcon(module.ContentsManager, 711884,  "Accessory 2"));
                equipSlotIcons.Add("Relic",         new ItemIcon(module.ContentsManager, 3255567,  "Relic"));
                equipSlotIcons.Add("Amulet",        new ItemIcon(module.ContentsManager, 455601,  "Amulet"));
                equipSlotIcons.Add("Ring 1",        new ItemIcon(module.ContentsManager, 455584,  "Ring 1"));
                equipSlotIcons.Add("Ring 2",        new ItemIcon(module.ContentsManager, 455590,  "Ring 2"));
                equipSlotIcons.Add("Aqua Breather", new ItemIcon(module.ContentsManager, 61297,   "Aqua Breather"));
            }

            foreach (var equipSlot in equipSlotIcons)
            {
                equipSlot.Value.Locked = module.ItemTracker.GetUnlockedItemCount(equipSlot.Value.TooltipText) == 0;
                equipSlot.Value.Size = new Point(48, 48);
            }

            progress.Report("Loading Skill Icons");
            logger.Debug("Loading Skill Icons");
            var skills = await skillTask;
            skillIcons.Clear();
            foreach (var skill in skills)
            {
                logger.Debug("Making icon for {}", skill.Name);
                var icon = new ItemIcon(module.ContentsManager, skill);
                var apName = getApSkillName(skill.Name, skill.Type);
                icon.Locked = module.ItemTracker.GetUnlockedItemCount(apName) == 0;
                logger.Debug("skill: {}, locked: {}", apName, icon.Locked);
                skillIcons.Add(apName, icon);
            }

            progress.Report("Loading Trait Icons");
            logger.Debug("Loading Trait Icons");
            var traits = await traitTask;
            foreach (var trait in traits)
            {
                var specName = specNames[trait.Specialization];
                var panel = specializationPanels[specName];
                var index = trait.Order + (trait.Tier - 1) * 3;
                logger.Debug("trait {}: {}", index, trait.Name);
                var apTraitName = getApTraitName(trait.Name, specName);
                panel[index] = trait;
                if (module.ItemTracker.GetUnlockedItemCount(apTraitName) > 0)
                {
                    panel.Unlock(apTraitName);
                }
            }

            return true;
        }

        private string getApSkillName(string skillName, string skillType)
        {
            if (skillType.Equals("Heal"))
            {
                skillType = "Healing";
            }
            else if (skillType.Equals("Profession"))
            {
                skillType = "Legend";
            }
            return skillName + " " + skillType + " Skill";
        }

        private string getApTraitName(string traitName, string specName)
        {
            return traitName + " " + specName + " Trait";
        }

        protected override void Build(Container container)
        {

            mistFragmentsLabel = new Label()
            {
                Text = "Mist Fragments: " + module.ItemTracker.GetUnlockedItemCount("Mist Fragment") + " / " + module.SlotData["MistFragmentsRequired"],
                Location = new Point(50, 0),
                Size = new Point(200, 30),
                Parent = container,
            };

            var xPos = 25;
            var yPos = 30;

            contentPanel = new Panel()
            {
                Location = new Point(40, 10),
                Size = new Point(600, 520),
                Parent = container,
            };

            scrollbar = new Scrollbar(contentPanel)
            {
                Location = new Point(30, 10),
                Size = new Point(10, 570),
                Parent = container,
            };

            foreach (var icon in equipSlotIcons)
            {
                icon.Value.Locked = module.ItemTracker.GetUnlockedItemCount(icon.Key) == 0;
                icon.Value.Build(contentPanel, new Point(xPos, yPos));
                xPos += icon.Value.Size.X + 5;
                if (xPos > 300)
                {
                    xPos = 25;
                    yPos += icon.Value.Size.Y + 5;
                }
            }

            xPos = 25;
            yPos += 60;


            foreach (var icon in skillIcons)
            {
                icon.Value.Locked = module.ItemTracker.GetUnlockedItemCount(icon.Key) == 0;
                icon.Value.Build(contentPanel, new Point(xPos, yPos));
                xPos += icon.Value.Size.X + 5;
                if (xPos > 500)
                {
                    xPos = 25;
                    yPos += icon.Value.Size.Y + 5;
                }
            }

            if (xPos > 25)
            {
                xPos = 25;
                yPos += 75;
            }

            foreach (var weaponSlot in weaponIcons)
            {
                var label = new Label()
                {
                    Text = weaponSlot.Key,
                    Size = new Point(100, 40),
                    Location = new Point(xPos, yPos),
                    Parent = contentPanel,
                };
                xPos += label.Size.X;
                foreach (var weaponIcon in weaponSlot.Value)
                {
                    weaponIcon.Locked = module.ItemTracker.GetUnlockedItemCount(weaponIcon.TooltipText) == 0;
                    weaponIcon.Build(contentPanel, new Point(xPos, yPos));
                    xPos += weaponIcon.Size.X + 5;
                }

                xPos = 25;
                yPos += 40;
            }

            foreach (var panel in specializationPanels)
            {
                panel.Value.Build(contentPanel, new Point(xPos, yPos), module.ItemTracker);
                yPos += 200;
            }
        }

        protected override void Unload()
        {
            skillIcons.Clear();
            specializationPanels.Clear();
            weaponIcons.Clear();
            equipSlotIcons.Clear();
            mistFragmentsLabel = null;

        }

        internal void OnItemUnlocked(string itemName, int itemCount)
        {
            if (mistFragmentsLabel == null)
            {
                return;
            }
            if (itemName.Equals("Mist Fragment"))
            {
                mistFragmentsLabel.Text = "Mist Fragments: " + itemCount + " / " + module.SlotData["MistFragmentsRequired"];
            }
            else if (itemName.EndsWith("Skill"))
            {
                skillIcons[itemName].Locked = false;
            }
            else if (equipSlotIcons.ContainsKey(itemName))
            {
                equipSlotIcons[itemName].Locked = false;
            }
            else if (itemName.EndsWith("Trait"))
            {
                var nameSubstr = itemName.Substring(0, itemName.Length - " Trait".Length);
                foreach (var spec in specializationPanels)
                {
                    if (nameSubstr.EndsWith(spec.Key)) {
                        spec.Value.Unlock(itemName);
                    }
                }
            }
            else if (!module.MapAccessTracker.IsMapName(itemName))
            {
                var weaponSlot = itemName.Split(' ')[0];
                foreach (var icon in weaponIcons[weaponSlot])
                {
                    if (icon.TooltipText.Equals(itemName))
                    {
                        icon.Locked = false;
                        break;
                    }
                }
            }
        }
    }

    
}
