using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // "IN YOUR HAND" - ONE MARK FOR IT. The action bar edged a worn item green
    // and the tool belt pinned a gold hand beside the tool: two words for one fact.
    // Both now say it with gold square corner brackets, the "selected" mark - no
    // fill, so nothing under it is covered, and crisp at any size.
    //
    // Eight plain rects on eight dark ones a rim wider, rather than an Outline per
    // arm: an Outline would draw its rim across the inside of every corner joint.
    internal sealed class EquipBrackets
    {
        internal static readonly Color Gold = new Color32(226, 178, 74, 255);
        private static readonly Color Rim = new Color32(24, 18, 12, 255);
        private const float RimWidth = 0.5f;

        private readonly GameObject _root;

        // x, y, w, h: the box the corners stand on, top-left based like Theme.Place
        internal EquipBrackets(Transform parent, float x, float y, float w, float h,
            float arm, float thick)
        {
            _root = Owned.New("EquipBrackets");
            _root.transform.SetParent(parent, false);
            Theme.Place((RectTransform)_root.transform, x, y, w, h);
            float[][] arms =
            {
                new[] { 0f, 0f, arm, thick }, new[] { 0f, 0f, thick, arm },
                new[] { w - arm, 0f, arm, thick }, new[] { w - thick, 0f, thick, arm },
                new[] { 0f, h - thick, arm, thick }, new[] { 0f, h - arm, thick, arm },
                new[] { w - arm, h - thick, arm, thick }, new[] { w - thick, h - arm, thick, arm },
            };
            foreach (float[] a in arms)
            {
                Image rim = Theme.KitImage(_root.transform, "Rim", null, 0, Rim);
                Theme.Place(rim, a[0] - RimWidth, a[1] - RimWidth, a[2] + 2f * RimWidth, a[3] + 2f * RimWidth);
            }
            foreach (float[] a in arms)
            {
                Image gold = Theme.KitImage(_root.transform, "Arm", null, 0, Gold);
                Theme.Place(gold, a[0], a[1], a[2], a[3]);
            }
            _root.SetActive(false);
        }

        internal bool Shown
        {
            set
            {
                if (_root.activeSelf != value)
                {
                    _root.SetActive(value);
                }
            }
        }

        internal void ToFront()
        {
            _root.transform.SetAsLastSibling();
        }
    }
}
