using UnityEngine;

namespace IslandUI
{
    // Using an item that is not in inventory.itemSlot[].
    //
    // THE OBSTACLE. `ItemManager.UseItem` takes an INDEX into
    // `inventory.itemSlot[]`, and most containers are not in that array: a
    // character's bag is `ItemSlot` components hung on the bag object
    // (`ItemManager.cs:931`), and our plates bind those arrays directly. A cell
    // only carries a real vanilla index when its container happens to be the live
    // mirror — which is why right-clicking food in your own pack, or a target's,
    // did nothing at all: it fell through to an equip-only branch and returned.
    //
    // THE DEAD END, recorded so nobody tries it twice. Pointing a spare
    // `itemSlot[]` entry at a bag slot and passing that index CRASHES:
    // `StackTextChange` does `itemSlot.gameObject.transform.Find("stack")`
    // (`InventoryManager.cs:546`) on whatever the index resolves to, and a bag slot
    // is a bare component with no such child.
    //
    // ONE COPY OF THIS, deliberately. The action bar solved it first and the
    // Inventory needed the same answer; two copies of a dispatch this fiddly is
    // exactly the drift seen before, where two gates carrying the same stale comment
    // disagreed with reality in the same way.
    internal static class ItemUse
    {
        // WHO AN ITEM ACTS ON (window-system.md 6.31, D141, superseding D25): YOU,
        // unless you dropped it on someone. The target is who you are dealing with,
        // not who you feed: a plain use with a target up used to feed them.
        //
        // ON YOU: vanilla picks `menuNPC ?? player`, so the field is emptied around
        // the call - the action bar's guard (D27), now everyone's. Safe for the
        // reason the bar wrote down: every read that picks the actor happens before
        // the first yield (ItemManager.UseFood / UseConsumables), so the actor is
        // captured by the time StartCoroutine returns.
        //
        // ON THEM: the drop has already made them the target (NPCMenuOpen sets
        // menuNPC the way vanilla does), so the call runs with it as it stands.
        // Recipient names them for the item paths of ours that do not go through
        // vanilla's choice (pocket_01).
        internal static CommonStates Recipient { get; private set; }

        internal static void OnYou(ManagersScript mn, System.Action use)
        {
            CommonStates focus = mn.gameMN.menuNPC;
            try
            {
                mn.gameMN.menuNPC = null;
                use();
            }
            finally
            {
                mn.gameMN.menuNPC = focus;
            }
        }

        internal static void OnThem(ManagersScript mn, CommonStates who, System.Action use)
        {
            CommonStates focus = mn.gameMN.menuNPC;
            Recipient = who;
            try
            {
                mn.gameMN.menuNPC = who;
                use();
            }
            finally
            {
                mn.gameMN.menuNPC = focus;
                Recipient = null;
            }
        }

        // NOT ON YOU. Vanilla consumes a consumable only when it took (the flag in
        // UseConsumables), so most wrong recipients are refused by the game itself
        // and the item is kept. These two always take: on the player they set your
        // affection toward yourself, and the potion is spent for nothing (reported
        // after 0.3.0). A plain use refuses them and says how to give one - the rule
        // stays "on you, unless dropped on someone", and the refusal teaches the drop.
        private static readonly System.Collections.Generic.HashSet<string> NotOnYou =
            new System.Collections.Generic.HashSet<string> { "potion_love_01", "potion_hate_01" };

        /// <summary>
        /// True, with the hint shown, when <paramref name="slot"/> holds an item
        /// that does nothing for you. Asked by every plain use before it fires.
        /// </summary>
        internal static bool RefusedOnYou(ManagersScript mn, ItemSlot slot)
        {
            if (mn == null || slot == null || string.IsNullOrEmpty(slot.itemKey)
                || !NotOnYou.Contains(slot.itemKey))
            {
                return false;
            }
            string name = InventoryWindow.GiveName();
            string hint = "You can't use this on yourself. Drag it onto "
                + (name ?? "someone") + " to give it.";
            if (mn.eventMN != null)
            {
                mn.eventMN.StartCoroutine(mn.eventMN.GoCautionSt(hint));
            }
            Plugin.Log.LogInfo("Use: " + slot.itemKey + " refused on you");
            return true;
        }

        /// <summary>
        /// Use <paramref name="slot"/> through <paramref name="aim"/>, which is the
        /// caller's own menuNPC guard and therefore decides WHO acts (D25).
        /// <paramref name="vanillaSlotId"/> is the slot's index in
        /// `inventory.itemSlot[]`, or -1 when it has none.
        /// <paramref name="mayLeaveStaged"/> must be true only when the staging
        /// destination and the source have the SAME holder — see below.
        /// Returns false when nothing was done, so the caller can say so.
        /// </summary>
        internal static bool Use(ManagersScript mn, ItemSlot slot, int vanillaSlotId,
            System.Action<System.Action> aim, bool mayLeaveStaged)
        {
            if (mn == null || mn.itemMN == null || mn.inventory == null
                || slot == null || string.IsNullOrEmpty(slot.itemKey) || aim == null)
            {
                return false;
            }
            ItemData data = mn.itemMN.FindItem(slot.itemKey);
            if (data == null)
            {
                return false;
            }

            // Already addressable: vanilla's whole use path, untouched.
            if (vanillaSlotId >= 0)
            {
                aim(() => mn.itemMN.UseItem(vanillaSlotId));
                return true;
            }

            // No index needed. UseEquipItem takes the ItemSlot OBJECT, so a bag
            // slot goes straight in — UseItem's own leaf, called without the
            // dispatcher that would have wanted the index. It refuses with caution
            // 20 when the character cannot wear the thing, which is vanilla's own
            // feedback, so don't pre-empt it.
            if (IsEquipType(data))
            {
                aim(() => mn.itemMN.UseEquipItem(slot));
                return true;
            }
            if (!HasAction(data))
            {
                return false;   // Item and ResourceW have no verb at all
            }

            // Everything else re-reads itemSlot[slotID] on the way down, so the
            // item has to genuinely BE in the array for the call. Stage it into a
            // free Shared Bag slot and fire from there.
            //
            // WHICH TYPES MAY BE STAGED IS A D25 QUESTION, not a convenience one.
            // Food and Bottle go through UseFood, which is synchronous: by the time
            // UseItem returns the stack is already decremented and nothing holds
            // the index, so the remainder goes straight back where it came from and
            // the item was never anywhere else. Consumables are the same, though
            // they look otherwise: UseConsumables is a coroutine, but one with no
            // `yield return` in it - every case, the pocket and the figure box
            // included, runs to its ConsumeItem (or its refusal) inside the
            // StartCoroutine call, and Drone takes no index. They were left staged
            // once, and a refused rejuvenation potion right-clicked in your pack
            // stayed in the Shared Bag, with what made room for it moved out.
            // Props (BuildStart) and creatures (SummonNPC) do hand the index to a
            // flow that re-reads it long afterwards - putting the item back under
            // them would have them consume an empty slot, so their remainder must
            // be LEFT in the Shared Bag.
            //
            // The Shared Bag is the player's (D25). Leaving someone else's goods in
            // it is a transfer, so it is allowed only when the holder is you.
            bool synchronous = data.itemType == ItemData.ItemType.Food
                || data.itemType == ItemData.ItemType.Bottle
                || data.itemType == ItemData.ItemType.Consumables;
            if (!synchronous && !mayLeaveStaged)
            {
                return false;
            }
            InventoryManager inv = mn.inventory;
            int staged = FreeSharedSlot(mn);
            bool traded = false;
            ItemSlot parked = null;
            if (staged < 0)
            {
                // THE SHARED BAG IS FULL (a right-click in your own bag did not use
                // the item, it tried to put it in - caution 34 for want of a free
                // cell). Room is made in YOUR BAG: a Shared Bag
                // item steps over into a free cell there, and for a synchronous use
                // it steps back afterwards. For the coroutine kinds it stays - both
                // bags are yours, so nothing changed hands.
                //
                // Trading places with the clicked cell was the first answer, and it
                // was wrong for a chest: a Shared Bag item landed IN the chest, swapped
                // with the full shared bag instead of going to the player's bag. A trade is kept only for a cell of your
                // own with no free cell beside it, where it moves nothing out.
                staged = TradeSlot(mn, slot.itemKey);
                if (staged < 0)
                {
                    return false;
                }
                ItemSlot room = inv.itemSlot[staged];
                if (room.itemKey == slot.itemKey)
                {
                    // every cell holds this very item: use the Shared Bag's own
                    // stack, one of the same is one of the same
                    aim(() => mn.itemMN.UseItem(staged));
                    return true;
                }
                ItemSlot free = FreeBagSlot(mn);
                if (free != null)
                {
                    inv.SlotToSlot(room, free);
                    parked = free;
                    inv.SlotToSlot(slot, room);
                }
                else if (FavouriteMoves.Yours(inv, slot))
                {
                    Swap(inv, slot, room);
                    traded = true;
                }
                else
                {
                    return false;   // no room in either of your bags
                }
            }
            else
            {
                inv.SlotToSlot(slot, inv.itemSlot[staged]);
            }
            aim(() => mn.itemMN.UseItem(staged));
            if (synchronous)
            {
                ItemSlot cell = inv.itemSlot[staged];
                if (traded)
                {
                    Swap(inv, slot, cell);   // the remainder home, the guest back
                }
                else
                {
                    if (!string.IsNullOrEmpty(cell.itemKey))
                    {
                        inv.SlotToSlot(cell, slot);
                    }
                    if (parked != null && string.IsNullOrEmpty(cell.itemKey))
                    {
                        inv.SlotToSlot(parked, cell);   // the one that made room goes back
                    }
                }
            }
            return true;
        }

        // The refusal, through the harness (it cannot right-click): the first
        // NotOnYou item in the Shared Bag is refused with the hint and keeps its
        // stack. Changes nothing; the hint shows as it would in play.
        internal static string RefusalSelfTest()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null)
            {
                return "skipped: no inventory";
            }
            for (int i = 0; i < inv.baseCount; i++)
            {
                ItemSlot s = inv.itemSlot[i];
                if (s == null || string.IsNullOrEmpty(s.itemKey) || !NotOnYou.Contains(s.itemKey))
                {
                    continue;
                }
                int stack = s.stack;
                bool refused = RefusedOnYou(mn, s);
                return (refused && s.stack == stack ? "ok: " : "FAIL: ") + s.itemKey
                    + " refused=" + refused + " stack " + stack + "->" + s.stack
                    + " target=" + (InventoryWindow.GiveName() ?? "none");
            }
            return "skipped: no love or hate potion in the Shared Bag";
        }

        // Harness door: the chest case with the Shared Bag full. One food item in a
        // cell that is not yours (a fresh drop object, as FavouriteRules' test uses)
        // is staged through Use with an aim that does NOT use it, so nothing is
        // eaten. While staged, the drop cell must hold nothing of yours and a bag
        // cell must hold what made room; afterwards both bags must be exactly as
        // found and the food back in its cell.
        internal static string SelfTest()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            CommonStates you = mn != null && mn.gameMN != null && mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null;
            ItemSlot[] bag = you != null && you.bag != null ? you.bag.slots : null;
            if (inv == null || bag == null || mn.itemMN == null || inv.hoverSlot.gameObject.activeSelf)
            {
                return "skipped: no inventory, or an item on the cursor";
            }
            if (FreeBagSlot(mn) == null)
            {
                return "skipped: your bag has no free cell";
            }
            // the Shared Bag must be full for room to be made: its free cells get a
            // placeholder stone each, taken out again at the end
            ItemData filler = mn.itemMN.FindItem("stone_01");
            var filled = new System.Collections.Generic.List<ItemSlot>();
            for (int i = FreeSharedSlot(mn); i >= 0 && filler != null; i = FreeSharedSlot(mn))
            {
                mn.itemMN.ItemToSlot(filler, inv.itemSlot[i], 1);
                filled.Add(inv.itemSlot[i]);
            }
            if (FreeSharedSlot(mn) >= 0)
            {
                return "skipped: could not fill the Shared Bag";
            }
            ItemData food = null;
            var mine = new System.Collections.Generic.List<ItemSlot>();
            for (int i = 0; i < inv.baseCount; i++) { mine.Add(inv.itemSlot[i]); }
            mine.AddRange(bag);
            foreach (ItemSlot s in mine)
            {
                ItemData d = s != null && !string.IsNullOrEmpty(s.itemKey) ? mn.itemMN.FindItem(s.itemKey) : null;
                if (food == null && d != null && d.itemType == ItemData.ItemType.Food) { food = d; }
            }
            if (food == null)
            {
                foreach (ItemSlot s in filled) { inv.SlotClear(s); }
                return "skipped: no food anywhere in your bags to copy";
            }
            var before = new System.Text.StringBuilder();
            foreach (ItemSlot s in mine) { before.Append(s.itemKey).Append('x').Append(s.stack).Append(';'); }
            ItemSlot outside = inv.MakeDropItem();
            mn.itemMN.ItemToSlot(food, outside, 1);
            int emptyBefore = 0;
            foreach (ItemSlot s in bag) { if (string.IsNullOrEmpty(s.itemKey)) { emptyBefore++; } }
            bool cleanWhileStaged = false;
            bool used = Use(mn, outside, -1, use =>
            {
                int emptyNow = 0;
                foreach (ItemSlot s in bag) { if (string.IsNullOrEmpty(s.itemKey)) { emptyNow++; } }
                cleanWhileStaged = string.IsNullOrEmpty(outside.itemKey) && InShared(inv, food.name)
                    && emptyNow == emptyBefore - 1;
            }, true);
            bool back = outside.itemKey == food.name && outside.stack == 1;
            var after = new System.Text.StringBuilder();
            foreach (ItemSlot s in mine) { after.Append(s.itemKey).Append('x').Append(s.stack).Append(';'); }
            bool same = before.ToString() == after.ToString();
            UnityEngine.Object.Destroy(outside.gameObject);
            // the placeholders out; only where they still are, so a failed
            // restore cannot clear something of yours
            foreach (ItemSlot s in filled)
            {
                if (s.itemKey == filler.name) { inv.SlotClear(s); }
            }
            InventoryWindow.Invalidate();
            return food.name + ": staged " + (used ? "ok" : "FAIL refused")
                + ", nothing of yours in the outside cell " + (cleanWhileStaged ? "ok" : "FAIL")
                + ", food back " + (back ? "ok" : "FAIL")
                + ", both bags as found " + (same ? "ok" : "FAIL");
        }

        private static bool InShared(InventoryManager inv, string key)
        {
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (inv.itemSlot[i] != null && inv.itemSlot[i].itemKey == key) { return true; }
            }
            return false;
        }

        // A free cell in your own bag. None while vanilla's panel holds a container:
        // the real array reads empty then (PickupCascade.CharacterBag).
        private static ItemSlot FreeBagSlot(ManagersScript mn)
        {
            if (VanillaContainerUI.MirrorsLive(mn.inventory) || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return null;
            }
            CommonStates you = mn.gameMN.pMove.common;
            ItemSlot[] bag = you != null && you.bag != null ? you.bag.slots : null;
            for (int i = 0; bag != null && i < bag.Length; i++)
            {
                if (bag[i] != null && string.IsNullOrEmpty(bag[i].itemKey))
                {
                    return bag[i];
                }
            }
            return null;
        }

        // Two cells trade contents through the game's own scratch cell, the
        // three moves RandomCharacter.EquipItem makes. Either may be empty:
        // SlotToSlot from an empty cell clears its destination.
        private static void Swap(InventoryManager inv, ItemSlot a, ItemSlot b)
        {
            inv.SlotToSlot(b, inv.equipSlot);
            inv.SlotToSlot(a, b);
            inv.SlotToSlot(inv.equipSlot, a);
        }

        // A Shared Bag cell to trade with: one holding a DIFFERENT item, since
        // SlotToSlot stacks like onto like instead of moving it; a favourite or a
        // hung tool only when nothing else will do, so its place is not disturbed.
        private static int TradeSlot(ManagersScript mn, string key)
        {
            InventoryManager inv = mn.inventory;
            int max = Mathf.Min(inv.baseCount, inv.itemSlot.Length);
            int any = -1, same = -1;
            for (int i = 0; i < max; i++)
            {
                ItemSlot s = inv.itemSlot[i];
                if (s == null || string.IsNullOrEmpty(s.itemKey))
                {
                    continue;
                }
                if (s.itemKey == key)
                {
                    if (same < 0) { same = i; }
                    continue;
                }
                if (!Favourites.IsFavourite(s) && !ToolBelt.IsHung(s))
                {
                    return i;
                }
                if (any < 0) { any = i; }
            }
            return any >= 0 ? any : same;
        }

        // The equip family, lifted from UseItem's own switch: the types whose leaf
        // takes an ItemSlot rather than an index, which is exactly why they stage
        // nothing.
        internal static bool IsEquipType(ItemData data)
        {
            switch (data.itemType)
            {
                case ItemData.ItemType.Tool:
                case ItemData.ItemType.Hat:
                case ItemData.ItemType.Cloth:
                case ItemData.ItemType.ClothU:
                case ItemData.ItemType.Acce:
                case ItemData.ItemType.Shoes:
                case ItemData.ItemType.Tool2:
                case ItemData.ItemType.Shield:
                case ItemData.ItemType.Support:
                case ItemData.ItemType.Hip:
                    return true;
                default:
                    return false;
            }
        }

        // UseItem's switch has no case for these, so a press would look identical
        // to a broken one.
        internal static bool HasAction(ItemData data)
        {
            switch (data.itemType)
            {
                case ItemData.ItemType.Item:
                case ItemData.ItemType.ResourceW:
                    return false;
                default:
                    return true;
            }
        }

        private static int FreeSharedSlot(ManagersScript mn)
        {
            InventoryManager inv = mn.inventory;
            int max = Mathf.Min(inv.baseCount, inv.itemSlot.Length);
            for (int i = 0; i < max; i++)
            {
                if (inv.itemSlot[i] != null && string.IsNullOrEmpty(inv.itemSlot[i].itemKey))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
