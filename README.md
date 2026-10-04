# AfterburnCAFE

**Afterburn: Combat Analytics For EVE**

Afterburn is a desktop application for reviewing EVE Online combat logs. It groups combat into estimated runs, shows damage and application statistics, and lets you manually record which site or Abyssal activity you entered.

Built with C#, .NET 9, Avalonia UI 11.3.21, and CommunityToolkit.Mvvm. Log discovery currently focuses on Windows Documents locations.

## Download for Windows

[Download AfterburnCAFE.exe (Windows x64)](https://github.com/thekazian/AfterburnCAFE/releases/latest/download/AfterburnCAFE.exe)

The download link becomes available when the first public GitHub Release containing `AfterburnCAFE.exe` is published. It does not point to a download yet.

The portable executable includes .NET; users do not need to install an SDK or runtime. Download and run it on 64-bit Windows. It is currently unsigned, so Windows may show a publisher or reputation prompt. Manual run markers are stored in your local application data, not alongside the executable.

## Build from source

Install the .NET 9 SDK or a compatible newer SDK. From the repository root:

```powershell
dotnet restore AfterburnCAFE.sln
dotnet build AfterburnCAFE.sln -c Release
dotnet run --project src/AfterburnCAFE
```

Open this folder in VS Code, or open `AfterburnCAFE.sln` in Visual Studio or Rider. The same commands work in VS Code's terminal. Microsoft's C# extension or C# Dev Kit provides C# editor and debugger support.

The project targets .NET 9 to match the initial development environment. Avalonia 11.3.21 is used for compatibility with that compiler. No Avalonia template installation is needed to build this repository.

## Review combat runs

On startup, Afterburn checks Windows' configured Documents folder, local Documents, and OneDrive Documents, including consumer and commercial OneDrive environment paths:

```text
%USERPROFILE%\Documents\EVE\logs\Gamelogs
%USERPROFILE%\OneDrive\Documents\EVE\logs\Gamelogs
```

Only top-level `.txt` files are scanned. Archived subfolders are excluded. Click **Refresh game logs** to rescan, including files still being written by EVE. There is no continuous file watcher.

Select a run in the left sidebar to explore:

- Encounter duration, estimated active combat time, damage dealt/taken, and average DPS.
- Peak incoming DPS over rolling one-, five-, and ten-second windows.
- Outgoing damage gaps, estimated downtime, target switches, and damage concentration.
- Damage totals and shares by weapon, target name, and incoming source.
- Incoming/outgoing hit-quality distributions, including zero-count buckets and attack denominators.
- Low-quality and high-quality rates, per-weapon breakdowns, and exploratory personal comparisons.
- First/last engagement times by opponent name and a cleaned event timeline.
- Data coverage and calculation limitations.

**Copy as Markdown**, above the detail panel, copies the selected run's summary and current tab to the clipboard.

## Mark a site or filament run

Click **Run marker** to open a movable, always-on-top window:

1. Select or type the exact pilot name from the game-log header.
2. Choose a combat site preset, enter a custom activity name, or select standard Abyssal weather and tier.
3. Optionally record a variant, fleet mode, ship/fit, or other notes.
4. Click **Mark entry now** when you enter and **Mark exit now** when you leave.

The clock uses the computer's UTC time; it does not read or synchronize with EVE's clock. Keep Windows time synchronized. An exit marker records leaving, not confirmed successful completion.

Markers are saved as separate JSON files under:

```text
%LOCALAPPDATA%\AfterburnCAFE\markers
```

Active entries survive application restarts. Each pilot can have one active entry. Closing the application does not automatically end it.

Manual markers annotate combat runs by pilot and time overlap. A run receives the activity name in its title only if one marker fully contains the combat interval. Partial overlaps remain annotations. Markers do not yet replace inferred combat boundaries or filter baseline comparisons. Entries without combat remain visible in the marker journal.

See [manual marker workflow and catalog scope](docs/manual-markers.md).

## How to interpret the numbers

EVE logs are text files containing session headers and timestamped messages with display markup. Afterburn strips that markup while retaining readable event messages.

Runs are currently segmented within each log file by system jumps or more than five minutes without combat. These are estimated combat intervals, not automatically identified site instances.

Active combat time uses the union of ten-second windows after recognized attacks, including misses. Damage downtime counts portions of outgoing no-damage gaps beyond a ten-second allowance. Both are heuristics, not measured weapon uptime.

Low-quality rate is **Miss + Graze + Glance Off**. High-quality rate is **Penetrate + Smash + Wreck**. Both use all recognized directional attacks as the denominator; unknown quality remains visible. No attacks produces N/A.

Personal comparisons use earlier loaded runs for the same pilot. Quality rates use per-run averages; other baseline metrics use medians. Sites, tiers, ships, fits, and weapon mixes are not matched. Diagnostic observations describe logged outcomes and do not establish causes such as range, transversal, or piloting.

Current limitations:

- No confirmed per-NPC time-to-kill: repeated NPC names are combined, and unique NPC IDs/kill evidence are not extracted.
- No explicit EM, thermal, kinetic, or explosive damage split.
- Drone classification currently recognizes Warrior II; its contribution is a lower bound.
- No automatic site/tier/ship/fit identification, completion detection, or clear-time prediction.
- All displayed event timestamps are UTC with whole-second log precision.

See [metric definitions, formulas, and diagnostic thresholds](docs/metrics.md).

## Data access and EVE policy

Afterburn reads ordinary game-log files and stores its own manual entries locally. It does not modify EVE logs, send gameplay inputs, inspect client memory, scrape the client cache, or intercept game network traffic. Logs and markers are not uploaded by the application; clipboard export is user initiated.

Afterburn is an independent project and is not endorsed by CCP. This design is not a guarantee of EULA compliance. Review [CCP's Third Party Policies](https://support.eveonline.com/hc/en-us/articles/8564030965660-Third-Party-Policies) and obtain policy clarification from CCP when needed, especially before adding new gameplay-facing features.

## Development and validation

```powershell
dotnet build AfterburnCAFE.sln -c Release
dotnet run --project tests/AfterburnCAFE.ParserChecks
```

The dependency-free check executable verifies parsing, damage totals, segmentation, timing/DPS calculations, quality buckets and baselines, plus marker persistence and matching. It also scans locally available logs for basic consistency. It creates temporary marker fixtures and removes them after the checks. It does not require real logs to pass and does not test the graphical UI or OS clipboard.

### Build the standalone Windows executable

```powershell
dotnet publish src/AfterburnCAFE/AfterburnCAFE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish/win-x64
```

Output: `artifacts/publish/win-x64/AfterburnCAFE.exe`. Native libraries are bundled and extracted by .NET at runtime. Trimming is disabled to preserve Avalonia's reflection-based functionality.

To make the download link live, publish a GitHub Release and attach this file with the exact asset name `AfterburnCAFE.exe`. Attach a SHA-256 checksum as well. Keep binaries in Releases rather than committing them into Git. The `/releases/latest/download/` link follows the latest full release; draft and prerelease uploads do not activate it.

```text
AfterburnCAFE.sln
src/AfterburnCAFE/
  Models/       Events, runs, metrics, hit quality, and manual markers
  Services/     Log discovery/parsing, Markdown export, and marker storage
  ViewModels/   MVVM state and commands
  Views/        Main run browser and floating marker window
  Assets/       Application resources
tests/AfterburnCAFE.ParserChecks/
docs/
```

Build output, IDE user files, and local environment files are excluded by `.gitignore`. Real game logs and marker data should remain outside the repository.
