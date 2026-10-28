using System.Collections.Generic;
using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE TALK BUBBLE (Forgejo #58; the design is art/ref/talk-carry-parchment-v1,
    // its IMPLEMENTATION.md the behaviour). Talk on the target dock opens the game's
    // conversation choices at once, in a parchment speech bubble whose tail points
    // at the character's head. The step between - Talk lit the panel's Sex, which
    // had no glyph and waited in More actions - is gone.
    //
    // THE CHOICES ARE VANILLA'S PANEL, READ (D65). The dock's Talk is NPCPanel/talk,
    // wired in the scene to UIManager.TalkPanelVisible, which opens talkPanel
    // (NPCPanel/talk/TalkPanel) under our veil. That panel's lit buttons are the list,
    // in its own order: Talk and Sex today, Want and Present when vanilla lights them,
    // and whatever another mod puts there (Forgejo #11). So Talk is still pressed
    // through, once, and the panel is read after it; a row presses its own button, so
    // the game's handler (NPCManager.NPCTalkButton, SexButton, a mod's listener) runs
    // as it always did. Nothing here decides what a choice does or whether it is on
    // offer, save one thing: no Sex for anyone under 18 (Underage). The panel is
    // vanilla's one for everybody, so vanilla is asked again for this person as it
    // opens (TargetVerbs.LightTalkPanel, Forgejo #127: Sex lit for someone else
    // showed here, and did nothing). A choice is terminal: the bubble closes and
    // leaves the panel to the game. Closed without a choice, it shuts the panel it
    // opened.
    //
    // A ROW'S ICON IS ITS WIRING, NEVER ITS WORDS. Sex is the button vanilla keeps in
    // npcPanelState whose one scene call is NPCManager.SexButton; Talk the one whose
    // scene call is NPCManager.NPCTalkButton, while exactly one is. Anything else - a
    // mod's button, a clone, Want, Present, a line literally called "Sex" - wears the
    // single outlined bubble. Labels are the buttons' own, in the game's language.
    //
    // IT TAKES NOTHING BUT ITSELF (Forgejo #58 round 2: it used to be modal, and the
    // player stood locked while it was up). You walk, swing, use the HUD and the keys
    // with it open; only the pointer over the paper is its own - for the world's click
    // and wheel (gameMN.OnUI, through DragOwnsThePointer), the game's UI raycasts (the
    // paper's own graphic) and this mod's polled surfaces (the hit-test chokepoint
    // below) - and a press begun on it, through its release. Escape is the one key it
    // keeps. It closes on Escape, its x, the Talk tile, a choice, and by itself when the
    // target is gone, changed or out of reach (Ended). The arrows are the walk's (the
    // game's Vertical axis) and Space the attack, so the rows take no keyboard focus.
    //
    // A LIST OF ACTS IN THE SAME PAPER (Forgejo #52, interim): the dock's Manage key
    // opened its list - Move, Scale, Edit, Remove - here (ToggleList), its rows from the
    // dock rather than from vanilla's talk panel, its tail on the key rather than a head.
    // It was asked to be presented some other way than a speech bubble, and since Forgejo
    // #142 it is a stem of keys rising out of the key (ManageFan), on the same ListSpec;
    // InventoryWindow.ShowManageList is the one place that picks between them. One
    // bubble at a time: either opening closes the other.
    internal static class TalkBubble
    {
        private enum Kind { Sex, Talk, Other }

        private sealed class Choice
        {
            public int Id;          // the source Button's instance id: who it is, never its label
            public Button Button;
            public string Label;
            public bool Rich;       // the source label's own rich-text setting, kept
            public Kind Kind;
            public bool Enabled;
            // a list's row (ToggleList): its own icon, Remove's red, and what a press does
            public Sprite Icon;
            public bool Danger;
            public System.Action Press;
            public System.Func<string> Why;
        }

        // A list's row as its owner gives it, re-read every frame as the talk panel is
        internal sealed class ListItem
        {
            public string Name;          // who it is from frame to frame: never its words
            public string Label;
            public Sprite Icon;
            public bool Danger;          // destroys something: red, last, under a full rule
            public bool Enabled = true;
            public System.Action Press;
            public System.Func<string> Why;   // said at the pointer when a dim row is pressed
        }

        internal sealed class ListSpec
        {
            public object Owner;                         // whose list it is: pressed again, it closes
            public string Title;
            public System.Func<RectTransform> Anchor;    // what the tail points at; null or hidden ends it
            public System.Action<List<ListItem>> Read;   // the rows
            public System.Func<string> Ended;            // why it ends now, or null
        }

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Fill, Select, Icon, Rule;
            public TextMeshProUGUI Label;
            public float Y, H, LabelH;
            public int Id;
            public bool Enabled;
        }

        // ONE PIXEL OF THE DESIGN'S 1920 x 1080 FRAME in MainCanvas units: the canvas is
        // 800 wide whatever the screen, so these sizes scale with the resolution and the
        // game's canvas scaling as one, like the rest of the HUD. The 2x exports carry
        // two texels per design pixel: 4.8 a unit, the board's own density (AnimePieces.Px).
        private const float U = 800f / 1920f;
        private const float Texels = 2f / U;
        // the design's numbers (IMPLEMENTATION.md section 4; preview.html for the row)
        private const float PreferredW = 360f, MaxW = 440f, Pad = 18f, HeaderH = 44f,
            TitleH = 38f, RowMinH = 48f, RowPadY = 11f, RowLead = 25f, RowTrail = 12f, IconS = 26f,
            IconGap = 12f, LabelSize = 20f, TitleSize = 24f, CloseS = 32f, CloseGlyph = 20f,
            Margin = 16f, MaxBodyH = 400f, TailW = 72f, TailH = 48f, TailX = 30f, TailOverlap = 15f,
            TipX = 7f, TipY = 46f, Corner = 28f, ShadowOff = 3f, BarW = 5f, BarRoom = 10f,
            RuleH = 1.25f, Slack = 12f, WheelStep = 48f;
        // the body's paper and its 36 px border at 2x; the row states' 12 px
        private static readonly Vector4 BodyBorder = new Vector4(72, 72, 72, 72);
        private static readonly Vector4 RowBorder = new Vector4(24, 24, 24, 24);
        // a wrapped label's lines at the preview's 1.35, over Georgia's own 1.14 (em/100)
        private const float LineGap = 20f;
        // THE TAIL'S TOP 10 PX OVER THE BODY'S CONTOUR, which runs 5 px inside the
        // paper's box (bubble-body.svg, y 187 of 192): the tail's own outline then starts
        // on the body's, and its fill covers the line between them (TailOverlap, from the box)
        // where the tail points: this far up the figure, just under the top of the hair
        private const float HeadAt = .92f;

        private static readonly Color Ink = Theme.ParseColor("#342014", Color.black);
        private static readonly Color Muted = Theme.ParseColor("#63472B", Color.black);
        private static readonly Color Rule = new Color(181f / 255f, 122f / 255f, 55f / 255f, .25f);
        private static readonly Color RuleFull = new Color(99f / 255f, 71f / 255f, 43f / 255f, .7f);
        private static readonly Color Danger = Theme.ParseColor("#A82C24", Color.red);
        private static readonly Color Track = new Color(229f / 255f, 201f / 255f, 155f / 255f, .8f);
        private static readonly Color Thumb = Theme.ParseColor("#AD773E", Color.grey);
        private static readonly Color Shade = new Color(0f, 0f, 0f, .2f);

        private static ManagersScript _mn;
        private static ListSpec _list;   // the list open instead of Talk's choices, or null
        private static readonly List<ListItem> ListRows = new List<ListItem>();
        private static CommonStates _who;
        private static Renderer _whoBody;
        private static bool _open;
        private static int _openedFrame = -10, _closedFrame = -10;
        private static bool _invoked;
        // a press begun on the bubble is eaten until its release, the bubble closing
        // under it included
        private static bool _pressHeld;
        // ...and a double click's second press, which came where the paper was after a
        // choice had closed it
        private const float DoubleClick = .35f;
        private static float _quietUntil = -1f;
        private static Rect _quietRect;
        private static int _pressedId;
        private static bool _pressedClose;
        private static int _hoverId;
        private static int _overFrame = -1;
        private static bool _over;
        private static float _scroll, _total, _viewH;
        private static int _side = -1, _mirror = -1;   // the last placement, kept near the edge
        private static float _w, _h;
        private static int _locGen = -1;
        private static Vector2 _screen;
        private static string _whyClosed = "";
        private static string _said;
        private static float _saidUntil = -1f;
        private const float SayFor = 1.4f;

        private static readonly List<Choice> Choices = new List<Choice>();
        private static readonly List<Choice> Fresh = new List<Choice>();
        private static readonly List<Row> Rows = new List<Row>();
        private static readonly List<Button> Scan = new List<Button>();

        private static GameObject _root;
        private static RectTransform _overlay, _bubble, _viewport;
        private static Canvas _sort;
        private static Camera _uiCam;
        private static Image _body, _tail, _bodyShade, _tailShade, _closeFill, _closeGlyph, _track, _thumb;
        private static TextMeshProUGUI _title, _empty;
        private static TMP_FontAsset _face, _faceBold;
        private static bool _facesTried;

        // Talk's bubble, up: it stands where the person's marker would
        internal static bool IsOpen => _open && _list == null;
        internal static bool OpenFor(CommonStates who) => _open && _list == null && ReferenceEquals(_who, who);
        internal static bool ListOpenFor(object owner) => _open && _list != null && ReferenceEquals(_list.Owner, owner);

        // the pointer is the bubble's while it is over the paper, through the release
        // of a press begun there, and where the paper was for a moment after a choice
        // (WorldHoverBlock's DragOwnsThePointer asks this)
        internal static bool OwnsPointer
        {
            get
            {
                if (_pressHeld && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)
                    && !Input.GetMouseButtonUp(0) && !Input.GetMouseButtonUp(1))
                {
                    _pressHeld = false;
                }
                return _pressHeld || (_open && PointerOnPaper())
                    || (Time.unscaledTime < _quietUntil && _quietRect.Contains(Input.mousePosition));
            }
        }

        // ...and Escape, while it is up and for the frame it closes on and the next: the
        // Escape that closed it would otherwise untarget or pause, whichever Update
        // reads the key after ours (FilterInput's key reads ask this)
        internal static bool Swallows(KeyCode key)
        {
            return key == KeyCode.Escape && (_open || Time.frameCount - _closedFrame <= 1);
        }

        // once a frame, past the chokepoint below, which asks it from inside the test
        private static bool PointerOnPaper()
        {
            if (_overFrame != Time.frameCount)
            {
                bool was = _bypass;
                _bypass = true;
                try { _over = OverBubble(Input.mousePosition); }
                finally { _bypass = was; }
                _overFrame = Time.frameCount;
            }
            return _over;
        }

        internal static void ResetForSceneLoad()
        {
            _root = null;
            _overlay = _bubble = _viewport = null;
            _sort = null;
            _open = false;
            _list = null;
            _who = null;
            _whoBody = null;
            _pressHeld = false;
            _quietUntil = -1f;
            Choices.Clear();
            Fresh.Clear();
            Rows.Clear();
            _probeHideVanilla = false;
            _probePin = null;
        }

        // ---- opening and closing -------------------------------------------------

        // The dock's Talk. Pressed again while the bubble is up it closes it; otherwise
        // `pressTalk` presses vanilla's own Talk, the act as it always was, and the
        // panel it opened is read.
        internal static void Toggle(ManagersScript mn, CommonStates who, System.Action pressTalk)
        {
            if (_open)
            {
                bool talk = _list == null;
                Close("toggle", true);
                if (talk)
                {
                    return;
                }
            }
            UIManager ui = mn != null ? mn.uiMN : null;
            if (who == null || ui == null || ui.talkPanel == null || mn.gameMN == null || pressTalk == null)
            {
                return;
            }
            _mn = mn;
            if (!Ensure())
            {
                return;
            }
            // UIManager.TalkPanelVisible: NPCMenuReset, then the panel up - always up,
            // since the reset shuts it first. Mods that add their lines on it have run.
            pressTalk();
            if (!ui.talkPanel.activeSelf || !ReferenceEquals(mn.gameMN.menuNPC, who))
            {
                Plugin.Log.LogInfo("Talk bubble: vanilla opened no talk panel for " + who.charaName);
                return;
            }
            // ...lit for THIS person: the panel is one for the whole game, and was
            // last lit for whoever vanilla or a read last asked about (Forgejo #127)
            TargetVerbs.LightTalkPanel(mn, who);
            _who = who;
            _whoBody = who.GetComponentInChildren<Renderer>();
            Show();
            Plugin.Log.LogInfo("Talk bubble: " + who.charaName + ", " + Choices.Count + " choices");
        }

        // A list in the bubble (the dock's Manage key). Pressed again for the same
        // owner it closes; for another, the bubble up closes and this one opens.
        internal static void ToggleList(ManagersScript mn, ListSpec spec)
        {
            if (_open)
            {
                bool same = _list != null && spec != null && ReferenceEquals(_list.Owner, spec.Owner);
                Close("toggle", true);
                if (same)
                {
                    return;
                }
            }
            if (mn == null || mn.uiMN == null || mn.gameMN == null || spec == null || spec.Read == null)
            {
                return;
            }
            _mn = mn;
            if (!Ensure())
            {
                return;
            }
            _list = spec;
            _who = null;
            _whoBody = null;
            Show();
            Plugin.Log.LogInfo("List bubble: " + spec.Title + ", " + Choices.Count + " rows");
        }

        private static void Show()
        {
            _open = true;
            _saidUntil = -1f;
            _openedFrame = Time.frameCount;
            _invoked = false;
            _hoverId = _pressedId = 0;
            _pressedClose = false;
            _overFrame = -1;
            _scroll = 0f;
            _side = _mirror = -1;
            Choices.Clear();
            Read(Choices);
            _root.SetActive(true);
            // asserted here: Unity drops overrideSorting set on an inactive object
            _sort.overrideSorting = true;
            _sort.sortingOrder = 2;   // over the HUD and the dock, under the cursor badge and tips (3)
            _root.transform.SetAsLastSibling();
            Rebuild();
        }

        private static void Close(string why, bool deliberate = false, bool shutPanel = true)
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            _closedFrame = Time.frameCount;
            _whyClosed = why;
            UIManager ui = _mn != null ? _mn.uiMN : null;
            bool talk = _list == null;
            _list = null;
            // the TalkPanel half of vanilla's NPCMenuReset: what Talk opened, shut
            if (talk && shutPanel && ui != null && ui.talkPanel != null && ui.talkPanel.activeSelf)
            {
                ui.talkPanel.SetActive(false);
            }
            if (_root != null)
            {
                _root.SetActive(false);
            }
            // DISMISSED, FOCUS GOES BACK TO TALK, the way it came - shown as the dock's
            // keyboard ring only when Escape closed it
            if (talk && deliberate && _who != null && InventoryWindow.TalkHeld(_who))
            {
                InventoryWindow.FocusTalk(why == "escape");
            }
            Plugin.Log.LogInfo((talk ? "Talk" : "List") + " bubble closed: " + why);
        }

        // Every frame, after the dock (Plugin.Update): what ends it, then the list, the
        // place and the input.
        internal static void Tick(ManagersScript mn)
        {
            if (!_open)
            {
                return;
            }
            _mn = mn;
            string why = Ended();
            if (why != null)
            {
                Close(why);
                return;
            }
            Refresh();
            if (!Place())
            {
                Close("off screen");
                return;
            }
            if (Time.frameCount != _openedFrame)
            {
                ReadInput();
                if (!_open)
                {
                    return;
                }
            }
            Paint();
        }

        // what takes the bubble away without asking: the target gone or changed, out
        // of reach, vanilla's menu or talk panel shut, a scene, the map
        private static string Ended()
        {
            if (_root == null || _mn == null || !_mn.allSet || _mn.uiMN == null || _mn.gameMN == null)
            {
                return "scene";
            }
            if (_list != null)
            {
                string why = _list.Ended != null ? _list.Ended() : null;
                if (why != null)
                {
                    return why;
                }
            }
            else if (_who == null || !_who.gameObject.activeInHierarchy || _who.life <= 0.0)
            {
                return "target gone";
            }
            else if (!InventoryWindow.TalkHeld(_who))
            {
                return "target changed or out of reach";
            }
            else if (!ReferenceEquals(_mn.gameMN.menuNPC, _who) || _mn.uiMN.talkPanel == null || !_mn.uiMN.talkPanel.activeSelf)
            {
                return "menu closed";
            }
            if (GameManager.nowEvent || (_mn.gameMN.pMove != null && !_mn.gameMN.pMove.controllable))
            {
                return "scene";
            }
            if (_mn.uiMN.mapCanvas != null && _mn.uiMN.mapCanvas.activeInHierarchy)
            {
                return "map";
            }
            return null;
        }

        // ---- the choices -----------------------------------------------------------

        // The panel's lit buttons in its own order: this one and every ancestor up to
        // the panel active (TargetVerbs.Reachable's rule - a button inside a shut
        // sub-panel is not on offer), the panel itself allowed to be veiled.
        private static void Read(List<Choice> into)
        {
            into.Clear();
            if (_list != null)
            {
                ReadList(into);
                return;
            }
            GameObject panel = _mn != null && _mn.uiMN != null ? _mn.uiMN.talkPanel : null;
            if (panel == null)
            {
                return;
            }
            panel.GetComponentsInChildren(true, Scan);
            int talks = 0;
            for (int i = 0; i < Scan.Count; i++)
            {
                if (Lit(Scan[i], panel) && Wired(Scan[i], "NPCTalkButton")) { talks++; }
            }
            for (int i = 0; i < Scan.Count; i++)
            {
                Button b = Scan[i];
                if (!Lit(b, panel))
                {
                    continue;
                }
                bool sex = Wired(b, "SexButton") && InState(b.gameObject);
                bool talk = talks == 1 && Wired(b, "NPCTalkButton");
                if (_probeHideVanilla && !b.name.StartsWith(FakePrefix))
                {
                    continue;
                }
                var c = new Choice
                {
                    Id = b.GetInstanceID(),
                    Button = b,
                    Kind = sex ? Kind.Sex : talk ? Kind.Talk : Kind.Other,
                    Enabled = b.enabled && b.interactable,
                };
                ReadLabel(b.gameObject, c);
                into.Add(c);
            }
            Scan.Clear();
        }

        // a list's rows, as its owner gives them; a row is known by its name's hash
        private static void ReadList(List<Choice> into)
        {
            ListRows.Clear();
            _list.Read(ListRows);
            for (int i = 0; i < ListRows.Count; i++)
            {
                ListItem item = ListRows[i];
                int id = Animator.StringToHash(item.Name ?? item.Label ?? i.ToString());
                into.Add(new Choice
                {
                    Id = id != 0 ? id : 1,
                    Label = item.Label ?? item.Name,
                    Kind = Kind.Other,
                    Enabled = item.Enabled && item.Press != null,
                    Icon = item.Icon,
                    Danger = item.Danger,
                    Press = item.Press,
                    Why = item.Why,
                });
            }
            ListRows.Clear();
        }

        private static bool Lit(Button b, GameObject panel)
        {
            if (b == null || b.gameObject == panel || !b.gameObject.activeSelf)
            {
                return false;
            }
            for (Transform t = b.transform.parent; t != null && t != panel.transform; t = t.parent)
            {
                if (!t.gameObject.activeSelf) { return false; }
            }
            return true;
        }

        // vanilla's own wiring: one call, set in the scene, on the game's NPCManager
        private static bool Wired(Button b, string method)
        {
            Button.ButtonClickedEvent e = b.onClick;
            return e.GetPersistentEventCount() == 1 && e.GetPersistentTarget(0) is NPCManager
                && e.GetPersistentMethodName(0) == method;
        }

        private static bool InState(GameObject go)
        {
            GameObject[] set = _mn.uiMN.npcPanelState;
            return set != null && System.Array.IndexOf(set, go) >= 0;
        }

        // the button's own words: TMP, or a mod's legacy Text, with its rich-text
        // setting; a button with none gets the neutral name, and keeps its callback
        private static void ReadLabel(GameObject go, Choice c)
        {
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            {
                c.Label = tmp.text;
                c.Rich = tmp.richText;
                return;
            }
            var legacy = go.GetComponentInChildren<Text>(true);
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
            {
                c.Label = legacy.text;
                c.Rich = legacy.supportRichText;
                return;
            }
            c.Label = Shared.Loc.Get("talk.choice_unnamed", "Conversation option");
            c.Rich = false;
        }

        // THE LIST IS RE-READ EVERY FRAME (a few buttons), and the bubble rebuilt only
        // when it changed - a mod's line arriving late, a label localised, Sex lit or
        // put out - or when the language or the screen did. Talk is not pressed again.
        private static void Refresh()
        {
            Read(Fresh);
            bool same = Fresh.Count == Choices.Count && _locGen == Shared.Loc.Generation
                && _screen == new Vector2(Screen.width, Screen.height);
            for (int i = 0; same && i < Fresh.Count; i++)
            {
                Choice a = Fresh[i], b = Choices[i];
                same = a.Id == b.Id && a.Enabled == b.Enabled && a.Kind == b.Kind && a.Label == b.Label && a.Rich == b.Rich
                    && a.Icon == b.Icon && a.Danger == b.Danger;
            }
            if (same)
            {
                return;
            }
            Choices.Clear();
            Choices.AddRange(Fresh);
            Rebuild();
        }

        // the row drawn for a choice; the pool keeps rows past the list, and those
        // still carry the ids they last drew
        private static int IndexOf(int id)
        {
            for (int i = 0; i < Choices.Count && i < Rows.Count; i++)
            {
                if (Rows[i].Id == id) { return i; }
            }
            return -1;
        }

        // ---- building ----------------------------------------------------------------

        private static bool Ensure()
        {
            if (_root != null)
            {
                return true;
            }
            UIManager ui = _mn.uiMN;
            Canvas canvas = ui.itemDescPanel != null ? ui.itemDescPanel.GetComponentInParent<Canvas>() : null;
            if (canvas == null || !Theme.Ensure(_mn, _mn.inventory))
            {
                return false;
            }
            Canvas rc = canvas.rootCanvas;
            _uiCam = rc.renderMode != RenderMode.ScreenSpaceOverlay ? rc.worldCamera : null;
            // ITS OWN SORTED CANVAS, over everything MainCanvas draws: sibling order
            // inside the clipped dock could not lift it over the HUD
            _root = Owned.New("IslandUI_TalkBubble");
            _root.transform.SetParent(rc.transform, false);
            _overlay = (RectTransform)_root.transform;
            _overlay.anchorMin = Vector2.zero;
            _overlay.anchorMax = Vector2.one;
            _overlay.pivot = Vector2.zero;
            _overlay.offsetMin = _overlay.offsetMax = Vector2.zero;
            _sort = _root.AddComponent<Canvas>();
            _root.AddComponent<GraphicRaycaster>();
            var bubbleGo = Owned.New("Bubble");
            bubbleGo.transform.SetParent(_root.transform, false);
            _bubble = (RectTransform)bubbleGo.transform;
            _bubble.anchorMin = _bubble.anchorMax = _bubble.pivot = Vector2.zero;
            // a soft shadow under the paper and the tail, a runtime effect, not baked
            _bodyShade = Piece(_bubble, "BodyShadow");
            _tailShade = Piece(_bubble, "TailShadow");
            _body = Piece(_bubble, "Body");
            _tail = Piece(_bubble, "Tail");
            Sliced(_bodyShade, "bubble_body", BodyBorder);
            Sliced(_body, "bubble_body", BodyBorder);
            AnimePieces.Set(_tailShade, "bubble_tail");
            AnimePieces.Set(_tail, "bubble_tail");
            _bodyShade.color = _tailShade.color = Shade;
            // the paper and its tail are the bubble's: the game's UI raycasts stop on them,
            // and nowhere else
            _body.raycastTarget = _tail.raycastTarget = true;
            _title = Text(_bubble, "Title", TitleSize * U, Ink, TextAlignmentOptions.MidlineLeft, true);
            _title.enableWordWrapping = false;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _closeFill = Piece(_bubble, "Close");
            _closeGlyph = Piece(_closeFill.transform, "Glyph");
            AnimePieces.Set(_closeGlyph, "bubble_close");
            _closeGlyph.color = Ink;
            var viewGo = Owned.New("Choices");
            viewGo.transform.SetParent(_bubble, false);
            _viewport = (RectTransform)viewGo.transform;
            viewGo.AddComponent<RectMask2D>();
            _empty = Text(_viewport, "Nothing", LabelSize * U, Muted, TextAlignmentOptions.MidlineLeft, false);
            _empty.enableWordWrapping = true;
            _empty.overflowMode = TextOverflowModes.Overflow;
            _track = Piece(_bubble, "Track");
            _thumb = Piece(_bubble, "Thumb");
            _track.color = Track;
            _thumb.color = Thumb;
            _root.SetActive(false);
            return true;
        }

        // GEORGIA, THE DESIGN'S FACE, built from the installed file as the marker's
        // fallback is: Theme.KitFont's OS route returns null on this Unity (SystemFont's
        // header), so the kit's text is the game's M+ in practice. A static Latin-1
        // atlas with the game's font behind it for everything else; the kit's face if
        // Windows has no Georgia.
        private static TextMeshProUGUI Text(Transform parent, string name, float size, Color color,
            TextAlignmentOptions align, bool bold)
        {
            if (!_facesTried)
            {
                _facesTried = true;
                _face = SystemFont.Build("georgia.ttf", "IslandUI Talk Georgia", Theme.Font);
                _faceBold = SystemFont.Build("georgiab.ttf", "IslandUI Talk Georgia Bold", Theme.Font);
            }
            TextMeshProUGUI text = Theme.MakeKitText(parent, name, size, color, align, bold);
            TMP_FontAsset face = bold ? _faceBold : _face;
            if (face != null)
            {
                text.font = face;
                text.fontStyle = FontStyles.Normal;   // the bold is its own face
                Theme.FlattenType(text);
            }
            return text;
        }

        private static Image Piece(Transform parent, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Sliced(Image image, string piece, Vector4 border)
        {
            AnimePieces.Sliced(image, piece, border);
            image.pixelsPerUnitMultiplier = Texels;
        }

        // a row state's piece, set only when it changes (a slice re-meshes)
        private static void State(Image image, string piece)
        {
            if (image.sprite != AnimePieces.Get(piece, RowBorder)) { Sliced(image, piece, RowBorder); }
        }

        // top-left placement inside the bubble, in design pixels
        private static void P(Component c, float x, float y, float w, float h)
        {
            Theme.Place(c, x * U, y * U, w * U, h * U);
        }

        private static Row NewRow(int i)
        {
            var go = Owned.New("Choice" + i);
            go.transform.SetParent(_viewport, false);
            var row = new Row { Rect = (RectTransform)go.transform };
            row.Rule = Piece(go.transform, "Rule");
            row.Rule.color = Rule;
            row.Fill = Piece(go.transform, "Highlight");
            row.Select = Piece(go.transform, "Marker");
            AnimePieces.Set(row.Select, "bubble_select");
            row.Select.color = Ink;
            row.Icon = Piece(go.transform, "Icon");
            row.Icon.preserveAspect = true;
            row.Label = Text(go.transform, "Label", LabelSize * U, Ink, TextAlignmentOptions.TopLeft, false);
            row.Label.enableWordWrapping = true;
            row.Label.overflowMode = TextOverflowModes.Overflow;
            row.Label.lineSpacing = LineGap;
            return row;
        }

        private static string IconOf(Kind kind)
        {
            return kind == Kind.Sex ? "choice_sex" : kind == Kind.Talk ? "choice_talk" : "choice_other";
        }

        // THE LAYOUT, from the list: the width fitted to the words within 360-440 (less
        // on a narrow screen), long labels wrapped onto taller rows - never shrunk or cut -
        // and the list scrolling under a fixed header past the height allowed
        private static void Rebuild()
        {
            _locGen = Shared.Loc.Generation;
            _screen = new Vector2(Screen.width, Screen.height);
            Vector2 room = _overlay.rect.size / U;
            _title.text = _list != null ? _list.Title : Shared.Loc.Get("talk.header", "Talk to {0}", _who != null ? _who.charaName : "");
            _empty.text = Shared.Loc.Get("talk.nothing", "Nothing to discuss");
            while (Rows.Count < Choices.Count)
            {
                Rows.Add(NewRow(Rows.Count));
            }
            float need = _title.GetPreferredValues(_title.text).x / U + 8f + CloseS;
            for (int i = 0; i < Choices.Count; i++)
            {
                Row r = Rows[i];
                r.Label.richText = Choices[i].Rich;
                r.Label.text = Choices[i].Label;
                need = Mathf.Max(need, RowLead + IconS + IconGap + r.Label.GetPreferredValues(r.Label.text).x / U + RowTrail);
            }
            if (Choices.Count == 0)
            {
                need = Mathf.Max(need, _empty.GetPreferredValues(_empty.text).x / U + RowLead);
            }
            float avail = room.x - 2f * Margin;
            // a narrow screen takes it under the 360, to the room there is between the
            // margins; the labels wrap the more
            _w = Mathf.Min(Mathf.Clamp(need + 2f * Pad, PreferredW, MaxW), avail);
            float maxBody = Mathf.Min(MaxBodyH, .55f * (room.y - 2f * Margin));
            float maxList = Mathf.Max(RowMinH, maxBody - 2f * Pad - HeaderH);
            float listW = _w - 2f * Pad;
            _total = Measure(listW);
            bool overflow = _total > maxList + .5f;
            if (overflow)
            {
                _total = Measure(listW - BarRoom);
            }
            _viewH = Mathf.Min(_total, maxList);
            _h = 2f * Pad + HeaderH + _viewH;
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _total - _viewH));
            // the header: the name, and the close at the corner
            P(_title, Pad, Pad, _w - 2f * Pad - CloseS - 8f, TitleH);
            P(_closeFill, _w - Pad - CloseS + 6f, Pad + (TitleH - CloseS) * .5f, CloseS, CloseS);
            P(_closeGlyph, (CloseS - CloseGlyph) * .5f, (CloseS - CloseGlyph) * .5f, CloseGlyph, CloseGlyph);
            P(_viewport, Pad, Pad + HeaderH, listW, _viewH);
            for (int i = 0; i < Rows.Count; i++)
            {
                bool shown = i < Choices.Count;
                if (Rows[i].Rect.gameObject.activeSelf != shown) { Rows[i].Rect.gameObject.SetActive(shown); }
                if (!shown) { continue; }
                Row r = Rows[i];
                Choice c = Choices[i];
                r.Id = c.Id;
                r.Enabled = c.Enabled;
                float rowW = listW - (overflow ? BarRoom : 0f);
                // a list row wears its own glyph in the ink (Remove's in its red), or none
                Color ink = c.Danger ? Danger : Ink;
                if (_list != null)
                {
                    r.Icon.sprite = c.Icon;
                    r.Icon.enabled = c.Icon != null;
                    r.Icon.color = c.Enabled ? ink : new Color(ink.r, ink.g, ink.b, .4f);
                }
                else
                {
                    r.Icon.enabled = true;
                    AnimePieces.Set(r.Icon, IconOf(c.Kind));
                    if (r.Icon.sprite == null) { AnimePieces.Set(r.Icon, "choice_other"); }
                    r.Icon.color = c.Enabled ? Color.white : new Color(1f, 1f, 1f, .4f);
                }
                r.Icon.preserveAspect = true;
                r.Label.color = c.Enabled ? ink : new Color(ink.r, ink.g, ink.b, .45f);
                // a single line sits in the middle of its 48; a wrapped label starts at
                // the row's top, and the icon and the marker stay with its first line
                bool single = r.H <= RowMinH + .01f;
                float lineMid = single ? r.H * .5f : RowPadY + LineH(r.Label) * .5f;
                float labelX = RowLead + IconS + IconGap;
                P(r.Rect, 0f, r.Y, rowW, r.H);
                P(r.Fill, 0f, 0f, rowW, r.H);
                // the rule over a destructive row is full width and full strength
                if (c.Danger) { P(r.Rule, 0f, -RuleH, rowW, 2f * RuleH); }
                else { P(r.Rule, RowLead, -RuleH * .5f, rowW - RowLead - RowTrail, RuleH); }
                r.Rule.color = c.Danger ? RuleFull : Rule;
                r.Rule.enabled = i > 0;
                P(r.Select, 1.5f, lineMid - 8f, 16f, 16f);
                P(r.Icon, RowLead, lineMid - IconS * .5f, IconS, IconS);
                P(r.Label, labelX, single ? (r.H - r.LabelH) * .5f : RowPadY, rowW - labelX - RowTrail, r.LabelH);
            }
            _empty.gameObject.SetActive(Choices.Count == 0);
            if (Choices.Count == 0)
            {
                P(_empty, RowLead * .5f, 0f, listW - RowLead * .5f, RowMinH);
            }
            _track.gameObject.SetActive(overflow);
            _thumb.gameObject.SetActive(overflow);
            if (overflow)
            {
                P(_track, _w - Pad - BarW, Pad + HeaderH, BarW, _viewH);
            }
            P(_body, 0f, 0f, _w, _h);
            P(_bodyShade, ShadowOff, ShadowOff, _w, _h);
            _bubble.sizeDelta = new Vector2(_w * U, _h * U);
        }

        // the rows' heights at a width: wrapped labels grow their row
        private static float Measure(float rowW)
        {
            float y = 0f;
            float labelW = rowW - RowLead - IconS - IconGap - RowTrail;
            for (int i = 0; i < Choices.Count; i++)
            {
                Row r = Rows[i];
                r.LabelH = Mathf.Max(LineH(r.Label), r.Label.GetPreferredValues(r.Label.text, labelW * U, 0f).y / U);
                r.H = Mathf.Max(RowMinH, r.LabelH + 2f * RowPadY);
                r.Y = y;
                y += r.H;
            }
            return Choices.Count == 0 ? RowMinH : y;
        }

        private static float LineH(TextMeshProUGUI t)
        {
            return t.GetPreferredValues("Ag").y / U;
        }

        // ---- placement -----------------------------------------------------------------

        // THE TAIL POINTS AT THE HEAD. The body prefers above the head and to its right,
        // the tail pointing down-left; it mirrors left at the screen's right, goes under
        // the head at its top, and is clamped on screen when nothing fits, the tail kept
        // on the straight edge under the head as near as it can. A placement that still
        // fits with a little slack is kept, so it does not flip back and forth at an
        // edge. False when the head is off the screen: no bubble pointing at nobody.
        private static bool Place()
        {
            Camera cam = Camera.main;
            if (cam == null || (_who == null && _list == null))
            {
                return false;
            }
            Vector3 screen;
            if (_probePin.HasValue)
            {
                screen = new Vector3(_probePin.Value.x, _probePin.Value.y, 1f);
            }
            else if (_list != null)
            {
                // a list's tail on the top of its key
                RectTransform key = _list.Anchor != null ? _list.Anchor() : null;
                if (key == null || !key.gameObject.activeInHierarchy)
                {
                    return false;
                }
                var corners = new Vector3[4];
                key.GetWorldCorners(corners);
                Vector2 top = RectTransformUtility.WorldToScreenPoint(_uiCam, (corners[1] + corners[2]) * .5f);
                screen = new Vector3(top.x, top.y, 1f);
            }
            else
            {
                Vector3 feet = _who.transform.position;
                Vector3 up = cam.transform.up;
                float rise = _whoBody != null ? _whoBody.bounds.max.y - feet.y : 2f;
                screen = cam.WorldToScreenPoint(feet + up * (rise * HeadAt / Mathf.Max(.2f, up.y)));
            }
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
            {
                return false;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, _uiCam, out Vector2 local))
            {
                return false;
            }
            Vector2 t = local / U;
            Vector2 room = _overlay.rect.size / U;
            float lift = TipY - TailOverlap;
            float above = t.y + lift, below = t.y - lift - _h;
            int side;
            if (_side == 0 && above + _h <= room.y - Margin + Slack) { side = 0; }
            else if (_side == 1 && below >= Margin - Slack) { side = 1; }
            else if (above + _h <= room.y - Margin) { side = 0; }
            else if (below >= Margin) { side = 1; }
            else { side = room.y - t.y >= t.y ? 0 : 1; }
            float by = Mathf.Clamp(side == 0 ? above : below, Margin, Mathf.Max(Margin, room.y - Margin - _h));
            float right = t.x - (TailX + TipX), left = t.x + (TailX + TipX) - _w;
            bool rightFits = right >= Margin && right + _w <= room.x - Margin;
            bool leftFits = left >= Margin && left + _w <= room.x - Margin;
            int mirror;
            if (_mirror == 0 && right >= Margin - Slack && right + _w <= room.x - Margin + Slack) { mirror = 0; }
            else if (_mirror == 1 && left >= Margin - Slack && left + _w <= room.x - Margin + Slack) { mirror = 1; }
            else if (rightFits) { mirror = 0; }
            else if (leftFits) { mirror = 1; }
            else { mirror = t.x <= room.x * .5f ? 0 : 1; }
            float bx = Mathf.Clamp(mirror == 0 ? right : left, Margin, Mathf.Max(Margin, room.x - Margin - _w));
            _side = side;
            _mirror = mirror;
            _bubble.anchoredPosition = new Vector2(bx * U, by * U);
            // the tail's root on the straight edge, away from the rounded corners
            float tx = mirror == 0 ? t.x - bx - TipX : t.x - bx - (TailW - TipX);
            tx = Mathf.Clamp(tx, Corner, Mathf.Max(Corner, _w - Corner - TailW));
            float ty = side == 0 ? _h - TailOverlap : TailOverlap - TailH;
            PlaceTail(_tail, tx, ty, mirror == 1, side == 1);
            PlaceTail(_tailShade, tx + ShadowOff, ty + ShadowOff, mirror == 1, side == 1);
            return true;
        }

        private static void PlaceTail(Image tail, float x, float y, bool mirrorX, bool mirrorY)
        {
            P(tail, x, y, TailW, TailH);
            AnimePieces.Centre(tail);
            tail.rectTransform.localScale = new Vector3(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f, 1f);
        }

        // ---- input ---------------------------------------------------------------------

        private static bool Over(Graphic g, Vector2 mouse)
        {
            return g != null && g.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, mouse, _uiCam);
        }

        private static bool OverBubble(Vector2 mouse)
        {
            return Over(_body, mouse) || Over(_tail, mouse);
        }

        private static int RowAt(Vector2 mouse)
        {
            if (_viewport == null || !RectTransformUtility.RectangleContainsScreenPoint(_viewport, mouse, _uiCam))
            {
                return 0;
            }
            for (int i = 0; i < Choices.Count && i < Rows.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(Rows[i].Rect, mouse, _uiCam)) { return Rows[i].Id; }
            }
            return 0;
        }

        // the pointer over the paper and Escape, read raw; everything else is the
        // world's and the HUD's, as if the bubble were not there
        private static void ReadInput()
        {
            Vector2 mouse = Input.mousePosition;
            bool overBubble = OverBubble(mouse);
            bool overClose = Over(_closeFill, mouse);
            int row = overBubble && !overClose ? RowAt(mouse) : 0;
            _hoverId = row;
            bool down = Input.GetMouseButtonDown(0);
            if (down || Input.GetMouseButtonDown(1))
            {
                // A PRESS OFF THE PAPER CLOSES NOTHING: it walks, swings or presses the
                // HUD as it would without the bubble, and the Talk tile's own press is
                // the dock's, which closes it (Toggle)
                _pressHeld |= overBubble;
                _pressedClose = down && overClose;
                _pressedId = down && !overClose ? row : 0;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool close = _pressedClose && overClose;
                int chosen = _pressedId != 0 && _pressedId == row ? row : 0;
                _pressedClose = false;
                _pressedId = 0;
                if (close) { Close("close", true); return; }
                if (chosen != 0)
                {
                    Activate(chosen);
                    if (!_open)
                    {
                        _quietUntil = Time.unscaledTime + DoubleClick;
                        _quietRect = Screened(_body.rectTransform);
                    }
                    return;
                }
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f && overBubble)
            {
                ScrollBy(-wheel * WheelStep);
            }
            if (FilterInput.RawKeyDown(KeyCode.Escape))
            {
                Close("escape", true);
            }
        }

        private static void ScrollBy(float dy)
        {
            _scroll = Mathf.Clamp(_scroll + dy, 0f, Mathf.Max(0f, _total - _viewH));
        }

        // ONE PRESS, ONE CALL. The same choice is found again in the panel as it is
        // now - never an index into the list as it was drawn - and must still be lit,
        // enabled and the target's; then the bubble closes and the button is pressed.
        private static void Activate(int id)
        {
            if (_invoked || id == 0)
            {
                return;
            }
            Read(Fresh);
            Choice c = null;
            for (int i = 0; i < Fresh.Count; i++)
            {
                if (Fresh[i].Id == id) { c = Fresh[i]; break; }
            }
            if (c == null || !c.Enabled)
            {
                // a list's dim row answers at the pointer for a moment, as the dock's
                // keys do beyond reach (Paint keeps saying it)
                _said = c != null && c.Why != null ? c.Why() : null;
                _saidUntil = Time.unscaledTime + SayFor;
                return;
            }
            if (_list != null)
            {
                PlayerMove mover = _mn.gameMN.pMove;
                if (mover != null && (mover.toolAction || mover.guard))
                {
                    return;
                }
                _invoked = true;
                Close("chose \"" + c.Label + "\"", false);
                c.Press();
                return;
            }
            if (!ReferenceEquals(_mn.gameMN.menuNPC, _who) || !InventoryWindow.TalkHeld(_who))
            {
                Close("refused");
                return;
            }
            // MID-SWING OR GUARDING, THE CHOICE WAITS: vanilla's menu never takes one
            // then - its loop closes it on the swing - and a talk or a scene begun
            // under a tool's coroutine would share the body with it
            PlayerMove p = _mn.gameMN.pMove;
            if (p != null && (p.toolAction || p.guard))
            {
                return;
            }
            _invoked = true;
            Close("chose " + c.Kind + " \"" + c.Label + "\"", false, false);
            c.Button.onClick.Invoke();
        }

        // ---- painting ------------------------------------------------------------------

        private static void Paint()
        {
            if (Time.unscaledTime < _saidUntil && !string.IsNullOrEmpty(_said))
            {
                CursorBadge.Show(_said);
            }
            bool held = Input.GetMouseButton(0);
            for (int i = 0; i < Choices.Count && i < Rows.Count; i++)
            {
                Row r = Rows[i];
                bool hot = r.Enabled && r.Id == _hoverId;
                bool down = r.Enabled && held && r.Id == _pressedId && r.Id == _hoverId;
                r.Fill.enabled = hot || down;
                if (r.Fill.enabled) { State(r.Fill, down ? "bubble_row_pressed" : "bubble_row_hover"); }
                // the marker on the one row that answers a release
                r.Select.enabled = hot;
                Vector2 at = r.Rect.anchoredPosition;
                float y = -(r.Y - _scroll) * U;
                if (!Mathf.Approximately(at.y, y)) { r.Rect.anchoredPosition = new Vector2(at.x, y); }
            }
            bool overClose = Over(_closeFill, Input.mousePosition);
            _closeFill.enabled = overClose;
            if (overClose) { State(_closeFill, held && _pressedClose ? "bubble_row_pressed" : "bubble_row_hover"); }
            if (_thumb.gameObject.activeSelf)
            {
                float th = Mathf.Max(18f, _viewH * _viewH / Mathf.Max(1f, _total));
                float span = Mathf.Max(1f, _total - _viewH);
                P(_thumb, _w - Pad - BarW, Pad + HeaderH + (_viewH - th) * (_scroll / span), BarW, th);
            }
        }

        // ---- the hit-test chokepoint -----------------------------------------------------

        private static bool _bypass;

        // EVERY POLLED SURFACE OF THIS MOD asks RectTransformUtility whether the pointer
        // is over it - the dock, the windows, the action bar, the tool belt, the docks'
        // chips: sixty-odd sites. While the bubble owns the pointer (over the paper, or
        // a press begun there) the answer is no, except for the bubble's own rects, so
        // nothing under the paper lights or takes a press; off it, nothing changes.
        // Only the pointer's own position is refused, and only on this overload: the
        // game's GraphicRaycaster calls the four-argument one, untouched.
        [HarmonyPatch(typeof(RectTransformUtility), nameof(RectTransformUtility.RectangleContainsScreenPoint),
            typeof(RectTransform), typeof(Vector2), typeof(Camera))]
        internal static class HitTest
        {
            [HarmonyPrefix]
            private static bool Prefix(RectTransform rect, Vector2 screenPoint, ref bool __result)
            {
                if (_bypass || !OwnsPointer || rect == null || _root == null || rect.IsChildOf(_root.transform)
                    || screenPoint != (Vector2)Input.mousePosition)
                {
                    return true;
                }
                __result = false;
                return false;
            }
        }

        // ---- the harness ---------------------------------------------------------------

        private static bool _probeHideVanilla;
        private static Vector2? _probePin;
        private static int _fakePresses;
        private const string FakePrefix = "IslandUI_FakeTalk";

        // For the harness: the bubble as drawn - each row's kind, words, state and the
        // centre of its rect in screen pixels, for a real click (post-input.ps1)
        internal static string Dump()
        {
            var sb = new System.Text.StringBuilder();
            if (!_open)
            {
                return sb.Append("closed (").Append(_whyClosed).Append(") fake presses ").Append(_fakePresses).ToString();
            }
            sb.Append("open: ").Append(_list != null ? "list" : _who != null ? _who.charaName : "?").Append(" | ").Append(_title.text)
                .Append(" | w ").Append(_w.ToString("0")).Append(" h ").Append(_h.ToString("0"))
                .Append(" | side ").Append(_side == 0 ? "above" : "below").Append(_mirror == 1 ? " left" : " right")
                .Append(" | body ").Append(ScreenRect(_body.rectTransform))
                .Append(" | close ").Append(Centre(_closeFill.rectTransform))
                .Append(" | scroll ").Append(_scroll.ToString("0")).Append('/').Append(Mathf.Max(0f, _total - _viewH).ToString("0"))
                .Append(" | hover ").Append(IndexOf(_hoverId));
            RectTransform talk = InventoryWindow.TalkSocket();
            if (talk != null) { sb.Append(" | talk ").Append(Centre(talk)); }
            for (int i = 0; i < Choices.Count; i++)
            {
                Choice c = Choices[i];
                sb.Append("\n  ").Append(i).Append(' ').Append(c.Kind).Append(c.Enabled ? "" : " (disabled)")
                    .Append(" \"").Append(c.Label).Append("\" ").Append(_list != null ? c.Danger ? "(danger)" : "(row)" : c.Button != null ? c.Button.name : "(gone)")
                    .Append(" at ").Append(Centre(Rows[i].Rect)).Append(" h ").Append(Rows[i].H.ToString("0"));
            }
            return sb.ToString();
        }

        private static string Centre(RectTransform r)
        {
            Rect s = Screened(r);
            return s.center.x.ToString("0") + "," + (Screen.height - s.center.y).ToString("0");
        }

        private static string ScreenRect(RectTransform r)
        {
            Rect s = Screened(r);
            return s.xMin.ToString("0") + "," + (Screen.height - s.yMax).ToString("0") + " " + s.width.ToString("0") + "x" + s.height.ToString("0");
        }

        private static Rect Screened(RectTransform r)
        {
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // For the harness: your people near you, with their age, to pick an adult to
        // talk to
        internal static string Nearby()
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            NPCManager npcs = mn != null ? mn.npcMN : null;
            Transform you = mn != null && mn.gameMN != null && mn.gameMN.player != null ? mn.gameMN.player.transform : null;
            if (npcs == null || you == null)
            {
                return "no game";
            }
            var near = new List<KeyValuePair<float, string>>();
            foreach (CommonStates who in npcs.friendList)
            {
                if (who == null || !who.gameObject.activeInHierarchy) { continue; }
                float d = Vector3.Distance(you.position, who.transform.position);
                if (d < 15f)
                {
                    near.Add(new KeyValuePair<float, string>(d, who.charaName + " age " + who.age + " npcID " + who.npcID
                        + " " + d.ToString("0.0") + " m dx " + (who.transform.position.x - you.position.x).ToString("0.0")
                        + " dz " + (who.transform.position.z - you.position.z).ToString("0.0")));
                }
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            var sb = new System.Text.StringBuilder();
            foreach (var n in near) { sb.Append(n.Value).Append('\n'); }
            return sb.ToString();
        }

        // For the harness (Forgejo #127): vanilla's own talk choices for one of your
        // people - a world click, whose CheckNPC runs NPCMenuOpen before it first
        // yields, so the talk panel is read as vanilla lit it, before any tick of ours
        internal static string Vanilla(string name)
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            CommonStates who = null;
            NPCManager npcs = mn != null ? mn.npcMN : null;
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                if (npcs.friendList[i] != null && npcs.friendList[i].charaName == name) { who = npcs.friendList[i]; break; }
            }
            if (who == null || mn.uiMN == null || mn.uiMN.talkPanel == null)
            {
                return "nobody named " + name;
            }
            string click = InventoryWindow.ClickPersonNamed(name);
            string head = name + " age " + who.age + " npcID " + who.npcID + " " + who.employ + ": vanilla ";
            return head + (ReferenceEquals(mn.gameMN.menuNPC, who) ? TalkLit(mn.uiMN.talkPanel) : "no menu (" + click + ")");
        }

        // ...and, touching nothing, every one of yours by that name: age, npcID and
        // what SexCheck says of them - for those too far to click
        internal static string Who(string name)
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            NPCManager npcs = mn != null ? mn.npcMN : null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                CommonStates who = npcs.friendList[i];
                if (who != null && who.charaName == name)
                {
                    bool check = mn.sexMN != null && mn.sexMN.SexCheck(mn.gameMN.playerCommons[GameManager.selectPlayer], who);
                    sb.Append(name).Append(" age ").Append(who.age).Append(" npcID ").Append(who.npcID)
                        .Append(" SexCheck ").Append(check).Append('\n');
                }
            }
            return sb.Length > 0 ? sb.ToString() : "nobody named " + name;
        }

        // ...and TargetVerbs asked about one of them as a sheet or the dock asks, with
        // the talk panel after: still lit for vanilla's menu, not for them
        internal static string AskAbout(string name)
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            NPCManager npcs = mn != null ? mn.npcMN : null;
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                if (npcs.friendList[i] != null && npcs.friendList[i].charaName == name)
                {
                    TargetVerbs.Read(mn, npcs.friendList[i]);
                    return TargetVerbs.Current.Count + " verbs read | " + Panel();
                }
            }
            return "nobody named " + name;
        }

        // ...and the talk panel as it stands, whose it is, and what SexCheck says of them
        internal static string Panel()
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            if (mn == null || mn.uiMN == null || mn.uiMN.talkPanel == null || mn.gameMN == null)
            {
                return "no game";
            }
            CommonStates who = mn.gameMN.menuNPC;
            string check = who != null && mn.sexMN != null
                ? mn.sexMN.SexCheck(mn.gameMN.playerCommons[GameManager.selectPlayer], who).ToString() : "-";
            return "lit " + TalkLit(mn.uiMN.talkPanel) + " | menuNPC " + (who != null ? who.charaName : "none") + " | SexCheck " + check;
        }

        private static string TalkLit(GameObject panel)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Transform t in panel.transform)
            {
                if (t.gameObject.activeSelf && t.GetComponent<Button>() != null)
                {
                    sb.Append(sb.Length > 0 ? ", " : "").Append(t.name);
                }
            }
            return "[" + sb + "]";
        }

        // HARNESS ONLY, INERT UNTIL CALLED: stands in for what no NPC or mod in the
        // test save offers, on vanilla's own talk panel, as a mod would put it there.
        // "many" ten mod lines; "long" one long line; "unknown" a clone of Sex with its
        // wiring replaced and the word "Sex" kept, a bare Button with a legacy Text
        // called "Talk", and a disabled one; "empty" hides vanilla's lines from the
        // bubble's read only; "remove" takes all of it away. Presses log and count.
        internal static string Probe(string mode)
        {
            ManagersScript mn = _mn ?? Object.FindObjectOfType<ManagersScript>();
            GameObject panel = mn != null && mn.uiMN != null ? mn.uiMN.talkPanel : null;
            if (panel == null)
            {
                return "no talkPanel";
            }
            _mn = mn;
            switch (mode)
            {
                case "remove":
                    int gone = 0;
                    for (int i = panel.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = panel.transform.GetChild(i).gameObject;
                        if (child.name.StartsWith(FakePrefix)) { Object.DestroyImmediate(child); gone++; }
                    }
                    _probeHideVanilla = false;
                    _probePin = null;
                    return "removed " + gone;
                case "empty":
                    _probeHideVanilla = true;
                    return "vanilla lines hidden from the read";
                case "many":
                    for (int i = 1; i <= 10; i++) { Fake(panel, "Mod conversation " + i, true); }
                    return "added 10";
                case "long":
                    Fake(panel, "A much longer conversation option supplied by another mod, wrapping onto multiple lines", true);
                    return "added 1";
                case "unknown":
                    Transform sex = panel.transform.Find("Sex");
                    if (sex != null)
                    {
                        GameObject clone = Owned.Instantiate(sex.gameObject, panel.transform);
                        clone.name = FakePrefix + "Sex";
                        foreach (MonoBehaviour b in clone.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (b.GetType().Name == "LocalizeStringEvent") { Object.DestroyImmediate(b); }
                        }
                        clone.GetComponentInChildren<TextMeshProUGUI>(true).text = "Sex";
                        FakePress(clone, "a mod's Sex");
                    }
                    Fake(panel, "Talk", false);
                    Fake(panel, "Mod-added option", false);
                    Button off = Fake(panel, "A disabled mod line", false);
                    off.interactable = false;
                    return "added 4";
                default:
                    return "presses " + _fakePresses;
            }
        }

        // ...and the anchor pinned at a screen point (pixels from the top left), to set
        // the bubble against an edge; Unpin lets it follow the head again
        internal static string Pin(float x, float y)
        {
            _probePin = new Vector2(x, Screen.height - y);
            return "pinned at " + x + "," + y;
        }

        internal static string Unpin()
        {
            _probePin = null;
            return "following the head";
        }

        // ...and the wheel, which post-input.ps1 cannot send
        internal static string Wheel(float notches)
        {
            ScrollBy(-notches * WheelStep);
            return "scroll " + _scroll.ToString("0");
        }

        private static Button Fake(GameObject panel, string words, bool tmp)
        {
            var go = Owned.New(FakePrefix);
            go.transform.SetParent(panel.transform, false);
            go.AddComponent<Image>();
            Button button = go.AddComponent<Button>();
            var label = Owned.New("Text");
            label.transform.SetParent(go.transform, false);
            if (tmp)
            {
                label.AddComponent<TextMeshProUGUI>().text = words;
            }
            else
            {
                Text legacy = label.AddComponent<Text>();
                legacy.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                legacy.text = words;
            }
            FakePress(go, words);
            return button;
        }

        private static void FakePress(GameObject go, string what)
        {
            go.SetActive(true);
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();   // a clone's vanilla wiring goes, as a mod's would
            button.onClick.AddListener(() => { _fakePresses++; Plugin.Log.LogInfo("fake talk choice pressed: " + what); });
        }
    }
}
