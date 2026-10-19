using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // The approved options artwork, scoped to the settings window. Rects below
    // are measured in the source PNG's top-left pixel coordinates, not grid cells.
    // Keeping the textures embedded also makes hot reload / distribution atomic.
    internal sealed class SettingsArt : MonoBehaviour
    {
        private readonly List<Object> _assets = new List<Object>();
        private TMP_FontAsset _regular, _bold;
        internal Sprite Frame, Button, ButtonHot, ButtonDown, Slip, SlipActive, Close;
        internal Sprite ToggleOff, ToggleOn, Field, FieldFocus, FieldInvalid;
        // The sheet draws the slider and the scrollbar whole, knob and thumb baked
        // in. They are cut apart here so the moving part can move: the lit run left
        // of the knob, the dark run right of it, the knob; the track's two ends
        // either side of the thumb, the thumb.
        internal Sprite SliderFill, SliderEmpty, SliderKnob, ScrollCap, ScrollTrack, ScrollThumb;

        internal void Init()
        {
            Texture2D frame = Load("IslandUI.SettingsFrame.png");
            Texture2D parts = Load("IslandUI.SettingsParts.png");
            Frame = Cut(frame, "Settings frame", 0, 0, 1536, 1024);
            Button = Cut(parts, "Settings oak", 28, 120, 226, 94, 18);
            ButtonHot = Cut(parts, "Settings pine", 277, 119, 229, 95, 18);
            ButtonDown = Cut(parts, "Settings pressed", 529, 120, 227, 94, 18);
            Slip = Cut(parts, "Settings slip", 783, 343, 226, 110, 18);
            SlipActive = Cut(parts, "Settings selected slip", 1034, 343, 228, 111, 18);
            Close = Cut(parts, "Settings close", 1200, 802, 110, 110);
            ToggleOff = Cut(parts, "Settings toggle off", 1045, 587, 204, 92);
            ToggleOn = Cut(parts, "Settings toggle on", 1298, 587, 200, 93);
            Field = Cut(parts, "Settings field", 30, 359, 221, 73, 20);
            FieldFocus = Cut(parts, "Settings field focused", 280, 358, 224, 74, 20);
            FieldInvalid = Cut(parts, "Settings field invalid", 532, 358, 223, 74, 20);
            SliderFill = Cut(parts, "Settings slider fill", 621, 839, 160, 33, new Vector4(16, 0, 0, 0));
            SliderEmpty = Cut(parts, "Settings slider empty", 845, 839, 160, 33, new Vector4(0, 0, 16, 0));
            SliderKnob = Cut(parts, "Settings slider knob", 788, 831, 50, 50);
            ScrollCap = Cut(parts, "Settings scroll cap", 1089, 766, 23, 34);
            ScrollTrack = Cut(parts, "Settings scroll track", 1089, 870, 23, 81, new Vector4(0, 12, 0, 0));
            ScrollThumb = Cut(parts, "Settings scroll thumb", 1089, 802, 23, 62);
            _regular = SystemFont.Build("georgia.ttf", "IslandUI Settings Georgia", Theme.Font);
            _bold = SystemFont.Build("georgiab.ttf", "IslandUI Settings Georgia Bold", Theme.Font);
            OwnFont(_regular);
            OwnFont(_bold);
        }

        private Texture2D Load(string resource)
        {
            using (Stream stream = typeof(SettingsArt).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) { throw new FileNotFoundException("Missing settings artwork", resource); }
                using (var bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _assets.Add(texture);
                    texture.name = resource;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
                    {
                        throw new IOException("Could not decode " + resource);
                    }
                    return texture;
                }
            }
        }

        private Sprite Cut(Texture2D texture, string name, int x, int y, int w, int h, int border = 0)
        {
            return Cut(texture, name, x, y, w, h, new Vector4(border, border, border, border));
        }

        // border is Unity's order: left, bottom, right, top
        private Sprite Cut(Texture2D texture, string name, int x, int y, int w, int h, Vector4 border)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(x, texture.height - y - h, w, h),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            _assets.Add(sprite);
            return sprite;
        }

        internal static void Skin(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Theme.KitPpu;
        }

        // A sliced piece whose ends must keep the art's own proportions (a track's
        // round cap): the borders are drawn at the scale the rest of it is.
        internal static void SkinAtScale(Image image, Sprite sprite, float artPixelsPerUnit)
        {
            Skin(image, sprite);
            image.pixelsPerUnitMultiplier = artPixelsPerUnit;
        }

        internal Image Image(Transform parent, string name, Sprite sprite)
        {
            GameObject go = Owned.New(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            Skin(image, sprite);
            return image;
        }

        internal void Type(TextMeshProUGUI text, bool bold = false)
        {
            TMP_FontAsset face = bold ? _bold ?? _regular : _regular;
            if (face != null) { text.font = face; }
            text.fontStyle = bold && _bold == null ? FontStyles.Bold : FontStyles.Normal;
            text.enableWordWrapping = false;
            Theme.FlattenType(text);
        }

        internal TextMeshProUGUI Text(Transform parent, string name, float size, Color color,
            TextAlignmentOptions alignment, bool bold = false)
        {
            TextMeshProUGUI text = Theme.MakeText(parent, name, size, color, alignment);
            Type(text, bold);
            return text;
        }

        private void OwnFont(TMP_FontAsset font)
        {
            if (font == null) { return; }
            if (font.atlasTextures != null)
            {
                foreach (Texture2D texture in font.atlasTextures)
                {
                    if (texture != null) { _assets.Add(texture); }
                }
            }
            if (font.material != null) { _assets.Add(font.material); }
            _assets.Add(font);
        }

        private void OnDestroy()
        {
            // Also called when the harness destroys Owned objects on hot reload.
            for (int i = _assets.Count - 1; i >= 0; i--)
            {
                if (_assets[i] != null) { Destroy(_assets[i]); }
            }
            _assets.Clear();
        }
    }
}
