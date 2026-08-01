using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Squawk.Config;
using Squawk.Radar;
using Squawk.Services;

// ─── Kullanıcı Config Dosyası Yolu ───────────────────────────────────────────
// %APPDATA%\squawk\config.json  (Windows)
// ~/.config/squawk/config.json  (macOS/Linux)
static string GetUserConfigPath()
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var dir = Path.Combine(appData, "squawk");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "config.json");
}

// ─── CLI Tanımları ────────────────────────────────────────────────────────────
var rootCmd = new RootCommand("squawk — Terminal ATC Radar");

var radiusOpt = new Option<double?>(
    name: "--radius",
    description: "Radar yarıçapı (km). Varsayılan: config'deki değer veya 20 km.");

var latOpt = new Option<double?>(
    name: "--lat",
    description: "Enlem. Varsayılan: config'deki değer.");

var lonOpt = new Option<double?>(
    name: "--lon",
    description: "Boylam. Varsayılan: config'deki değer.");

var setupOpt = new Option<bool>(
    name: "--setup",
    description: "İlk kurulum sihirbazını çalıştırır.");

rootCmd.AddOption(radiusOpt);
rootCmd.AddOption(latOpt);
rootCmd.AddOption(lonOpt);
rootCmd.AddOption(setupOpt);

rootCmd.SetHandler(async (radius, lat, lon, forceSetup) =>
{
    string userConfigPath = GetUserConfigPath();

    // ─── Konfigürasyon Yükle ─────────────────────────────────────────────
    var configBuilder = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile(userConfigPath, optional: true)
        .AddEnvironmentVariables("SQUAWK_");

    var configuration = configBuilder.Build();

    var appConfig = new AppConfig();
    configuration.Bind(appConfig);

    // CLI argümanları config'i override eder
    if (radius.HasValue)  appConfig.Radar.RadiusKm   = radius.Value;
    if (lat.HasValue)     appConfig.Location.Latitude  = lat.Value;
    if (lon.HasValue)     appConfig.Location.Longitude = lon.Value;

    // ─── İlk Kurulum ─────────────────────────────────────────────────────
    if (forceSetup || !appConfig.Location.IsConfigured)
    {
        await RunSetupWizardAsync(appConfig, userConfigPath);
    }

    // ─── DI Container ─────────────────────────────────────────────────────
    var services = new ServiceCollection();
    services.AddSingleton(appConfig);
    services.AddSingleton<GeoService>();
    services.AddHttpClient<OpenSkyService>();
    services.AddSingleton<RadarRenderer>();

    var sp = services.BuildServiceProvider();

    // ─── Graceful Shutdown ─────────────────────────────────────────────────
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // ─── Radar Başlat ──────────────────────────────────────────────────────
    var renderer = sp.GetRequiredService<RadarRenderer>();
    await renderer.RunAsync(cts.Token);

}, radiusOpt, latOpt, lonOpt, setupOpt);

return await rootCmd.InvokeAsync(args);

// ─── İlk Kurulum Sihirbazı ───────────────────────────────────────────────────

static async Task RunSetupWizardAsync(AppConfig config, string savePath)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.Clear();

    void Header(string text)
    {
        Console.WriteLine();
        Console.Write($"\x1b[92m  {text}\x1b[0m");
        Console.WriteLine();
    }

    string Ask(string prompt, string? defaultVal = null)
    {
        string suffix = defaultVal != null ? $" \x1b[90m[{defaultVal}]\x1b[92m" : "";
        Console.Write($"\x1b[32m  ▸ {prompt}{suffix}: \x1b[92m");
        string? input = Console.ReadLine()?.Trim();
        Console.Write("\x1b[0m");
        return string.IsNullOrEmpty(input) ? (defaultVal ?? "") : input;
    }

    double AskDouble(string prompt, double defaultVal)
    {
        string raw = Ask(prompt, defaultVal.ToString("G", CultureInfo.InvariantCulture));
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : defaultVal;
    }

    Console.WriteLine("\x1b[92m");
    Console.WriteLine("  ╔══════════════════════════════════════════╗");
    Console.WriteLine("  ║         ◉  SQUAWK — İlk Kurulum         ║");
    Console.WriteLine("  ╚══════════════════════════════════════════╝");
    Console.WriteLine("\x1b[0m");
    Console.WriteLine("\x1b[32m  Ayarlar şuraya kaydedilecek:\x1b[90m");
    Console.WriteLine($"  {savePath}");
    Console.WriteLine("\x1b[0m");

    // Konum
    Header("📍 Konum");
    config.Location.Name = Ask(
        "Konum adı (örn. Istanbul)",
        string.IsNullOrEmpty(config.Location.Name) ? null : config.Location.Name);
    config.Location.Latitude = AskDouble(
        "Enlem (Latitude, örn. 41.0082)",
        config.Location.Latitude == 0 ? 41.0082 : config.Location.Latitude);
    config.Location.Longitude = AskDouble(
        "Boylam (Longitude, örn. 28.9784)",
        config.Location.Longitude == 0 ? 28.9784 : config.Location.Longitude);

    // Radar
    Header("📡 Radar");
    config.Radar.RadiusKm = AskDouble(
        "Yarıçap (km)",
        config.Radar.RadiusKm > 0 ? config.Radar.RadiusKm : 20);

    // OpenSky
    Header("🔑 OpenSky Network API");
    Console.WriteLine("\x1b[90m  (Boş bırakırsanız anonim erişim kullanılır — rate limit daha düşük olur)\x1b[0m");
    config.OpenSky.ClientId = Ask(
        "Client ID",
        string.IsNullOrEmpty(config.OpenSky.ClientId) ? null : config.OpenSky.ClientId);
    config.OpenSky.ClientSecret = Ask(
        "Client Secret",
        string.IsNullOrEmpty(config.OpenSky.ClientSecret) ? null : config.OpenSky.ClientSecret);

    // Kaydet
    var saveObj = new
    {
        OpenSky = new
        {
            config.OpenSky.ClientId,
            config.OpenSky.ClientSecret,
            config.OpenSky.TokenUrl,
        },
        Location = new
        {
            config.Location.Latitude,
            config.Location.Longitude,
            config.Location.Name,
        },
        Radar = new
        {
            config.Radar.RadiusKm,
            config.Radar.RefreshSeconds,
        },
    };

    var json = JsonSerializer.Serialize(saveObj, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(savePath, json);

    Console.WriteLine();
    Console.WriteLine($"\x1b[92m  ✓ Ayarlar kaydedildi → {savePath}\x1b[0m");
    Console.WriteLine("\x1b[32m  Radar başlatılıyor...\x1b[0m");
    await Task.Delay(1500);
}
