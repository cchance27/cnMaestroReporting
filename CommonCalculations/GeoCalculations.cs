using System;
using System.Collections.Generic;
using System.Text;

namespace CommonCalculations
{
    static class GeoCalc
    {
        public struct Location
        {
            public float Latitude;
            public float Longitude;
        }

        public static double GeoDistance(Location p1, Location p2)
        {
            // From the OdataSamples: https://csharp.hotexamples.com/examples/Microsoft.Spatial/GeographyPoint/-/php-geographypoint-class-examples.html
            var lat1 = Math.PI * p1.Latitude / 180;
            var lat2 = Math.PI * p2.Latitude / 180;
            var lon1 = Math.PI * p1.Longitude / 180;
            var lon2 = Math.PI * p2.Longitude / 180;
            var item1 = Math.Sin((lat1 - lat2) / 2) * Math.Sin((lat1 - lat2) / 2);
            var item2 = Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin((lon1 - lon2) / 2) * Math.Sin((lon1 - lon2) / 2);
            return Math.Asin(Math.Sqrt(item1 + item2));
        }
    }
}
