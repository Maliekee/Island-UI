using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace IslandUI
{
    // One idempotent "give me back control" key. Closes whatever is open, does
    // nothing when nothing is — and NEVER pauses (pause is vanilla's Escape).
    //
    // The Escape cascade runs first and shallowest-first: the settings window (the
    // outermost thing on screen while it is up), then the Inventory, then a
    // pinned recipe deselects and gets out of the way — only a press nothing
    // lighter consumed closes the vanilla panels. The by-item window used to sit in
    // the middle; it retired in WI-6.
    //
    // Once lived in QoLTweaks, reaching these windows through the
    // IslandEvents CloseAllIntercept event. Everything it closes is UI, so it moved
    // here and the event went with the core mod.
    //
    // The bare [HarmonyPatch] is load-bearing: the census skips a class without one.
    [HarmonyPatch]
    internal static class CloseAll
    {
        private static ManagersScript _mn;
        // the frame whose world-focus press closed something (Spent)
        private static int _spentFrame = -1;

        internal static void Tick(ManagersScript mn)
        {
            _mn = mn;
            KeyCode focus = Settings.WorldFocusKey.Value;
            if (focus != KeyCode.None && Input.GetKeyDown(focus))
            {
                WorldFocus(mn);
            }
            KeyCode key = Settings.CloseAllKey.Value;
            // Plain Input read, like the window's other hotkeys: a field's focus
            // suppression (FilterField) applies, which is what makes a press while
            // typing release the field instead — the NEXT press closes. If the key is
            // also a vanilla key the keymap has remapped, the remap applies.
            if (key != KeyCode.None && Input.GetKeyDown(key))
            {
                Run(mn);
            }
        }

        // THE WORLD-FOCUS KEY (Space by default): a quick way back
        // to the world. It closes only what this mod owns - your bag and sheet, the
        // target's bag and sheet, crafting, vicinity and roster - and the map and
        // quest overlays, and nothing else of vanilla's. The safe route:
        // Close All's vanilla sweep once closed a sex scene's own option menu and
        // left no way out of it, and Space is the key vanilla advances every scene
        // with. Plain key read, so a focused text field (FilterField) swallows it as
        // it does Close All. With nothing on screen to close it does nothing, and the
        // press is vanilla's alone.
        private static void WorldFocus(ManagersScript mn)
        {
            if (!HasWork(mn))
            {
                return;
            }
            _spentFrame = Time.frameCount;
            InventoryWindow.CloseOwnedWindows();
            if (mn.uiMN.mapCanvas.activeSelf)
            {
                mn.uiMN.MapView();
            }
            if (mn.uiMN.questDiaryPanel.activeSelf)
            {
                mn.uiMN.QuestDiaryVisible();
            }
        }

        private static bool HasWork(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || SaveManager.nowSaving
                || mn.uiMN.nameInput.enabled || mn.uiMN.mainInputField.gameObject.activeSelf
                || mn.uiMN.talkEditPanel.activeSelf)
            {
                return false;
            }
            return InventoryWindow.OwnedWindowsShown() || mn.uiMN.mapCanvas.activeSelf
                || mn.uiMN.questDiaryPanel.activeSelf;
        }

        // ONE PRESS, ONE MEANING (attack stays on Space like vanilla, and Space also
        // keeps closing windows). Space attacks again, and a Space that closes something
        // is not also a swing: vanilla's attack (PlayerMove.AttackButton, whose only
        // caller is the Space read) asks here first. PlayerMove's Update runs before
        // or after ours, so both orders are answered - closed already this frame, or
        // about to be.
        private static bool Spent()
        {
            if (_spentFrame == Time.frameCount)
            {
                return true;
            }
            KeyCode focus = Settings.WorldFocusKey.Value;
            return focus != KeyCode.None && Input.GetKeyDown(focus) && HasWork(_mn);
        }

        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.AttackButton))]
        [HarmonyPrefix]
        private static bool AttackButton_Prefix(ref IEnumerator __result)
        {
            if (!Spent())
            {
                return true;
            }
            __result = NoSwing();
            return false;
        }

        private static IEnumerator NoSwing()
        {
            yield break;
        }

        internal static void Run(ManagersScript mn)
        {
            if (SettingsWindow.TryEscapeClose())
            {
                return;
            }
            if (ItemCard.TryBack()) return;
            if (InventoryWindow.TryEscapeClose())
            {
                return;
            }
            if (BrowserShell.TryEscapeDeselect())
            {
                return;
            }
            CloseVanillaWindows(mn);
        }

        // Each panel is closed through the game's own toggle/close method so side
        // effects (chest unload, glow reset, status refresh) run exactly as if its
        // close button was clicked; every call is guarded by activeSelf, making a
        // second press a no-op.
        //
        // While a text field is focused this intentionally does nothing: the field's
        // own Escape handling releases focus (and the search field's input suppression
        // + nameInput grace frames keep this handler from seeing that press), so the
        // NEXT press closes the windows.
        private static void CloseVanillaWindows(ManagersScript mn)
        {
            // never touch UI during story events/dialogs — their panels are sequenced
            if (mn == null || !mn.allSet || GameManager.nowEvent || SaveManager.nowSaving)
            {
                return;
            }
            UIManager ui = mn.uiMN;
            InventoryManager inv = mn.inventory;
            if (ui == null || inv == null
                || ui.nameInput.enabled                          // typing (incl. search grace frames)
                || ui.mainInputField.gameObject.activeSelf       // game CLI / name dialogs
                || ui.talkEditPanel.activeSelf)
            {
                return;
            }

            // paused (P) -> resume; part of "gain control", and distinct from pausing
            if (Time.timeScale == 0f)
            {
                mn.gameMN.PauseGame(resume: true);
            }
            // open chest/bag UI first: unloads mirror slots back into the real container
            if (inv.subInventory != null && inv.subInventory.activeSelf)
            {
                inv.SubInventoryVisible();
            }
            // left panel group: craft / collection / dissect / shop
            if (ui.craftPanel.activeSelf || ui.collectionPanel.activeSelf
                || ui.dissectPanel.activeSelf || ui.shopPanel.activeSelf)
            {
                ui.LeftPanelState(0);
            }
            if (ui.mapCanvas.activeSelf)
            {
                ui.MapView();
            }
            if (ui.settingMenu.activeSelf)
            {
                ui.SettingPanelOpen();   // toggle-close; also closes setting/save/status children
            }
            if (ui.statusPanel.activeSelf)
            {
                ui.StatusView();
            }
            if (ui.npcPanel.activeSelf)
            {
                ui.NPCMenuClose();
            }
            if (ui.followerListPanel.activeSelf)
            {
                ui.FollowerPanelClose();
            }
            if (ui.friendListPanel.activeSelf)
            {
                ui.FriendListView();
            }
            if (ui.apparelPanel.activeSelf)
            {
                ui.ApparelPanelVisible();
            }
            if (ui.questDiaryPanel.activeSelf)
            {
                ui.QuestDiaryVisible();
            }
            if (ui.itemDescPanel.activeSelf)
            {
                ui.ItemDescClose();
            }
            if (ui.propPanel.activeSelf)
            {
                ui.PropPanelVisible(false);
            }
        }
    }
}
