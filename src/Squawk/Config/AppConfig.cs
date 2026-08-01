namespace Squawk.Config;

public class AppConfig
{
    public OpenSkyConfig OpenSky { get; set; } = new();
    public LocationConfig Location { get; set; } = new();
    public RadarConfig Radar { get; set; } = new();
}

public class OpenSkyConfig
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string TokenUrl { get; set; } =
        "https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token";
}

public class LocationConfig
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Konum ayarlanmış mı? Hem 0,0 ise ayarlanmamış sayılır.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Name) && (Latitude != 0.0 || Longitude != 0.0);
}

public class RadarConfig
{
    public double RadiusKm { get; set; } = 20.0;
    public int RefreshSeconds { get; set; } = 30;
}
