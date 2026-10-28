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
    // under the grid or the reading. On the target dock (Enamel) the acts are by
    // kind since Forgejo #52: a thing's Purpose leads and its Move and Remove are the
    // card's Manage key, a person's Talk · Carry · Follow lead and Purpose trails.
    //
    // It knows nothing of vanilla: a slot is a name, a label or an icon, whether it
    // is lit and live, and what to do when pressed. The readers (TargetVerbs,
    // PropVerbs) fill it; it draws and answers the pointer.
    internal sealed class ActRow
    {
        // THE KINDS (Forgejo #52, docs/dock-actions.md §2): why an act is on the row.
        // Purpose is why you picked the target - Drink, Craft, Trade, a birth that is
        // due - and wears the brass rim and the gold caption; Character is what every
        // person offers (Talk, Carry, Follow, a body's Dissect). Manage left the row for
        // the card's corner, and the windows are the doors (RightCount).
        internal enum ActKind { Other, Purpose, Character }

        // AUTO-USAGE (Forgejo #119): an act that can also run by itself - the
        // well's Drink, the food box's Eat (AutoUse). A right-click on its socket
        // toggles it; the socket wears AutoLook's gold corners for as long as it has
        // a switch, and its running sparks while the switch is on; Tip is the line
        // the dock's tooltip adds under the act's name.
        internal interface IAuto
        {
            bool On { get; }
            void Toggle();
            string Tip { get; }
        }

        internal sealed class Slot
        {
            public string Name;
            public string Label;
            public Sprite Icon;      // drawn instead of the label when set
            public bool Lit;         // a toggle that is on: pine, ink type
            public bool Live = true; // false: out of reach - dim, and refuses the press
            public bool Faded;       // drawn out of reach, but pressed: a door that arms until you walk up
            public bool OwnColors;   // vanilla's own art (Move, Remove, Repair): drawn as it is, not inked
            public bool Due;         // an act that is due now - the birth: pink enamel, breathing
            public System.Func<bool> Held;   // drawn pressed while it answers true: Talk while its bubble is up
            public System.Func<bool> Open;   // drawn as an open door while it answers true: Craft while crafting is up
            public System.Action Press;
            public string Tag;       // points to spend, on the socket's top-right corner (PointsTag)
            public string Caption;   // the board's word under the tile; the label in capitals when null
            public PointsTag.Kind TagKind;
            public ActKind Kind;
            public IAuto Auto;       // null: the act has no auto-usage, and a right-click does nothing
            public string Look;      // the board's art by another name than Name: a thing's act, "act3", draws as "drink" (Forgejo #130)
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
        // THE ENAMEL DOCK'S TWO SIDES: the acts on the target are square keys on
        // the left; the doors to its windows are small windows on the right, wider
        // than a key, parted from the acts by a groove
        private const float DoorW = 26f;
        private const float GrooveW = 2.5f;
        private const float GrooveRoom = 9f;

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
        // the inventory's leather (look 07, InventoryWindow.Leather.cs): the close
        // key's ivory cut wide, its word in the HUD's serif, no band behind them
        public bool Leather;
        // the vicinity's foot (facelift look 02, InventoryWindow.Vicinity.cs): the
        // leather's painted buttons, each as wide as its word, flush right
        public bool Vicinity;
        // Last N slots form a separate, right-aligned navigation/transfer group.
        public int RightCount;
        // Room kept at the row's left end for its owner's own controls (the
        // vicinity window's family chips): the band still spans the row, the
        // sockets share only what is left.
        public float LeftReserve;
        // the vicinity's verbs: where the leftmost one starts, once laid out
        public float VicLeft { get; private set; }
        public float Height { get; private set; } = H;
        private string _pressedName;
        private readonly Image _band;
        private readonly Image _groove;
        private readonly Image[] _sockets = new Image[Sockets];
        private readonly TextMeshProUGUI[] _words = new TextMeshProUGUI[Sockets];
        private readonly Image[] _art = new Image[Sockets];
        private readonly AnimeCell[] _anime = new AnimeCell[Sockets];
        private readonly Slot[] _slots = new Slot[Sockets];
        private readonly PointsTag[] _tags = new PointsTag[Sockets];
        private int _hot = -1;
        private int _pressed = -1;
        private int _pressedRight = -1;
        private string _pressedRightName;
        private readonly AutoLook[] _autoLooks = new AutoLook[Sockets];
        // A PRESS BEYOND REACH ANSWERS, rather than giving no feedback outside
        // range: the socket flashes, and
        // its owner can say why for as long as RefusedRecently holds.
        private const float RefuseFlash = 0.35f;
        private const float RefuseSay = 1.4f;
        private int _refused = -1;
        private float _refusedAt = -10f;
        public bool RefusedRecently { get { return Time.unscaledTime - _refusedAt < RefuseSay; } }
        public Slot RefusedSlot { get { return _refused >= 0 && _refused < Sockets ? _slots[_refused] : null; } }

        // a piece of the owner's drawn over the band and under the tiles: the target
        // dock's name tab, which a lit tile rises in front of
        public void UnderTiles(Transform piece)
        {
            piece.SetParent(Go.transform, false);
            piece.SetSiblingIndex(_band.transform.GetSiblingIndex() + 1);
        }

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
            _groove = Theme.KitImage(Go.transform, "Groove", "kit_emblem_divider", 0, Color.white);
            _groove.raycastTarget = false;
            _groove.gameObject.SetActive(false);
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
        private static readonly Color LeadGold = Theme.ParseColor("#F4C668", Color.white);

        // the close key's chestnut, for a word on the leather's ivory key
        private static readonly Color LeatherInk = Theme.ParseColor("#432716", Color.black);
        private const float LeatherWordSize = 17f / Theme.KitPpu;

        private static void LeatherWord(TextMeshProUGUI word, bool on)
        {
            TMP_FontAsset serif = IslandUI.InventoryWindow.HudStrongFont;
            if (serif == null)
            {
                return;
            }
            if (on)
            {
                if (word.font != serif)
                {
                    word.font = serif;
                    word.fontStyle = FontStyles.Normal;
                    word.characterSpacing = 0f;
                    Theme.FlattenType(word);
                }
                word.enableAutoSizing = false;
                word.fontSize = LeatherWordSize;
            }
            else if (word.font == serif)
            {
                word.font = Theme.KitFont(true);
                word.fontStyle = FontStyles.Bold;
                word.characterSpacing = 0f;
                word.enableAutoSizing = true;
                Theme.FlattenType(word);
            }
        }

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
            if (Vicinity) { LayoutVicinity(x, y, w); return; }
            int left = 0, right = 0;
            for (int i = 0; i < Sockets; i++)
            {
                if (_slots[i] == null && (Compact || i >= ShapeSockets)) { continue; }
                if (i >= Sockets - RightCount) { right++; } else { left++; }
            }
            float room = w - LeftReserve;
            // on the leather there is no band to keep a margin inside: the keys run
            // to the grid's own edges, flush under its last row
            float padX = Leather ? 0f : PadX;
            float padY = Leather ? 0f : PadY;
            int capacity = Mathf.Max(3, Mathf.FloorToInt((room - 2f * padX) / 34f));
            bool wrap = left + right > capacity;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)left / capacity)) + (wrap && right > 0 ? 1 : 0);
            Height = SocketH + 2f * padY + (rows - 1) * (SocketH + SocketGap);
            Theme.Place(_band, x, y, w, Height);
            float sw = Mathf.Min(48f, (room - 2f * padX - GroupGap - (Mathf.Min(capacity, left + right) - 1) * SocketGap)
                / Mathf.Max(1, Mathf.Min(capacity, left + right)));
            int li = 0, ri = 0;
            for (int i = 0; i < Sockets; i++)
            {
                bool shown = _slots[i] != null || (!Compact && i < ShapeSockets);
                _sockets[i].gameObject.SetActive(shown);
                if (!shown) { continue; }
                bool trailing = i >= Sockets - RightCount;
                int row = trailing ? (wrap ? rows - 1 : 0) : li / capacity;
                float sx = trailing ? x + w - padX - right * (sw + SocketGap) + SocketGap + ri++ * (sw + SocketGap)
                    : x + padX + LeftReserve + (li++ % capacity) * (sw + SocketGap);
                Theme.Place(_sockets[i], sx, y + padY + row * (SocketH + SocketGap), sw, SocketH);
                Theme.Place(_words[i], 0f, 0f, sw, SocketH);
                float ih = SocketH - 2f;
                Theme.Place(_art[i], (sw - ih) * 0.5f, 1f, ih, ih);
            }
        }

        // .frame-button: 39 px, 15 px either side of its word, 7 apart
        private void LayoutVicinity(float x, float y, float w)
        {
            float px = UI.Facelift.Px;
            float h = 39f * px;
            Height = h;
            Theme.Place(_band, x, y, w, h);
            float right = x + w;
            for (int i = Sockets - 1; i >= 0; i--)
            {
                bool shown = _slots[i] != null;
                _sockets[i].gameObject.SetActive(shown);
                if (!shown) { continue; }
                VicinityWord(_words[i]);
                float word = _words[i].GetPreferredValues(_slots[i].Label ?? "").x;
                float sw = word + 30f * px;
                right -= sw;
                Theme.Place(_sockets[i], right, y, sw, h);
                Theme.Place(_words[i], 0f, 0f, sw, h);
                Theme.Place(_art[i], (sw - 29f * px) * 0.5f, (h - 29f * px) * 0.5f, 29f * px, 29f * px);
                right -= 7f * px;
            }
            VicLeft = right + 7f * px;
        }

        // the roster's tab words (7.5 units), not the design's 16 px: the vicinity
        // read small beside the roster it swaps with
        public const float VicWordPx = 24f;

        private static void VicinityWord(TextMeshProUGUI word)
        {
            UI.Facelift.Face(word, true, true, UI.Facelift.VicShade);
            word.enableAutoSizing = false;
            word.fontSize = VicWordPx * UI.Facelift.Px;
            word.alignment = TextAlignmentOptions.Center;
        }

        private void LayoutIcons(float x, float y, float w)
        {
            if (Enamel) { LayoutAnime(x, y, w); return; }
            float left = x + PadX, top = y + PadY;
            int occupiedRight = 0;
            for (int i = Sockets - RightCount; i < Sockets; i++)
            {
                if (_slots[i] != null) { occupiedRight = Mathf.Max(occupiedRight, Sockets - i); }
            }
            float doorW = Enamel ? DoorW : 22f;
            float pitch = doorW + 3f;
            float doorsLeft = x + w - PadX - occupiedRight * pitch + 3f;
            float reserved = occupiedRight > 0 ? occupiedRight * pitch + (Enamel ? GrooveRoom : 0f) : 0f;
            bool groove = Enamel && occupiedRight > 0;
            if (_groove.gameObject.activeSelf != groove) { _groove.gameObject.SetActive(groove); }
            if (groove) { Theme.Place(_groove, doorsLeft - 4f - GrooveW, y + PadY + 1f, GrooveW, 20f); }
            for (int i = 0; i < Sockets; i++)
            {
                bool shown = _slots[i] != null || (!Compact && i < ShapeSockets);
                _sockets[i].gameObject.SetActive(shown); if (!shown) continue;
                bool door = i >= Sockets - RightCount;
                float sw = door ? doorW : _slots[i] == null || _slots[i].Icon != null ? 22f : Enamel ? 54f : 48f;
                if (door) { left = x + w - PadX - (Sockets - i) * pitch + (Enamel ? 3f : 0f); top = y + PadY; }
                else if (left + sw > x + w - PadX - reserved) { left = x + PadX; top += 25f; }
                Theme.Place(_sockets[i], left, top, sw, 22f); Theme.Place(_words[i], 2, 0, sw - 4, 22);
                // a door's glyph sits on the paper below its title strip
                if (door && Enamel) { Theme.Place(_art[i], 7f, 7.6f, 12f, 12f); }
                else { Theme.Place(_art[i], (sw - 15f) / 2, 3.5f, 15, 15); }
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

        // the socket an act stands in, by its name: the Talk bubble's toggle and the
        // focus it gives back
        public RectTransform SocketRect(string name)
        {
            for (int i = 0; i < Sockets; i++)
            {
                if (_slots[i] != null && _slots[i].Name == name) { return _sockets[i].rectTransform; }
            }
            return null;
        }

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
                if (Enamel && IconMode && filled)
                {
                    if (_anime[i] == null) _anime[i] = NewCell(i);
                    bool door = i >= Sockets - RightCount;
                    string tile = BoardTile(s.Look ?? s.Name);
                    // CRAFT WEARS THE CRAFTING BUTTON: a crafter's Craft opens the
                    // same window as the player panel's first button, so it is that
                    // button's painted tile, its hammer and all. A THING'S INVENTORY
                    // WEARS THE VICINITY'S CHEST, the panel's second: a person's is
                    // the backpack (tile_bag), everything else's is a chest
                    bool craft = s.Name == "craft";
                    Sprite panelCut = craft ? PlayerPanelArt.ButtonCut(0)
                        : s.Name == "chest" ? PlayerPanelArt.ButtonCut(1) : null;
                    bool blank = tile == "tile_blank" && panelCut == null;
                    // a due act with no tile of its own (a wreck's Repair) wears the
                    // birth's pink enamel
                    if (s.Due && blank) tile = "tile_due";
                    // a faded door still answers the pointer, a press arms it, but it
                    // looks out of reach like any act: the resting tile, its glyph and
                    // caption dimmed, and the footprints besides (Forgejo #120)
                    bool enabled = s.Live || (door && s.Faded);
                    bool refused = i == _refused && Time.unscaledTime - _refusedAt < RefuseFlash;
                    Sprite glyph = BoardGlyph(s.Look ?? s.Name);
                    _anime[i].PaintTile(panelCut != null ? panelCut : AnimePieces.Get(live ? tile : tile + "_off") ?? AnimePieces.Get(tile),
                        glyph ?? (blank ? s.Icon : null),
                        door && lit || s.Open != null && s.Open(), !door && lit, s.Due, door && s.Faded, enabled, hot, down, s.OwnColors || glyph != null, refused,
                        // no brass rim on Craft: drawn for the board's round-cornered
                        // tiles, it stood off the button's chamfers as flat gold bands;
                        // the gold caption still says it is the purpose
                        glyph != null ? BoardGlyphBox : AnimeCell.GlyphBox, s.Held != null && s.Held(), !door && !craft && s.Kind == ActKind.Purpose,
                        s.Auto != null, s.Auto != null && s.Auto.On);
                    // the button has no resting tile of its own: dimmed, as the panel's
                    if (panelCut != null && !live && !(hot && enabled)) _anime[i].Face.color = new Color(.65f, .65f, .65f, 1f);
                    _art[i].enabled = false;
                    PaintAutoLook(i, null);
                    // an act with neither a board tile nor a glyph: its caption names
                    // it, and the tile carries its initial in the captions' serif, a
                    // monogram rather than the word a second time in the game's face
                    _words[i].text = "";
                    string initial = blank && glyph == null && s.Icon == null && !string.IsNullOrEmpty(s.Label)
                        ? s.Label.Substring(0, 1).ToUpperInvariant() : "";
                    PaintMonogram(i, initial, live);
                    PaintCaption(i, s, live);
                    continue;
                }
                // an empty socket is the walnut recess, dim: a place kept, not a button
                string skin = IconMode ? (i >= Sockets - RightCount ? (lit || hot ? "kit_chip_walnut_round" : "kit_chip_walnut_round") : (lit || hot ? "kit_chip_oak" : "kit_chip_walnut")) : !filled ? "kit_plank_walnut" : lit || hot ? "kit_plank_pine" : "kit_plank_oak";
                if (Enamel && IconMode && i >= Sockets - RightCount)
                {
                    // a door is a small window; its title strip goes green while the window is up.
                    // Out of reach only its paper says so - the frame and the strip stay whole, so
                    // the open/shut strip reads the same near or far
                    Theme.KitSkin(_sockets[i], filled && !live
                        ? lit ? "kit_emblem_door_on_far" : hot ? "kit_emblem_door_hot_far" : "kit_emblem_door_idle_far"
                        : lit ? "kit_emblem_door_on" : hot ? "kit_emblem_door_hot" : "kit_emblem_door_idle", 0);
                }
                else if (Enamel && IconMode)
                {
                    bool key = !filled || s.Icon != null;
                    Theme.KitSkin(_sockets[i], key
                        ? filled && s.Due ? hot ? "kit_emblem_key_due_hot" : "kit_emblem_key_due"
                        : lit ? "kit_emblem_key_on" : hot ? "kit_emblem_key_hot" : "kit_emblem_key_idle"
                        : "kit_emblem_plate", key ? 0 : 20);
                }
                else if (Leather && !IconMode)
                {
                    Theme.KitSkin(_sockets[i], down ? "kit_inv_key_down" : hot ? "kit_inv_key_hot" : "kit_inv_key", 8);
                }
                else if (Vicinity && !IconMode)
                {
                    UI.Facelift.Skin(_sockets[i], UI.Facelift.Id("vicinity-button",
                        UI.Facelift.Pick(!live, down, false, lit, hot)));
                }
                else if (Shed && !IconMode)
                {
                    Theme.KitSkin(_sockets[i], "kit_shed_block", 14);
                }
                else
                {
                    Theme.KitSkin(_sockets[i], skin, 18);
                }
                // (the leather key has a pressed face of its own)
                float k = down && !(Leather && !IconMode) ? 0.8f : 1f;
                // A DUE ACT BREATHES: a slow dip and rise, 1.6 s round, so the one
                // act that is waiting on you cannot sit unseen in the row. Not
                // under the pointer, where hover already answers.
                if (filled && s.Due && !hot && live)
                {
                    float breath = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 1.6f));
                    k *= Mathf.Lerp(0.72f, 1f, breath);
                }
                bool paper = Enamel && IconMode && i >= Sockets - RightCount;
                float a = !filled ? 0.55f : live || paper ? 1f : 0.45f;
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
                Color ink = filled && s.OwnColors ? Color.white : paper ? Theme.KitInk : Enamel ? EnamelCream : lit ? Theme.KitInk : Theme.KitCream;
                _art[i].color = filled && !live ? paper ? new Color(ink.r, ink.g, ink.b, 0.44f) : new Color(ink.r * 0.7f, ink.g * 0.7f, ink.b * 0.7f, 0.4f) : ink;
                _words[i].alpha = a;
                _words[i].color = Enamel ? EnamelCream : lit ? Theme.KitInk : Theme.KitCream;
                if (Vicinity && !IconMode)
                {
                    // the painted button carries its own state and its own fade; the
                    // word is faded to match, never the button twice
                    ShedWord(_words[i], false);
                    VicinityWord(_words[i]);
                    _sockets[i].color = Color.white;
                    float wa = live || !filled ? 1f : 0.45f;
                    _words[i].color = new Color(UI.Facelift.VicFoot.r, UI.Facelift.VicFoot.g, UI.Facelift.VicFoot.b, wa);
                    _art[i].color = new Color(1f, 1f, 1f, wa);
                    // a press sinks the word one pixel; the target stays put
                    _words[i].rectTransform.anchoredPosition = new Vector2(0f, down ? -UI.Facelift.Px : 0f);
                }
                else if (Leather && !IconMode)
                {
                    // the key's word: the HUD's serif in the close's dark chestnut
                    ShedWord(_words[i], false);
                    LeatherWord(_words[i], true);
                    _words[i].color = new Color(LeatherInk.r, LeatherInk.g, LeatherInk.b, a);
                    _art[i].color = live || !filled ? LeatherInk : new Color(LeatherInk.r, LeatherInk.g, LeatherInk.b, 0.4f);
                }
                else if (Shed && !IconMode)
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
                    if (!Enamel)
                    {
                        LeatherWord(_words[i], false);
                    }
                }
                if (i == _refused && Time.unscaledTime - _refusedAt < RefuseFlash)
                {
                    _sockets[i].color = new Color(1f, 0.5f, 0.45f, 1f);
                }
                PaintAutoLook(i, filled ? s.Auto : null);
            }
            // the anime dock's body is the board's own (LayoutAnime); re-skinning it
            // here each frame drew the first pass's rail over it, its frame level
            // with the name tab instead of under it
            if (Enamel && !IconMode) { AnimeHud.Skin(_band, "rail"); }
            _band.color = Shed || Leather || Vicinity ? Color.clear : Color.white;
        }

        // THE BOARD'S TARGET DOCK (art/hud-anime-v1, section 3), in its pixels from the
        // frame's top-left corner: tiles 107 x 111 at a 113 pitch under a 22 px rim, a
        // caption band below each, a 37 px gap holding the divider before the doors,
        // and the painted right end.
        private const float BoardTileW = 107f, BoardTileH = 111f, BoardPitch = 113f, BoardLead = 14f,
            BoardTop = 22f, BoardFrame = 170f, BoardDoorGap = 37f, BoardEnd = 39f,
            BodyEnd = 38f;   // make_anime_hud.py's BODY_END
        private TextMeshProUGUI[] _captions;
        private Image _divider;

        private void CountAnime(out int acts, out int doors)
        {
            acts = doors = 0;
            for (int i = 0; i < Sockets; i++) if (_slots[i] != null) { if (i >= Sockets - RightCount) doors++; else acts++; }
        }

        private static float BoardWidth(int acts, int doors)
        {
            int n = acts + doors;
            return BoardLead + n * BoardPitch - (BoardPitch - BoardTileW) + (acts > 0 && doors > 0 ? BoardDoorGap - (BoardPitch - BoardTileW) : 0f) + BoardEnd;
        }

        // ONE ROW, ALWAYS: two rows break the board's design, so the plank is as wide
        // as its acts and doors need (InventoryWindow.LayoutUnit) and never wraps
        public float AnimeWidth
        {
            get
            {
                CountAnime(out int acts, out int doors);
                return BoardWidth(acts, doors) / AnimePieces.Px;
            }
        }

        private void LayoutAnime(float x,float y,float w)
        {
            CountAnime(out int acts, out int doors);
            float px = AnimePieces.Px;
            Height = BoardFrame / px;
            // the body's painting starts 26 px above its frame (the leaves' room) and
            // ends 4 px below, and its left end reaches BodyEnd px behind the card,
            // whose chamfer shows the end's lower corner (Forgejo #48); a 9-slice, so
            // it grows left - its corners' chamfers and the right end's leaves in the
            // borders, which do not stretch
            AnimePieces.Sliced(_band, "target_body", new Vector4(48, 70, 43, 64));
            Theme.Place(_band, x - BodyEnd / px, y - 26f / px, w + BodyEnd / px, Height + 30f / px);
            int a = 0, d = 0;
            // the doors lead, beside the portrait; the acts follow the divider
            float doorsRight = BoardLead + doors * BoardPitch - (BoardPitch - BoardTileW);
            float actsLeft = doors > 0 ? doorsRight + BoardDoorGap : BoardLead;
            for (int i = 0; i < Sockets; i++)
            {
                bool shown = _slots[i] != null; _sockets[i].gameObject.SetActive(shown); if (!shown) continue;
                bool door = i >= Sockets - RightCount;
                // the doors in reverse socket order: Inventory first, then Character
                float sx = door ? BoardLead + (doors - 1 - d++) * BoardPitch : actsLeft + a++ * BoardPitch, sy = BoardTop;
                if (_anime[i] == null) _anime[i] = NewCell(i);
                _anime[i].Place(x + sx / px, y + sy / px, BoardTileW / px, BoardTileH / px);
                Theme.Place(_words[i], 1, 1, BoardTileW / px - 2, BoardTileH / px - 2);
                Caption(i).gameObject.SetActive(true);
                Theme.Place(Caption(i), x + (sx - 3f) / px, y + (sy + BoardTileH - 4f) / px, (BoardTileW + 6f) / px, 32f / px);
            }
            for (int i = 0; i < Sockets; i++) if (_slots[i] == null && _captions != null && _captions[i] != null) _captions[i].gameObject.SetActive(false);
            _groove.gameObject.SetActive(false);
            if (_divider == null) { _divider = AnimePieces.Image(Go.transform, "Divider", "divider"); }
            _divider.gameObject.SetActive(doors > 0 && acts > 0);
            Theme.Place(_divider, x + (actsLeft - BoardDoorGap * .5f - 2f) / px, y + (BoardTop + 20f) / px, 4f / px, 76f / px);
        }

        // the harness's state preview (InventoryWindow.PreviewHudStates): the first
        // act hovered, the second pressed, the sheet's door focused, the bag's refused
        private AnimeCell NewCell(int i)
        {
            return new AnimeCell(_sockets[i]) { Role = i == 0 ? 1 : i == 1 ? 2 : i == Sockets - 2 ? 4 : i == Sockets - 1 ? 3 : 0 };
        }

        private TextMeshProUGUI[] _monograms;

        private void PaintMonogram(int i, string initial, bool enabled)
        {
            if (_monograms == null) _monograms = new TextMeshProUGUI[Sockets];
            var t = _monograms[i];
            if (initial.Length == 0) { if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false); return; }
            if (t == null)
            {
                // a child of the tile's face, so it lifts and sinks with it
                t = Theme.MakeKitText(_anime[i].Face.transform, "Monogram", 12f, EnamelCream, TextAlignmentOptions.Center, bold: true);
                t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
                CaptionFit(t);
                t.enableAutoSizing = false; t.fontSize = 12f; t.outlineWidth = .16f;
                var r = t.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                _monograms[i] = t;
            }
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            if (t.text != initial) t.text = initial;
            t.color = enabled ? EnamelCream : new Color(.62f, .6f, .57f, .8f);
        }

        private TextMeshProUGUI Caption(int i)
        {
            if (_captions == null) _captions = new TextMeshProUGUI[Sockets];
            if (_captions[i] != null) return _captions[i];
            var t = Theme.MakeKitText(Go.transform, "Caption" + i, 3.6f, EnamelCream, TextAlignmentOptions.Center, bold: true);
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
            CaptionFit(t);
            return _captions[i] = t;
        }

        // The board's words under its tiles: the doors are named for their windows
        private void PaintCaption(int i, Slot s, bool live)
        {
            var t = Caption(i);
            string word = s.Caption ?? (s.Label ?? s.Name).ToUpperInvariant();
            if (t.text != word) t.text = word;
            // vanilla's longer acts (a bed's SLEEP TILL MORNING beside SLEEP TILL
            // NIGHT) ran into each other on one line at the smallest size: a
            // caption of several words wraps at its spaces instead
            bool words = word.IndexOf(' ') >= 0;
            if (t.enableWordWrapping != words) t.enableWordWrapping = words;
            // a longer language than the English the tiles were drawn for may shrink further
            float least = Shared.Loc.Language == "en" ? 2.6f : 2.1f;
            if (t.fontSizeMin != least) t.fontSizeMin = least;
            // Purpose's caption in the rim's gold
            Color ink = s.Kind == ActKind.Purpose && i < Sockets - RightCount ? LeadGold : EnamelCream;
            t.color = new Color(ink.r, ink.g, ink.b, live ? 1f : .55f);
        }

        // The board's captions: condensed serif capitals, cream with a dark edge, at
        // the board's cap height (12 of its pixels). A word too long for its tile
        // still steps down rather than meeting its neighbour.
        internal static void CaptionFit(TextMeshProUGUI t)
        {
            InventoryWindow.SetCaptionFont(t);
            t.characterSpacing = 0f;
            // a longer language than the English the tiles were drawn for may shrink further
            t.enableAutoSizing = true; t.fontSizeMin = Shared.Loc.Language == "en" ? 2.6f : 2.1f; t.fontSizeMax = 3.5f;
            t.outlineWidth = .2f; t.outlineColor = new Color32(26, 16, 12, 255);
        }

        // the board's painted tile for an act, or a blank tile for the game's glyph
        private static string BoardTile(string name)
        {
            switch (AnimeHud.Canonical(name))
            {
                case "TalkButton": return "tile_blank";
                case "CarryButton": return "tile_blank";
                case "FollowButton": return "tile_follow";
                case "BirthButton": return "tile_birth";
                case "details": return "tile_details";
                case "bag": return "tile_bag";
                default: return "tile_blank";
            }
        }

        // THE PARCHMENT SET'S GLYPHS (art/ref/talk-carry-parchment-v1, Forgejo #58):
        // ivory pictograms drawn on the blank tile in their own two tones - Carry's
        // brown separation stroke is what keeps the two figures apart, so it is never
        // tinted. Talk's is the overlapping bubbles: the single outlined bubble the old
        // tile wore now means a choice the bubble does not know. Their box is the
        // design's 38 px of a 52 px tile, where the game's glyphs on the blank tile keep
        // AnimeCell's smaller one.
        // A THING'S COMMON PURPOSE ACTS wear the same set's glyphs (Forgejo #130,
        // make_purpose_glyphs.py) where they wore their initial: the touch by its
        // name, sleep or use, vanilla's acts by the look the thing's dock gives them
        // (InventoryWindow.PurposeLook). The rarer ones keep the letter.
        private const float BoardGlyphBox = .72f;

        private static Sprite BoardGlyph(string name)
        {
            switch (AnimeHud.Canonical(name))
            {
                case "TalkButton": return AnimePieces.Get("glyph_talk");
                case "CarryButton": return AnimePieces.Get("glyph_carry");
                case "drink": case "eat": case "meat":
                case "sleep": case "sleep_morning": case "sleep_night":
                case "use": case "enter": case "peek":
                case "place":   // a person's Set place (Forgejo #166)
                    return AnimePieces.Get("glyph_" + name);
                default: return null;
            }
        }

        // A plank socket's autocast look (the dock's tile draws its own, AnimeCell):
        // AutoLook over the whole socket, on top of its art and word, scaled as a
        // board tile of the socket's shorter side
        private void PaintAutoLook(int i, IAuto auto)
        {
            if (auto == null)
            {
                _autoLooks[i]?.Hide();
                return;
            }
            if (_autoLooks[i] == null)
            {
                _autoLooks[i] = new AutoLook(_sockets[i].transform, _sockets[i].transform.childCount);
            }
            Vector2 size = _sockets[i].rectTransform.sizeDelta;
            _autoLooks[i].Paint(true, auto.On, 0f, 0f, size.x, size.y, Mathf.Min(size.x, size.y) / BoardTileW);
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
                _tags[i] = PointsTag.Create(Go.transform, "Points" + i, Enamel);
            }
            PointsTag tag = _tags[i];
            tag.Show(s.TagKind, s.Tag, Enamel ? PointsTag.DockPill : PointsTag.Height);
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
            // the right button toggles an act's auto-usage, near or far: it is the
            // player's standing order, not the act itself
            if (Input.GetMouseButtonDown(1) && hover >= 0)
            {
                _pressedRight = hover;
                _pressedRightName = _slots[hover].Name;
            }
            if (Input.GetMouseButtonUp(1))
            {
                Slot s = hover >= 0 ? _slots[hover] : null;
                if (_pressedRight >= 0 && _pressedRight == hover && s != null && s.Name == _pressedRightName && s.Auto != null)
                {
                    s.Auto.Toggle();
                }
                _pressedRight = -1;
            }
            return hover >= 0;
        }
    }
}
