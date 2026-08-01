<div align="center">

🌐 **Language / Dil:** English &nbsp;|&nbsp; [Türkçe](README.tr.md)

```
  ╔══════════════════════════════════════════════════════════════════╗
  ║  ◉ SQUAWK  ▸ Ankara  39.9476°N 32.6097°E              03:12:48  ║
  ╠══════════════════════════════════════════════╦═══════════════════╣
  ║              N                               ║ AIRCRAFT          ║
  ║         . . · . . .                          ║ ───────────────── ║
  ║      ·    THY448      ·                      ║ CALLSIGN DIST HDG ║
  ║    ·          ✈         ·                    ║ ─────────────────║
  ║   ·                      ·                   ║ ✈ THY448  8.2km E║
  ║  W·----------⊕----------·E                  ║   FL280  480kt    ║
  ║   ·    ✈                 ·                   ║   SQ:2341        ║
  ║    · PGT891     20km   ·                     ║ ✈ PGT891 14.7km S║
  ║      ·               ·                       ║   FL320  510kt    ║
  ║         . . · . . .                          ║   SQ:4412         ║
  ║              S                               ║                   ║
  ╠══════════════════════════════════════════════╩═══════════════════╣
  ║  ● Son güncelleme: 03:12:35  ·  2 uçak            Sonraki: 18s   ║
  ╚══════════════════════════════════════════════════════════════════╝
```

# squawk ✈

**A terminal-based ATC radar that shows real aircraft flying near you — in real time.**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-lightgrey)](#requirements)
[![OpenSky](https://img.shields.io/badge/data-OpenSky%20Network-003366?logo=openstreetmap)](https://opensky-network.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

---

`squawk` pulls live flight data from the [OpenSky Network API](https://opensky-network.org/) every 30 seconds and renders it in your terminal as a green phosphor radar — just like the ones air traffic controllers use.

---

## Table of Contents

- [**Quick Install**](#quick-install) ← start here
- [Features](#features)
- [Requirements](#requirements)
- [First Run — Setup Wizard](#first-run--setup-wizard)
- [OpenSky Network API Setup](#opensky-network-api-setup)
- [Usage](#usage)
- [Understanding the Display](#understanding-the-display)
  - [The Radar (Left Panel)](#the-radar-left-panel)
  - [Aircraft List (Right Panel)](#aircraft-list-right-panel)
  - [Compass Directions](#compass-directions)
  - [Special Squawk Codes](#special-squawk-codes)
  - [Status Bar](#status-bar)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Reconfiguring](#reconfiguring)
- [Building a Single-File Binary](#building-a-single-file-binary)
- [Publishing to NuGet](#publishing-to-nuget)
- [Security](#security)
- [How It Works](#how-it-works)
- [License](#license)

---

## Features

- 🟢 **Green phosphor radar** — rotating sweep line, concentric range rings, compass labels
- ✈ **Live aircraft** from [OpenSky Network](https://opensky-network.org/) — updates every 30 seconds
- 📡 **Configurable radius** — default 20 km, overridable per-run with `--radius`
- 🔢 **Rich aircraft data** — flight level, speed, heading, transponder (squawk) code
- 🚨 **Emergency squawk highlighting** — 7700/7500/7600 codes turn yellow automatically
- 💾 **Credentials stored safely** — your API keys and location live in your OS user data folder, never in the repo
- 🖥️ **Cross-platform** — Windows Terminal, PowerShell 7+, macOS Terminal

---

## Requirements

| Requirement | Details |
|---|---|
| **.NET 10 SDK** | [Download here](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Terminal** | Windows Terminal or PowerShell 7+ (Windows) · Terminal.app or iTerm2 (macOS) |
| **OpenSky Account** | Free at [opensky-network.org](https://opensky-network.org/) — anonymous access also works |

> **Why not CMD?**  
> Classic `cmd.exe` has limited support for the Unicode characters (✈, ─, ╔, etc.) and ANSI colour sequences this app uses. Windows Terminal or PowerShell 7+ handle them perfectly.

---

## Quick Install

> **Requires [.NET 10 SDK or Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)** — the only prerequisite.

Install squawk as a global .NET tool in one command:

```bash
dotnet tool install -g squawk
```

Then run it from **anywhere** in your terminal:

```bash
squawk
```

The first time it launches, the [Setup Wizard](#first-run--setup-wizard) will ask for your location and (optionally) your OpenSky API credentials.

### Updating

```bash
dotnet tool update -g squawk
```

### Uninstalling

```bash
dotnet tool uninstall -g squawk
```

---

## Installation from Source

For contributors or if you want to hack on the code:

**1. Clone**

```bash
git clone https://github.com/cankatabaci/squawk.git
cd squawk/src/Squawk
```

**2. Run**

```bash
dotnet run
```

No Docker, no npm, no Python — just .NET.

---

## First Run — Setup Wizard

The first time you run `squawk`, an interactive wizard starts automatically:

```
dotnet run
```

```
  ╔══════════════════════════════════════════╗
  ║         ◉  SQUAWK — İlk Kurulum         ║
  ╚══════════════════════════════════════════╝

  📍 Konum
  ▸ Konum adı (örn. Istanbul): Ankara
  ▸ Enlem (Latitude, örn. 41.0082):  39.9476
  ▸ Boylam (Longitude, örn. 28.9784): 32.6097

  📡 Radar
  ▸ Yarıçap (km) [20]: 20

  🔑 OpenSky Network API
  ▸ Client ID: YOUR_CLIENT_ID
  ▸ Client Secret: YOUR_CLIENT_SECRET

  ✓ Ayarlar kaydedildi.
```

Your answers are saved to a **config file outside the project**:

| OS | Config file location |
|---|---|
| Windows | `%APPDATA%\squawk\config.json` |
| macOS | `~/.config/squawk/config.json` |
| Linux | `~/.config/squawk/config.json` |

This means your credentials and location are **never committed to Git**.

---

## OpenSky Network API Setup

OpenSky provides two access levels:

### Option A — Anonymous (simplest)

Just leave Client ID and Client Secret blank during setup. Anonymous access works, but has a lower daily request quota. With a 30-second refresh interval, this is usually fine for personal use.

### Option B — Authenticated (recommended)

Authenticated access gives you a higher rate limit and is free.

1. Create a free account at [opensky-network.org](https://opensky-network.org/)
2. Log in → **Account** → **API Clients** → **Create new client**
3. You'll get a `client_id` and `client_secret`
4. Enter them when the setup wizard asks, or run `dotnet run -- --setup` to re-configure

> **Note:** As of March 2026, OpenSky uses **OAuth2 client credentials** (not username/password). squawk handles the token fetch and automatic renewal for you.

---

## Usage

```bash
# Default — use whatever you configured in the wizard
dotnet run

# Override the radar radius for this session only (doesn't change saved config)
dotnet run -- --radius 50

# Override your location for this session
dotnet run -- --lat 41.0082 --lon 28.9784

# Combine options
dotnet run -- --lat 41.0082 --lon 28.9784 --radius 30

# Re-run the setup wizard (to change location, radius, or API keys)
dotnet run -- --setup

# Help
dotnet run -- --help
```

### All CLI options

| Option | Type | Description |
|---|---|---|
| `--radius <km>` | `float` | Radar radius in km (default: value from config, fallback 20) |
| `--lat <degrees>` | `float` | Your latitude (overrides config for this session) |
| `--lon <degrees>` | `float` | Your longitude (overrides config for this session) |
| `--setup` | flag | Re-run the interactive setup wizard |
| `--help` | flag | Show help |

---

## Understanding the Display

### The Radar (Left Panel)

```
              N
         . . · . . .
      ·               ·
    ·    ✈              ·
   ·   THY448            ·
  W·---------⊕---------·E
   ·         ✈          ·
    ·      PGT891  20km·
      ·               ·
         . . · . . .
              S
```

| Element | Meaning |
|---|---|
| `⊕` | **Your position** — the center of the radar |
| `N / S / E / W` | **Compass labels** — North, South, East, West |
| `. . · . .` | **Range rings** — three concentric circles at 1/3, 2/3, and full radius |
| `20km` | **Range label** — the outermost ring's distance (your configured radius) |
| `✈` | **Aircraft** — positioned on the radar according to their real bearing and distance from you |
| Callsign below `✈` | **Flight identifier** — the aircraft's callsign (e.g., `THY448`) or ICAO address if no callsign is available |
| Rotating bright line | **Sweep line** — completes one full rotation every 30 seconds, in sync with the API refresh |
| Fading trail behind sweep | **Persistence effect** — mimics the phosphor glow fade on real radar screens |

**How to read where an aircraft is:**  
The radar is a top-down map. If a ✈ appears in the upper-right area of the circle, that aircraft is to your **northeast**. If it's near the edge, it's close to your maximum range. If it's near the center, it's close to you.

---

### Aircraft List (Right Panel)

Each aircraft gets **two lines**:

```
✈ THY448   8.2km  E    ↑
  FL280    480kt  SQ:2341
```

#### Line 1

| Field | Example | Meaning |
|---|---|---|
| `✈` | `✈` | Aircraft indicator |
| Callsign | `THY448` | The flight's radio callsign. Airlines use codes like `THY` (Turkish Airlines), `PGT` (Pegasus), `SXS` (SunExpress). If no callsign is broadcast, the ICAO hex address is shown instead. |
| Distance | `8.2km` | Straight-line distance from your location to the aircraft, calculated using the Haversine formula |
| Heading | `E` | The direction **the aircraft is flying toward** (not the direction from you to the aircraft — that's shown by where the ✈ icon sits on the radar). See [Compass Directions](#compass-directions). |
| Vertical | `↑` `↓` `→` | Whether the aircraft is **climbing** (↑), **descending** (↓), or in **level flight** (→) |

#### Line 2

| Field | Example | Meaning |
|---|---|---|
| Flight Level | `FL280` | Altitude expressed as a **Flight Level** — FL280 means approximately 28,000 feet (about 8,534 metres). Divide FL by 10 to get a rough kilometre figure: FL280 ≈ 8.5 km up. |
| Speed | `480kt` | Groundspeed in **knots** (kt). 1 knot = 1.852 km/h. Typical cruising speed is 450–500 kt for commercial jets. |
| Squawk | `SQ:2341` | The **transponder code** the pilot has dialled in. A 4-digit code assigned by air traffic control to identify the flight on radar. See [Special Squawk Codes](#special-squawk-codes). |

---

### Compass Directions

The **HDG** (Heading) column shows which direction the aircraft is flying, split into 8 compass points:

| Code | Full name | Meaning |
|---|---|---|
| `N` | North | Flying toward north (roughly 0° / 360°) |
| `NE` | Northeast | Flying toward northeast (~45°) |
| `E` | East | Flying toward east (~90°) |
| `SE` | Southeast | Flying toward southeast (~135°) |
| `S` | South | Flying toward south (~180°) |
| `SW` | Southwest | Flying toward southwest (~225°) |
| `W` | West | Flying toward west (~270°) |
| `NW` | Northwest | Flying toward northwest (~315°) |
| `--` | Unknown | The aircraft is not broadcasting its heading |

**Example:** A flight showing `SW` in the HDG column is heading toward southwestern Europe or the Mediterranean. A flight showing `NE` might be heading toward Russia or Central Asia.

> **Heading vs. position on radar:**  
> `HDG` tells you where the aircraft is **going**.  
> Where the `✈` icon sits on the radar circle tells you where the aircraft **is** relative to you.  
> These are two different things — a plane can be to your north while flying south (it already passed you and is heading away).

---

### Special Squawk Codes

These codes are internationally standardised emergency transponder codes. When squawk detects one, it highlights the aircraft row in **yellow**:

| Code | Display | Meaning |
|---|---|---|
| `7700` | `SQ:7700⚠` | **General emergency** — the aircraft has declared an emergency (engine failure, medical, fuel, etc.) |
| `7600` | `SQ:7600📻` | **Radio failure** — the aircraft has lost communication with ATC |
| `7500` | `SQ:7500🚨` | **Hijacking** — the aircraft is being hijacked |

All other codes (e.g., `SQ:2341`) are normal ATC-assigned codes with no special meaning visible to the public.

---

### Status Bar

```
  ● Son güncelleme: 03:12:35  ·  2 uçak            Sonraki: 18s
```

| Element | Meaning |
|---|---|
| `●` | Green = data loaded successfully |
| `◌` | Blinking = currently fetching from API |
| `⚠` | Yellow = last API request failed |
| `Son güncelleme: HH:MM:SS` | Timestamp of the last successful data refresh |
| `N uçak` | Number of aircraft currently detected within your radius |
| `Sonraki: Ns` | Countdown in seconds until the next API request |

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `R` | Force an immediate API refresh (don't wait for the 30s timer) |
| `Q` or `Esc` | Quit squawk |
| `Ctrl+C` | Graceful shutdown |

---

## Reconfiguring

Changed location? New OpenSky credentials? Run the setup wizard again:

```bash
dotnet run -- --setup
```

Or edit the config file directly:

**Windows:** `%APPDATA%\squawk\config.json`  
**macOS/Linux:** `~/.config/squawk/config.json`

```json
{
  "OpenSky": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TokenUrl": "https://auth.opensky-network.org/auth/realms/opensky-network/protocol/openid-connect/token"
  },
  "Location": {
    "Latitude": 39.9476,
    "Longitude": 32.6097,
    "Name": "Ankara"
  },
  "Radar": {
    "RadiusKm": 20,
    "RefreshSeconds": 30
  }
}
```

> **Tip:** You can also use environment variables to override any setting without touching the file, using the prefix `SQUAWK_`. For example: `SQUAWK_Radar__RadiusKm=50 dotnet run`

---

## Building a Single-File Binary

To get a standalone `squawk` executable you can run from anywhere without `dotnet run`:

**Windows (x64):**
```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o out/win
# Result: out/win/squawk.exe
```

**macOS (Apple Silicon):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o out/mac
# Result: out/mac/squawk
```

**macOS (Intel):**
```bash
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o out/mac-intel
```

**Linux (x64):**
```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o out/linux
```

After publishing, add the binary to your `PATH` to run it from anywhere as just `squawk`.

---

## Publishing to NuGet

This is how you publish a new version so anyone can `dotnet tool install -g squawk`.

### One-time setup

**1. Create a NuGet account**

Go to [nuget.org](https://www.nuget.org/) → Sign in with your GitHub or Microsoft account → **API Keys** → **Create** → name it `squawk-publish`, set **Glob pattern** to `squawk`, copy the key.

**2. Add the key to your GitHub repo**

GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**  
Name: `NUGET_API_KEY`, Value: the key you just copied.

**3. Fill in your details in `Squawk.csproj`**

Replace `YOUR_NAME` and `YOUR_GITHUB_USERNAME` with your actual name and GitHub username — or just leave them as `Cankat ABACI` and `cankatabaci` if you've already filled them in.

> **Check the package name!** Before publishing, search [nuget.org/packages/squawk](https://www.nuget.org/packages/squawk) to see if `squawk` is taken. If it is, change `<PackageId>` to something unique like `squawk-radar` or `squawk-atc`. The `<ToolCommandName>squawk</ToolCommandName>` can stay as `squawk` — that's the command users type in the terminal.

### Publishing (automated via GitHub Actions)

```bash
# 1. Commit all your changes
git add .
git commit -m "feat: first release"

# 2. Tag the release — this triggers the workflow
git tag v1.0.0
git push origin main --tags
```

That's it. The [GitHub Actions workflow](.github/workflows/release.yml) automatically:
1. Builds the NuGet package with version `1.0.0`
2. Pushes it to nuget.org
3. Creates a GitHub Release

### Publishing (manual)

```bash
dotnet pack src/Squawk/Squawk.csproj -c Release -p:Version=1.0.0 -o nupkg

dotnet nuget push nupkg/squawk.1.0.0.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Releasing a new version

```bash
# bump <Version> in Squawk.csproj to 1.0.1, then:
git add .
git commit -m "fix: something"
git tag v1.0.1
git push origin main --tags
```

---

## Security

| What | Where it lives | In the repo? |
|---|---|---|
| OpenSky Client ID & Secret | `%APPDATA%\squawk\config.json` | ❌ No |
| Your latitude & longitude | `%APPDATA%\squawk\config.json` | ❌ No |
| Default config template | `src/Squawk/appsettings.json` | ✅ Yes — all values are empty strings |
| Source code | `src/Squawk/` | ✅ Yes — no secrets anywhere |

The `appsettings.json` file that ships with the source code contains only empty placeholders. Your real credentials are saved to your OS user data folder, which is listed in `.gitignore`.

---

## How It Works

```
┌─────────────────────────────────────────────────────────┐
│                    Every 30 seconds                      │
│  1. Compute bounding box around your location           │
│  2. GET /api/states/all?lamin=...&lomin=...             │
│     with Bearer token (OAuth2 client_credentials)       │
│  3. Parse JSON state vectors                            │
│  4. Calculate Haversine distance for each aircraft      │
│  5. Filter to radius, sort by distance                  │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                    Every ~80ms (12 FPS)                  │
│  1. Advance sweep angle by 1.2°                         │
│     (360° / 30s = 12°/s = 1.2° per 80ms frame)         │
│  2. Draw to off-screen char buffer (clear → rings →     │
│     crosshairs → sweep trail → sweep line → aircraft)   │
│  3. Diff render: only write changed terminal cells      │
│     → no flicker, minimal I/O                           │
└─────────────────────────────────────────────────────────┘
```

**Key design choices:**

- **No external UI library** — raw ANSI escape codes for full control and minimal dependencies
- **Diff rendering** — the canvas compares current and previous frame; only changed characters are written to the terminal, eliminating flicker
- **Aspect ratio correction** — terminal character cells are roughly 2× taller than wide, so the vertical radar radius is set to half the horizontal radius to produce a visually circular display
- **Bearing → screen position** — aircraft are placed using `sin(bearing)` for X and `-cos(bearing)` for Y, which correctly maps geographic bearings (0° = North, clockwise) to screen coordinates (Y increases downward)

---

## License

MIT — do whatever you want with it.

---

<div align="center">
Made with ☕ and too much curiosity about the planes overhead.
</div>
