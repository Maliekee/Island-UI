using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IslandUI
{
    // Does this Island UI know the game it runs in? The title screen says so when it does not.
    //
    // The game names its build in one place, the BuildDate text asset that vanilla's corner stamp
    // shows ("Build_" and the date). The release records the builds it was tested on in
    // Data/tested-game-builds.txt, embedded in the DLL; the release script writes the same file, so
    // the mod and the script read one source. Offline: nothing here goes near the network.
    //
    // Two notices, one plate, standing to the right of the mods' icons in the tip's dress:
    //   untested  this build is not one the release was tested on. It may well work: the words
    //             say "not tested", never "broken". Dismissed per build: the build the player
    //             hid it for goes into [Game version] DismissedBuild, and a new build brings it back.
    //   failed    a patch class did not bind (Shared/PatchCensus.cs): part of a mod is not working.
    //             The stronger notice, and it cannot be dismissed. It covers Island Ex and Island AI
    //             too, read from the BepInEx log their census writes to, so neither needs to know
    //             about this plate.
    // Both carry the badge menu's own Check for updates line when the update can take one
    // (ModUpdate.CheckLine): a press opens the usual check, nothing is fetched by itself.
    //
    // Only while Island UI runs: switched off, it patches nothing and draws only its icon.
    internal static class GameVersionNotice
    {
        private const string PlateName = "IslandGameVersionNotice";
        private const string TestedResource = "IslandUI.tested-game-builds.txt";
        private const float Width = 620f;

        // the badge tip's dress (Shared/TitleBadge.cs), and a stripe that says which notice
        private static readonly Color32 Ink = new Color32(0x28, 0x1A, 0x10, 0xFF);
        private static readonly Color32 DarkPaper = new Color32(0x3A, 0x2A, 0x1C, 0xFF);
        private static readonly Color32 Cream = new Color32(0xEA, 0xD4, 0xB0, 0xFF);
        private static readonly Color32 Wheat = new Color32(0xCE, 0xA0, 0x64, 0xFF);
        private static readonly Color32 Coral = new Color32(0xEE, 0x54, 0x40, 0xFF);
        private static readonly Color32 Button = new Color32(0x55, 0x3E, 0x2A, 0xFF);
        private static readonly Color32 ButtonHover = new Color32(0x6E, 0x52, 0x38, 0xFF);

        internal enum Preview { Off, Untested, PatchFailed }

        private static ConfigEntry<string> _dismissed;
        private static ConfigEntry<Preview> _preview;
        private static PatchCensusLine _own;
        private static string _build;
        private static List<string> _tested;
        private static bool _hidden;        // dismissed in this session, before the scene changes
        private static GameObject _plate;

        // one mod's census, as this start found it
        internal sealed class PatchCensusLine
        {
            internal string Mod;
            internal int Failed;
        }

        internal static void Bind(ConfigFile config)
        {
            _dismissed = config.Bind("Game version", "DismissedBuild", "",
                "The game build whose 'not tested on this version of the game' notice was hidden on the title screen. "
                + "The notice stays away until the game's build changes. Empty: not hidden.");
            _preview = config.Bind("Game version", "PreviewNotice", Preview.Off,
                "Shows the title-screen notice whatever the game's build is, to see how it looks: Untested, or PatchFailed. Off: only when it is true.");
        }

        // in Awake, after the census: what Island UI's own patches did, and the build line the
        // release script reads back from LogOutput.log
        internal static void Install(Shared.PatchCensus.Result census, ManualLogSource log)
        {
            _own = new PatchCensusLine
            {
                Mod = Plugin.PluginName,
                Failed = census != null ? census.Failed.Count + census.Unattributed.Count : 0,
            };
            _build = ReadBuild();
            _tested = ReadTested();
            log.LogInfo("Game build: " + (_build.Length > 0 ? _build : "unknown") + " - "
                + (IsTested() ? "tested with this " + Plugin.PluginName
                    : "not among the builds this " + Plugin.PluginName + " was tested on (" + string.Join(", ", _tested.ToArray()) + ")"));
            SceneManager.sceneLoaded += OnSceneLoaded;
            Show(SceneManager.GetActiveScene());
        }

        // hot reload: the next generation draws its own
        internal static void Remove()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_plate != null)
            {
                UnityEngine.Object.Destroy(_plate);
            }
            _plate = null;
        }

        // The game's build as its title screen shows it after "Build_". Empty: the game no longer
        // has the asset, which is itself a sign of a different game.
        internal static string ReadBuild()
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>("BuildDate");
                return asset != null ? Normalise(asset.text) : "";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Game build: could not read it: " + e.Message);
                return "";
            }
        }

        // vanilla drops the first character and shows the rest; the same here, without the
        // line break or spaces a text asset may carry
        private static string Normalise(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return (text.Length > 1 ? text.Substring(1) : text).Trim();
        }

        private static List<string> ReadTested()
        {
            var builds = new List<string>();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TestedResource))
            {
                if (stream == null)
                {
                    return builds;
                }
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0 && !line.StartsWith("#"))
                        {
                            builds.Add(line);
                        }
                    }
                }
            }
            return builds;
        }

        // what DismissedBuild holds for this build; a game with no build asset is "unknown"
        private static string BuildKey
        {
            get { return _build.Length > 0 ? _build : "unknown"; }
        }

        private static bool IsTested()
        {
            return _build.Length > 0 && _tested.Contains(_build);
        }

        // For the harness and a bug report: what the notice would say right now, in one line.
        internal static string Describe()
        {
            var parts = new List<string>();
            foreach (PatchCensusLine line in Census())
            {
                parts.Add(line.Mod + " " + line.Failed);
            }
            return "build=" + _build + " tested=" + IsTested() + " (" + string.Join(", ", _tested.ToArray()) + ")"
                + " dismissed=" + (_dismissed != null ? _dismissed.Value : "") + " preview=" + (_preview != null ? _preview.Value.ToString() : "")
                + " census=[" + string.Join(", ", parts.ToArray()) + "] shows=" + Which();
        }

        // "failed", "untested" or "" for none
        private static string Which()
        {
            Preview preview = _preview != null ? _preview.Value : Preview.Off;
            if (preview == Preview.PatchFailed || Failures().Count > 0)
            {
                return "failed";
            }
            if (preview == Preview.Untested)
            {
                return _hidden ? "" : "untested";
            }
            if (IsTested() || _hidden || (_dismissed != null && _dismissed.Value == BuildKey))
            {
                return "";
            }
            return "untested";
        }

        // the mods with a patch class that did not bind; a preview counts two for Island UI
        private static List<PatchCensusLine> Failures()
        {
            var failed = new List<PatchCensusLine>();
            foreach (PatchCensusLine line in Census())
            {
                if (line.Failed > 0)
                {
                    failed.Add(line);
                }
            }
            if (failed.Count == 0 && _preview != null && _preview.Value == Preview.PatchFailed)
            {
                failed.Add(new PatchCensusLine { Mod = Plugin.PluginName, Failed = 2 });
            }
            return failed;
        }

        // Island UI's own census, and the others' from the log. Their copies of PatchCensus write
        // "Patch census [IslandEx]: ..." to the shared BepInEx log; the last line per assembly is
        // what this start found. Island UI's own lines are skipped: a hot reload adds one per
        // generation, and the live one is _own.
        private static List<PatchCensusLine> Census()
        {
            var lines = new List<PatchCensusLine>();
            if (_own != null)
            {
                lines.Add(_own);
            }
            var others = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (string text in LogLines())
            {
                Match m = Regex.Match(text, @"Patch census \[([^\]]+)\]:(.*)$");
                if (!m.Success)
                {
                    continue;
                }
                string assembly = m.Groups[1].Value;
                if (assembly.StartsWith("IslandUI", StringComparison.Ordinal))
                {
                    continue;
                }
                string rest = m.Groups[2].Value;
                int failed = Count(rest, @"(\d+) class\(es\) FAILED") + Count(rest, @"(\d+) class\(es\) SKIPPED");
                if (!others.ContainsKey(assembly))
                {
                    order.Add(assembly);
                }
                others[assembly] = failed;
            }
            foreach (string assembly in order)
            {
                lines.Add(new PatchCensusLine { Mod = Pretty(assembly), Failed = others[assembly] });
            }
            return lines;
        }

        private static int Count(string text, string pattern)
        {
            Match m = Regex.Match(text, pattern);
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static string Pretty(string assembly)
        {
            switch (assembly)
            {
                case "IslandEx": return "Island Ex";
                case "IslandAI": return "Island AI";
                default: return assembly;
            }
        }

        // This start's BepInEx log, flushed first: the disk listener writes it on a timer. A
        // second game running at once writes LogOutput.log.1 and on, so the newest file is ours.
        // No log on disk (a player who turned it off): Island UI's own census still counts.
        private static IEnumerable<string> LogLines()
        {
            var lines = new List<string>();
            try
            {
                foreach (ILogListener listener in BepInEx.Logging.Logger.Listeners)
                {
                    var disk = listener as DiskLogListener;
                    if (disk != null && disk.LogWriter != null)
                    {
                        disk.LogWriter.Flush();
                    }
                }
                FileInfo newest = null;
                foreach (FileInfo file in new DirectoryInfo(Paths.BepInExRootPath).GetFiles("LogOutput.log*"))
                {
                    if (newest == null || file.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                    {
                        newest = file;
                    }
                }
                if (newest == null)
                {
                    return lines;
                }
                using (var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf("Patch census [", StringComparison.Ordinal) >= 0)
                        {
                            lines.Add(line);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Game build notice: could not read the log: " + e.Message);
            }
            return lines;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Show(scene);
        }

        private static void Show(Scene scene)
        {
            if (!scene.IsValid() || !scene.name.StartsWith("title", StringComparison.OrdinalIgnoreCase))
            {
                _hidden = false;
                return;
            }
            try
            {
                Draw();
            }
            catch (Exception e)
            {
                // a notice that fails must never cost the player the mod
                Plugin.Log.LogWarning("Game build notice: " + e.Message);
            }
        }

        private static void Draw()
        {
            if (_plate != null)
            {
                UnityEngine.Object.Destroy(_plate);
                _plate = null;
            }
            string which = Which();
            if (which.Length == 0)
            {
                return;
            }
            Transform canvas = Shared.TitleBadge.Canvas();
            if (canvas == null || canvas.childCount == 0)
            {
                Plugin.Log.LogWarning("Game build notice: no badge row on the title screen to stand beside");
                return;
            }
            bool failed = which == "failed";
            string build = _build.Length > 0 ? _build : Shared.Loc.Get("gamever.build_unknown", "unknown");

            string heading, body;
            if (failed)
            {
                var names = new List<string>();
                foreach (PatchCensusLine line in Failures())
                {
                    names.Add(Shared.Loc.Plural("gamever.failed_part", "{1}: {0} part", "{1}: {0} parts", line.Failed, line.Mod));
                }
                heading = Shared.Loc.Get("gamever.failed_title", "Some parts did not load on this version of the game");
                body = Shared.Loc.Get("gamever.failed_body",
                    "{0}. Those parts are not working; everything else is. A newer version of the mod may fit this game.",
                    string.Join(Shared.Loc.Separator, names.ToArray()));
            }
            else
            {
                heading = Shared.Loc.Get("gamever.untested_title", "{0} {1} has not been tested on this version of the game",
                    Plugin.PluginName, Plugin.PluginVersion);
                body = Shared.Loc.Get("gamever.untested_body",
                    "It may work fine. If something looks wrong, a newer version of the mod may help.");
            }
            body += "\n" + Shared.Loc.Get("gamever.build", "Game build: {0}", build);

            // inside the row, right of the last icon whatever number of mods is there, and out of
            // its layout; the badge menus open upward from the icons, so they never cover it
            Transform row = canvas.GetChild(IndexOfRow(canvas));
            _plate = new GameObject(PlateName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter), typeof(LayoutElement));
            _plate.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)_plate.transform;
            rect.SetParent(row, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(14f, 0f);
            Image rim = _plate.GetComponent<Image>();
            rim.color = Ink;
            rim.raycastTarget = false;
            HorizontalLayoutGroup outer = _plate.GetComponent<HorizontalLayoutGroup>();
            outer.padding = new RectOffset(2, 2, 2, 2);
            outer.spacing = 0f;
            outer.childControlWidth = outer.childControlHeight = true;
            outer.childForceExpandWidth = false;
            outer.childForceExpandHeight = true;
            ContentSizeFitter fit = _plate.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // the stripe: wheat for untested, coral for a part that did not attach
            var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            stripe.transform.SetParent(_plate.transform, false);
            Image stripeImage = stripe.GetComponent<Image>();
            stripeImage.color = failed ? Coral : Wheat;
            stripeImage.raycastTarget = false;
            stripe.GetComponent<LayoutElement>().preferredWidth = 6f;

            var paper = new GameObject("Paper", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            paper.transform.SetParent(_plate.transform, false);
            paper.GetComponent<LayoutElement>().preferredWidth = Width;
            Image paperImage = paper.GetComponent<Image>();
            paperImage.color = DarkPaper;
            paperImage.raycastTarget = false;
            VerticalLayoutGroup column = paper.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(12, 12, 8, 10);
            column.spacing = 4f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            Label(paper.transform, heading, failed ? Coral : Wheat, 19);
            Label(paper.transform, body, Cream, 17);

            var buttons = new List<Shared.TitleBadge.Item>();
            Shared.TitleBadge.Item check = Shared.ModUpdate.CheckLine();
            if (check != null)
            {
                buttons.Add(check);
            }
            if (!failed)
            {
                buttons.Add(new Shared.TitleBadge.Item(Shared.Loc.Get("gamever.dismiss", "Hide until the game updates"), Dismiss));
            }
            if (buttons.Count > 0)
            {
                var bar = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                bar.transform.SetParent(paper.transform, false);
                HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.padding = new RectOffset(0, 0, 4, 0);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                foreach (Shared.TitleBadge.Item item in buttons)
                {
                    ButtonFor(bar.transform, item);
                }
            }
        }

        // the row is the canvas child with the layout group; a menu's blocker may sit before it
        private static int IndexOfRow(Transform canvas)
        {
            for (int i = 0; i < canvas.childCount; i++)
            {
                if (canvas.GetChild(i).GetComponent<HorizontalLayoutGroup>() != null)
                {
                    return i;
                }
            }
            return 0;
        }

        // hidden for this build: written down, and gone from the screen now
        private static void Dismiss()
        {
            _hidden = true;
            if (_dismissed != null)
            {
                _dismissed.Value = BuildKey;
            }
            Plugin.Log.LogInfo("Game build notice: hidden for build " + BuildKey + " until the game's build changes.");
            if (_plate != null)
            {
                UnityEngine.Object.Destroy(_plate);
                _plate = null;
            }
        }

        private static Text Label(Transform parent, string words, Color32 colour, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Shared.TitleBadge.TitleFont();
            text.fontSize = size;
            text.lineSpacing = 1.1f;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = colour;
            text.raycastTarget = false;
            text.text = words;
            return text;
        }

        // a square button in the badge menu's colours; the press runs the line's own action
        private static void ButtonFor(Transform parent, Shared.TitleBadge.Item item)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = Button;
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 5, 5);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Text label = Label(go.transform, item.Label, Cream, 17);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            Listen(trigger, EventTriggerType.PointerEnter, () => image.color = ButtonHover);
            Listen(trigger, EventTriggerType.PointerExit, () => image.color = Button);
            Action act = item.Act;
            Listen(trigger, EventTriggerType.PointerClick, act);
        }

        private static void Listen(EventTrigger trigger, EventTriggerType type, Action act)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ =>
            {
                try { act(); }
                catch (Exception e) { Plugin.Log.LogWarning("Game build notice: " + e.Message); }
            });
            trigger.triggers.Add(entry);
        }
    }
}
