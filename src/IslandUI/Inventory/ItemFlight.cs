using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // The flying item notice, as a kit.
    //
    // The pickup flight built this inside PickupToast for the one direction it needed:
    // world -> inventory. The quick-stack flight is the same notice run the other way
    // — inventory -> chest — and everything between "an item moved" and "the eye followed
    // it" is the same in both: the same prefab, the same item tile, the same
    // disarming of the prefab's own entrance animation, the same arc.
    //
    // What is NOT here is the lead-in. A pickup holds beside the player because the
    // toast is the only thing that ever says WHAT you picked up; a quick-stack
    // does not, because you pressed the key and the interesting fact is where the
    // items went. Each caller owns its own opening and then hands over to Travel.
    //
    // Everything in here was learned the hard way in the pickup flight — the Animator that
    // silently overwrites every transform write, the 48-unit content offset that
    // turns out to be a pose rather than geometry, the pivot-vs-camera confusion
    // in projecting one canvas's rect into another's. The comments explaining each
    // came with the code and are the reason it is a kit and not a rewrite.
    internal static class ItemFlight
    {
        // A toast built the way vanilla builds one (UIManager.ItemGetView,
        // UIManager.cs:2784): instantiate getItemIcon under getItemPanel, set the
        // art and the count, activate. Deliberately NOT a call to ItemGetView
        // itself — that is the pickup's entry point and PickupToast patches it, so
        // routing a quick-stack through it would fly the toast to the inventory,
        // which is the exact opposite of what this one has to say.
        //
        // `count` is signed and the format is vanilla's own: positive prints "+12",
        // negative "-12". Items leaving your bags are negative, which is the
        // game's vocabulary rather than ours.
        internal static GameObject Spawn(UIManager ui, ItemData item, int count)
        {
            if (ui == null || ui.getItemIcon == null || ui.getItemPanel == null || item == null)
            {
                return null;
            }
            GameObject toast = Owned.Instantiate(ui.getItemIcon, ui.getItemPanel.transform);
            toast.transform.localPosition = Vector3.zero;
            Transform art = toast.transform.Find("imageBG/itemImage");
            Image icon = art != null ? art.GetComponent<Image>() : null;
            if (icon != null)
            {
                icon.sprite = item.itemSprite;
            }
            // includeInactive, unlike vanilla's own call: the prefab root is not
            // active yet here, and a search that skips inactive objects would find
            // nothing and leave the count blank
            Text text = toast.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = count.ToString("+#;-#;");
            }
            toast.SetActive(true);
            return toast;
        }

        // The root canvas the flight happens on, and the camera to project with.
        // Null when there is nothing to fly across — the caller then does nothing
        // rather than inventing a surface.
        internal static RectTransform RootCanvas(UIManager ui, out Camera uiCam)
        {
            uiCam = null;
            Canvas canvas = ui != null && ui.getItemPanel != null
                ? ui.getItemPanel.GetComponentInParent<Canvas>() : null;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            if (root == null)
            {
                return null;
            }
            uiCam = root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            return (RectTransform)root.transform;
        }

        // Take the toast over: out of getItemPanel (whose layout group would
        // overwrite every frame we set) onto the root canvas, centre-anchored, with
        // the prefab's own animation disarmed and its content re-centred.
        //
        // The RESOLVED size is captured before the anchors are touched: if the
        // prefab is anchored stretched, sizeDelta is a pair of offsets rather than
        // a size, and re-anchoring to centre would collapse the toast.
        internal static CanvasGroup Detach(GameObject toast, RectTransform canvasRect,
            out Vector2 size)
        {
            var rect = (RectTransform)toast.transform;
            size = rect.rect.size;
            toast.transform.SetParent(canvasRect, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            // order matters: the animator has already posed the contents, so stop
            // it first and let Recentre neutralise the pose it left behind
            StopVanillaAnimation(toast);
            Recentre(toast);
            var group = toast.GetComponent<CanvasGroup>() ?? toast.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            return group;
        }

        // The trip itself, in canvas-local coordinates: ease out along a lifted
        // line, shrinking into the destination, fading only at the very end so the
        // count stays readable for most of the way.
        //
        // UNSCALED time: a pickup can land while something has paused the game, and
        // a frozen toast halfway across would look broken.
        internal static IEnumerator Travel(RectTransform rect, CanvasGroup group,
            Vector2 from, Vector2 to, float seconds)
        {
            // a lift proportional to the trip, capped: a long flight across the
            // screen wants an arc, a short hop next to the bar does not
            float arc = Mathf.Min(90f, Vector2.Distance(from, to) * 0.25f);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float e = 1f - (1f - t) * (1f - t);          // ease out
                Vector2 p = Vector2.Lerp(from, to, e);
                p.y += Mathf.Sin(e * Mathf.PI) * arc;
                rect.anchoredPosition = p;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
                if (group != null)
                {
                    group.alpha = t < 0.75f ? 1f : Mathf.InverseLerp(1f, 0.75f, t);
                }
                yield return null;
                if (rect == null)
                {
                    yield break;   // something destroyed it mid-flight
                }
            }
        }

        // A rect's screen point, resolved through the RECT'S OWN canvas.
        //
        // Two bugs lived in the one line this replaces. It projected with the
        // TOAST's camera, which is only right while both live under the same canvas
        // — the bottom-right button bar does not, so a Screen-Space-Camera
        // projection was being applied to an Overlay rect (or the reverse) and the
        // answer was meaningless. And it used `position`, which is the PIVOT: our
        // plates pivot bottom-right, so even a correct projection would have aimed
        // at a corner rather than at the thing.
        //
        // Screen space is the common currency between canvases, so converting each
        // rect through its own is the only version that cannot care where the
        // target lives.
        internal static Vector2 ScreenPointOf(RectTransform target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas != null
                && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);   // pivot-independent: the visual centre
            Vector3 centre = (corners[0] + corners[2]) * 0.5f;
            return RectTransformUtility.WorldToScreenPoint(cam, centre);
        }

        internal static bool ToLocal(RectTransform canvasRect, Camera uiCam,
            Vector2 screen, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, uiCam, out local);
        }

        // THE ACTUAL CAUSE of three failed attempts at the pickup animation: there is an
        // ANIMATOR on imageBG.
        //
        //   'imageBG' ... [CanvasRenderer,Image,Animator]
        //
        // The prefab animates its own entrance — it slides in from the right, which
        // is precisely what was reported at the start and then talked out of. An
        // Animator writes transforms in LateUpdate, AFTER a coroutine's Update-time
        // writes, so it silently won every argument: re-centring was undone, the
        // pivot flip was undone, position was undone. Each fix "did nothing"
        // because each fix was being overwritten within the same frame.
        //
        // It is also where the 47.7-unit content offset came from. That was not
        // authored geometry, it was a pose — the entrance animation caught partway.
        //
        // Once we take the toast over, our animation is the only one. Disable the
        // Animator, then force every graphic opaque: a curve stopped mid-fade
        // leaves an alpha behind, and a CanvasGroup cannot rescue a graphic whose
        // own alpha the animator parked at zero.
        internal static void StopVanillaAnimation(GameObject toast)
        {
            foreach (Animator animator in toast.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
            foreach (LayoutGroup group in toast.GetComponentsInChildren<LayoutGroup>(true))
            {
                group.enabled = false;
            }
            foreach (ContentSizeFitter fitter in toast.GetComponentsInChildren<ContentSizeFitter>(true))
            {
                fitter.enabled = false;
            }
            foreach (Graphic graphic in toast.GetComponentsInChildren<Graphic>(true))
            {
                Color c = graphic.color;
                c.a = 1f;
                graphic.color = c;
            }
        }

        // The visible square sits well to the right of the root's own origin:
        //
        //   root 'imageRoot_00(Clone)'  size=(30,30)
        //   'imageBG'   offsetFromRootOrigin=(47.7, 0.0)
        //   'itemImage' offsetFromRootOrigin=(47.7, 0.0)
        //   'Text'      offsetFromRootOrigin=(42.3, -7.5)
        //
        // Everything derived about the animation assumed content near the origin,
        // and a scale multiplies that offset: the sprite swings by 0.3 x 47.7 =
        // ~14 units during a pop, which is half its own width and swamps every
        // effect being reasoned about.
        //
        // Re-centre once, and the rect means what it appears to mean: position
        // places the sprite, scale grows about the sprite, pivot decides which way.
        // Children move together, so their relative layout (count under the icon)
        // is untouched.
        internal static void Recentre(GameObject toast)
        {
            var root = (RectTransform)toast.transform;
            Transform bg = root.Find("imageBG");
            if (bg == null)
            {
                return;
            }
            Vector3 offset = root.InverseTransformPoint(bg.position);
            for (int i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).localPosition -= offset;
            }
        }

        internal static void Style(GameObject toast)
        {
            try
            {
                // the black square is the prefab's own imageBG, the parent of the
                // art — the same relationship a cell's frame has to its icon, so
                // it is the right thing to hand the tile treatment
                Transform bg = toast != null ? toast.transform.Find("imageBG") : null;
                if (bg != null)
                {
                    InventoryWindow.ApplyItemTile(bg.GetComponent<Image>());
                }
            }
            catch (System.Exception e)
            {
                // a toast is a garnish; never let one break a pickup or a stack
                Plugin.Log.LogWarning($"ItemFlight: could not style — {e.Message}");
            }
        }

    }
}
