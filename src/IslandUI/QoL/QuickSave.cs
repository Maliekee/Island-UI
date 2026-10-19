using HarmonyLib;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // The quick-save key: saves to the slot this game was loaded from. The first
    // step toward continuous save, kept deliberately simple.
    //
    // It is the game's own Save, so the slot-00 backup copy, the screenshot and our
    // <IslandUI> block (SaveBlock) all come along unchanged. What it does NOT keep is
    // the game's banner (a plain line of text says it instead of the vanilla
    // popup), which SaveCoroutine also holds up for 2.5 s after the
    // file is written. The banner is SaveTextPanel, switched on and off by
    // SetActive calls inside the coroutine; rather than rewrite the coroutine, the
    // panel is scaled to nothing for as long as this save runs, and a line of text
    // of ours says "Saving..." and then "Saved".
    //
    // "Loaded from" is SaveManager.currentLoadID, which only LoadStart sets. A new
    // game, or a save loaded from the DLSave folder, has none (-1), and a manual save
    // to another slot leaves it alone - that slot is a marker, the game you are in
    // stays where it was.
    [HarmonyPatch]
    internal static class QuickSave
    {
        private const float Hold = 1.6f;   // "Saved" stays this long, then fades
        private const float Fade = 0.5f;

        private static int _slot = -1;          // the slot a quiet save is writing, -1 when none
        private static Transform _banner;       // the game's SaveTextPanel, while hidden
        private static Vector3 _bannerScale;
        private static TextMeshProUGUI _text;
        private static float _fadeAt = -1f;     // unscaled time the fade starts; -1 = no fade

        internal static void Run(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.save == null || mn.gameMN.pMove == null
                || SaveManager.SaveStatic.loaded)
            {
                return;
            }
            UIManager ui = mn.uiMN;
            if (ui.nameInput.enabled || ui.mainInputField.gameObject.activeSelf || ui.talkEditPanel.activeSelf)
            {
                return;
            }
            if (SaveManager.nowSaving)
            {
                return; // a save is already running; its own message is on screen
            }
            int slot = mn.save.currentLoadID;
            if (slot < 0 || slot >= 20)
            {
                Show("No save slot to save to: save from the menu once and load that slot", Theme.KitCream);
                _fadeAt = Time.unscaledTime + Hold + 1.5f;
                return;
            }
            // the gates the save menu's own path applies: UIManager.SaveOpen, then
            // YesNoCommand's SavableCheck (which shows its own caution)
            if (!ui.savable || GameManager.nowEvent)
            {
                mn.eventMN.StartCoroutine(mn.eventMN.GoCaution(42));
                return;
            }
            if (!mn.gameMN.SavableCheck())
            {
                return;
            }
            Plugin.Log.LogInfo($"Quick save to slot {slot}");
            HideBanner();
            _slot = slot;
            Show("Saving...", Theme.KitCream);
            _fadeAt = -1f;
            mn.save.Save(slot);
        }

        // Also called for slot 00 while a save to another slot backs the old file up
        // there. The call for the slot itself comes right after the file is written.
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveTextChange))]
        [HarmonyPostfix]
        private static void SaveTextChange_Postfix(int saveID)
        {
            if (_slot < 0 || saveID != _slot)
            {
                return;
            }
            Show(saveID == 0 ? "Saved to the backup slot" : $"Saved to slot {saveID}", Theme.KitCream);
            _fadeAt = Time.unscaledTime + Hold;
        }

        // every frame from QoL.Tick
        internal static void Tick()
        {
            // the coroutine clears nowSaving as its last step, after the banner's
            // linger: only then does the panel get its size back, already switched off
            if (_slot >= 0 && !SaveManager.nowSaving)
            {
                _slot = -1;
                RestoreBanner();
            }
            if (_text == null || _fadeAt < 0f)
            {
                return;
            }
            float t = Time.unscaledTime - _fadeAt;
            if (t >= Fade)
            {
                _text.gameObject.SetActive(false);
                _fadeAt = -1f;
                return;
            }
            if (t > 0f)
            {
                _text.alpha = 1f - t / Fade;
            }
        }

        private static void HideBanner()
        {
            _banner = GameObject.Find("StaticGroup")?.transform.Find("FrontCanvas/SaveTextPanel");
            if (_banner != null)
            {
                _bannerScale = _banner.localScale;
                _banner.localScale = Vector3.zero;
            }
        }

        private static void RestoreBanner()
        {
            if (_banner != null)
            {
                _banner.localScale = _bannerScale;
            }
            _banner = null;
        }

        // Under MainCanvas, so the screenshot the save takes (CapForSave hides that
        // canvas for its frame) does not have the message in it.
        private static void Show(string message, Color color)
        {
            if (_text == null)
            {
                Transform canvas = GameObject.Find("StaticGroup")?.transform.Find("MainCanvas");
                if (canvas == null)
                {
                    return;
                }
                _text = Theme.MakeText(canvas, "IslandUI_QuickSave", 26f, color, TextAlignmentOptions.Top);
                _text.enableWordWrapping = false;
                _text.overflowMode = TextOverflowModes.Overflow;
                var rt = _text.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(900f, 40f);
                rt.anchoredPosition = new Vector2(0f, -90f);
            }
            _text.transform.SetAsLastSibling();
            _text.text = message;
            _text.color = color;
            _text.alpha = 1f;
            _text.gameObject.SetActive(true);
        }
    }
}
