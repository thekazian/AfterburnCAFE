# Manual run markers

Open **Run marker** in the main window. The separate movable window stays on top while Afterburn is open. Close the marker window to hide it; saved and active entries persist. Closing Afterburn closes its marker window without silently ending active entries.

1. Select or type the exact pilot name used in the EVE game-log header.
2. Choose Combat site, Abyssal Deadspace, or Other / custom.
3. For standard Abyssal Deadspace, choose weather and tier. For other activities, choose a common site preset or enter an exact name. Variant / fleet mode and notes are optional.
4. Click **Mark entry now** as you enter, then **Mark exit now** as you leave. These record the click time, not the completion of a disk write. An exit means you marked leaving; it does not certify a successful completion.

The clock is `DateTime.UtcNow`, displayed as EVE time (UTC). It is not synchronized to CCP servers or read from the client. Keep Windows time synchronized; entry/exit precision also depends on when you click. No game inputs, memory access, client modifications, or network interception are used.

Entries are separate JSON files under `%LOCALAPPDATA%\AfterburnCAFE\markers`. Files are saved using a temporary file and rename. EVE logs are never modified. Each pilot can have one active entry; other pilots can be tracked separately. Reopening the window restores active entries so you can mark exit later.

Scanning annotates combat runs by exact pilot name (case-insensitive) and overlapping UTC intervals. A run gets a named title only when exactly one marker fully contains its combat interval. Partial or multiple overlaps appear as annotations without relabeling the full interval. These annotations do not yet replace combat segmentation or constrain personal baselines. Empty activities with no logged combat are visible in the marker journal, not the combat sidebar.

## Catalog scope

Standard Abyssal Deadspace has seven tiers (T0 Tranquil, T1 Calm, T2 Agitated, T3 Fierce, T4 Raging, T5 Chaotic, T6 Cataclysmic) and five weathers (Dark, Electrical, Exotic, Firestorm, Gamma): 35 tier/weather labels. Fleet mode is independent optional metadata, and the encountered NPC mix is not selected in advance. Special event filaments are recorded under Other / custom rather than forced into this standard catalog.

Combat sites use common full-name presets plus a free-text fallback. This is not an exhaustive catalog or an unrestricted faction/type cross-product. DED sites, missions, wormholes, events, and different spawn variants need their own names and context. For example, Angel Sanctum has Ring/Station variants; record that in the variant field if known.

Sources checked October 4, 2026:

- [EVE University: Abyssal Deadspace](https://wiki.eveuniversity.org/Abyssal_Deadspace)
- [EVE University: Angel Sanctum](https://wiki.eveuniversity.org/Angel_Sanctum)
- [EVE University: Combat anomaly overview](https://wiki.eveuniversity.org/Template:Combat_Anomalies)

The catalog can grow without changing timestamp storage. Existing entries retain the names you recorded.
