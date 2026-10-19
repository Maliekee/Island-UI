using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IslandUI.UI
{
    // Every sprite the game has LOADED, written out as PNGs so a glyph can be
    // chosen by looking at it.
    //
    // This exists because the icon question kept being answered by guessing.
    // Theme harvests its badges by NAME MATCH ("does this sprite's name contain
    // 'chest'?"), which finds *a* sprite and says nothing about whether it belongs
    // to the same visual family as the one next to it — and that is exactly the
    // fault the gutter icons had: ui_equip_cloth is white line art, ui_backpack is
    // a full-colour painted item, and no name told anyone so.
    //
    // Plugin.DumpUiSpriteNames already lists names, and names are what turned out
    // not to be enough. It also only sees sprites that some Image in the canvas is
    // CURRENTLY showing, so anything loaded-but-unused is invisible to it;
    // FindObjectsOfTypeAll sees the whole loaded pool.
    internal static class SpriteDump
    {
        // Atlas textures are almost never CPU-readable, and one blit per SPRITE
        // would redo the same atlas dozens of times.
        private static readonly Dictionary<Texture2D, Texture2D> _readable =
            new Dictionary<Texture2D, Texture2D>();

        internal static void Run(string prefix, string directory)
        {
            _readable.Clear();
            Directory.CreateDirectory(directory);
            var seen = new HashSet<string>();
            var names = new SortedSet<string>();
            // A PNG says what a glyph LOOKS like and not how it is drawn. A frame
            // sprite is 9-sliced, and without its border and pixelsPerUnit anything
            // rebuilding the layout outside the game has to guess where the
            // stretchable middle is - which is the difference between a frame and a
            // smear. Written beside the pictures so the two cannot drift apart.
            var manifest = new List<string>();
            int written = 0;
            int skipped = 0;

            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                Sprite sprite = all[i];
                if (sprite == null || sprite.texture == null)
                {
                    continue;
                }
                string name = sprite.name;
                if (!string.IsNullOrEmpty(prefix)
                    && !name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                // one file per NAME: the same sprite is referenced from many
                // objects, and duplicates would just be the same picture again
                if (!seen.Add(name))
                {
                    continue;
                }
                names.Add(name);
                if (Write(sprite, directory))
                {
                    written++;
                    Vector4 b = sprite.border;
                    Rect r = sprite.textureRect;
                    manifest.Add("  {\"name\":\"" + Sanitise(name) + "\""
                        + $",\"w\":{r.width:0},\"h\":{r.height:0}"
                        + $",\"border\":[{b.x:0},{b.y:0},{b.z:0},{b.w:0}]"
                        + $",\"ppu\":{sprite.pixelsPerUnit:0.###}"
                        + $",\"pivot\":[{sprite.pivot.x:0.###},{sprite.pivot.y:0.###}]}}");
                }
                else
                {
                    skipped++;
                }
            }

            File.WriteAllText(Path.Combine(directory, "manifest.json"),
                "[\n" + string.Join(",\n", manifest.ToArray()) + "\n]\n");

            foreach (KeyValuePair<Texture2D, Texture2D> pair in _readable)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value);
                }
            }
            _readable.Clear();

            Plugin.Log.LogInfo($"SpriteDump: {written} written, {skipped} skipped -> {directory}");
            Plugin.Log.LogInfo($"SpriteDump: names: {string.Join(", ", new List<string>(names).ToArray())}");
        }

        private static bool Write(Sprite sprite, string directory)
        {
            Texture2D source = Readable(sprite.texture);
            if (source == null)
            {
                return false;
            }
            Rect r = sprite.textureRect;
            int x = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, source.width);
            int y = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, source.height);
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, source.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, source.height - y);
            Texture2D cut = null;
            try
            {
                cut = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cut.SetPixels(source.GetPixels(x, y, w, h));
                cut.Apply();
                // one file per sprite rather than a contact sheet: the sheet is
                // built outside the game, where there is a font to label it with
                File.WriteAllBytes(Path.Combine(directory, Sanitise(sprite.name) + ".png"),
                    cut.EncodeToPNG());
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"SpriteDump: '{sprite.name}' failed: {e.Message}");
                return false;
            }
            finally
            {
                if (cut != null)
                {
                    Object.Destroy(cut);
                }
            }
        }

        // A texture imported without Read/Write is unreadable from script; a blit
        // through a RenderTexture is the way to get the pixels back regardless,
        // and it costs one copy per ATLAS rather than one per sprite.
        private static Texture2D Readable(Texture2D texture)
        {
            Texture2D copy;
            if (_readable.TryGetValue(texture, out copy))
            {
                return copy;
            }
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                copy.Apply();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"SpriteDump: atlas '{texture.name}' unreadable: {e.Message}");
                copy = null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            _readable[texture] = copy;
            return copy;
        }

        private static string Sanitise(string name)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                sb.Append(System.Array.IndexOf(bad, name[i]) >= 0 ? '_' : name[i]);
            }
            return sb.ToString();
        }
    }
}
