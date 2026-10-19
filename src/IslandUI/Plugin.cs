using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using IslandUI.UI;
using UnityEngine;

namespace IslandUI
{
    // Island UI — one integrated UI module over Mad Island's inventory, crafting
    // and item browsing. The architecture it implements is settled in
    // docs/inventory-window-ux.md: "the Catalogue Browser knows, the item card
    // points, the Inventory grabs."
    //
    // Folder map (navigation only — the assembly is one module by design, since
    // browsing, the card and the inventory share the theme kit, the search engine
    // and the container model):
    //   Model/      derived game data (recipe index, item tags) — no UI
    //   UI/         the Island UI kit: theme tokens, window, button, rows
    //   Browser/    the Catalogue Browser (left, reading)
    //   Card/       the item card adapter, wherever an item appears
    //   Inventory/  the Inventory (right, acting)
    //   Crafting/   crafting GAMEPLAY patches: sourcing, counting, consumption
    //   Legacy/     vanilla-panel taming; deletable as one unit once the browser
    //               fully replaces the vanilla grid
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "madisland.islandui";
        public const string PluginName = "Island UI";
        public const string PluginVersion = "0.3.0";

        internal static ManualLogSource Log;

        // [General] Enabled, flipped by clicking the icon on the title screen (Shared/TitleBadge.cs).
        // Read ONCE, in Awake: Teardown only leaves things right for a fresh assembly - statics are
        // never reset - so this mod cannot come back on inside the same process. _running is what
        // Awake found; the entry may say something else until the next start, and the badge shows
        // exactly that difference.
        internal static ConfigEntry<bool> Enabled;
        private bool _running;

        private ManagersScript _mn;
        private static float _nextMoveRefresh;
        private static float _nextPanelSetCheck;
        private ItemManager _warmedItemMN;
        // PlayerMoved subscriptions; disposing one unsubscribes it
        private System.IDisposable _reachMoveSub;
        private System.IDisposable _craftMoveSub;
        private Harmony _harmony;

        // Managers only exist once the game scene is up, so resolve lazily and re-try on scene changes
        private ManagersScript Mn
        {
            get
            {
                if (_mn == null)
                {
                    _mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
                }
                return _mn;
            }
        }

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Turns Island UI on or off. Takes effect the next time the game starts; clicking the mod's icon on the title screen also switches it.");
            _running = Enabled.Value;
            Shared.TitleBadge.Install(Assembly.GetExecutingAssembly(), "IslandUI.icon.png", "Island UI", PluginVersion,
                () => _running, () => Enabled.Value, on =>
                {
                    Enabled.Value = on;
                    Log.LogInfo(PluginName + " set to " + (on ? "ON" : "OFF") + " from the title screen"
                        + (on == _running ? "." : " - at the next start."));
                });
            if (!_running)
            {
                // nothing bound, nothing patched, no objects: Update, LateUpdate and OnDestroy all
                // ask _running first. That includes QoL/DevLaunch, so -LoadSave does nothing either.
                Log.LogInfo($"{PluginName} v{PluginVersion} is switched OFF ([General] Enabled) - only the title-screen icon is loaded.");
                return;
            }
            Settings.Bind(Config);
            QoL.Bind(Config);   // the keymap, hotkeys and tweaks that were QoLTweaks
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");

            GameCursor.Init();   // nothing in the game competes for the cursor

            _harmony = new Harmony(PluginGuid);
            // Not PatchAll: it aborts on the first bad target and takes every later patch
            // class with it, and it skips a class with no type-level attribute in silence.
            // The census isolates each class and says which ones no longer bind — which is
            // the first thing to look at after a game update. See src/Shared/PatchCensus.cs.
            Shared.PatchCensus.ApplyAndReport(_harmony, Assembly.GetExecutingAssembly(), Log);
            UIProbes.Install(_harmony);
            StoryChoices.Install(_harmony);   // marks each story choice's call site
            StoryChoices.TakeOver();
            VanillaContainerUI.VerifyPatches();
            QoL.Apply(_harmony);

            // The Escape cascade (settings -> Inventory -> pinned recipe -> vanilla
            // panels) is CloseAll.Run, ticked from Update with the other hotkeys.
            // the Inventory's reach set follows the player
            _reachMoveSub = PlayerMovedWatcher.Subscribe(1f, _ => InventoryWindow.OnPlayerMoved());
            // Loading a save reloads the scene: statics that describe the torn-down
            // scene must not leak into the new one (a stale bench scope would open
            // the next session's menu filtered to last session's bench)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnQuitting;
            // a hot reload's predecessor left the bar, favourites and hooks behind
            try { SaveBlock.TakeOver(); MoodLedger.TakeOver(); }
            catch (System.Exception e) { Log.LogWarning("Reload: taking over the UI state threw: " + e); }

            // The innate menu's chest scan is anchored to the player, so unlike a bench
            // it goes stale on movement; refresh as the player walks with the browser
            // open. A full CraftOpen(0) rebuilds all 500 buttons plus sections —
            // expensive enough to hang the game when fired every meter in a dense base
            // — so it only runs when the nearby BENCH set changed (the recipe list
            // itself changes); otherwise a light pass re-scans chests and refreshes
            // craftability/counts on the existing buttons.
            _craftMoveSub = PlayerMovedWatcher.Subscribe(1f, _ =>
            {
                var mn = Mn;
                // inventory/uiMN can be briefly null while a save loads mid-session
                if (mn == null || !mn.allSet || mn.inventory == null || mn.uiMN == null)
                {
                    return;
                }
                if (mn.inventory.craftPanelID != 0)
                {
                    return;
                }
                if (mn.uiMN.craftPanel == null || !mn.uiMN.craftPanel.activeSelf)
                {
                    return;
                }
                if (Time.unscaledTime < _nextMoveRefresh)
                {
                    return;
                }
                _nextMoveRefresh = Time.unscaledTime + Mathf.Max(0.25f, Settings.MoveRefreshCooldown.Value);
                // a scoped menu (clicked bench) shows a fixed panel — the
                // nearby-bench set doesn't apply; counts still refresh below
                if (AggregatedCrafting.BenchScope == 0 && AggregatedCrafting.NearbyBenchSetChanged(mn))
                {
                    mn.inventory.CraftOpen(0);
                }
                else
                {
                    // CraftableCheck(refFind:true) re-runs the player-centered chest
                    // scan (our prefix) and re-sweeps all buttons via the snapshot
                    mn.inventory.CraftableCheck();
                }
            });
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            AggregatedCrafting.BenchScope = 0;
            VanillaContainerUI.ResetForSceneLoad();
            ReasonTip.ResetForSceneLoad();
            CursorBadge.ResetForSceneLoad();
            // a focus jump holds a container from the scene that just died,
            // and its link lines are objects in it
            GameCursor.ResetForSceneLoad();
            // the bar's objects died with the scene, and its bindings belong to
            // the save that just went away; a load puts the new save's back
            // (SaveBlock)
            ActionBar.ResetForSceneLoad();
            InventoryWindow.ResetUnitForSceneLoad();
            // a slot mark is only true of this scene's slots; a load puts
            // the save's marks back afterwards (SaveBlock)
            Favourites.ResetForSceneLoad();
            ToolBelt.ResetForSceneLoad();   // the same, for the tool belt's hooks
            SplitDialog.ResetForSceneLoad();
            UI.DevConsole.ResetForSceneLoad();
            FavouriteRules.ResetForSceneLoad();
            StoryChoices.ResetForSceneLoad();
            PickupToast.ResetForSceneLoad();
            ReachButton.ResetForSceneLoad();
            InventoryWindow.ResetHud();
            // ...and the settings window plus its clone in the game's menu.
            // The lesson from the reach pool, applied at the time rather than
            // after a save reload found it: a new object gets wired into the
            // LIFECYCLE reset, not only the per-refresh one.
            SettingsWindow.ResetForSceneLoad();
            // the objects that died with the scene leave the ownership list too
            Owned.Prune();
        }

        // Samples the player position for PlayerMovedWatcher. Managers only exists in
        // the game scene; Mn re-resolves it lazily and Update reads it every frame anyway.
        private void TickPlayerMoved()
        {
            var mn = Mn;
            PlayerMove pMove = mn != null && mn.allSet && mn.gameMN != null ? mn.gameMN.pMove : null;
            if (pMove == null)
            {
                PlayerMovedWatcher.PlayerGone();
                return;
            }
            PlayerMovedWatcher.Tick(pMove.transform.position);
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }
            // FIRST: the PlayerMoved subscribers (reach set, innate-menu refresh) fire
            // ahead of this frame's window ticks, as they did when the IslandEvents
            // plugin's own Update ran before ours.
            TickPlayerMoved();
            if (Input.GetKeyDown(KeyCode.F10))
            {
                DumpPlayerState();
            }
            // F10 dumps the containers NEAR YOU, one line each, which answers
            // "what is this particular chest". F7 asks the other question: what
            // kinds exist across the whole save, how many of each the player has
            // actually built, and which engine drives them. See ContainerCensus.
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ContainerCensus.Run(Mn);
            }
            // F10 lists sprite NAMES, and names turned out not to be enough to
            // choose an icon by - "does it contain 'chest'" cannot see that one
            // glyph is line art and the next is a painted item. F8 writes them out.
            if (Input.GetKeyDown(KeyCode.F8))
            {
                UI.SpriteDump.Run("ui_",
                    System.IO.Path.Combine(BepInEx.Paths.PluginPath, "sprite-dump"));
            }
            GameCursor.Tick();
            QoL.Tick(Mn);
            // FindItem's first lookup per item is a synchronous Resources.Load — cold,
            // ~500 of them land inside the first browser open of a session and stutter.
            // Warm the cache in the background right after a save loads (per
            // ItemManager instance: scene reloads reset the game's cache).
            var mnWarm = Mn;
            if (mnWarm != null && mnWarm.allSet && mnWarm.itemMN != null
                && !ReferenceEquals(mnWarm.itemMN, _warmedItemMN))
            {
                _warmedItemMN = mnWarm.itemMN;
                CanvasIsolation.Apply(mnWarm);
                BrowserShell.Prewarm(mnWarm);
                StartCoroutine(WarmFindItemCache(mnWarm, mnWarm.itemMN));
            }
            CraftLazyFill.TryPostOpenRefresh(Mn);
            CraftLazyFill.Maintain();
            // One vicinity band per frame while nothing is asking for them, so
            // the first open of the window does not build a base's worth of
            // cells in the frame you pressed the key.
            InventoryWindow.WarmPool();
            // the console first: whether it owns the keyboard is the first thing
            // FilterInput asks
            UI.DevConsole.Tick(Mn);
            // BEFORE the windows that read a query: the input owner decides
            // whether anything is focused this frame, and a window reading that one
            // frame stale flickers on the first keystroke of every filter.
            UI.FilterInput.Tick(Mn);
            BrowserShell.Tick(Mn);
            BrowserSearch.Tick(Mn);
            // before the window: it decides whether a container is intercepted,
            // which is what the window's hide/read rules key off this frame
            VanillaContainerUI.Tick(Mn);
            InventoryWindow.Tick(Mn);
            InventoryWindow.TickHud(Mn);
            ActionBar.Tick(Mn);
            InventoryWindow.TickPlayerVitals(Mn);
            // after the bar: the unit stands on the bar's rect (D108)
            InventoryWindow.TickUnit(Mn);
            SettingsWindow.Tick(Mn);
            MoodReport.Tick(Mn);
            UI.MapMood.Tick(Mn);
            // after every surface has had its say about what the cursor is over
            InventoryWindow.CommitBadge();
            // PageUp/PageDown page through the recipe grid (blocked automatically
            // while typing in search — the input-suppression patch covers these reads)
            var mnNow = Mn;
            if (mnNow != null && mnNow.uiMN != null && mnNow.uiMN.craftPanel != null
                && mnNow.uiMN.craftPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    CraftPanelLayout.ScrollRows(mnNow.uiMN, -CraftPanelLayout.VisibleRows(mnNow.uiMN));
                }
                else if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    CraftPanelLayout.ScrollRows(mnNow.uiMN, CraftPanelLayout.VisibleRows(mnNow.uiMN));
                }

                // crafter NPCs move on their own — the panel set can change
                // while the PLAYER stands still (the PlayerMoved subscriber never
                // fires). Poll the set once a second; rebuild only on change.
                if (mnNow.inventory != null
                    && mnNow.inventory.craftPanelID == 0
                    && AggregatedCrafting.BenchScope == 0 // scoped menu: fixed panel
                    && Time.unscaledTime >= _nextPanelSetCheck)
                {
                    _nextPanelSetCheck = Time.unscaledTime + 1f;
                    if (AggregatedCrafting.NearbyBenchSetChanged(mnNow))
                    {
                        mnNow.inventory.CraftOpen(0);
                    }
                }
            }
            // plain Input read is fine: the default (G) is not among the game's keys, so
            // the keymap (QoL/KeyRemap.cs) never remaps it (and it must stay physical anyway)
            KeyCode stackKey = Settings.QuickStackKey.Value;
            if (stackKey != KeyCode.None && Input.GetKeyDown(stackKey))
            {
                QuickStack.TryExecute(Mn);
            }
            // V by default. The game reads V in exactly one place - the drone
            // flight loop, where it hides the drone model - so a plain Input read
            // is right: nothing to remap around outside that loop.
            KeyCode reachKey = Settings.ReachKey.Value;
            if (reachKey != KeyCode.None && Input.GetKeyDown(reachKey)
                && Settings.ShowNearby.Value)
            {
                InventoryWindow.ToggleReach();
            }
            // close-all: unbound by default, see [Window] CloseAllKey
            CloseAll.Tick(Mn);
        }

        // Warms two of the game's lazily-built caches in idle frames after a save
        // loads, so their one-time cost never lands inside a UI open:
        // - ItemManager.FindItem: cold lookups are synchronous Resources.Load calls;
        //   ~500 of them used to hit the first browser open.
        // - RandomCharacter.EquipableNPCCache: cold entries scan the whole NPC roster
        //   per item; the apparel UI (spider/pretty closet) queries it for every
        //   clothing item at once — the "very big stutter" on first closet open.
        //   Also feeds the card's equip list and the search's character index.
        private System.Collections.IEnumerator WarmFindItemCache(ManagersScript mn, ItemManager target)
        {
            yield return null;
            int lookups = 0;
            int equipWarmed = 0;
            CraftManager.CraftData[] craftData = mn.craftMN != null ? mn.craftMN.craftData : null;
            if (craftData == null)
            {
                yield break;
            }
            for (int p = 0; p < craftData.Length; p++)
            {
                CraftInfo[] recipes = craftData[p] != null ? craftData[p].craftInfo : null;
                if (recipes == null)
                {
                    continue;
                }
                for (int i = 0; i < recipes.Length; i++)
                {
                    // scene changed mid-warm: this ItemManager (and its cache) is gone
                    if (mn == null || mn.itemMN != target)
                    {
                        yield break;
                    }
                    if (recipes[i] == null)
                    {
                        continue;
                    }
                    ItemData item = target.FindItem(recipes[i].name);
                    lookups++;
                    if (item != null && mn.randChar != null
                        && mn.randChar.ItemTypeToEquipID(item.itemType) != -1)
                    {
                        mn.randChar.EquipableNPCCache(item.name);
                        equipWarmed++;
                        if (equipWarmed % 4 == 0)
                        {
                            yield return null; // roster scans are the heavy part
                        }
                    }
                    if (lookups % 25 == 0)
                    {
                        yield return null;
                    }
                }
            }

            // Also warm everything the player OWNS (chests, closets): the apparel UI
            // runs equipability checks on stored clothing, which includes drops and
            // purchases no craft panel covers — those were still cold on the first
            // closet open.
            InventorySlot[] containers = FindObjectsOfType<InventorySlot>();
            var seen = new HashSet<string>();
            for (int c = 0; c < containers.Length; c++)
            {
                ItemSlot[] slots = containers[c] != null ? containers[c].slots : null;
                if (slots == null)
                {
                    continue;
                }
                for (int s = 0; s < slots.Length; s++)
                {
                    if (mn == null || mn.itemMN != target)
                    {
                        yield break;
                    }
                    string itemKey = slots[s] != null ? slots[s].itemKey : null;
                    if (string.IsNullOrEmpty(itemKey) || !seen.Add(itemKey))
                    {
                        continue;
                    }
                    ItemData item = target.FindItem(itemKey);
                    lookups++;
                    if (item != null && mn.randChar != null
                        && mn.randChar.ItemTypeToEquipID(item.itemType) != -1)
                    {
                        mn.randChar.EquipableNPCCache(item.name);
                        equipWarmed++;
                        if (equipWarmed % 4 == 0)
                        {
                            yield return null;
                        }
                    }
                    if (lookups % 25 == 0)
                    {
                        yield return null;
                    }
                }
            }
            Log.LogDebug($"Caches warmed: {lookups} FindItem lookups, {equipWarmed} equipable scans");
        }

        // ---- hot reload (docs/test-harness.md §7) ------------------------------------
        //
        // OnDestroy runs ONLY when this component is destroyed explicitly - the test
        // harness's hot reload - never at game exit: Application.quitting has fired by
        // then and the process is going away, exactly as before this existed. Teardown
        // puts the game back the way a fresh instance expects to find it: windows closed
        // through their own paths, patches off, our objects gone, nothing of ours left
        // subscribed to anything that outlives us. Statics need no reset - the next
        // instance is a new assembly with its own.
        private bool _quitting;

        private void OnQuitting()
        {
            _quitting = true;
        }

        private void OnDestroy()
        {
            Application.quitting -= OnQuitting;
            if (_quitting)
            {
                return;
            }
            if (!_running)
            {
                Shared.TitleBadge.Remove();     // all there is to take down
                return;
            }
            Teardown();
        }

        private void Teardown()
        {
            Log.LogInfo(PluginName + ": tearing down for reload");
            // 1. windows and vanilla panels close through their own paths - which is
            //    what puts mirror slots back into their containers
            try { CloseAll.Run(Mn); }
            catch (System.Exception e) { Log.LogWarning("Teardown: close-all threw: " + e); }
            // ...and the bar, favourites and hooks go to the next generation
            StoryChoices.HandOver();
            try { SaveBlock.HandOver(); MoodLedger.HandOver(); }
            catch (System.Exception e) { Log.LogWarning("Teardown: handover threw: " + e); }
            // ...and the one vanilla rect the mod moves RELATIVELY goes back where it
            // was, or the next generation lifts it from a lifted place
            try { BrowserShell.RestoreVanillaPanel(); }
            catch (System.Exception e) { Log.LogWarning("Teardown: panel restore threw: " + e); }
            // ...and so does the one it GROWS, or the next generation grows it again
            try { CraftPanelLayout.RestoreVanilla(Mn); }
            catch (System.Exception e) { Log.LogWarning("Teardown: panel size restore threw: " + e); }
            // ...and the vanilla panels the unit veils show again
            try { InventoryWindow.UnveilVanilla(Mn); }
            catch (System.Exception e) { Log.LogWarning("Teardown: unveil threw: " + e); }
            // 2. nothing of ours stays subscribed to anything that outlives us
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UI.DevConsole.Teardown();
            Shared.TitleBadge.Remove();
            _reachMoveSub?.Dispose();
            _craftMoveSub?.Dispose();
            // 3. patches off: vanilla runs vanilla until the next instance patches
            try { _harmony?.UnpatchSelf(); }
            catch (System.Exception e) { Log.LogWarning("Teardown: unpatch threw: " + e); }
            // 4. every object we built (deferred destroy; the reloader waits a frame)
            InventoryWindow.ResetHud();
            HudGlyphs.Clear();
            int destroyed = Owned.DestroyAll();
            // 5. globals we set
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Log.LogInfo(PluginName + ": teardown complete, " + destroyed + " objects destroyed");
            // 6. our log source, or every reload adds a duplicate to BepInEx's list
            BepInEx.Logging.Logger.Sources.Remove(Logger);
        }

        private void LateUpdate()
        {
            if (!_running)
            {
                return;
            }
            CraftingPatches.RunCoalescedSweep();
            InventoryWindow.LateSync();
            StoryChoices.Tick(Mn);
        }

        private void DumpPlayerState()
        {
            var mn = Mn;
            if (mn == null || !mn.allSet)
            {
                Log.LogInfo("F10: managers not ready (main menu?)");
                return;
            }
            var pMove = mn.gameMN.pMove;
            if (pMove == null || pMove.common == null)
            {
                Log.LogInfo("F10: no player yet");
                return;
            }

            var common = pMove.common;
            int usedSlots = 0;
            for (int i = 0; i < mn.inventory.baseCount; i++)
            {
                if (!string.IsNullOrEmpty(mn.inventory.itemSlot[i].itemKey))
                {
                    usedSlots++;
                }
            }

            Log.LogInfo($"F10: player '{common.charaName}' | life {common.life:0.#}/{common.maxLife:0.#} | " +
                        $"pos {pMove.transform.position} | inventory {usedSlots}/{mn.inventory.baseCount} slots used");

            // Container-replacement state: which container we have claimed, and whether the vanilla
            // mirror is live. Per-open logging answered this and drowned the log to
            // do it; asking on demand costs nothing until something looks wrong.
            InventorySlot claimed = VanillaContainerUI.Intercepted;
            Log.LogInfo($"F10: container UI "
                + $"claimed={(claimed != null ? claimed.gameObject.name : "none")} "
                + $"mirrorsLive={VanillaContainerUI.MirrorsLive(mn.inventory)} "
                + $"panel={(mn.inventory.subInventory != null && mn.inventory.subInventory.activeSelf)}");

            // list nearby containers with their InventorySlot.Type (for classifying
            // chests); `plain` is the replacement predicate — exactly which of them we take
            // over and which keep the vanilla panel, without opening each one
            Collider[] hits = Physics.OverlapSphere(pMove.transform.position, ReachRule.Widest(mn), LayerMask.GetMask("BG"));
            foreach (Collider hit in hits)
            {
                InventorySlot slot = hit.GetComponent<InventorySlot>();
                if (slot != null)
                {
                    Log.LogInfo($"F10: nearby container '{slot.gameObject.name}' type={slot.type} size={slot.size} owned={mn.buildMN.OwnCheck(slot.GetComponent<ItemInfo>())} plain={VanillaContainerUI.IsPlainStorage(slot)}");
                }
            }

            // the held item is vanilla's hoverSlot; we strip its backdrop, and if
            // the black square turns out to live on a CHILD instead this says so
            ItemSlot hover = mn.inventory != null ? mn.inventory.hoverSlot : null;
            if (hover != null)
            {
                Log.LogInfo("F10: ---- hoverSlot (the held item) ----");
                DumpTree(hover.transform, 0, 3);
            }

            // GameManager.cursorImage is declared and never READ anywhere in
            // the code — but it is a public field, so the scene may still assign
            // art to it. If it does there is a cursor to harvest; if not, one has
            // to be authored.
            Sprite cursor = mn.gameMN != null ? mn.gameMN.cursorImage : null;
            Log.LogInfo($"F10: cursorImage = {(cursor != null ? $"'{cursor.name}' {cursor.rect.width}x{cursor.rect.height}" : "unassigned")}");

            DumpEquipSlotIcons(mn);
            DumpUiSpriteNames(mn);
        }

        // Every ui_ sprite the canvas can reach, so a wanted glyph (mouse buttons,
        // keys, markers) can be found by name instead of guessed at.
        private static void DumpUiSpriteNames(ManagersScript mn)
        {
            Canvas canvas = mn.uiMN != null && mn.uiMN.craftPanel != null
                ? mn.uiMN.craftPanel.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                return;
            }
            var names = new SortedSet<string>();
            foreach (var image in canvas.rootCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.sprite != null && image.sprite.name.StartsWith("ui_"))
                {
                    names.Add(image.sprite.name);
                }
            }
            Log.LogInfo($"F10: {names.Count} ui_ sprites: {string.Join(", ", new List<string>(names).ToArray())}");
        }

        // The vanilla equip strip draws a small TYPE icon over each loadout slot
        // (hat / hands / shirt / legs / shoes ...). Slots 40..49 are scene objects
        // wired in the inspector, so the icon objects are invisible to the
        // decompiled source — this dumps their child tree so the Inventory window
        // can reuse the real sprites instead of inventing new ones.
        private static void DumpEquipSlotIcons(ManagersScript mn)
        {
            GameObject panel = mn.inventory != null ? mn.inventory.equipPanel : null;
            if (panel == null)
            {
                Log.LogInfo("F10: equipPanel unavailable");
                return;
            }
            Log.LogInfo("F10: ---- equipPanel tree (sprite/colour harvest) ----");
            DumpTree(panel.transform, 0, 4);
            Log.LogInfo("F10: ---- end equipPanel ----");
        }

        private static void DumpTree(Transform node, int depth, int maxDepth)
        {
            var image = node.GetComponent<UnityEngine.UI.Image>();
            string detail = "";
            if (image != null)
            {
                string sprite = image.sprite != null ? image.sprite.name : "-";
                string over = image.overrideSprite != null ? image.overrideSprite.name : "-";
                Color32 c = image.color;
                detail = $"  [Image sprite={sprite} override={over} type={image.type}"
                    + $" ppu={image.pixelsPerUnitMultiplier} rgba={c.r},{c.g},{c.b},{c.a}]";
            }
            var slot = node.GetComponent<ItemSlot>();
            if (slot != null)
            {
                detail += $"  [ItemSlot id={slot.slotID}]";
            }
            Log.LogInfo($"F10: {new string(' ', depth * 2)}{node.name}"
                + $" active={node.gameObject.activeSelf}{detail}");
            if (depth >= maxDepth)
            {
                return;
            }
            foreach (Transform child in node)
            {
                DumpTree(child, depth + 1, maxDepth);
            }
        }
    }
}
