using System.Collections.Generic;
using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE MANAGE LIST'S OWN LOOK (Forgejo #142, direction A,
    // "the stem"). The ask on Forgejo #53: "Instead of a speech bubble, we should have the options
    // presented somehow else." The list rises out of the Manage key as a column of round
    // keys of the key's own kind - the emblem socket, a cream glyph - each with its word on
    // a small dark tab to its right, the name tab's piece and face: Move nearest the key
    // (the key wears Move's glyph, so the first option is the key unfolding), then Scale and
    // Edit, and Remove last and farthest, a step apart, in red enamel. Weighed against a
    // wooden drawer of rows (B, a small window again) and a quarter-circle fan (C, words
    // only under the pointer, and running off the card into whatever stands left of it).
    //
    // THE BEHAVIOUR IS THE BUBBLE'S LIST (TalkBubble.ToggleList, still there): the same
    // ListSpec - rows re-read every frame, a row known by its name, a dim row answering a
    // press at the pointer, ended by its owner - asked for in one place
    // (InventoryWindow.ShowManageList). It takes nothing but itself: the pointer over a
    // key or its tab, a press begun there through its release, and Escape. A press
    // anywhere else closes it and goes on to do what it would (walk, swing, press the
    // HUD); a press on the Manage key is the dock's, which closes it.
    internal static class ManageFan
    {
        private sealed class Row
        {
            public RectTransform Rect;
            public Image Hit, Tab, Socket, Glyph;
            public TextMeshProUGUI Label;
            public string Name, Skin;
            public bool Enabled, Danger;
            public float Y, W, PlacedY = float.NaN, PlacedW;
        }

        // BOARD PIXELS FROM THE KEY'S CENTRE, y down, at the key's own scale (it is
        // InventoryWindow.ManageKeyS across): a socket a little smaller than the key,
        // its glyph as the key's, the tab the name tab's piece at 40 of its 50 tall
        private const float SocketS = 56f, GlyphS = 34f, Gap = 8f, DangerGap = 12f,
            TabH = 40f, TabX = 6f, TextX = 36f, TabTrail = 34f, TextSize = 5.6f;
        private static readonly Vector4 TabBorder = new Vector4(30, 0, 30, 0);
        // it rises out of the key in this long, eased; a press waits for nothing
        private const float RiseFor = .14f, RiseFrom = 22f;
        private const float DoubleClick = .35f, SayFor = 1.4f;

        private static readonly Color Cream = Theme.ParseColor("#F8EBCD", Color.white);
        private static readonly Color Lit = Theme.ParseColor("#FFF7E2", Color.white);
        private static readonly Color DangerInk = Theme.ParseColor("#FFA896", Color.red);
        private static readonly Color Outline = new Color32(26, 16, 12, 255);

        private static ManagersScript _mn;
        private static TalkBubble.ListSpec _spec;
        private static bool _open;
        private static float _openedAt;
        private static int _openedFrame = -10, _closedFrame = -10;
        private static bool _invoked, _pressHeld;
        private static string _pressedName, _hoverName, _whyClosed = "";
        private static float _quietUntil = -1f;
        private static Rect _quietRect;
        private static string _said;
        private static float _saidUntil = -1f;
        private static int _locGen = -1;
        private static int _overFrame = -1;
        private static bool _over;

        private static readonly List<TalkBubble.ListItem> Items = new List<TalkBubble.ListItem>();
        private static readonly List<Row> Rows = new List<Row>();
        private static int _count;

        private static GameObject _root;
        private static RectTransform _overlay, _stem;
        private static Canvas _sort;
        private static Camera _uiCam;

        internal static bool OpenFor(object owner) => _open && _spec != null && ReferenceEquals(_spec.Owner, owner);

        // the pointer is the stem's over a key or its tab, through the release of a
        // press begun there, and where it stood for a moment after a choice
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
                return _pressHeld || (_open && PointerOnStem())
                    || (Time.unscaledTime < _quietUntil && _quietRect.Contains(Input.mousePosition));
            }
        }

        // ...and Escape, while it is up and for the frame it closes on and the next
        // (FilterInput's key reads ask this)
        internal static bool Swallows(KeyCode key)
        {
            return key == KeyCode.Escape && (_open || Time.frameCount - _closedFrame <= 1);
        }

        internal static void ResetForSceneLoad()
        {
            _root = null;
            _overlay = _stem = null;
            _sort = null;
            _open = false;
            _spec = null;
            _pressHeld = false;
            _quietUntil = -1f;
            Rows.Clear();
            Items.Clear();
            _count = 0;
        }

        // ---- opening and closing -------------------------------------------------

        // Pressed again for the same owner it closes; for another, it opens on that one.
        internal static void Toggle(ManagersScript mn, TalkBubble.ListSpec spec)
        {
            if (_open)
            {
                bool same = spec != null && ReferenceEquals(_spec.Owner, spec.Owner);
                Close("toggle");
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
            _spec = spec;
            _open = true;
            _openedAt = Time.unscaledTime;
            _openedFrame = Time.frameCount;
            _invoked = false;
            _pressedName = _hoverName = null;
            _saidUntil = -1f;
            _overFrame = -1;
            _count = -1;
            _root.SetActive(true);
            // asserted here: Unity drops overrideSorting set on an inactive object
            _sort.overrideSorting = true;
            _sort.sortingOrder = 2;   // over the HUD and the dock, under the cursor badge and tips (3)
            _root.transform.SetAsLastSibling();
            Refresh();
            if (!Place())
            {
                Close("off screen");
                return;
            }
            Paint();
            Plugin.Log.LogInfo("Manage stem: " + spec.Title + ", " + _count + " keys");
        }

        private static void Close(string why)
        {
            if (!_open)
            {
                return;
            }
            _open = false;
            _closedFrame = Time.frameCount;
            _whyClosed = why;
            _spec = null;
            if (_root != null)
            {
                _root.SetActive(false);
            }
            Plugin.Log.LogInfo("Manage stem closed: " + why);
        }

        // Every frame, after the dock (Plugin.Update): what ends it, the rows, the
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

        // its owner's end (the thing gone, another target, nothing to manage), a
        // scene, the map
        private static string Ended()
        {
            if (_root == null || _mn == null || !_mn.allSet || _mn.uiMN == null || _mn.gameMN == null)
            {
                return "scene";
            }
            string why = _spec.Ended != null ? _spec.Ended() : null;
            if (why != null)
            {
                return why;
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

        // ---- the rows ------------------------------------------------------------------

        // RE-READ EVERY FRAME, as the bubble's list is: a key's reach changes as you
        // walk. Rebuilt only when what is drawn changes - a row come or gone, its word
        // or glyph, the language - and repainted every frame.
        private static void Refresh()
        {
            Items.Clear();
            _spec.Read(Items);
            bool same = Items.Count == _count && _locGen == Shared.Loc.Generation;
            for (int i = 0; same && i < Items.Count; i++)
            {
                Row r = Rows[i];
                TalkBubble.ListItem item = Items[i];
                same = r.Name == item.Name && r.Label.text == (item.Label ?? item.Name)
                    && r.Glyph.sprite == item.Icon && r.Danger == item.Danger;
            }
            if (!same)
            {
                Rebuild();
            }
            for (int i = 0; i < _count; i++)
            {
                Rows[i].Enabled = Items[i].Enabled && Items[i].Press != null;
            }
        }

        private static void Rebuild()
        {
            _locGen = Shared.Loc.Generation;
            while (Rows.Count < Items.Count)
            {
                Rows.Add(NewRow(Rows.Count));
            }
            float y = -(InventoryWindow.ManageKeyS * .5f + Gap + SocketS * .5f);
            for (int i = 0; i < Rows.Count; i++)
            {
                Row r = Rows[i];
                bool shown = i < Items.Count;
                if (r.Rect.gameObject.activeSelf != shown) { r.Rect.gameObject.SetActive(shown); }
                if (!shown)
                {
                    continue;
                }
                TalkBubble.ListItem item = Items[i];
                r.Name = item.Name;
                r.Danger = item.Danger;
                r.Label.text = item.Label ?? item.Name;
                r.Glyph.sprite = item.Icon;
                r.Glyph.enabled = item.Icon != null;
                // Remove a step apart from the rest, as it was under the bubble's full rule
                if (item.Danger && i > 0) { y -= DangerGap; }
                r.Y = y;
                y -= SocketS + Gap;
                float words = r.Label.GetPreferredValues(r.Label.text).x * AnimePieces.Px;
                float tabW = TextX - TabX + words + TabTrail;
                r.W = TabX + tabW + SocketS * .5f;
                AnimePieces.Place(r.Tab, SocketS * .5f + TabX, (SocketS - TabH) * .5f, tabW, TabH);
                AnimePieces.Place(r.Socket, 0f, 0f, SocketS, SocketS);
                AnimePieces.Place(r.Glyph, (SocketS - GlyphS) * .5f, (SocketS - GlyphS) * .5f, GlyphS, GlyphS);
                AnimePieces.Place(r.Label, SocketS * .5f + TextX, 0f, words + 8f, SocketS);
            }
            _count = Items.Count;
        }

        private static Row NewRow(int i)
        {
            var go = Owned.New("Key" + i);
            go.transform.SetParent(_stem, false);
            // a row is a point on the stem's; its box hangs from it (Paint)
            var r = new Row { Rect = (RectTransform)go.transform };
            r.Rect.anchorMin = r.Rect.anchorMax = r.Rect.pivot = new Vector2(.5f, .5f);
            r.Rect.sizeDelta = Vector2.zero;
            // the row's whole box takes the pointer, so the game's UI raycasts stop on it
            r.Hit = AnimeHud.Image(go.transform, "Hit", "mask");
            r.Hit.color = Color.clear;
            r.Hit.raycastTarget = true;
            r.Tab = AnimePieces.Image(r.Hit.transform, "Tab", "target_tab");
            AnimePieces.Sliced(r.Tab, "target_tab", TabBorder, TabH / 50f);
            r.Socket = Theme.KitImage(r.Hit.transform, "Socket", "kit_emblem_socket_idle", 0, Color.white);
            r.Glyph = Theme.KitImage(r.Hit.transform, "Glyph", "kit_emblem_move", 0, Color.white);
            r.Label = Theme.MakeKitText(r.Hit.transform, "Label", TextSize, Cream, TextAlignmentOptions.MidlineLeft, true);
            TMP_FontAsset face = InventoryWindow.HudStrongFont;
            if (face != null)
            {
                r.Label.font = face;
                r.Label.fontStyle = FontStyles.Normal;
                Theme.FlattenType(r.Label);
            }
            r.Label.enableWordWrapping = false;
            r.Label.overflowMode = TextOverflowModes.Overflow;
            r.Label.outlineWidth = .15f;
            r.Label.outlineColor = Outline;
            r.Label.raycastTarget = false;
            return r;
        }

        // ---- placement -------------------------------------------------------------------

        // ON THE KEY, AT ITS SCALE: the stem's origin is the key's centre and its board
        // pixel the key's, measured from the key's own rect every frame - the dock moves
        // with the bar and the HUD's scale. False when the key is gone or hidden.
        private static bool Place()
        {
            RectTransform key = _spec.Anchor != null ? _spec.Anchor() : null;
            if (key == null || !key.gameObject.activeInHierarchy)
            {
                return false;
            }
            var corners = new Vector3[4];
            key.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[2]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, a, _uiCam, out Vector2 la)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, b, _uiCam, out Vector2 lb))
            {
                return false;
            }
            float scale = Mathf.Abs(lb.y - la.y) / (InventoryWindow.ManageKeyS / AnimePieces.Px);
            if (scale < .01f)
            {
                return false;
            }
            _stem.anchoredPosition = (la + lb) * .5f;
            _stem.localScale = new Vector3(scale, scale, 1f);
            return true;
        }

        // ---- input -------------------------------------------------------------------------

        private static bool Over(Graphic g, Vector2 mouse)
        {
            return g != null && g.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, mouse, _uiCam);
        }

        // the row under the pointer, by name, or null; a key's socket and its tab, and
        // the space between them
        private static string RowAt(Vector2 mouse)
        {
            for (int i = 0; i < _count; i++)
            {
                if (Over(Rows[i].Hit, mouse)) { return Rows[i].Name; }
            }
            return null;
        }

        private static bool PointerOnStem()
        {
            if (_overFrame != Time.frameCount)
            {
                bool was = _bypass;
                _bypass = true;
                try { _over = _root != null && _root.activeInHierarchy && RowAt(Input.mousePosition) != null; }
                finally { _bypass = was; }
                _overFrame = Time.frameCount;
            }
            return _over;
        }

        private static bool OnAnchor(Vector2 mouse)
        {
            RectTransform key = _spec != null && _spec.Anchor != null ? _spec.Anchor() : null;
            bool was = _bypass;
            _bypass = true;
            try { return key != null && key.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(key, mouse, null); }
            finally { _bypass = was; }
        }

        // the pointer over the keys and Escape, read raw; a press anywhere else closes
        // the stem and is the world's and the HUD's as if the stem were not there
        private static void ReadInput()
        {
            Vector2 mouse = Input.mousePosition;
            string row = RowAt(mouse);
            _hoverName = row;
            bool down = Input.GetMouseButtonDown(0);
            if (down || Input.GetMouseButtonDown(1))
            {
                if (row == null && !OnAnchor(mouse))
                {
                    Close("pressed outside");
                    return;
                }
                _pressHeld |= row != null;
                _pressedName = down ? row : null;
            }
            if (Input.GetMouseButtonUp(0))
            {
                string chosen = _pressedName != null && _pressedName == row ? row : null;
                _pressedName = null;
                if (chosen != null)
                {
                    Rect at = StemRect();
                    Activate(chosen);
                    if (!_open)
                    {
                        _quietUntil = Time.unscaledTime + DoubleClick;
                        _quietRect = at;
                    }
                    return;
                }
            }
            if (FilterInput.RawKeyDown(KeyCode.Escape))
            {
                Close("escape");
            }
        }

        // ONE PRESS, ONE CALL. The row is found again in the list as it is now - never
        // an index into the column as drawn - and must still be enabled; then the stem
        // closes and the act is pressed. A dim row answers at the pointer.
        private static void Activate(string name)
        {
            if (_invoked || name == null)
            {
                return;
            }
            Items.Clear();
            _spec.Read(Items);
            TalkBubble.ListItem item = null;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Name == name) { item = Items[i]; break; }
            }
            if (item == null || !item.Enabled || item.Press == null)
            {
                _said = item != null && item.Why != null ? item.Why() : null;
                _saidUntil = Time.unscaledTime + SayFor;
                return;
            }
            // MID-SWING OR GUARDING, THE ACT WAITS, as the bubble's did
            PlayerMove mover = _mn.gameMN.pMove;
            if (mover != null && (mover.toolAction || mover.guard))
            {
                return;
            }
            _invoked = true;
            Close("chose \"" + (item.Label ?? item.Name) + "\"");
            item.Press();
        }

        // the column's box on the screen, for the double click's quiet moment
        private static Rect StemRect()
        {
            var r = new Rect();
            bool any = false;
            var corners = new Vector3[4];
            for (int i = 0; i < _count; i++)
            {
                Rows[i].Hit.rectTransform.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(_uiCam, corners[2]);
                Rect one = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, one.xMin), Mathf.Min(r.yMin, one.yMin),
                    Mathf.Max(r.xMax, one.xMax), Mathf.Max(r.yMax, one.yMax)) : one;
                any = true;
            }
            return r;
        }

        // ---- painting ------------------------------------------------------------------------

        // A KEY AS THE DOCK'S: warm under the pointer, a little darker held, Remove in
        // red enamel; out of reach the socket fades and the glyph greys, as ActRow dims
        // an act, and its word with them. It rises out of the Manage key as it opens.
        private static void Paint()
        {
            if (Time.unscaledTime < _saidUntil && !string.IsNullOrEmpty(_said))
            {
                CursorBadge.Show(_said);
            }
            float t = Mathf.Clamp01((Time.unscaledTime - _openedAt) / RiseFor);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            float first = -(InventoryWindow.ManageKeyS * .5f + Gap + SocketS * .5f);
            bool held = Input.GetMouseButton(0);
            for (int i = 0; i < _count; i++)
            {
                Row r = Rows[i];
                float y = Mathf.Lerp(first + RiseFrom, r.Y, e);
                if (y != r.PlacedY || r.W != r.PlacedW)
                {
                    AnimePieces.Place(r.Hit, -SocketS * .5f, y - SocketS * .5f, r.W, SocketS);
                    r.PlacedY = y;
                    r.PlacedW = r.W;
                }
                bool hot = r.Enabled && r.Name == _hoverName;
                bool down = hot && held && r.Name == _pressedName;
                string skin = r.Danger
                    ? hot ? "kit_emblem_socket_danger_hot" : "kit_emblem_socket_danger"
                    : hot ? "kit_emblem_socket_hot" : "kit_emblem_socket_idle";
                if (r.Skin != skin)
                {
                    Theme.KitSkin(r.Socket, skin, 0);
                    r.Skin = skin;
                }
                float k = down ? .8f : 1f;
                float a = (r.Enabled ? 1f : .45f) * e;
                r.Socket.color = new Color(k, k, k, a);
                r.Glyph.color = r.Enabled ? new Color(k, k, k, e) : new Color(.7f, .7f, .7f, .4f * e);
                r.Tab.color = new Color(k, k, k, (r.Enabled ? 1f : .8f) * e);
                Color ink = r.Danger ? DangerInk : hot ? Lit : Cream;
                r.Label.color = new Color(ink.r * k, ink.g * k, ink.b * k, (r.Enabled ? 1f : .5f) * e);
            }
        }

        // ---- building ------------------------------------------------------------------------

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
            // ITS OWN SORTED CANVAS over everything MainCanvas draws, as the Talk
            // bubble's: the dock is clipped, and its siblings could not lift it over the HUD
            _root = Owned.New("IslandUI_ManageStem");
            _root.transform.SetParent(rc.transform, false);
            _overlay = (RectTransform)_root.transform;
            _overlay.anchorMin = Vector2.zero;
            _overlay.anchorMax = Vector2.one;
            _overlay.pivot = Vector2.zero;
            _overlay.offsetMin = _overlay.offsetMax = Vector2.zero;
            _sort = _root.AddComponent<Canvas>();
            _root.AddComponent<GraphicRaycaster>();
            // the stem: a point on the key's centre that its rows hang from (Theme.Place
            // is top-left based, and a rect of no size has its top-left on its pivot)
            var stemGo = Owned.New("Stem");
            stemGo.transform.SetParent(_root.transform, false);
            _stem = (RectTransform)stemGo.transform;
            _stem.anchorMin = _stem.anchorMax = Vector2.zero;
            _stem.pivot = new Vector2(.5f, .5f);
            _stem.sizeDelta = Vector2.zero;
            _root.SetActive(false);
            return true;
        }

        // ---- the hit-test chokepoint -----------------------------------------------------

        private static bool _bypass;

        // THE BUBBLE'S CHOKEPOINT (TalkBubble.HitTest), for the stem: while it owns the
        // pointer every polled surface of this mod under it answers no, and its own
        // rects yes. Only the pointer's position, only this overload.
        [HarmonyPatch(typeof(RectTransformUtility), nameof(RectTransformUtility.RectangleContainsScreenPoint),
            typeof(RectTransform), typeof(Vector2), typeof(Camera))]
        internal static class HitTest
        {
            [HarmonyPrefix]
            private static bool Prefix(RectTransform rect, Vector2 screenPoint, ref bool __result)
            {
                if (_bypass || _root == null || !OwnsPointer || rect == null || rect.IsChildOf(_root.transform)
                    || screenPoint != (Vector2)Input.mousePosition)
                {
                    return true;
                }
                __result = false;
                return false;
            }
        }

        // ---- the harness -----------------------------------------------------------------

        // each key's name, words, state and centre in screen pixels from the top left,
        // for a real click (post-input.ps1)
        internal static string Dump()
        {
            var sb = new System.Text.StringBuilder();
            if (!_open)
            {
                return sb.Append("closed (").Append(_whyClosed).Append(')').ToString();
            }
            sb.Append("open, ").Append(_count).Append(" keys");
            var corners = new Vector3[4];
            for (int i = 0; i < _count; i++)
            {
                Row r = Rows[i];
                r.Hit.rectTransform.GetWorldCorners(corners);
                Vector2 c = RectTransformUtility.WorldToScreenPoint(_uiCam, (corners[0] + corners[1]) * .5f);
                float x = c.x + (RectTransformUtility.WorldToScreenPoint(_uiCam, corners[3]).x - c.x) * (SocketS * .5f / r.W);
                sb.Append(" | ").Append(r.Name).Append(" \"").Append(r.Label.text).Append("\" ")
                    .Append(r.Enabled ? "on" : "dim").Append(r.Danger ? " danger" : "")
                    .Append(" at ").Append(x.ToString("0")).Append(',').Append((Screen.height - c.y).ToString("0"));
            }
            return sb.ToString();
        }
    }
}
