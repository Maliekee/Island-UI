using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // THE NEWEST CAUTION HAS THE LINE. Vanilla's caution (EventManager.GoCaution,
    // GoCautionSt) shows its text on the one centre line for three seconds, then
    // hides the line if the text there is still its own. Raise the same caution
    // twice inside three seconds - a refused right-click, clicked again - and the
    // first one's timer finds "its" text and hides the line early, so the second
    // showed only for what was left of the first's three seconds. The same text
    // is the whole trigger, which is why it happened only sometimes.
    //
    // Replaced, keeping everything else vanilla does: the same line, text and
    // three seconds, a different caution taking over the line at once. Only the
    // ownership changes - a number per raise, and only the latest may hide it.
    // Runs after the other prefixes on these two (the dev console's echo, the
    // give's caution count), which still see every raise.
    [HarmonyPatch]
    internal static class CautionLine
    {
        private static readonly AccessTools.FieldRef<EventManager, ManagersScript> MnRef =
            AccessTools.FieldRefAccess<EventManager, ManagersScript>("mn");

        private static int _latest;
        private const float Seconds = 3f;   // vanilla's

        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCaution))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool GoCaution_Prefix(EventManager __instance, int id, ref IEnumerator __result)
        {
            ManagersScript mn = MnRef(__instance);
            string[] all = mn != null && mn.textMN != null ? mn.textMN.cautions : null;
            if (all == null || id < 0 || id >= all.Length)
            {
                return true;   // vanilla's own "Caution text not found"
            }
            __result = Show(__instance, all[id]);
            return false;
        }

        [HarmonyPatch(typeof(EventManager), nameof(EventManager.GoCautionSt))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool GoCautionSt_Prefix(EventManager __instance, string message, ref IEnumerator __result)
        {
            __result = Show(__instance, message);
            return false;
        }

        private static IEnumerator Show(EventManager events, string text)
        {
            int mine = ++_latest;
            if (events.cautionText == null)
            {
                yield break;
            }
            events.tmpCaution = text;
            events.cautionText.text = text;
            events.cautionText.gameObject.SetActive(true);
            float left = Seconds;
            while (left > 0f && mine == _latest)
            {
                left -= Time.deltaTime;
                yield return null;
            }
            if (mine == _latest && events.cautionText != null)
            {
                events.cautionText.gameObject.SetActive(false);
            }
        }
    }
}
