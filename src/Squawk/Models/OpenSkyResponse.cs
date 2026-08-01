using System.Text.Json;
using System.Text.Json.Serialization;

namespace Squawk.Models;

/// <summary>OpenSky /states/all endpoint yanıtı.</summary>
public class OpenSkyStateResponse
{
    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("states")]
    public JsonElement[][]? States { get; set; }
}

/// <summary>OAuth2 client credentials token yanıtı.</summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";
}
