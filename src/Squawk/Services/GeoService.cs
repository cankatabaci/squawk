namespace Squawk.Services;

/// <summary>Coğrafi hesaplamalar: Haversine mesafesi, pusula yönü, bounding box.</summary>
public class GeoService
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>İki koordinat arasındaki Haversine mesafesini km cinsinden hesaplar.</summary>
    public double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2.0 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Noktadan hedefe pusula yönünü (bearing) hesaplar.
    /// 0° = Kuzey, 90° = Doğu, 180° = Güney, 270° = Batı.
    /// </summary>
    public double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = ToRad(lon2 - lon1);
        var y = Math.Sin(dLon) * Math.Cos(ToRad(lat2));
        var x = Math.Cos(ToRad(lat1)) * Math.Sin(ToRad(lat2))
              - Math.Sin(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Cos(dLon);
        return (ToDeg(Math.Atan2(y, x)) + 360.0) % 360.0;
    }

    /// <summary>Verilen merkez ve yarıçap için API sorgusu bounding box'ını hesaplar.</summary>
    public (double MinLat, double MaxLat, double MinLon, double MaxLon) BoundingBox(
        double lat, double lon, double radiusKm)
    {
        var deltaLat = radiusKm / EarthRadiusKm * (180.0 / Math.PI);
        var deltaLon = radiusKm / (EarthRadiusKm * Math.Cos(ToRad(lat))) * (180.0 / Math.PI);
        return (lat - deltaLat, lat + deltaLat, lon - deltaLon, lon + deltaLon);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;
}
