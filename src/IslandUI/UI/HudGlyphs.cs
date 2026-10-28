using System.Collections.Generic;
using UnityEngine;

namespace IslandUI.UI
{
    // One tintable line family for navigation and actions, rasterized once.
    internal static class HudGlyphs
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        internal static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var found))
                return found;
            var paths = new List<Vector2[]>();
            System.Action<float[]> line = a => { var p = new Vector2[a.Length / 2]; for (int i = 0; i < p.Length; i++) p[i] = new Vector2(a[i * 2], 24 - a[i * 2 + 1]); paths.Add(p); };
            System.Action<float, float, float> circle = (x, y, r) => { var p = new float[66]; for (int i = 0; i <= 32; i++) { float t = i * Mathf.PI / 16; p[i * 2] = x + Mathf.Cos(t) * r; p[i * 2 + 1] = y + Mathf.Sin(t) * r; } line(p); };
            // an arc in degrees, y down like the rest (0 = right, 90 = down)
            System.Action<float, float, float, float, float> arc = (x, y, r, from, to) => { var p = new float[34]; for (int i = 0; i <= 16; i++) { float t = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / 16f); p[i * 2] = x + Mathf.Cos(t) * r; p[i * 2 + 1] = y + Mathf.Sin(t) * r; } line(p); };
            switch (name)
            {
                case "bag":
                    line(new float[] { 8, 7, 8, 4, 16, 4, 16, 7 });
                    line(new float[] { 6, 7, 18, 7, 20, 11, 20, 21, 4, 21, 4, 11, 6, 7 });
                    line(new float[] { 7, 13, 17, 13, 17, 18, 7, 18, 7, 13 });
                    break;
                case "details":
                    line(new float[] { 4, 3, 20, 3, 20, 21, 4, 21, 4, 3 });
                    circle(12, 9, 3);
                    line(new float[] { 7, 18, 7, 16, 9, 14, 15, 14, 17, 16, 17, 18 });
                    break;
                case "craft":
                    line(new float[] { 5, 4, 14, 4, 19, 9, 15, 13, 10, 8, 5, 8, 5, 4 });
                    line(new float[] { 12, 10, 4, 18, 6, 20, 14, 12 });
                    break;
                case "vicinity":
                    line(new float[] { 3, 5, 21, 5, 21, 10, 3, 10, 3, 5 });
                    line(new float[] { 5, 10, 5, 21, 19, 21, 19, 10 });
                    line(new float[] { 9, 14, 15, 14 });
                    break;
                case "roster":
                    circle(9, 7, 3);
                    circle(18, 8, 2);
                    line(new float[] { 3, 21, 3, 16, 6, 13, 12, 13, 15, 16, 15, 21 });
                    line(new float[] { 17, 13, 20, 14, 22, 17, 22, 21 });
                    break;
                case "map":
                    line(new float[] { 3, 5, 9, 3, 15, 5, 21, 3, 21, 19, 15, 21, 9, 19, 3, 21, 3, 5 });
                    line(new float[] { 9, 3, 9, 19 });
                    line(new float[] { 15, 5, 15, 21 });
                    break;
                case "quests":
                    line(new float[] { 12, 6, 8, 4, 3, 4, 3, 19, 8, 19, 12, 21, 16, 19, 21, 19, 21, 4, 16, 4, 12, 6, 12, 21 });
                    break;
                // the tool belt's chip: an axe and a pick, crossed
                case "tools":
                    line(new float[] { 4.5f, 19.5f, 16.5f, 7.5f });
                    line(new float[] { 10.5f, 3, 14.4f, 4.2f, 17.5f, 6.5f, 19.8f, 9.6f, 21, 13.5f });
                    line(new float[] { 19.5f, 19.5f, 6.5f, 6.5f });
                    line(new float[] { 6.5f, 6.5f, 3.8f, 8.3f, 3.3f, 11.6f, 5.4f, 13.6f, 9.8f, 9.8f });
                    break;
                case "settings":
                    circle(12, 12, 4);
                    circle(12, 12, 8);
                    for (int i = 0; i < 8; i++)
                    {
                        float t = i * Mathf.PI / 4;
                        line(new float[] { 12 + 8 * Mathf.Cos(t), 12 + 8 * Mathf.Sin(t), 12 + 11 * Mathf.Cos(t), 12 + 11 * Mathf.Sin(t) });
                    }
                    break;
                case "talk":
                    line(new float[] { 4, 4, 20, 4, 22, 7, 22, 15, 19, 18, 10, 18, 4, 22, 4, 17, 2, 14, 2, 7, 4, 4 });
                    break;
                case "carry":
                    line(new float[] { 5, 15, 3, 11, 5, 9, 8, 13, 8, 4, 10, 3, 12, 5, 12, 11, 12, 2, 15, 2, 15, 11, 15, 4, 18, 4, 18, 12, 18, 7, 21, 7, 21, 16, 18, 21, 10, 21, 5, 15 });
                    break;
                case "follow":
                    circle(7, 5, 2);
                    circle(17, 9, 2);
                    line(new float[] { 5, 10, 8, 10, 9, 17, 5, 17, 5, 10 });
                    line(new float[] { 15, 14, 18, 14, 19, 21, 15, 21, 15, 14 });
                    break;
                // Set place: a pin standing on the ring of its territory
                case "place":
                    arc(12, 8, 5, 135, 405);
                    line(new float[] { 8.5f, 11.5f, 12, 18, 15.5f, 11.5f });
                    circle(12, 8, 1.8f);
                    {
                        // the ring lies flat, parted behind the pin's point
                        var ring = new float[34];
                        for (int i = 0; i <= 16; i++)
                        {
                            float t = Mathf.Deg2Rad * Mathf.Lerp(-65f, 245f, i / 16f);
                            ring[i * 2] = 12 + Mathf.Cos(t) * 9;
                            ring[i * 2 + 1] = 19 + Mathf.Sin(t) * 3;
                        }
                        line(ring);
                    }
                    break;
                case "dissect":
                    line(new float[] { 10, 13, 19, 4, 21, 3, 20, 6, 12, 15 });
                    line(new float[] { 9, 12, 13, 16 });
                    line(new float[] { 10, 13, 4, 19, 6, 21, 12, 15 });
                    break;
                // the act row's doors and rarer acts, so they can take a
                // socket instead of waiting in More: trade, the two token shops, the
                // vacuum, a birth and the body switch; craft reuses the hammer
                case "trade":
                    line(new float[] { 3, 8, 21, 8 });
                    line(new float[] { 17, 4, 21, 8, 17, 12 });
                    line(new float[] { 21, 16, 3, 16 });
                    line(new float[] { 7, 12, 3, 16, 7, 20 });
                    break;
                case "token":
                    circle(12, 12, 9);
                    line(new float[] { 8.5f, 8.5f, 15.5f, 8.5f });
                    line(new float[] { 12, 8.5f, 12, 16.5f });
                    break;
                case "sweets":
                    circle(12, 12, 4.5f);
                    line(new float[] { 7.5f, 12, 3, 7.5f, 3, 16.5f, 7.5f, 12 });
                    line(new float[] { 16.5f, 12, 21, 7.5f, 21, 16.5f, 16.5f, 12 });
                    break;
                case "vacuum":
                    line(new float[] { 4, 5, 20, 5, 14, 13, 14, 20, 10, 20, 10, 13, 4, 5 });
                    line(new float[] { 8, 9, 16, 9 });
                    break;
                case "birth":
                    circle(12, 11, 7);
                    line(new float[] { 9.5f, 13, 12, 14.5f, 14.5f, 13 });
                    line(new float[] { 12, 4, 13.5f, 2 });
                    break;
                case "swap":
                    arc(12, 12, 8, 200, 340);
                    line(new float[] { 17.5f, 3.5f, 19.5f, 7.3f, 15.6f, 8.8f });
                    arc(12, 12, 8, 20, 160);
                    line(new float[] { 6.5f, 20.5f, 4.5f, 16.7f, 8.4f, 15.2f });
                    break;
                case "split":
                    circle(6, 6, 3);
                    circle(6, 18, 3);
                    line(new float[] { 20, 4, 8.1f, 15.9f });
                    line(new float[] { 14.5f, 14.5f, 20, 20 });
                    line(new float[] { 8.1f, 8.1f, 12, 12 });
                    break;
                // Ctrl+click's two (ClickMap): into another bag, and onto the ground
                case "move":
                    line(new float[] { 3, 12, 15, 12 });
                    line(new float[] { 10, 7, 15, 12, 10, 17 });
                    line(new float[] { 20, 4, 20, 20 });
                    break;
                case "drop":
                    line(new float[] { 12, 3, 12, 15 });
                    line(new float[] { 7, 10, 12, 15, 17, 10 });
                    line(new float[] { 4, 20, 20, 20 });
                    break;
                case "names":   // a name tag, square-cut, with a line of writing
                    line(new float[] { 3, 7, 17, 7, 21, 12, 17, 17, 3, 17, 3, 7 });
                    line(new float[] { 6, 12, 13, 12 });
                    break;
                case "home":    // a house: the roof, the walls, the door
                    line(new float[] { 2, 12, 12, 3, 22, 12 });
                    line(new float[] { 5, 10, 5, 21, 19, 21, 19, 10 });
                    line(new float[] { 10, 21, 10, 15, 14, 15, 14, 21 });
                    break;
                case "star":
                    line(new float[] { 12, 2, 15.1f, 8.3f, 22, 9.3f, 17, 14.1f, 18.2f, 21, 12, 17.8f, 5.8f, 21, 7, 14.1f, 2, 9.3f, 8.9f, 8.3f, 12, 2 });
                    break;
                case "remove":
                    line(new float[] { 6, 6, 18, 18 });
                    line(new float[] { 18, 6, 6, 18 });
                    break;
                case "more":
                    circle(5, 12, 1);
                    circle(12, 12, 1);
                    circle(19, 12, 1);
                    break;
                case "selected":
                    line(new float[] { 4, 8, 12, 16, 20, 8 });
                    break;
                // the indicator's emblems: what the target IS, one glyph per kind
                case "kind_heart":
                    arc(8, 9, 4, 180, 360);
                    arc(16, 9, 4, 180, 360);
                    line(new float[] { 4, 9, 12, 20, 20, 9 });
                    break;
                case "kind_swords":
                    line(new float[] { 4, 4, 17, 17 });
                    line(new float[] { 14, 19, 19, 14 });
                    line(new float[] { 17, 17, 20, 20 });
                    line(new float[] { 20, 4, 7, 17 });
                    line(new float[] { 5, 14, 10, 19 });
                    line(new float[] { 7, 17, 4, 20 });
                    break;
                case "kind_skull":
                    arc(12, 10, 7, 140, 400);
                    line(new float[] { 6.6f, 14.5f, 8, 17, 16, 17, 17.4f, 14.5f });
                    circle(9, 11, 1.8f);
                    circle(15, 11, 1.8f);
                    line(new float[] { 9, 17, 9, 20, 15, 20, 15, 17 });
                    line(new float[] { 12, 17, 12, 20 });
                    break;
                case "kind_chest":
                    line(new float[] { 4, 9, 20, 9, 20, 20, 4, 20, 4, 9 });
                    line(new float[] { 4, 9, 5, 5, 19, 5, 20, 9 });
                    line(new float[] { 11, 11, 13, 11, 13, 14, 11, 14, 11, 11 });
                    break;
                case "kind_person":
                    circle(12, 8, 4);
                    line(new float[] { 4, 21, 5, 16, 8, 14, 16, 14, 19, 16, 20, 21 });
                    break;
                case "skills":
                    line(new float[] { 12, 2, 15, 9, 22, 12, 15, 15, 12, 22, 9, 15, 2, 12, 9, 9, 12, 2 });
                    break;
                default:
                    line(new float[] { 5, 12, 19, 12, 14, 7 });
                    line(new float[] { 19, 12, 14, 17 });
                    break;
            }
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) * 24 / size, (y + .5f) * 24 / size);
                float d = 100;
                foreach (var path in paths)
                for (int i = 1; i < path.Length; i++)
                {
                    var a = path[i - 1];
                    var b = path[i];
                    var v = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / Mathf.Max(.0001f, v.sqrMagnitude));
                    d = Mathf.Min(d, Vector2.Distance(p, a + t * v));
                }
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((.85f - d) * size / 24));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD_" + name, filterMode = FilterMode.Bilinear };
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            sprite.name = "HUD_" + name;
            Cache[name] = sprite;
            return sprite;
        }
        // Harness door: the named glyphs side by side on a dark ground, as a PNG to
        // judge a set by (the kit's mockup, drawn by the renderer that ships).
        internal static string DumpSheet(string path, string names)
        {
            string[] list = names.Split('+');
            const int cell = 64, pad = 16;
            var sheet = new Texture2D(list.Length * (cell + pad) + pad, cell + 2 * pad, TextureFormat.RGBA32, false);
            var ground = new Color(0.16f, 0.12f, 0.09f, 1f);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) { fill[i] = ground; }
            sheet.SetPixels(fill);
            for (int n = 0; n < list.Length; n++)
            {
                Texture2D glyph = Get(list[n].Trim()).texture;
                Color[] px = glyph.GetPixels();
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    Color c = px[y * cell + x];
                    int sx = pad + n * (cell + pad) + x, sy = pad + y;
                    sheet.SetPixel(sx, sy, Color.Lerp(ground, new Color(0.96f, 0.9f, 0.78f, 1f), c.a));
                }
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.Destroy(sheet);
            return path;
        }

        internal static void Clear()
        {
            foreach (var s in Cache.Values)
            {
                if (s != null)
                {
                    Object.Destroy(s.texture);
                    Object.Destroy(s);
                }
            }
            Cache.Clear();
        }
    }
}
