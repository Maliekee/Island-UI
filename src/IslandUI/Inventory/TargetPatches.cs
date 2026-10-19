using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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

        // Escape ends the target before it pauses (D150). Vanilla reads Escape in
        // one place, GameManager.Update, and PauseGame is all it does with it; an
        // unpause (resume, or a second press while paused) is left alone.
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PauseGame))]
        [HarmonyPrefix]
        private static bool PauseGame_Prefix(bool resume)
        {
            return resume || Time.timeScale == 0f
                || (!ItemCard.EscapeNavigates() && !InventoryWindow.EscapeUntargets());
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
    }
}
