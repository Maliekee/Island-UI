using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI
{
    // THE MAP (M) PANS UNDER THE POINTER. The game pans it on the right button only,
    // by the mouse's axis times ten (UIManager.MapMove), so the map runs away from the
    // cursor at a speed that depends on the mouse. Here either button grabs it and the
    // spot under the cursor stays under the cursor. A press on a control - a pin, the
    // brightness slider, a button - is that control's, not a pan, so a pin's click
    // still walks to its person. The game's clamp to the map's edges is kept.
    [HarmonyPatch]
    internal static class MapDrag
    {
        private static bool _grabbed;
        private static Vector2 _last;

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.MapMove))]
        [HarmonyPrefix]
        private static bool MapMove_Prefix(UIManager __instance, RectTransform ___mapRect)
        {
            if (___mapRect == null || __instance.mapPivot == null)
            {
                return true;
            }
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                _grabbed = !OnControl(mouse);
                _last = mouse;
            }
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                _grabbed = false;
                return false;
            }
            if (!_grabbed || mouse == _last)
            {
                return false;
            }
            var parent = ___mapRect.parent as RectTransform;
            Canvas canvas = ___mapRect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            if (parent != null
                && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, _last, cam, out Vector3 from)
                && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, mouse, cam, out Vector3 to))
            {
                ___mapRect.position += to - from;
                float room = 250f * (__instance.mapPivot.transform.localScale.x - 0.2f);
                ___mapRect.localPosition = new Vector2(Mathf.Clamp(___mapRect.localPosition.x, -room, room),
                    Mathf.Clamp(___mapRect.localPosition.y, -room, room));
            }
            _last = mouse;
            return false;
        }

        // is the press on something that takes clicks of its own
        private static bool OnControl(Vector2 at)
        {
            if (EventSystem.current == null)
            {
                return false;
            }
            var data = new PointerEventData(EventSystem.current) { position = at };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() != null;
        }
    }
}
