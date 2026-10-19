using HarmonyLib;

namespace IslandUI
{
    // NO PULSE ON WHOEVER YOU SELECTED (vanilla's flashing on a selected NPC is
    // distracting). UIManager.OutGlow adds an OutlineGlow to the chosen
    // character and runs FriendSlotGlowStart, which throbs a white _OverlayColor
    // over their materials for as long as they stay chosen - but only when it was
    // handed the materials, which it does only while the game's own "interact
    // glow" setting is on.
    //
    // So the setting is held off for the length of the call and put straight
    // back: the component, the coroutine and GameManager.tmpGlow are all still
    // made, so nothing that reads them changes, and the player's saved choice is
    // never overwritten. Who is selected is still said - by the world marker and
    // the unit (InventoryWindow.WorldMarker, D101).
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.OutGlow))]
    internal static class SelectionGlow
    {
        [HarmonyPrefix]
        private static void Prefix(bool state, out int __state)
        {
            __state = SaveManager.SaveSettingStatic.interactGlow;
            if (state && !Settings.SelectionGlow.Value)
            {
                SaveManager.SaveSettingStatic.interactGlow = 0;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(int __state)
        {
            SaveManager.SaveSettingStatic.interactGlow = __state;
        }
    }
}
