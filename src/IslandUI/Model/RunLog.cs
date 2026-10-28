using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // ALWAYS RUN IN THE LOG (Forgejo #134). Vanilla shows the run state on its HUD
    // button, which Island UI hides (InventoryWindow.Hud.cs), so a press of the
    // run key changed nothing you could see. Every flip of GameManager.toggleRun
    // now says so in the game's log: the key's, and the SPEED caption's on your
    // sheet (Dossier.cs), which calls the same ToggleRunChange - one call, one line.
    //
    // SILENT WHILE A SAVE LOADS: Loading() calls ToggleRunChange once to put the
    // saved state back, and that is no news. The flag covers each step of the
    // coroutine and nothing between them, so a press during the load still speaks.
    [HarmonyPatch]
    internal static class RunLog
    {
        private static bool _loading;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.ToggleRunChange))]
        [HarmonyPostfix]
        private static void ToggleRunChange_Postfix(GameManager __instance)
        {
            if (_loading)
            {
                return;
            }
            GameObject go = GameObject.Find("Managers");
            ManagersScript mn = go != null ? go.GetComponent<ManagersScript>() : null;
            if (mn == null || mn.uiMN == null)
            {
                return;
            }
            mn.uiMN.GoLogText(__instance.toggleRun
                ? Shared.Loc.Get("log.always_run.on", "Always run: on")
                : Shared.Loc.Get("log.always_run.off", "Always run: off"));
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Loading))]
        [HarmonyPostfix]
        private static void Loading_Postfix(ref IEnumerator __result)
        {
            __result = Quiet(__result);
        }

        private static IEnumerator Quiet(IEnumerator inner)
        {
            while (true)
            {
                bool more;
                _loading = true;
                try
                {
                    more = inner.MoveNext();
                }
                finally
                {
                    _loading = false;
                }
                if (!more)
                {
                    yield break;
                }
                yield return inner.Current;
            }
        }
    }
}
