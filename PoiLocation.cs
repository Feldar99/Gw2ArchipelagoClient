using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    class PoiLocation : Location
    {
        public PointOfInterest poi;

        public PoiLocation(PointOfInterest poi)
        {
            this.poi = poi;
        }

        public Vector3 Position
        {
            get
            {
                return new Vector3(poi.xPos, poi.zPos, poi.yPos); //swap y and z because they are swapped in the mumble api results
            }
        }
    }
}
