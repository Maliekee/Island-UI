using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // Where the game's own code has to hear that the target is ours
    // (window-system.md 6.32, D146, D150, D153). The rules live in
    // InventoryWindow.Target.cs; these three only ask them.
    //
    // The bare [HarmonyPatch] is load-bearing: the census skips a class without one.
    [HarmonyPatch]
    internal static class TargetPatches
    {
        // The game's menu lifetime ends the moment you move; ours does not (D153).
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.NPCMenuClose))]
        [HarmonyPrefix]
        private static bool NPCMenuClose_Prefix()
        {
            return !InventoryWindow.KeepPastVanillaEnd();
        }

        // A change of character ends the target: the one you swapped to was it,
        // and would be yourself. SetPlayer is the swap's one door (ChangePlayer,
        // load, story); the prefix runs while pMove is still the old player's.
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SetPlayer))]
        [HarmonyPrefix]
        private static void SetPlayer_Prefix()
        {
            InventoryWindow.Untarget("change");
        }

        // Escape ends the target before it pauses (D150). Vanilla reads Escape in
        // one place, GameManager.Update, and PauseGame is all it does with it; an
        // unpause (resume, or a second press while paused) is left alone. The
        // split dialog is asked first: it is in front of everything, and with it
        // open the target keeps the key, so Escape used to fall through to pause.
        // Then a Set place pick (Forgejo #20): Escape cancels it, and only it.
        // Vanilla's dialogue editor before all of them (Forgejo #230): it is what
        // the key is pressed at, and the untarget would leave it writing to you.
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PauseGame))]
        [HarmonyPrefix]
        private static bool PauseGame_Prefix(bool resume)
        {
            return resume || Time.timeScale == 0f
                || (!InventoryWindow.EscapeClosesTalkEdit() && !SplitDialog.EscapeCancels() &&!InventoryWindow.EscapeCancelsPlacing() && !ItemCard.EscapeNavigates() && !InventoryWindow.EscapeUntargets());
        }

        // Tab is the target key (D157): it no longer opens the game's menu panel -
        // while that panel is up, Tab still closes it, as the toggle it is
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.SettingPanelOpen))]
        [HarmonyPrefix]
        private static bool SettingPanelOpen_Prefix(UIManager __instance)
        {
            bool opening = __instance.settingMenu != null && !__instance.settingMenu.activeSelf;
            return !(opening && InventoryWindow.TabTakesTheKey());
        }

        // Your bag and sheet draw after the game's menu and hide it (Forgejo #202), so
        // they make way on every way in. Up after the call means it opened: a close
        // leaves it down, and so does the Tab press refused above.
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.SettingPanelOpen))]
        [HarmonyPostfix]
        private static void SettingPanelOpen_Postfix(UIManager __instance)
        {
            if (__instance.settingMenu != null && __instance.settingMenu.activeSelf)
            {
                InventoryWindow.ClearForGameMenu();
            }
        }

        // NO CTRL+CLICK IN THE WORLD (D158): the Tab click does the job better.
        // Vanilla's
        // InteractAction reads Left Ctrl twice - a prop of yours used from build
        // range, a person reached from build range - and both become a plain
        // click: walk to it. Tab targets without walking, and the construction
        // skill's range still counts there (ReachRule). Ctrl+click on an item cell
        // is ours and untouched (a click over UI sets no target).
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.InteractAction))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> InteractAction_NoCtrl(IEnumerable<CodeInstruction> code)
        {
            MethodInfo getKey = AccessTools.Method(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) });
            MethodInfo never = AccessTools.Method(typeof(TargetPatches), nameof(NoCtrlReach));
            var list = new List<CodeInstruction>(code);
            int swapped = 0;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Calls(getKey) && list[i - 1].LoadsConstant((long)KeyCode.LeftControl))
                {
                    list[i].operand = never;   // same signature, labels kept
                    swapped++;
                }
            }
            if (swapped != 2)
            {
                Plugin.Log.LogWarning("[target] InteractAction: " + swapped + " Ctrl reads replaced, 2 expected");
            }
            return list;
        }

        private static bool NoCtrlReach(KeyCode key)
        {
            return false;
        }

        // A PIECE OF WHAT YOU BUILT ANSWERS AS THE WHOLE. A table's top is its own
        // collider, a Platform trigger for standing things on, with no ItemInfo. The
        // ray from the camera meets it first, so a click on the part of a table you
        // see most of set a target the game could do nothing with, and remove mode
        // saw nothing there. The game reads one such piece as its parent already
        // (BrokenColl, in InteractAction); this is that reading for the rest: the
        // eleven tables' tops, and the parts of the fake tree and the wreath.
        //
        // A solid Platform stays itself: that is a stair or a raft, and a click on
        // it means walk there. So does anything of the map's own, whose pieces are
        // the game's business, as a house's door is.
        private const int LayerBG = 12;
        private const int LayerPlatform = 25;

        internal static GameObject PieceOwner(ManagersScript mn, GameObject piece)
        {
            if (piece == null || mn == null || mn.buildMN == null
                || (piece.layer != LayerBG && piece.layer != LayerPlatform)
                || piece.GetComponent<ItemInfo>() != null)
            {
                return piece;
            }
            Collider part = piece.GetComponent<Collider>();
            if (piece.layer == LayerPlatform && (part == null || !part.isTrigger))
            {
                return piece;
            }
            ItemInfo owner = piece.GetComponentInParent<ItemInfo>();
            return owner != null && mn.buildMN.OwnCheck(owner) ? owner.gameObject : piece;
        }

        private static readonly AccessTools.FieldRef<PlayerMove, ManagersScript> PlayerMn =
            AccessTools.FieldRefAccess<PlayerMove, ManagersScript>("_mn");

        // before the walk reads it, where the game reads BrokenColl
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.InteractAction))]
        [HarmonyPrefix]
        private static void InteractAction_Piece(PlayerMove __instance)
        {
            __instance.target = PieceOwner(PlayerMn(__instance), __instance.target);
        }

        // remove mode reads this one, from a coroutine that runs after Update
        [HarmonyPatch(typeof(PlayerMove), "Update")]
        [HarmonyPostfix]
        private static void Update_Piece(PlayerMove __instance)
        {
            __instance.cursoring = PieceOwner(PlayerMn(__instance), __instance.cursoring);
        }

        // A caution raised, counted: a give that raises one was refused (Drop.cs).
        // Both run their first segment inside StartCoroutine, so the count moves
        // inside the call that caused it.
        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCaution))]
        [HarmonyPrefix]
        private static void GoCaution_Prefix()
        {
            InventoryWindow.CautionsRaised++;
        }

        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCautionSt))]
        [HarmonyPrefix]
        private static void GoCautionSt_Prefix()
        {
            InventoryWindow.CautionsRaised++;
        }

        // A walk to give (InventoryWindow.Drop.cs) ends in the give, not in their
        // menu: should vanilla arrive at 1 m before reach was seen at 3 m
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.InteractNPC))]
        [HarmonyPrefix]
        private static bool InteractNPC_Prefix(PlayerMove __instance)
        {
            return !InventoryWindow.ArrivedToGive(__instance);
        }
    }
}
