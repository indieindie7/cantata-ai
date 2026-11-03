using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Cantata;
using Cantata.AI;
using HarmonyLib;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CantataAIMod
{
    [BepInPlugin("local.cantata.aimod", "Cantata AI Mod", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> Enabled, FixLowHp, KillBonusOn, AlarmOn, LogTerms;
        internal static ConfigEntry<float> KillBonus, LowHpBonus;
        internal static ConfigEntry<int> AlarmHurtRadius, AlarmTargetRadius;
        internal static ConfigEntry<string> FactionFlavour;
        internal static Dictionary<string, float[]> Flavour = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        internal static HashSet<string> SeenFactions = new HashSet<string>();
        internal static StreamWriter Csv;
        internal static int Calls;
        static float hAdv; internal static ConfigEntry<bool> HEnabled; internal static ConfigEntry<string> HSave, HTag, HScen; internal static ConfigEntry<int> HMaxRounds; internal static ConfigEntry<float> HScale;
        int hs; float hT, hLast; string hSig = ""; float hSigT; int hStartUnits;
        void Update() { if (HEnabled != null && HEnabled.Value) { try { Harness(); } catch (Exception e) { Logger.LogError("harness " + e); hs = 99; } } }

        void Awake()
        {
            Enabled = Config.Bind("General", "Enabled", true, "master switch (false = stock behaviour)");
            FixLowHp = Config.Bind("Fix", "FixCloseToDestruction", true, "fix precedence bug: low-HP target priority");
            KillBonusOn = Config.Bind("FocusFire", "KillBonusEnabled", true, "bonus when allies+me can kill target");
            KillBonus = Config.Bind("FocusFire", "KillBonus", 0.25f, "added to attack score when kill is secured (clamped 1)");
            LowHpBonus = Config.Bind("FocusFire", "WoundedBonus", 0.15f, "added x (1-hp fraction) to attack score");
            AlarmOn = Config.Bind("Alarm", "WiderAlarm", true, "wider alarm broadcast");
            AlarmHurtRadius = Config.Bind("Alarm", "HurtRadius", 10, "stock 6");
            AlarmTargetRadius = Config.Bind("Alarm", "TargetRadius", 7, "stock 4");
            FactionFlavour = Config.Bind("Faction", "Flavour", "", "faction=killMul,alarmMul;faction2=... Unknown faction = 1,1");
            LogTerms = Config.Bind("Debug", "LogScoreTerms", true, "CSV of score terms per decision");
            foreach (var kv in FactionFlavour.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = kv.Split('=');
                if (p.Length != 2) continue;
                var v = p[1].Split(',');
                float a = 1, b = 1;
                float.TryParse(v[0], out a);
                if (v.Length > 1) float.TryParse(v[1], out b);
                Flavour[p[0].Trim()] = new[] { a, b };
            }
            if (LogTerms.Value)
            {
                Csv = new StreamWriter(Path.Combine(Paths.BepInExRootPath, "cantata_ai_terms.csv"), true) { AutoFlush = true };
                Csv.WriteLine("kind,round,faction,attacker,target,base,killTerm,hpFrac,final");
            }
            HEnabled = Config.Bind("Harness", "Enabled", false, "auto-load a save with all players AI, log result, quit");
            HSave = Config.Bind("Harness", "SavePath", "", "full path .csave");
            HMaxRounds = Config.Bind("Harness", "MaxRounds", 30, "stop after N rounds");
            HScale = Config.Bind("Harness", "TimeScale", 8f, "Time.timeScale");
            HScen = Config.Bind("Harness", "Scenario", "", "bundled campaign scenario id (CH1..) instead of save");
            HTag = Config.Bind("Harness", "Tag", "run", "label in result file");
            Application.runInBackground = true;
            new Harmony("local.cantata.aimod").PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("loaded; enabled=" + Enabled.Value);
        }


        string Snap()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var pl in MatchManager.PlayerList)
            {
                int n = 0, hp = 0;
                foreach (var it in pl.OnMapInteractables) { n++; hp += it.CurrentHealth; }
                sb.Append(pl.PlayerName + "(" + pl.Faction + "):" + n + "u/" + hp + "hp" + (pl.Eliminated ? "X" : "") + " ");
            }
            return sb.ToString();
        }
        void Finish(string why)
        {
            string line = string.Format("{0}|{1}|mod={2}|round={3}|why={4}|{5}|ai={6}", DateTime.Now.ToString("s"), HTag.Value, Enabled.Value, MatchManager.RoundNumber, why, Snap(), "");
            File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "harness_results.txt"), line + Environment.NewLine);
            Logger.LogInfo("RESULT " + line);
            Time.timeScale = 1f; hs = 99; Application.Quit();
        }
        static void DumpNarr()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("== " + HScen.Value + " round " + MatchManager.RoundNumber);
            foreach (var pl in MatchManager.PlayerList) sb.AppendLine("player " + pl.ScenarioID + " " + pl.PlayerData.PlayerFactionType + " units=" + pl.Interactables.Count);
            foreach (var o in MatchManager.ObjectivesManager.Objectives)
            {
                sb.AppendLine("OBJ " + o.GetType().Name + " [" + o.ID + "] " + o.Name + " en=" + o.Enabled + " tgtP=" + o.TargetPlayerScenarioID + " :: " + o.ShortDesc);
                try { foreach (var l in o.Locations) sb.AppendLine("  hint loc " + l.Name + " tiles=" + l.Tiles.Count + " c=" + Ctr(l)); } catch { }
                try { if (o.Trigger != null) { var r = new TriggerData.TriggerReport(o.Trigger); sb.AppendLine("  COND " + r.Conditions); sb.AppendLine("  ACT " + r.Actions); } } catch (Exception e) { sb.AppendLine("  trig err " + e.Message); }
            }
            int n = 0;
            foreach (var tr in MatchManager.TriggerManager.Triggers)
            {
                if (n++ > 150) break;
                try { var r = new TriggerData.TriggerReport(tr); sb.AppendLine("TRG " + tr.Name + " tm=" + tr.Timing + " en=" + tr.Enabled + " obj=" + tr.ParentObjectiveID + " C:" + r.Conditions + " A:" + r.Actions); } catch { }
            }
            foreach (var l in MatchManager.LocationManager.Locations) sb.AppendLine("LOC " + l.Name + " g=" + l.Group + " tiles=" + l.Tiles.Count + " c=" + Ctr(l));
            File.WriteAllText(Path.Combine(Paths.GameRootPath, "BepInEx", "dump_" + HScen.Value + ".txt"), sb.ToString());
        }
        static float hGoal;
        // Fair belief layer: only what own units have seen. id -> (tile, round seen)
        static readonly Dictionary<Player, Dictionary<Interactable, (Tile t, int r)>> belief = new Dictionary<Player, Dictionary<Interactable, (Tile t, int r)>>();
        const int BeliefDecay = 20;
        static void Goals()
        {
            var map = MatchManager.CurrentMap; var rnd = new System.Random();
            foreach (var pl in MatchManager.PlayerList)
            {
                if (pl.Eliminated || pl.Faction.ToString() == "Environment") continue;
                var units = pl.OnMapInteractables.Where(u => u.CanMoveCheck).ToList();
                if (units.Count == 0) continue;
                if (!belief.TryGetValue(pl, out var bl)) belief[pl] = bl = new Dictionary<Interactable, (Tile, int)>();
                int rd = MatchManager.RoundNumber;
                foreach (var u in pl.OnMapInteractables)
                {
                    foreach (var e in u.LocalVisibleEnemyInteractables) if (e.CurrentTile != null) bl[e] = (e.CurrentTile, rd);
                }
                var vis = new HashSet<Tile>(); foreach (var u in pl.OnMapInteractables) vis.UnionWith(u.VisibleTiles);
                var seenNow = new HashSet<Interactable>(); foreach (var u in pl.OnMapInteractables) seenNow.UnionWith(u.LocalVisibleEnemyInteractables);
                foreach (var k in bl.Keys.ToList())
                {
                    var v = bl[k];
                    if (k == null || k.OffMap || rd - v.r > BeliefDecay || (!seenNow.Contains(k) && vis.Contains(v.t))) bl.Remove(k); // gone or decayed
                }
                Tile tgt = null; string why = "";
                var me = units[0].CurrentTile; int bd = int.MaxValue;
                foreach (var kv in bl) { int d = Math.Abs(kv.Value.t.X - me.X) + Math.Abs(kv.Value.t.Y - me.Y); if (d < bd) { bd = d; tgt = kv.Value.t; why = "believed enemy"; } }
                if (tgt == null)
                {
                    foreach (var o in MatchManager.ObjectivesManager.Objectives)
                    {
                        if (!o.Enabled || o.TargetPlayerScenarioID != pl.ScenarioID || o.Completed()) continue;
                        foreach (var l in o.Locations) { float x = 0, y = 0; int c = 0; foreach (var tl in l.Tiles) { x += tl.X; y += tl.Y; c++; } Tile ct; if (c > 0 && map.GetTileAtCoords((int)(x / c), (int)(y / c), out ct)) { tgt = ct; why = o.Name; break; } }
                        if (tgt != null) break;
                    }
                }
                if (tgt == null)
                {   // scout: random unexplored tile (belief empty -> infer enemy is where we have not looked)
                    var unk = map.Tiles.Where(t => !pl.ExploredTiles.Contains(t)).ToList();
                    if (unk.Count > 0) { tgt = unk[rnd.Next(unk.Count)]; why = "scout"; }
                }
                if (Csv != null && bl.Count > 0) Csv.WriteLine("BELIEF " + pl.Faction + " n=" + bl.Count + " why=" + why);
                if (tgt == null) continue;
                foreach (var u in units) { try { u.SetPatrol((tgt, null)); } catch { } }
                Debug.Log("[AIMod] goal P" + pl.ScenarioID + " -> " + tgt.X + "," + tgt.Y + " (" + why + ") units=" + units.Count);
            }
        }
        static string Ctr(LocationData l) { float x = 0, y = 0; int c = 0; foreach (var t in l.Tiles) { x += t.X; y += t.Y; c++; } return c == 0 ? "-" : (x / c).ToString("0") + "," + (y / c).ToString("0"); }
        void Harness()
        {
            hT += Time.unscaledDeltaTime;
            if (hs == 0 && hT > 25f && GameManager.ActiveScene != CScene.MatchMain)
            {
                if (!string.IsNullOrEmpty(HScen.Value))
                {
                    var sc = GameManager.ModManager.CoreMod.Scenarios.FirstOrDefault(x => x.ID == HScen.Value);
                    if (sc == null) { Logger.LogError("scenario not found " + HScen.Value); Finish("noscenario"); return; }
                    var sfi = new ScenarioFileInfo(sc, GameManager.ModManager.CoreMod);
                    var pl0 = new List<MatchPlayerData>(); int k = 0;
                    foreach (var d in sfi.ScenarioFileProbe.PlayerData) { ulong id = (ulong)(100 + 100 * k++); pl0.Add(new MatchPlayerData(id, id, d.ScenarioID, PlayerControlType.AI, MatchPlayerData.NetworkPlayerType.LOCAL, d.PlayerFactionType)); }
                    GameManager.LoadContext(new NewCampaignMatchContext(sfi, pl0)).Forget();
                    hs = 1; hT = 0; Logger.LogInfo("harness: campaign " + HScen.Value); return;
                }
                var fi = new FileInfo(HSave.Value);
                var mfi = new MatchFileInfo(fi);
                var list = new List<MatchPlayerData>();
                foreach (var d in mfi.MatchFileProbe.MatchState.PlayerData)
                    list.Add(new MatchPlayerData(d.PlayerID, d.OwnerID, d.ScenarioID, PlayerControlType.AI, d.NetworkPlayerType, d.PlayerFactionType));
                GameManager.LoadContext(new LoadMatchContext(mfi, list)).Forget();
                hs = 1; hT = 0; Logger.LogInfo("harness: loading " + fi.Name); return;
            }
            if (hs == 1 && GameManager.ActiveScene == CScene.MatchMain && MatchManager.PlayerList != null && MatchManager.PlayerList.Count > 0 && hT > 10f)
            { hs = 2; Time.timeScale = HScale.Value; hLast = hT; hSigT = hT; Logger.LogInfo("harness: match up " + Snap());
                foreach (var pl in MatchManager.PlayerList) { pl.PlayerData.PlayerControlType = PlayerControlType.AI; }
                var am = UnityEngine.Object.FindObjectOfType<AIManager>();
                Logger.LogInfo("harness: forced AI; AIManager=" + (am != null));
                try { DumpNarr(); } catch (Exception e) { Logger.LogWarning("dump " + e.Message); }
                if (am != null) { var mi = typeof(AIManager).GetMethod("OnPlayerTurnReady", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); foreach (var pl in MatchManager.PlayerList) if (pl.HasStartedTurn) mi.Invoke(am, new object[] { pl }); }
                return; }
            if (hs == 2 && hT - hAdv > 0.5f)
            {
                hAdv = hT;
                try { if (MatchManager.DialogManager != null && MatchManager.DialogManager.ActiveDialog) Events.DialogUI.AdvanceDialog?.Invoke(); } catch (Exception e) { Logger.LogWarning("adv " + e.Message); }
            }
            if (hs == 2 && hT - hGoal > 8f) { hGoal = hT; try { Goals(); } catch (Exception e) { Logger.LogWarning("goals " + e.Message); } }
            if (hs == 2 && hT - hLast > 3f)
            {
                hLast = hT;
                try { var am2 = UnityEngine.Object.FindObjectOfType<AIManager>(); var mi2 = typeof(AIManager).GetMethod("OnPlayerTurnReady", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); foreach (var pl in MatchManager.PlayerList) if (pl.HasStartedTurn && !pl.Eliminated) mi2.Invoke(am2, new object[] { pl }); } catch { }
                if (MatchManager.ShowingResolutionScreen) { Finish("resolution"); return; } string s = Snap() + "r" + MatchManager.RoundNumber;
                if (s != hSig) { hSig = s; hSigT = hT; Logger.LogInfo("harness: " + s); }
                foreach (var pl in MatchManager.PlayerList) if (pl.Eliminated) { Finish("eliminated"); return; }
                if (MatchManager.RoundNumber >= HMaxRounds.Value) { Finish("maxrounds"); return; }
                if (hT - hSigT > 900f) { Finish("stalled"); return; }
            }
            if (hs == 1 && hT > 180f) Finish("loadfail");
        }

        internal static float Mul(string faction, int idx)
        {
            if (faction == null) return 1f;
            if (SeenFactions.Add(faction)) Debug.Log("[AIMod] faction seen: " + faction);
            float[] f;
            return Flavour.TryGetValue(faction, out f) ? f[idx] : 1f;
        }
    }

    [HarmonyPatch(typeof(Scoring), nameof(Scoring.CloseToDestruction))]
    static class P_Close
    {
        static bool Prefix(Interactable i, ref float __result)
        {
            if (!Plugin.Enabled.Value || !Plugin.FixLowHp.Value) return true;
            float frac = i.MaxHealth <= 0 ? 0f : 1f - (float)i.CurrentHealth / i.MaxHealth;
            __result = Mathf.Clamp01(Mathf.Ceil(frac * 10f) / 10f);
            return false;
        }
    }

    static class AtkHelper
    {
        public static void Post(string kind, ref float score, CanAttack attacker, Interactable target)
        {
            if (!Plugin.Enabled.Value) return;
            float baseS = score, kill = 0f, hpf = 1f;
            try
            {
                string fac = attacker.Interactable.Owner.Faction;
                if (Plugin.KillBonusOn.Value && Scoring.AreOpponents(attacker.Interactable, target))
                {
                    kill = Scoring.CouldDestroyWithLocalThreats(attacker, target, 5);
                    hpf = target.MaxHealth <= 0 ? 1f : (float)target.CurrentHealth / target.MaxHealth;
                    float m = Plugin.Mul(fac, 0);
                    float bonus = (kill >= 0.999f ? Plugin.KillBonus.Value : 0f) + Plugin.LowHpBonus.Value * (1f - hpf);
                    score = Mathf.Min(1f, score + bonus * m);
                }
                if (Plugin.Csv != null && ++Plugin.Calls % 5 == 0)
                    Plugin.Csv.WriteLine(string.Format("{0},{1},{2},{3},{4},{5:F2},{6:F2},{7:F2},{8:F2}", kind, MatchManager.RoundNumber, fac, attacker.Interactable.name, target.name, baseS, kill, hpf, score));
            }
            catch (Exception e) { if (Plugin.Csv != null) Plugin.Csv.WriteLine("ERR " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Scoring), nameof(Scoring.GetAttackScore))]
    static class P_Atk
    {
        static void Postfix(bool __result, ref float score, CanAttack attacker, Interactable targetedInteractable)
        { if (__result) AtkHelper.Post("atk", ref score, attacker, targetedInteractable); }
    }

    [HarmonyPatch(typeof(Scoring), nameof(Scoring.GetAttackTargetScore))]
    static class P_Tgt
    {
        static void Postfix(bool __result, ref float score, CanAttack attacker, Interactable potentialTarget)
        { if (__result) AtkHelper.Post("tgt", ref score, attacker, potentialTarget); }
    }

    [HarmonyPatch(typeof(Decisions), nameof(Decisions.GetAllDecisionsForInteractable))]
    static class P_Alarm
    {
        static void Prefix(Interactable i)
        {
            if (!Plugin.Enabled.Value || !Plugin.AlarmOn.Value) return;
            try
            {
                float am = Plugin.Mul(i.Owner.Faction, 1);
                var rnd = new System.Random();
                var dmg = i.GetMostRecentDamage();
                if (dmg != null && dmg.Round >= MatchManager.RoundNumber - 2)
                {
                    var tiles = MatchManager.CurrentMap.GetTileAtIndex(dmg.AttackerTile).SurroundingTiles(4);
                    Rally(i, Mathf.RoundToInt(Plugin.AlarmHurtRadius.Value * am), tiles, rnd);
                }
                if (i.MoveAttackTarget != null && !i.MoveAttackTarget.OffMap)
                {
                    var tiles = i.MoveAttackTarget.CurrentTile.SurroundingTiles(3);
                    Rally(i, Mathf.RoundToInt(Plugin.AlarmTargetRadius.Value * am), tiles, rnd);
                }
            }
            catch (Exception e) { if (Plugin.Csv != null) Plugin.Csv.WriteLine("ERR alarm " + e.Message); }
        }

        static void Rally(Interactable i, int radius, List<Tile> tiles, System.Random rnd)
        {
            if (tiles == null || tiles.Count == 0) return;
            i.CurrentTile.IterateSurroundingTiles(radius, (Tile t) =>
            {
                var o = t.Occupant;
                if (o != null && o.Owner == i.Owner && o.CanMoveCheck && (o.AttackStance == AttackStance.CHASE || o.AttackStance == AttackStance.AGGRESSIVE))
                    o.SetPatrol((t: tiles[rnd.Next(0, tiles.Count)], patrolLocation: null));
            });
        }
    }
}

namespace CantataAIMod
{
    // Fairness: each AI player acts only on tiles ITS OWN units explored (no global fog lift).
    [HarmonyPatch(typeof(AIManager), "GatherViableTilesForAIActing")]
    static class P_Viable
    {
        static bool Prefix(ref HashSet<Tile> __result)
        {
            if (Plugin.HEnabled == null || !Plugin.HEnabled.Value) return true;
            var cp = MatchManager.CurrentPlayer;
            if (cp == null || cp.Faction.ToString() == "Environment") return true;
            __result = cp.ExploredTiles;
            return false;
        }
    }
}
