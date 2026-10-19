using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE ACT ROW (D105; one component in two homes, used in both places for
    // consistency). An oak
    // band of six FIXED sockets: the shape group first - Talk · Carry · Follow on a
    // person, Move · Remove · Repair on a thing - then the target's own acts, the
    // doors last, empties kept dim so a position means the same on every target.
    // It is the unit's foot under the reading, and the foot of every thing's window
    // under the grid or the reading.
    //
    // It knows nothing of vanilla: a slot is a name, a label or an icon, whether it
    // is lit and live, and what to do when pressed. The readers (TargetVerbs,
    // PropVerbs) fill it; it draws and answers the pointer.
    internal sealed class ActRow
    {
        internal sealed class Slot
        {
            public string Name;
            public string Label;
            public Sprite Icon;      // drawn instead of the label when set
            public bool Lit;         // a toggle that is on: pine, ink type
            public bool Live = true; // false: out of reach - dim, and refuses the press
            public bool Faded;       // drawn out of reach, but pressed: a door that arms until you walk up
            public bool OwnColors;   // vanilla's own art (Move, Remove, Repair): drawn as it is, not inked
            public System.Action Press;
            public string Tag;       // points to spend, on the socket's top-right corner (PointsTag)
            public PointsTag.Kind TagKind;
        }

        // anything to show at all - an enemy's dock and a stranger's idle thing's have nothing
        public bool Any
        {
            get
            {
                for (int i = 0; i < Sockets; i++) { if (_slots[i] != null) { return true; } }
                return false;
            }
        }

        public const int Sockets = 16;
        public const int ShapeSockets = 3;
        public const float PadX = 5f;
        public const float PadY = 3f;
        public const float SocketH = 13f;
        public const float SocketGap = 3f;
        public const float GroupGap = 4f;    // after the shape group
        public const float H = SocketH + 2f * PadY;

        public readonly GameObject Go;
        // COMPACT: a socket per act present, left-aligned, no shape group - a
        // thing's own acts are one or two and unpredictable, so there are no
        // fixed positions to keep; the band and the socket size stay the unit's
        public bool Compact;
        public bool IconMode;
        // The target dock shares the floating indicator's walnut/brass materials.
        // Other ActRow homes (crates and sheets) keep their existing skins.
        public bool Enamel;
        public TMP_FontAsset LabelFont;
        // the tool shed's crates: wooden blocks with a stencilled word,
        // no band behind them
        public bool Shed;
        // Last N slots form a separate, right-aligned navigation/transfer group.
        public int RightCount;
        // Room kept at the row's left end for its owner's own controls (the
        // vicinity window's family chips): the band still spans the row, the
        // sockets share only what is left.
        public float LeftReserve;
        public float Height { get; private set; } = H;
        private string _pressedName;
        private readonly Image _band;
        private readonly Image[] _sockets = new Image[Sockets];
        private readonly TextMeshProUGUI[] _words = new TextMeshProUGUI[Sockets];
        private readonly Image[] _art = new Image[Sockets];
        private readonly Slot[] _slots = new Slot[Sockets];
        private readonly PointsTag[] _tags = new PointsTag[Sockets];
        private int _hot = -1;
        private int _pressed = -1;
        // A PRESS BEYOND REACH ANSWERS, rather than giving no feedback outside
        // range: the socket flashes, and
        // its owner can say why for as long as RefusedRecently holds.
        private const float RefuseFlash = 0.35f;
        private const float RefuseSay = 1.4f;
        private int _refused = -1;
        private float _refusedAt = -10f;
        public bool RefusedRecently { get { return Time.unscaledTime - _refusedAt < RefuseSay; } }
        public Slot RefusedSlot { get { return _refused >= 0 && _refused < Sockets ? _slots[_refused] : null; } }

        private ActRow(Transform parent, string name)
        {
            Go = Owned.New(name);
            Go.transform.SetParent(parent, false);
            // the row's own rect covers its parent, so Layout's top-left coordinates
            // are the parent's - a fresh RectTransform sits centred at 100x100 and
            // drew the band beside the unit's head and on the well's rail
            var rect = (RectTransform)Go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _band = Theme.KitImage(Go.transform, "Band", "kit_plank_walnut", 18, Color.white);
            _band.raycastTarget = true;   // a click on the band's wood must not reach the world
            for (int i = 0; i < Sockets; i++)
            {
                _sockets[i] = Theme.KitImage(Go.transform, "Socket" + i, "kit_plank_walnut", 18, Color.white);
                _sockets[i].raycastTarget = true;
                var artGo = Owned.New("Art");
                artGo.transform.SetParent(_sockets[i].transform, false);
                _art[i] = artGo.AddComponent<Image>();
                _art[i].raycastTarget = false;
                _art[i].preserveAspect = true;
                _art[i].enabled = false;
                _words[i] = Theme.MakeKitText(_sockets[i].transform, "Word", 8f,
                    Theme.KitCream, TextAlignmentOptions.Center, bold: true);
                _words[i].enableWordWrapping = false;
                _words[i].overflowMode = TextOverflowModes.Overflow;
                _words[i].enableAutoSizing = true;
                _words[i].fontSizeMin = 5.5f;
                _words[i].fontSizeMax = 8f;
            }
        }

        // the stencil's cream, for a word on kit_shed_block's dark carved face
        private static readonly Color CarvedInk = Theme.ParseColor("#F3E7CC", Color.white);
        private static readonly Color EnamelCream = Theme.ParseColor("#F8EBCD", Color.white);

        private static void ShedWord(TextMeshProUGUI word, bool shed)
        {
            TMP_FontAsset stencil = UI.ToolShed.StencilFont;
            if (shed)
            {
                if (stencil != null && word.font != stencil)
                {
                    word.font = stencil;
                    word.fontStyle = FontStyles.Normal;
                    word.characterSpacing = 3f;
                    Theme.FlattenType(word);
                }
            }
            else if (stencil != null && word.font == stencil)
            {
                word.font = Theme.KitFont(true);
                word.fontStyle = FontStyles.Bold;
                word.characterSpacing = 0f;
                Theme.FlattenType(word);
            }
        }

        public static ActRow Create(Transform parent, string name)
        {
            return new ActRow(parent, name);
        }

        // Top-left placement inside the parent, as everything in this UI is placed.
        public void Layout(float x, float y, float w)
        {
            if (IconMode) { LayoutIcons(x, y, w); return; }
            int left = 0, right = 0;
            for (int i = 0; i < Sockets; i++)
            {
                if (_slots[i] == null && (Compact || i >= ShapeSockets)) { continue; }
                if (i >= Sockets - RightCount) { right++; } else { left++; }
            }
            float room = w - LeftReserve;
            int capacity = Mathf.Max(3, Mathf.FloorToInt((room - 2f * PadX) / 34f));
            bool wrap = left + right > capacity;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)left / capacity)) + (wrap && right > 0 ? 1 : 0);
            Height = H + (rows - 1) * (SocketH + SocketGap);
            Theme.Place(_band, x, y, w, Height);
            float sw = Mathf.Min(48f, (room - 2f * PadX - GroupGap - (Mathf.Min(capacity, left + right) - 1) * SocketGap)
                / Mathf.Max(1, Mathf.Min(capacity, left + right)));
            int li = 0, ri = 0;
            for (int i = 0; i < Sockets; i++)
            {
                bool shown = _slots[i] != null || (!Compact && i < ShapeSockets);
                _sockets[i].gameObject.SetActive(shown);
                if (!shown) { continue; }
                bool trailing = i >= Sockets - RightCount;
                int row = trailing ? (wrap ? rows - 1 : 0) : li / capacity;
                float sx = trailing ? x + w - PadX - right * (sw + SocketGap) + SocketGap + ri++ * (sw + SocketGap)
                    : x + PadX + LeftReserve + (li++ % capacity) * (sw + SocketGap);
                Theme.Place(_sockets[i], sx, y + PadY + row * (SocketH + SocketGap), sw, SocketH);
                Theme.Place(_words[i], 0f, 0f, sw, SocketH);
                float ih = SocketH - 2f;
                Theme.Place(_art[i], (sw - ih) * 0.5f, 1f, ih, ih);
            }
        }

        private void LayoutIcons(float x, float y, float w)
        {
            float left = x + PadX, top = y + PadY;
            int occupiedRight = 0;
            for (int i = Sockets - RightCount; i < Sockets; i++)
            {
                if (_slots[i] != null) { occupiedRight = Mathf.Max(occupiedRight, Sockets - i); }
            }
            for (int i = 0; i < Sockets; i++)
            {
                bool shown = _slots[i] != null || (!Compact && i < ShapeSockets);
                _sockets[i].gameObject.SetActive(shown); if (!shown) continue;
                bool door = i >= Sockets - RightCount;
                float sw = door || _slots[i] == null || _slots[i].Icon != null ? 22f : Enamel ? 54f : 48f;
                if (door) { left = x + w - PadX - (Sockets - i) * 25f; top = y + PadY; }
                else if (left + sw > x + w - PadX - occupiedRight * 25f) { left = x + PadX; top += 25f; }
                Theme.Place(_sockets[i], left, top, sw, 22f); Theme.Place(_art[i], (sw - 15f) / 2, 3.5f, 15, 15); Theme.Place(_words[i], 2, 0, sw - 4, 22);
                left += sw + 3;
            }
            float bottom = 25f;
            for (int i = 0; i < Sockets; i++) if (_slots[i] != null) bottom = Mathf.Max(bottom, -_sockets[i].rectTransform.anchoredPosition.y + 22f);
            Height = bottom + PadY; Theme.Place(_band, x, y, w, Height);
        }

        public void Show(bool on)
        {
            if (Go.activeSelf != on)
            {
                Go.SetActive(on);
            }
            if (!on)
            {
                _hot = -1;
                _pressed = -1;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Sockets; i++) { _slots[i] = null; }
        }

        public void Set(int socket, Slot slot)
        {
            if (socket >= 0 && socket < Sockets) { _slots[socket] = slot; }
        }

        public Slot Get(int socket)
        {
            return socket >= 0 && socket < Sockets ? _slots[socket] : null;
        }

        // the first free socket from `from` on, for the acts that take what is left
        public int NextFree(int from)
        {
            for (int i = from; i < Sockets - RightCount; i++)
            {
                if (_slots[i] == null) { return i; }
            }
            return -1;
        }

        public int Hot { get { return _hot; } }

        public void Paint()
        {
            bool anyDown = Input.GetMouseButton(0);
            for (int i = 0; i < Sockets; i++)
            {
                Slot s = _slots[i];
                bool filled = s != null;
                PaintTag(i, s);
                if (Compact || i >= ShapeSockets)
                {
                    bool shown = filled;
                    if (_sockets[i].gameObject.activeSelf != shown) { _sockets[i].gameObject.SetActive(shown); }
                    if (!shown) { continue; }
                }
                bool hot = filled && _hot == i;
                bool down = hot && _pressed == i && anyDown;
                bool lit = filled && s.Lit;
                bool live = filled && s.Live && !s.Faded;   // how it LOOKS; the press asks Live
                // an empty socket is the walnut recess, dim: a place kept, not a button
                string skin = IconMode ? (i >= Sockets - RightCount ? (lit || hot ? "kit_chip_walnut_round" : "kit_chip_walnut_round") : (lit || hot ? "kit_chip_oak" : "kit_chip_walnut")) : !filled ? "kit_plank_walnut" : lit || hot ? "kit_plank_pine" : "kit_plank_oak";
                if (Enamel && IconMode)
                {
                    bool round = i >= Sockets - RightCount || !filled || s.Icon != null;
                    Theme.KitSkin(_sockets[i], round
                        ? lit ? "kit_emblem_socket_on" : hot ? "kit_emblem_socket_hot" : "kit_emblem_socket_idle"
                        : "kit_emblem_plate", round ? 0 : 20);
                }
                else if (Shed && !IconMode)
                {
                    Theme.KitSkin(_sockets[i], "kit_shed_block", 14);
                }
                else
                {
                    Theme.KitSkin(_sockets[i], skin, 18);
                }
                float k = down ? 0.8f : 1f;
                float a = !filled ? 0.55f : live ? 1f : 0.45f;
                _sockets[i].color = new Color(k, k, k, a);
                if (IconMode && lit && !Enamel) { Theme.KitSkin(_sockets[i], "kit_plank_pine", 18); }
                Sprite art = filled ? s.Icon : null;
                if (_art[i].sprite != art) { _art[i].sprite = art; }
                if (_art[i].enabled != (art != null)) { _art[i].enabled = art != null; }
                string word = filled && art == null ? s.Label : string.Empty;
                if (_words[i].text != word) { _words[i].text = word; }
                if (Enamel && word.Length > 0)
                {
                    // The bed's two actions fit as readable two-line labels.
                    // Keep vanilla's localised wording and action identifiers.
                    _words[i].enableWordWrapping = true;
                    _words[i].overflowMode = TextOverflowModes.Ellipsis;
                    _words[i].fontSizeMin = 7f;
                    _words[i].fontSizeMax = 7.5f;
                    if (LabelFont != null && _words[i].font != LabelFont)
                    {
                        _words[i].font = LabelFont;
                        _words[i].fontStyle = FontStyles.Normal;
                        Theme.FlattenType(_words[i]);
                    }
                }
                // OUT OF REACH THE GLYPH DIMS TOO, greyed, as a disabled act: the socket alone dimmed before, and a cream
                // glyph on it stayed at full strength, so a dead act looked alive
                Color ink = filled && s.OwnColors ? Color.white : Enamel ? EnamelCream : lit ? Theme.KitInk : Theme.KitCream;
                _art[i].color = filled && !live ? new Color(ink.r * 0.7f, ink.g * 0.7f, ink.b * 0.7f, 0.4f) : ink;
                _words[i].alpha = a;
                _words[i].color = Enamel ? EnamelCream : lit ? Theme.KitInk : Theme.KitCream;
                if (Shed && !IconMode)
                {
                    // a stencilled word on a panel carved into the crate:
                    // cream on the dark stain, and lifted by hover with the panel
                    // rather than by a lighter wood
                    ShedWord(_words[i], true);
                    _art[i].color = live || !filled ? CarvedInk : new Color(CarvedInk.r * 0.7f, CarvedInk.g * 0.7f, CarvedInk.b * 0.7f, 0.4f);
                    _words[i].color = new Color(CarvedInk.r, CarvedInk.g, CarvedInk.b, a);
                    float lift = hot ? 1.22f * k : k;
                    _sockets[i].color = new Color(lift, lift, lift, a);
                }
                else
                {
                    ShedWord(_words[i], false);
                }
                if (i == _refused && Time.unscaledTime - _refusedAt < RefuseFlash)
                {
                    _sockets[i].color = new Color(1f, 0.5f, 0.45f, 1f);
                }
            }
            if (Enamel) { Theme.KitSkin(_band, "kit_emblem_plate", 20); }
            _band.color = Shed ? Color.clear : Color.white;
        }

        // A slot's points tag: centred on the socket's top-right corner, over the
        // sockets beside it (made last, so it draws after them)
        private void PaintTag(int i, Slot s)
        {
            if (s == null || string.IsNullOrEmpty(s.Tag))
            {
                if (_tags[i] != null) { _tags[i].Hide(); }
                return;
            }
            if (_tags[i] == null || _tags[i].Rect == null)
            {
                _tags[i] = PointsTag.Create(Go.transform, "Points" + i);
            }
            PointsTag tag = _tags[i];
            tag.Show(s.TagKind, s.Tag);
            var r = _sockets[i].rectTransform;
            float right = r.anchoredPosition.x + r.sizeDelta.x;
            float top = -r.anchoredPosition.y;
            Theme.Place(tag.Rect, right - tag.Width * 0.5f - 1f, top - tag.H * 0.5f + 1f, tag.Width, tag.H);
            if (tag.Rect.GetSiblingIndex() != Go.transform.childCount - 1)
            {
                tag.Rect.SetAsLastSibling();
            }
        }

        // The pointer, polled as every surface in this UI is. Returns true while
        // the pointer is over a filled socket, so the owner leaves it alone.
        public bool ReadInput()
        {
            int hover = -1;
            for (int i = 0; i < Sockets; i++)
            {
                if (_slots[i] != null && _sockets[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                        _sockets[i].rectTransform, Input.mousePosition, null))
                {
                    hover = i;
                    break;
                }
            }
            _hot = hover;
            if (Input.GetMouseButtonDown(0) && hover >= 0)
            {
                _pressed = hover;
                _pressedName = _slots[hover].Name;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressed >= 0 && _pressed == hover)
                {
                    Slot s = _slots[hover];
                    if (s != null && s.Name == _pressedName && s.Live && s.Press != null)
                    {
                        s.Press();
                    }
                    else if (s != null && s.Name == _pressedName && !s.Live)
                    {
                        _refused = hover;
                        _refusedAt = Time.unscaledTime;
                    }
                }
                _pressed = -1;
            }
            return hover >= 0;
        }
    }
}
