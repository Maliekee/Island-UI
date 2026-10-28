using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // Shared anime HUD artwork. Frames are generated at fixed texel sizes and
    // sliced, so their chamfers and ink outlines survive arbitrary rail lengths.
    internal static class AnimeHud
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Texture2D _atlas, _frames;
        public const float CellSize = 25f;
        public static readonly Color Cream = Hex("F6ECD8");
        public static readonly Color Jade = Hex("61AE8B");
        public static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out Color c); return c; }

        public static Sprite Icon(string name)
        {
            name = Canonical(name);
            string[] keys = { "craft", "vicinity", "roster", "map", "quests", "tools", "details", "bag",
                "settings", "TalkButton", "CarryButton", "FollowButton", "BirthButton", "food", "water", "leaf" };
            int n = System.Array.IndexOf(keys, name);
            if (n < 0) return null;
            string key = "icon/" + name;
            if (Sprites.TryGetValue(key, out Sprite result)) return result;
            if (_atlas == null)
            {
                using (Stream stream = typeof(AnimeHud).Assembly.GetManifestResourceStream("IslandUI.AnimeIcons.png"))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[stream.Length];
                    int offset = 0, count;
                    while (offset < bytes.Length && (count = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += count;
                    _atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _atlas.LoadImage(bytes);
                    _atlas.wrapMode = TextureWrapMode.Clamp;
                    _atlas.filterMode = FilterMode.Bilinear;
                }
            }
            int w = _atlas.width / 4, h = _atlas.height / 4;
            // Trim transparent gutters at sprite creation, leaving the source intact.
            int x0 = (n % 4) * w, y0 = (3 - n / 4) * h;
            int loX = w, loY = h, hiX = 0, hiY = 0;
            for (int y = 4; y < h - 4; y++) for (int x = 4; x < w - 4; x++)
                if (_atlas.GetPixel(x0 + x, y0 + y).a > .15f)
                { loX = Mathf.Min(loX, x); hiX = Mathf.Max(hiX, x); loY = Mathf.Min(loY, y); hiY = Mathf.Max(hiY, y); }
            Rect rect = hiX > loX ? new Rect(x0 + loX - 2, y0 + loY - 2, hiX - loX + 5, hiY - loY + 5) : new Rect(x0, y0, w, h);
            result = Sprite.Create(_atlas, rect, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            result.name = key; Sprites[key] = result; return result;
        }

        public static string Material(string name)
        {
            name = Canonical(name);
            if (name == "craft") return "blue";
            if (name == "vicinity" || name == "bag") return "amber";
            if (name == "roster" || name == "details") return "paper";
            if (name == "BirthButton") return "rose";
            return "tile";
        }
        public static string Canonical(string name)
        {
            switch (name)
            {
                case "talk": return "TalkButton";
                case "carry": return "CarryButton";
                case "follow": case "goodbye": return "FollowButton";
                case "delivery": return "BirthButton";
                default: return name;
            }
        }

        public static Sprite Gauge(string name, bool rim)
        {
            string key = name + (rim ? "/rim" : "/empty");
            if (Sprites.TryGetValue(key, out Sprite result)) return result;
            Sprite source = Icon(name); if (source == null) return null;
            Rect r = source.rect; int w = (int)r.width, h = (int)r.height;
            Color[] input = source.texture.GetPixels((int)r.x, (int)r.y, w, h);
            var output = new Color[input.Length];
            Color neutral = Hex("756C5D");
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                Color c = input[y * w + x];
                if (!rim)
                {
                    float l = c.grayscale;
                    output[y * w + x] = new Color(neutral.r * (.55f + .45f * l), neutral.g * (.55f + .45f * l), neutral.b * (.55f + .45f * l), c.a);
                }
                else
                {
                    bool edge = false;
                    for (int dy = -2; dy <= 2 && !edge; dy++) for (int dx = -2; dx <= 2; dx++)
                    { int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h || input[yy * w + xx].a < .1f) { edge = true; break; } }
                    output[y * w + x] = edge ? c : Color.clear;
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels(output); t.Apply();
            result = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            Sprites[key] = result; return result;
        }

        public static Sprite Shape(string kind)
        {
            if (Sprites.TryGetValue(kind, out Sprite result)) return result;
            int frame = System.Array.IndexOf(new[] { "rail", "blue", "amber", "paper", "rose", "tile" }, kind);
            if (frame >= 0) return PaintedFrame(kind, frame);
            const int w = 128, h = 96;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            Color body = Hex(kind == "blue" ? "326D97" : kind == "amber" ? "A86B36" : kind == "paper" ? "E5D3A7" : kind == "rose" ? "97566C" : kind == "hp" ? "E44862" : kind == "stun" ? "F6EACB" : kind == "mask" ? "FFFFFF" : kind == "track" ? "262126" : kind == "coin" ? "D7B678" : "49392F");
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float diagonal = Mathf.Min(Mathf.Min(x + y, (w - 1 - x) + y), Mathf.Min(x + (h - 1 - y), (w - 1 - x) + (h - 1 - y))) - 10;
                float d = Mathf.Min(edge, diagonal * .7071f);
                Color c = Color.clear;
                bool plain = kind == "hp" || kind == "stun" || kind == "mask";
                if (d >= 0)
                {
                    if (plain) c = body;
                    else if (kind == "recess") c = Hex("251E1B");
                    else if (d < 2) c = Hex("241B18");
                    else if (d < 4) c = Hex("C7A765");
                    else if (d < 6) c = Hex("6D533A");
                    else if (d < 7) c = Hex("30251F");
                    else c = body;
                    if (kind == "portrait-border" && d > 7) c = Color.clear;
                    if (d > 7 && kind != "mask") c = Color.Lerp(c, y > h * .7f ? Color.white : Color.black, y > h * .7f ? .045f : .045f);
                    c.a *= Mathf.Clamp01(d + 0.5f);
                }
                pixels[y * w + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply();
            result = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            result.name = "anime/" + kind; Sprites[kind] = result; return result;
        }

        private static Sprite PaintedFrame(string kind, int index)
        {
            if (_frames == null)
            {
                using (var stream = typeof(AnimeHud).Assembly.GetManifestResourceStream("IslandUI.AnimeFrames.png"))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory); _frames = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _frames.LoadImage(memory.ToArray()); _frames.wrapMode = TextureWrapMode.Clamp;
                }
            }
            int w = _frames.width / 3, h = _frames.height / 2, x0 = index % 3 * w, y0 = (1 - index / 3) * h;
            int lx = w, ly = h, hx = 0, hy = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (_frames.GetPixel(x0 + x, y0 + y).a > .3f)
            { lx = Mathf.Min(lx, x); ly = Mathf.Min(ly, y); hx = Mathf.Max(hx, x); hy = Mathf.Max(hy, y); }
            var rect = new Rect(x0 + lx, y0 + ly, hx - lx + 1, hy - ly + 1);
            var result = Sprite.Create(_frames, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(70, 70, 70, 70));
            result.name = "anime/" + kind; Sprites[kind] = result; return result;
        }

        public static Sprite Mark(string kind)
        {
            if (Sprites.TryGetValue(kind, out Sprite result)) return result;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                bool mark = false; Color c = Hex("E8B65B");
                if (kind == "feet")
                {
                    mark = Mathf.Pow((x - 21) / 7f, 2) + Mathf.Pow((y - 26) / 12f, 2) < 1 || Mathf.Pow((x - 43) / 7f, 2) + Mathf.Pow((y - 35) / 12f, 2) < 1;
                    for (int i = 0; i < 3; i++) mark |= Vector2.Distance(new Vector2(x, y), new Vector2(16 + i * 5, 42)) < 3 || Vector2.Distance(new Vector2(x, y), new Vector2(38 + i * 5, 51)) < 3;
                }
                else if (kind == "check")
                {
                    mark = (new Vector2(x - 32, y - 32)).sqrMagnitude < 28 * 28; c = Jade;
                    if (LineDistance(new Vector2(x, y), new Vector2(16, 32), new Vector2(27, 21)) < 3 || LineDistance(new Vector2(x, y), new Vector2(27, 21), new Vector2(48, 44)) < 3) c = Cream;
                }
                else if (kind == "left" || kind == "up")
                {
                    Vector2 p = new Vector2(x, y);
                    Vector2 a = kind == "left" ? new Vector2(40, 13) : new Vector2(13, 24);
                    Vector2 b = kind == "left" ? new Vector2(21, 32) : new Vector2(32, 43);
                    Vector2 z = kind == "left" ? new Vector2(40, 51) : new Vector2(51, 24);
                    mark = LineDistance(p, a, b) < 4 || LineDistance(p, b, z) < 4; c = Cream;
                }
                else mark = Mathf.Sqrt(Mathf.Abs(x - 32) / 27f) + Mathf.Sqrt(Mathf.Abs(y - 32) / 31f) < 1;
                pixels[y * 64 + x] = mark ? c : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(); result = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100);
            Sprites[kind] = result; return result;
        }
        private static float LineDistance(Vector2 p, Vector2 a, Vector2 b)
        { Vector2 ab = b - a; return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)); }

        public static void Reset()
        {
            var textures = new HashSet<Texture2D>();
            foreach (var s in Sprites.Values) if (s != null) { textures.Add(s.texture); Object.Destroy(s); }
            foreach (var t in textures) if (t != null) Object.Destroy(t);
            Sprites.Clear(); _atlas = _frames = null;
        }

        public static void Skin(Image image, string kind)
        {
            image.sprite = Shape(kind == "portrait-border" ? "rail" : kind); image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false;
            image.fillCenter = kind != "portrait-border";
            image.pixelsPerUnitMultiplier = image.sprite.texture == _frames ? 20f : 3.2f; image.color = Color.white;
        }
        public static Image Image(Transform parent, string name, string kind)
        {
            var go = Owned.New(name); go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>(); Skin(image, kind); image.raycastTarget = false; return image;
        }
        public static Image Art(Transform parent, string name, string key)
        {
            var image = Image(parent, name, "mask"); image.sprite = Icon(key); image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true; return image;
        }
        public static void Leaves(Transform parent, float x, float y, float w = 15f)
        {
            var leaf = Art(parent, "Leaves", "leaf"); Theme.Place(leaf, x, y, w, w * .7f);
        }
    }

    internal sealed class AnimeCell
    {
        public readonly Image Hit, Face, Icon;
        private readonly Image _line, _focus, _arrow, _rim, _lead;
        private static bool _keyboard;
        private readonly Image _state, _distance;
        private AutoLook _auto;
        private float _w = 25, _h = 25;
        public AnimeCell(Image hit)
        {
            Hit = hit; AnimeHud.Skin(hit, "recess"); hit.raycastTarget = true;
            // the lead's brass rim first, so the tile covers its inner edge
            _lead = AnimePieces.Image(hit.transform, "Lead", "rim_lead");
            _lead.raycastTarget = false; _lead.enabled = false;
            Face = AnimeHud.Image(hit.transform, "Face", "tile");
            Icon = AnimeHud.Art(Face.transform, "Illustration", "bag");
            _rim = AnimePieces.Image(hit.transform, "Rim", "rim_on");
            _rim.enabled = false;
            _line = AnimeHud.Image(hit.transform, "Intent", "stun");
            _state = AnimeHud.Image(hit.transform, "State", "mask");
            _distance = AnimeHud.Image(hit.transform, "Reach", "mask");
            _state.type = _distance.type = Image.Type.Simple;
            _distance.sprite = AnimeHud.Mark("feet");
            _state.raycastTarget = _distance.raycastTarget = false;
            _focus = AnimeHud.Image(hit.transform, "Focus", "portrait-border");
            _arrow = AnimeHud.Image(hit.transform, "Expansion", "mask"); _arrow.type = Image.Type.Simple;
        }
        public void Place(float x, float y, float w, float h)
        { Theme.Place(Hit, x, y, w, h); _w = w; _h = h; }

        // THE STATES, in the board's own marks (section 5): open is the "On" rim -
        // amber, cream, amber - with the underline, and the button lifted over the
        // frame as the panel's are; the pointer over a button is the same rim,
        // faint; pressed sinks it and lowers its light; keyboard focus is a cream
        // ring standing off the tile; a press refused out of reach flashes the rim
        // red. A colour above white did nothing for hover: UI vertex colours clamp.
        private const float Lift = 2.2f;
        private static readonly Color RimOpen = Color.white, RimHot = new Color(1f, 1f, 1f, .5f),
            RimRefused = new Color(1f, .32f, .28f, 1f), Pressed = new Color(.84f, .82f, .8f, 1f);

        // Harness only: for PreviewUntil, a cell with a Role is drawn hovered (1),
        // pressed (2), refused (3) or keyboard-focused (4) - presentation, no input.
        internal static float PreviewUntil;
        internal int Role;

        // the keyboard's ring shown on whatever is selected: focus handed back by a
        // surface that took the keys while it was up (TalkBubble's Escape)
        internal static void KeyboardFocus()
        {
            _keyboard = true;
        }

        // ...and HELD is an act whose panel is up from it (Talk while its bubble is):
        // sunk as pressed, in the open rim's amber
        private void PaintStates(bool open, bool hot, bool down, bool refused, bool held = false)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) _keyboard = false;
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) _keyboard = true;
            bool preview = Role != 0 && Time.unscaledTime < PreviewUntil;
            if (preview) { hot = Role == 1; down = Role == 2; refused = Role == 3; }
            Face.color = down || held ? Pressed : Color.white;
            Theme.Place(Face, 0, open && !down ? -Lift : down || held ? .25f : 0, _w, _h);
            _line.enabled = open && !down; _line.color = AnimeHud.Cream;
            Theme.Place(_line, 2.5f, _h - Lift + .55f, _w - 5, .8f);
            _rim.enabled = refused || open || held || hot && !down;
            if (_rim.enabled)
            {
                AnimePieces.Sliced(_rim, "rim_on", new Vector4(24, 24, 24, 24));
                _rim.color = refused ? RimRefused : open || held ? RimOpen : RimHot;
                Theme.Place(_rim, -.2f, open && !down ? -Lift - .2f : -.2f, _w + .4f, _h + .4f);
            }
            var es = UnityEngine.EventSystems.EventSystem.current;
            _focus.enabled = preview ? Role == 4 : _keyboard && es != null && es.currentSelectedGameObject == Hit.gameObject;
            if (_focus.enabled)
            {
                AnimePieces.Sliced(_focus, "ring_focus", new Vector4(24, 24, 24, 24));
                _focus.color = Color.white;
                Theme.Place(_focus, -1f, open && !down ? -Lift - 1f : -1f, _w + 2f, _h + 2f);
            }
        }

        public void PaintPanel(Sprite art, bool on, bool hot, bool down)
        {
            Hit.color=Color.clear;
            Face.sprite=art;Face.type=Image.Type.Simple;Face.fillCenter=true;
            Icon.enabled=_state.enabled=_distance.enabled=_arrow.enabled=false; _auto?.Hide();
            PaintStates(on, hot, down, false);
        }
        // A painted board tile (AnimePieces): the act's own tile, or the blank one
        // with the game's glyph on it. Marks sit where the board paints them: the
        // follow check and the birth sparkle over the top-right corner, the
        // footprints of an out-of-reach door over the bottom-right.
        // `tile` is already the resting (_off) tile when the act is out of reach.
        // `box` is the glyph's share of the tile, centred.
        public const float GlyphBox = .6f;

        // `lead` is Purpose's mark (Forgejo #52): the brass rim and its glow round the
        // tile, LeadRoom board px out on every side, riding with it; dimmed out of reach
        private const float LeadRoom = 12f;

        // `canAuto` is an act that can run by itself and `autoOn` one that does
        // (Forgejo #119, AutoUse): AutoLook's gold corners and its sparks running
        // round the edge, part of the tile's frame - over its rim, under the marks -
        // and at full strength out of reach too: a standing order, not the act's
        // availability. Made on the first tile that needs it.
        public void PaintTile(Sprite tile, Sprite glyph, bool open, bool check, bool spark, bool far, bool enabled, bool hot, bool down, bool ownColors = false, bool refused = false, float box = GlyphBox, bool held = false, bool lead = false, bool canAuto = false, bool autoOn = false)
        {
            Hit.color = Color.clear;
            Face.sprite = tile; Face.type = Image.Type.Simple;
            held &= enabled;
            // a far door answers the pointer but is drawn as out of reach as any act
            bool dim = !enabled || far;
            PaintStates(open, hot && enabled, down && enabled, refused, held);
            Icon.sprite = glyph; Icon.enabled = glyph != null;
            Icon.color = dim ? new Color(.62f, .6f, .57f, .8f) : ownColors ? Color.white : AnimeHud.Cream;
            Theme.Place(Icon, _w * (1f - box) * .5f, _h * (1f - box) * .5f, _w * box, _h * box);
            float k = _w / 107f;
            // the marks ride with the tile when it lifts or sinks
            float dy = open && !(down && enabled) ? -Lift : (down && enabled) || held ? .25f : 0f;
            _state.enabled = check || (spark && enabled);
            _state.sprite = AnimePieces.Get(check ? "mark_check" : "mark_spark"); _state.color = Color.white;
            if (check) Theme.Place(_state, 64 * k, -9 * k + dy, 50 * k, 50 * k);
            else Theme.Place(_state, 72 * k, -6 * k + dy, 50 * k, 54 * k);
            _distance.enabled = far; _distance.sprite = AnimePieces.Get("mark_feet"); _distance.color = Color.white;
            Theme.Place(_distance, 74 * k, 66 * k + dy, 40 * k, 48 * k);
            _arrow.enabled = false;
            if (canAuto && _auto == null) _auto = new AutoLook(Hit.transform, _rim.transform.GetSiblingIndex() + 1);
            _auto?.Paint(canAuto, autoOn, 0, dy, _w, _h, k);
            _lead.enabled = lead;
            if (lead)
            {
                float r = LeadRoom * _w / 107f;
                Theme.Place(_lead, -r, -r + dy, _w + 2f * r, _h + 2f * r);
                _lead.color = !dim ? Color.white : new Color(.7f, .7f, .7f, .5f);
            }
        }

        public void Paint(string name, Sprite fallback, bool on, bool toggle, bool far, bool enabled, bool hot, bool down, bool due, bool expanded = false)
        {
            AnimeHud.Skin(Face, AnimeHud.Material(name));
            float lift = on ? -1.2f : 0f;
            Theme.Place(Face, 1, 1 + lift + (down ? .4f : 0), _w - 2, _h - 2);
            Theme.Place(Icon, 3, 3, _w - 8, _h - 8);
            var art = AnimeHud.Icon(name); Icon.sprite = art ?? fallback; Icon.enabled = Icon.sprite != null;
            Icon.color = art != null ? Color.white : AnimeHud.Cream;
            Face.color = enabled ? (hot ? new Color(1.13f, 1.13f, 1.13f, 1) : Color.white) : new Color(.65f, .65f, .65f, 1);
            if (!enabled) Icon.color = new Color(.7f, .7f, .7f, .55f);
            _line.enabled = on || toggle || expanded; Theme.Place(_line, 5, _h - 1, _w - 10, 1.2f); _line.color = toggle ? AnimeHud.Jade : AnimeHud.Cream;
            _state.enabled = toggle || (due && enabled); _state.sprite = AnimeHud.Mark(toggle ? "check" : "spark");
            _state.color = Color.white;
            Theme.Place(_state, _w - 7, -3, 9, 9);
            _distance.enabled = far; Theme.Place(_distance, _w - 9, _h - 11, 10, 10);
            _arrow.enabled = name == "tools" || name == "settings";
            if (_arrow.enabled)
            {
                _arrow.sprite = AnimeHud.Mark(name == "tools" ? "left" : "up");
                Theme.Place(_arrow, name == "tools" ? -2 : (_w - 6) * .5f, name == "tools" ? (_h - 6) * .5f : -2, 6, 6);
                _arrow.rectTransform.localScale = new Vector3(expanded ? -1 : 1, expanded ? -1 : 1, 1);
            }
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) _keyboard = false;
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) _keyboard = true;
            var es = UnityEngine.EventSystems.EventSystem.current;
            _focus.enabled = _keyboard && es != null && es.currentSelectedGameObject == Hit.gameObject;
            Theme.Place(_focus, -1, -1, _w + 2, _h + 2); _focus.color = AnimeHud.Cream;
        }
    }
}
