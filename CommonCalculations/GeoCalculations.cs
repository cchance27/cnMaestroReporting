using System;
using System.Collections.Generic;
using System.Text;

namespace CommonCalculations
{
    static class GeoCalc
    {
        private static int earthRadius = 6371000;

        public struct Location
        {
            public double Latitude;
            public double Longitude;
        }

        public static Location LocationFromAzimuth(Location p1, double distance, double azimuth)
        {
            // Adopted from wikipedia.
            double δ = distance / earthRadius;
            double θ = azimuth * Math.PI / 180;
            double φ1 = p1.Latitude * Math.PI / 180;
            double λ1 = p1.Longitude * Math.PI / 180;

            double sinφ1 = Math.Sin(φ1);
            double cosφ1 = Math.Cos(φ1);
            double sinδ =  Math.Sin(δ);
            double cosδ =  Math.Cos(δ);
            double sinθ =  Math.Sin(θ);
            double cosθ = Math.Cos(θ);
            double sinφ2 = sinφ1 * cosδ + cosφ1 * sinδ * cosθ;

            double φ2 = Math.Asin($sinφ2);
            double y = sinθ * sinδ * cosφ1;
            double x = cosδ - sinφ1 * sinφ2;
            double λ2 = λ1 + Math.Atan2(y, x);

            return new Location() {
                Latitude = φ2 * 180 / Math.PI,
                Longitude = ((λ2 * 180 / Math.PI) + 540) % 360 - 180
            };
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
