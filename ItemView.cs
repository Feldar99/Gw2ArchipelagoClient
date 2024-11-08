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

        private ContentsManager contentsManager;
        private Container container;
        private SlotData slotData;
        private ArchipelagoSession apSession;
        private Gw2ApiManager gw2ApiManager;
        private Panel contentPanel;
        private Scrollbar scrollbar;

        private Dictionary<string, ItemIcon> skillIcons;
        private Dictionary<string, SpecializationPanel> specializationPanels;
        private Dictionary<string, List<ItemIcon>> weaponIcons;
        private Dictionary<string, ItemIcon> equipSlotIcons;
        private Label mistFragmentsLabel;

        private int mistFragmentCount = 0;
        private Dictionary<string, int> receivedItemCounts;

        public ItemView(ContentsManager contentsManager, SlotData slotData, ArchipelagoSession apSession, Gw2ApiManager gw2ApiManager)
        {
            this.contentsManager = contentsManager;
            this.slotData = slotData;
            this.apSession = apSession;
            this.gw2ApiManager = gw2ApiManager;

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
            Profession characterProfession = (Profession)Enum.ToObject(typeof(Profession), slotData["CharacterProfession"]);
            Race       characterRace       = (Race)Enum.ToObject(typeof(Race), slotData["CharacterRace"]);

            var deserializer = new DeserializerBuilder()
            .WithNamingConvention(LowerCaseNamingConvention.Instance)
            .Build();

            progress.Report("Reading Skill Ids");
            logger.Debug("Reading Skill Ids");
            var skillIds = new Dictionary<string, int>();
            {
                var reader = new StreamReader(contentsManager.GetFileStream("Skills.yaml"));
                var skillData = deserializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, int>>>>>(reader);

                addSkillIdsForEntry(characterProfession.GetName(), skillData, skillIds);
                addSkillIdsForEntry(characterRace.GetName(), skillData, skillIds);

            }

            progress.Report("Reading Specializations from File");
            logger.Debug("Reading Specializations from File");
            Dictionary<string, SpecializationData> specData;
            var specialiationIds = new List<int>();
            {
                var reader = new StreamReader(contentsManager.GetFileStream("Traits.yaml"));
                var data = deserializer.Deserialize<Dictionary<string, Dictionary<string, SpecializationData>>>(reader);

                specData = data[characterProfession.GetName()];
                foreach (var spec in specData)
                {
                    specialiationIds.Add(spec.Value.Id);
                }
            }

            var skillTask = gw2ApiManager.Gw2ApiClient.V2.Skills.ManyAsync(skillIds.Values);
            var specializationTask = gw2ApiManager.Gw2ApiClient.V2.Specializations.ManyAsync(specialiationIds);

            progress.Report("Loading Specializaiton Data");
            logger.Debug("Loading Specializaiton Data");
            var specializations = await specializationTask;
            receivedItemCounts = new Dictionary<string, int>();
            foreach (var receivedItem in apSession.Items.AllItemsReceived)
            {
                var itemName = receivedItem.ItemName;
                if (receivedItemCounts.ContainsKey(itemName))
                {
                    receivedItemCounts[itemName] += 1;
                }
                else
                {
                    receivedItemCounts[itemName] = 1;
                }
            }
            var traitIds = new List<int>();
            var specNames = new Dictionary<int, string>();
            specializationPanels = new Dictionary<string, SpecializationPanel>();
            foreach (var specialization in specializations)
            {
                var panel = new SpecializationPanel(contentsManager, specialization);
                logger.Debug("Progressive " + specialization.Name + " Trait");
                int unlockedCount;
                receivedItemCounts.TryGetValue("Progressive " + specialization.Name + " Trait", out unlockedCount);
                panel.UnlockedCount = unlockedCount;
                specNames[specialization.Id] = specialization.Name;
                specializationPanels[specialization.Name] = panel;

                traitIds.AddRange(specialization.MajorTraits);
            }
            var traitTask = gw2ApiManager.Gw2ApiClient.V2.Traits.ManyAsync(traitIds);


            progress.Report("Reading Weapons from File");
            logger.Debug("Reading Weapons from File");
            weaponIcons = new Dictionary<string, List<ItemIcon>>();
            weaponIcons.Add("Mainhand", new List<ItemIcon>());
            weaponIcons.Add("Offhand", new List<ItemIcon>());
            weaponIcons.Add("TwoHanded", new List<ItemIcon>());
            weaponIcons.Add("Aquatic", new List<ItemIcon>());
            {
                var reader = new StreamReader(contentsManager.GetFileStream("Weapons.yaml"));
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
                                var icon = ItemIcon.FromWeapon(contentsManager, weaponSlot.Key, weapon.Key);
                                var weaponName = weaponSlot.Key + " " + weapon.Key;
                                if (!receivedItemCounts.ContainsKey(weaponName))
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
            equipSlotIcons = new Dictionary<string, ItemIcon>();
            {
                equipSlotIcons.Add("Head",          new ItemIcon(contentsManager, 699210,  "Head"));
                equipSlotIcons.Add("Shoulders",     new ItemIcon(contentsManager, 699208,  "Shoulders"));
                equipSlotIcons.Add("Chest",         new ItemIcon(contentsManager, 699212,  "Chest"));
                equipSlotIcons.Add("Gloves",        new ItemIcon(contentsManager, 699211,  "Gloves"));
                equipSlotIcons.Add("Legs",          new ItemIcon(contentsManager, 699209,  "Legs"));
                equipSlotIcons.Add("Boots",         new ItemIcon(contentsManager, 699213,  "Boots"));
                equipSlotIcons.Add("Back",          new ItemIcon(contentsManager, 61004,   "Back"));
                equipSlotIcons.Add("Amulet",        new ItemIcon(contentsManager, 455601,  "Amulet"));
                equipSlotIcons.Add("Accessory 1",   new ItemIcon(contentsManager, 1203063, "Accessory 1"));
                equipSlotIcons.Add("Accessory 2",   new ItemIcon(contentsManager, 711884,  "Accessory 2"));
                equipSlotIcons.Add("Ring 1",        new ItemIcon(contentsManager, 455584,  "Ring 1"));
                equipSlotIcons.Add("Ring 2",        new ItemIcon(contentsManager, 455590,  "Ring 2"));
                equipSlotIcons.Add("Relic",         new ItemIcon(contentsManager, 961418,  "Relic"));
            }

            foreach (var equipSlot in equipSlotIcons)
            {
                equipSlot.Value.Locked = !receivedItemCounts.ContainsKey(equipSlot.Value.TooltipText);
                equipSlot.Value.Size = new Point(48, 48);
            }

            receivedItemCounts.TryGetValue("Mist Fragment", out mistFragmentCount);

            progress.Report("Loading Skill Icons");
            logger.Debug("Loading Skill Icons");
            var skills = await skillTask;
            skillIcons = new Dictionary<string, ItemIcon>();
            foreach (var skill in skills)
            {
                logger.Debug("Making icon for {}", skill.Name);
                var icon = new ItemIcon(contentsManager, skill);
                var apName = getApSkillName(skill.Name, skill.Type);
                icon.Locked = !receivedItemCounts.ContainsKey(apName);
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
                if (receivedItemCounts.ContainsKey(apTraitName))
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
                Text = "Mist Fragments: " + mistFragmentCount + " / " + slotData["MistFragmentsRequired"],
                Size = new Point(140, 30),
                Location = new Point(300, 0),
                Parent = container,
            };

            var xPos = 25;
            var yPos = 30;

            contentPanel = new Panel()
            {
                Location = new Point(20, 10),
                Size = new Point(600, 520),
                Parent = container,
            };

            scrollbar = new Scrollbar(contentPanel)
            {
                Location = new Point(10, 10),
                Size = new Point(10, 570),
                Parent = container,
            };

            foreach (var icon in equipSlotIcons)
            {
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
                    weaponIcon.Build(contentPanel, new Point(xPos, yPos));
                    xPos += weaponIcon.Size.X + 5;
                }

                xPos = 25;
                yPos += 40;
            }

            foreach (var panel in specializationPanels)
            {
                panel.Value.Build(contentPanel, new Point(xPos, yPos));
                yPos += 200;
            }
        }

        protected override void Unload()
        {
            skillIcons = null;
            specializationPanels = null;
            weaponIcons = null;
            equipSlotIcons = null;

        }

        internal void UpdateMistFragments(int mistFragments)
        {
            mistFragmentCount = mistFragments;
            mistFragmentsLabel.Text = "Mist Fragments: " + mistFragments + " / " + slotData["MistFragmentsRequired"];
        }
        internal void UpdateItemCount(string itemName, int itemCount)
        {
            if (receivedItemCounts.ContainsKey(itemName))
            {
                if (receivedItemCounts[itemName] == itemCount)
                {
                    return;
                }
                else
                {
                    receivedItemCounts[itemName] = itemCount;
                }
            }

            if (itemName.EndsWith("Skill"))
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
            else
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
