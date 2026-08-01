using System.Text;

namespace Squawk.Radar;

/// <summary>ANSI escape code sabitleri ve yardımcıları.</summary>
public static class Ansi
{
    // Renkler
    public const string Reset        = "\x1b[0m";
    public const string BrightGreen  = "\x1b[92m";
    public const string Green        = "\x1b[32m";
    public const string DimGreen     = "\x1b[2;32m";
    public const string Cyan         = "\x1b[96m";
    public const string Yellow       = "\x1b[93m";
    public const string White        = "\x1b[97m";
    public const string DarkGray     = "\x1b[90m";
    public const string BrightYellow = "\x1b[93m";

    // İmleç
    public static string MoveTo(int row, int col) => $"\x1b[{row};{col}H";
    public static string HideCursor => "\x1b[?25l";
    public static string ShowCursor => "\x1b[?25h";
    public static string ClearScreen => "\x1b[2J\x1b[H";

    /// <summary>Windows Terminal / PowerShell için ANSI desteğini etkinleştirir.</summary>
    public static void EnableOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        WindowsVT.Enable();
    }
}

/// <summary>
/// Çift-tamponlu konsol canvas.
/// Yalnızca değişen hücreler ekrana yazılır (diff rendering) → titreme olmaz.
/// </summary>
public class ConsoleCanvas
{
    private readonly char[,] _chars;
    private readonly string[,] _colors;
    private readonly char[,] _prevChars;
    private readonly string[,] _prevColors;

    public int Width { get; }
    public int Height { get; }

    public ConsoleCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _chars = new char[height, width];
        _colors = new string[height, width];
        _prevChars = new char[height, width];
        _prevColors = new string[height, width];
        Clear();
        // Prev'i farklı bir değerle doldur ki ilk frame tam çizilsin
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                _prevChars[y, x] = '\0';
    }

    /// <summary>Canvas'ı boşlukla temizler.</summary>
    public void Clear()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                _chars[y, x] = ' ';
                _colors[y, x] = Ansi.Reset;
            }
    }

    /// <summary>Belirtilen pozisyona bir karakter yazar.</summary>
    public void Set(int x, int y, char c, string color = "")
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        _chars[y, x] = c;
        _colors[y, x] = string.IsNullOrEmpty(color) ? Ansi.Reset : color;
    }

    /// <summary>Belirtilen pozisyondaki karakteri okur.</summary>
    public char GetChar(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return '\0';
        return _chars[y, x];
    }

    /// <summary>Bir metni belirtilen pozisyondan itibaren yazar.</summary>
    public void WriteString(int x, int y, string text, string color = "")
    {
        var c = string.IsNullOrEmpty(color) ? Ansi.Reset : color;
        for (int i = 0; i < text.Length && x + i < Width; i++)
            Set(x + i, y, text[i], c);
    }

    /// <summary>
    /// Önceki frame'e göre yalnızca değişen hücreleri terminale yazar.
    /// Çok hızlı render sağlar, titreme olmaz.
    /// </summary>
    public void RenderDiff()
    {
        var sb = new StringBuilder(4096);
        string currentColor = "";
        bool needsMove = true;
        int lastRow = -1, lastCol = -1;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char c = _chars[y, x];
                string color = _colors[y, x];

                // Değişmediyse atla
                if (c == _prevChars[y, x] && color == _prevColors[y, x])
                {
                    needsMove = true;
                    continue;
                }

                // İmleç konumu güncelle
                if (needsMove || y != lastRow || x != lastCol + 1)
                {
                    sb.Append(Ansi.MoveTo(y + 1, x + 1));
                    needsMove = false;
                }

                // Renk güncelle (sadece değişirse)
                if (color != currentColor)
                {
                    sb.Append(color);
                    currentColor = color;
                }

                sb.Append(c);
                _prevChars[y, x] = c;
                _prevColors[y, x] = color;
                lastRow = y;
                lastCol = x;
            }
        }

        if (sb.Length > 0)
            Console.Write(sb.ToString());
    }

    /// <summary>Tüm canvas'ı terminale yazar (tam yeniden çizim).</summary>
    public void RenderFull()
    {
        var sb = new StringBuilder(Width * Height * 4);
        string currentColor = "";

        for (int y = 0; y < Height; y++)
        {
            sb.Append(Ansi.MoveTo(y + 1, 1));
            for (int x = 0; x < Width; x++)
            {
                string color = _colors[y, x];
                if (color != currentColor)
                {
                    sb.Append(color);
                    currentColor = color;
                }
                sb.Append(_chars[y, x]);
                _prevChars[y, x] = _chars[y, x];
                _prevColors[y, x] = color;
            }
        }

        sb.Append(Ansi.Reset);
        Console.Write(sb.ToString());
    }
}

/// <summary>Windows P/Invoke — ENABLE_VIRTUAL_TERMINAL_PROCESSING ayarı.</summary>
internal static class WindowsVT
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    public static void Enable()
    {
        try
        {
            var handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (GetConsoleMode(handle, out uint mode))
                SetConsoleMode(handle, mode | 0x0004u); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }
        catch { /* Yoksay — terminal zaten destekliyordur */ }
    }
}
