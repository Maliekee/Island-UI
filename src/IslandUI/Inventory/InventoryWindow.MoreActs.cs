using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static bool _moreActsOpen;
        private static Image _moreActsPanel;
        private static readonly List<TargetVerbs.Verb> MoreActs = new List<TargetVerbs.Verb>();
        private static readonly List<Button> MoreButtons = new List<Button>();
        private static readonly List<TextMeshProUGUI> MoreLabels = new List<TextMeshProUGUI>();

        internal static void ToggleMoreActs()
        {
            _moreActsOpen = !_moreActsOpen;
            PaintMoreActs();
        }

        private static void PaintMoreActs()
        {
            if (_moreActsPanel == null && _moreActsOpen)
            {
                _moreActsPanel = Theme.KitImage(_unit.transform, "MoreActions", "kit_plank_walnut", 18, Color.white);
                _moreActsPanel.raycastTarget = true;
                MoreButtons.Clear();
                MoreLabels.Clear();
            }
            if (_moreActsPanel == null)
                return;
            bool show = _moreActsOpen && MoreActs.Count > 0;
            _moreActsPanel.gameObject.SetActive(show);
            if (!show)
                return;
            while (MoreButtons.Count < MoreActs.Count)
            {
                int index = MoreButtons.Count;
                var face = Theme.KitImage(_moreActsPanel.transform, "Action" + index, "kit_plank_walnut", 18, Color.white);
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.onClick.AddListener(() => { if (index < MoreActs.Count) { string name = MoreActs[index].Name; _moreActsOpen = false; PressUnitAct(_unitFor, name); PaintMoreActs(); } });
                var text = Theme.MakeKitText(face.transform, "Label", 9, Theme.KitCream, TextAlignmentOptions.MidlineLeft);
                text.raycastTarget = false;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                MoreButtons.Add(button);
                MoreLabels.Add(text);
            }
            float height = MoreActs.Count * 20 + 8;
            Theme.Place(_moreActsPanel, 0, -height - 4, 140, height);
            for (int i = 0; i < MoreButtons.Count; i++)
            {
                bool live = i < MoreActs.Count;
                MoreButtons[i].gameObject.SetActive(live);
                if (!live)
                    continue;
                Theme.Place(MoreButtons[i].GetComponent<Image>(), 4, 4 + i * 20, 132, 19);
                Theme.Place(MoreLabels[i], 5, 0, 122, 19);
                MoreLabels[i].text = MoreActs[i].Label;
                MoreButtons[i].interactable = InReach(_unitFor) || ReachFree(MoreActs[i].Name);
            }
            _moreActsPanel.transform.SetAsLastSibling();
        }
    }
}
