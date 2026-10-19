using HarmonyLib;

namespace IslandUI
{
    // The map and the quest diary are one overlay slot: opening either closes the
    // other. Postfixes on vanilla's two toggles rather than on our HUD buttons,
    // because the map also opens from the M key and on death - and these two
    // methods are the only places either overlay's SetActive is called.
    [HarmonyPatch(typeof(UIManager))]
    internal static class MapQuestExclusion
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIManager.MapView))]
        private static void AfterMap(UIManager __instance)
        {
            if (__instance.mapCanvas.activeSelf && __instance.questDiaryPanel.activeSelf)
                __instance.QuestDiaryVisible();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UIManager.QuestDiaryVisible))]
        private static void AfterQuests(UIManager __instance)
        {
            // MapView, not SetActive: its close path also hides the map's hover text
            if (__instance.questDiaryPanel.activeSelf && __instance.mapCanvas.activeSelf)
                __instance.MapView();
        }
    }
}
