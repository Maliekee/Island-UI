using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // A fixed-composition painted panel. Coordinates use the artwork's 2048-wide
    // design space; only the dynamic interiors are removed from its empty skin.
    internal static class PlayerPanelArt
    {
        public const float Scale = 174f / 2048f;
        public const float Split = 342f;
        private static Texture2D _source, _empty;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Rect[] Buttons = {
            new Rect(141,74,275,254), new Rect(438,74,281,254), new Rect(741,74,264,254),
            new Rect(1125,74,399,254), new Rect(1558,74,316,254) };
        private static readonly Rect Health = new Rect(304, 380, 1241, 77);
        private static readonly Rect Stun = new Rect(322, 516, 1198, 44);
        private static readonly Rect Food = new Rect(1585, 465, 158, 161);
        private static readonly Rect Water = new Rect(1785, 460, 121, 153);
        public static Rect ButtonRect(int index) => Buttons[index];

        private static void Ensure()
        {
            if (_source != null) return;
            using (Stream stream = typeof(PlayerPanelArt).Assembly.GetManifestResourceStream("IslandUI.PlayerPanelV2.png"))
            {
                byte[] data = new byte[stream.Length];
                int offset = 0, read;
                while (offset < data.Length && (read = stream.Read(data, offset, data.Length - offset)) > 0) offset += read;
                _source = AnimePieces.Painted(data, "PlayerPanelV2");
            }
            Color[] skin = _source.GetPixels();
            ExtractFill("health", Health, skin);
            ExtractFill("stun", Stun, skin);
            ExtractFill("food", Food, skin);
            ExtractFill("water", Water, skin);
            for (int i = 0; i < 3; i++) ExtractFill("heart" + i, new Rect(1588 + 125 * i, 365, 112, 99), skin);
            // An open button lifts out of its slot, so the skin beneath shows the
            // slot's recess (copied row by row from the gap after the first button).
            int gap = Mathf.RoundToInt(427 * _source.width / 2048f);
            foreach (Rect b in Buttons)
            {
                Rect r = Pixels(b);
                for (int y = (int)r.y; y < (int)(r.y + r.height); y++)
                    for (int x = (int)r.x; x < (int)(r.x + r.width); x++) skin[y * _source.width + x] = skin[y * _source.width + gap];
            }
            _empty = AnimePieces.Painted(Pixels32(skin), _source.width, _source.height, "PlayerPanelV2/empty");
        }

        private static Rect Pixels(Rect r)
        {
            float k = _source.width / 2048f;
            return new Rect(Mathf.Round(r.x * k), Mathf.Round(_source.height - (r.y + r.height) * k), Mathf.Round(r.width * k), Mathf.Round(r.height * k));
        }

        private static void ExtractFill(string key, Rect area, Color[] skin)
        {
            Rect r = Pixels(area); int w = (int)r.width, h = (int)r.height;
            Color[] fill = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int at = ((int)r.y + y) * _source.width + (int)r.x + x;
                Color c = skin[at];
                // Colour segmentation retains the painted outlines and dark gaps.
                bool inside = key == "health" ? (c.r > c.g * 1.45f && c.r > c.b * 1.2f) || (c.r > .75f && c.b > .6f && c.g > .4f)
                    : key == "stun" ? c.r > .65f && c.g > .50f
                    : key == "water" ? c.b > c.r * 1.5f && c.g > c.r * 1.35f
                    : key.StartsWith("heart") ? c.r > .75f && c.g > .65f && c.b > .45f && InHeart(x, h - 1 - y, w, h)
                    : c.r > c.b * 2f && c.g > c.b * 1.3f && c.r > .55f;
                if (!inside) continue;
                fill[y * w + x] = key.StartsWith("heart") ? HealthRed(c, y / (h - 1f)) : c;
                float l = .105f + c.grayscale * .055f;
                skin[at] = new Color(l * 1.1f, l, l * .97f, c.a);
            }
            var texture = AnimePieces.Painted(Pixels32(fill), w, h, "PlayerPanelV2/" + key);
            Cache[key] = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

        private static Color32[] Pixels32(Color[] pixels)
        {
            var result = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) result[i] = pixels[i];
            return result;
        }

        // A heart's rect reaches into the food and water outlines' tips below it:
        // below its lobes only the narrowing point is the heart's.
        private static bool InHeart(int x, int fromTop, int w, int h)
        {
            return fromTop < h * .55f || Mathf.Abs(x - w * .5f) < (h - fromTop) * 1.15f + 2;
        }

        // Lives share the health bar's red: its painted top-to-bottom gradient,
        // shaded by the cream heart's own luminance so highlights survive.
        private static Color HealthRed(Color c, float up)
        {
            Color low = new Color32(0xAA, 0x02, 0x1C, 0xFF), mid = new Color32(0xF4, 0x09, 0x28, 0xFF), high = new Color32(0xFC, 0x4A, 0x6C, 0xFF);
            Color red = up < .6f ? Color.Lerp(low, mid, up / .6f) : Color.Lerp(mid, high, (up - .6f) / .4f);
            red *= Mathf.Clamp(c.grayscale / .9f, .6f, 1.1f);
            if (c.grayscale > .97f) red = Color.Lerp(red, new Color(1f, .85f, .88f), .5f);
            red.a = c.a; return red;
        }

        private static Sprite Slice(string key, Rect rect, bool empty = false)
        {
            Ensure();
            if (Cache.TryGetValue(key, out Sprite sprite)) return sprite;
            sprite = Sprite.Create(empty ? _empty : _source, Pixels(rect), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite; return sprite;
        }
        public static Sprite Top => Slice("top", new Rect(0, 0, 2048, Split), true);
        public static Sprite Bottom => Slice("bottom", new Rect(0, Split, 2048, 2048f * 724 / 2172 - Split), true);
        public static Sprite Button(int i) => Slice("button" + i, Buttons[i]);

        // A button cut out of the panel, for use away from it (the target dock's
        // Craft): its rect also holds the panel's wood and leaves round the
        // tile's chamfered corners. Flooded in from the rect's edge, everything
        // that is neither the tile's dark ink outline nor its enamel goes clear.
        public static Sprite ButtonCut(int i)
        {
            Ensure();
            string key = "cut" + i;
            if (Cache.TryGetValue(key, out Sprite cut)) return cut;
            Rect r = Pixels(Buttons[i]);
            int w = (int)r.width, h = (int)r.height;
            Color[] px = _source.GetPixels((int)r.x, (int)r.y, w, h);
            bool Outside(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) > .22f && c.b < Mathf.Max(c.r, c.g) + .05f;
            var clear = new bool[px.Length];
            var todo = new Stack<int>();
            for (int x = 0; x < w; x++) { todo.Push(x); todo.Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { todo.Push(y * w); todo.Push(y * w + w - 1); }
            while (todo.Count > 0)
            {
                int at = todo.Pop();
                if (clear[at] || !Outside(px[at])) continue;
                clear[at] = true;
                int x = at % w, y = at / w;
                if (x > 0) todo.Push(at - 1);
                if (x < w - 1) todo.Push(at + 1);
                if (y > 0) todo.Push(at - w);
                if (y < h - 1) todo.Push(at + w);
            }
            for (int k = 0; k < px.Length; k++) if (clear[k]) px[k] = Color.clear;
            Texture2D texture = AnimePieces.Painted(Pixels32(px), w, h, "PlayerPanelV2/" + key);
            cut = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            Cache[key] = cut; return cut;
        }
        public static Sprite Heart(int i) { Ensure(); return Cache["heart" + i]; }
        public static Sprite Fill(string key) { Ensure(); return Cache[key]; }

        // A fill's halo: white, fading out round its painted shape and clear inside
        // it, so tinted it lights the panel round the bar or icon without reading as
        // more of its fill. It has to reach past the painted cream rim round each
        // fill, or it only recolours that rim. The sprite is the fill's size plus
        // AuraPad texels on every side; three box blurs make the falloff round.
        public const int AuraPad = 80;
        public static Sprite Aura(string key)
        {
            Ensure();
            if (Cache.TryGetValue("aura/" + key, out Sprite aura)) return aura;
            Texture2D fill = Cache[key].texture;
            int fw = fill.width, fh = fill.height, p = AuraPad, w = fw + 2 * p, h = fh + 2 * p;
            Color32[] src = fill.GetPixels32();
            float[] shape = new float[w * h], glow = new float[w * h], tmp = new float[w * h];
            for (int y = 0; y < fh; y++) for (int x = 0; x < fw; x++)
                shape[(y + p) * w + x + p] = src[y * fw + x].a / 255f;
            System.Array.Copy(shape, glow, glow.Length);
            for (int pass = 0; pass < 3; pass++) { BoxBlur(glow, tmp, w, h, p * 3 / 10, true); BoxBlur(tmp, glow, w, h, p * 3 / 10, false); }
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Mathf.Clamp01(Mathf.Pow(glow[i], .5f) * 1.6f) * (1f - shape[i]);
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            var texture = AnimePieces.Painted(pixels, w, h, "PlayerPanelV2/aura/" + key);
            aura = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            Cache["aura/" + key] = aura; return aura;
        }

        private static void BoxBlur(float[] from, float[] to, int w, int h, int r, bool across)
        {
            int n = across ? w : h, lines = across ? h : w, step = across ? 1 : w, next = across ? w : 1;
            for (int line = 0; line < lines; line++)
            {
                int start = line * next; float sum = 0;
                for (int i = -r; i <= r; i++) sum += from[start + Mathf.Clamp(i, 0, n - 1) * step];
                for (int i = 0; i < n; i++)
                {
                    to[start + i * step] = sum / (2 * r + 1);
                    sum += from[start + Mathf.Min(i + r + 1, n - 1) * step] - from[start + Mathf.Max(i - r, 0) * step];
                }
            }
        }
        public static Rect FillRect(string key) => key == "health" ? Health : key == "stun" ? Stun : key == "food" ? Food : Water;
        public static Image Image(Transform parent, string name, Sprite sprite)
        {
            var go = Owned.New(name); go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image;
        }
        public static void Reset()
        {
            var textures = new HashSet<Texture2D>();
            foreach (var sprite in Cache.Values) { textures.Add(sprite.texture); Object.Destroy(sprite); }
            if (_source != null) textures.Add(_source); if (_empty != null) textures.Add(_empty);
            foreach (var texture in textures) Object.Destroy(texture);
            Cache.Clear(); _source = _empty = null;
        }
    }
}
