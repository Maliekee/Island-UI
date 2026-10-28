using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // WHAT A STORY CHOICE LEADS TO: the quest choices carry the implications of the
    // choices you make in the main story, with the dramatic part behind an extra
    // click to reveal.
    //
    // The hints are written down - Data/story-choices.tsv, from the game's own code
    // (what each option's branch does) and the community wiki's event page (where the
    // story goes) - and keyed by CALL SITE: the k-th MultipleChoice / YesNoForStory
    // call inside a StoryManager coroutine, "Kana02#2". A transpiler marks each call
    // as it happens. The line spoken before a choice was the first idea for a key and
    // is not one: a choice can follow a different line on another path (the Hunter's
    // options come after a defeat, the source's previous line is the victory's).
    //
    // An option is matched by its text, back to its index in TextManager.choices
    // ("XXXX" stands for a name), not by its position: some choices add an option
    // only when you carry an item or have someone following.
    //
    // A translation, Localization/<lang>/story-choices.tsv, rewords the English rows;
    // the option column is what a scene prompt's hint is headed with, so it is the game's
    // own prompt in that language (Forgejo #19).
    internal static class StoryChoices
    {
        private sealed class Hint
        {
            public string Option, Plain, Spoiler;
        }

        private const string Resource = "IslandUI.story-choices.tsv";
        private const string DataName = "story-choices.tsv";
        private static Dictionary<string, Dictionary<string, Hint>> _hints;
        private static readonly Dictionary<string, int> Marked = new Dictionary<string, int>();

        // the site whose choice is up, set by the marked call and dropped when it closes;
        // the scene prompt likewise, which every ControlTextActive call re-marks - a
        // hiding call's site has no hints, so the prompt's hints go with it
        private static string _site;
        private static string _prompt;
        private static string _shownKey;
        // THE HINTS BEHIND A CLICK, on HintMode.OnClick (the default): a player who wants
        // the story blind sees one "Hints" bar where the column would stand, and the hints
        // only once it is clicked - for that choice or prompt alone. A choice and a scene
        // prompt are one thing to the player, so one setting covers both; the spoilers
        // inside a hint are always masked, whatever the setting.
        // The site the bar was clicked for; the frame of the click, which no scene reads.
        private static string _opened;
        private static int _openedFrame = -1;
        // the bar's tip says the setting exists: a new player meets the bar before the settings
        private static KitTooltip _tip;

        // ---------- the data ----------

        // read again when the language changes: the sites and options are the English
        // file's, their words Localization/<lang>/story-choices.tsv's (Forgejo #19)
        private static int _generation = -1;

        private static Dictionary<string, Dictionary<string, Hint>> Hints
        {
            get
            {
                if (_hints == null || _generation != Shared.Loc.Generation)
                {
                    _generation = Shared.Loc.Generation;
                    _hints = Load();
                }
                return _hints;
            }
        }

        private static Dictionary<string, Dictionary<string, Hint>> Load()
        {
            var hints = new Dictionary<string, Dictionary<string, Hint>>();
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource))
            {
                if (stream == null)
                {
                    Plugin.Log.LogWarning("Story choices: " + Resource + " is not in the assembly");
                    return hints;
                }
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    Read(reader, hints, true);
                }
            }
            foreach (string path in Shared.Loc.DataFiles(DataName))
            {
                try
                {
                    using (var reader = new StreamReader(path, Encoding.UTF8))
                    {
                        Read(reader, hints, false);
                    }
                }
                catch (IOException e)
                {
                    Plugin.Log.LogWarning("Story choices: could not read " + path + " - " + e.Message);
                }
            }
            return hints;
        }

        // `site <TAB> choice id <TAB> option <TAB> plain <TAB> spoiler`. The English makes
        // the sites; a translation only rewords them, so a row the English lacks is ignored
        // and a spoiler stays where the English has one.
        private static void Read(TextReader reader, Dictionary<string, Dictionary<string, Hint>> hints, bool english)
        {
            string row;
            while ((row = reader.ReadLine()) != null)
            {
                if (row.Length == 0 || row[0] == '#')
                {
                    continue;
                }
                string[] f = row.Split('\t');
                if (f.Length < 4)
                {
                    continue;
                }
                string spoiler = f.Length > 4 ? f[4] : "";
                if (english)
                {
                    if (!hints.TryGetValue(f[0], out var site))
                    {
                        hints[f[0]] = site = new Dictionary<string, Hint>();
                    }
                    site[f[1]] = new Hint
                    {
                        Option = Shared.Loc.Untranslated(f[2]),
                        Plain = Shared.Loc.Untranslated(f[3]),
                        Spoiler = spoiler.Length > 0 ? Shared.Loc.Untranslated(spoiler) : "",
                    };
                }
                else if (hints.TryGetValue(f[0], out var site) && site.TryGetValue(f[1], out Hint hint))
                {
                    hint.Option = f[2];
                    hint.Plain = f[3];
                    if (hint.Spoiler.Length > 0 && spoiler.Length > 0)
                    {
                        hint.Spoiler = spoiler;
                    }
                }
            }
        }

        // ---------- marking the call sites ----------

        // Manual, one coroutine at a time: a story method that is renamed or removed in
        // a game update costs its own hints, never the patch census of the rest.
        internal static void Install(Harmony harmony)
        {
            var methods = new HashSet<string>();
            foreach (string site in Hints.Keys)
            {
                methods.Add(site.Substring(0, site.IndexOfAny(new[] { '#', '!' })));
            }
            int patched = 0;
            foreach (string name in methods)
            {
                try
                {
                    MethodInfo coroutine = AccessTools.Method(typeof(StoryManager), name);
                    MethodInfo moveNext = coroutine != null ? AccessTools.EnumeratorMoveNext(coroutine) : null;
                    if (moveNext == null)
                    {
                        Plugin.Log.LogWarning("Story choices: no coroutine StoryManager." + name);
                        continue;
                    }
                    harmony.Patch(moveNext, transpiler: new HarmonyMethod(typeof(StoryChoices), nameof(Transpiler)));
                    patched++;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("Story choices: could not mark " + name + " - " + e.Message);
                }
            }
            // a count that disagrees with the data means the game moved its choices:
            // the hints of that coroutine would land on the wrong call
            var expected = new Dictionary<string, int>();
            foreach (string site in Hints.Keys)
            {
                if (site.IndexOf('#') < 0) { continue; }   // a scene prompt: no count to keep
                string name = site.Substring(0, site.IndexOf('#'));
                int k = int.Parse(site.Substring(site.IndexOf('#') + 1));
                expected[name] = Mathf.Max(expected.TryGetValue(name, out int e) ? e : 0, k);
            }
            var off = new List<string>();
            foreach (var pair in expected)
            {
                if (!Marked.TryGetValue(pair.Key, out int found) || found != pair.Value)
                {
                    off.Add(pair.Key + " " + (Marked.TryGetValue(pair.Key, out int f) ? f : 0) + "/" + pair.Value);
                }
            }
            Plugin.Log.LogInfo("Story choices: " + Hints.Count + " choices in " + patched + " coroutines"
                + (off.Count > 0 ? " - COUNTS DIFFER: " + string.Join(", ", off.ToArray()) : ", every count as written"));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            string name = CoroutineName(__originalMethod);
            MethodInfo multi = AccessTools.Method(typeof(UIManager), nameof(UIManager.MultipleChoice));
            MethodInfo yesNo = AccessTools.Method(typeof(UIManager), nameof(UIManager.YesNoForStory));
            MethodInfo mark = AccessTools.Method(typeof(StoryChoices), nameof(Mark));
            MethodInfo control = AccessTools.Method(typeof(UIManager), nameof(UIManager.ControlTextActive));
            MethodInfo markPrompt = AccessTools.Method(typeof(StoryChoices), nameof(MarkPrompt));
            MethodInfo mouse = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButtonDown), new[] { typeof(int) });
            MethodInfo mouseDown = AccessTools.Method(typeof(StoryChoices), nameof(MouseDown));
            int k = 0, j = 0;
            foreach (CodeInstruction ins in instructions)
            {
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
                    && ins.operand is MethodInfo called && (called == multi || called == yesNo))
                {
                    // the call's own arguments are on the stack already; this pair
                    // pushes and consumes one string of its own, so it is neutral
                    k++;
                    yield return new CodeInstruction(OpCodes.Ldstr, name + "#" + k);
                    yield return new CodeInstruction(OpCodes.Call, mark);
                }
                // A SCENE PROMPT (an interaction that displays no prompt of its own but
                // runs a scripted scene): the
                // Giant's and Shino's "[Left-Click] Leave. [F] Kill." with keys the
                // coroutine polls itself. Counted apart, as coroutine!k.
                else if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
                    && ins.operand is MethodInfo shown && shown == control)
                {
                    j++;
                    yield return new CodeInstruction(OpCodes.Ldstr, name + "!" + j);
                    yield return new CodeInstruction(OpCodes.Call, markPrompt);
                }
                // the scene's own "[Left-Click]" poll, read through MouseDown: same
                // signature, so only the operand changes and the labels stay on it
                else if (ins.opcode == OpCodes.Call && ins.operand is MethodInfo polled && polled == mouse)
                {
                    ins.operand = mouseDown;
                }
                yield return ins;
            }
            Marked[name] = k;
        }

        // "<Kana02>d__231" -> "Kana02"
        private static string CoroutineName(MethodBase moveNext)
        {
            string type = moveNext.DeclaringType != null ? moveNext.DeclaringType.Name : "";
            int open = type.IndexOf('<'), close = type.IndexOf('>');
            return open >= 0 && close > open ? type.Substring(open + 1, close - open - 1) : type;
        }

        public static void Mark(string site)
        {
            _site = site;
        }

        public static void MarkPrompt(string site)
        {
            _prompt = site;
        }

        // A SCENE THAT POLLS THE MOUSE ITSELF (clicking "click to reveal" ruined the
        // scene): the Giant's and Shino's
        // "[Left-Click] Leave." is the coroutine's own Input.GetMouseButtonDown(0), which
        // no UI button swallows - the click that revealed a spoiler also left. The
        // marked coroutines read the mouse through here, and a press on a hint is ours.
        public static bool MouseDown(int button)
        {
            return Input.GetMouseButtonDown(button) && !OverHint() && Time.frameCount != _openedFrame;
        }

        private static bool Guarded(string site)
        {
            return Settings.StoryHints.Value == HintMode.OnClick && _opened != site;
        }

        private static bool OverHint()
        {
            foreach (Widget w in Widgets)
            {
                if (w.Root == null || !w.Root.gameObject.activeInHierarchy) { continue; }
                Canvas canvas = w.Root.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera : null;
                if (RectTransformUtility.RectangleContainsScreenPoint(w.Root, Input.mousePosition, cam)) { return true; }
            }
            return false;
        }

        // ---------- on screen ----------

        private sealed class Widget
        {
            public RectTransform Root;
            public Image Back;
            public TextMeshProUGUI Plain;
            public Button Mask;
            public TextMeshProUGUI MaskText;
            public TextMeshProUGUI Spoiler;
            public bool Revealed;
            public bool Guard;             // the "Hints" bar, standing in for the hints
            public RectTransform Anchor;   // the game's button this hint belongs to
            public bool Below;             // yes/no: under the button, not beside it
        }

        private const float Width = 175f;   // a hint names what happens now and later
        private const float Pad = 4f;
        private static readonly List<Widget> Widgets = new List<Widget>();
        private static readonly Color Ground = new Color(0.10f, 0.075f, 0.05f, 0.92f);
        private static readonly Color MaskGround = new Color(0.03f, 0.02f, 0.015f, 0.95f);
        private static readonly Color SpoilerInk = new Color32(236, 150, 130, 255);

        internal static void Tick(ManagersScript mn)
        {
            UIManager ui = mn != null ? mn.uiMN : null;
            if (ui == null || mn.story == null)
            {
                Hide();
                return;
            }
            bool multi = ui.choicesPanel != null && ui.choicesPanel.activeInHierarchy;
            bool yesNo = !multi && ui.yesNoPanel != null && ui.yesNoPanel.activeInHierarchy;
            Dictionary<string, Hint> site = null;
            if (multi || yesNo)
            {
                if (mn.story.commandState != -1 || Settings.StoryHints.Value == HintMode.Off
                    || _site == null || !Hints.TryGetValue(_site, out site))
                {
                    Hide();
                    return;
                }
                // the generation: a language switch while the choice is up words it again
                bool guarded = Guarded(_site);
                string key = _site + "/" + (multi ? CountActive(ui) : 2) + "/" + _generation + (guarded ? "/guard" : "");
                if (key != _shownKey)
                {
                    Build(mn, ui, site, multi);
                    if (guarded) { GuardUp(_site); }
                    _shownKey = key;
                }
                Place();
                TickTip();
                return;
            }
            // the choice is answered or gone: the next one is a new mark
            _site = null;
            bool prompt = ui.controlDescText != null && ui.controlDescText.gameObject.activeInHierarchy;
            if (prompt && Settings.StoryHints.Value != HintMode.Off && _prompt != null && Hints.TryGetValue(_prompt, out site))
            {
                bool guarded = Guarded(_prompt);
                string key = _prompt + "/" + _generation + (guarded ? "/guard" : "");
                if (key != _shownKey)
                {
                    BuildPrompt(mn, ui, site);
                    if (guarded) { GuardUp(_prompt); }
                    _shownKey = key;
                }
                Place();
                TickTip();
                return;
            }
            // nothing is up: the next choice starts behind the bar again
            _opened = null;
            Hide();
        }

        // The built hints give way to one bar in the first hint's place: a choice the data
        // has no hint for shows no bar either. Its click opens the site, and the next Tick
        // builds the hints in the column the bar stood at, spoilers still masked.
        private static void GuardUp(string site)
        {
            if (Widgets.Count == 0 || Widgets[0].Root == null)
            {
                return;
            }
            Transform canvas = Widgets[0].Root.parent;
            RectTransform anchor = Widgets[0].Anchor;
            bool below = Widgets[0].Below;
            foreach (Widget hint in Widgets)
            {
                if (hint.Root != null) { hint.Root.gameObject.SetActive(false); }   // destroyed at the frame's end
            }
            Clear();

            var w = new Widget { Anchor = anchor, Below = below, Guard = true };
            GameObject go = Owned.New("IslandUI_StoryHintGuard");
            go.transform.SetParent(canvas, false);
            w.Root = (RectTransform)go.transform;
            w.Root.anchorMin = w.Root.anchorMax = new Vector2(0.5f, 0.5f);
            w.Root.pivot = new Vector2(0f, 1f);
            w.Back = go.AddComponent<Image>();
            w.Back.color = MaskGround;
            w.Mask = go.AddComponent<Button>();
            float h = 18f;
            w.MaskText = Theme.MakeText(go.transform, "Label", Theme.FontCaption, Theme.TextDim, TextAlignmentOptions.Center);
            w.MaskText.text = Shared.Loc.Get("story.show_hints", "Hints - click to show");
            Theme.Place(w.MaskText, 0f, 0f, Width, h);
            w.Root.sizeDelta = new Vector2(Width, h);
            w.Mask.onClick.AddListener(() =>
            {
                _opened = site;
                _openedFrame = Time.frameCount;
            });
            Widgets.Add(w);
        }

        private static void TickTip()
        {
            Widget bar = Widgets.Count == 1 && Widgets[0].Guard ? Widgets[0] : null;
            Canvas canvas = bar != null && bar.Root != null && bar.Root.gameObject.activeInHierarchy
                ? bar.Root.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                _tip?.Hide();
                return;
            }
            Camera cam = canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (_tip == null)
            {
                _tip = KitTooltip.Create((RectTransform)canvas.rootCanvas.transform);
            }
            bool over = RectTransformUtility.RectangleContainsScreenPoint(bar.Root, Input.mousePosition, cam);
            _tip.Tick(over ? "<b>" + Shared.Loc.Get("settings.story.hints", "Story hints") + "</b>\n" + Shared.Loc.Get("story.show_hints.tip", "What each option leads to waits behind this bar. To show the hints at once, or switch them off, see the Story page of the settings.") : null, cam);
        }

        // one hint per input, in the data's order, beside the control text
        private static void BuildPrompt(ManagersScript mn, UIManager ui, Dictionary<string, Hint> site)
        {
            Clear();
            if (!Theme.Ensure(mn, mn.inventory))
            {
                return;
            }
            var anchor = (RectTransform)ui.controlDescText.transform;
            foreach (Hint hint in site.Values)
            {
                Widgets.Add(Make(anchor.parent, hint, anchor, false, hint.Option));
            }
        }

        private static int CountActive(UIManager ui)
        {
            int n = 0;
            for (int i = 0; i < ui.choicesText.Length; i++)
            {
                if (ui.choicesText[i] != null && ui.choicesText[i].transform.parent.gameObject.activeSelf) { n++; }
            }
            return n;
        }

        private static void Build(ManagersScript mn, UIManager ui, Dictionary<string, Hint> site, bool multi)
        {
            Clear();
            if (!Theme.Ensure(mn, mn.inventory))
            {
                return;
            }
            Transform canvas = (multi ? ui.choicesPanel : ui.yesNoPanel).transform.parent;
            if (multi)
            {
                for (int i = 0; i < ui.choicesText.Length; i++)
                {
                    TextMeshProUGUI label = ui.choicesText[i];
                    if (label == null || !label.transform.parent.gameObject.activeSelf) { continue; }
                    string id = ChoiceId(mn, label.text, site);
                    if (id != null && site.TryGetValue(id, out Hint hint))
                    {
                        Widgets.Add(Make(canvas, hint, (RectTransform)label.transform.parent, false, label.text));
                    }
                }
            }
            else
            {
                Transform row = ui.yesNoPanel.transform.Find("HorizonPanel");
                Transform yes = row != null ? row.Find("YesButton") : null;
                Transform no = row != null ? row.Find("NoButton") : null;
                if (yes != null && site.TryGetValue("yes", out Hint y)) { Widgets.Add(Make(canvas, y, (RectTransform)yes, true, ButtonText(yes, "Yes"))); }
                if (no != null && site.TryGetValue("no", out Hint n)) { Widgets.Add(Make(canvas, n, (RectTransform)no, true, ButtonText(no, "No"))); }
            }
        }

        private static string ButtonText(Transform button, string fallback)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            return text != null && !string.IsNullOrEmpty(text.text) ? text.text : fallback;
        }

        // The option's index in TextManager.choices, read back from what the button says.
        // A text can stand in the table twice (English "Yes." is 20 and 33, and Shino's
        // choice uses 33): the index this site has a hint for wins over the first one.
        private static string ChoiceId(ManagersScript mn, string shown, Dictionary<string, Hint> site)
        {
            string[] table = mn.textMN != null ? mn.textMN.choices : null;
            if (table == null || string.IsNullOrEmpty(shown))
            {
                return null;
            }
            string first = null;
            for (int i = 0; i < table.Length; i++)
            {
                bool match = table[i] == shown
                    || (table[i] != null && table[i].Contains("XXXX")
                        && Regex.IsMatch(shown, "^" + Regex.Escape(table[i]).Replace("XXXX", ".+") + "$"));
                if (!match) { continue; }
                string id = i.ToString();
                if (site.ContainsKey(id)) { return id; }
                if (first == null) { first = id; }
            }
            return first;
        }

        // One hint, headed by the option it belongs to: the hints stand in a column
        // beside the panel rather than each beside its button, because a hint is
        // taller than the gap between two buttons and neighbours would overlap.
        private static Widget Make(Transform canvas, Hint hint, RectTransform anchor, bool below, string option)
        {
            var w = new Widget { Anchor = anchor, Below = below };
            GameObject go = Owned.New("IslandUI_StoryHint");
            go.transform.SetParent(canvas, false);
            w.Root = (RectTransform)go.transform;
            w.Root.anchorMin = w.Root.anchorMax = new Vector2(0.5f, 0.5f);
            w.Root.pivot = new Vector2(0f, 1f);
            w.Back = go.AddComponent<Image>();
            w.Back.color = Ground;
            w.Back.raycastTarget = false;

            float inner = Width - 2f * Pad;
            float y = Pad;
            TextMeshProUGUI head = Theme.MakeText(go.transform, "Option", Theme.FontCaption, Theme.AccentGold, TextAlignmentOptions.TopLeft);
            head.enableWordWrapping = true;
            head.fontStyle = FontStyles.Bold;
            head.text = option;
            float hh = Mathf.Max(8f, head.GetPreferredValues(option, inner, 0f).y);
            Theme.Place(head, Pad, y, inner, hh);
            y += hh + 1f;

            w.Plain = Theme.MakeText(go.transform, "Plain", Theme.FontCaption, Theme.TextCream, TextAlignmentOptions.TopLeft);
            w.Plain.enableWordWrapping = true;
            w.Plain.text = hint.Plain;
            float h = Mathf.Max(8f, w.Plain.GetPreferredValues(hint.Plain, inner, 0f).y);
            Theme.Place(w.Plain, Pad, y, inner, h);
            y += h;

            if (!string.IsNullOrEmpty(hint.Spoiler))
            {
                y += 2f;
                w.Spoiler = Theme.MakeText(go.transform, "Spoiler", Theme.FontCaption, SpoilerInk, TextAlignmentOptions.TopLeft);
                w.Spoiler.enableWordWrapping = true;
                w.Spoiler.text = hint.Spoiler;
                float sh = Mathf.Max(8f, w.Spoiler.GetPreferredValues(hint.Spoiler, inner, 0f).y);
                Theme.Place(w.Spoiler, Pad, y, inner, sh);
                // NOT DRAWN until the click, rather than drawn under a cover: the
                // text renders above the cover whatever the sibling order (the
                // first build showed the spoiler straight through it). The bar is
                // the spoiler's own size, so revealing moves nothing.
                w.Spoiler.gameObject.SetActive(false);
                GameObject maskGo = Owned.New("SpoilerMask");
                maskGo.transform.SetParent(go.transform, false);
                Image mask = maskGo.AddComponent<Image>();
                mask.color = MaskGround;
                Theme.Place(mask, Pad, y, inner, sh);
                w.Mask = maskGo.AddComponent<Button>();
                w.MaskText = Theme.MakeText(maskGo.transform, "Label", Theme.FontCaption, Theme.TextDim, TextAlignmentOptions.Center);
                w.MaskText.text = Shared.Loc.Get("story.spoiler", "Spoiler - click to reveal");
                Theme.Place(w.MaskText, 0f, 0f, inner, sh);
                Widget captured = w;
                w.Mask.onClick.AddListener(() =>
                {
                    captured.Revealed = true;
                    captured.Mask.gameObject.SetActive(false);
                    captured.Spoiler.gameObject.SetActive(true);
                });
                y += sh;
            }
            w.Root.sizeDelta = new Vector2(Width, y + Pad);
            return w;
        }

        // A column: beside the choice panel from its first button's top, or under a
        // yes/no row from its left; each hint under the one before. Kept on screen
        // (the panel sits low, and two hints ran past the bottom): lifted when it would run off the
        // bottom, moved to the panel's left when it would run off the right. Re-placed
        // each frame, since the panel lays its buttons out after it is shown.
        private static void Place()
        {
            if (Widgets.Count == 0 || Widgets[0].Anchor == null || Widgets[0].Root == null)
            {
                return;
            }
            var corners = new Vector3[4];
            Widgets[0].Anchor.GetWorldCorners(corners);   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            float unit = Widgets[0].Root.lossyScale.y;
            float total = -3f;
            foreach (Widget w in Widgets)
            {
                if (w.Root != null) { total += w.Root.sizeDelta.y + 3f; }
            }
            Vector3 at = Widgets[0].Below
                ? new Vector3(corners[0].x, corners[0].y - 4f * unit, corners[0].z)
                : new Vector3(corners[2].x + 6f * unit, corners[2].y, corners[2].z);
            var screen = new Vector3[4];
            ((RectTransform)Widgets[0].Root.parent).GetWorldCorners(screen);
            float margin = 6f * unit;
            if (at.x + Width * unit > screen[2].x - margin)
            {
                at.x = corners[0].x - (6f + Width) * unit;   // the panel's left instead
            }
            float bottom = at.y - total * unit;
            if (bottom < screen[0].y + margin)
            {
                at.y += screen[0].y + margin - bottom;
            }
            at.y = Mathf.Min(at.y, screen[1].y - margin);
            foreach (Widget w in Widgets)
            {
                if (w.Root == null) { continue; }
                w.Root.position = at;
                w.Root.SetAsLastSibling();
                at.y -= (w.Root.sizeDelta.y + 3f) * unit;
            }
        }

        private static void Hide()
        {
            if (Widgets.Count > 0)
            {
                Clear();
            }
            _tip?.Hide();
            _shownKey = null;
        }

        private static void Clear()
        {
            foreach (Widget w in Widgets)
            {
                if (w.Root != null) { Object.Destroy(w.Root.gameObject); }
            }
            Widgets.Clear();
        }

        // A hot reload in the middle of a choice: the mark was made by the retired
        // generation, and the next Awake would not know which choice is up
        private const string HandoverKey = "IslandUI.StoryChoices.site";

        internal static void HandOver()
        {
            System.AppDomain.CurrentDomain.SetData(HandoverKey, new[] { _site, _prompt });
        }

        internal static void TakeOver()
        {
            string[] marks = System.AppDomain.CurrentDomain.GetData(HandoverKey) as string[];
            System.AppDomain.CurrentDomain.SetData(HandoverKey, null);
            _site = marks != null ? marks[0] : null;
            _prompt = marks != null && marks.Length > 1 ? marks[1] : null;
        }

        internal static void ResetForSceneLoad()
        {
            Widgets.Clear();
            _site = null;
            _prompt = null;
            _shownKey = null;
            _opened = null;
            _tip = null;
        }

        // Harness door: put a site's hints up on the game's own panel without playing the
        // story to it - the panel shows the site's options as the data names them.
        internal static string Preview(string site)
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn == null || mn.uiMN == null || !Hints.TryGetValue(site, out var hints))
            {
                return "no such site: " + site;
            }
            if (site.StartsWith("@") || hints.ContainsKey("yes"))
            {
                return "yes/no preview is not supported";
            }
            var shown = new List<string>();
            foreach (string id in hints.Keys)
            {
                if (int.TryParse(id, out int i) && i < mn.textMN.choices.Length)
                {
                    shown.Add(mn.textMN.choices[i].Replace("XXXX", "Kana"));
                }
            }
            mn.story.StartCoroutine(mn.uiMN.MultipleChoice(shown.ToArray()));
            _site = site;
            return site + ": " + string.Join(" / ", shown.ToArray());
        }
    }

    internal enum HintMode
    {
        Off,
        OnClick,
        Always,
    }
}
