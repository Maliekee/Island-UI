using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IslandUI
{
    // Feature: larger crafting panel. The game's grid is a GridLayoutGroup with
    // FixedColumnCount=4 and ~4x6 visible cells; the panel is drag-resizable by design
    // (UIManager.CraftPanelSizing), so enlarging it is sanctioned. We bump the column
    // constraint and grow the panel by whole cell pitches; the scrollbar and the grid's
    // ContentSizeFitter adapt on their own.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class CraftPanelLayout
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        private const int VanillaVisibleRows = 6;
        private const int VanillaColumns = 4;     // the grid's constraintCount as vanilla builds it
        // the grid as we grow it
        internal const int GridColumns = 6;
        internal const int GridRows = 8;

        private static Vector2 _vanillaPanelSize;
        private static int _vanillaColumns;
        private static bool _resizeLogged;
        private static float _vanillaMaskWidth;
        private static float _vanillaGridWidth;
        private static UIManager _ui;
        private static ScrollRect _craftScrollRect;

        [HarmonyPatch(nameof(InventoryManager.CraftOpen))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void CraftOpen_Prefix(InventoryManager __instance, int id)
        {
            ManagersScript mn = MnRef(__instance);
            if (CraftLazyFill.ShouldSkip(mn, id))
            {
                return;
            }
            GridLayoutGroup grid = FindGrid(mn);
            if (grid == null)
            {
                return;
            }
            RectTransform panel = mn.uiMN.craftPanel.GetComponent<RectTransform>();
            RectTransform mask = mn.uiMN.craftPanel.transform.Find("mask") as RectTransform;
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            if (_vanillaColumns == 0)
            {
                // THE BASE IS VANILLA'S OWN RECORD, not the panel as found. A hot
                // reload hands the next generation a panel already grown, and a
                // base read from it grew the panel two rows taller on every
                // reload, climbing from its bottom pivot until the tabs left the
                // screen. UIManager keeps the size it booted with.
                // The mask and grid have no record of their own, but they are
                // always widened together with the column count, by the same
                // width, so the count's excess over vanilla's four says how much a
                // previous generation left on them.
                Vector2 start = mn.uiMN.craftPanelStartSize;
                float grown = (grid.constraintCount - VanillaColumns) * (grid.cellSize.x + grid.spacing.x);
                _vanillaPanelSize = start.y > 1f ? start : panel.sizeDelta - new Vector2(grown, 0f);
                _vanillaColumns = VanillaColumns;
                _vanillaMaskWidth = mask != null ? mask.sizeDelta.x - grown : 0f;
                _vanillaGridWidth = gridRect.sizeDelta.x - grown;
            }
            int columns = GridColumns;
            int rows = GridRows;
            float pitchX = grid.cellSize.x + grid.spacing.x;
            float pitchY = grid.cellSize.y + grid.spacing.y;
            float widthDelta = (columns - _vanillaColumns) * pitchX;
            grid.constraintCount = columns;
            panel.sizeDelta = _vanillaPanelSize + new Vector2(
                widthDelta,
                (rows - VanillaVisibleRows) * pitchY);
            if (!_resizeLogged)
            {
                _resizeLogged = true;
                Plugin.Log.LogDebug($"LayoutPrefix: cols={columns} vanillaCols={_vanillaColumns} " +
                    $"widthDelta={widthDelta} vanillaPanel={_vanillaPanelSize} " +
                    $"-> panel sizeDelta={panel.sizeDelta} rect={panel.rect.size}");
            }
            // the mask stretches vertically with the panel but has a FIXED width — widen
            // it (and the grid's own background plate) or columns beyond vanilla get
            // rendered then clipped invisible
            if (mask != null)
            {
                mask.sizeDelta = new Vector2(_vanillaMaskWidth + widthDelta, mask.sizeDelta.y);
            }
            gridRect.sizeDelta = new Vector2(_vanillaGridWidth + widthDelta, gridRect.sizeDelta.y);

            // cache for the unified scroll interception below
            _ui = mn.uiMN;
            _craftScrollRect = mn.uiMN.craftPanel.GetComponentInChildren<ScrollRect>(true);
        }

        // ---------- unified scroll behavior ----------

        // Two vanilla wheel paths existed with wildly different feels: hovering a recipe
        // button added a FIXED FRACTION (0.15) of total content per notch (explodes with
        // aggregated list length), while hovering the panel background used the
        // ScrollRect's native pixel scrolling. Both are intercepted here and routed
        // through ONE row-based step, so the speed is identical by construction.
        internal static void StepRows(UIManager ui, float sign)
        {
            ScrollRows(ui, sign * Mathf.Max(0.1f, Settings.ScrollSpeed.Value));
        }

        internal static void ScrollRows(UIManager ui, float signedRows)
        {
            if (BrowserShell.Active)
            {
                BrowserShell.ScrollRows(signedRows);
                return;
            }
            if (ui == null || ui.craftPanel == null || ui.craftPanelScroll == null)
            {
                return;
            }
            Transform gridT = ui.craftPanel.transform.Find("mask/grid");
            RectTransform mask = ui.craftPanel.transform.Find("mask") as RectTransform;
            GridLayoutGroup grid = gridT != null ? gridT.GetComponent<GridLayoutGroup>() : null;
            if (grid == null || mask == null)
            {
                return;
            }
            float scrollable = ((RectTransform)gridT).rect.height - mask.rect.height;
            if (scrollable <= 1f)
            {
                return;
            }
            float rowPitch = grid.cellSize.y + grid.spacing.y;
            ui.craftPanelScroll.value = Mathf.Clamp01(
                ui.craftPanelScroll.value + signedRows * rowPitch / scrollable);
        }

        // page = the number of currently visible rows
        internal static float VisibleRows(UIManager ui)
        {
            if (BrowserShell.Active)
            {
                return BrowserShell.PageRows();
            }
            if (ui == null || ui.craftPanel == null)
            {
                return 6f;
            }
            RectTransform mask = ui.craftPanel.transform.Find("mask") as RectTransform;
            GridLayoutGroup grid = FindGridOf(ui);
            if (mask == null || grid == null)
            {
                return 6f;
            }
            return Mathf.Max(1f, Mathf.Floor(mask.rect.height / (grid.cellSize.y + grid.spacing.y)));
        }

        private static GridLayoutGroup FindGridOf(UIManager ui)
        {
            Transform gridT = ui.craftPanel.transform.Find("mask/grid");
            return gridT != null ? gridT.GetComponent<GridLayoutGroup>() : null;
        }

        // recipe-button hover path (ItemSlot.OnScroll -> CraftPanelScroll)
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.CraftPanelScroll))]
        [HarmonyPrefix]
        private static bool CraftPanelScroll_Prefix(UIManager __instance, float value)
        {
            StepRows(__instance, Mathf.Sign(value));
            return false;
        }

        // panel-background hover path (the craft ScrollRect's native wheel handler)
        [HarmonyPatch(typeof(ScrollRect), "OnScroll")]
        [HarmonyPrefix]
        private static bool ScrollRect_OnScroll_Prefix(ScrollRect __instance, PointerEventData data)
        {
            if (_craftScrollRect == null || __instance != _craftScrollRect || _ui == null)
            {
                return true;
            }
            if (!Mathf.Approximately(data.scrollDelta.y, 0f))
            {
                StepRows(_ui, data.scrollDelta.y > 0f ? 1f : -1f);
            }
            return false;
        }

        // The panel, its mask and its grid back at vanilla's size and columns, for
        // Plugin.Teardown: the next generation is handed the panel vanilla built,
        // not the one this generation grew.
        internal static void RestoreVanilla(ManagersScript mn)
        {
            if (_vanillaColumns == 0 || mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null)
            {
                return;
            }
            GridLayoutGroup grid = FindGrid(mn);
            RectTransform panel = mn.uiMN.craftPanel.GetComponent<RectTransform>();
            RectTransform mask = mn.uiMN.craftPanel.transform.Find("mask") as RectTransform;
            panel.sizeDelta = _vanillaPanelSize;
            if (mask != null)
            {
                mask.sizeDelta = new Vector2(_vanillaMaskWidth, mask.sizeDelta.y);
            }
            if (grid != null)
            {
                grid.constraintCount = _vanillaColumns;
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                gridRect.sizeDelta = new Vector2(_vanillaGridWidth, gridRect.sizeDelta.y);
            }
        }

        // What the first CraftOpen will ADD to the panel's sizeDelta, in the
        // panel's own units - zero once it has. Slot.Read asks, so a window laid
        // out against the still-closed panel comes out the size the open one will
        // be, instead of vanilla's.
        internal static Vector2 PendingGrowth(ManagersScript mn)
        {
            if (_vanillaColumns != 0)
            {
                return Vector2.zero;
            }
            GridLayoutGroup grid = FindGrid(mn);
            if (grid == null)
            {
                return Vector2.zero;
            }
            int columns = GridColumns;
            int rows = GridRows;
            return new Vector2(
                (columns - grid.constraintCount) * (grid.cellSize.x + grid.spacing.x),
                (rows - VanillaVisibleRows) * (grid.cellSize.y + grid.spacing.y));
        }

        internal static GridLayoutGroup FindGrid(ManagersScript mn)
        {
            if (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null)
            {
                return null;
            }
            Transform grid = mn.uiMN.craftPanel.transform.Find("mask/grid");
            return grid != null ? grid.GetComponent<GridLayoutGroup>() : null;
        }
    }
}
