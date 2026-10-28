using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The anime HUD's painted pieces beyond the player panel: cut from the approved
    // board by make_anime_hud.py into art/hud-anime-v1/runtime/pieces,
    // embedded one PNG each as IslandUI.Anime.<name>.png. Positions in the HUD are
    // board pixels divided by Px - the scale at which the board's player panel is
    // the in-game panel's 174 units - so every dock keeps the board's proportions.
    internal static class AnimePieces
    {
        public const float Px = 4.9f;
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(string name, Vector4 border = default(Vector4))
        {
            string key = name + border;
            if (Sprites.TryGetValue(key, out Sprite sprite)) return sprite;
            Texture2D texture = null;
            foreach (var s in Sprites.Values) if (s != null && s.texture != null && s.texture.name == name) { texture = s.texture; break; }
            if (texture == null)
            {
                using (Stream stream = typeof(AnimePieces).Assembly.GetManifestResourceStream("IslandUI.Anime." + name + ".png"))
                {
                    if (stream == null) return null;
                    var memory = new MemoryStream(); stream.CopyTo(memory);
                    texture = Painted(memory.ToArray(), name);
                }
            }
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = "anime/" + name; Sprites[key] = sprite; return sprite;
        }

        // PAINTED ART IS DRAWN SMALLER THAN ITS PIXELS: the board's pieces about 1.5x
        // and the panel's painting about 3.9x on a 1440p screen (3.2 px a unit).
        // Bilinear alone reads a few texels of each screen pixel's footprint and skips
        // the rest, so thin ink lines - the blueprint grid, outlines - broke into
        // dashes. A mip chain filters them; trilinear blends between its levels and a
        // small negative bias keeps the result sharp. Transparent texels take their
        // nearest opaque neighbour's colour first, or the smaller levels would mix
        // the PNG's black into every edge as a dark halo.
        public const float MipBias = -.35f;

        public static Texture2D Painted(byte[] png, string name)
        {
            var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            loaded.LoadImage(png);
            Color32[] pixels = loaded.GetPixels32();
            var texture = Painted(pixels, loaded.width, loaded.height, name);
            Object.Destroy(loaded);
            return texture;
        }

        public static Texture2D Painted(Color32[] pixels, int w, int h, string name)
        {
            Bleed(pixels, w, h);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels); texture.Apply(true);
            texture.name = name; texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear; texture.mipMapBias = MipBias;
            return texture;
        }

        // Spreads opaque colour into the transparent texels beside it, a breadth-first
        // ring at a time, as far as the mip levels that matter reach (alpha untouched).
        private static void Bleed(Color32[] px, int w, int h, int reach = 16)
        {
            var depth = new byte[px.Length];
            var ring = new List<int>();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0) depth[i] = 1;
            for (int i = 0; i < px.Length; i++)
                if (depth[i] == 0 && HasFilled(depth, i, w, h, 1)) ring.Add(i);
            for (byte d = 2; d < reach + 2 && ring.Count > 0; d++)
            {
                foreach (int i in ring)
                {
                    int x = i % w, y = i / w, r = 0, g = 0, b = 0, n = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (depth[j] == 0 || depth[j] >= d) continue;
                        r += px[j].r; g += px[j].g; b += px[j].b; n++;
                    }
                    if (n > 0) px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                }
                foreach (int i in ring) depth[i] = d;
                var next = new List<int>();
                foreach (int i in ring)
                {
                    int x = i % w, y = i / w;
                    if (x + 1 < w && depth[i + 1] == 0) { depth[i + 1] = 255; next.Add(i + 1); }
                    if (x > 0 && depth[i - 1] == 0) { depth[i - 1] = 255; next.Add(i - 1); }
                    if (y + 1 < h && depth[i + w] == 0) { depth[i + w] = 255; next.Add(i + w); }
                    if (y > 0 && depth[i - w] == 0) { depth[i - w] = 255; next.Add(i - w); }
                }
                foreach (int i in next) depth[i] = 0;
                ring = next;
            }
        }

        private static bool HasFilled(byte[] depth, int i, int w, int h, byte level)
        {
            int x = i % w, y = i / w;
            return (x + 1 < w && depth[i + 1] == level) || (x > 0 && depth[i - 1] == level)
                || (y + 1 < h && depth[i + w] == level) || (y > 0 && depth[i - w] == level);
        }

        // A piece's size in HUD units at the board's scale.
        public static Vector2 Size(string name)
        {
            Sprite s = Get(name); return s == null ? Vector2.zero : s.rect.size / Px;
        }

        public static Image Image(Transform parent, string objectName, string piece)
        {
            var go = Owned.New(objectName); go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>(); image.raycastTarget = false;
            Set(image, piece); return image;
        }

        public static void Set(Image image, string piece)
        {
            image.sprite = Get(piece); image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = false;
            image.color = Color.white; image.enabled = image.sprite != null;
        }

        // A stretchable piece: border in the PNG's pixels, drawn at the board's scale.
        public static void Sliced(Image image, string piece, Vector4 border, float scale = 1f)
        {
            image.sprite = Get(piece, border); image.type = UnityEngine.UI.Image.Type.Sliced; image.fillCenter = true;
            image.pixelsPerUnitMultiplier = Px / scale; image.color = Color.white; image.enabled = image.sprite != null;
        }

        // Places at board-pixel coordinates relative to an origin, in units.
        public static void Place(Graphic g, float x, float y, float w, float h, float scale = 1f)
        {
            Theme.Place(g, x * scale / Px, y * scale / Px, w * scale / Px, h * scale / Px);
        }

        // Moves a placed rect's pivot to its middle, leaving it where it is, so a
        // mirror (localScale -1) flips it in place.
        public static void Centre(Graphic g)
        {
            var r = g.rectTransform;
            r.anchoredPosition += new Vector2(r.sizeDelta.x * (.5f - r.pivot.x), r.sizeDelta.y * (.5f - r.pivot.y));
            r.pivot = new Vector2(.5f, .5f);
        }

        public static void Reset()
        {
            var textures = new HashSet<Texture2D>();
            foreach (var s in Sprites.Values) if (s != null) { textures.Add(s.texture); Object.Destroy(s); }
            foreach (var t in textures) if (t != null) Object.Destroy(t);
            Sprites.Clear();
        }
    }
}
