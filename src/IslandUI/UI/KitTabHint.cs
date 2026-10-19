using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // A label above an icon rail. It shares the rail's visibility and lifetime.
    internal sealed class KitTabHint
    {
        private readonly Image _face;
        private readonly TextMeshProUGUI _label;
        internal KitTabHint(RectTransform rail)
        {
            _face = Theme.KitImage(rail, "Tab hint", "kit_plank_walnut", 18, Color.white);
            _face.raycastTarget = false;
            _label = Theme.MakeKitText(_face.transform, "Label", 8, Theme.KitCream, TextAlignmentOptions.Center);
            _label.raycastTarget = false;
            _label.enableWordWrapping = false;
            _face.gameObject.SetActive(false);
        }
        internal void Show(string label)
        {
            _face.gameObject.SetActive(!string.IsNullOrEmpty(label));
            if (string.IsNullOrEmpty(label))
                return;
            _label.text = label;
            float width = Mathf.Max(40, _label.preferredWidth + 12);
            Theme.Place(_face, 0, -24, width, 16);
            Theme.Place(_label, 4, 0, width - 8, 16);
            _face.transform.SetAsLastSibling();
        }
    }
}
