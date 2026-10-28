using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // THE BLUEPRINT (the card redesigned to carry a blueprint-like appearance - the
    // cyanotype, where the whole card is the sheet). Its material in one place: the
    // inks, the DIN face a drawing is lettered in, the rules and the grid
    // (`art/kit_bp_*.png`), and the item as a line drawing.
    internal static class Blueprint
    {
        public static readonly Color Ground = new Color32(22, 60, 118, 255);
        public static readonly Color Ink = new Color32(236, 244, 255, 255);
        public static readonly Color Dim = new Color32(170, 196, 230, 255);
        public static readonly Color Short = new Color32(255, 146, 118, 255); // a count that falls short
        public static readonly Color Lit = new Color32(255, 214, 110, 255);   // something you can act on

        // the crafting window wears the sheet too (BrowserShell.Sheet.cs), as the
        // recipe card does

        // for vanilla's legacy Text (the crafting cell's stack count)
        private static Font _legacyFace;
        public static Font LegacyFace => _legacyFace != null ? _legacyFace
            : (_legacyFace = Font.CreateDynamicFontFromOSFont("Bahnschrift", 32));

        private static TMP_FontAsset _face;
        private static bool _faceTried;

        // Bahnschrift is Windows' DIN 1451 - the lettering engineering drawings are
        // actually set in. Built from the file, as the shed's faces are.
        public static TMP_FontAsset Face
        {
            get
            {
                if (!_faceTried && Theme.Font != null)
                {
                    _faceTried = true;
                    _face = SystemFont.Build("bahnschrift.ttf", "IslandUI Blueprint", Theme.Font);
                }
                return _face;
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, Color color,
            TextAlignmentOptions align)
        {
            TextMeshProUGUI text = Theme.MakeKitText(parent, name, size, color, align);
            if (Face != null)
            {
                text.font = Face;
                text.fontStyle = FontStyles.Normal;
                Theme.FlattenType(text);
            }
            text.enableWordWrapping = false;
            return text;
        }

        // a rule: one canvas unit of ink at most, usually half
        public static Image Rule(Transform parent, string name, float alpha = 1f)
        {
            Image rule = Theme.KitImage(parent, name, null, 0, WithAlpha(Ink, alpha));
            rule.preserveAspect = false;
            return rule;
        }

        public static Image Tile(Transform parent, string name, string sprite, Color color)
        {
            Image image = Theme.KitImage(parent, name, null, 0, color);
            image.sprite = Theme.KitTile(sprite);
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Theme.KitPpu;
            return image;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------- the item as a drawing ----------

        private static readonly Dictionary<long, Sprite> Drawings = new Dictionary<long, Sprite>();
        private const int PerFrame = 3;
        private static int _frame = -1, _madeThisFrame;
        // some drawings were put off to a later frame: ask for them again
        public static bool Pending;

        // The sprite's outline and its inner edges in one white ink, over a breath
        // of fill - a Sobel pass on alpha and on luminance. It runs on a copy
        // scaled to the size it will be SHOWN at (`pixels` on the long side), since
        // a line found at 256 px and shown at 40 is a line nobody sees. Once per
        // item and size; tint it with the Image's colour.
        public static Sprite Drawing(Sprite source, int pixels)
        {
            if (source == null || source.texture == null)
            {
                return null;
            }
            long key = ((long)source.GetInstanceID() << 12) ^ (uint)pixels;
            if (Drawings.TryGetValue(key, out Sprite hit))
            {
                return hit;   // null too: a sprite that could not be drawn is not tried again
            }
            // A drawing costs a read back from the GPU. A page that wants thirty of
            // them at once was a hitch on the first hover (235 ms measured for one
            // card); so a few per frame, and the
            // caller asks again while Pending says some are owed.
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _madeThisFrame = 0;
            }
            if (_madeThisFrame >= PerFrame)
            {
                Pending = true;
                return null;
            }
            _madeThisFrame++;
            Sprite made = null;
            try
            {
                made = Draw(source, pixels);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Blueprint: no drawing of '{source.name}': {e.Message}");
            }
            Drawings[key] = made;
            return made;
        }

        private static readonly Dictionary<int, Sprite> Trims = new Dictionary<int, Sprite>();

        // FullRect, always: Sprite.Create's default is a TIGHT mesh, which it makes by
        // analysing the texture - and the items' atlas is 8192 px square. That was the
        // 236 ms of a first hover; the read back itself is a third of a millisecond.
        // The painted sprite cut to what is painted, so a thing in colour stands as
        // large as its drawing does (a drawing is made from the cut, an icon sits in
        // a padded cell). The same texture, a smaller rect: nothing is copied.
        public static Sprite Trimmed(Sprite source)
        {
            if (source == null || source.texture == null)
            {
                return source;
            }
            int key = source.GetInstanceID();
            if (Trims.TryGetValue(key, out Sprite hit) && hit != null)
            {
                return hit;
            }
            Sprite made = source;
            try
            {
                if (!source.packed || source.packingMode == SpritePackingMode.Rectangle)
                {
                    Rect r = source.textureRect;
                    if (Painted(source, 64, out _, out int sw, out int sh, out int x0, out int y0, out int x1, out int y1))
                    {
                        // found on a small copy: a cell of slack each way, back in texture pixels
                        float fx = r.width / sw, fy = r.height / sh;
                        float left = Mathf.Max(0, x0 - 1) * fx, right = Mathf.Min(sw, x1 + 2) * fx;
                        float bottom = Mathf.Max(0, y0 - 1) * fy, top = Mathf.Min(sh, y1 + 2) * fy;
                        made = Sprite.Create(source.texture,
                            new Rect(r.x + left, r.y + bottom, right - left, top - bottom), new Vector2(0.5f, 0.5f), 100f,
                            0, SpriteMeshType.FullRect);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Blueprint: '{source.name}' not trimmed: {e.Message}");
            }
            Trims[key] = made;
            return made;
        }

        // The sprite's pixels and the box of what is painted in them - from a copy the
        // GPU has already scaled to `cap` on its long side. Some item sprites are
        // 1024 px: a million pixels walked in Mono was most of the 235 ms.
        private static bool Painted(Sprite source, int cap, out Color32[] src, out int sw, out int sh,
            out int x0, out int y0, out int x1, out int y1)
        {
            Rect r = SourceRect(source);
            float k = Mathf.Min(1f, cap / Mathf.Max(1f, Mathf.Max(r.width, r.height)));
            sw = Mathf.Max(1, Mathf.RoundToInt(r.width * k));
            sh = Mathf.Max(1, Mathf.RoundToInt(r.height * k));
            x0 = sw; y0 = sh; x1 = -1; y1 = -1;
            src = ReadSprite(source, sw, sh);
            if (src == null)
            {
                return false;
            }
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    if (src[y * sw + x].a > 8)
                    {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                }
            }
            return x1 >= x0;
        }

        private static Sprite Draw(Sprite source, int pixels)
        {
            // what is drawn, not the cell it sits in
            if (!Painted(source, pixels * 2, out Color32[] src, out int sw, out _, out int x0, out int y0, out int x1, out int y1))
            {
                return null;
            }
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            float scale = Mathf.Min(1f, pixels / (float)Mathf.Max(bw, bh));
            const int pad = 3;
            int w = Mathf.Max(1, Mathf.RoundToInt(bw * scale)) + 2 * pad;
            int h = Mathf.Max(1, Mathf.RoundToInt(bh * scale)) + 2 * pad;
            var alpha = new float[w * h];
            var lum = new float[w * h];
            // box-average down: every source pixel lands in the cell it covers
            var hits = new float[w * h];
            for (int y = y0; y <= y1; y++)
            {
                int ty = Mathf.Min(h - pad - 1, pad + (int)((y - y0) * scale));
                for (int x = x0; x <= x1; x++)
                {
                    int tx = Mathf.Min(w - pad - 1, pad + (int)((x - x0) * scale));
                    Color32 c = src[y * sw + x];
                    float a = c.a / 255f;
                    int i = ty * w + tx;
                    alpha[i] += a;
                    lum[i] += a * (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    hits[i] += 1f;
                }
            }
            for (int i = 0; i < alpha.Length; i++)
            {
                if (hits[i] > 0f)
                {
                    alpha[i] /= hits[i];
                    lum[i] /= hits[i];
                }
            }
            var ink = new float[w * h];
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    float outer = Sobel(alpha, w, i) * 1.6f;
                    // inner edges only well inside the silhouette, or the outline doubles
                    float inside = Mathf.Min(Mathf.Min(alpha[i - 1], alpha[i + 1]), Mathf.Min(alpha[i - w], alpha[i + w]));
                    float inner = inside > 0.9f ? Mathf.Max(0f, Sobel(lum, w, i) - 0.045f) * 5f : 0f;
                    ink[i] = Mathf.Clamp01(Mathf.Max(outer, inner * 0.8f));
                }
            }
            var pixelsOut = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    // one pass of softening, so a rule is a rule and not a staircase
                    float soft = ink[i];
                    if (x > 0 && x < w - 1 && y > 0 && y < h - 1)
                    {
                        soft = ink[i] * 0.6f + (ink[i - 1] + ink[i + 1] + ink[i - w] + ink[i + w]) * 0.1f;
                    }
                    float a = Mathf.Clamp01(Mathf.Max(soft * 1.35f, alpha[i] * 0.10f));
                    pixelsOut[i] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(pixelsOut);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static float Sobel(float[] v, int w, int i)
        {
            float gx = v[i - w + 1] + 2f * v[i + 1] + v[i + w + 1] - v[i - w - 1] - 2f * v[i - 1] - v[i + w - 1];
            float gy = v[i + w - 1] + 2f * v[i + w] + v[i + w + 1] - v[i - w - 1] - 2f * v[i - w] - v[i - w + 1];
            return Mathf.Sqrt(gx * gx + gy * gy) * 0.25f;
        }

        // Atlases are not CPU-readable: blit the sprite's own rect to a target of
        // its size and read that back (SpriteDump's way, one sprite instead of all).
        // A TIGHT-packed sprite has no rectangle of its own in its atlas: its
        // textureRect reads zero and its neighbours share its box, so a read of
        // the rectangle found nothing and the Native House's drawing came out
        // empty (Forgejo #178). It is drawn from its own mesh instead, at the size
        // of its own rect.
        private static bool Tight(Sprite s)
        {
            return s.packed && s.packingMode == SpritePackingMode.Tight;
        }

        // the pixels a sprite is read from: its atlas rectangle, or a tight one's own rect
        internal static Rect SourceRect(Sprite s)
        {
            return Tight(s) ? s.rect : s.textureRect;
        }

        // the sprite's pixels, scaled by the GPU to w x h
        internal static Color32[] ReadSprite(Sprite s, int w, int h)
        {
            return Tight(s) ? ReadMesh(s, w, h) : ReadRect(s.texture, s.textureRect, w, h);
        }

        private static Material _meshInk;

        private static Color32[] ReadMesh(Sprite s, int w, int h)
        {
            if (_meshInk == null)
            {
                _meshInk = new Material(Shader.Find("Sprites/Default"));
            }
            return ReadInto(w, h, () =>
            {
                GL.Clear(true, true, Color.clear);
                _meshInk.mainTexture = s.texture;
                _meshInk.SetPass(0);
                GL.PushMatrix();
                GL.LoadOrtho();
                GL.Begin(GL.TRIANGLES);
                GL.Color(Color.white);
                Vector2[] v = s.vertices;
                Vector2[] uv = s.uv;
                ushort[] tri = s.triangles;
                Rect rect = s.rect;
                float ppu = s.pixelsPerUnit;
                for (int i = 0; i < tri.Length; i++)
                {
                    int t = tri[i];
                    // local units back to the sprite's own rect, 0..1
                    GL.TexCoord2(uv[t].x, uv[t].y);
                    GL.Vertex3((v[t].x * ppu + s.pivot.x) / rect.width,
                        (v[t].y * ppu + s.pivot.y) / rect.height, 0f);
                }
                GL.End();
                GL.PopMatrix();
            });
        }

        private static Color32[] ReadRect(Texture2D texture, Rect r, int w, int h)
        {
            var scale = new Vector2(r.width / texture.width, r.height / texture.height);
            var offset = new Vector2(r.x / texture.width, r.y / texture.height);
            return ReadInto(w, h, () => Graphics.Blit(texture, RenderTexture.active, scale, offset));
        }

        // a temporary target, made active, drawn into by `paint`, and read back
        private static Color32[] ReadInto(int w, int h, System.Action paint)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                RenderTexture.active = rt;
                paint();
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null)
                {
                    Object.Destroy(copy);
                }
            }
        }
    }
}
