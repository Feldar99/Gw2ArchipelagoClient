using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Gw2Archipelago
{
    class Location
    {

        [JsonInclude]
        public string LocationName;
        [JsonInclude]
        public bool LocationComplete = false;
        [JsonInclude]
        public HashSet<string> Regions = new HashSet<string>();

        public bool HasRegion(string region)
        {
            return Regions.Contains(region);
        }
    }
}
