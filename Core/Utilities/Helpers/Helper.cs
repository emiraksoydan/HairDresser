using Entities.Concrete.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Utilities.Helpers
{

    public static class Geo
    {

        const double EarthRadiusKm = 6371.0;

        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }


        private static double ToRad(double deg) => deg * Math.PI / 180.0;

    }
    public static class OpenControl
    {
        public static bool IsOpenNow(IEnumerable<WorkingHour> hours, DateTime nowLocal)
        {
            var today = nowLocal.DayOfWeek;
            var slot = hours.FirstOrDefault(h => h.DayOfWeek == today && !h.IsClosed);
            if (slot == null) return false;
            var t = nowLocal.TimeOfDay;
            return t >= slot.StartTime && t < slot.EndTime;
        }
    }
    public static class GeoBounds
    {
        public static (double minLat, double maxLat, double minLon, double maxLon)
            BoxKm(double lat, double lon, double radiusKm)
        {
            if (radiusKm <= 0) throw new ArgumentOutOfRangeException(nameof(radiusKm));

            const double kmPerDegLat = 110.574;
            double dLat = radiusKm / kmPerDegLat;

            double kmPerDegLonAtLat = 111.320 * Math.Cos(lat * Math.PI / 180.0);
            if (Math.Abs(kmPerDegLonAtLat) < 1e-9) kmPerDegLonAtLat = 1e-9;

            double dLon = radiusKm / kmPerDegLonAtLat;

            double minLat = Math.Max(-90.0, Math.Min(90.0, lat - dLat));
            double maxLat = Math.Max(-90.0, Math.Min(90.0, lat + dLat));

            double minLon = NormalizeLongitude(lon - dLon);
            double maxLon = NormalizeLongitude(lon + dLon);

            return (minLat, maxLat, minLon, maxLon);
        }

        public static (double minLat, double maxLat, double minLon, double maxLon)
            Box1Km(double lat, double lon) => BoxKm(lat, lon, 1.0);

        private static double NormalizeLongitude(double lon)
        {
            lon = (lon + 180.0) % 360.0;
            if (lon < 0) lon += 360.0;
            return lon - 180.0;
        }
    }


}
