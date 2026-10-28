using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // ONE CLOSE BUTTON, square rather than round. Every window's close is the enamel tag - the
    // target medallion's brass rim and red enamel on a square tag, a cream X outlined in
    // ink, three faces baked by a dev script - at one size, centred on the
    // frame's top edge 10 units in from its right, and it answers the pointer the same way
    // everywhere: hover lights the enamel, a press sinks it. It replaced five looks: the
    // shed's red tag, a walnut chip with a 7- or an 8-unit cross (the settings' larger),
    // the kit roster's flat slate chip, and the sheets' walnut chip whose cross a pooled
    // crate had switched off.
    internal static class CloseChip
    {
        public const float Size = 14f;
        public const float FromRight = 10f;   // its centre, from the frame's right edge
        public const string Face = "kit_close";   // + _idle, _hot, _down

        // a chip on the parent's top-right corner, half over its top edge
        public static Image Create(Transform parent)
        {
            Image chip = Theme.KitImage(parent, "Close", "kit_close_idle", 0, Color.white);
            chip.raycastTarget = true;
            var rect = chip.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            rect.anchoredPosition = new Vector2(-FromRight, 0f);
            return chip;
        }

        // `rise`: how far the frame's visible top stands above its rect - the sheet's
        // stacked sheets behind it - so the chip still sits on the edge you see, and
        // `size`, for a window whose close is its own painted key (the sheet's, look
        // 04), and `fromRight`, for one whose key sits inside its frame (the
        // inventory's, look 07). Pooled frames change skins: every skin places it,
        // so a size and a place come back.
        public static void Place(Image chip, float rise, float size = Size, float fromRight = FromRight)
        {
            if (chip == null)
            {
                return;
            }
            var at = new Vector2(-fromRight, rise);
            if (chip.rectTransform.anchoredPosition != at)
            {
                chip.rectTransform.anchoredPosition = at;
            }
            var wh = new Vector2(size, size);
            if (chip.rectTransform.sizeDelta != wh)
            {
                chip.rectTransform.sizeDelta = wh;
            }
        }

        // a set's three names, made once: Paint runs every frame
        private static readonly Dictionary<string, string[]> Faces = new Dictionary<string, string[]>();

        private static string[] FacesOf(string face)
        {
            if (!Faces.TryGetValue(face, out string[] names))
            {
                names = new[] { face + "_idle", face + "_hot", face + "_down" };
                Faces[face] = names;
            }
            return names;
        }

        // the face for the pointer: `down` is pressed with the pointer still on it;
        // `face`, the set the faces come from
        public static void Paint(Image chip, bool hot, bool down, string face = Face)
        {
            if (chip == null)
            {
                return;
            }
            string[] faces = FacesOf(face);
            Sprite sprite = Theme.Kit(down ? faces[2] : hot ? faces[1] : faces[0], 0);
            if (chip.sprite != sprite)
            {
                chip.sprite = sprite;
                chip.type = Image.Type.Simple;
                chip.preserveAspect = true;
            }
            if (chip.color != Color.white)
            {
                chip.color = Color.white;
            }
        }
    }
}
