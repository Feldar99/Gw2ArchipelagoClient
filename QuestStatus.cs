using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    class QuestStatus
    {
        [JsonInclude]
        public Queue<Location> QuestLocations = new Queue<Location>();
        [JsonInclude]
        public HashSet<int> QuestIds = new HashSet<int>();
        [JsonInclude]
        public int CompleteQuestCount = 0;
    }
}
