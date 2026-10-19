using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // A change to any element of a Unity canvas re-batches the WHOLE canvas — and
    // this game keeps its entire UI on one giant MainCanvas, so opening or updating
    // a 500-button panel regenerated geometry for everything (measured: 40-80ms one
    // frame after every craft-menu open). Nested Canvas components create isolation
    // islands: changes inside stay inside, changes outside don't touch the panel.
    // Applied to the panels this mod churns: craft menu, chest UI, apparel closet —
    // and to every window this mod builds.
    internal static class CanvasIsolation
    {
        internal static void Apply(ManagersScript mn)
        {
            if (mn == null || !Settings.IsolatePanelCanvases.Value)
            {
                return;
            }
            Isolate(mn.uiMN != null ? mn.uiMN.craftPanel : null);
            Isolate(mn.inventory != null ? mn.inventory.subInventory : null);
            Isolate(mn.uiMN != null ? mn.uiMN.apparelPanel : null);
        }

        internal static void Isolate(GameObject panel)
        {
            if (panel == null || !Settings.IsolatePanelCanvases.Value
                || panel.GetComponent<Canvas>() != null)
            {
                return;
            }
            panel.AddComponent<Canvas>();
            panel.AddComponent<GraphicRaycaster>(); // clicks route per-canvas
            Plugin.Log.LogDebug($"Sub-canvas isolation applied to '{panel.name}'");
        }
    }
}
