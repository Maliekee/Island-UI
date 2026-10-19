using System.Collections;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // The pickup toast — the little panel that appears at the right edge saying
    // "+12" with the item's art — wears the ITEM TILE like everything else that
    // shows an item (InventoryWindow.ApplyItemTile). It shipped as a hard black
    // square, which was fine while it was the only thing on screen shaped like
    // that; once a bag cell and the cursor both carry a framed, shaded tile, a
    // bare black square is the odd one out, and the toast is the very moment you
    // most want to recognise the shape.
    //
    // The toast MECHANICS — building one, taking it over from the prefab's own
    // animation, projecting between canvases, the trip itself — moved to
    // `ItemFlight` when quick-stack's flights gave them a second caller (quick-stack, the same
    // notice run inventory -> chest). What stays here is what only a PICKUP does:
    // wrapping vanilla's iterator, and the hold beside the player.
    //
    // Vanilla builds it in UIManager.ItemGetView (UIManager.cs:2784): Instantiate
    // the getItemIcon prefab under getItemPanel, set imageBG/itemImage's sprite,
    // set a legacy Text to the count, wait 5s, Destroy. Fresh instance per pickup,
    // no pooling, no code anywhere else touching either field.
    //
    // ItemGetView is an ITERATOR, so a prefix would run at construction — before
    // the object it builds exists. Wrapping `ref IEnumerator __result` instead
    // lets us step it exactly once (which runs everything up to its first yield,
    // so the Instantiate and the SetActive have both happened) and restyle before
    // the frame is ever drawn. No flash of the old square, and no per-frame poll
    // of a panel that is empty almost always. Same shape as FavouriteRules'
    // RestoreAfter, which wraps the harvest coroutines.
    [HarmonyPatch]
    internal static class PickupToast
    {
        // tmpItem by NAME, which is how Harmony binds a prefix/postfix parameter to
        // the original's — it is what tells the flight which slot to aim at, and
        // ItemGetView is the only place the key and the toast are both in hand
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.ItemGetView))]
        [HarmonyPostfix]
        private static void ItemGetView_Postfix(UIManager __instance, ItemData tmpItem,
            ref IEnumerator __result)
        {
            if (__result != null)
            {
                __result = Styled(__instance, tmpItem != null ? tmpItem.name : null, __result);
            }
        }

        private static IEnumerator Styled(UIManager ui, string itemKey, IEnumerator inner)
        {
            if (!inner.MoveNext())
            {
                yield break;
            }
            GameObject toast = Newest(ui);
            ItemFlight.Style(toast);
            // Fly it from the player to wherever the item landed, INSTEAD of
            // vanilla's five-second dwell at the screen edge. A notice that tells
            // you the direction is finished the moment it arrives; holding it
            // there afterwards would just cover the bag you are pointing at.
            IEnumerator flight = Flight(ui, toast, itemKey);
            if (flight != null)
            {
                // THE DRAIN MUST BE UNCONDITIONAL. It was written as the next
                // statement after the flight, which quietly made vanilla's Destroy
                // conditional on our animation finishing normally — so a throw
                // anywhere in Flight, or this coroutine being stopped, left the
                // toast parked on the root canvas forever. A stray toast wears the
                // item tile, so it reads as a framed item icon sitting wherever it
                // happened to land: over a plate, over the loadout's first slot.
                //
                // try/FINALLY, never try/catch — an iterator allows one and not the
                // other — and a finally may not `yield`, but the drain does not: it
                // steps the inner iterator without yielding, which is exactly what
                // skips the 5s dwell and runs the Destroy. Dispose runs it too, so
                // even a stopped coroutine cleans up after itself.
                try
                {
                    while (flight.MoveNext())
                    {
                        yield return flight.Current;
                    }
                }
                finally
                {
                    while (inner.MoveNext())
                    {
                    }
                    // the object's lifetime is vanilla's to end — but if it did not
                    // end it, ours to clean up rather than leave on screen
                    if (toast != null)
                    {
                        Plugin.Log.LogWarning("pickup toast: a toast outlived vanilla's Destroy "
                            + "— removing it (this should not happen; the flight was "
                            + "interrupted or the inner iterator did not reach its end)");
                        Object.Destroy(toast);
                    }
                }
                yield break;
            }
            yield return inner.Current;
            while (inner.MoveNext())
            {
                yield return inner.Current;
            }
        }

        // Null when we cannot honestly aim: no camera, no player, no canvas, or
        // nothing on screen to fly to. The caller then leaves vanilla's toast
        // exactly as it was rather than inventing a destination.
        private static IEnumerator Flight(UIManager ui, GameObject toast, string itemKey)
        {
            if (toast == null || !Settings.PickupFlight.Value)
            {
                return null;
            }
            Camera world = Camera.main;
            ManagersScript mn = ui != null ? ui.mn : null;
            PlayerMove player = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
            if (world == null || player == null || ui.getItemPanel == null)
            {
                return null;
            }
            RectTransform canvasRect = ItemFlight.RootCanvas(ui, out Camera uiCam);
            if (canvasRect == null)
            {
                return null;
            }
            // Harvest before reading Theme, not just at window-build time. A save
            // load destroys the bag button with the rest of the scene, and nothing
            // re-harvests until some window of ours is opened — so the first
            // pickups after a load aimed at a corpse, Flight returned null, and the
            // toast sat at the screen edge in its vanilla place. Only with the bag
            // CLOSED, because an open one supplies a live cell and never reaches
            // this fallback. Cheap: Ensure early-outs once the theme is current.
            Theme.Ensure(mn, mn.inventory);
            RectTransform target = InventoryWindow.LandingSpot(mn, itemKey)
                ?? InventoryWindow.BagSpot();
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return null;
            }
            // The player's transform origin is at their FEET, which is where a
            // dropped thing would have been lying — so the notice comes off the
            // ground rather than out of their chest. It started at UIManager's own
            // head offset (1.5, UIManager.cs:2266) because that is where vanilla
            // anchors nameplates, but a nameplate labels a person and this labels
            // something they just picked up off the floor.
            Vector3 from3 = world.WorldToScreenPoint(player.transform.position
                + Vector3.up * OriginHeight);
            if (from3.z < 0f)
            {
                return null;   // player behind the camera: the point is mirrored, not offscreen
            }
            Vector2 to = ItemFlight.ScreenPointOf(target);
            return Fly(toast, canvasRect, uiCam, from3, to);
        }

        private static IEnumerator Fly(GameObject toast, RectTransform canvasRect,
            Camera uiCam, Vector2 fromScreen, Vector2 toScreen)
        {
            var rect = (RectTransform)toast.transform;
            // Vanilla still owns the Destroy; only the parent changes.
            CanvasGroup group = ItemFlight.Detach(toast, canvasRect, out Vector2 size);

            if (!ItemFlight.ToLocal(canvasRect, uiCam, fromScreen, out Vector2 from)
                || !ItemFlight.ToLocal(canvasRect, uiCam, toScreen, out Vector2 to))
            {
                yield break;
            }
            // Beside the player, not on top of them. Fine to anchor on the
            // character for a thing that leaves immediately, wrong for a thing that
            // now sits there. Measured in TOAST WIDTHS so it holds at any canvas
            // scale rather than being a pixel guess.
            float offsetX = HoldOffsetX;
            from.x += size.x * offsetX;
            // which edge faces the player: +1 when the toast sits to their right
            float toward = offsetX < 0f ? -1f : 1f;
            // A burst — a harvest yielding three things — would otherwise stack
            // every toast on the same point and hold them there, reading as one.
            // Lanes only matter because of the hold; before it they separated on
            // their own within a few frames.
            int lane = _inFlight % Lanes;
            from.y += lane * (size.y + 4f);

            float seconds = Mathf.Clamp(Settings.PickupFlightSeconds.Value, 0.1f, 3f);
            float hold = HoldSeconds;
            rect.anchoredPosition = from;   // before the first frame is drawn
            _inFlight++;
            try
            {
                // HOLD FIRST. The flight was legible as a direction and useless as
                // a notice: by the time the eye found the thing it had already
                // left. So it appears, is read, and only then goes.
                //
                // It GROWS AWAY FROM THE PLAYER, and NOTHING MOVES while it does.
                // A centre-pivot scale-up expands both ways at once, and the edge
                // growing back toward the character is what read as the toast
                // sliding in from the far side — the eye takes the nearer edge as
                // the anchor and infers motion from the other one.
                //
                // Two wrong fixes came first, and both share a mistake worth
                // naming: they animated POSITION to express something that is
                // purely about where the scale is anchored. The first drifted the
                // whole toast out from the player (visibly wrong — out to the
                // right, then the flight took it straight back left). The second
                // compensated position against the scale, which pins the edge
                // correctly but still slides the centre by half the growth.
                //
                // Scaling about the pinned EDGE is the version with no motion in it
                // at all: Unity scales a rect about its pivot, so putting the pivot
                // on the player-facing edge makes that edge unable to move, and
                // anchoredPosition stays constant for the whole hold. The pivot
                // goes back to centre before the flight, which is a no-op visually
                // because at full scale both describe the same rectangle.
                // Pin the edge that faces the player, so growth can only go outward.
                // This is only trustworthy because Recentre ran: on the raw prefab
                // the content sat 48 units off a 30-unit root, and a +-15 unit pivot
                // shift cannot outvote that.
                float pivotX = toward > 0f ? 0f : 1f;
                rect.pivot = new Vector2(pivotX, 0.5f);
                rect.anchoredPosition = new Vector2(
                    from.x + (pivotX - 0.5f) * size.x, from.y);

                Vector2 holdAt = rect.anchoredPosition;
                float held = 0f;
                while (held < hold)
                {
                    held += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(held / Mathf.Max(0.0001f, hold * 0.35f));
                    rect.localScale = Vector3.one
                        * Mathf.Lerp(0.7f, 1f, 1f - (1f - k) * (1f - k));
                    // re-asserted every frame, not set once: anything else that
                    // writes this rect gets overwritten rather than fought
                    rect.anchoredPosition = holdAt;
                    yield return null;
                    if (toast == null)
                    {
                        yield break;
                    }
                }
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = from;

                IEnumerator leg = ItemFlight.Travel(rect, group, from, to, seconds);
                while (leg.MoveNext())
                {
                    yield return leg.Current;
                }
            }
            finally
            {
                _inFlight--;   // try/FINALLY, never try/catch: an iterator allows one and not the other
            }
        }

        // How many toasts are mid-hold, so a burst can be fanned into lanes rather
        // than piled on one point. Wraps at Lanes so a long burst repeats rather
        // than marching off the top of the screen.
        private static int _inFlight;
        private const int Lanes = 4;
        // the notice comes from waist height (world units above the feet), waits
        // one toast width to the player's right, for this long, then flies
        private const float OriginHeight = 0.85f;
        private const float HoldOffsetX = 1f;
        private const float HoldSeconds = 0.45f;

        // A scene unload can destroy the ItemManager running a flight, and a
        // coroutine that never finishes never runs its finally — so the counter
        // would drift upward across save loads and every toast would start out
        // offset. Scene-reset discipline (§6).
        internal static void ResetForSceneLoad()
        {
            _inFlight = 0;
        }

        // The coroutine keeps its instance in a local we cannot reach, but it was
        // just parented to getItemPanel, so the last child IS it.
        private static GameObject Newest(UIManager ui)
        {
            Transform panel = ui != null && ui.getItemPanel != null
                ? ui.getItemPanel.transform : null;
            return panel != null && panel.childCount > 0
                ? panel.GetChild(panel.childCount - 1).gameObject
                : null;
        }
    }
}
