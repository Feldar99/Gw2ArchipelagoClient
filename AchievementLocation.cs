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

        [JsonInclude]
        public int Target;

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

        public static int GetTarget(Achievement achievement, AccountAchievement progress, int? maxAmount)
        {
            int target = 1;
            int current = progress?.Current ?? 0;
            if (achievement.Bits != null && progress != null)
            {
                target = current + 1;
            }
            else if (achievement.Tiers != null)
            {
                foreach (var tier in achievement.Tiers)
                {
                    if (tier.Count <= current) continue;
                    target = tier.Count;
                    break;
                }
            }

            if (maxAmount != null && progress != null)
            {
                target = Math.Min(target, maxAmount.Value + current);
            }

            return target;
        }
    }
}
