using HarmonyLib;

namespace IslandUI
{
    // The tool belt's marks follow their tools (ToolBelt) through the same doors
    // FavouriteMoves reads: SlotLoadSlot copies an item, SlotClear empties a slot,
    // and every move in the game - the cursor, a swap through stockSlot, the tool
    // swap's EquipItem through equipSlot - is built from those two. StackSameItem's
    // full-stack swap is the one move around them, and two tools of one kind are
    // always full stacks, so it is covered too. Only into YOUR slots
    // (FavouriteMoves.Yours): a tool put anywhere else leaves its hook empty.
    //
    // Its own class rather than more lines in FavouriteMoves: Harmony chains both
    // postfixes on the same methods, and the favourite's rules stay as they are.
    [HarmonyPatch(typeof(InventoryManager))]
    internal static class ToolBeltMoves
    {
        private static readonly AccessTools.FieldRef<InventoryManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<InventoryManager, ManagersScript>("mn");

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.SlotLoadSlot))]
        private static void AfterLoad(InventoryManager __instance, ItemSlot from, ItemSlot to)
        {
            if (ToolBelt.Any && to != null && !ReferenceEquals(from, to))
            {
                ToolBelt.Loaded(from, to, FavouriteMoves.Yours(__instance, to));
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.SlotClear))]
        private static void AfterClear(ItemSlot tmpSlot)
        {
            if (ToolBelt.Any)
            {
                ToolBelt.Cleared(tmpSlot);
            }
        }

        private struct Trade
        {
            public bool Swap;
            public int FromHooks, ToHooks;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(InventoryManager.StackSameItem))]
        private static void BeforeStack(InventoryManager __instance, ItemSlot from, ItemSlot to,
            bool swapable, out Trade __state)
        {
            __state = default(Trade);
            if (!ToolBelt.Any || !swapable || from == null || to == null
                || string.IsNullOrEmpty(from.itemKey) || from.itemKey != to.itemKey)
            {
                return;
            }
            __state.FromHooks = ToolBelt.HooksOf(from);
            __state.ToHooks = ToolBelt.HooksOf(to);
            if (__state.FromHooks == 0 && __state.ToHooks == 0)
            {
                return;
            }
            ManagersScript mn = MnRef(__instance);
            ItemData data = mn != null && mn.itemMN != null ? mn.itemMN.FindItem(to.itemKey) : null;
            __state.Swap = data != null && to.stack == data.maxStack;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventoryManager.StackSameItem))]
        private static void AfterStack(InventoryManager __instance, ItemSlot from, ItemSlot to, Trade __state)
        {
            if (__state.Swap)
            {
                ToolBelt.Traded(from, to, __state.FromHooks, __state.ToHooks,
                    FavouriteMoves.Yours(__instance, from), FavouriteMoves.Yours(__instance, to));
            }
        }

        // Harness-only: preserve exact item references during a visual hot reload.
        // A bag hosts multiple ItemSlot components, so a GameObject path alone
        // cannot restore the selected instance. The unloaded generation still
        // holds its marks; consult its validated Hung result, never copy by key.
        internal static string RestoreAfterReload(int generation)
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "IslandUI_g" + generation) { continue; }
                System.Type model = assembly.GetType("IslandUI.ToolBelt");
                if (model == null || model == typeof(ToolBelt)) { continue; }
                System.Reflection.MethodInfo hung = model.GetMethod("Hung",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (hung == null) { continue; }
                var log = new System.Text.StringBuilder();
                for (int h = 0; h < ToolBelt.Hooks; h++)
                {
                    ItemSlot slot = hung.Invoke(null, new object[] { h }) as ItemSlot;
                    if (ToolBelt.Hung(h) == null && slot != null && ToolBelt.Fits(h, slot.toolType))
                    {
                        ToolBelt.Hang(h, slot);
                        log.Append(h).Append('=').Append(slot.itemKey).Append(" life=").Append(slot.itemLife).Append(' ');
                    }
                }
                return log.Length > 0 ? log.ToString().Trim() : "no assignments to restore";
            }
            return "generation not found";
        }

        // Harness door for a look at the board: on = 1 hangs the first tool from your
        // Shared Bag or bag that fits each EMPTY hook; on = 0 takes down exactly the
        // hooks it filled, so what you hung yourself is never touched.
        private static int _demoHooks;

        internal static string Demo(int on)
        {
            ManagersScript mn = UnityEngine.GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            CommonStates you = mn != null && mn.gameMN != null && mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null;
            if (inv == null || you == null)
            {
                return "skipped: no inventory";
            }
            var log = new System.Text.StringBuilder();
            for (int h = 0; h < ToolBelt.Hooks; h++)
            {
                if (on == 0)
                {
                    if ((_demoHooks & (1 << h)) != 0) { ToolBelt.TakeDown(h); log.Append(" down ").Append(h); }
                    continue;
                }
                if (ToolBelt.Hung(h) != null)
                {
                    continue;
                }
                var mine = new System.Collections.Generic.List<ItemSlot>();
                for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++) { mine.Add(inv.itemSlot[i]); }
                if (you.bag != null && you.bag.slots != null) { mine.AddRange(you.bag.slots); }
                foreach (ItemSlot s in mine)
                {
                    if (s != null && !string.IsNullOrEmpty(s.itemKey) && ToolBelt.Fits(h, s.toolType))
                    {
                        ToolBelt.Hang(h, s);
                        _demoHooks |= 1 << h;
                        log.Append(" ").Append(h).Append("=").Append(s.itemKey);
                        break;
                    }
                }
            }
            if (on == 0) { _demoHooks = 0; }
            return log.Length > 0 ? log.ToString().Trim() : "nothing to do";
        }

        // Harness door: hang a tool from your Shared Bag, move it to an empty cell and
        // back through the real primitives, and put the board back as it was found.
        internal static string SelfTest()
        {
            ManagersScript mn = UnityEngine.GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null || inv.hoverSlot == null || inv.hoverSlot.gameObject.activeSelf)
            {
                return "skipped: no inventory, or an item on the cursor";
            }
            ItemSlot tool = null, empty = null;
            int hook = -1;
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot s = inv.itemSlot[i];
                if (empty == null && string.IsNullOrEmpty(s.itemKey)) { empty = s; }
                for (int h = 0; tool == null && h < ToolBelt.Hooks; h++)
                {
                    if (!string.IsNullOrEmpty(s.itemKey) && ToolBelt.Fits(h, s.toolType)) { tool = s; hook = h; }
                }
            }
            if (tool == null || empty == null)
            {
                return "skipped: needs a tool and an empty cell in the Shared Bag";
            }
            ItemSlot was = ToolBelt.Hung(hook);
            var log = new System.Text.StringBuilder(tool.itemKey + " on hook " + hook + ":");
            ToolBelt.Hang(hook, tool);
            inv.SlotToSlot(tool, empty);
            log.Append(" move ").Append(ToolBelt.Hung(hook) == empty && !ToolBelt.On(hook, tool) ? "ok" : "FAIL");
            inv.SlotToSlot(empty, tool);
            log.Append(" back ").Append(ToolBelt.Hung(hook) == tool && !ToolBelt.On(hook, empty) ? "ok" : "FAIL");
            ToolBelt.Hang(hook, was);
            InventoryWindow.Invalidate();
            return log.ToString();
        }
    }
}
