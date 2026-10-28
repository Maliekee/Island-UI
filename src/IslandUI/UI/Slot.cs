using UnityEngine;

namespace IslandUI.UI
{
    // Shared dimensions for Crafting, Vicinity and Roster. The bottom dock
    // selects the occupant and anchors the window; content grows upward.
    internal static class Slot
    {
        public const float Inset = 10f;      // the inventory's FrameInsetX
        public const float PadTop = 8f;      // ...and FrameInsetY
        public const float HeaderH = 13f;    // the header row: the filter and a pill
        public const float HeaderGap = 3f;   // between the header row and the grid
        public const float PillReserve = 21f; // the header row's right end: the pill (18) and a gap
        public const float BarW = 6.5f;      // the kit scrollbar, 20px
        public const float BarGap = 4f;
        public const float TabX = 9f;        // the plate tabs, in from the left
        // THE SLOT'S THREE OCCUPANTS (D53): the bench, the containers around you,
        // and the people you employ. One row of tabs, one array, read by both
        // windows that can hold the slot - the crafting shell and this one - so a
        // tab cannot exist on one and not the other. The names are the tabs' ids
        // (their GameObjects); TabLabel is what the player reads.
        public static readonly string[] TabNames = { "Crafting", "In reach", "Roster" };
        public static readonly string[] TabGlyphs = { "craft", "vicinity", "roster" };

        public static string TabLabel(int index)
        {
            return index == 0 ? Shared.Loc.Get("hud.chip.crafting", "Crafting")
                : index == 1 ? Shared.Loc.Get("reach.title", "In reach")
                : Shared.Loc.Get("roster.title", "Roster");
        }
        // the tabs' woods, in TabNames order: the bench's walnut, then the two
        // ledgers' oak - the vicinity list and the roster are the same parchment
        // (D46), so they wear the same wood and only their height says which is up
        public static readonly string[] TabWoods = { "kit_plank_walnut", "kit_plank_oak", "kit_plank_oak" };
        public static bool Known { get; private set; }
        public static float Left { get; private set; }
        public static float Bottom { get; private set; }
        public static float Height { get; private set; }

        // AS WIDE AS THE OTHER INVENTORY WINDOWS, the same space as the other
        // two (ten cells): the slot's content is MaxPlateColumns
        // of the inventory's own cells, the bags' width exactly.
        public static int ReachCols => InventoryWindow.MaxPlateColumns;
        public static float Pitch => InventoryWindow.CellPitchNow;
        public static float Box => InventoryWindow.CellBoxNow;
        public static float ContentW => ReachCols * Pitch - (Pitch - Box);
        // THE GUTTER IS EXTRA WIDTH, NOT A CELL: ten cells is good, so the window
        // grows wider rather than losing a cell. The vicinity's plates carry their
        // icon in a gutter left of the cells, so the slot is ten cells AND a gutter.
        public static float GutterW => InventoryWindow.GutterWidth;
        public static float InnerW => GutterW + ContentW;
        // THE CRAFTING GRID KEEPS VANILLA'S PITCH (the original space between
        // cells, even though it then differs from the other windows) and
        // takes as many of its wider columns as fit - eight in ten cells' width,
        // not the nine guessed, since 9 x 25 wants eleven units the bags do not
        // have; the leftover centres the grid.
        public const float CraftPitch = 25f;
        public const float CraftBox = 20f;
        public static int CraftCols => Mathf.Max(1, Mathf.FloorToInt((InnerW + (CraftPitch - CraftBox)) / CraftPitch));
        public static float CraftW => CraftCols * CraftPitch - (CraftPitch - CraftBox);
        public static float CraftLeft => Inset + (InnerW - CraftW) * 0.5f;
        public static int Cols => CraftCols;
        // Scrolling has its own gutter, independent of the compact container layout.
        public static float Width => 2f * Inset + InnerW + BarGap + BarW;
        public static float GridTop => PadTop + HeaderH + HeaderGap;
        public static float GridH => Mathf.Max(Pitch, Height - GridTop - PadTop);

        // The bottom dock owns navigation. Every left window grows above it.
        public static bool Read(ManagersScript mn, RectTransform root)
        {
            Known = root != null && mn != null && mn.uiMN != null;
            if (!Known) return false;
            Left = 8f;
            Bottom = 72f;
            Height = Mathf.Min(280f, Mathf.Max(80f, root.rect.height - Bottom - 24f));
            return true;
        }
    }
}
