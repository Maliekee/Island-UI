using UnityEngine;

namespace IslandUI.UI
{
    // The pointer, authored — because there is nothing to harvest. The game
    // declares GameManager.cursorImage and never READS it, nothing anywhere calls
    // Cursor.SetCursor, and the F10 dump reports the field unassigned in the scene. So the
    // OS default is what you get, and nothing will fight what we set.
    //
    // A LAB: several shapes, one key to cycle
    // them, so the choice is made by looking rather than by describing. Every
    // shape is GENERATED — no asset ships, and the colours and size stay config.
    //
    // Legibility is the actual complaint, not prettiness: this pointer has to
    // survive bright sand AND dark foliage, which is why every shape is a fill
    // with a contrasting outline rather than a flat colour. That is also why the
    // outline is drawn OUTSIDE the shape — an inner stroke eats a small cursor.
    internal static class GameCursor
    {
        internal enum Style
        {
            Off,          // the OS default
            Arrow,        // the familiar pointer
            ArrowBold,    // the same, thickened — a thin pointer dies over foliage
            Carved,       // arrow with concave flanks: a whittled point, not an OS glyph
            Spear,        // narrow head over a crossguard — the game's own vocabulary
            Claw,         // a crescent talon; reads as this island rather than as Windows
            Crosshair,    // gapped cross: precise, and never hides what it is over
            Ring,         // reticle — outline circle with a centre dot
        }

        // How the texture is handed over. Hardware cursors are crisp and lag-free
        // but the OS caps them (32px is the size that is safe everywhere); software
        // cursors can be any size but trail the real pointer by a frame. Hardware
        // until the size forces software, so asking for BIG just works and only
        // pays the lag when it has to.
        private const int HardwareLimit = 32;

        private const int Super = 4;   // supersampling factor for the shape mask

        private static Texture2D _texture;
        private static Style _applied = (Style)(-1);
        private static int _appliedSize = -1;
        private static float _appliedOutline = -1f;
        private static Color _appliedFill;
        private static Color _appliedEdge;

        internal static void Init()
        {
            Settings.CursorStyle.SettingChanged += (_, __) => Apply();
            Settings.CursorSize.SettingChanged += (_, __) => Apply();
            Settings.CursorFill.SettingChanged += (_, __) => Apply();
            Settings.CursorOutline.SettingChanged += (_, __) => Apply();
            Settings.CursorOutlineWidth.SettingChanged += (_, __) => Apply();
            Apply();
        }

        internal static void Tick()
        {
            KeyCode key = Settings.CursorCycleKey.Value;
            if (key == KeyCode.None || !Input.GetKeyDown(key))
            {
                return;
            }
            // wraps through Off, so the OS default is one of the things you compare
            int next = ((int)Settings.CursorStyle.Value + 1)
                % (System.Enum.GetValues(typeof(Style)).Length);
            Settings.CursorStyle.Value = (Style)next;   // SettingChanged applies it
            Plugin.Log.LogInfo($"Cursor: {Settings.CursorStyle.Value}");
        }

        // Re-asserted after a scene load: cheap, and cursor state is exactly the
        // kind of global a load is entitled to disturb.
        internal static void ResetForSceneLoad()
        {
            _applied = (Style)(-1);
            Apply();
        }

        private static void Apply()
        {
            Style style = Settings.CursorStyle.Value;
            int size = Mathf.Clamp(Settings.CursorSize.Value, 12, 64);
            float outline = Mathf.Clamp(Settings.CursorOutlineWidth.Value, 0f, 6f);
            Color fill = ParseColor(Settings.CursorFill.Value, new Color(1f, 0.95f, 0.84f, 1f));
            Color edge = ParseColor(Settings.CursorOutline.Value, new Color(0.13f, 0.09f, 0.05f, 1f));
            if (style == _applied && size == _appliedSize && outline == _appliedOutline
                && fill == _appliedFill && edge == _appliedEdge)
            {
                return;
            }
            _applied = style;
            _appliedSize = size;
            _appliedOutline = outline;
            _appliedFill = fill;
            _appliedEdge = edge;

            if (style == Style.Off)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }
            _texture = Build(style, size, outline, fill, edge);
            bool software = size > HardwareLimit;
            Cursor.SetCursor(_texture, Hotspot(style, size),
                software ? CursorMode.ForceSoftware : CursorMode.Auto);
            Plugin.Log.LogInfo($"Cursor: {style} at {size}px, "
                + (software ? "software (any size, one frame of lag)" : "hardware (crisp)"));
        }

        // The click point. An arrow points with its tip (the top-left corner);
        // everything else is aimed from its middle.
        private static Vector2 Hotspot(Style style, int size)
        {
            return style == Style.Arrow || style == Style.ArrowBold
                ? new Vector2(1f, 1f)
                : new Vector2(size * 0.5f, size * 0.5f);
        }

        // ---------- generation ----------
        //
        // Shapes are described as a plain "is this point inside" test in a unit
        // square, sampled at Super x Super and then dilated for the outline. That
        // is slower than a signed-distance field and enormously easier to add a
        // shape to — which is the whole point of a lab.
        private static Texture2D Build(Style style, int size, float outlineWidth,
            Color fill, Color edge)
        {
            int hi = size * Super;
            bool[] inside = new bool[hi * hi];
            for (int y = 0; y < hi; y++)
            {
                for (int x = 0; x < hi; x++)
                {
                    // texture space is y-up; the shapes are written y-down, which is
                    // how anyone sketches a cursor
                    float u = (x + 0.5f) / hi;
                    float v = 1f - (y + 0.5f) / hi;
                    inside[y * hi + x] = Inside(style, u, v);
                }
            }
            int radius = Mathf.RoundToInt(outlineWidth * Super);
            bool[] outer = radius > 0 ? Dilate(inside, hi, radius) : inside;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inCount = 0;
                    int outCount = 0;
                    for (int sy = 0; sy < Super; sy++)
                    {
                        for (int sx = 0; sx < Super; sx++)
                        {
                            int i = (y * Super + sy) * hi + (x * Super + sx);
                            if (inside[i]) inCount++;
                            if (outer[i]) outCount++;
                        }
                    }
                    float total = Super * Super;
                    float f = inCount / total;
                    float o = outCount / total;
                    // the outline is whatever the dilation added, so the fill sits
                    // ON it rather than being cut into it
                    Color c = Color.Lerp(edge, fill, f <= 0f ? 0f : f);
                    c.a = Mathf.Max(o, f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static bool[] Dilate(bool[] src, int side, int radius)
        {
            bool[] dst = new bool[src.Length];
            int r2 = radius * radius;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    if (src[y * side + x])
                    {
                        dst[y * side + x] = true;
                        continue;
                    }
                    bool hit = false;
                    for (int dy = -radius; dy <= radius && !hit; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= side) continue;
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= side || dx * dx + dy * dy > r2) continue;
                            if (src[ny * side + nx]) { hit = true; break; }
                        }
                    }
                    dst[y * side + x] = hit;
                }
            }
            return dst;
        }

        // u,v in [0,1] with v measured DOWN from the top-left, i.e. the corner an
        // arrow points from.
        private static bool Inside(Style style, float u, float v)
        {
            switch (style)
            {
                case Style.Arrow:
                    return InPolygon(ArrowShape, u, v);
                case Style.ArrowBold:
                    return InPolygon(BoldArrowShape, u, v);
                case Style.Carved:
                    return InPolygon(CarvedShape, u, v);
                case Style.Spear:
                {
                    // a leaf-bladed head over a short crossguard
                    bool head = InPolygon(SpearShape, u, v);
                    bool guard = Mathf.Abs(u - 0.42f) < 0.26f
                        && v > 0.60f && v < 0.70f;
                    return head || guard;
                }
                case Style.Claw:
                {
                    // a crescent: inside one disc and outside another, offset — the
                    // cheapest honest curve, and it lands on a talon
                    float d1 = Dist(u, v, 0.44f, 0.52f);
                    float d2 = Dist(u, v, 0.70f, 0.74f);
                    return d1 < 0.44f && d2 > 0.46f;
                }
                case Style.Crosshair:
                {
                    float du = Mathf.Abs(u - 0.5f);
                    float dv = Mathf.Abs(v - 0.5f);
                    const float arm = 0.07f;   // half-thickness
                    const float gap = 0.10f;   // the hole that keeps the target visible
                    bool bar = (du <= arm && dv <= 0.46f) || (dv <= arm && du <= 0.46f);
                    return bar && (du > gap || dv > gap);
                }
                case Style.Ring:
                {
                    float d = Dist(u, v, 0.5f, 0.5f);
                    return (d > 0.30f && d < 0.40f) || d < 0.07f;
                }
                default:
                    return false;
            }
        }

        private static float Dist(float u, float v, float cx, float cy)
        {
            return Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
        }

        // the familiar pointer: tip at the origin, tail flicked to the right
        private static readonly Vector2[] ArrowShape =
        {
            new Vector2(0.02f, 0.02f), new Vector2(0.02f, 0.80f), new Vector2(0.22f, 0.61f),
            new Vector2(0.35f, 0.92f), new Vector2(0.50f, 0.85f), new Vector2(0.37f, 0.55f),
            new Vector2(0.62f, 0.55f),
        };

        // the same silhouette, widened — a thin pointer disappears against busy
        // foliage no matter what colour it is
        private static readonly Vector2[] BoldArrowShape =
        {
            new Vector2(0.02f, 0.02f), new Vector2(0.02f, 0.90f), new Vector2(0.26f, 0.68f),
            new Vector2(0.42f, 0.98f), new Vector2(0.64f, 0.87f), new Vector2(0.47f, 0.60f),
            new Vector2(0.80f, 0.60f),
        };

        // Concave flanks, so the silhouette reads as WHITTLED rather than as a
        // vector arrowhead — the game's props are all carved wood and bone.
        private static readonly Vector2[] CarvedShape =
        {
            new Vector2(0.02f, 0.02f), new Vector2(0.10f, 0.44f), new Vector2(0.06f, 0.92f),
            new Vector2(0.30f, 0.72f), new Vector2(0.46f, 0.99f), new Vector2(0.60f, 0.88f),
            new Vector2(0.44f, 0.62f), new Vector2(0.78f, 0.52f), new Vector2(0.40f, 0.40f),
        };

        // a leaf-bladed spear head; the crossguard is added separately
        private static readonly Vector2[] SpearShape =
        {
            new Vector2(0.42f, 0.02f), new Vector2(0.60f, 0.30f), new Vector2(0.58f, 0.64f),
            new Vector2(0.42f, 0.76f), new Vector2(0.26f, 0.64f), new Vector2(0.24f, 0.30f),
        };

        private static bool InPolygon(Vector2[] poly, float u, float v)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (poly[i].y > v != poly[j].y > v
                    && u < (poly[j].x - poly[i].x) * (v - poly[i].y)
                        / (poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static Color ParseColor(string html, Color fallback)
        {
            return !string.IsNullOrEmpty(html)
                && ColorUtility.TryParseHtmlString(html.Trim(), out Color c) ? c : fallback;
        }
    }
}
