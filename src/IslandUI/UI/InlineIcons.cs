using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IslandUI.UI
{
    // ICONS INSIDE A LINE OF TEXT: a TMP sprite asset built at runtime from the
    // kit's own masks, so a tooltip can say "Likes <crown> Selfish" with the
    // token the sheets draw. An icon is named and defined as its layers, bottom
    // up - a kit mask and the colour it is printed in - exactly as the sheets
    // stack the same masks as Images; the sheet is composed on the CPU the first
    // time it is asked for after a change, one 48 px cell per icon.
    //
    // A layer's mask is a kit sprite's name, or one of the plain marks the sheets
    // draw without a sprite: "#square" (a filled square), "#rim" (its outline),
    // "#dot" (a small square at the centre), "#bar" (a thin upright), "#dash" (a
    // short level stroke).
    //
    // In text: InlineIcons.Tag("type6"). A text shows them once its spriteAsset is
    // InlineIcons.Asset. TMP sizes an inline sprite to the font's ascent, so an
    // icon stands on the baseline at the height of a capital, a little below it.
    internal static class InlineIcons
    {
        private const int Cell = 48;
        private const float Sink = 0.12f;     // the share of an icon under the baseline
        private const float Gap = 0.12f;      // the space after it, against its width

        private static readonly List<string> Names = new List<string>();
        private static readonly List<KeyValuePair<string, Color>[]> Layers = new List<KeyValuePair<string, Color>[]>();
        private static TMP_SpriteAsset _asset;
        private static Texture2D _sheet;
        private static bool _dirty = true;

        internal static void Define(string name, KeyValuePair<string, Color>[] layers)
        {
            int at = Names.IndexOf(name);
            if (at < 0)
            {
                Names.Add(name);
                Layers.Add(layers);
            }
            else
            {
                Layers[at] = layers;
            }
            _dirty = true;
        }

        internal static string Tag(string name)
        {
            return "<sprite name=\"" + name + "\">";
        }

        internal static TMP_SpriteAsset Asset
        {
            get
            {
                if (_dirty)
                {
                    Build();
                }
                return _asset;
            }
        }

        private static void Build()
        {
            _dirty = false;
            int n = Names.Count;
            if (n == 0)
            {
                return;
            }
            if (_sheet == null || _sheet.width != Cell * n)
            {
                if (_sheet != null) { Object.Destroy(_sheet); }
                _sheet = new Texture2D(Cell * n, Cell, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            var pixels = new Color[Cell * n * Cell];
            for (int i = 0; i < n; i++)
            {
                foreach (KeyValuePair<string, Color> layer in Layers[i])
                {
                    Paint(pixels, Cell * n, i * Cell, layer.Key, layer.Value);
                }
            }
            _sheet.SetPixels(pixels);
            _sheet.Apply(false, false);

            if (_asset == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Sprite");
                if (shader == null && TMP_Settings.defaultSpriteAsset != null && TMP_Settings.defaultSpriteAsset.material != null)
                {
                    shader = TMP_Settings.defaultSpriteAsset.material.shader;
                }
                if (shader == null)
                {
                    Plugin.Log.LogWarning("Inline icons: no TextMeshPro sprite shader; tooltips print without icons");
                    return;
                }
                _asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                _asset.name = "IslandUI icons";
                _asset.hideFlags = HideFlags.HideAndDontSave;
                // made in the current layout: an empty version would have it "upgraded"
                // from the legacy sprite list, which is empty, on its first lookup
                typeof(TMP_SpriteAsset).GetField("m_Version", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)?.SetValue(_asset, "1.1.0");
                _asset.spriteInfoList = new List<TMP_Sprite>();
                _asset.material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            _asset.spriteSheet = _sheet;
            _asset.material.SetTexture(ShaderUtilities.ID_MainTex, _sheet);
            _asset.spriteGlyphTable.Clear();
            _asset.spriteCharacterTable.Clear();
            for (int i = 0; i < n; i++)
            {
                var metrics = new UnityEngine.TextCore.GlyphMetrics(Cell, Cell, 0f, Cell * (1f - Sink), Cell * (1f + Gap));
                var rect = new UnityEngine.TextCore.GlyphRect(i * Cell, 0, Cell, Cell);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, rect, 1f, 0);
                _asset.spriteGlyphTable.Add(glyph);
                _asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = Names[i] });
            }
            _asset.UpdateLookupTables();
        }

        // harness: build the sheet, write it out as a PNG to look at, and say what is in it
        internal static string Dump(string path)
        {
            TMP_SpriteAsset asset = Asset;
            if (_sheet != null && !string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllBytes(path, _sheet.EncodeToPNG());
            }
            return Names.Count + " icon(s): " + string.Join(", ", Names.ToArray())
                + (asset != null ? "; shader " + asset.material.shader.name + ", " + asset.spriteCharacterTable.Count + " character(s)" : "; no asset");
        }

        // one layer over the cell at x0: the mask's alpha in `color`, over what is there
        // a plain mark's coverage at cell pixel (x, y), y up
        private static float Plain(string mark, int x, int y)
        {
            float u = (x + 0.5f) / Cell, v = (y + 0.5f) / Cell;
            bool In(float a, float b, float c, float d) => u >= a && u <= b && v >= c && v <= d;
            switch (mark)
            {
                case "#square": return In(0.14f, 0.86f, 0.14f, 0.86f) ? 1f : 0f;
                case "#rim": return In(0.14f, 0.86f, 0.14f, 0.86f) && !In(0.24f, 0.76f, 0.24f, 0.76f) ? 1f : 0f;
                case "#dot": return In(0.4f, 0.6f, 0.4f, 0.6f) ? 1f : 0f;
                case "#bar": return In(0.42f, 0.58f, 0.04f, 0.96f) ? 1f : 0f;
                case "#dash": return In(0.28f, 0.72f, 0.44f, 0.56f) ? 1f : 0f;
                default: return 0f;
            }
        }

        private static void Paint(Color[] dst, int stride, int x0, string mask, Color color)
        {
            if (mask != null && mask.StartsWith("#", System.StringComparison.Ordinal))
            {
                for (int y = 0; y < Cell; y++)
                {
                    for (int x = 0; x < Cell; x++)
                    {
                        float a = Plain(mask, x, y) * color.a;
                        if (a <= 0f) { continue; }
                        int at = y * stride + x0 + x;
                        Color d = dst[at];
                        float outA = a + d.a * (1f - a);
                        Color o = (color * a + d * d.a * (1f - a)) / outA;
                        o.a = outA;
                        dst[at] = o;
                    }
                }
                return;
            }
            Sprite sprite = Theme.Kit(mask, 0);
            if (sprite == null || sprite.texture == null)
            {
                return;
            }
            Texture2D src = sprite.texture;
            Rect r = sprite.textureRect;
            Color[] from;
            try
            {
                from = src.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            }
            catch (UnityException)
            {
                return;   // not readable: the icon goes without this layer
            }
            int sw = (int)r.width, sh = (int)r.height;
            for (int y = 0; y < Cell; y++)
            {
                int sy = Mathf.Min(sh - 1, y * sh / Cell);
                for (int x = 0; x < Cell; x++)
                {
                    float a = from[sy * sw + Mathf.Min(sw - 1, x * sw / Cell)].a * color.a;
                    if (a <= 0f)
                    {
                        continue;
                    }
                    int at = y * stride + x0 + x;
                    Color d = dst[at];
                    float outA = a + d.a * (1f - a);
                    Color o = (color * a + d * d.a * (1f - a)) / outA;
                    o.a = outA;
                    dst[at] = o;
                }
            }
        }
    }
}
