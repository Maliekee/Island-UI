using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // The cascade's first call site: where picked-up loot goes when the Shared Bag is full.
    //
    // The destination rule was deferred on purpose — "an automatic destination is a
    // rule you cannot see and cannot undo" — with exactly one question left open:
    // whether a Focus (the container you are standing at) should outrank your own
    // bag. Answered from play, which is how §9.4 said it should be answered: for a
    // PICKUP it should not. Picking something up is not "put this away", and the
    // intuitive destination stays your own pack however many chests are in reach.
    // The Focus keeps its claim on the senders that DO mean "put this away", which
    // is why this is one call site rather than a shared helper yet.
    //
    // Scope was once the losing branch only: vanilla tops up Shared Bag
    // stacks and takes the first free Shared Bag slot, and we stepped in only when
    // there was none. That split a pickup off from a stack of the same item in your
    // bag (20 berries in the player bag, and new berries looted into the shared
    // bag as a separate stack). So the whole placement is ours now: stacks
    // in both bags, then empty cells in both, the Shared Bag first each time.
    //
    // TWO call sites, because vanilla has two placement routines and they lose
    // differently (the second was MISSED at first, found in play as a full
    // shared bag whose overflow never reached the player bag):
    //
    //   GetItem(ItemData, int)      — an item that does not exist in the world yet:
    //                                 crafting output, GetDrop's harvest yields
    //                                 (ItemManager.cs:339 just loops into GetItem,
    //                                 so that whole path comes free). Loses by
    //                                 dropping through stockSlot with caution 5.
    //   ItemDropToSlot(ItemSlot)    — an item that IS in the world, as a live slot
    //                                 object you walked over. Loses by cautioning
    //                                 and simply not taking it, leaving it lying
    //                                 there. Nothing is dropped, so the bug reads
    //                                 as "pickup does nothing" rather than as loss.
    //
    // The second carries INSTANCE data — attack, itemLife, common, itemColor, hsb,
    // which vanilla moves with ItemDataTransfer (:327). A cascade that used plain
    // ItemToSlot would silently reset a picked-up weapon's stats, so the empty-slot
    // step takes the source slot and repeats that transfer.
    [HarmonyPatch(typeof(ItemManager))]
    internal static class PickupCascade
    {
        private static readonly AccessTools.FieldRef<ItemManager, ManagersScript> ItemMn =
            AccessTools.FieldRefAccess<ItemManager, ManagersScript>("mn");

        [HarmonyPatch(nameof(ItemManager.GetItem))]
        [HarmonyPrefix]
        private static bool GetItem_Prefix(ItemManager __instance, ItemData tmpItem, ref int count)
        {
            try
            {
                return Cascade(__instance, tmpItem, ref count);
            }
            catch (System.Exception e)
            {
                // picking things up is a core verb; degrade to vanilla, never break it
                Plugin.Log.LogWarning($"pickup cascade skipped — {e}");
                return true;
            }
        }

        [HarmonyPatch(nameof(ItemManager.ItemDropToSlot))]
        [HarmonyPrefix]
        private static bool ItemDropToSlot_Prefix(ItemManager __instance, ItemSlot tmpItem)
        {
            try
            {
                return CascadeWorldItem(__instance, tmpItem);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"world-pickup cascade skipped — {e}");
                return true;
            }
        }

        // Vanilla's losing branch here (ItemManager.cs:321) cautions and returns
        // with the item still lying in the world — so unlike GetItem nothing is
        // destroyed and there is no urgency about correctness of the remainder;
        // whatever we do not take simply stays where it was.
        private static bool CascadeWorldItem(ItemManager itemMN, ItemSlot tmpItem)
        {
            if (!Settings.CascadeToCharacterBag.Value || tmpItem == null || tmpItem.stack <= 0)
            {
                return true;
            }
            ManagersScript mn = ItemMn(itemMN);
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null)
            {
                return true;
            }
            // vanilla refuses an oversized carry item outright (:318). Cascading one
            // into the pack would smuggle it past a gate that exists on purpose.
            if (mn.npcMN != null && mn.npcMN.CheckCarrySize(tmpItem.itemKey)
                && !mn.npcMN.CarriableCheck())
            {
                return true;
            }
            ItemData data = mn.itemMN != null ? mn.itemMN.FindItem(tmpItem.itemKey) : null;
            ItemSlot[] bag = data != null ? CharacterBag(mn, inv) : null;
            if (bag == null)
            {
                return true;
            }

            // LIKE WITH LIKE ACROSS BOTH BAGS (with 20 berries in the player bag
            // and nothing in the shared bag, new berries went into the shared bag
            // as a separate stack). Every stack of
            // the item fills first, the Shared Bag's then yours, and only then an
            // empty cell, again the Shared Bag's before yours - the order a chest's
            // All out already keeps.
            int wanted = tmpItem.stack;
            int count = TopUp(inv.itemSlot, inv.baseCount, data, wanted, inv);
            count = TopUp(bag, bag.Length, data, count, null);
            count = IntoEmpty(inv.itemSlot, inv.baseCount, itemMN, data, count, tmpItem, inv);
            count = IntoEmpty(bag, bag.Length, itemMN, data, count, tmpItem);

            int placed = wanted - count;
            if (placed <= 0)
            {
                return true;   // nothing moved: let vanilla caution exactly as before
            }
            tmpItem.stack = count;
            InventoryWindow.Invalidate();
            itemMN.StartCoroutine(mn.uiMN.ItemGetView(data, placed));
            if (count > 0)
            {
                // took some. Vanilla runs on, finds the stacks full and no free
                // slot, and cautions for a remainder that stays in the world — with
                // its own pickup sound, which is why we do not play one here.
                return true;
            }
            // took all of it, so finish vanilla's job: the sound it opens with, the
            // world object it ends by destroying, and the carry recount between
            if (mn.sound != null && mn.gameMN != null && mn.gameMN.player != null)
            {
                mn.sound.GoSound(4, mn.gameMN.player.transform.position);
            }
            if (mn.npcMN != null)
            {
                mn.npcMN.GetCarryCount();
            }
            Object.Destroy(tmpItem.gameObject);
            return false;
        }

        private static bool Cascade(ItemManager itemMN, ItemData tmpItem, ref int count)
        {
            if (!Settings.CascadeToCharacterBag.Value || tmpItem == null || count <= 0)
            {
                return true;
            }
            ManagersScript mn = ItemMn(itemMN);
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null)
            {
                return true;
            }
            ItemSlot[] bag = CharacterBag(mn, inv);
            if (bag == null)
            {
                return true;   // no bag to cascade into; vanilla drops as it always did
            }

            int wanted = count;
            // like with like across both bags first, Shared Bag before yours, then
            // the empty cells in the same order (CascadeWorldItem says why)
            count = TopUp(inv.itemSlot, inv.baseCount, tmpItem, count, inv);
            count = TopUp(bag, bag.Length, tmpItem, count, null);
            count = IntoEmpty(inv.itemSlot, inv.baseCount, itemMN, tmpItem, count, null, inv);
            count = IntoEmpty(bag, bag.Length, itemMN, tmpItem, count);

            int placed = wanted - count;
            if (placed > 0)
            {
                InventoryWindow.Invalidate();
                itemMN.StartCoroutine(mn.uiMN.ItemGetView(tmpItem, placed));
            }
            // Whatever would not fit goes back to vanilla with the count reduced: it
            // finds the stacks full and no empty slot, and drops exactly that
            // remainder with caution 5, as it always would have.
            return count > 0;
        }

        // Harness door: the berries case. One of an item that sits in your bag with
        // room on its stack and nowhere in the Shared Bag is picked up through the
        // real GetItem, with a Shared Bag cell free (one item parked in the game's
        // scratch cell if none is); it must land on the bag's stack. Everything is
        // put back as it was found.
        internal static string SelfTest()
        {
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            ItemSlot[] bag = inv != null ? CharacterBag(mn, inv) : null;
            if (bag == null || mn.itemMN == null || inv.equipSlot == null || !string.IsNullOrEmpty(inv.equipSlot.itemKey))
            {
                return "skipped: no bag, or the scratch cell is busy";
            }
            ItemSlot stack = null;
            ItemData data = null;
            for (int i = 0; stack == null && i < bag.Length; i++)
            {
                ItemSlot s = bag[i];
                ItemData d = s != null && !string.IsNullOrEmpty(s.itemKey) ? mn.itemMN.FindItem(s.itemKey) : null;
                if (d != null && d.maxStack > 1 && s.stack < d.maxStack && !InShared(inv, s.itemKey))
                {
                    stack = s;
                    data = d;
                }
            }
            if (stack == null)
            {
                return "skipped: nothing in your bag with room on its stack that the Shared Bag lacks";
            }
            ItemSlot parked = null;
            if (inv.EmptySlotCheck() == -1)
            {
                for (int i = 0; parked == null && i < inv.baseCount; i++)
                {
                    ItemSlot s = inv.itemSlot[i];
                    if (!Favourites.IsFavourite(s) && !ToolBelt.IsHung(s)) { parked = s; }
                }
                if (parked == null)
                {
                    return "skipped: no Shared Bag cell could be freed";
                }
                inv.SlotToSlot(parked, inv.equipSlot);
            }
            int before = stack.stack;
            mn.itemMN.GetItem(data, 1);
            bool onStack = stack.stack == before + 1;
            bool split = InShared(inv, data.name);
            // undo: the one taken back off wherever it went, the parked item home
            if (onStack) { stack.stack = before; }
            for (int i = 0; split && i < inv.baseCount; i++)
            {
                if (inv.itemSlot[i].itemKey == data.name) { inv.SlotClear(inv.itemSlot[i]); }
            }
            if (parked != null) { inv.SlotToSlot(inv.equipSlot, parked); }
            InventoryWindow.Invalidate();
            return data.name + " x" + before + " in your bag, Shared Bag " + (parked != null ? "freed" : "had room")
                + ": " + (onStack && !split ? "stacked in your bag ok" : split ? "FAIL split into the Shared Bag" : "FAIL went elsewhere");
        }

        private static bool InShared(InventoryManager inv, string key)
        {
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (inv.itemSlot[i] != null && inv.itemSlot[i].itemKey == key) { return true; }
            }
            return false;
        }

        // The pack's real slots. Skipped while a VANILLA container UI owns a
        // container: it moves contents into mirror slots 50+ and the real array
        // reads empty, so we would be filling a bag the game is holding elsewhere.
        internal static ItemSlot[] CharacterBag(ManagersScript mn, InventoryManager inv)
        {
            if (VanillaContainerUI.MirrorsLive(inv))
            {
                return null;
            }
            CommonStates player = mn.gameMN != null && mn.gameMN.pMove != null
                ? mn.gameMN.pMove.common : null;
            return player != null && player.bag != null ? player.bag.slots : null;
        }

        // Returns what is left over. `ui` non-null means these are vanilla UI slots
        // whose on-screen number has to be refreshed.
        private static int TopUp(ItemSlot[] slots, int limit, ItemData tmpItem, int count,
            InventoryManager ui)
        {
            if (slots == null)
            {
                return count;
            }
            for (int i = 0; i < limit && i < slots.Length && count > 0; i++)
            {
                ItemSlot slot = slots[i];
                if (slot == null || slot.itemKey != tmpItem.name || slot.stack >= tmpItem.maxStack)
                {
                    continue;
                }
                int room = tmpItem.maxStack - slot.stack;
                int moved = count < room ? count : room;
                slot.stack += moved;
                count -= moved;
                if (ui != null)
                {
                    ui.StackTextChange(i);
                }
            }
            return count;
        }

        // `source` non-null means the item came from a live world slot carrying
        // instance data — durability, rolled attack, colour. Vanilla moves it with
        // ItemDataTransfer (ItemManager.cs:327) and so must we, or a picked-up
        // weapon quietly becomes a factory-fresh one on the way into the pack.
        // `ui` non-null means these are the Shared Bag's cells: empty is vanilla's
        // own test there (EmptySlotCheck reads stack == 0) and the number is redrawn.
        private static int IntoEmpty(ItemSlot[] slots, int limit, ItemManager itemMN, ItemData tmpItem,
            int count, ItemSlot source = null, InventoryManager ui = null)
        {
            for (int i = 0; i < limit && i < slots.Length && count > 0; i++)
            {
                bool empty = slots[i] != null
                    && (ui != null ? slots[i].stack == 0 : string.IsNullOrEmpty(slots[i].itemKey));
                if (!empty)
                {
                    continue;
                }
                // one stack at a time. Vanilla's own free-slot placement writes the
                // whole count into a single slot and will happily exceed maxStack;
                // there is no reason to reproduce that where we are already looping.
                int moved = count < tmpItem.maxStack ? count : tmpItem.maxStack;
                itemMN.ItemToSlot(tmpItem, slots[i], moved);
                if (source != null)
                {
                    itemMN.ItemDataTransfer(source, slots[i]);
                }
                if (ui != null)
                {
                    ui.StackTextChange(i);
                }
                count -= moved;
            }
            return count;
        }
    }

    // The pickup routines PickupCascade cannot reach (carrying a corpse with
    // the shared inventory full did not fall through to the player inventory
    // as other drops do). NPCManager.Carry places
    // the person itself: EmptySlotCheck() on the Shared Bag, caution 2 and out if
    // there is none, and otherwise a body of work that is all vanilla's - the
    // friend and follower bookkeeping, the sex-place release, the item's `common`.
    // NPCManager.GetNPC (a downed animal) and GameManager.PickUpParts (a severed
    // part) place the same way, and get the same treatment.
    //
    // So the placement is lent rather than rewritten. With the Shared Bag full and
    // a free cell in your bag, one Shared Bag cell's item is PARKED in that free
    // cell, vanilla carries into the cell it just freed, and the two are swapped
    // back: the person in your bag, the parked item home in the cell it never
    // really left. The swap back runs whatever Carry did - an early return leaves
    // the freed cell empty, and the same swap puts the parked item back into it.
    //
    // A cell with a favourite or a tool-board mark is never the one parked: marks
    // belong to the slot object, and it would carry a mark to the wrong item.
    //
    // Vanilla's carry cap and its weight debuff count only the Shared Bag
    // (GetCarryCount). A body in your bag is still carried, so the count includes
    // it - otherwise the pack would be a way round the cap.
    [HarmonyPatch(typeof(NPCManager))]
    internal static class CarryCascade
    {
        private static readonly AccessTools.FieldRef<NPCManager, ManagersScript> NpcMn =
            AccessTools.FieldRefAccess<NPCManager, ManagersScript>("mn");

        private static readonly AccessTools.FieldRef<GameManager, ManagersScript> GameMn =
            AccessTools.FieldRefAccess<GameManager, ManagersScript>("mn");

        // Carrying a person or a body (state 0; the other states place nothing)
        [HarmonyPatch(nameof(NPCManager.Carry))]
        [HarmonyPrefix]
        private static void Carry_Prefix(NPCManager __instance, int state, out ItemSlot[] __state)
        {
            __state = state == 0 ? Lend(NpcMn(__instance)) : null;
        }

        [HarmonyPatch(nameof(NPCManager.Carry))]
        [HarmonyFinalizer]
        private static void Carry_Finalizer(NPCManager __instance, ItemSlot[] __state)
        {
            GiveBack(NpcMn(__instance), __state);
        }

        // Picking up a downed animal (a carried deer did not reach the player
        // inventory either): the same placement again,
        // EmptySlotCheck() and caution 2
        [HarmonyPatch(nameof(NPCManager.GetNPC))]
        [HarmonyPrefix]
        private static void GetNPC_Prefix(NPCManager __instance, out ItemSlot[] __state)
        {
            __state = Lend(NpcMn(__instance));
        }

        [HarmonyPatch(nameof(NPCManager.GetNPC))]
        [HarmonyFinalizer]
        private static void GetNPC_Finalizer(NPCManager __instance, ItemSlot[] __state)
        {
            GiveBack(NpcMn(__instance), __state);
        }

        // ...and a severed part picked up off the ground (PickUpItem hands "parts_*"
        // here, before the GetItem that PickupCascade covers)
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PickUpParts))]
        [HarmonyPrefix]
        private static void PickUpParts_Prefix(GameManager __instance, out ItemSlot[] __state)
        {
            __state = Lend(GameMn(__instance));
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PickUpParts))]
        [HarmonyFinalizer]
        private static void PickUpParts_Finalizer(GameManager __instance, ItemSlot[] __state)
        {
            GiveBack(GameMn(__instance), __state);
        }

        // With the Shared Bag full and a free cell in your bag: park one Shared Bag
        // item in that cell, so the routine finds a free Shared Bag cell. Returns
        // {freed cell, your cell}, or null to leave vanilla to caution.
        private static ItemSlot[] Lend(ManagersScript mn)
        {
            try
            {
                InventoryManager inv = mn != null ? mn.inventory : null;
                if (!Settings.CascadeToCharacterBag.Value || inv == null || inv.EmptySlotCheck() != -1)
                {
                    return null;
                }
                ItemSlot[] bag = PickupCascade.CharacterBag(mn, inv);
                ItemSlot free = null;
                for (int i = 0; bag != null && free == null && i < bag.Length; i++)
                {
                    if (bag[i] != null && string.IsNullOrEmpty(bag[i].itemKey)) { free = bag[i]; }
                }
                ItemSlot parked = null;
                for (int i = 0; free != null && parked == null && i < inv.baseCount; i++)
                {
                    ItemSlot s = inv.itemSlot[i];
                    if (s != null && !Favourites.IsFavourite(s) && !ToolBelt.IsHung(s)) { parked = s; }
                }
                if (parked == null)
                {
                    return null;   // nowhere to put it: vanilla cautions as it always did
                }
                Swap(parked, free);
                return new[] { parked, free };
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"carry cascade skipped — {e}");
                return null;
            }
        }

        // Whatever the routine did: what it placed goes to your cell, the parked
        // item back home - or, if it placed nothing, the parked item simply home.
        private static void GiveBack(ManagersScript mn, ItemSlot[] lent)
        {
            if (lent == null)
            {
                return;
            }
            Swap(lent[0], lent[1]);
            Redraw(mn, lent[0]);
            if (mn != null && mn.npcMN != null)
            {
                mn.npcMN.CarryWeightCheck();
            }
            InventoryWindow.Invalidate();
        }

        [HarmonyPatch(nameof(NPCManager.GetCarryCount))]
        [HarmonyPostfix]
        private static void GetCarryCount_Postfix(NPCManager __instance, ref int __result)
        {
            ManagersScript mn = NpcMn(__instance);
            InventoryManager inv = mn != null ? mn.inventory : null;
            ItemSlot[] bag = inv != null ? PickupCascade.CharacterBag(mn, inv) : null;
            int inBag = 0;
            for (int i = 0; bag != null && i < bag.Length; i++)
            {
                if (bag[i] != null && __instance.CheckCarrySize(bag[i].itemKey)) { inBag++; }
            }
            if (inBag == 0)
            {
                return;
            }
            __result += inBag;
            // vanilla has already written its own count into the counter
            Transform text = GameObject.Find("MainCanvas")?.transform.Find("carryCapaText");
            TMPro.TextMeshProUGUI label = text != null ? text.GetComponent<TMPro.TextMeshProUGUI>() : null;
            if (label != null)
            {
                label.text = __result.ToString("") + "/" + __instance.CarryMAX().ToString("");
            }
        }

        // every field SlotLoadSlot copies, and `common` unconditionally: SlotLoadSlot
        // leaves the target's person in place when the source has none
        private static void Swap(ItemSlot a, ItemSlot b)
        {
            Sprite image = a.itemImage; a.itemImage = b.itemImage; b.itemImage = image;
            string key = a.itemKey; a.itemKey = b.itemKey; b.itemKey = key;
            ItemData.ToolType tool = a.toolType; a.toolType = b.toolType; b.toolType = tool;
            float attack = a.attack; a.attack = b.attack; b.attack = attack;
            float life = a.itemLife; a.itemLife = b.itemLife; b.itemLife = life;
            int stack = a.stack; a.stack = b.stack; b.stack = stack;
            Color32 color = a.itemColor; a.itemColor = b.itemColor; b.itemColor = color;
            float[] hsb = a.hsb; a.hsb = b.hsb; b.hsb = hsb;
            CommonStates common = a.common; a.common = b.common; b.common = common;
        }

        // the Shared Bag cell is a vanilla UI slot: its picture and number are
        // drawn from these fields only when something asks
        private static void Redraw(ManagersScript mn, ItemSlot slot)
        {
            Transform image = slot.transform.Find("itemImage");
            if (image != null && mn != null && mn.itemMN != null)
            {
                image.GetComponent<UnityEngine.UI.Image>().overrideSprite =
                    string.IsNullOrEmpty(slot.itemKey) ? mn.itemMN.emptySprite : slot.itemImage;
            }
            if (mn != null && mn.inventory != null)
            {
                mn.inventory.StackTextChange(slot.slotID);
            }
        }
    }
}
