using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // Feature: no world-hover name under the UI. The game's own OnUI flag
    // (EventSystem.IsPointerOverGameObject, set in GameManager.OnUICheck) gates
    // clicks and interactions — but PlayerMove's hover path calls
    // UIManager.CursorName(cursoring) WITHOUT consulting it, so props behind the
    // crafting menu (or any panel) still show their floating name text.
    // Gate it at the chokepoint: over UI, the world-hover name hides.
    //
    // A WRECK NAMES ITS BUILDING (on hover it showed no text over the cursor, as
    // intact structures do). A broken
    // building's own colliders are off (DefenceInfo.VisibleState Dead) and the ray
    // lands on its "BrokenColl" child, which carries no ItemInfo, so CursorName hid
    // the name. The game's click already reads that child as its parent
    // (PlayerMove.cs, `target.name == "BrokenColl"`); the name now does the same.
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.CursorName))]
    internal static class WorldHoverBlock
    {
        private static readonly AccessTools.FieldRef<UIManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<UIManager, ManagersScript>("mn");

        [HarmonyPrefix]
        private static void Prefix(UIManager __instance, ref GameObject target)
        {
            ManagersScript mn = MnRef(__instance);
            if (mn != null && mn.gameMN != null && mn.gameMN.OnUI)
            {
                target = null; // CursorName(null) hides the name text
                return;
            }
            if (target != null && target.name == "BrokenColl" && target.transform.parent != null)
            {
                target = target.transform.parent.gameObject;
            }
        }
    }

    // While an item is being DRAGGED the pointer belongs to the inventory,
    // even when it is out over the world. PlayerMove's move-toward-the-cursor
    // branch (PlayerMove.cs:293) runs on a HELD left button and is gated on
    // gameMN.OnUI — so without this the player walks toward the cursor for the
    // whole length of a drag, which is the one gesture where the button is
    // deliberately held down over the world.
    //
    // A postfix on the game's own check rather than an assignment from our Update:
    // GameManager.Update recomputes OnUI every frame and plugin update order is not
    // guaranteed, so this is the only place that reliably has the last word.
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.OnUICheck))]
    internal static class DragOwnsThePointer
    {
        [HarmonyPostfix]
        private static void Postfix(GameManager __instance)
        {
            // ActionBar.IsDragging joins them for the bar's drag-off-to-remove: that
            // gesture deliberately ENDS out over the world, which is where the
            // held button would otherwise be read as "walk there". The tool belt's
            // take-down is the same gesture and joins for the same reason.
            // InventoryWindow.MatrixOwnsPointer: the relations matrix is modal, and
            // the press that closes it must not land in the world after it
            if (InventoryWindow.IsDragging || ActionBar.IsDragging || InventoryWindow.BoardDragging
                || ReachButton.PointerOver || SettingsWindow.PointerOver
                || InventoryWindow.MatrixOwnsPointer)
            {
                __instance.OnUI = true;
            }
        }
    }
}
