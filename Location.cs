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
        public string Region;
    }
}
