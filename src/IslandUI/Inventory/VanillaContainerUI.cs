using System.Collections;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // WI-4: the vanilla container UI, replaced cold turkey (recon
    // docs/inventory-window-ux.md §7.2).
    //
    // The vanilla panel does not DISPLAY a container, it MOVES one.
    // SubInventoryLoad empties a container's real slots into the on-screen mirror
    // slots 50+, and SubInventoryUnLoad writes them back when the panel closes.
    // Our Inventory reads and writes the REAL slots, so the two cannot both be
    // live: for anything we draw, the mirroring must simply never run.
    //
    // SCOPED, NOT BLANKET. Only PLAIN STORAGE is intercepted — character bags and
    // ordinary chests, material storages and treasure. Racks, poles, planters,
    // traps, breeding props and the Stock family drive their gameplay THROUGH the
    // mirror grid: for them the panel is the machine, not a view of it, so they
    // keep the vanilla path and the vanilla panel.
    //
    // The panel GameObject stays ACTIVE but empty and invisible. Three interaction
    // coroutines (PlayerMove.CheckNPC / SearchNPC / UseProp) use
    // subInventory.activeSelf as their "the menu is still open" condition;
    // deactivating it closes the NPC dialog on the very next frame. It is a
    // lifetime token now, nothing more — InventoryWindow hides it visually.
    //
    // The bare [HarmonyPatch] is load-bearing: PatchAll skips any class without a
    // CLASS-level Harmony attribute, however many method-level ones it carries.
    // The patches simply never apply, silently. It is bare rather than typed
    // because this class patches both InventoryManager and ItemManager.
    [HarmonyPatch]
    internal static class VanillaContainerUI
    {
        // ItemManager.mn and NPCManager.mn are private; everything else is public
        private static readonly AccessTools.FieldRef<ItemManager, ManagersScript> ItemMn =
            AccessTools.FieldRefAccess<ItemManager, ManagersScript>("mn");
        private static readonly AccessTools.FieldRef<NPCManager, ManagersScript> NpcMn =
            AccessTools.FieldRefAccess<NPCManager, ManagersScript>("mn");
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> InvMn =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        // The container whose mirroring we skipped, i.e. the one the panel is
        // holding open as a token while WE own its slots. Null means the vanilla
        // panel owns whatever is loaded (or nothing is).
        internal static InventorySlot Intercepted;

        private static GameObject _equipIcon;
        private static Canvas _iconCanvas;
        // the container handed back to vanilla for the duration of a Vacuum
        private static InventorySlot _lentToVanilla;
        private static bool _bypass;

        // ---------- what everyone else needs to ask ----------

        // True when the VANILLA panel owns the open container — its contents
        // really are in mirror slots 50+ and the real array reads empty.
        // Every test that used to be `subInventory.activeSelf` for that meaning
        // must ask this instead: under interception the panel is active and empty.
        internal static bool MirrorsLive(InventoryManager inv)
        {
            return inv != null && inv.subInventory != null && inv.subInventory.activeSelf
                && Intercepted == null;
        }

        // Plain storage is what we replace. Everything else keeps vanilla's panel.
        internal static bool IsPlainStorage(InventorySlot container)
        {
            if (container == null)
            {
                return false;
            }
            // a character's bag — always plain storage, whoever owns it. This is
            // vanilla's own test for "this container belongs to somebody"
            // (InventoryManager.cs:905).
            if (container.transform.parent != null
                && container.transform.parent.GetComponent<CommonStates>() != null)
            {
                return true;
            }
            // Traps, planters, beehives and the rest are Type.None exactly like a
            // chest, but BreedInventory POLLS the mirror slots to notice what was
            // planted or caught. The grid WAS the mechanism there, so they stayed
            // on the vanilla path — type alone would not have separated them.
            //
            // NO LONGER EXCLUDED. The exclusion was never about breed
            // props being unfit to draw; it was that taking the panel away took
            // the mechanism with it, and we had nothing to put back. D39 is that
            // something: a write from our window calls the same *Check the poll
            // would have called, so the grid has stopped being the only way to
            // drive them. Skipping SubInventoryLoad also means BreedInventory
            // never starts, so there is no second watcher to fire twice.
            //
            // Nothing is lost with the panel, which is the other half of the
            // argument. For Type.None props UIManager's interact switch falls to
            // `default → propPanel.SetActive(false)` (UIManager.cs:1928), so a
            // cage has no prop panel, no SubInventoryTextView, none of the extra
            // widgets Restrain and Milk and Toilet hang off theirs. It is a plain
            // grid with All in / Stack / All out.
            // A crafting bench's storage strip. Vanilla moves it into the
            // mirror slots and CONSUMES from there — `CraftItem` and
            // `CraftableCheckSlot` both take a `craftPanelID != 0` branch that
            // reads `itemSlot[50 + j]` — so intercepting while that path is live
            // would craft from an empty grid. It is not live: the bench redirect turns every
            // `CraftOpen(id != 0)` into the whole menu's `CraftOpen(0)`, so craftPanelID is
            // always 0 and vanilla's bench branch never runs. That holds only while
            // the custom grid is on, which is exactly this condition — with it off
            // the vanilla panels are back and the mirror is load-bearing again.
            if (container.type == InventorySlot.Type.Bench)
            {
                return Settings.CustomGrid.Value;
            }
            if (container.type == InventorySlot.Type.Treasure)
            {
                // A locked chest refuses, and a collection chest hands its contents
                // straight to the collection instead of ever showing a grid. Both
                // are answers vanilla gives before a panel exists, so leave them to
                // it — only an ordinary loot treasure becomes storage we draw.
                var treasure = container.GetComponent<TreasureScript>();
                return treasure != null && treasure.unlocked && !treasure.isCollection;
            }
            // A CLOSET ("Spider Closet" opened vanilla's grid). Type.Edit,
            // which the interact switch reserves for the mirror, the surgery bed
            // and the two closets - and only the closets reach SubInventoryLoad
            // (UIManager.cs:2131, by item key). The dress-up behind the "Outfit"
            // action is RandomCharacter.EditApparel, which works the apparel
            // slots on the CHARACTER and never reads the closet's own; those
            // twenty slots are plain storage with a launcher standing in front.
            if (container.type == InventorySlot.Type.Edit)
            {
                return ContainerPolicy.IsCloset(container);
            }
            // THE PIG FARM. Type.FactoryBreed, and it was
            // refused for no reason that survives reading the code: nothing polls
            // its mirror. UIManager's interact switch lights ONE prop act and
            // starts GameManager.FactoryBreedUse, which does nothing but display
            // "life/100" and update it while the panel is open. The grid beside
            // that readout is an ordinary bag.
            //
            // So it is admitted the same way a chest is, and the readout is not
            // ours to disturb - we never write ItemInfo.life.
            //
            // THE FLESH COLLECTOR (it opened vanilla's grid). Filed with
            // the Well and the Food Box as a prop, but it is a bag with a launcher
            // in front, the pig farm's shape. Its one act (state 11) runs
            // DeathManager.CollectFlesh, which drags bodies in range into it through
            // ItemToChest - interception-aware below - and nothing reads its mirror.
            return container.type == InventorySlot.Type.None
                || container.type == InventorySlot.Type.Material
                || container.type == InventorySlot.Type.FactoryBreed
                || container.type == InventorySlot.Type.FleshCollector;
        }

        // ---------- the entry points (recon §7.2) ----------

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SubInventoryLoad))]
        [HarmonyPrefix]
        private static bool SubInventoryLoad_Prefix(InventoryManager __instance,
            InventorySlot chest, ItemData tmpData, bool subInventoryActive)
        {
            // Not ours: hand it back to vanilla AND forget any previous
            // interception — from here on the mirror slots are real again.
            if (_bypass || chest == null || !subInventoryActive
                || __instance.lockSubInventory || !IsPlainStorage(chest))
            {
                Intercepted = null;
                return true;
            }
            if (__instance.subInventory == null)
            {
                Intercepted = null;
                return true;
            }

            // A non-intercepted container may still be loaded (a rack left open
            // when a chest is clicked). Write it back BEFORE re-pointing
            // tmpSubInventory — vanilla does it after, which pours the old
            // container's contents into the new one. Nothing depends on that bug.
            // The claim is dropped first so the unload is allowed to run at all;
            // an intercepted container never leaves anything in the mirror, so
            // this loop and a live claim are mutually exclusive anyway.
            Intercepted = null;
            for (int i = 0; i < __instance.subCount && 50 + i < __instance.itemSlot.Length; i++)
            {
                if (!string.IsNullOrEmpty(__instance.itemSlot[50 + i].itemKey))
                {
                    __instance.SubInventoryUnLoad();
                    break;
                }
            }

            __instance.tmpSubInventory = chest;

            // A treasure chest generates its loot on first open, into the REAL slots
            // — exactly where we want it. Locked and collection chests never get
            // here; IsPlainStorage leaves those to vanilla.
            if (chest.type == InventorySlot.Type.Treasure)
            {
                var treasure = chest.GetComponent<TreasureScript>();
                if (treasure != null)
                {
                    treasure.TreasureSet();
                }
            }

            if (!__instance.subInventory.activeSelf)
            {
                __instance.SubInventoryVisible();
            }
            // SubInventoryVisible refuses while locked; without an active panel the
            // interaction coroutines would end immediately, so don't claim one.
            if (!__instance.subInventory.activeSelf)
            {
                Intercepted = null;
                return false;
            }
            Intercepted = chest;
            OpenLeftPanel(__instance, chest, tmpData);
            return false;
        }

        // Vanilla ends a plain-storage load with CraftOpen(0): the left panel
        // returns to the innate recipe list, and the sweep re-counts materials now
        // that another container is in play.
        //
        // A BENCH instead opens ITS panel, chosen by a switch on the bench's item
        // key. We have to run that switch ourselves now, and
        // AggregatedCrafting.BenchPanels already mirrors it entry for entry — the
        // bench redirect then turns whatever we ask for into the whole menu, so the
        // browser comes up at that bench's section. A key the switch does not list
        // opens NOTHING, which is vanilla falling out of its own switch rather
        // than an oversight worth correcting.
        private static void OpenLeftPanel(InventoryManager inv, InventorySlot chest, ItemData tmpData)
        {
            if (chest.type != InventorySlot.Type.Bench || tmpData == null)
            {
                inv.CraftOpen(0);
                return;
            }
            int panel = AggregatedCrafting.PanelOf(tmpData.name);
            if (panel == 0)
            {
                return;
            }
            // the one bench vanilla raises the panel for before filling it
            if (tmpData.name == "bench_surround")
            {
                ManagersScript mn = InvMn(inv);
                if (mn != null && mn.uiMN != null && mn.uiMN.craftPanel != null
                    && !mn.uiMN.craftPanel.activeSelf)
                {
                    mn.uiMN.LeftPanelState(1);
                }
            }
            inv.CraftOpen(panel);
        }

        // Writing back is the dangerous half: the mirror slots are EMPTY under
        // interception, and SlotToSlot clears the destination when its source is
        // empty — an unload would wipe the container it claims to be saving.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SubInventoryUnLoad))]
        [HarmonyPrefix]
        private static bool SubInventoryUnLoad_Prefix()
        {
            return Intercepted == null;
        }

        // Closing the panel ends the interception; the next load decides afresh.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SubInventoryVisible))]
        [HarmonyPostfix]
        private static void SubInventoryVisible_Postfix(InventoryManager __instance)
        {
            if (__instance.subInventory != null && !__instance.subInventory.activeSelf)
            {
                ReleaseClaim(__instance);
            }
        }

        // Letting go of a BENCH changes what crafting can pay for, and the
        // CraftOpen(0) vanilla runs on close happens INSIDE the call this
        // postfixes — while the claim is still live, so that rebuild still counts
        // the bench. Re-sweep after letting go, or recipes the bench funded stay
        // lit once you have walked away from it.
        //
        // The claim cannot simply be dropped EARLIER instead: SubInventoryUnLoad
        // is suppressed by the claim, and an unload with an empty mirror writes
        // that emptiness into the container.
        private static void ReleaseClaim(InventoryManager inv)
        {
            bool wasBench = Intercepted != null
                && Intercepted.type == InventorySlot.Type.Bench;
            Intercepted = null;
            if (wasBench && inv != null)
            {
                inv.CraftableCheck();
            }
        }

        // ---------- the two icons, merged ----------

        // "Open my bag" is implemented as "talk to yourself" (§8.2): I sets the
        // player as its own NPC target and loads their bag into the panel. Both
        // the I key (GameManager.cs:471) and the bag icon land here, so one prefix
        // takes over both — and the self-conversation stops happening at all.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.BackPackButton))]
        [HarmonyPrefix]
        private static bool BackPackButton_Prefix()
        {
            InventoryWindow.Toggle();
            return false;
        }

        // The sword-and-shield icon is wired only in the scene and never called
        // from code. Its panel is our Loadout plate now, so the icon is hidden and
        // the callback folded into the bag icon's job.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.EquipVisible))]
        [HarmonyPrefix]
        private static bool EquipVisible_Prefix()
        {
            InventoryWindow.Toggle();
            return false;
        }

        // ---------- pocket_01 (§7.3) ----------

        // Both vanilla branches (ItemManager.cs:924-960) are gated on the vanilla
        // panel: the NPC branch needs `subInventory.activeSelf && menuNPC`, the
        // chest branch needs `buildMN.tmpProp && tmpSubInventory`. With the panel
        // gone the item would become a silent no-op — no effect, no caution, no
        // consumption — so the effect is re-implemented here against an
        // unambiguous target instead of inheriting a hidden precondition.
        internal struct PocketTarget
        {
            public InventorySlot Container;
            public CommonStates Owner;    // set when the container is a bag
            public string Name;           // how the hover readout names it
            public bool Expandable;       // false = the click would do nothing
            public bool Capped;           // already at the 50-slot ceiling
        }

        // the chest a pocket_01 was dropped on, for the length of that use
        internal static InventorySlot PocketDrop;

        internal static PocketTarget ResolvePocketTarget(ManagersScript mn)
        {
            var result = default(PocketTarget);
            if (mn == null || mn.gameMN == null)
            {
                return result;
            }
            // D141: a plain use expands YOUR bag; dropped on someone it expands
            // theirs, dropped on a chest's window that chest (PocketDrop)
            object focus = (object)ItemUse.Recipient ?? PocketDrop;
            if (focus is CommonStates npc && npc.bag != null)
            {
                result.Container = npc.bag;
                result.Owner = npc;
                result.Name = string.IsNullOrEmpty(npc.charaName) ? Shared.Loc.Get("inv.pocket.their_bag", "their Bag")
                    : Shared.Loc.Get("inv.pocket.bag_of", "{0}'s Bag", npc.charaName);
            }
            else if (focus is InventorySlot container)
            {
                result.Container = container;
                var info = container.GetComponent<ItemInfo>();
                ItemData data = info != null ? mn.itemMN.FindItem(info.itemKey) : null;
                result.Name = data != null ? data.GetItemName() : Shared.Loc.Get("inv.pocket.that_container", "that container");
                // vanilla only ever grew real chests, never material storages or
                // treasure — keep that, an expanding storage is a different feature
                if (info == null || !mn.itemMN.IsChest(info.itemKey))
                {
                    result.Expandable = false;
                    return result;
                }
            }
            else
            {
                CommonStates player = mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null;
                if (player == null || player.bag == null)
                {
                    return result;
                }
                result.Container = player.bag;
                result.Owner = player;
                result.Name = Shared.Loc.Get("inv.pocket.your_bag", "your Bag");
            }

            InventorySlot target = result.Container;
            // bagSlot 0 means the character has no bag at all (recon §7.3)
            result.Expandable = target != null && target.size > 0 && target.slots != null;
            result.Capped = result.Expandable && target.slots.Length >= 50;
            return result;
        }

        private static bool TryExpand(PocketTarget target)
        {
            if (!target.Expandable || target.Capped)
            {
                return false;
            }
            InventorySlot container = target.Container;
            int before = container.slots.Length;
            container.gameObject.AddComponent<ItemSlot>();
            container.slots = container.gameObject.GetComponents<ItemSlot>();
            // Re-reading the components is vanilla's own idiom, and it is the
            // array's truth — size must follow it rather than be incremented
            // independently, or the grid and the save would disagree.
            container.size = container.slots.Length;
            if (container.size != before + 1)
            {
                Plugin.Log.LogInfo($"pocket_01: '{container.gameObject.name}' went {before} -> "
                    + $"{container.size} slots (expected {before + 1}) — extra ItemSlot components?");
            }
            return true;
        }

        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.UseConsumables))]
        [HarmonyPrefix]
        private static bool UseConsumables_Prefix(ItemManager __instance, int slotID,
            ref IEnumerator __result)
        {
            if (slotID < 0)
            {
                return true;
            }
            ManagersScript mn = ItemMn(__instance);
            if (mn == null || mn.inventory == null || slotID >= mn.inventory.itemSlot.Length)
            {
                return true;
            }
            ItemSlot slot = mn.inventory.itemSlot[slotID];
            if (slot == null || slot.itemKey != "pocket_01")
            {
                return true;
            }

            PocketTarget target = ResolvePocketTarget(mn);
            if (TryExpand(target))
            {
                mn.sound.GoSound(207, __instance.transform.position);
                mn.inventory.ConsumeItem(slotID, 1);
                InventoryWindow.Invalidate();
                Plugin.Log.LogInfo($"pocket_01: {target.Name} expanded to {target.Container.size} slots");
            }
            else
            {
                // caution 51 is vanilla's own refusal for this item
                __instance.StartCoroutine(mn.eventMN.GoCaution(51));
            }
            __result = Done();
            return false;
        }

        private static IEnumerator Done()
        {
            yield break;
        }

        // ---------- keeping the game's own mirror readers honest ----------
        //
        // A family of vanilla helpers asks "is this container on screen right now?"
        // and reads mirror slots 50+ when it is. Under interception the answer is
        // yes and the mirror is EMPTY, so an NPC depositing into a chest you happen
        // to be looking at would write into nothing, and a shift-click would post an
        // item into a slot no one ever reads back.
        //
        // Rather than re-implement their bodies, the container's IDENTITY is hidden
        // for the duration of the call: vanilla then takes its own real-slot branch,
        // which is exactly the behaviour we want. Identity is only hidden, never
        // dropped — Vacuum and the prop-action panel key off tmpSubInventory to know
        // WHICH container is in play, and they must keep seeing it.
        private static InventorySlot Unmask(InventoryManager inv, InventorySlot container)
        {
            if (inv == null || Intercepted == null
                || !ReferenceEquals(inv.tmpSubInventory, Intercepted)
                || (container != null && !ReferenceEquals(container, Intercepted)))
            {
                return null;   // not ours, or an outer call already unmasked it
            }
            inv.tmpSubInventory = null;
            return Intercepted;
        }

        private static void Remask(InventoryManager inv, InventorySlot state)
        {
            if (inv != null && state != null)
            {
                inv.tmpSubInventory = state;
            }
        }

        private static InventoryManager InventoryOf(ItemManager itemMN)
        {
            ManagersScript mn = ItemMn(itemMN);
            return mn != null ? mn.inventory : null;
        }

        private static InventoryManager NpcInventory(NPCManager npcMN)
        {
            ManagersScript mn = NpcMn(npcMN);
            return mn != null ? mn.inventory : null;
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.GetCurrentItemSlot))]
        [HarmonyPrefix]
        private static void GetCurrentItemSlot_Prefix(InventoryManager __instance,
            InventorySlot tmpInventory, out InventorySlot __state)
        {
            __state = Unmask(__instance, tmpInventory);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.GetCurrentItemSlot))]
        [HarmonyPostfix]
        private static void GetCurrentItemSlot_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.GetCurrentInventorySlot))]
        [HarmonyPrefix]
        private static void GetCurrentInventorySlot_Prefix(InventoryManager __instance,
            InventorySlot tmpInventory, out InventorySlot __state)
        {
            __state = Unmask(__instance, tmpInventory);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.GetCurrentInventorySlot))]
        [HarmonyPostfix]
        private static void GetCurrentInventorySlot_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.EmptySlotCheck))]
        [HarmonyPrefix]
        private static void EmptySlotCheck_Prefix(InventoryManager __instance,
            InventorySlot tmpInventory, out InventorySlot __state)
        {
            // the no-argument overload asks about the shared bag and never touches
            // tmpSubInventory — leave it alone rather than unmask for nothing
            __state = tmpInventory != null ? Unmask(__instance, tmpInventory) : null;
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.EmptySlotCheck))]
        [HarmonyPostfix]
        private static void EmptySlotCheck_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        // ItemToChest re-does the mirror test inline, so patching the accessor it
        // calls is not enough — the whole call has to run unmasked. This is the NPC
        // deposit path: harvesters and haulers filling storages.
        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.ItemToChest))]
        [HarmonyPrefix]
        private static void ItemToChest_Prefix(ItemManager __instance,
            InventorySlot tmpInventory, out InventorySlot __state)
        {
            __state = Unmask(InventoryOf(__instance), tmpInventory);
        }

        [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.ItemToChest))]
        [HarmonyPostfix]
        private static void ItemToChest_Postfix(ItemManager __instance, InventorySlot __state)
        {
            Remask(InventoryOf(__instance), __state);
        }

        // The NPC work payout (harvest boxes, factories). Like ItemToChest it does
        // its OWN inline mirror test rather than going through the accessors, so
        // letting the harvest box in without this would mean a working NPC
        // finds the box empty and its output goes nowhere.
        [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.WorkResult))]
        [HarmonyPrefix]
        private static void WorkResult_Prefix(NPCManager __instance,
            InventorySlot tmpInventory, out InventorySlot __state)
        {
            __state = Unmask(NpcInventory(__instance), tmpInventory);
        }

        [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.WorkResult))]
        [HarmonyPostfix]
        private static void WorkResult_Postfix(NPCManager __instance, InventorySlot __state)
        {
            Remask(NpcInventory(__instance), __state);
        }

        // Shift-click on any vanilla slot (the Shared Bag included) — vanilla's own
        // transfer, and it posts straight into the mirror. Unmasked it degrades to
        // "no container is open", which is a no-op rather than a lost item; WI-3's
        // own smart-move is the deferred replacement.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.QuickMove))]
        [HarmonyPrefix]
        private static void QuickMove_Prefix(InventoryManager __instance, out InventorySlot __state)
        {
            __state = Unmask(__instance, null);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.QuickMove))]
        [HarmonyPostfix]
        private static void QuickMove_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        // The panel's own "move everything across" buttons. Unreachable now (the
        // panel takes no raycasts), but they write mirror slots unconditionally, so
        // they are unmasked into no-ops rather than left as a loaded gun.
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveAllItemsToMain))]
        [HarmonyPrefix]
        private static void MoveAllItemsToMain_Prefix(InventoryManager __instance, out InventorySlot __state)
        {
            __state = Unmask(__instance, null);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveAllItemsToMain))]
        [HarmonyPostfix]
        private static void MoveAllItemsToMain_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveAllItemsToSub))]
        [HarmonyPrefix]
        private static void MoveAllItemsToSub_Prefix(InventoryManager __instance, out InventorySlot __state)
        {
            __state = Unmask(__instance, null);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveAllItemsToSub))]
        [HarmonyPostfix]
        private static void MoveAllItemsToSub_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveSameItemsToSub))]
        [HarmonyPrefix]
        private static void MoveSameItemsToSub_Prefix(InventoryManager __instance, out InventorySlot __state)
        {
            __state = Unmask(__instance, null);
        }

        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.MoveSameItemsToSub))]
        [HarmonyPostfix]
        private static void MoveSameItemsToSub_Postfix(InventoryManager __instance, InventorySlot __state)
        {
            Remask(__instance, __state);
        }

        // ---------- upkeep ----------

        // PatchAll fails SILENTLY — no exception, no log — when a class carries no
        // class-level Harmony attribute, and the symptom is indistinguishable from
        // the feature simply not being written. The entry points everything here
        // rests on are therefore asserted at startup rather than assumed.
        internal static void VerifyPatches()
        {
            AssertPatched(typeof(InventoryManager), nameof(InventoryManager.SubInventoryLoad));
            AssertPatched(typeof(InventoryManager), nameof(InventoryManager.SubInventoryUnLoad));
            AssertPatched(typeof(InventoryManager), nameof(InventoryManager.BackPackButton));
            AssertPatched(typeof(InventoryManager), nameof(InventoryManager.QuickMove));
            AssertPatched(typeof(ItemManager), nameof(ItemManager.UseConsumables));
            AssertPatched(typeof(NPCManager), nameof(NPCManager.WorkResult));
        }

        private static void AssertPatched(System.Type type, string method)
        {
            System.Reflection.MethodBase target = AccessTools.Method(type, method);
            if (target == null || Harmony.GetPatchInfo(target) == null)
            {
                Plugin.Log.LogError($"container UI: {type.Name}.{method} is NOT patched — the vanilla "
                    + "container UI is still live and will fight the Inventory over the same slots");
            }
        }

        // Loading a save reloads the scene: a claim on a torn-down container must
        // not survive into the new one (scene-reset discipline, §6).
        internal static void ResetForSceneLoad()
        {
            Intercepted = null;
            _lentToVanilla = null;
            _bypass = false;
            _equipIcon = null;
            _iconCanvas = null;
        }

        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.inventory == null)
            {
                return;
            }
            // Invariant: we only ever claim a load that leaves the panel ACTIVE.
            // A closed panel therefore means the claim is stale — and a stale claim
            // would keep suppressing unloads for a container vanilla owns again.
            if (Intercepted != null && (mn.inventory.subInventory == null
                    || !mn.inventory.subInventory.activeSelf))
            {
                ReleaseClaim(mn.inventory);
            }
            // An unmasked call that threw would leave the identity hidden. Nothing
            // else nulls tmpSubInventory while we hold a claim, so this is safe to
            // heal rather than diagnose.
            if (Intercepted != null && mn.inventory.tmpSubInventory == null)
            {
                mn.inventory.tmpSubInventory = Intercepted;
            }
            SyncVacuumLoan(mn);
            SyncEquipIcon(mn);
        }

        // Vacuum (the three "ruck" NPCs, NPCManager.cs:6753) pulls loot out of the
        // world and posts it into mirror slots 50+ with no test at all — it simply
        // assumes the panel owns the container. Unmasking cannot help: there is no
        // real-slot branch to fall through to. So for the length of a Vacuum the
        // container is LENT BACK to vanilla — mirrored properly, worked on by
        // vanilla, written back when the lock clears — and then reclaimed.
        // lockSubInventory is set by nothing else, which makes it an exact signal.
        private static void SyncVacuumLoan(ManagersScript mn)
        {
            InventoryManager inv = mn.inventory;
            if (inv.lockSubInventory)
            {
                if (_lentToVanilla != null || Intercepted == null)
                {
                    return;
                }
                _lentToVanilla = Intercepted;
                Intercepted = null;
                _bypass = true;
                try
                {
                    // vanilla's own load: mirrors the contents, sizes the panel,
                    // and leaves the machinery in the state Vacuum expects
                    inv.SubInventoryLoad(_lentToVanilla, null);
                }
                finally
                {
                    _bypass = false;
                }
                Plugin.Log.LogInfo($"container UI: lent '{_lentToVanilla.gameObject.name}' back to the vanilla "
                    + "panel for a Vacuum");
                return;
            }
            if (_lentToVanilla == null)
            {
                return;
            }
            InventorySlot lent = _lentToVanilla;
            _lentToVanilla = null;
            // the panel may have closed mid-Vacuum, in which case vanilla already
            // wrote the contents back on its way out
            if (inv.subInventory != null && inv.subInventory.activeSelf
                && ReferenceEquals(inv.tmpSubInventory, lent))
            {
                inv.SubInventoryUnLoad();
                Intercepted = lent;
                InventoryWindow.Invalidate();
                Plugin.Log.LogInfo($"container UI: reclaimed '{lent.gameObject.name}' after the Vacuum");
            }
        }

        private static void SyncEquipIcon(ManagersScript mn)
        {
            // fake-null after a scene reload: re-find rather than hold a reference
            // into a torn-down scene, and only re-scan once per canvas
            if (_equipIcon == null)
            {
                Canvas canvas = mn.uiMN != null && mn.uiMN.itemDescPanel != null
                    ? mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>()
                    : null;
                if (canvas == null || ReferenceEquals(canvas.rootCanvas, _iconCanvas))
                {
                    return;
                }
                _iconCanvas = canvas.rootCanvas;
                _equipIcon = Theme.FindByPersistentCall(_iconCanvas, "EquipVisible");
                Plugin.Log.LogInfo(_equipIcon != null
                    ? $"container UI: equip icon '{_equipIcon.name}' found — folding it into the bag icon"
                    : "container UI: equip icon not found; the bag icon still toggles the Inventory");
            }
            if (_equipIcon != null && _equipIcon.activeSelf)
            {
                _equipIcon.SetActive(false);
            }
        }
    }
}
