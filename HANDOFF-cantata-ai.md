# HANDOFF cantata-ai (Cantata BepInEx fair AI mod)
## Index (read this first, open one section by line range)
1. State - repo, commit, remote blocked, game state - lines 10-14
2. What the mod does - patches, fairness design, not built - lines 16-21
3. Results - fair 10 games per arm, retired old claim - lines 23-26
4. Paths - game dir, cfg, source, saves - lines 28-33
5. Commands - build, deploy, batch harness - lines 35-39
6. Left - push, campaign goals, director layer - lines 41-45

## 1 State
Repo C:\re\cantata\repo (branch main). Last mod commit f9ea0e0. Remote github.com/indieindie7/cantata-ai does NOT exist; push blocked by classifier (see 6).
Dev plugin source C:\re\cantata\mod\plugin\Plugin.cs (identical to repo plugin/Plugin.cs at f9ea0e0).
Cantata was reinstalled via Steam app 690370 into C:\Program Files (x86)\Steam\steamapps\common\Cantata (BepInEx kept). Game closed; cfg General Enabled=true, Harness Enabled=false.

## 2 What the mod does
Focus-fire score bonus, wider alarm radii, FixCloseToDestruction bug fix, Faction flavour cfg, score-term CSV, harness (all-AI skirmish, dialog auto-advance, MaxRounds, stall limit), Goals() with belief layer.
Fairness (user rule 11-03: AIs play fair, directors balance, like Alien Isolation): no global fog lift. P_Viable gives each AI its own ExploredTiles. Belief layer = last-seen enemies from currently visible tiles, decay 20 rounds, dropped when tile seen empty; Goals() patrols to believed enemy, then objective hint centroid, then scouts unexplored tiles.
Two stock fog leaks gated to visible tiles in harness runs (both arms): Scoring.HasThreatsAtTile, and the CHASE trigger (LocalExploredEnemyInteractables intersected with LocalVisibleEnemyInteractables in Decisions.GetAttackMoveDecisions). Attack targeting and GetLocalThreats were already visibility-gated.
Not built: director layer for difficulty knobs (must never touch perception or resources). Campaign objective goals untested.

## 3 Results (10 games per arm, ch1 R10 and R13 saves, P1 Human ~10u vs P2 Machine ~70u)
Stock: P1 eliminated 10/10, mean round 27.0. Mod: 10/10, mean round 21.6 (about 20% faster).
Old "9/10 vs 0/10" used the all-tiles fog lift: retired. Raw lines in harness_results.txt (g* mod, h* stock; earlier f*/s* ran before the leak gates). g5b gave no result, rerun as g6b.

## 4 Paths
G = C:\Program Files (x86)\Steam\steamapps\common\Cantata. Plugin dll G\BepInEx\plugins\CantataAIMod.dll. Cfg G\BepInEx\config\local.cantata.aimod.cfg (line 64 General Enabled, line 71 Harness Enabled). Results G\BepInEx\harness_results.txt.
Decompiled source (local only, never publish): C:\re\cantata\src\acs. NARRATIVE.md derived from it: not published.
Mod tools C:\re\cantata\mod (run.sh, batch.sh, NOTES.md, saves_backup). Saves: C:\Users\john\AppData\LocalLow\afterschool\cantata\saves\campaign.

## 5 Commands
Build: cd C:\re\cantata\mod\plugin; dotnet build -c Release; stop cantata.exe; copy bin\Release\*\CantataAIMod.dll to G\BepInEx\plugins (copy fails while game runs).
Batch (hidden, one instance only, 4-6 min per run): rm -f /c/re/cantata/mod/batch.done (absolute path); cd /c/re/cantata/mod; nohup bash batch.sh "true:g1a_R10 false:h1a_R10" &; wait until batch.done exists. Tag suffix _R5|_R10|_R13 picks the save. run.sh flips General Enabled: reset cfg line 64 to true and line 71 to false afterwards.
Find stray batch.sh: Get-CimInstance Win32_Process | ? CommandLine -match 'batch\.sh'; never kill in bulk.

## 6 Left
1 Push: in C:\re\cantata\repo run: gh repo create indieindie7/cantata-ai --public --source . --push (classifier denied it from the session; user/parent runs it).
2 Test campaign objective Goals() per chapter (Harness.Scenario=CHn, CH1..CH9); user said stop relaunching until told.
3 Optional director layer (difficulty knobs only).
4 Repo NOTES.md lacks the fair-AI details; this file and README hold them.
