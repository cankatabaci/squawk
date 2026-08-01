namespace Squawk.Models;

/// <summary>
/// Bir uçağın anlık durum vektörü.
/// OpenSky API'dan parse edilen veriler + hesaplanmış alanlar.
/// </summary>
public class Aircraft
{
    // — API Alanları —
    public string Icao24 { get; set; } = "";
    public string? Callsign { get; set; }
    public string? OriginCountry { get; set; }
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }
    public double? BaroAltitude { get; set; }  // metre
    public double? GeoAltitude { get; set; }   // metre
    public bool OnGround { get; set; }
    public double? Velocity { get; set; }      // m/s
    public double? TrueTrack { get; set; }     // derece, 0=Kuzey, saat yönünde
    public double? VerticalRate { get; set; }  // m/s, + = tırmanış
    public string? Squawk { get; set; }

    // — Hesaplanmış Alanlar —
    public double DistanceKm { get; set; }
    public double BearingDeg { get; set; }     // 0=Kuzey, saat yönünde

    // — Yardımcı Özellikler —
    public string DisplayCallsign =>
        !string.IsNullOrWhiteSpace(Callsign) ? Callsign.Trim() : Icao24.ToUpper();

    /// <summary>Barometrik veya geometrik irtifa. Yoksa "N/A".</summary>
    public string AltitudeDisplay =>
        BaroAltitude.HasValue ? $"{BaroAltitude.Value:F0}m" : "N/A";

    /// <summary>Flight Level — irtifayı 100ft birimiyle gösterir (FL320 gibi).</summary>
    public string FlightLevelDisplay
    {
        get
        {
            double? alt = GeoAltitude ?? BaroAltitude;
            if (!alt.HasValue) return "-----";
            int fl = (int)(alt.Value * 3.28084 / 100.0);
            return $"FL{fl:D3}";
        }
    }

    /// <summary>Hız km/h cinsinden. Yoksa boş string.</summary>
    public string SpeedDisplay =>
        Velocity.HasValue ? $"{Velocity.Value * 3.6:F0}kt" : "";

    /// <summary>
    /// TrueTrack'ten 8-yönlü pusula kısaltması: N, NE, E, SE, S, SW, W, NW.
    /// </summary>
    public string HeadingDisplay
    {
        get
        {
            if (!TrueTrack.HasValue || double.IsNaN(TrueTrack.Value)) return " -- ";
            string[] dirs = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
            int idx = (int)Math.Round(TrueTrack.Value / 45.0) % 8;
            return dirs[idx < 0 ? 0 : idx];
        }
    }

    /// <summary>Transponder (squawk) kodu. Özel kodlar için uyarı etiketi döner.</summary>
    public string SquawkDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Squawk)) return "----";
            return Squawk.Trim() switch
            {
                "7700" => "7700⚠", // Genel acil durum
                "7600" => "7600📻", // Radyo arızası
                "7500" => "7500🚨", // Kaçırılma
                var s  => s,
            };
        }
    }

    public string VerticalDisplay =>
        VerticalRate.HasValue && Math.Abs(VerticalRate.Value) > 0.5
            ? (VerticalRate.Value > 0 ? "↑" : "↓")
            : "→";
}
