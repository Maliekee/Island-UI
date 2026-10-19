using System.Collections;
using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // Quick-stack says where it put things — a toast per item per chest,
    // leaving the inventory cell it came from and arriving at the box in the
    // world. The pickup notice run the other way round, on the same kit.
    //
    // Why this direction is worth the same effort as the pickup's: a quick-stack
    // is the one move in the game you never watch happen. A pickup has an object
    // on the ground and a character walking to it; a sweep is a keypress, after
    // which a dozen stacks are simply elsewhere. The chests it chose are ITS
    // choice, made out of a radius scan and a "does this box already hold one"
    // rule that nothing on screen explains — so "twelve fibre went into THAT
    // one" is genuinely new information, not decoration.
    //
    // The two ends are resolved at different times, on purpose:
    //   source — a UI rect, captured by QuickStack BEFORE the move, because the
    //            question is "which cell holds this key" and a moment later no
    //            cell does. Screen points do not drift; the cell is not asked again.
    //   chest  — a world object, resolved at LAUNCH, because the camera can turn
    //            during the stagger and a point captured early would be stale.
    internal static class QuickStackFlight
    {
        // Where the item is leaving FROM, in screen space. Deliberately the same
        // ladder the pickup lands on (`LandingSpot`, then the bag button): an item
        // should leave from exactly where the notice that brought it in put it.
        //
        // False when we cannot aim honestly — the flight is then simply not drawn,
        // rather than started from a guessed point.
        internal static bool SourceOf(ManagersScript mn, string itemKey, out Vector2 screen)
        {
            screen = Vector2.zero;
            if (!Settings.QuickStackFlight.Value || mn == null)
            {
                return false;
            }
            // same reason as the pickup's: a save load destroys the bag button with
            // the rest of the scene, and nothing re-harvests until a window opens
            Theme.Ensure(mn, mn.inventory);
            RectTransform from = InventoryWindow.LandingSpot(mn, itemKey)
                ?? InventoryWindow.BagSpot();
            if (from == null || !from.gameObject.activeInHierarchy)
            {
                return false;
            }
            screen = ItemFlight.ScreenPointOf(from);
            return true;
        }

        internal static void Show(ManagersScript mn, List<QuickStack.Deposit> deposits)
        {
            if (deposits == null || deposits.Count == 0 || !Settings.QuickStackFlight.Value)
            {
                return;
            }
            UIManager ui = mn != null ? mn.uiMN : null;
            // StartCoroutine throws on an inactive behaviour, and a sweep can land
            // in the same frame a scene is coming down
            if (ui == null || !ui.isActiveAndEnabled || Camera.main == null)
            {
                return;
            }
            RectTransform canvasRect = ItemFlight.RootCanvas(ui, out Camera uiCam);
            if (canvasRect == null)
            {
                return;
            }
            ui.StartCoroutine(Stream(ui, deposits, canvasRect, uiCam));
        }

        // Launches spaced out, so a sweep reads as a STREAM of things leaving
        // rather than one simultaneous blob — but the burst as a whole is bounded,
        // so emptying twenty stacks does not take twenty times as long to say so.
        private static IEnumerator Stream(UIManager ui, List<QuickStack.Deposit> deposits,
            RectTransform canvasRect, Camera uiCam)
        {
            float seconds = FlightSeconds;
            float gap = Mathf.Min(Stagger, MaxBurstSeconds / deposits.Count);
            for (int i = 0; i < deposits.Count; i++)
            {
                if (ui == null || !ui.isActiveAndEnabled)
                {
                    yield break;
                }
                ui.StartCoroutine(One(ui, deposits[i], canvasRect, uiCam, seconds));
                float waited = 0f;
                while (waited < gap)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static IEnumerator One(UIManager ui, QuickStack.Deposit deposit,
            RectTransform canvasRect, Camera uiCam, float seconds)
        {
            // THE CHEST'S END IS ITS CELL WHEN THE GRID IS OPEN, the
            // chest in the world otherwise - as the bag's end is its cell when the
            // bags are open and the bag button otherwise
            Vector2 storageScreen;
            RectTransform cell = InventoryWindow.CellFor(deposit.Storage, deposit.Item);
            if (deposit.HasStorageScreen)
            {
                storageScreen = deposit.StorageScreen;   // captured before the stack left
            }
            else if (cell != null)
            {
                storageScreen = ItemFlight.ScreenPointOf(cell);
            }
            else if (!Destination(deposit.Storage, out storageScreen))
            {
                yield break;   // the chest turned out not to be somewhere we can point at
            }
            // NEGATIVE, in vanilla's own format ("+#;-#;"): these items left your
            // bags, and the pickup's "+12" is the same sentence with the other sign.
            // A WITHDRAWAL (a chest's All out) flies the other way and counts up:
            // the items are arriving, and FromScreen is then where they land.
            Vector2 fromScreen = deposit.Reverse ? storageScreen : deposit.FromScreen;
            Vector2 toScreen = deposit.Reverse ? deposit.FromScreen : storageScreen;
            GameObject toast = ItemFlight.Spawn(ui, deposit.Item, deposit.Reverse ? deposit.Count : -deposit.Count);
            if (toast == null)
            {
                yield break;
            }
            // try/FINALLY, never try/catch — an iterator allows one and not the
            // other. THE TOAST IS OURS: unlike the pickup's, nothing in vanilla
            // knows this object exists, so nothing else will ever destroy it. Every
            // exit — a bad projection, an interrupted coroutine, a normal finish —
            // has to go through here or a framed item icon is left on the canvas.
            try
            {
                ItemFlight.Style(toast);
                var rect = (RectTransform)toast.transform;
                CanvasGroup group = ItemFlight.Detach(toast, canvasRect, out Vector2 _);

                if (!ItemFlight.ToLocal(canvasRect, uiCam, fromScreen, out Vector2 from)
                    || !ItemFlight.ToLocal(canvasRect, uiCam, toScreen, out Vector2 to))
                {
                    yield break;
                }
                rect.anchoredPosition = from;             // before the first frame is drawn
                rect.localScale = Vector3.one * PopFrom;

                // A short POP, where the pickup holds. The pickup toast waits to be
                // read because it is the only thing that ever says what you got; a
                // quick-stack was asked for and the item is already known, so the
                // fact worth showing is the trip. The growth is only there to give
                // the eye something to catch before the thing moves.
                float popped = 0f;
                while (popped < PopSeconds)
                {
                    popped += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(popped / PopSeconds);
                    rect.localScale = Vector3.one
                        * Mathf.Lerp(PopFrom, 1f, 1f - (1f - k) * (1f - k));
                    // re-asserted every frame, not set once: anything else that
                    // writes this rect gets overwritten rather than fought
                    rect.anchoredPosition = from;
                    yield return null;
                    if (toast == null)
                    {
                        yield break;
                    }
                }

                IEnumerator leg = ItemFlight.Travel(rect, group, from, to, seconds);
                while (leg.MoveNext())
                {
                    yield return leg.Current;
                }
            }
            finally
            {
                if (toast != null)
                {
                    Object.Destroy(toast);
                }
            }
        }

        // The chest's screen point — its VOLUME's centre, not its transform origin.
        // A prop's origin sits on the ground, so aiming at it lands the toast at
        // the box's feet rather than on the box.
        //
        // The COLLIDER is the right body to ask, and not by accident: quick-stack
        // finds its storages with `Physics.OverlapSphere`, and reads the
        // `InventorySlot` off the collider it hit — so the collider is on this very
        // GameObject, it is guaranteed to exist, and it is literally the shape the
        // sweep considered "the chest". Bounds are world-space, so a rotated or
        // scaled prop needs nothing extra, and a big storage and a small one each
        // get their own middle without a dial.
        private static bool Destination(InventorySlot storage, out Vector2 screen)
        {
            screen = Vector2.zero;
            Camera world = Camera.main;
            if (world == null || storage == null)
            {
                return false;
            }
            Vector3 point = storage.transform.position;
            Collider body = storage.GetComponent<Collider>();
            if (body != null)
            {
                point = body.bounds.center;
            }
            Vector3 projected = world.WorldToScreenPoint(point);
            if (projected.z < 0f)
            {
                return false;   // behind the camera: the point is mirrored, not offscreen
            }
            screen = projected;
            return true;
        }

        private const float PopFrom = 0.7f;
        private const float PopSeconds = 0.12f;
        private const float MaxBurstSeconds = 0.6f;
        private const float FlightSeconds = 0.5f;
        private const float Stagger = 0.06f;    // between launches, so a sweep reads as a stream
    }
}
