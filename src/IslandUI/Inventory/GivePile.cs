using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE ITEMS YOU ARE ABOUT TO GIVE, IN YOUR HAND. While a drop would give several
    // (Shift, ItemUse.UsesAsked) the held icon becomes a small stack of three with the
    // count on it, and goes back when it no longer would. On the release the items
    // flow from that stack to whoever takes them, one after the other. It replaced a
    // fan on a line across the flight, which only showed up once the click had
    // already been made, and then a block of every item, which was too much.
    //
    // Like CursorBadge, nothing hides it: Want is asked every frame it should be up,
    // and not being asked folds it. The tiles are the flight's own (ItemFlight), so
    // one held is one flown, with no change of look at the moment it leaves.
    internal static class GivePile
    {
        private const float OpenSeconds = 0.16f;
        private const float CloseSeconds = 0.1f;
        private const float TileScale = 0.75f;
        private const int StackShown = 3;         // tiles drawn, however many are given
        private const float StackStep = 3.5f;     // each on the one below, in canvas units
        private const float FlightSeconds = 0.45f;
        private const float FlowEvery = 0.05f;    // between one tile leaving and the next

        private sealed class Tile
        {
            internal GameObject Go;
            internal RectTransform Rect;
            internal CanvasGroup Group;
            internal Image Icon;
            internal Text Count;
        }

        private static readonly List<Tile> Tiles = new List<Tile>();
        private static bool _wanted;
        private static ItemData _wantedItem;
        private static int _wantedCount;
        private static ItemData _item;
        private static int _count;
        private static float _open;
        private static CanvasGroup _hand;

        // Every frame a drop under the pointer would give `count` of `item`.
        internal static void Want(ItemData item, int count)
        {
            _wanted = item != null && count > 1;
            _wantedItem = item;
            _wantedCount = count;
        }

        // Every frame, whatever is on screen: see CommitBadge.
        internal static void Commit(ManagersScript mn)
        {
            bool wanted = _wanted;
            _wanted = false;
            Tiles.RemoveAll(t => t.Go == null);   // a scene load took them
            _open = Mathf.MoveTowards(_open, wanted ? 1f : 0f,
                Time.unscaledDeltaTime / (wanted ? OpenSeconds : CloseSeconds));
            if (wanted)
            {
                _item = _wantedItem;
                _count = _wantedCount;
            }
            PaintHand(mn, 1f - _open);
            UIManager ui = mn != null ? mn.uiMN : null;
            RectTransform canvasRect = ItemFlight.RootCanvas(ui, out Camera uiCam);
            if (_open <= 0f || canvasRect == null || _item == null
                || !ItemFlight.ToLocal(canvasRect, uiCam, Input.mousePosition, out Vector2 hand))
            {
                ShowOnly(0);
                return;
            }
            if (wanted)
            {
                Fill(ui, canvasRect, Mathf.Min(_count, StackShown));
            }
            int shown = Mathf.Min(Mathf.Min(_count, StackShown), Tiles.Count);
            ShowOnly(shown);
            float eased = 1f - (1f - _open) * (1f - _open);
            for (int i = 0; i < shown; i++)
            {
                Tile tile = Tiles[i];
                tile.Rect.anchoredPosition = Vector2.Lerp(hand, Home(hand, i, shown), eased);
                tile.Rect.localScale = Vector3.one * TileScale;
                tile.Group.alpha = eased;
                // the count on the top one, the last drawn
                if (tile.Count != null)
                {
                    string words = wanted && i == shown - 1 ? "×" + _count : string.Empty;
                    if (tile.Count.text != words)
                    {
                        tile.Count.text = words;
                    }
                }
            }
        }

        // The icon in the hand is what became the tiles, so it gives way to them.
        private static void PaintHand(ManagersScript mn, float alpha)
        {
            if (_hand == null)
            {
                ItemSlot hover = mn != null && mn.inventory != null ? mn.inventory.hoverSlot : null;
                if (alpha >= 1f || hover == null)
                {
                    return;
                }
                _hand = hover.GetComponent<CanvasGroup>() ?? hover.gameObject.AddComponent<CanvasGroup>();
            }
            if (_hand.alpha != alpha)
            {
                _hand.alpha = alpha;
            }
        }

        private static void ShowOnly(int shown)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i].Go.activeSelf != (i < shown))
                {
                    Tiles[i].Go.SetActive(i < shown);
                }
            }
        }

        private static void Fill(UIManager ui, RectTransform canvasRect, int count)
        {
            while (Tiles.Count < count)
            {
                Tile tile = NewTile(ui, canvasRect, _item, 0);
                if (tile == null)
                {
                    return;
                }
                Tiles.Add(tile);
            }
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i].Icon != null && Tiles[i].Icon.sprite != _item.itemSprite)
                {
                    Tiles[i].Icon.sprite = _item.itemSprite;
                }
            }
        }

        private static Tile NewTile(UIManager ui, RectTransform canvasRect, ItemData item, int count)
        {
            GameObject toast = ItemFlight.Spawn(ui, item, count);
            if (toast == null)
            {
                return null;
            }
            ItemFlight.Style(toast);
            CanvasGroup group = ItemFlight.Detach(toast, canvasRect, out _);
            Transform art = toast.transform.Find("imageBG/itemImage");
            return new Tile
            {
                Go = toast, Rect = (RectTransform)toast.transform, Group = group,
                Icon = art != null ? art.GetComponent<Image>() : null,
                Count = toast.GetComponentInChildren<Text>(true),
            };
        }

        // Where a tile rests: the stack sits where the held icon was, each tile a
        // little up and to the right of the one below.
        private static Vector2 Home(Vector2 hand, int index, int shown)
        {
            float along = (index - (shown - 1) * 0.5f) * StackStep;
            return hand + new Vector2(along, along);
        }

        // THE RELEASE: `spent` tiles flow to `toScreen`. Those of the stack leave from
        // where they are, and the rest of the count start where it stands (where it
        // would have stood, for a walk-and-give, whose stack folded on the way). One is
        // the plain item with its "-1".
        // What was not spent stays in the pile and folds back into the hand.
        internal static void Launch(UIManager ui, RectTransform canvasRect, Camera uiCam, ItemData item, int spent,
            Vector2 toScreen)
        {
            _wanted = false;   // asked this frame, before the release: not a new pile
            if (!ItemFlight.ToLocal(canvasRect, uiCam, toScreen, out Vector2 to)
                || !ItemFlight.ToLocal(canvasRect, uiCam, Input.mousePosition, out Vector2 hand))
            {
                return;
            }
            var leaving = new List<Tile>();
            bool held = _open > 0f && ReferenceEquals(_item, item);
            while (held && leaving.Count < spent && Tiles.Count > 0 && Tiles[0].Go.activeSelf)
            {
                if (Tiles[0].Count != null)
                {
                    Tiles[0].Count.text = string.Empty;
                }
                leaving.Add(Tiles[0]);
                Tiles.RemoveAt(0);
            }
            _count = Mathf.Min(_count, Tiles.Count);
            int stack = Mathf.Min(spent, StackShown);
            for (int i = leaving.Count; i < spent; i++)
            {
                Tile fresh = NewTile(ui, canvasRect, item, spent > 1 ? 0 : spent);
                if (fresh == null)
                {
                    break;
                }
                fresh.Rect.anchoredPosition = spent > 1 ? Home(hand, i % stack, stack) : hand;
                leaving.Add(fresh);
            }
            for (int i = 0; i < leaving.Count; i++)
            {
                leaving[i].Group.alpha = 1f;
                ui.StartCoroutine(Flow(leaving[i], i * FlowEvery, to, spent > 1 ? TileScale : 1f));
            }
        }

        private static IEnumerator Flow(Tile tile, float wait, Vector2 to, float scale)
        {
            // try/finally: the tile is ours, nothing in vanilla will destroy it
            try
            {
                tile.Rect.localScale = Vector3.one * scale;
                float until = Time.unscaledTime + wait;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                    if (tile.Rect == null)
                    {
                        yield break;
                    }
                }
                IEnumerator leg = ItemFlight.Travel(tile.Rect, tile.Group, tile.Rect.anchoredPosition, to, FlightSeconds, scale);
                while (leg.MoveNext())
                {
                    yield return leg.Current;
                }
            }
            finally
            {
                if (tile.Go != null)
                {
                    Object.Destroy(tile.Go);
                }
            }
        }
    }
}
