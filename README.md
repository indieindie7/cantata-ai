# cantata-ai

BepInEx 5 + Harmony mod that makes the skirmish/campaign AI in **Cantata** (Steam) stronger. Source only: no game files, no decompiled code, no game DLLs.

## What it does
- Fixes a stock bug in `CloseToDestruction` (low-HP term was ~1.0 for every unit).
- Focus-fire bonus: attack score +0.25 if the unit and allies within 5 tiles can kill the target, +0.15*(1-hp fraction).
- Wider alarm radii (rally 6->10, target sighted 4->7).
- Optional per-faction flavour (`Faction.Flavour` in cfg; empty = defaults).
- Test harness: loads a save or a campaign scenario with every player on AI, runs N rounds hidden, logs unit/hp per player to `BepInEx/harness_results.txt`.
- Score-term CSV (`cantata_ai_terms.csv`) for tuning.

## Result (skirmish, AI vs AI, same saves, n=10 per arm)
Stock AI eliminated the 10-unit side 0/10 times; with the mod 9/10 (mean round ~21). See `NOTES.md`, raw data `harness_results.txt`. One scenario family (Human vs Machine), so no per-faction conclusions.

## Install
1. Install BepInEx 5.4.23.6 win_x64 into the Cantata folder (https://github.com/BepInEx/BepInEx/releases).
2. Build: `cd plugin; dotnet build -c Release -p:CantataDir="<Cantata folder>"` (references the game's own DLLs from there).
3. Copy `plugin/bin/Release/net472/CantataAIMod.dll` to `BepInEx/plugins/`. Close the game first.
4. Disable: `General.Enabled = false` in `BepInEx/config/local.cantata.aimod.cfg`. Uninstall: `tools/uninstall.ps1` (edit the path).

## Campaign status
**Not beaten yet.** The AI plays skirmishes well, but the all-AI campaign run (CH1-CH9, harness) stalls or idles on most missions: it does not pursue mission objectives.

## Planned: story objectives
Idea: hook the AI into the game's narrative/objective system instead of only fighting what it sees.
- **Objective goals** in the utility AI: read the active objectives and their hint zones; reach/escape -> move units to the zone, survive/protect -> hold, kill target -> focus that unit, capture -> take the region.
- **Dialog auto-advance** so story popups never block an AI-driven run (done in the harness).
- **Fairness (Alien Isolation style)**: the AI plays fair. No global fog lift: each AI player acts only on tiles its own units explored. A belief layer keeps last-seen enemy positions (decay 20 rounds, dropped when the tile is re-seen empty), patrols toward the nearest believed enemy, then objectives, then scouts unexplored tiles. Any difficulty balancing belongs in a separate director layer that must never touch perception or resources (not built yet).
- **Reinforcement/survive goals** for timed missions (CH9 has an 80-turn limit; CH8 fails if a protected unit drops to <=4 hp).

State: a first version is in `Plugin.cs` (`DumpNarr` objective dump, `Goals()` sets patrol targets per objective every 8 s, Environment skip). It builds, but it is **untested**: the CH1-CH9 re-run was stopped. Partial runs: CH1 ran 60 rounds with no change, CH3 reached the resolution screen at round 4, CH4/CH7 still stalled at round 1.
Left: verify per mission with the harness (`tools/batch.sh`), tune goal selection (most objectives start disabled and are toggled by trigger actions), then try a full campaign playthrough.

## Fair-AI result (no fog lift, per-player explored tiles, belief layer)
Skirmish, 10 games per arm (5 each from the ch1 R10 and R13 saves; P1 Human 10u vs P2 Machine ~70u):
- Stock AI: P1 eliminated 10/10, mean round 26.7.
- Mod AI: P1 eliminated 10/10, mean round 21.3 (20% faster).
The earlier "9/10 vs 0/10" came from the old all-tiles fog lift and is retired. With fair perception the 7:1 side wins either way; the mod's gain is speed, not outcome. Raw lines: harness_results.txt (tags f* = mod, s* = stock).
