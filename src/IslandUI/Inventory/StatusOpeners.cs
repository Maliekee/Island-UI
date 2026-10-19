using HarmonyLib;

namespace IslandUI
{
    // §6.20: "the character sheet" is not a window type - it is a column on a
    // tab. The vanilla status window's three openers all funnel through
    // UIManager.StatusView (the toolbar's StatusButton and the NPC menu's Status
    // verb are scene-wired to it; FriendSlot.FriendView sets menuNPC and calls
    // it), so ONE prefix retires the window: the same toggle, landing on the
    // carrier's column with its Status aspect up. The panel object stays where
    // it is, inactive - UIManager.Awake caches its buttons, NameInput polls its
    // activeSelf, and the sheets read their labels and icons off it.
    //
    // Vanilla's own subject rule is kept exactly: a focused, employed NPC gets
    // the sheet, anyone else means you (StatusView's `employ == None ? player`).
    [HarmonyPatch(typeof(UIManager))]
    internal static class StatusOpeners
    {
        [HarmonyPatch(nameof(UIManager.StatusView))]
        [HarmonyPrefix]
        private static bool StatusView(UIManager __instance)
        {
            if (!Settings.StatusOnColumn.Value)
            {
                return true;
            }
            ManagersScript mn = __instance.mn;
            if (mn == null || mn.gameMN == null || mn.inventory == null)
            {
                return true;
            }
            CommonStates focus = mn.gameMN.menuNPC;
            bool theirs = focus != null && focus.employ != CommonStates.Employ.None;
            if (!InventoryWindow.ToggleStatusSheet(mn, theirs ? focus : null))
            {
                return true;   // the window is not up to it yet: vanilla's sheet
            }
            // as vanilla does on the way out: the mirror slots follow the subject
            mn.inventory.EquipSlotLoad();
            return false;
        }
    }

    // D53: "opened by vanilla's roster button and its key". UIManager.FriendListView
    // is the one entry into the friend-list panel - the toolbar button is scene-wired
    // to it - so one prefix moves the roster into the slot, the same way StatusView
    // above moved the character sheet onto the column. The panel object stays where
    // it is, inactive: CreateFriendList still parents a slot per friend into it and
    // FriendStatusChange still writes to those slots, and neither may be broken.
    [HarmonyPatch(typeof(UIManager))]
    internal static class RosterOpener
    {
        [HarmonyPatch(nameof(UIManager.FriendListView))]
        [HarmonyPrefix]
        private static bool FriendListView(UIManager __instance)
        {
            if (!Settings.RosterOnSlot.Value)
            {
                return true;
            }
            ManagersScript mn = __instance.mn;
            if (mn == null || mn.gameMN == null || mn.inventory == null)
            {
                return true;
            }
            // pressed again with it up, it closes - which is what the vanilla
            // panel's own toggle did
            if (!InventoryWindow.ToggleRoster(mn))
            {
                return true;   // the window is not up to it yet: vanilla's panel
            }
            return false;
        }
    }

    // The left slot has vanilla occupants besides crafting.
    // LeftPanelState raises the collections (2), the dissect panel (3, the body
    // search's Dissect verb, scene-wired) and the shop (4, ShopManager.ShopStart)
    // there, on top of our vicinity / roster window. One occupant of the slot:
    // when one of vanilla's takes it, ours closes - the rule crafting already
    // follows. The other way round is ToggleReach's.
    [HarmonyPatch(typeof(UIManager))]
    internal static class LeftSlotShare
    {
        [HarmonyPatch(nameof(UIManager.LeftPanelState))]
        [HarmonyPostfix]
        private static void LeftPanelState(UIManager __instance)
        {
            if (__instance.leftPanelState >= 2)
            {
                InventoryWindow.CloseReach();
            }
        }
    }
}
