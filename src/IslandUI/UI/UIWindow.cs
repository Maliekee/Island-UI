using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // A themed window shell in two styles:
    // - Parchment: the game's frame sprite + gold header (for primary panels)
    // - DarkCard: frameless dark plate with a subtle edge (the game's own tooltip
    //   surface language — for satellite/info cards)
    // Behavioral rules live with the caller (docking, closing with its parent).
    internal sealed class UIWindow
    {
        public GameObject Go;
        public RectTransform Rect;
        public TextMeshProUGUI Header;
        public RectTransform Content;

        private bool _darkCard;

        public static UIWindow Create(Transform parent, string name, float width, float height,
            bool darkCard = false)
        {
            var window = new UIWindow();
            window._darkCard = darkCard;
            window.Go = Owned.New(name);
            window.Go.transform.SetParent(parent, false);
            window.Rect = (RectTransform)window.Go.transform;

            Image background = window.Go.AddComponent<Image>();
            background.raycastTarget = true; // absorb clicks so the world doesn't get them
            if (darkCard)
            {
                background.color = Theme.PlateDark; // OPAQUE — reading surfaces always are
                Outline edge = window.Go.AddComponent<Outline>();
                edge.effectColor = new Color(0.45f, 0.34f, 0.22f, 1f);
                edge.effectDistance = new Vector2(1f, -1f);
            }
            else
            {
                background.sprite = Theme.Frame;
                background.type = Theme.FrameType;
                background.pixelsPerUnitMultiplier = Theme.FramePixelsPerUnit;
                background.color = Color.white;
            }

            window.Header = Theme.MakeText(window.Go.transform, "Header",
                Theme.FontHeader, darkCard ? Theme.AccentGold : Theme.Ink,
                TextAlignmentOptions.MidlineLeft);
            window.Header.enableWordWrapping = false;
            if (darkCard)
            {
                window.Header.gameObject.SetActive(false); // cards carry their own headers
            }
            else
            {
                Theme.Place(window.Header, Theme.Pad + 28f, 18f,
                    width - 2f * Theme.Pad - 28f, 18f);
            }

            var contentGo = Owned.New("Content");
            contentGo.transform.SetParent(window.Go.transform, false);
            window.Content = (RectTransform)contentGo.transform;
            window.Rect.sizeDelta = new Vector2(width, height);
            window.ApplyContentGeometry(width, height);
            return window;
        }

        public void SetSize(float width, float height)
        {
            Rect.sizeDelta = new Vector2(width, height);
            ApplyContentGeometry(width, height);
        }

        private void ApplyContentGeometry(float width, float height)
        {
            if (_darkCard)
            {
                Theme.Place(Content, Theme.Pad + 2f, Theme.Pad + 2f,
                    width - 2f * (Theme.Pad + 2f), height - 2f * (Theme.Pad + 2f));
            }
            else
            {
                // the parchment frame's rope/wood borders are thick — clear them
                Theme.Place(Content, Theme.Pad + 4f, 44f,
                    width - 2f * Theme.Pad - 8f, height - 44f - 14f);
                ((RectTransform)Header.transform).sizeDelta =
                    new Vector2(width - 2f * Theme.Pad - 28f, 18f);
            }
        }
    }
}
