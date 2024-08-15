using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    class ItemLocation : Location
    {
        [JsonInclude]
        public Dictionary<int, int> targetQuantities = new Dictionary<int, int>(); // itemId -> target quantity

    }
}
