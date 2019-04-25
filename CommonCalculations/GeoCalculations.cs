using System;
using System.Collections.Generic;
using System.Text;

namespace CommonCalculations
{
    public static class GeoCalc
    {
        private static int earthRadius = 6371000;

        public static (double latitude, double longitude) LocationFromAzimuth(double latitude, double longitude, double distance, double azimuth)
        {
            if (azimuth < 0)
            {
                azimuth = 360 - azimuth;
            }

            // Adopted from wikipedia.
            double δ = distance / earthRadius;
            double θ = azimuth * Math.PI / 180;
            double φ1 = latitude * Math.PI / 180;
            double λ1 = longitude * Math.PI / 180;

            double sinφ1 = Math.Sin(φ1);
            double cosφ1 = Math.Cos(φ1);
            double sinδ =  Math.Sin(δ);
            double cosδ =  Math.Cos(δ);
            double sinθ =  Math.Sin(θ);
            double cosθ = Math.Cos(θ);
            double sinφ2 = sinφ1 * cosδ + cosφ1 * sinδ * cosθ;

            double φ2 = Math.Asin(sinφ2);
            double y = sinθ * sinδ * cosφ1;
            double x = cosδ - sinφ1 * sinφ2;
            double λ2 = λ1 + Math.Atan2(y, x);

            return (latitude: φ2 * 180 / Math.PI, longitude: ((λ2 * 180 / Math.PI) + 540) % 360 - 180);
        }
        
        public static double GeoDistance(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            var φ1 = latitude1 * Math.PI / 180;
            var φ2 = latitude2 * Math.PI / 180;
            var Δφ = (latitude2 - latitude1) * Math.PI / 180;
            var Δλ = (longitude2 - longitude1) * Math.PI / 180;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }
    }
}
