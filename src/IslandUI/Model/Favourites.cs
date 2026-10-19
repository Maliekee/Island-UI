using System.Collections.Generic;

namespace IslandUI
{
    // The ONE mark. Alt+click makes an item a favourite; a favourite stays in
    // your bags - quick-stack and a chest's All in / Stack leave it - and is the
    // one the game reaches for (the tool auto-swap derives from it rather than
    // adding a second mark). Terraria's semantics and Terraria's gesture.
    //
    // MARKED BY SLOT, AND IT TRAVELS WITH THE ITEM (several axes, only one of them
    // favourited and the rest auto-stacked). It was by ITEM KEY until then, which cannot tell one axe from its twin. The
    // mark now sits on the ItemSlot holding the item, and FavouriteMoves carries it
    // through the game's move primitives, so it follows a drag, a swap, a merge or
    // an equip. It is a mark on YOUR inventory: used up or broken, it is gone. It
    // cannot be moved into a chest or dropped at all while marked (FavouriteRules,
    // kept); before that, such a move simply ended the mark.
    //
    // Each mark keeps the key it was made on, and a slot that no longer holds that
    // key is not a favourite. A move path the patches miss therefore loses the mark
    // rather than handing it to whatever lands in the slot next.
    //
    // MARKS PERSIST INSIDE THE SAVE: SaveBlock writes each mark
    // as the cell holding it plus the item's key, and puts it back on load where
    // that cell still holds that item. A slot mark is only true of the slots of
    // this scene, so the scene load still clears them first.
    internal static class Favourites
    {
        private static readonly Dictionary<ItemSlot, string> Marked = new Dictionary<ItemSlot, string>();

        internal static bool Any => Marked.Count > 0;

        // every slot still holding what was marked in it (SaveBlock)
        internal static List<ItemSlot> Slots()
        {
            var slots = new List<ItemSlot>();
            foreach (KeyValuePair<ItemSlot, string> m in Marked)
            {
                if (m.Key != null && m.Key.itemKey == m.Value)
                {
                    slots.Add(m.Key);
                }
            }
            return slots;
        }

        internal static bool IsFavourite(ItemSlot slot)
        {
            return slot != null && !string.IsNullOrEmpty(slot.itemKey)
                && Marked.TryGetValue(slot, out string key) && key == slot.itemKey;
        }

        internal static bool Toggle(ItemSlot slot)
        {
            bool on = !IsFavourite(slot);
            Set(slot, on);
            return on && IsFavourite(slot);
        }

        internal static void Set(ItemSlot slot, bool on)
        {
            if (slot == null)
            {
                return;
            }
            if (on && !string.IsNullOrEmpty(slot.itemKey))
            {
                Marked[slot] = slot.itemKey;
            }
            else
            {
                Marked.Remove(slot);
            }
        }

        internal static void ResetForSceneLoad()
        {
            Marked.Clear();
        }
    }
}
