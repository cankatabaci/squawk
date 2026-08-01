using System.Net.Http.Headers;
using System.Text.Json;
using Squawk.Config;
using Squawk.Models;

namespace Squawk.Services;

/// <summary>
/// OpenSky Network REST API istemcisi.
/// OAuth2 client credentials flow ile token yönetimi dahil.
/// </summary>
public class OpenSkyService
{
    private readonly HttpClient _http;
    private readonly AppConfig _config;
    private readonly GeoService _geo;

    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenSkyService(HttpClient http, AppConfig config, GeoService geo)
    {
        _http = http;
        _config = config;
        _geo = geo;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Kullanıcının belirlediği yarıçap içindeki uçakları döndürür.
    /// Önce bounding box ile API sorgusu yapılır, ardından Haversine ile kesin filtreleme.
    /// </summary>
    public async Task<List<Aircraft>> GetNearbyAircraftAsync(CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);

        var loc = _config.Location;
        var radar = _config.Radar;

        var (minLat, maxLat, minLon, maxLon) = _geo.BoundingBox(
            loc.Latitude, loc.Longitude, radar.RadiusKm);

        var url = $"https://opensky-network.org/api/states/all" +
                  $"?lamin={minLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&lomin={minLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&lamax={maxLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&lomax={maxLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<OpenSkyStateResponse>(json, _jsonOpts);

        if (data?.States == null) return [];

        var result = new List<Aircraft>();

        foreach (var state in data.States)
        {
            var ac = ParseState(state);
            if (ac?.Latitude == null || ac.Longitude == null) continue;
            if (ac.OnGround) continue; // Yerde olan uçakları atla

            ac.DistanceKm = _geo.HaversineDistance(
                loc.Latitude, loc.Longitude,
                ac.Latitude.Value, ac.Longitude.Value);

            if (ac.DistanceKm > radar.RadiusKm) continue;

            ac.BearingDeg = _geo.Bearing(
                loc.Latitude, loc.Longitude,
                ac.Latitude.Value, ac.Longitude.Value);

            result.Add(ac);
        }

        return result.OrderBy(a => a.DistanceKm).ToList();
    }

    // — OAuth2 Token Yönetimi —

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.OpenSky.ClientId) ||
            string.IsNullOrEmpty(_config.OpenSky.ClientSecret))
            return; // Anonim erişim

        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return; // Token hâlâ geçerli

        await RefreshTokenAsync(ct);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        try
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config.OpenSky.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.OpenSky.ClientSecret),
            ]);

            using var response = await _http.PostAsync(_config.OpenSky.TokenUrl, content, ct);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(ct);
            var token = JsonSerializer.Deserialize<TokenResponse>(json, _jsonOpts);
            if (token == null || string.IsNullOrEmpty(token.AccessToken)) return;

            _accessToken = token.AccessToken;
            // Token süresinden 60s önce yenile
            _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
        }
        catch (Exception)
        {
            // Token alınamazsa anonim devam et
            _accessToken = null;
        }
    }

    // — State Vector Parser —

    private static Aircraft? ParseState(JsonElement[] s)
    {
        if (s.Length < 9) return null;

        static string? Str(JsonElement[] arr, int i) =>
            arr.Length > i && arr[i].ValueKind != JsonValueKind.Null
                ? arr[i].GetString()
                : null;

        static double? Dbl(JsonElement[] arr, int i) =>
            arr.Length > i && arr[i].ValueKind == JsonValueKind.Number
                ? arr[i].GetDouble()
                : null;

        static bool Bool(JsonElement[] arr, int i) =>
            arr.Length > i && arr[i].ValueKind == JsonValueKind.True;

        return new Aircraft
        {
            Icao24 = Str(s, 0) ?? "",
            Callsign = Str(s, 1)?.Trim(),
            OriginCountry = Str(s, 2),
            Longitude = Dbl(s, 5),
            Latitude = Dbl(s, 6),
            BaroAltitude = Dbl(s, 7),
            OnGround = Bool(s, 8),
            Velocity = Dbl(s, 9),
            TrueTrack = Dbl(s, 10),
            VerticalRate = Dbl(s, 11),
            GeoAltitude = Dbl(s, 13),
            Squawk = Str(s, 14),
        };
    }
}
