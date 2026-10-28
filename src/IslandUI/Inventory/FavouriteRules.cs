using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // What the mark DOES. The flag lives in Model/Favourites; this is
    // the behaviour it buys, and all of it is "the game reaches for this one":
    //
    //   never swept   quick-stack skips it (in QuickStack, where the sweep is)
    //   kept          it never leaves your bags until unmarked (below)
    //
    // The tool swap also lives here, but it reaches for the tool on the TOOL
    // BELT's hook (Model/ToolBelt), not a favourite: the board is separate from
    // favoriting.
    //
    // The bare [HarmonyPatch] is load-bearing: PatchAll skips any class without a
    // CLASS-level Harmony attribute, however many method-level ones it carries.
    [HarmonyPatch]
    internal static class FavouriteRules
    {
        // What an auto-swap displaced, so the action can hand it back. Only ever
        // one: PlayerMove.InteractAction returns early while `nowAction` is set, so
        // a second harvest cannot begin while one is running.
        private sealed class Swap
        {
            public CommonStates Owner;
            public ItemSlot Source;      // where the tool came from — now holds what it replaced
            public string ToolKey;       // what we put into equip[1]
            public string ReplacedKey;   // what came out of it; empty = nothing to give back
        }

        private static Swap _pending;
        private static ManagersScript _mn;

        internal static void ResetForSceneLoad()
        {
            _pending = null;
            _mn = null;
        }

        // PlayerMove.mn is a PROPERTY, not a field. FieldRefAccess on it throws at
        // TYPE-INIT, so the first call into this class failed — and a prefix that
        // throws aborts the call it is attached to, which meant every Harvest died
        // and nothing in the world could be chopped, mined or picked. Resolve it
        // the way the property itself does, and cache with a fake-null re-lookup so
        // a scene reload cannot hand back a dead instance.
        private static ManagersScript Mn()
        {
            if (_mn == null)
            {
                GameObject managers = GameObject.Find("Managers");
                _mn = managers != null ? managers.GetComponent<ManagersScript>() : null;
            }
            return _mn;
        }

        // ---------- kept in your bags ----------

        // "favorited items are not supposed to be moved out of the player
        // container ... putting it in another stash should also be prevented until
        // you have uh, unmarked it as favorite" - and, asked, a favourite may still
        // be LIFTED: to rearrange your bags, or to bind it to the action bar, which
        // reads the held item. What it may not do is leave. Each way out is refused
        // at the game's own door, so a vanilla panel's click keeps the rule too:
        //
        //   a place       the hand into anything not yours    SlotToSlot
        //   a drop        the hand onto the world             ItemDrop
        //   an equip      onto anyone but you                 RandomCharacter.EquipItem
        //   a quick move  vanilla's shift-click across        InventoryManager.QuickMove
        //
        // Our gestures ask first and never start - the place, the drag's drop and
        // Ctrl+click (InventoryWindow) - and send the item home; All in, Stack and
        // quick-stack already leave it. A refusal says why at the cursor for a
        // moment: a click that silently does nothing reads as broken.
        internal static string KeptWords => Shared.Loc.Get("inv.hint.kept", "favourite  ·  alt-click to unmark first");
        internal static float KeptUntil;

        internal static void Refuse()
        {
            KeptUntil = Time.unscaledTime + 1.6f;
        }

        // would this take a favourite out of your inventory?
        internal static bool Leaves(InventoryManager inv, ItemSlot from, ItemSlot to)
        {
            return Favourites.Any && Favourites.IsFavourite(from) && !FavouriteMoves.Yours(inv, to);
        }

        // Only the HAND: a transit slot's step inside a game move (EquipItem's
        // equipSlot, a swap's stockSlot) is refused at that move's door, never
        // halfway through it, where the item would be stranded in the transit slot.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.SlotToSlot))]
        private static bool KeepPlace(InventoryManager __instance, ItemSlot from, ItemSlot to)
        {
            if (!ReferenceEquals(from, __instance.hoverSlot) || !Leaves(__instance, from, to))
            {
                return true;
            }
            Refuse();
            return false;
        }

        // vanilla drops the hand on a click over the world (InventoryManager.Hover);
        // refused, the item goes home, and the hover's loop ends with the hand empty
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.ItemDrop))]
        private static bool KeepDrop(InventoryManager __instance, ItemSlot tmpItem)
        {
            if (!Favourites.Any || !Favourites.IsFavourite(tmpItem))
            {
                return true;
            }
            Refuse();
            if (ReferenceEquals(tmpItem, __instance.hoverSlot))
            {
                InventoryWindow.HomeHeld();
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RandomCharacter), nameof(RandomCharacter.EquipItem))]
        private static bool KeepEquip(CommonStates common, ItemSlot slot)
        {
            ManagersScript mn = Favourites.Any ? Mn() : null;
            if (mn == null || mn.gameMN == null || common == null || mn.gameMN.IsPlayer(common)
                || !Favourites.IsFavourite(slot))
            {
                return true;
            }
            Refuse();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.QuickMove))]
        private static bool KeepQuickMove(InventoryManager __instance, int slotID)
        {
            ItemSlot[] slots = __instance.itemSlot;
            if (!Favourites.Any || slots == null || slotID < 0 || slotID >= slots.Length
                || !Favourites.IsFavourite(slots[slotID]))
            {
                return true;
            }
            Refuse();
            return false;
        }

        // Harness door: the lock through the real primitives on your own Shared Bag -
        // mark a stack, lift it, drop it (refused, home), place it outside (refused,
        // home) - leaving the bag as it was found. The outside is a fresh drop object,
        // destroyed after, so a failure lands in the world and not in a container.
        internal static string KeptTest()
        {
            ManagersScript mn = Mn();
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null || inv.hoverSlot == null || inv.hoverSlot.gameObject.activeSelf)
            {
                return "skipped: no inventory, or an item on the cursor";
            }
            ItemSlot a = null;
            int ai = -1;
            var before = new System.Collections.Generic.HashSet<ItemSlot>();
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot s = inv.itemSlot[i];
                if (Favourites.IsFavourite(s)) { before.Add(s); }
                else if (!string.IsNullOrEmpty(s.itemKey) && s.common == null && a == null) { a = s; ai = i; }
            }
            if (a == null)
            {
                return "skipped: needs an unmarked item in the Shared Bag";
            }
            _before = before;
            var log = new System.Text.StringBuilder(a.itemKey + " x" + a.stack + ":");
            try
            {
                return KeptSteps(inv, a, ai, log);
            }
            catch (System.Exception e)
            {
                Favourites.Set(a, false);
                return log + " THREW " + e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace;
            }
        }

        private static string KeptSteps(InventoryManager inv, ItemSlot a, int ai, System.Text.StringBuilder log)
        {
            Favourites.Set(a, true);
            inv.SlotToSlot(a, inv.hoverSlot);
            log.Append(" lift ").Append(Favourites.IsFavourite(inv.hoverSlot) ? "ok" : "FAIL");
            inv.ItemDrop(inv.hoverSlot);
            ItemSlot home = MarkedIn(inv);
            log.Append(" drop ").Append(home != null && !inv.hoverSlot.gameObject.activeSelf ? "refused-home" : "FAIL");
            if (home != null && !ReferenceEquals(home, a)) { inv.SlotToSlot(home, a); }
            inv.SlotToSlot(a, inv.hoverSlot);
            ItemSlot outside = inv.MakeDropItem();
            inv.SlotToSlot(inv.hoverSlot, outside);
            bool refused = string.IsNullOrEmpty(outside.itemKey) && Favourites.IsFavourite(inv.hoverSlot);
            log.Append(" place ").Append(refused ? "refused" : "FAIL");
            if (!refused && !string.IsNullOrEmpty(outside.itemKey)) { inv.SlotToSlot(outside, a); }
            UnityEngine.Object.Destroy(outside.gameObject);
            InventoryWindow.HomeHeld();
            home = MarkedIn(inv);
            if (home != null && !ReferenceEquals(home, a)) { inv.SlotToSlot(home, a); }
            inv.QuickMove(ai);
            log.Append(" quickmove ").Append(Favourites.IsFavourite(a) ? "kept" : "FAIL");
            log.Append(" back ").Append(Favourites.IsFavourite(a) && !inv.hoverSlot.gameObject.activeSelf ? "ok" : "FAIL");
            Favourites.Set(a, false);
            KeptUntil = 0f;
            InventoryWindow.Invalidate();
            return log.ToString();
        }

        private static System.Collections.Generic.HashSet<ItemSlot> _before;

        // where the test's favourite is now: the Shared Bag's one marked cell that
        // was not marked before the test began
        private static ItemSlot MarkedIn(InventoryManager inv)
        {
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (Favourites.IsFavourite(inv.itemSlot[i]) && !_before.Contains(inv.itemSlot[i]))
                {
                    return inv.itemSlot[i];
                }
            }
            return null;
        }

        // ---------- auto-equipped ----------

        // Harvesting reads ONE thing: `common.equip[1].toolType`. Every gate in
        // PlayerMove.Harvest tests it, the damage comes from `equip[1].attack`, and
        // the swing loop runs while that toolType is unchanged. Nothing in that
        // path ever looks at a bag — so the board's axe sitting in your inventory
        // is inert, and the tool swap is not "harvest with the item in the bag" but "put it in
        // equip[1] before vanilla's gate runs".
        //
        // Vanilla's gate then passes on its own. We never re-implement the check;
        // we only change what it is checking, which is why this stays a prefix that
        // always returns true.
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.Harvest))]
        [HarmonyPrefix]
        private static void Harvest_Prefix(PlayerMove __instance, ItemInfo tmpInfo)
        {
            // A convenience must never be able to stop a core verb. A prefix that
            // throws aborts the call it is attached to, so anything wrong in here —
            // now or later — would take ALL harvesting with it, which is exactly
            // what happened the first time this shipped.
            try
            {
                TrySwapToHung(__instance, tmpInfo);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"tool swap skipped — {e}");
            }
        }

        private static void TrySwapToHung(PlayerMove __instance, ItemInfo tmpInfo)
        {
            if (tmpInfo == null || __instance == null)
            {
                return;
            }
            EnsureTool(__instance.common, AcceptedTools(tmpInfo.itemKey), tmpInfo.itemKey);
        }

        // Cutting limbs off a corpse gates on the same slot as harvesting does —
        // `pMove.common.equip[1].toolType`, Knife for the head (state 1) and
        // Axe/Axe2/Picknaxe for every other part (NPCManager.Dissect:4616). Same
        // problem, same answer: put the board's tool in equip[1] and let vanilla's own
        // check pass.
        [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.Dissect))]
        [HarmonyPrefix]
        private static void Dissect_Prefix(int state)
        {
            try
            {
                ManagersScript mn = Mn();
                PlayerMove pMove = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
                if (pMove != null)
                {
                    EnsureTool(pMove.common, state == 1 ? Knives : Axes, $"dissect {state}");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"dissect tool swap skipped — {e}");
            }
        }

        // The whole tool swap, once the caller has said what the action needs.
        private static void EnsureTool(CommonStates common, ItemData.ToolType[] wanted,
            string what)
        {
            ManagersScript mn = Mn();
            if (wanted == null || common == null || mn == null || mn.inventory == null
                || mn.randChar == null || common.equip == null || common.equip.Length < 2)
            {
                return;   // nothing asked of the tool, or nothing to ask it of
            }
            if (Accepts(wanted, common.equip[1]))
            {
                return;   // already holding the right thing
            }
            ItemSlot pick = FindHungTool(mn, common, wanted);
            if (pick == null)
            {
                return;   // nothing hung that would do — vanilla's caution stands
            }
            Plugin.Log.LogInfo($"tool swap: swapping to the board's '{pick.itemKey}' for {what}");
            // the three-way swap through inventory.equipSlot: the tool being
            // replaced lands in the slot this one came from, and nothing is left on
            // the cursor. Safe from both bag kinds — that branch is chosen by
            // `IsEquipSlot(slot.slotID)`, which is 40..49 only, and a Shared Bag
            // slot is 0..17 while a real bag slot carries 0 (§7.1's trap).
            //
            // That property is also what makes giving the weapon back trivial:
            // EquipItem on the SAME slot is its own inverse.
            var swap = new Swap
            {
                Owner = common,
                Source = pick,
                ToolKey = pick.itemKey,
                ReplacedKey = common.equip[1].itemKey,
            };
            mn.randChar.EquipItem(common, pick);
            _pending = swap;
            InventoryWindow.Invalidate();
        }

        // ---------- and giving it back ----------

        // Taking your sword away to chop a tree is only acceptable if you get it
        // back. The action's own coroutine is the honest boundary — it ends when
        // the tree falls, when you walk away, when you retarget, when you die, when
        // you raise guard. Every one of those is an interruption, and every one of
        // them should hand the weapon back, which is exactly what "the coroutine
        // finished" means.
        //
        // Wrapping the enumerator is safe here in a way it would not be in general:
        // nothing in the game stops these (the only seven StopCoroutine calls are
        // StoryManager's own cutscene coroutines, and there is no
        // StopAllCoroutines anywhere), so they always run to completion. The
        // try/finally covers the remaining case — the object being destroyed under
        // a scene load — for free.
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.Harvest))]
        [HarmonyPostfix]
        private static void Harvest_Postfix(ref IEnumerator __result)
        {
            __result = RestoreAfter(__result);
        }

        [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.Dissect))]
        [HarmonyPostfix]
        private static void Dissect_Postfix(ref IEnumerator __result)
        {
            __result = RestoreAfter(__result);
        }

        private static IEnumerator RestoreAfter(IEnumerator inner)
        {
            try
            {
                while (inner.MoveNext())
                {
                    yield return inner.Current;
                }
            }
            finally
            {
                RestoreSwappedTool();
            }
        }

        // Both guards are about INTENT rather than mechanism, which is why neither
        // is merely a null check.
        private static void RestoreSwappedTool()
        {
            Swap swap = _pending;
            _pending = null;
            ManagersScript mn = Mn();
            if (swap == null || mn == null || mn.randChar == null || mn.inventory == null
                || swap.Owner == null || swap.Owner.equip == null || swap.Owner.equip.Length < 2)
            {
                return;
            }
            if (swap.Owner.equip[1].itemKey != swap.ToolKey)
            {
                return;   // you changed tools yourself mid-action; your choice stands
            }
            if (swap.Source == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(swap.ReplacedKey))
            {
                // Your hand was empty, so "giving it back" means emptying it again:
                // the tool goes home to the slot it was lifted from. EquipItem
                // cannot do this — its unequip branch is
                // keyed on a UI slotID no real equip slot carries (§7.1) and would
                // drop the tool in the first free base slot anyway — so it is the
                // same shape InventoryWindow.UnequipToBag reproduces by hand.
                if (!string.IsNullOrEmpty(swap.Source.itemKey))
                {
                    return;   // you filled the hole yourself; do not shove it aside
                }
                Plugin.Log.LogInfo($"tool swap: hanging '{swap.ToolKey}' back up, hand empty again");
                ItemSlot worn = swap.Owner.equip[1];
                mn.randChar.UnEquip(swap.Owner, worn);
                mn.inventory.SlotToSlot(worn, swap.Source);
                mn.randChar.EquipEffectCheck(swap.Owner, 1);
                mn.inventory.EquipSlotLoad();
                mn.uiMN.StatusChange(swap.Owner);
                InventoryWindow.Invalidate();
                return;
            }
            if (swap.Source.itemKey != swap.ReplacedKey)
            {
                return;   // what we displaced has been moved; do not chase it
            }
            Plugin.Log.LogInfo($"tool swap: returning '{swap.ReplacedKey}' to hand");
            mn.randChar.EquipItem(swap.Owner, swap.Source);
            InventoryWindow.Invalidate();
        }

        // PlayerMove.Harvest's own table, mirrored. Anything not listed here sets
        // `wpReduce = false` in vanilla and gates on nothing, so it gets no swap.
        // The four "needs SOME tool" keys are deliberately absent: "any tool" is
        // not a preference, and picking one for you there would be guessing.
        private static readonly ItemData.ToolType[] Axes =
            { ItemData.ToolType.Axe, ItemData.ToolType.Axe2, ItemData.ToolType.Picknaxe };
        private static readonly ItemData.ToolType[] HardAxes =
            { ItemData.ToolType.Axe2, ItemData.ToolType.Picknaxe };
        private static readonly ItemData.ToolType[] Picknaxes =
            { ItemData.ToolType.Picknaxe };
        private static readonly ItemData.ToolType[] Picks =
            { ItemData.ToolType.Pickax, ItemData.ToolType.Pickax2, ItemData.ToolType.Picknaxe };
        private static readonly ItemData.ToolType[] HardPicks =
            { ItemData.ToolType.Pickax2, ItemData.ToolType.Picknaxe };
        private static readonly ItemData.ToolType[] Knives =
            { ItemData.ToolType.Knife };

        private static ItemData.ToolType[] AcceptedTools(string resourceKey)
        {
            switch (resourceKey)
            {
                case "res_tree":
                case "res_cactus":
                    return Axes;
                case "res_tree_02":
                    return HardAxes;
                case "res_tree_03":
                    return Picknaxes;
                case "res_stone":
                case "res_junk":
                case "explosionbag_01":
                    return Picks;
                case "ore_blood_01":
                case "ore_chaos_01":
                case "diamond_01":
                    return HardPicks;
                case "event_rope":
                    return Knives;
                default:
                    return null;
            }
        }

        private static bool Accepts(ItemData.ToolType[] wanted, ItemSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.itemKey))
            {
                return false;
            }
            for (int i = 0; i < wanted.Length; i++)
            {
                if (slot.toolType == wanted[i])
                {
                    return true;
                }
            }
            return false;
        }

        // The tool belt's hooks, the need's own kind first: a stone
        // asks the pick hook before the axe hook, where a picknaxe would also do.
        private static ItemSlot FindHungTool(ManagersScript mn, CommonStates common,
            ItemData.ToolType[] wanted)
        {
            int own = System.Array.IndexOf(wanted, ItemData.ToolType.Knife) >= 0 ? ToolBelt.Knife
                : System.Array.IndexOf(wanted, ItemData.ToolType.Pickax2) >= 0 ? ToolBelt.Pick
                : ToolBelt.Axe;
            for (int n = 0; n < ToolBelt.Hooks; n++)
            {
                ItemSlot slot = ToolBelt.Hung((own + n) % ToolBelt.Hooks);
                if (slot != null && Accepts(wanted, slot) && OnYou(mn, common, slot))
                {
                    return slot;
                }
            }
            return null;
        }

        // Your two containers, where the swap reaches from: the Shared Bag is the
        // hand, the Character Bag the pack. A tool anywhere else is not "on you", and
        // reaching into a chest would be the remote-access this project has refused
        // everywhere else.
        private static bool OnYou(ManagersScript mn, CommonStates common, ItemSlot slot)
        {
            InventoryManager inv = mn.inventory;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (ReferenceEquals(inv.itemSlot[i], slot))
                {
                    return true;
                }
            }
            ItemSlot[] bag = common.bag != null ? common.bag.slots : null;
            return bag != null && !VanillaContainerUI.MirrorsLive(inv) && System.Array.IndexOf(bag, slot) >= 0;
        }
    }
}
