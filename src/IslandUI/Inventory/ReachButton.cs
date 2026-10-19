using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // The HUD toggle that raises the containers in reach, beside vanilla's
    // backpack icon.
    //
    // This exists because the reach pool is IRREDUCIBLY large and we stopped
    // fighting that. Twelve chests is twelve chests; shrinking
    // the rows only postpones the problem. What was actually wrong is that the
    // pool had no toggle of its own — it rode the BAG's, so opening your bags
    // implicitly requested a survey you never asked for. The rule the window
    // system now keeps:
    //
    //   AUTOMATIC = what the game decided you are dealing with (the target).
    //   MANUAL    = what you chose to survey (your bags, the vicinity).
    //
    // NearbyDefaultOpen was trying to reach the same place and could not, because
    // a section collapsed inside someone else's window still sits in the column
    // inviting expansion, and once expanded it stays expanded for the session.
    //
    // The widget is the quick-stack button, resurrected. It was deleted in 70652b0
    // for having no home ("a control with no home rather than a control in the
    // wrong home"); this control has one, and the docking, the pointer test and the
    // chest-glyph harvest were all already written and working.
    internal static class ReachButton
    {
        internal static bool PointerOver
        {
            get
            {
                return _rect != null && _rect.gameObject.activeInHierarchy && ContainsPointer(_rect);
            }
        }

        private static RectTransform _rect;
        private static Image _face;
        private static Color _faceColor;
        private static TextMeshProUGUI _caption;
        private static Sprite _chestSprite;
        private static bool _pressed;

        internal static void ResetForSceneLoad()
        {
            DestroyOwned();
            _chestSprite = null;
            _pressed = false;
        }

        private static void DestroyOwned()
        {
            if (_rect != null)
            {
                Object.Destroy(_rect.gameObject);
            }
            _rect = null;
            _face = null;
            _caption = null;
        }

        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || !Settings.ShowNearby.Value)
            {
                Hide();
                return;
            }
            if (!Ensure(mn))
            {
                return;
            }
            if (!_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(true);
            }
            PlaceNextToBag();

            bool over = ContainsPointer(_rect);
            bool down = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);
            bool up = Input.GetMouseButtonUp(0);

            if (over && down && !InventoryWindow.IsDragging)
            {
                _pressed = true;
            }
            if (_pressed && (!held || !over))
            {
                if (up && over && !InventoryWindow.IsDragging)
                {
                    InventoryWindow.ToggleReach();
                }
                _pressed = false;
            }
            Paint(over);
        }

        private static void Hide()
        {
            if (_rect != null && _rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(false);
            }
        }

        private static bool Ensure(ManagersScript mn)
        {
            if (mn.inventory == null || !Theme.Ensure(mn, mn.inventory))
            {
                return false;
            }
            GameObject bag = Theme.Live(Theme.BagButton) ? Theme.BagButton : null;
            if (bag == null)
            {
                return false;
            }
            Canvas canvas = CanvasOf(mn);
            if (canvas == null)
            {
                return false;
            }
            Sprite chest = ChestSprite(canvas.rootCanvas);
            if (chest == null)
            {
                return false;
            }
            // Alive is alive — Unity's fake-null means a leftover destroyed with
            // its scene reports as null here and we rebuild.
            if (_rect != null)
            {
                return true;
            }
            DestroyOwned();
            SweepLeftovers(bag.transform.parent);

            // Clone the BAG, not the left-cluster chest. The bag is the pack's own
            // chrome and parent; the Collections button is only the art.
            // ignoreLayout: the pack is a laid-out row of N icons. Inserting a real
            // sibling made the row reflow and two icons sat on one slot.
            GameObject go = Owned.Instantiate(bag, bag.transform.parent, false);
            go.name = "IslandUI_Reach";
            StripInteractive(go);
            var ignore = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;
            go.SetActive(true);
            _rect = (RectTransform)go.transform;
            _face = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>(true);
            if (_face != null)
            {
                _face.sprite = chest;
                _face.raycastTarget = true;
                _faceColor = _face.color;
            }

            _caption = Theme.MakeText(go.transform, "Caption", Theme.FontCaption,
                Theme.AccentGold, TextAlignmentOptions.Midline);
            _caption.enableWordWrapping = false;
            _caption.overflowMode = TextOverflowModes.Overflow;
            _caption.raycastTarget = false;
            _caption.gameObject.SetActive(false);
            var capRect = (RectTransform)_caption.transform;
            capRect.anchorMin = new Vector2(0.5f, 1f);
            capRect.anchorMax = new Vector2(0.5f, 1f);
            capRect.pivot = new Vector2(0.5f, 0f);
            capRect.anchoredPosition = new Vector2(0f, 2f);
            capRect.sizeDelta = new Vector2(140f, 12f);

            Plugin.Log.LogInfo("Reach: HUD button cloned from bag " + bag.name
                + " sprite=" + chest.name);
            return true;
        }

        // Vanilla's own left-panel chest button, borrowed for its art. The glyph
        // does double duty — that button opens the Collections panel — but a chest
        // IS what this raises, and by the game's own convention a painted glyph is
        // a button you press, which this is.
        private static Sprite ChestSprite(Canvas root)
        {
            if (_chestSprite != null)
            {
                return _chestSprite;
            }
            GameObject chest = Theme.FindByPersistentCall(root, "LeftPanelState", 2);
            if (chest == null)
            {
                return null;
            }
            Image image = chest.GetComponent<Image>()
                ?? chest.GetComponentInChildren<Image>(true);
            if (image == null || image.sprite == null)
            {
                return null;
            }
            _chestSprite = image.sprite;
            Plugin.Log.LogInfo("Reach: chest glyph " + _chestSprite.name
                + " from " + chest.name + " live=" + chest.activeInHierarchy);
            return _chestSprite;
        }

        private static void StripInteractive(GameObject go)
        {
            foreach (Component comp in go.GetComponents<Component>())
            {
                bool keep = comp is RectTransform || comp is CanvasRenderer || comp is Image
                    || comp is Shadow || comp is TextMeshProUGUI || comp is Text
                    || comp is CanvasGroup || comp is LayoutElement;
                if (!keep)
                {
                    Object.DestroyImmediate(comp);
                }
            }
            foreach (Transform child in go.transform)
            {
                StripInteractive(child.gameObject);
            }
        }

        private static void PlaceNextToBag()
        {
            RectTransform bag = Theme.Live(Theme.BagButton)
                ? Theme.BagButton.transform as RectTransform
                : null;
            if (bag == null)
            {
                return;
            }
            if (_rect.parent != bag.parent)
            {
                _rect.SetParent(bag.parent, false);
            }
            _rect.anchorMin = bag.anchorMin;
            _rect.anchorMax = bag.anchorMax;
            _rect.pivot = bag.pivot;
            float width = bag.rect.width > 4f ? bag.rect.width : ButtonSize;
            float height = bag.rect.height > 4f ? bag.rect.height : ButtonSize;
            _rect.sizeDelta = new Vector2(width, height);
            // left of the bag, outside the pack's layout so the other icons keep
            // the slots they already have
            _rect.anchoredPosition = bag.anchoredPosition
                + new Vector2(-(width + Gap), 0f);
        }

        private static void SweepLeftovers(Transform parent)
        {
            if (parent == null)
            {
                return;
            }
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == "IslandUI_Reach"
                    && (_rect == null || child != _rect))
                {
                    Object.Destroy(child.gameObject);
                }
            }
        }

        private static void Paint(bool hovered)
        {
            bool open = InventoryWindow.ReachOpen;
            if (_face != null)
            {
                // the harvested sprite already IS the wooden button; only brighten
                // or darken it, never replace its colour. Open is the LIT state,
                // because this is a toggle and a toggle has to say which way it is.
                float k = _pressed ? 0.65f : open ? 1.25f : 1f;
                if (hovered && !_pressed)
                {
                    k *= 1.1f;
                }
                _face.color = new Color(
                    Mathf.Clamp01(_faceColor.r * k),
                    Mathf.Clamp01(_faceColor.g * k),
                    Mathf.Clamp01(_faceColor.b * k),
                    _faceColor.a);
            }
            if (_caption == null)
            {
                return;
            }
            if (!hovered)
            {
                if (_caption.gameObject.activeSelf)
                {
                    _caption.gameObject.SetActive(false);
                }
                return;
            }
            _caption.text = open ? "hide chests" : "chests in reach";
            if (!_caption.gameObject.activeSelf)
            {
                _caption.gameObject.SetActive(true);
            }
        }

        private static Canvas CanvasOf(ManagersScript mn)
        {
            if (mn.uiMN.itemDescPanel != null)
            {
                return mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>();
            }
            if (Theme.BagButton != null)
            {
                return Theme.BagButton.GetComponentInParent<Canvas>();
            }
            return null;
        }

        private static bool ContainsPointer(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
        }

        private const float ButtonSize = 32f;
        private const float Gap = 6f;
    }
}
