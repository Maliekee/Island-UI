using System.Collections.Generic;

namespace IslandUI
{
    // THE TOOL BELT's three hooks - axe, pickaxe, knife. The tool on a
    // hook is the one the game reaches for when an action needs that kind
    // (FavouriteRules' tool swap, which once read favourites; the belt keeps it
    // separate from favouriting).
    //
    // A hung tool is a MARK on the ItemSlot holding it, as a favourite is
    // (Favourites), and it travels with its item the same way (ToolBeltMoves): a
    // load copies it into your own slots, a clear drops it. So it follows a drag, a
    // swap and the tool swap's own equip, and it ends when the item leaves your
    // inventory, breaks or is used up - the hook is then empty again. Each mark keeps
    // the key it was hung by, and a slot that no longer holds that key is not hung.
    //
    // One tool to a hook, but one tool may hang on two: a picknaxe is both an axe
    // and a pickaxe. Kept inside the save with the favourites (SaveBlock).
    internal static class ToolBelt
    {
        internal const int Axe = 0, Pick = 1, Knife = 2, Hooks = 3;

        // Per hook, the slots holding its tool: one, except for the length of a move
        // (a load copies before the clear) and while a copy of your bag is shown.
        private static readonly Dictionary<ItemSlot, string>[] Marked =
        {
            new Dictionary<ItemSlot, string>(),
            new Dictionary<ItemSlot, string>(),
            new Dictionary<ItemSlot, string>(),
        };

        internal static bool Any => Marked[Axe].Count + Marked[Pick].Count + Marked[Knife].Count > 0;
        internal static int Revision { get; private set; }

        internal static bool IsEquipped(int hook, CommonStates owner)
        {
            return owner != null && owner.equip != null && owner.equip.Length > 1
                && On(hook, owner.equip[1]);
        }

        // the harvest table's families (FavouriteRules.Axes / Picks / Knives)
        internal static bool Fits(int hook, ItemData.ToolType type)
        {
            switch (hook)
            {
                case Axe:
                    return type == ItemData.ToolType.Axe || type == ItemData.ToolType.Axe2
                        || type == ItemData.ToolType.Picknaxe;
                case Pick:
                    return type == ItemData.ToolType.Pickax || type == ItemData.ToolType.Pickax2
                        || type == ItemData.ToolType.Picknaxe;
                case Knife:
                    return type == ItemData.ToolType.Knife;
                default:
                    return false;
            }
        }

        // the tool on a hook: the first of its slots still holding what was hung
        internal static ItemSlot Hung(int hook)
        {
            foreach (KeyValuePair<ItemSlot, string> m in Marked[hook])
            {
                if (m.Key != null && m.Key.itemKey == m.Value)
                {
                    return m.Key;
                }
            }
            return null;
        }

        // every slot of a hook still holding what was hung: more than one only while
        // a move or a shown copy of your bag is under way (SaveBlock picks the one
        // it can address)
        internal static List<ItemSlot> HungSlots(int hook)
        {
            var slots = new List<ItemSlot>();
            foreach (KeyValuePair<ItemSlot, string> m in Marked[hook])
            {
                if (m.Key != null && m.Key.itemKey == m.Value)
                {
                    slots.Add(m.Key);
                }
            }
            return slots;
        }

        internal static bool On(int hook, ItemSlot slot)
        {
            return slot != null && !string.IsNullOrEmpty(slot.itemKey)
                && Marked[hook].TryGetValue(slot, out string key) && key == slot.itemKey;
        }

        internal static bool IsHung(ItemSlot slot)
        {
            return Any && (On(Axe, slot) || On(Pick, slot) || On(Knife, slot));
        }

        internal static void Hang(int hook, ItemSlot slot)
        {
            Revision++;
            Marked[hook].Clear();
            if (slot != null && !string.IsNullOrEmpty(slot.itemKey))
            {
                Marked[hook][slot] = slot.itemKey;
            }
        }

        internal static void TakeDown(int hook)
        {
            if (Marked[hook].Count > 0) { Revision++; }
            Marked[hook].Clear();
        }

        // SlotLoadSlot copied `from`'s item into `to`: the marks go with it into your
        // slots, and whatever `to` held was overwritten, its marks with it
        internal static void Loaded(ItemSlot from, ItemSlot to, bool yours)
        {
            for (int h = 0; h < Hooks; h++)
            {
                if (yours && On(h, from))
                {
                    if (!On(h, to)) { Revision++; }
                    Marked[h][to] = to.itemKey;
                }
                else
                {
                    if (Marked[h].Remove(to)) { Revision++; }
                }
            }
        }

        internal static void Cleared(ItemSlot slot)
        {
            for (int h = 0; h < Hooks; h++)
            {
                if (Marked[h].Remove(slot)) { Revision++; }
            }
        }

        // StackSameItem's full-stack swap: two tools of one kind trade contents in
        // place, the slots do not move - so the marks trade. `was` are the hooks
        // each side was on before, as bits.
        internal static void Traded(ItemSlot a, ItemSlot b, int aWas, int bWas, bool aYours, bool bYours)
        {
            if (aWas != 0 || bWas != 0) { Revision++; }
            for (int h = 0; h < Hooks; h++)
            {
                Marked[h].Remove(a);
                Marked[h].Remove(b);
                if ((aWas & (1 << h)) != 0 && bYours)
                {
                    Marked[h][b] = b.itemKey;
                }
                if ((bWas & (1 << h)) != 0 && aYours)
                {
                    Marked[h][a] = a.itemKey;
                }
            }
        }

        internal static int HooksOf(ItemSlot slot)
        {
            int bits = 0;
            for (int h = 0; h < Hooks; h++)
            {
                if (On(h, slot))
                {
                    bits |= 1 << h;
                }
            }
            return bits;
        }

        internal static void ResetForSceneLoad()
        {
            if (Any) { Revision++; }
            for (int h = 0; h < Hooks; h++)
            {
                Marked[h].Clear();
            }
        }
    }
}
