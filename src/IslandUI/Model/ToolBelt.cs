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
    //
    // ONE BELT PER PROTAGONIST (Forgejo #175), as the action bar keeps a row each:
    // Man and Yona each hang their own tools, keyed by the played character's
    // npcID (0 and 1), and the belt shown and reached for is the one of whoever
    // is played. A mark still travels with its item whichever belt it is on, so
    // a move updates every belt, not only the shown one.
    internal static class ToolBelt
    {
        internal const int Axe = 0, Pick = 1, Knife = 2, Hooks = 3;

        // Per protagonist, per hook, the slots holding its tool: one, except for the
        // length of a move (a load copies before the clear) and while a copy of your
        // bag is shown.
        private static readonly Dictionary<int, Dictionary<ItemSlot, string>[]> Belts =
            new Dictionary<int, Dictionary<ItemSlot, string>[]>();
        private static int _who;

        private static Dictionary<ItemSlot, string>[] Marked => BeltOf(_who);

        private static Dictionary<ItemSlot, string>[] BeltOf(int who)
        {
            if (!Belts.TryGetValue(who, out Dictionary<ItemSlot, string>[] belt))
            {
                belt = new[]
                {
                    new Dictionary<ItemSlot, string>(),
                    new Dictionary<ItemSlot, string>(),
                    new Dictionary<ItemSlot, string>(),
                };
                Belts[who] = belt;
            }
            return belt;
        }

        // any belt: the moves' fast path, which must see the belt not shown too
        internal static bool Any
        {
            get
            {
                foreach (Dictionary<ItemSlot, string>[] belt in Belts.Values)
                {
                    if (belt[Axe].Count + belt[Pick].Count + belt[Knife].Count > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static int Revision { get; private set; }

        // the protagonists whose belts exist, for the save and a reload
        internal static List<int> Owners => new List<int>(Belts.Keys);

        // The played character changed: the belt turns to theirs.
        internal static void Follow(ManagersScript mn)
        {
            CommonStates player = mn != null && mn.gameMN != null && mn.gameMN.pMove != null
                ? mn.gameMN.pMove.common : null;
            if (player != null && player.npcID != _who)
            {
                _who = player.npcID;
                Revision++;
            }
        }

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
            return HungSlots(_who, hook);
        }

        internal static List<ItemSlot> HungSlots(int who, int hook)
        {
            var slots = new List<ItemSlot>();
            foreach (KeyValuePair<ItemSlot, string> m in BeltOf(who)[hook])
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
            return On(Marked, hook, slot);
        }

        private static bool On(Dictionary<ItemSlot, string>[] belt, int hook, ItemSlot slot)
        {
            return slot != null && !string.IsNullOrEmpty(slot.itemKey)
                && belt[hook].TryGetValue(slot, out string key) && key == slot.itemKey;
        }

        internal static bool IsHung(ItemSlot slot)
        {
            return Any && (On(Axe, slot) || On(Pick, slot) || On(Knife, slot));
        }

        internal static void Hang(int hook, ItemSlot slot)
        {
            Hang(_who, hook, slot);
        }

        // on a named protagonist's belt: a load, for the one not being played too
        internal static void Hang(int who, int hook, ItemSlot slot)
        {
            Revision++;
            Dictionary<ItemSlot, string>[] belt = BeltOf(who);
            belt[hook].Clear();
            if (slot != null && !string.IsNullOrEmpty(slot.itemKey))
            {
                belt[hook][slot] = slot.itemKey;
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
            foreach (Dictionary<ItemSlot, string>[] belt in Belts.Values)
            {
                for (int h = 0; h < Hooks; h++)
                {
                    if (yours && On(belt, h, from))
                    {
                        if (!On(belt, h, to)) { Revision++; }
                        belt[h][to] = to.itemKey;
                    }
                    else
                    {
                        if (belt[h].Remove(to)) { Revision++; }
                    }
                }
            }
        }

        internal static void Cleared(ItemSlot slot)
        {
            foreach (Dictionary<ItemSlot, string>[] belt in Belts.Values)
            {
                for (int h = 0; h < Hooks; h++)
                {
                    if (belt[h].Remove(slot)) { Revision++; }
                }
            }
        }

        // StackSameItem's full-stack swap: two tools of one kind trade contents in
        // place, the slots do not move - so the marks trade. `was` are the hooks
        // each side was on before, as bits, by belt (HooksOf).
        internal static void Traded(ItemSlot a, ItemSlot b, Dictionary<int, int> aWas,
            Dictionary<int, int> bWas, bool aYours, bool bYours)
        {
            if (aWas == null && bWas == null)
            {
                return;
            }
            Revision++;
            foreach (KeyValuePair<int, Dictionary<ItemSlot, string>[]> pair in Belts)
            {
                Dictionary<ItemSlot, string>[] belt = pair.Value;
                int aBits = aWas != null && aWas.TryGetValue(pair.Key, out int x) ? x : 0;
                int bBits = bWas != null && bWas.TryGetValue(pair.Key, out int y) ? y : 0;
                for (int h = 0; h < Hooks; h++)
                {
                    belt[h].Remove(a);
                    belt[h].Remove(b);
                    if ((aBits & (1 << h)) != 0 && bYours)
                    {
                        belt[h][b] = b.itemKey;
                    }
                    if ((bBits & (1 << h)) != 0 && aYours)
                    {
                        belt[h][a] = a.itemKey;
                    }
                }
            }
        }

        // the hooks a slot is on, as bits, for each belt it is on; null for none
        internal static Dictionary<int, int> HooksOf(ItemSlot slot)
        {
            Dictionary<int, int> bits = null;
            foreach (KeyValuePair<int, Dictionary<ItemSlot, string>[]> pair in Belts)
            {
                int b = 0;
                for (int h = 0; h < Hooks; h++)
                {
                    if (On(pair.Value, h, slot))
                    {
                        b |= 1 << h;
                    }
                }
                if (b != 0)
                {
                    bits = bits ?? new Dictionary<int, int>();
                    bits[pair.Key] = b;
                }
            }
            return bits;
        }

        internal static void ResetForSceneLoad()
        {
            if (Any) { Revision++; }
            Belts.Clear();
            _who = 0;
        }
    }
}
