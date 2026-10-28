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
            // the press that closes it must not land in the world after it.
            // InventoryWindow.OfferUnderPointer: an item held over someone is given
            // to them on release, not dropped on the ground on the press.
            // InventoryWindow.PlacePanelUnderPointer: a click on the placement panel
            // must not also place the person on the ground behind it.
            // TalkBubble.OwnsPointer: the bubble is modal, the press outside that
            // closes it included, and the wheel under it is not the camera's.
            // ManageFan.OwnsPointer: the Manage key's stem, the same over its keys
            if (Ours())
            {
                __instance.OnUI = true;
            }
        }

        internal static bool Ours()
        {
            // InventoryWindow.SheetDragging: the sheet dragged by its tabs passes
            // out over the world on its way between its two homes
            return InventoryWindow.IsDragging || ActionBar.IsDragging || InventoryWindow.BoardDragging
                || InventoryWindow.SheetDragging
                || ReachButton.PointerOver || SettingsWindow.PointerOver
                || InventoryWindow.MatrixOwnsPointer || InventoryWindow.OfferUnderPointer
                || InventoryWindow.PlacePanelUnderPointer || TalkBubble.OwnsPointer || ManageFan.OwnsPointer;
        }
    }

    // ...AND THE WALK STILL SETTLES WHEN YOU STOP. PlayerMove.Update returns early
    // on a held button over UI - before the branch that turns the walk cycle back
    // to idle - so with the pointer ours for a drag, letting go of WASD stopped
    // the body and left its legs walking in place. The same idle vanilla adds in
    // that branch, added here for exactly that case.
    [HarmonyPatch(typeof(PlayerMove), "Update")]
    internal static class DragLetsTheWalkSettle
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerMove __instance)
        {
            if (!Input.GetMouseButton(0) || __instance.xInput != 0f || __instance.yInput != 0f
                || __instance.toolAction || __instance.target != null || !DragOwnsThePointer.Ours()
                || __instance.common == null || __instance.common.anim == null)
            {
                return;
            }
            if (__instance.CheckWalking() || __instance.CheckRunning())
            {
                __instance.common.anim.state.AddAnimation(0, __instance.GetMoveAnim(0), loop: true, 0f);
            }
        }
    }
}
