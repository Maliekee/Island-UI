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
        // folders stacked behind it (DossierTabs.Lift a folder) - so the chip still
        // sits on the edge you see. Pooled frames change skins: every skin places it.
        public static void Place(Image chip, float rise)
        {
            var at = new Vector2(-FromRight, rise);
            if (chip != null && chip.rectTransform.anchoredPosition != at)
            {
                chip.rectTransform.anchoredPosition = at;
            }
        }

        // the face for the pointer: `down` is pressed with the pointer still on it
        public static void Paint(Image chip, bool hot, bool down)
        {
            if (chip == null)
            {
                return;
            }
            Sprite face = Theme.Kit(down ? "kit_close_down" : hot ? "kit_close_hot" : "kit_close_idle", 0);
            if (chip.sprite != face)
            {
                chip.sprite = face;
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
