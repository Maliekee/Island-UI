using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace IslandUI.UI
{
    // A TMP FONT FROM AN INSTALLED FONT FILE. Theme.OsFont builds one from
    // Font.CreateDynamicFontFromOSFont, and on this game's Unity (2021.3, TMP 3.0)
    // that returns null every time: TMP loads a face from the Font's embedded data,
    // and an OS font has none, so CreateFontAsset fails and the caller falls back
    // to the game's font without a word. FontEngine CAN load a face from a file,
    // so this renders the glyphs a UI needs from C:\Windows\Fonts into an SDF atlas
    // once and wraps them in a STATIC font asset; anything outside the set falls
    // back to the game's font. Found building the tool shed (window-system.md 6.30).
    internal static class SystemFont
    {
        private const int PointSize = 72;
        private const int Padding = 7;
        private const int Atlas = 1024;

        // what the UI writes: ASCII, Latin-1, the typographic marks it uses
        private static readonly string Glyphs =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
            + "¡¢£¥\u00A7©«®°±²³´µ¶·¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÑÒÓÔÕÖ×ØÙÚÛÜÝßàáâãäåæçèéêëìíîïñòóôõö÷øùúûüýÿ"
            + "–—‘’“”…•→←↑↓✓✗★";

        private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic;

        public static TMP_FontAsset Build(string file, string name, TMP_FontAsset fallback)
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts), file);
            if (!System.IO.File.Exists(path))
            {
                Plugin.Log.LogWarning($"SystemFont: {path} not found");
                return null;
            }
            return Build(() => FontEngine.LoadFontFace(path, PointSize), file, name, fallback);
        }

        // The same from a face the mod carries (an embedded OFL font), so a look does
        // not hang on what a player's Windows happens to have installed. It is written
        // to BepInEx's cache and loaded as a file: loaded from a byte array, FreeType
        // went on reading the array after the call, and the second build after a
        // reload rendered no glyphs at all.
        public static TMP_FontAsset BuildEmbedded(string resource, string name, TMP_FontAsset fallback)
        {
            string path;
            try
            {
                using (var stream = typeof(SystemFont).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                    {
                        Plugin.Log.LogWarning($"SystemFont: no embedded {resource}");
                        return null;
                    }
                    var data = new byte[stream.Length];
                    int offset = 0, read;
                    while (offset < data.Length && (read = stream.Read(data, offset, data.Length - offset)) > 0) offset += read;
                    string folder = System.IO.Path.Combine(BepInEx.Paths.CachePath, "IslandUI");
                    System.IO.Directory.CreateDirectory(folder);
                    // named by content, so a changed font is a new file, never a stale one
                    path = System.IO.Path.Combine(folder, resource + "." + Hash(data) + ".ttf");
                    if (!System.IO.File.Exists(path)) System.IO.File.WriteAllBytes(path, data);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"SystemFont: could not stage {resource}: {e.Message}");
                return null;
            }
            return Build(() => FontEngine.LoadFontFace(path, PointSize), resource, name, fallback);
        }

        private static string Hash(byte[] data)
        {
            uint h = 2166136261;
            foreach (byte b in data) h = (h ^ b) * 16777619;
            return h.ToString("x8");
        }

        private static TMP_FontAsset Build(System.Func<FontEngineError> load, string file, string name, TMP_FontAsset fallback)
        {
            try
            {
                FontEngine.InitializeFontEngine();
                if (load() != FontEngineError.Success)
                {
                    Plugin.Log.LogWarning($"SystemFont: FontEngine could not load {file}");
                    return null;
                }
                FaceInfo face = FontEngine.GetFaceInfo();
                MethodInfo addGlyph = typeof(FontEngine).GetMethod("TryAddGlyphToTexture", Any, null,
                    new[] { typeof(uint), typeof(int), typeof(GlyphPackingMode), typeof(List<GlyphRect>),
                        typeof(List<GlyphRect>), typeof(GlyphRenderMode), typeof(Texture2D), typeof(Glyph).MakeByRefType() },
                    null);
                if (addGlyph == null)
                {
                    Plugin.Log.LogWarning("SystemFont: FontEngine.TryAddGlyphToTexture not found");
                    return null;
                }

                var texture = new Texture2D(Atlas, Atlas, TextureFormat.Alpha8, false);
                texture.name = name + " Atlas";
                var clear = new Color32[Atlas * Atlas];
                texture.SetPixels32(clear);
                var free = new List<GlyphRect> { new GlyphRect(0, 0, Atlas - 1, Atlas - 1) };
                var used = new List<GlyphRect>();
                var glyphs = new List<Glyph>();
                var characters = new List<TMP_Character>();
                var seen = new Dictionary<uint, Glyph>();
                foreach (char ch in Glyphs)
                {
                    uint unicode = ch;
                    if (!FontEngine.TryGetGlyphWithUnicodeValue(unicode, GlyphLoadFlags.LOAD_DEFAULT, out Glyph probe))
                    {
                        continue;
                    }
                    if (!seen.TryGetValue(probe.index, out Glyph glyph))
                    {
                        object[] args = { probe.index, Padding, GlyphPackingMode.BestShortSideFit, free, used,
                            GlyphRenderMode.SDFAA, texture, null };
                        if (!(bool)addGlyph.Invoke(null, args))
                        {
                            continue;
                        }
                        glyph = (Glyph)args[7];
                        seen[probe.index] = glyph;
                        glyphs.Add(glyph);
                    }
                    characters.Add(new TMP_Character(unicode, glyph));
                }
                texture.Apply(false, false);

                TMP_FontAsset asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
                asset.name = name;
                Set(asset, "m_Version", "1.1.0");
                asset.faceInfo = face;
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
                asset.atlasTextures = new[] { texture };
                Set(asset, "m_AtlasWidth", Atlas);
                Set(asset, "m_AtlasHeight", Atlas);
                Set(asset, "m_AtlasPadding", Padding);
                Set(asset, "m_AtlasRenderMode", GlyphRenderMode.SDFAA);
                Set(asset, "m_GlyphTable", glyphs);
                Set(asset, "m_CharacterTable", characters);

                // the game font's material, re-pointed at our atlas: the same shader
                // and keywords every other label in the game is drawn with
                Material template = fallback != null ? fallback.material : null;
                Material material = template != null ? new Material(template)
                    : new Material(Shader.Find("TextMeshPro/Distance Field"));
                material.name = name + " Material";
                material.SetTexture(ShaderUtilities.ID_MainTex, texture);
                material.SetFloat(ShaderUtilities.ID_TextureWidth, Atlas);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, Atlas);
                material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
                material.SetFloat(ShaderUtilities.ID_WeightNormal, asset.normalStyle);
                material.SetFloat(ShaderUtilities.ID_WeightBold, asset.boldStyle);
                asset.material = material;
                if (fallback != null)
                {
                    asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                }
                asset.ReadFontAssetDefinition();
                Plugin.Log.LogInfo($"SystemFont: '{name}' built from {file} ({characters.Count} characters)");
                return asset;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"SystemFont: '{name}' from {file} failed: {e}");
                return null;
            }
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, Any);
            if (f == null)
            {
                Plugin.Log.LogWarning($"SystemFont: TMP_FontAsset has no field {field}");
                return;
            }
            f.SetValue(target, value);
        }
    }
}
