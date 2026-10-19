using HarmonyLib;

namespace IslandUI
{
    // The favourite mark follows its item (Favourites). Every move in the game ends
    // in InventoryManager's two primitives - SlotLoadSlot copies an item into a
    // slot, SlotClear empties one - and SlotToSlot, the cursor's swap through
    // stockSlot and RandomCharacter.EquipItem's swap through equipSlot are all built
    // from them. So a load copies the mark with the item and a clear drops it. The
    // two moves that work around them are covered here as well: StackSameItem
    // merges or swaps two stacks of one kind in place, and SlotToSlotSplit loads
    // only PART of a stack.
    //
    // The mark only lands in YOUR inventory - the Shared Bag, your bag, your
    // equipment, and the transit slots a move passes through. Loaded anywhere else
    // (a chest, a follower, the world) it is not copied. Those moves are refused
    // now (FavouriteRules, kept); this is the backstop for any path they miss, which
    // then ends the mark rather than carrying it somewhere that is not yours.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class FavouriteMoves
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // set while SlotToSlotSplit runs: its load copies a PART, and a part split
        // off a favourite starts unmarked
        private static bool _splitting;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.SlotLoadSlot))]
        private static void AfterLoad(InventoryManager __instance, ItemSlot from, ItemSlot to)
        {
            if (!Favourites.Any || to == null || ReferenceEquals(from, to))
            {
                return;
            }
            Favourites.Set(to, !_splitting && Favourites.IsFavourite(from) && Yours(__instance, to));
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.SlotClear))]
        private static void AfterClear(ItemSlot tmpSlot)
        {
            if (Favourites.Any)
            {
                Favourites.Set(tmpSlot, false);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(InventoryManager.SlotToSlotSplit))]
        private static void BeforeSplit(ItemSlot from, out bool __state)
        {
            __state = Favourites.IsFavourite(from);
            _splitting = true;
        }

        // A split that took the whole stack is a move, and a move carries the mark.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.SlotToSlotSplit))]
        private static void AfterSplit(InventoryManager __instance, ItemSlot from, ItemSlot to, bool __state)
        {
            _splitting = false;
            if (__state && from != null && string.IsNullOrEmpty(from.itemKey) && Yours(__instance, to))
            {
                Favourites.Set(to, true);
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(InventoryManager.SlotToSlotSplit))]
        private static void SplitFinally()
        {
            _splitting = false;
        }

        private struct Stacking
        {
            public bool Same, Swap, FromMarked, ToMarked;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(InventoryManager.StackSameItem))]
        private static void BeforeStack(InventoryManager __instance, ItemSlot from, ItemSlot to,
            bool swapable, out Stacking __state)
        {
            __state = default(Stacking);
            if (!Favourites.Any || from == null || to == null || string.IsNullOrEmpty(from.itemKey)
                || from.itemKey != to.itemKey)
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            ItemData data = mn != null && mn.itemMN != null ? mn.itemMN.FindItem(to.itemKey) : null;
            __state.Same = true;
            __state.Swap = swapable && data != null && to.stack == data.maxStack;
            __state.FromMarked = Favourites.IsFavourite(from);
            __state.ToMarked = Favourites.IsFavourite(to);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.StackSameItem))]
        private static void AfterStack(InventoryManager __instance, ItemSlot from, ItemSlot to, Stacking __state)
        {
            if (!__state.Same || !(__state.FromMarked || __state.ToMarked))
            {
                return;
            }
            if (__state.Swap)
            {
                // two full stacks of one kind trade contents in place - life, stack
                // and owner move, the slots do not - so the marks trade with them
                Favourites.Set(to, __state.FromMarked && Yours(__instance, to));
                Favourites.Set(from, __state.ToMarked && Yours(__instance, from));
                return;
            }
            // merged, wholly or in part: the stack it went into is marked if either was
            if (__state.FromMarked && Yours(__instance, to))
            {
                Favourites.Set(to, true);
            }
        }

        internal static bool Yours(InventoryManager inv, ItemSlot slot)
        {
            if (inv == null || slot == null)
            {
                return false;
            }
            if (ReferenceEquals(slot, inv.hoverSlot) || ReferenceEquals(slot, inv.stockSlot)
                || ReferenceEquals(slot, inv.equipSlot))
            {
                return true;
            }
            ItemSlot[] shared = inv.itemSlot;
            for (int i = 0; shared != null && i < inv.baseCount && i < shared.Length; i++)
            {
                if (ReferenceEquals(shared[i], slot))
                {
                    return true;
                }
            }
            ManagersScript mn = MnRef(inv);
            CommonStates you = mn != null && mn.gameMN != null && mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null;
            return you != null && (Holds(you.bag != null ? you.bag.slots : null, slot) || Holds(you.equip, slot));
        }

        // Harness door: a round trip through the real primitives on your own Shared
        // Bag - mark a stack, move it to an empty cell and back, split one off and
        // merge it back - leaving the bag exactly as it was found.
        internal static string SelfTest()
        {
            ManagersScript mn = UnityEngine.GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null || inv.hoverSlot == null || inv.hoverSlot.gameObject.activeSelf)
            {
                return "skipped: no inventory, or an item on the cursor";
            }
            ItemSlot a = null, b = null;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot s = inv.itemSlot[i];
                if (b == null && string.IsNullOrEmpty(s.itemKey)) { b = s; }
                if (!string.IsNullOrEmpty(s.itemKey) && s.common == null && (a == null || s.stack > a.stack)) { a = s; }
            }
            if (a == null || b == null)
            {
                return "skipped: needs one item and one empty cell in the Shared Bag";
            }
            var log = new System.Text.StringBuilder(a.itemKey + " x" + a.stack + ":");
            bool was = Favourites.IsFavourite(a);
            Favourites.Set(a, true);
            inv.SlotToSlot(a, b);
            log.Append(" move ").Append(Favourites.IsFavourite(b) && !Favourites.IsFavourite(a) ? "ok" : "FAIL");
            inv.SlotToSlot(b, a);
            log.Append(" back ").Append(Favourites.IsFavourite(a) && !Favourites.IsFavourite(b) ? "ok" : "FAIL");
            if (a.stack > 1)
            {
                inv.SlotToSlotSplit(a, b, 1);
                log.Append(" split ").Append(Favourites.IsFavourite(a) && !Favourites.IsFavourite(b) ? "ok" : "FAIL");
                inv.SlotToSlotSplit(b, a, 1);
                log.Append(" merge ").Append(Favourites.IsFavourite(a) && string.IsNullOrEmpty(b.itemKey) ? "ok" : "FAIL");
            }
            Favourites.Set(a, was);
            InventoryWindow.Invalidate();
            return log.ToString();
        }

        private static bool Holds(ItemSlot[] slots, ItemSlot slot)
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (ReferenceEquals(slots[i], slot))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
