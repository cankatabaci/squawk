using Squawk.Config;
using Squawk.Models;
using Squawk.Services;

namespace Squawk.Radar;

/// <summary>
/// Ana radar render döngüsü.
///
/// Layout:
///   Row 0      : ╔══...══╗  (üst kenar)
///   Row 1      : ║ Header ║  (başlık)
///   Row 2      : ╠══...══╣  (ayraç)
///   Row 3..H-4 : ║ Radar  ║  Uçak Paneli ║
///   Row H-3    : ╠══...══╣  (ayraç)
///   Row H-2    : ║ Status ║  (durum)
///   Row H-1    : ╚══...══╝  (alt kenar)
///
/// Radar dairesel (ekran üzerinde ellips) görünür.
/// Karakter hücreleri ~2:1 yükseklik:genişlik → Y yarıçapı = X yarıçapının yarısı.
/// </summary>
public class RadarRenderer
{
    private readonly AppConfig _config;
    private readonly OpenSkyService _openSky;

    // Layout
    private ConsoleCanvas? _canvas;
    private int _totalW, _totalH;
    private int _divX;           // Radar/panel dikey ayraç X koordinatı
    private int _cx, _cy;        // Radar merkezi
    private int _rx, _ry;        // Radar yarıçapı (x: karakter, y: satır)
    private int _prevW, _prevH;  // Terminal boyut takibi

    // Durum
    private List<Aircraft> _aircraft = [];
    private DateTime _lastUpdate = DateTime.MinValue;
    private DateTime _nextUpdate = DateTime.UtcNow;
    private double _sweepAngleDeg;   // 0° = Kuzey, saat yönünde
    private string _statusMsg = "Başlatılıyor...";
    private bool _isError;
    private bool _isFetching;

    private const int FrameMs = 80; // ~12 FPS

    public RadarRenderer(AppConfig config, OpenSkyService openSky)
    {
        _config = config;
        _openSky = openSky;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        Ansi.EnableOnWindows();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Write(Ansi.HideCursor);
        Console.Clear();
        Console.Write(Ansi.ClearScreen);

        try
        {
            InitLayout();

            // İlk veri çekimi
            _ = FetchAsync(ct);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (!ct.IsCancellationRequested)
            {
                long frameStart = sw.ElapsedMilliseconds;

                // Terminal boyut değişti mi?
                if (Console.WindowWidth != _prevW || Console.WindowHeight != _prevH)
                {
                    Console.Clear();
                    Console.Write(Ansi.ClearScreen);
                    InitLayout();
                }

                // Sweep açısını ilerlet: 360° / (refreshSec * 1000ms / frameMs) derece/frame
                double degsPerFrame = 360.0 / (_config.Radar.RefreshSeconds * 1000.0 / FrameMs);
                _sweepAngleDeg = (_sweepAngleDeg + degsPerFrame) % 360.0;

                // Yenileme zamanı geldiyse veri çek
                if (!_isFetching && DateTime.UtcNow >= _nextUpdate)
                {
                    _nextUpdate = DateTime.UtcNow.AddSeconds(_config.Radar.RefreshSeconds);
                    _ = FetchAsync(ct);
                }

                // Frame çiz
                DrawFrame();

                // Kılavuz tuşlar (Q=çıkış, R=hemen yenile)
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        break;
                    if (key.Key == ConsoleKey.R)
                        _nextUpdate = DateTime.UtcNow; // zorla yenile
                }

                long elapsed = sw.ElapsedMilliseconds - frameStart;
                int delay = Math.Max(1, FrameMs - (int)elapsed);
                await Task.Delay(delay, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Write(Ansi.Reset);
            Console.Clear();
            Console.WriteLine($"Beklenmeyen hata: {ex.Message}");
        }
        finally
        {
            Console.Write(Ansi.Reset);
            Console.Write(Ansi.ShowCursor);
            Console.WriteLine();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Layout
    // ─────────────────────────────────────────────────────────────────────────

    private void InitLayout()
    {
        _totalW = Math.Max(100, Math.Min(Console.WindowWidth - 1, 160));
        _totalH = Math.Max(28, Math.Min(Console.WindowHeight - 1, 50));

        _prevW = Console.WindowWidth;
        _prevH = Console.WindowHeight;

        // Radar alanı: sol ~62%
        int radarAreaW = (_totalW * 62) / 100;
        int radarAreaH = _totalH - 7; // header(2) + separator(1) + footer(3) + bottom(1)

        // Karakter aspect ratio ~2:1 (yükseklik:genişlik)
        // Görsel daire için: rx = 2 * ry
        int maxRxFromWidth  = radarAreaW / 2 - 4;
        int maxRxFromHeight = radarAreaH - 4; // = 2 * (radarAreaH/2 - 2)

        _rx = Math.Min(maxRxFromWidth, maxRxFromHeight);
        _ry = _rx / 2;

        _divX = radarAreaW + 1;
        _cx   = radarAreaW / 2;
        _cy   = 3 + radarAreaH / 2; // header(3 rows) + half radar area

        _canvas = new ConsoleCanvas(_totalW, _totalH);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  API Fetch
    // ─────────────────────────────────────────────────────────────────────────

    private async Task FetchAsync(CancellationToken ct)
    {
        _isFetching = true;
        _statusMsg = "Veri alınıyor...";
        _isError = false;
        try
        {
            var ac = await _openSky.GetNearbyAircraftAsync(ct);
            _aircraft = ac;
            _lastUpdate = DateTime.Now;
            _statusMsg = $"Son güncelleme: {_lastUpdate:HH:mm:ss}  ·  {_aircraft.Count} uçak tespit edildi";
            _isError = false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _statusMsg = $"API hatası: {ex.Message[..Math.Min(ex.Message.Length, 60)]}";
            _isError = true;
        }
        finally
        {
            _isFetching = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Frame Drawing
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawFrame()
    {
        if (_canvas == null) return;
        _canvas.Clear();

        DrawBorder();
        DrawHeader();
        DrawRadar();
        DrawAircraftPanel();
        DrawFooter();

        _canvas.RenderDiff();
    }

    // — Dış çerçeve + paneller —

    private void DrawBorder()
    {
        var c = _canvas!;
        int w = _totalW, h = _totalH;

        // Üst kenar
        c.WriteString(0, 0, "╔" + new string('═', w - 2) + "╗", Ansi.Green);
        // Alt kenar
        c.WriteString(0, h - 1, "╚" + new string('═', w - 2) + "╝", Ansi.Green);
        // Sol/sağ kenarlar
        for (int y = 1; y < h - 1; y++)
        {
            c.Set(0, y, '║', Ansi.Green);
            c.Set(w - 1, y, '║', Ansi.Green);
        }

        // Başlık ayracı (row 2)
        c.WriteString(0, 2, "╠" + new string('═', w - 2) + "╣", Ansi.Green);

        // Footer ayracı (row H-3)
        c.WriteString(0, h - 3, "╠" + new string('═', w - 2) + "╣", Ansi.Green);

        // Radar/Panel dikey ayraç
        c.Set(_divX, 2, '╦', Ansi.Green);
        c.Set(_divX, h - 3, '╩', Ansi.Green);
        for (int y = 3; y < h - 3; y++)
            c.Set(_divX, y, '│', Ansi.DimGreen);
    }

    // — Başlık satırı —

    private void DrawHeader()
    {
        var c = _canvas!;
        var loc = _config.Location;

        string latStr = loc.Latitude >= 0
            ? $"{loc.Latitude:F4}°N"
            : $"{-loc.Latitude:F4}°S";
        string lonStr = loc.Longitude >= 0
            ? $"{loc.Longitude:F4}°E"
            : $"{-loc.Longitude:F4}°W";

        // Sol: Logo
        c.WriteString(2, 1, "◉ SQUAWK", Ansi.BrightGreen);

        // Orta: Konum
        string locStr = $"▸ {loc.Name}  {latStr} {lonStr}  ·  R: {_config.Radar.RadiusKm:F0} km";
        int locX = Math.Max(12, (_totalW - locStr.Length) / 2);
        c.WriteString(locX, 1, locStr, Ansi.Green);

        // Sağ: Saat
        string timeStr = DateTime.Now.ToString("HH:mm:ss");
        c.WriteString(_totalW - timeStr.Length - 2, 1, timeStr, Ansi.BrightGreen);
    }

    // — Radar çizimi —

    private void DrawRadar()
    {
        DrawCrosshairs();
        DrawRings();
        DrawCompassLabels();
        DrawRangeLabels();
        DrawSweep();
        DrawAircraftOnRadar();
        // Merkez nokta (her şeyin üstünde)
        _canvas!.Set(_cx, _cy, '⊕', Ansi.BrightGreen);
    }

    private void DrawCrosshairs()
    {
        var c = _canvas!;
        // Yatay (D-B ekseni)
        for (int x = _cx - _rx; x <= _cx + _rx; x++)
            c.Set(x, _cy, '·', Ansi.DimGreen);
        // Dikey (K-G ekseni)
        for (int y = _cy - _ry; y <= _cy + _ry; y++)
            c.Set(_cx, y, '·', Ansi.DimGreen);
    }

    private void DrawRings()
    {
        // 3 eşit halka
        for (int ring = 1; ring <= 3; ring++)
        {
            double rRx = _rx * ring / 3.0;
            double rRy = _ry * ring / 3.0;
            DrawEllipse(rRx, rRy, '·', Ansi.DimGreen);
        }
    }

    private void DrawEllipse(double rRx, double rRy, char symbol, string color)
    {
        // Adım boyutunu kümülatif pixele göre belirle
        double step = Math.Min(0.5, 1.0 / Math.Max(rRx, 1));
        for (double theta = 0; theta < Math.PI * 2; theta += step)
        {
            int px = _cx + (int)Math.Round(rRx * Math.Cos(theta));
            int py = _cy + (int)Math.Round(rRy * Math.Sin(theta));
            _canvas!.Set(px, py, symbol, color);
        }
    }

    private void DrawCompassLabels()
    {
        var c = _canvas!;
        // N
        c.WriteString(_cx - 1, _cy - _ry - 1, " N ", Ansi.Green);
        // S
        c.WriteString(_cx - 1, _cy + _ry + 1, " S ", Ansi.Green);
        // E
        c.Set(_cx + _rx + 2, _cy, 'E', Ansi.Green);
        // W
        c.Set(_cx - _rx - 3, _cy, 'W', Ansi.Green);
    }

    private void DrawRangeLabels()
    {
        var c = _canvas!;
        for (int ring = 1; ring <= 3; ring++)
        {
            double distKm = _config.Radar.RadiusKm * ring / 3.0;
            string label = $"{distKm:F0}km";
            int lx = _cx + (int)(_rx * ring / 3.0) + 1;
            // Küçük ofsette, crosshair'in biraz üstüne
            c.WriteString(lx, _cy - 1, label, Ansi.DimGreen);
        }
    }

    // — Sweep çizgisi + iz —

    private void DrawSweep()
    {
        double sweepRad = _sweepAngleDeg * Math.PI / 180.0;

        // İz (sweep'in gerisinde soluklaşan gölge, ~25°)
        for (int trail = 25; trail >= 1; trail--)
        {
            double trailDeg  = (_sweepAngleDeg - trail + 360.0) % 360.0;
            double trailRad  = trailDeg * Math.PI / 180.0;
            string trailColor = trail <= 6 ? Ansi.Green : Ansi.DimGreen;

            DrawRadialLine(trailRad, '·', trailColor, onlyIfEmpty: true);
        }

        // Ana sweep çizgisi (parlak)
        DrawRadialLine(sweepRad, '│', Ansi.BrightGreen, onlyIfEmpty: false);
    }

    private void DrawRadialLine(double angleRad, char symbol, string color, bool onlyIfEmpty)
    {
        double sinA = Math.Sin(angleRad);
        double cosA = Math.Cos(angleRad);
        double step = 1.0 / Math.Max(_rx, 1);

        // Not: 0° = Kuzey → sin(N→E), -cos(N→up)
        for (double t = step; t <= 1.0; t += step)
        {
            int px = _cx + (int)Math.Round(_rx * t * sinA);
            int py = _cy - (int)Math.Round(_ry * t * cosA);

            if (onlyIfEmpty && _canvas!.GetChar(px, py) != ' ')
                continue;
            _canvas!.Set(px, py, symbol, color);
        }
    }

    // — Uçaklar radar üzerinde —

    private void DrawAircraftOnRadar()
    {
        foreach (var ac in _aircraft)
        {
            if (ac.Latitude == null || ac.Longitude == null) continue;

            double ratio   = ac.DistanceKm / _config.Radar.RadiusKm;
            double bearRad = ac.BearingDeg * Math.PI / 180.0;

            int ax = _cx + (int)Math.Round(_rx * ratio * Math.Sin(bearRad));
            int ay = _cy - (int)Math.Round(_ry * ratio * Math.Cos(bearRad));

            // Uçak simgesi
            _canvas!.Set(ax, ay, '✈', Ansi.BrightGreen);

            // Callsign etiketi — ikonun BIR SATIR ALTINDA, ortalanmış
            // Böylece ikon hiçbir zaman harflerin üstüne gelmez.
            string label = ac.DisplayCallsign.Length > 8
                ? ac.DisplayCallsign[..8]
                : ac.DisplayCallsign;

            int labelY = ay + 1;                        // ikonun altındaki satır
            int labelX = ax - label.Length / 2;         // ortalanmış

            // Eğer alt satır footer veya radar dışına giriyorsa üst satırı dene
            if (labelY >= _totalH - 3)
                labelY = ay - 1;

            // Yatay sınırlamalar
            if (labelX < 1) labelX = 1;
            if (labelX + label.Length >= _divX) labelX = _divX - label.Length - 1;

            // Radar alanı içindeyse göster
            if (labelY >= 3 && labelY < _totalH - 3 && labelX >= 1)
                _canvas.WriteString(labelX, labelY, label, Ansi.Green);
        }
    }

    // — Sağ panel: Uçak listesi —

    private void DrawAircraftPanel()
    {
        var c    = _canvas!;
        int px   = _divX + 2;
        int py   = 3;
        int maxW = _totalW - px - 2;

        // Başlık
        c.WriteString(px, py++, "AIRCRAFT", Ansi.BrightGreen);
        c.WriteString(px, py++, new string('─', Math.Min(maxW, 36)), Ansi.DimGreen);

        // Kolon başlıkları — 2 satırlık format
        c.WriteString(px, py++, "CALLSIGN  DIST   HDG  VERT", Ansi.DimGreen);
        c.WriteString(px, py++, "  FL     SPEED    SQ", Ansi.DimGreen);
        c.WriteString(px, py++, new string('─', Math.Min(maxW, 36)), Ansi.DimGreen);

        if (_aircraft.Count == 0)
        {
            string noAcMsg = _isFetching ? "Aranıyor..." : "Uçak bulunamadı";
            c.WriteString(px, py, noAcMsg, Ansi.DimGreen);
            return;
        }

        // Her uçak için 2 satır (+ 1 boşluk)
        int rowsPerAc = 3;
        int maxVisible = (_totalH - py - 4) / rowsPerAc;
        int shown = 0;

        foreach (var ac in _aircraft)
        {
            if (shown >= maxVisible) break;
            if (py + rowsPerAc >= _totalH - 3) break;

            // Yakınlığa göre renk
            string col = ac.DistanceKm < _config.Radar.RadiusKm * 0.35
                ? Ansi.BrightGreen
                : ac.DistanceKm < _config.Radar.RadiusKm * 0.70
                    ? Ansi.Green
                    : Ansi.DimGreen;

            // Squawk acil durum rengi
            bool isEmergency = ac.Squawk is "7700" or "7500" or "7600";
            string squawkCol = isEmergency ? Ansi.Yellow : col;

            // ── Satır 1: ✈ Callsign  Mesafe  Heading  Dikey ──
            string callsign = PadTrunc(ac.DisplayCallsign, 9);
            string dist     = PadTrunc($"{ac.DistanceKm:F1}km", 7);
            string heading  = PadTrunc(ac.HeadingDisplay, 5);
            string vert     = ac.VerticalDisplay;

            string line1 = $"✈ {callsign}{dist}{heading}{vert}";
            c.WriteString(px, py++, PadTrunc(line1, maxW), col);

            // ── Satır 2: FL  Hız  Squawk ──
            string fl    = PadTrunc(ac.FlightLevelDisplay, 7);
            string speed = PadTrunc(ac.SpeedDisplay, 9);
            string sq    = $"SQ:{ac.SquawkDisplay}";

            string line2 = $"  {fl}{speed}{sq}";
            c.WriteString(px, py, PadTrunc(line2, maxW), squawkCol);
            py++;

            // Boşluk satırı
            py++;
            shown++;
        }

        if (_aircraft.Count > shown)
            c.WriteString(px, py, $"  +{_aircraft.Count - shown} daha...", Ansi.DimGreen);
    }

    // — Footer: Durum ve geri sayım —

    private void DrawFooter()
    {
        var c = _canvas!;
        int footerY = _totalH - 2;

        string bullet = _isError ? "⚠" : (_isFetching ? "◌" : "●");
        string bulletColor = _isError ? Ansi.Yellow : Ansi.BrightGreen;

        c.Set(2, footerY, bullet[0], bulletColor);
        c.WriteString(4, footerY, _statusMsg, _isError ? Ansi.Yellow : Ansi.Green);

        // Geri sayım
        var remaining = _nextUpdate - DateTime.UtcNow;
        string countdown = remaining.TotalSeconds > 0
            ? $"Sonraki: {remaining.TotalSeconds:F0}s "
            : "Güncelleniyor...";
        c.WriteString(_totalW - countdown.Length - 2, footerY, countdown, Ansi.DimGreen);

        // Tuş kılavuzu (son satır)
        c.WriteString(2, _totalH - 1, "[Q] Çıkış", Ansi.DimGreen);
        c.WriteString(14, _totalH - 1, "[R] Hemen Yenile", Ansi.DimGreen);

        // Sweep yönü göstergesi
        string sweepIndicator = $"Sweep: {_sweepAngleDeg:F0}°";
        c.WriteString(_totalW - sweepIndicator.Length - 2, _totalH - 1, sweepIndicator, Ansi.DimGreen);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Yardımcılar
    // ─────────────────────────────────────────────────────────────────────────

    private static string PadTrunc(string s, int width)
    {
        if (s.Length >= width) return s[..width];
        return s.PadRight(width);
    }
}
