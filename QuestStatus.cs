using Gw2Sharp.WebApi.V2.Models;
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
        public Dictionary<int, Quest> Quests = new Dictionary<int, Quest>();
        [JsonInclude]
        public int CompleteQuestCount = 0;
    }
}
