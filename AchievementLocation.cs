using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    class AchievementLocation : Location
    {
        public Achievement Achievement;
        private AccountAchievement progress;
        private AchievementCategory category;

        [JsonInclude]
        public bool Done = false;
        [JsonInclude]
        public int? Repeated = null;
        [JsonInclude]
        public HashSet<int> CompleteBits = new HashSet<int>();
        [JsonInclude]
        public int Tier = 0;
        [JsonInclude]
        public int Id;
        [JsonInclude]
        public int Current;

        [JsonInclude]
        public int CateoryId;

        [JsonIgnore]
        public AchievementCategory Category
        {
            get { return category; }
            set {
                category = value;
                CateoryId = category.Id;
            }
        }

        [JsonIgnore]
        public AccountAchievement Progress
        {
            get { return progress; }
            set
            {
                progress = value;
                if (value == null)
                {
                    Current = 0;
                    Done = false;
                    Repeated = null;
                    Tier = 0;
                }
                else
                {
                    Current = progress.Current;
                    Done = progress.Done;
                    Repeated = progress.Repeated;
                    if (progress.Bits != null)
                    {
                        foreach (var bit in progress.Bits)
                        {
                            CompleteBits.Add(bit);
                        }
                    }

                    Tier = CalculateTier();
                }
            }
        }
        
        public int BitCount { get { return CompleteBits.Count; } }

        public int CalculateTier()
        {
            if (progress == null)
            {
                return 0;
            }
            var result = 0;
            foreach (var tier in Achievement.Tiers)
            {
                if (progress.Current >= tier.Count)
                {
                    result++;
                }
            }

            return result;
        }

    }
}
