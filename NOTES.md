# Cantata AI mod notes (2026-10-29)
## Index
1. What was built (l.9)
2. Results stock vs mod (l.20)
3. Install / uninstall / rebuild (l.30)
4. Test harness + manual steps (l.40)
5. Not done / next (l.50)
6. Extended batch (stopped 10-29)
8. Campaign run 10-30: AI plays CH1-CH9 (partial, blocked)
9. Objective-aware run: objective kinds, per-mission table, diagnosis (10-30)
7. Resume 10-30: n=4-6 per arm, tuning verdict

## 1. What was built
BepInEx 5.4.23.6 win_x64 (official GitHub release) + Harmony plugin `plugin/Plugin.cs` -> `CantataAIMod.dll`. Game files untouched (BepInEx only adds files).
Patches (all toggle in `BepInEx/config/local.cantata.aimod.cfg`; `General.Enabled=false` = stock):
- FixCloseToDestruction: stock bug `MaxHealth - cur/max` made low-HP term ~1.0 for all. Now ceil(10*(1-cur/max))/10.
- Focus-fire / kill bonus: postfix on Scoring.GetAttackScore + GetAttackTargetScore: +0.25 if me+allies in r5 can kill target, +0.15*(1-hpFrac). Clamped 1.
- Wider alarm: prefix on Decisions.GetAllDecisionsForInteractable: rally radius hurt 6->10, target-sighted 4->7 (same SetPatrol logic, CHASE/AGGRESSIVE only).
- Faction flavour: cfg `Faction.Flavour` = `Name=killMul,alarmMul;...`. Default empty (1,1). Faction names seen in test save: Human, Machine, Environment, Neutral Faction. Not tuned (no data).
- Score-term CSV: `BepInEx/cantata_ai_terms.csv` (every 5th attack score: kind,round,faction,attacker,target,base,killTerm,hpFrac,final). Sample: `terms_sample.csv`.
- Incremental replanning: stock already does (only actor recomputed after acting). Not changed.
## 2. Results (harness, same save, 3 variants, timescale 8)
Save: campaign ch1 R13 autosave (P1 Human 10u/36hp vs P2 Machine 71u/486hp; + Shoal env, Nobody neutral). All players forced AI, run to round 30 or elimination. Raw: `harness_results.txt`.
| run | outcome |
|---|---|
| stock1 | r30 no elim; P1 7u/26hp, P2 69u/479hp |
| stock2 | r30 no elim; P1 8u/27hp, P2 70u/478hp |
| mod1 | P1 ELIMINATED r21; P2 70u/483hp |
| mod2 | P1 ELIMINATED r21; P2 70u/483hp |
Read: mod wins by r21 (stock stalls, P1 survives 17 rounds+), machine side loses fewer hp (483 vs ~478). n=2 each, one lopsided scenario, only P1(10 units) was hurt: weak evidence, not balance test. Mod likely more lethal for any faction (not per-faction yet).
## 3. Install / uninstall
Installed now in G=`C:\Program Files (x86)\Steam\steamapps\common\Cantata`: `BepInEx\`, `winhttp.dll`, `doorstop_config.ini`, `changelog.txt`; plugin in `BepInEx\plugins\CantataAIMod.dll`. Original dir listing: `install_before.txt`.
Uninstall: `powershell -File C:\re\cantata\mod\uninstall.ps1` (removes all above). Disable only: set `Enabled = false` in cfg, or delete plugin dll.
Rebuild: `cd plugin; dotnet build -c Release`, copy `bin\Release\net472\CantataAIMod.dll` to `G\BepInEx\plugins`.
## 4. Harness
Cfg `[Harness] Enabled/SavePath/MaxRounds/TimeScale/Tag`. On start (25 s) loads save with all players AI (LoadMatchContext), sets runInBackground (minimized game otherwise pauses), patches AIManager.GatherViableTilesForAIActing to all tiles, logs per-player units/hp, appends `BepInEx/harness_results.txt`, quits. Drivers: `run.sh <true|false> <tag>`, `batch.sh "true:modA false:stockA"` (hidden/minimized, ~4 min/run). Currently Harness.Enabled=false (game plays normally).
Manual: start game, play skirmish vs AI, compare feel; cfg Enabled toggles.
## 5. Not done
- Only one scenario; need balanced AI-vs-AI saves, more runs, per-faction tuning from data.
- Retreat threshold, CHASE/HOLD stance changes, Curiosity not touched.
## 6. Extended batch (stopped by user 10-29, not resumed today)
24-run batch (R5/R10/R13 saves x stock/mod x4) STOPPED after 2 runs. Finished: stock1_R5 and mod1_R5 both "stalled" at r7-8 (P1 5u/20hp, P2 70u/494hp, no change) = save R5 gives no fight in harness (inconclusive, same result both arms). No new evidence; section 2 numbers (n=2) stand. Per-faction tuning NOT done (no data).
Resume: `nohup bash batch.sh "false:stock1_R10 true:mod1_R10 ..."` (tag suffix after last _ picks save R5|R10|R13). Harness.Enabled is false again; game closed.

## 7. Resume 10-30 (hidden, MaxRounds 40, saves R10 + R13; R5 dropped = no fight)
| save | arm | n | P1 eliminated | elim round |
|---|---|---|---|---|
| R10 | stock | 4 | 0/4 (2x maxrounds, 1 stalled r18, 1 maxrounds P1 2u) | - |
| R10 | mod | 4 | 3/4 | 18,18,20 (1 stalled r12, P1 8u/25hp) |
| R13 | stock | 6 | 0/6 | - (P1 ends 7-8u/26-29hp) |
| R13 | mod | 6 | 6/6 | 21,21,22,22,22,20 (mean 21.3) |
Total: stock 0/10, mod 9/10. P2 (Machine) loss mod ~ same as stock (~480hp left either way).
Raw: harness_results.txt.
Caveat: 10 vs 71 units, one scenario family; only Human-vs-Machine fights (Environment/Neutral inert) -> no per-faction signal.
Tuning verdict: Faction.Flavour left EMPTY (defaults). Data cannot separate faction weights; no change = no regression. Defaults win 9/10.
Harness.Enabled set false (normal play). Game closed.

## 8. Campaign run 10-30 (AI on all players, scenario loaded fresh; hidden; Harness.Scenario=CHn)
Added to plugin: Harness.Scenario (NewCampaignMatchContext, all players AI), dialog auto-advance, resolution-screen exit. Saves untouched (backup mod\saves_backup; autosaves land in skirmish dir).
| mission | result | rounds | notes |
|---|---|---|---|
| CH1 | no win (maxrounds) | 60 | P1 Human 4u/22hp vs Machine 74u; zero change: AI never acted |
| CH2,3,6,9 | no result | - | game load/run exceeded batch timeout, no result line |
| CH4 | stalled | 1 | P1 12u vs Alien 141u + Shoal 69u |
| CH5 | no win (frozen) | 26+ | P1 Alien 2u vs 100u+; units frozen from r4 |
| CH7 | stalled | 1 | P1 21u vs 405u |
| CH8 | no win (maxrounds) | 60 | P1 Alien 6u/86hp vs Alien 30u + Shoal 261u; some combat |
Blockers: (1) campaign starts player tiny vs huge enemy = scripted objectives (escape/survive/reinforce), utility AI has no objective logic -> idles; (2) fog gate: AI only acts on tiles explored by focused local player; (3) intro triggers/dialogs stall r1 on CH4/CH7; (4) several missions load slower than batch timeout (600s). Beating the game needs objective-aware AI + trigger driving: not done. Harness.Enabled=false, game closed.

## 9. Objective-aware run (10-30, tag e_, all-AI P1, mod on, 60 rounds max, stall 240s)
Dumps: G\BepInEx\dump_CH1..9.txt (objectives, trigger cond/act, locations; written by Plugin.DumpNarr at match up).
Objective kinds seen: destroy named units (kill target), enter/see Location (reach), region TeamOwned (capture), "X must survive" (protect unit), TurnLimit (CH9 80t), CH8 Survive = unit health<=4 fail.
Most objectives en=False at start: toggled by trigger actions (dialogs). Hint locations in CH2 only (centroids 47,16 / 30,29 / 79,13 / 38,62).
| mission | start (P1 vs main foe) | objective types | result | rounds |
| CH1 | Human 4u vs Machine 74u | scout/see loc, destroy Heart x4, enter Dome, kill Hatchback | maxrounds, no change (AI idle) | 60 |
| CH2 | Alien 6u vs Human 192u | hint locs, destroy base bldgs | maxrounds, idle | 60 |
| CH3 | Robot 10u vs 51/114u | capture 5 Hands of God regions, 10 Crowns survive | maxrounds, idle | 60 |
| CH4 | Human 12u vs Alien 141u | kill scouts, see loc, kill Queen, save crew | stalled r1 | 1 |
| CH5 | Alien 2u vs 101u | reach guardians, survive He Carves, clear base | stalled r17 | 17 |
| CH6 | Robot 12u vs 165u | find cores, kill Samson, capture core, Vashti survive | stalled r1 | 1 |
| CH7 | Human 21u vs 405u | kill Shotar, capture 4 factories | stalled r1 | 1 |
| CH8 | Alien 6u vs 30u+261 Shoal | advance, protect Prophet, destroy Computer | stalled r1 | 1 |
| CH9 | Robot 14u vs 238u | Vashti/Ophanim survive, raze HQs, 80-turn limit | stalled r1 | 1 |
Diagnosis: (a) r1 stalls = Shoal(Environment, 37-261u) turn never finishes in 240s; P_Viable lifts fog for ALL players -> Shoal scans whole map. Fix idea: lift only for non-Environment, raise stall to 900s. (b) CH1-3 idle: no attack decision scores >=0.5, no objective-driven moves. Plan: periodically SetPatrol P1 units toward hint-location / objective tile (patrolling units always act).
Status: objective goals NOT implemented yet. Harness.Enabled must be set false at end.
