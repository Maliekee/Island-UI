using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IslandUI.UI
{
    // WHAT A WINDOW'S FIRST OPEN COSTS, measured rather than guessed: the frame a
    // window comes up in, its first time against the times after, and every frame
    // over HitchMs with what opened in it and how many objects Island UI built.
    // The answer decides what is worth warming up after a load (WarmPool's way)
    // and what is the game's own to pay.
    //
    // Watched: every GameObject the game's UIManager holds (its panels, the menu,
    // the map, the talk panel) and Island UI's own window roots. An open is seen
    // the frame after it happens or the frame it happens, depending on where in
    // the frame the SetActive fell, so its cost is the longer of those two frames.
    //
    // A dev measurement: armed only when the test harness is (a -Harness launch),
    // otherwise it reads nothing and logs nothing. The log lines start "FirstOpen:";
    // the harness reads the table with `game.ps1 invoke IslandUI.UI.FirstOpenTimer Report`.
    internal static class FirstOpenTimer
    {
        private const string HarnessName = "Island Test Harness";
        private const float HitchMs = 50f;

        private sealed class Watch
        {
            public string Name;
            public GameObject Go;
            public bool On;
        }

        private sealed class Pending
        {
            public string Name;
            public bool First;
            public float Ms;
            public int Built;
        }

        private sealed class Record
        {
            public float First = -1f;
            public float Worst;   // the worst open after the first
            public float Total;   // ...and their sum, for the mean
            public int Again;
        }

        // our own roots, by field: private statics of the window classes
        private static readonly string[][] OwnRoots =
        {
            new[] { "IslandUI.InventoryWindow", "_root", "inventory" },
            new[] { "IslandUI.InventoryWindow", "_reachPanel", "vicinity" },
            new[] { "IslandUI.SettingsWindow", "_root", "settings window" },
            new[] { "IslandUI.BrowserShell", "_root", "item browser" },
            new[] { "IslandUI.ActionBar", "_root", "action bar" },
            new[] { "IslandUI.ManageFan", "_root", "manage fan" },
            new[] { "IslandUI.TalkBubble", "_root", "talk bubble" },
            new[] { "IslandUI.UI.SplitDialog", "_root", "split dialog" },
            new[] { "IslandUI.UI.DevConsole", "_root", "dev console" },
        };

        private static bool? _armed;
        private static UIManager _ui;
        private static readonly List<Watch> Watches = new List<Watch>();
        private static readonly List<Pending> Waiting = new List<Pending>();
        private static readonly Dictionary<string, Record> Records = new Dictionary<string, Record>();
        private static readonly List<string> OpenedNow = new List<string>();
        private static readonly List<string> OpenedLast = new List<string>();
        private static float _typical = 16f;
        private static int _builtAt, _builtLast;
        private static int _gcAt;
        private static float _ownRootsAt = float.NegativeInfinity;

        internal static void Tick(ManagersScript mn)
        {
            if (_armed == null)
            {
                _armed = false;
                foreach (BepInEx.PluginInfo info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                {
                    // an unarmed harness destroys itself in Awake: its Instance reads null
                    if (info.Metadata.Name == HarnessName && info.Instance != null) { _armed = true; }
                }
                if (_armed.Value) { Plugin.Log.LogInfo("FirstOpen: armed (harness launch), hitches over " + HitchMs + " ms"); }
                _builtAt = Owned.Created;
                _gcAt = System.GC.CollectionCount(0);
            }
            if (!_armed.Value) { return; }

            // the frame that just ended
            float ms = Time.unscaledDeltaTime * 1000f;
            int built = Owned.Created - _builtAt;
            _builtAt = Owned.Created;
            int gc = System.GC.CollectionCount(0) - _gcAt;
            _gcAt += gc;

            for (int i = 0; i < Waiting.Count; i++)
            {
                Finish(Waiting[i], Mathf.Max(Waiting[i].Ms, ms), Waiting[i].Built + built);
            }
            Waiting.Clear();

            OpenedLast.Clear();
            OpenedLast.AddRange(OpenedNow);
            OpenedNow.Clear();
            Scan(mn, ms, built);

            if (ms >= HitchMs)
            {
                var sb = new StringBuilder("FirstOpen: hitch ").Append(ms.ToString("0")).Append(" ms (frame ")
                    .Append(Time.frameCount).Append(", typical ").Append(_typical.ToString("0")).Append(" ms)");
                if (built + _builtLast > 0) { sb.Append(", Island UI built ").Append(built + _builtLast).Append(" objects"); }
                if (gc > 0) { sb.Append(", GC"); }
                if (OpenedNow.Count + OpenedLast.Count > 0)
                {
                    sb.Append(", opened: ").Append(string.Join(", ", OpenedLast.ToArray()));
                    if (OpenedLast.Count > 0 && OpenedNow.Count > 0) { sb.Append(", "); }
                    sb.Append(string.Join(", ", OpenedNow.ToArray()));
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
            else
            {
                _typical = Mathf.Lerp(_typical, ms, 0.05f);
            }
            _builtLast = built;
        }

        private static void Scan(ManagersScript mn, float ms, int built)
        {
            UIManager ui = mn != null ? mn.uiMN : null;
            if (!ReferenceEquals(ui, _ui))
            {
                _ui = ui;
                Watches.Clear();
                _ownRootsAt = float.NegativeInfinity;
                if (ui != null)
                {
                    foreach (FieldInfo f in typeof(UIManager).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType != typeof(GameObject)) { continue; }
                        GameObject go = f.GetValue(ui) as GameObject;
                        if (go != null) { Watches.Add(new Watch { Name = "game " + f.Name, Go = go, On = go.activeInHierarchy }); }
                    }
                }
            }
            // ours are built lazily: look for the ones not found yet once a second
            if (Time.unscaledTime - _ownRootsAt >= 1f)
            {
                _ownRootsAt = Time.unscaledTime;
                FindOwnRoots();
            }
            for (int i = 0; i < Watches.Count; i++)
            {
                Watch w = Watches[i];
                bool on = w.Go != null && w.Go.activeInHierarchy;
                if (on && !w.On)
                {
                    OpenedNow.Add(w.Name);
                    Waiting.Add(new Pending { Name = w.Name, First = !Records.ContainsKey(w.Name), Ms = ms, Built = built });
                    if (!Records.ContainsKey(w.Name)) { Records[w.Name] = new Record(); }
                }
                w.On = on;
            }
        }

        private static void FindOwnRoots()
        {
            Assembly asm = typeof(FirstOpenTimer).Assembly;
            for (int i = 0; i < OwnRoots.Length; i++)
            {
                string name = "ours " + OwnRoots[i][2];
                Watch existing = Watches.Find(w => w.Name == name);
                if (existing != null && existing.Go != null) { continue; }
                System.Type type = asm.GetType(OwnRoots[i][0]);
                FieldInfo f = type != null ? type.GetField(OwnRoots[i][1], BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) : null;
                Object value = f != null ? f.GetValue(null) as Object : null;
                GameObject go = value as GameObject ?? (value is Component c ? c.gameObject : null);
                if (go == null) { continue; }
                if (existing != null) { Watches.Remove(existing); }
                // a root that is up when first found was opened before we looked: its
                // first open is the frame it was built in, which the hitch line caught
                Watches.Add(new Watch { Name = name, Go = go, On = go.activeInHierarchy });
            }
        }

        private static void Finish(Pending p, float ms, int built)
        {
            Record r = Records[p.Name];
            if (p.First)
            {
                r.First = ms;
                Plugin.Log.LogInfo("FirstOpen: " + p.Name + " first open " + ms.ToString("0") + " ms"
                    + (built > 0 ? ", Island UI built " + built + " objects" : ""));
                return;
            }
            r.Again++;
            r.Total += ms;
            r.Worst = Mathf.Max(r.Worst, ms);
            if (r.Again == 1)
            {
                Plugin.Log.LogInfo("FirstOpen: " + p.Name + " again " + ms.ToString("0") + " ms (first was " + r.First.ToString("0") + ")");
            }
        }

        // A STOPWATCH FOR A PIECE OF WORK, for finding which part of a slow frame is
        // ours: Now() at the start, Span() at the end, logged when it took MinSpanMs
        // or more. Unarmed, Now() is 0 and Span() returns at once; a caller that
        // builds its name tests for the 0 first, so an unarmed game builds nothing.
        private const float MinSpanMs = 5f;

        internal static long Now() => _armed == true ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

        internal static void Span(string what, long start)
        {
            if (start == 0L) { return; }
            float ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            if (ms >= MinSpanMs) { Plugin.Log.LogInfo("FirstOpen: span " + what + " " + ms.ToString("0.0") + " ms (frame " + Time.frameCount + ")"); }
        }

        // harness: every window opened this session, first open against the rest
        internal static string Report()
        {
            if (_armed != true) { return "not armed (launch with -Harness)"; }
            var names = new List<string>(Records.Keys);
            names.Sort((a, b) => Records[b].First.CompareTo(Records[a].First));
            var sb = new StringBuilder("typical frame ").Append(_typical.ToString("0")).Append(" ms\n");
            foreach (string n in names)
            {
                Record r = Records[n];
                sb.Append(r.First.ToString("0").PadLeft(5)).Append(" ms first");
                if (r.Again > 0)
                {
                    sb.Append(", then mean ").Append((r.Total / r.Again).ToString("0")).Append(" worst ").Append(r.Worst.ToString("0"))
                        .Append(" over ").Append(r.Again);
                }
                sb.Append("  ").Append(n).Append("\n");
            }
            return sb.ToString();
        }
    }
}
