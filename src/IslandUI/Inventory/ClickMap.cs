using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // THE CLICK MAP: what a held modifier makes a click do to the thing under the
    // pointer. The click handlers and the cursor badge both ask it, so the badge
    // can never promise what the click will not do (CursorBadge's first rule), and
    // a gesture is added or moved in one place. The table:
    //
    //   Alt+click     an item in your bags or        favourite / unmark
    //                 on your loadout
    //   Shift+click   an item cell you can reach     the split dialog (a stack of 2+)
    //   Ctrl+click    an item in a bag or chest      move it to the other side, or
    //                                                drop it (InventoryWindow.Move)
    //   Shift+click   an action bar slot             remove the action
    //   Shift+drag    an action bar slot             move the action (ActionBar)
    //
    // Ctrl+click on a favourite moves nothing: a favourite stays in your bags
    // (FavouriteRules), so Ctrl names why instead of where (Verb.Kept).
    //
    // A verb is only offered with an empty hand: mid-gesture a click already means
    // "place this", and one click may not mean two things. docs/keymap.md carries
    // the same table for the keyboard side.
    internal static class ClickMap
    {
        internal enum Verb { None, Favourite, Unmark, Split, RemoveAction, Move, Drop, Kept }

        internal static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        internal static bool Alt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        internal static bool Ctrl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        // An item cell of our windows. `yourBags` is InventoryWindow.WearsFavourites;
        // `moveTo` is where Ctrl+click would send it (InventoryWindow.RouteFrom):
        // 0 nowhere, 1 somewhere, 2 the ground.
        internal static Verb ForCell(ItemSlot slot, bool handEmpty, bool yourBags, bool loadout, int moveTo = 0)
        {
            if (slot == null || string.IsNullOrEmpty(slot.itemKey) || !handEmpty)
            {
                return Verb.None;
            }
            if (Alt)
            {
                return !yourBags ? Verb.None : Favourites.IsFavourite(slot) ? Verb.Unmark : Verb.Favourite;
            }
            if (Shift && !loadout && slot.stack > 1)
            {
                return Verb.Split;
            }
            if (Ctrl && !Shift && moveTo != 0)
            {
                // a favourite is only ever in your bags, and from there every
                // route leads out
                return Favourites.IsFavourite(slot) ? Verb.Kept : moveTo == 2 ? Verb.Drop : Verb.Move;
            }
            return Verb.None;
        }

        // An action bar slot: only a bound one has something to remove.
        internal static Verb ForBarSlot(bool bound, bool handEmpty)
        {
            return Shift && !Alt && bound && handEmpty ? Verb.RemoveAction : Verb.None;
        }

        internal static void Offer(Verb verb, string where = null)
        {
            switch (verb)
            {
                case Verb.Move:
                    CursorBadge.Show("ctrl-click  →  " + where, HudGlyphs.Get("move"));
                    break;
                case Verb.Drop:
                    CursorBadge.Show("ctrl-click  →  drop", HudGlyphs.Get("drop"));
                    break;
                case Verb.Kept:
                    CursorBadge.Show(FavouriteRules.KeptWords, HudGlyphs.Get("star"));
                    break;
                case Verb.Favourite:
                    CursorBadge.Show("alt-click  →  favourite", HudGlyphs.Get("star"));
                    break;
                case Verb.Unmark:
                    CursorBadge.Show("alt-click  →  unmark", HudGlyphs.Get("star"));
                    break;
                case Verb.Split:
                    CursorBadge.Show("shift-click  →  split", HudGlyphs.Get("split"));
                    break;
                case Verb.RemoveAction:
                    CursorBadge.Show("shift-click  →  remove    shift-drag  →  move", HudGlyphs.Get("remove"));
                    break;
            }
        }
    }
}
