using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Lifecycle, open/close, slot resolution.
    internal static partial class InventoryWindow
    {
        // ---------- lifecycle ----------

        private static bool EnsureBuilt(ManagersScript mn)
        {
            _mn = mn;
            if (_root != null)
            {
                return true;
            }
            if (mn.uiMN == null || mn.uiMN.itemDescPanel == null || mn.inventory == null
                || !Theme.Ensure(mn, mn.inventory))
            {
                return false;
            }
            // scene reload discipline: fresh build, fresh pools
            Plates.Clear();
            _platesUsed = 0;
            // The pool has plates of its own, and they die with the root exactly as
            // these do. They were not being cleared with them: a save reload rebuilt
            // the root, reseeded Plates under it, and left ReachPlates holding
            // GameObjects that Unity had already destroyed - so TakePlate handed out
            // corpses and the window collapsed into a blob in the corner.
            //
            // The second pool was wired into the per-refresh reset and not into the
            // lifecycle one. Anything with two pools has two places to remember, and
            // this is the one that only shows up on a scene change.
            ReachPlates.Clear();
            ReachOrder.Clear();
            _reachPlatesUsed = 0;
            _reachContent = null;
            _slotTabs = null;
            for (int g = 0; g < GroupCount; g++)
            {
                _footTabs[g] = null;   // died with the root, like the slot's tabs
                _dossierTabs[g] = null;
                _inventoryW[g] = 0f;
            }
            SheetPlates.Clear();
            _vanillaAspectIcons = null;
            _relNamesWho = null;
            _matrix = null;
            _relNamesCount = -1;
            _reachSpacer = null;
            _reachPanel = null;
            _reachScroll = 0f;
            _reachAnchor = null;
            // ...and the scan cache, which holds containers from the scene that
            // just died. It would heal on its own - a destroyed slot reads null
            // and the one-second ceiling refills it - but "heals on its own" is
            // what the sentence above says about the pool, and that one did not.
            ReachScan.Clear();
            ReachHeights.Clear();
            InvalidateReachScan();
            _target = null;
            ApplySlotScale();

            Canvas rootCanvas = mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>();
            Transform parent = rootCanvas != null
                ? rootCanvas.rootCanvas.transform
                : mn.uiMN.itemDescPanel.transform.parent;
            _root = Owned.New("IslandUI_Inventory");
            _root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            CanvasIsolation.Isolate(_root);

            for (int i = 0; i < PlatePool; i++)
            {
                Plates.Add(CreatePlate(_root.transform));
            }

            _hover = Theme.MakeText(_root.transform, "Hover", Theme.FontCaption,
                Theme.TextDim, TextAlignmentOptions.MidlineRight);
            _hover.enableWordWrapping = false;
            // RETIRED FROM SIGHT: the hover text just on top of the bag is gone.
            // The item card names what
            // you point at and the cursor badge says what a modifier will do, so the
            // one-line readout only repeated them. It is still written each tick,
            // because ApplyEquipPreview, which fills it, also lights the recipient.
            _hover.gameObject.SetActive(false);

            _root.SetActive(false);
            Plugin.Log.LogInfo("Inventory built (container plates, bottom-up)");
            return true;
        }

        // A fixed pool meant that with the Nearby section expanded the last
        // groups bound — the target's loadout and title — simply never appeared.
        // Silent truncation of a layout is worse than the cost of one allocation.
        // A plate's ROLE is not part of its identity — the pool hands out whatever
        // index comes next, so a plate that framed a group last refresh can be a
        // title this one. Every role flag is cleared HERE rather than in each
        // binder, because the binder that forgets one is the bug: a title left
        // flagged as a group frame would be skipped by the layout AND would steal
        // the frame's slot, so the real frame kept whatever rect it had last.
        // ONE PLATE PER FRAME, WHILE NOTHING IS ASKING.
        //
        // Measured: building a plate and its twenty cells costs ~8 ms, and the
        // first open of a base with fifteen containers built 145 cells in one
        // frame - 66 ms, a visible lurch. Pooling already meant it happened once;
        // the problem was WHEN, which is always the moment you asked for the
        // window.
        //
        // Standing still is worse than moving, which sounds backwards and is the
        // clue: when you walk, the window is rebinding every 300 ms anyway and
        // the canvas is never idle, so one more burst goes unnoticed. Stand still
        // and the burst is the only thing happening.
        //
        // So the pool fills itself in the quiet, one plate a frame - the same
        // answer the crafting menu reached (and WarmFindItemCache beside
        // it): the work is not avoidable, only movable, and a millisecond a frame
        // for twenty frames is invisible where twenty in one frame is not.
        private const int WarmSheetPlates = 4;   // frame, sheet, title for each column
        private const int WarmSheetRows = 16;
        private const int WarmTarget = 20;   // vicinity bands, about the most containers in reach

        internal static void WarmPool()
        {
            if (_root == null)
            {
                return;
            }
            // A SHEET'S ROWS, one a frame, on the plates a sheet lands on (§6.20).
            // The first open of a fourteen-row sheet built its rows in the frame
            // you clicked - 75 ms - so they are built in the quiet instead, the
            // same answer the plates themselves reached.
            for (int i = 0; i < Plates.Count && i < WarmSheetPlates; i++)
            {
                Plate warm = Plates[i];
                if (warm.Rows == null) { warm.Rows = new List<SheetRow>(); }
                if (warm.Rows.Count < WarmSheetRows)
                {
                    SheetRow row = CreateSheetRow(warm);
                    row.Go.SetActive(false);
                    warm.Rows.Add(row);
                    return;
                }
            }
            if (_reachContent == null || ReachPlates.Count >= WarmTarget)
            {
                return;
            }
            Plate plate = CreatePlate(_reachContent);
            while (plate.Cells.Count < WarmCells)
            {
                plate.Cells.Add(CreateCell(plate));
            }
            HideCells(plate);
            plate.Window.Go.SetActive(false);
            ReachPlates.Add(plate);
        }

        private const int WarmCells = 20;

        // YOUR SHEET'S PAGES, BOUND ONCE INSIDE THE LOAD. The Social page's first
        // bind cost 163 ms (FirstOpenTimer): its bond and partner rows build their
        // planks and form pieces on first need, and its writers fill their caches -
        // all of it once per row, cheap ever after (8 ms). WarmPool's one-a-frame
        // answer does not reach it, since what a page builds is only known by
        // binding it. So each page is bound for real, right after the save loads,
        // where frames already run to seconds and one more piece goes unseen, and
        // the sheet is put back as it was in the same frame: nothing is drawn.
        // The rows stay on the plates your sheet takes when it opens alone.
        internal static void WarmSheetPages(ManagersScript mn)
        {
            if (mn == null || !EnsureBuilt(mn) || mn.gameMN == null || mn.gameMN.pMove == null)
            {
                return;
            }
            long spanAt = UI.FirstOpenTimer.Now();
            bool open = _ownSheetOpen;
            Aspect aspect = _ownAspect;
            _ownSheetOpen = true;
            foreach (Aspect a in OwnAspects)
            {
                _ownAspect = a;
                Refresh();
            }
            _ownSheetOpen = open;
            _ownAspect = aspect;
            Refresh();
            UI.FirstOpenTimer.Span("warm sheet pages", spanAt);
        }

        // For a plate that draws no cells at all. The bind paths that DO draw
        // them switch off their own surplus, which is cheaper because it touches
        // only the difference.
        private static void HideCells(Plate plate)
        {
            for (int i = 0; i < plate.Cells.Count; i++)
            {
                CellView c = plate.Cells[i];
                if (c.Go.activeSelf)
                {
                    c.Go.SetActive(false);
                }
            }
        }

        // WHAT A REFRESH TOOK AND DID NOT USE, let go once every bind has run: a
        // row past what its plate shows, and a sheet's mask on a plate no sheet
        // claimed. Done here rather than in TakePlate, because a plate bound to
        // the same sheet again would otherwise switch its rows off and back on
        // (TakePlate).
        private static void ReleaseUnclaimed()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                List<Plate> pool = pass == 0 ? Plates : ReachPlates;
                int used = pass == 0 ? _platesUsed : _reachPlatesUsed;
                for (int p = 0; p < used && p < pool.Count; p++)
                {
                    Plate plate = pool[p];
                    // a holder's portrait and summary, unless a vicinity bind claimed
                    // the plate this refresh
                    if (!plate.WasVicinity && (plate.VicMount != null || plate.FreeBack != null))
                    {
                        ShedVicinity(plate);
                    }
                    if (!plate.SheetMasked && plate.SheetMask != null && plate.SheetMask.enabled)
                    {
                        plate.SheetMask.enabled = false;
                    }
                    for (int i = plate.RowsShown; plate.Rows != null && i < plate.Rows.Count; i++)
                    {
                        if (plate.Rows[i].Go.activeSelf)
                        {
                            plate.Rows[i].Go.SetActive(false);
                        }
                    }
                }
            }
        }

        private static Plate TakePlate()
        {
            List<Plate> pool = _bindingReach ? ReachPlates : Plates;
            Transform parent = _bindingReach && _reachContent != null
                ? _reachContent : _root.transform;
            int used = _bindingReach ? _reachPlatesUsed : _platesUsed;
            while (used >= pool.Count)
            {
                pool.Add(CreatePlate(parent));
            }
            Plate plate = pool[used++];
            if (_bindingReach) { _reachPlatesUsed = used; } else { _platesUsed = used; }
            plate.IsViewport = false;
            plate.Group = GroupNone;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            plate.IsSharedBag = false;
            plate.IsLoadout = false;
            plate.Owner = null;
            plate.SheetMasked = false;   // the mask goes at the refresh's end, unless a sheet claims it (ReleaseUnclaimed)
            plate.WasVicinity = false;   // ...and so do a holder's portrait and summary
            if (plate.SheetTrack != null) { plate.SheetTrack.gameObject.SetActive(false); }
            plate.SheetContentH = 0f;
            plate.Window.Content.anchoredPosition = Vector2.zero;
            if (plate.Rail != null && plate.Rail.enabled)
            {
                plate.Rail.enabled = false;   // a frame's rail, opted in by BindGroupFrame alone
            }
            if (plate.PlateMask != null && plate.PlateMask.enabled)
            {
                plate.PlateMask.enabled = false;   // a name plate's crop, opted in by BindStyledTitle alone
            }
            if (plate.Close != null && plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(false);   // likewise its close chip
            }
            ShedLeather(plate);   // ...and the leather's head and section parts
            // A POOLED PLATE MUST NOT ARRIVE WEARING THE LAST ONE'S DECORATION.
            // The badge is turned on by exactly one bind path (the style-5 title
            // pill) and was turned off by that same path — which is no protection
            // at all, because the pool hands the same plate to the OTHER paths too.
            // Bind a container, and its title pill carries the container's own item
            // sprite as a badge; bind an NPC next, the same plate comes back as the
            // loadout, nothing there touches the badge, and it draws at its old
            // local spot — the left of the header, over the first slot's glyph. So
            // the harvest box's icon appeared to follow onto every NPC afterwards.
            //
            // Resetting at the SOURCE is what makes that class of bug impossible
            // rather than fixed: a bind path opts decoration IN, and no bind path
            // has to know what the previous one opted into.
            if (plate.Badge != null && plate.Badge.gameObject.activeSelf)
            {
                plate.Badge.gameObject.SetActive(false);
            }
            if (plate.Star != null && plate.Star.gameObject.activeSelf)
            {
                plate.Star.gameObject.SetActive(false);   // the name plate's star, same rule
            }
            if (plate.Tiles != null)
            {
                for (int i = 0; i < plate.Tiles.Count; i++)
                {
                    if (plate.Tiles[i].gameObject.activeSelf) { plate.Tiles[i].gameObject.SetActive(false); }
                }
            }
            HideWear(plate);   // the rim's wear band, same rule as the tiles
            if (plate.Acts != null)
            {
                plate.Acts.Show(false);   // ...and a thing's act row, opted in by BindActRow alone
            }
            if (plate.Band != null && plate.Band.gameObject.activeSelf)
            {
                plate.Band.gameObject.SetActive(false);   // ...and the collapsed frame's band, Layout's alone
            }
            // ...and the Field is the same bug again, written the same day the
            // comment above was read. It is opted into by the group-frame path
            // alone and sized only there, so a plate that framed a group and comes
            // back as a member keeps a cream rectangle at the FRAME's dimensions -
            // which paints a slab of parchment over half the screen and reads as
            // "the panel is enormous" rather than as a leftover.
            if (plate.Field != null && plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(false);
            }
            // ...and the dossier's paperclip, the same bug a fourth time: a target's
            // sheet frame that came back as the backpack's plate kept its clip,
            // parked at the old frame's top-right offset, far above the bag
            if (plate.Mark != null && plate.Mark.gameObject.activeSelf)
            {
                plate.Mark.gameObject.SetActive(false);
            }
            // The separator, for the third time: a hairline drawn in the gap
            // UNDER a grouped member. Bind turns it on and off, and BindGroupFrame
            // turned it off - but BindStyledTitle never did, so a plate that had
            // been a 10-wide container band and came back as an NPC's heading kept
            // the line, still active, still at its old local offset. That drew it
            // across the loadout slots underneath the title, at the WRONG WIDTH
            // for the band it appeared to belong to, which is the tell.
            if (plate.Separator != null && plate.Separator.gameObject.activeSelf)
            {
                plate.Separator.gameObject.SetActive(false);
            }
            // ...and the title's own two, moved here from Bind for the same
            // reason. Every decoration a bind path can opt INTO is now cleared in
            // exactly one place. A new one added later inherits the rule instead
            // of having to rediscover it.
            if (plate.Scroll != null && plate.Scroll.gameObject.activeSelf)
            {
                plate.Scroll.gameObject.SetActive(false);
            }
            // ...and the heading's buttons, opted into by ONE heading -
            // inheriting the rule stated above rather than rediscovering it.
            if (plate.Buttons != null)
            {
                for (int i = 0; i < plate.Buttons.Count; i++)
                {
                    if (plate.Buttons[i].gameObject.activeSelf)
                    {
                        plate.Buttons[i].gameObject.SetActive(false);
                    }
                }
            }
            // ...and the loadout SEAM's flag, the same class of bug measured as a
            // three-unit height change on the Focus frame when your column
            // toggles: the plate that becomes the Focus's title had
            // been a loadout, and its stale LoadoutEdge bought the seam's air.
            plate.LoadoutEdge = false;
            // ...and the loadout's TYPE ICONS, for the same reason and found the
            // same way: a sheet replacing bag + loadout shifts every
            // plate one role along, so the character's FRAME arrived wearing the
            // loadout's nine glyphs across its top, and BindGroupFrame never
            // touched them because no frame had ever been a loadout before.
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                if (plate.TypeIcons[i].gameObject.activeSelf)
                {
                    plate.TypeIcons[i].gameObject.SetActive(false);
                }
            }
            // ...and a sheet's rows, the newest decoration, under the same rule
            plate.IsSheet = false;
            plate.IsHeader = false;
            plate.IsCard = false;
            plate.IsRoster = false;
            // ...and the PROP: RepaintSheets asks it first, so the
            // roster's control row, which never sets it, repainted itself as the
            // Water line of the well the plate had last been
            plate.Prop = null;
            // ...and its rows are let go at the refresh's END, not here
            // (ReleaseUnclaimed): a sheet bound onto the plate it had keeps the
            // rows it shows, where switching them off here and on again in
            // BindSheet re-registered every graphic of a Social page - ninety
            // milliseconds a refresh (Forgejo #12)
            plate.RowsShown = 0;
            // ...and the CELLS, for the fifth time and the last. Every bind path
            // that draws cells switches off the ones past its own count - and that
            // is no protection at all, because the paths that draw NO cells never
            // run that loop. The viewport spacer is one: it sets Shown = 0 by hand,
            // never goes through Bind, and so came back wearing ten live cells from
            // whatever band it used to be. They sit at its top-left, which is
            // exactly the window's top row, and an empty cell drawn twice composites
            // to 0.21 alpha darker - the doubled band, measured to the byte.
            //
            // Cleared HERE because that is what this function is for: a bind path
            // opts cells IN, and no bind path has to remember what the last one
            // left on.
            //
            // WHAT IS CLEARED HERE IS THE SLOT, NOT THE OBJECT. This
            // used to switch every cell of every plate OFF, and the bind then
            // switched back on the ones it wanted - roughly 220 SetActive calls a
            // rebind, three times a second. GameObject.SetActive is Unity's most
            // expensive routine operation on a Graphic: it registers and
            // unregisters the thing with its canvas, so the cost grows with what
            // else is on that canvas. That is the whole of "it gets worse after I
            // open the crafting window", reported from play and visible in the
            // log as the same cell count costing 25x more.
            //
            // The invariant survives without the toggle. `Slot = null` is what
            // makes a stale cell unpickable, and it is a field write. Being
            // VISIBLE is handled where it always should have been: a path that
            // draws cells switches off the ones past its own count, and the three
            // paths that draw NONE - the group frame, the title, the viewport
            // spacer - call HideCells. The first two already did.
            for (int i = 0; i < plate.Cells.Count; i++)
            {
                plate.Cells[i].Slot = null;
            }
            if (plate.Rule != null && plate.Rule.gameObject.activeSelf)
            {
                plate.Rule.gameObject.SetActive(false);
            }
            return plate;
        }

        private static Plate CreatePlate(Transform parent)
        {
            _platesMade++;
            var plate = new Plate();
            plate.Window = UIWindow.Create(parent, "Plate", 100f, 40f, darkCard: true);
            plate.Back = plate.Window.Go.GetComponent<Image>();
            plate.Edge = plate.Window.Go.GetComponent<Outline>();
            // plates anchor to the bottom-right corner and grow upward
            plate.Window.Rect.anchorMin = new Vector2(1f, 0f);
            plate.Window.Rect.anchorMax = new Vector2(1f, 0f);
            plate.Window.Rect.pivot = new Vector2(1f, 0f);
            plate.Header = Theme.MakeText(plate.Window.Content, "Label", Theme.FontCaption,
                Theme.TextDim, TextAlignmentOptions.MidlineLeft);
            plate.Header.enableWordWrapping = false;
            plate.Window.Go.SetActive(false);
            return plate;
        }

        private static CellView CreateCell(Plate plate)
        {
            _cellsMade++;
            return NewCellView(plate.Window.Content);
        }

        private static CellView NewCellView(Transform parent)
        {
            var cell = new CellView();
            // A NEW CELL HAS DRAWN NOTHING, and the memo must be able to say so.
            // Left at its default the record reads key null, stack 0, life 0, not
            // starred - which is precisely the signature of an EMPTY SLOT, so the
            // first bind of every cell matched the memo and returned before
            // painting anything. The cell then kept the white frame it was born
            // with (reported from play). -1 is a stack no slot can
            // hold, so the first bind always misses and always paints.
            cell.DrewStack = -1;
            cell.Go = Owned.New("Cell");
            cell.Rect = (RectTransform)cell.Go.transform;
            cell.Rect.SetParent(parent, false);
            cell.Box = cell.Go.AddComponent<Image>();
            // the game's slot frame until the first bind skins the cell as its
            // window wears it: the leather (LeatherCell), the vicinity's, a crate
            cell.Box.sprite = Theme.SlotFrame;
            cell.Box.raycastTarget = false;
            var iconGo = Owned.New("Icon");
            iconGo.transform.SetParent(cell.Go.transform, false);
            cell.Icon = iconGo.AddComponent<Image>();
            cell.Icon.raycastTarget = false;
            cell.Icon.preserveAspect = true;
            Theme.PlaceSlotIcon(cell.Icon, CellBox);
            cell.Count = Theme.MakeText(cell.Go.transform, "Count", Theme.FontCaption,
                Theme.TextCream, TextAlignmentOptions.BottomRight);
            cell.Count.enableWordWrapping = false;
            Theme.Place(cell.Count, 0f, CellBox - 10f, CellBox - 1f, 10f);
            cell.Highlight = cell.Go.AddComponent<Outline>();
            cell.Highlight.effectDistance = new Vector2(2f, -2f);
            cell.Highlight.enabled = false;
            // top-left: the stack count owns bottom-right, and the art is centred
            var starGo = Owned.New("Star");
            starGo.transform.SetParent(cell.Go.transform, false);
            cell.Star = starGo.AddComponent<Image>();
            cell.Star.sprite = Theme.Star;
            cell.Star.color = Theme.AccentGold;
            cell.Star.raycastTarget = false;
            Theme.Place(cell.Star, 0.5f, 0.5f, StarSize, StarSize);
            starGo.SetActive(false);
            cell.ToolMark = MakeToolBadge(cell.Go.transform, "AssignedTool", "tools",
                new Color32(125, 225, 218, 255));
            Theme.Place(cell.ToolMark, CellBox - StarSize - 0.5f, 0.5f, StarSize, StarSize);
            cell.ToolMark.gameObject.SetActive(false);
            return cell;
        }

        // ---------- open/close ----------

        private static void Open(ManagersScript mn)
        {
            if (!EnsureBuilt(mn))
            {
                return;
            }
            _ownOpen = true;
            _root.SetActive(true);
            SyncSharedBag(mn);
            Refresh();
        }

        internal static void Close()
        {
            _ownOpen = false;
            _forceRefresh = true;
            _nextRefresh = 0f;
            if (IsOpen && !_focusShown && !_ownSheetOpen && !_reachOpen)
            {
                _root.SetActive(false);
            }
            SyncSharedBag(_mn);
            if (_mn != null)
            {
                SyncVanillaContainerPanel(_mn);
            }
        }

        // Vanilla's bottom bar, bound to itemSlot[0..baseCount), is kept hidden.
        private static void SyncSharedBag(ManagersScript mn)
        {
            GameObject bar = FindSharedBag(mn);
            if (bar == null)
            {
                return;
            }
            // We draw the Shared Bag ourselves now, so vanilla's bar is simply
            // gone - it was the last vanilla surface we were leaning on, and
            // leaning on it is what let vanilla's own gates refuse our clicks.
            if (bar.activeSelf)
            {
                bar.SetActive(false);
            }
        }

        // Talking to an NPC makes NPCMenuOpen pop the vanilla container panel
        // over our own. It cannot simply be deactivated: CheckNPC, SearchNPC and
        // UseProp all loop on subInventory.activeSelf, so deactivating it closes
        // the NPC dialog on the next frame (recon §7.2). The documented answer is
        // to keep the object ACTIVE as a lifetime token and hide it visually —
        // which also leaves the mirror slots intact, and our target column reads
        // through those.
        private static void SyncVanillaContainerPanel(ManagersScript mn)
        {
            if (mn.inventory == null || mn.inventory.subInventory == null)
            {
                return;
            }
            if (_vanillaContainerPanel == null)
            {
                _vanillaContainerPanel = mn.inventory.subInventory.GetComponent<CanvasGroup>()
                    ?? mn.inventory.subInventory.AddComponent<CanvasGroup>();
            }
            // An intercepted container leaves the panel active but EMPTY (WI-4):
            // there is nothing in it to show and showing its frame would be a lie,
            // so that case is not negotiable. Otherwise the older rule stands —
            // hidden whenever the Focus is standing in for it, independent of
            // whether your own bags are open. A prop we do not replace keeps its
            // panel either way.
            // ...and asked LIVE, not only from the Tick's answer. Vanilla opens a
            // prop inside PlayerMove's Update, which can run after ours, and the
            // panel it raised then showed for the rest of that frame (the vanilla
            // inventory flashed for a moment before ours replaced it, on the Sand bag
            // and the Spider Closet). LateSync asks again once every Update has run.
            bool standingIn = _focusShown || StandsIn(mn);
            // ...and a PERSON held with the unit up never shows it: NPCMenuOpen loads
            // their bag into it, and our bag window is where their bag is (D120),
            // open or closed - it showed as a bare vanilla panel under the unit
            // whenever neither the sheet nor the bag window was up
            bool person = Settings.TheUnit.Value && _heldVar == Held.Menu;
            // ...and nor does a thing we stand in for (D156): its grid is our
            // Inventory door's window, open or closed - a rack we do not replace
            // keeps vanilla's, as Focus refuses it
            bool thing = Settings.TheUnit.Value && _heldVar == Held.Prop && Focus() != null;
            bool hide = VanillaContainerUI.Intercepted != null
                || standingIn || person || thing;

            float alpha = hide ? 0f : 1f;
            if (!Mathf.Approximately(_vanillaContainerPanel.alpha, alpha))
            {
                _vanillaContainerPanel.alpha = alpha;
                _vanillaContainerPanel.blocksRaycasts = !hide;
                _vanillaContainerPanel.interactable = !hide;
            }
        }

        // Would the window stand in for what is open right now - the Tick's rule
        // for _focusShown, asked of this moment
        private static bool StandsIn(ManagersScript mn)
        {
            if (mn.gameMN == null)
            {
                return false;
            }
            object focus = ResolveTarget(mn);
            return focus != null && IsReplaceable(focus) && SheetOpenFor(focus);
        }

        private static GameObject FindSharedBag(ManagersScript mn)
        {
            // fake-null after a scene reload: rediscover rather than hold a
            // reference into a destroyed scene
            if (_vanillaSharedBag != null)
            {
                return _vanillaSharedBag;
            }
            if (mn == null || mn.uiMN == null || mn.uiMN.itemDescPanel == null)
            {
                return null;
            }
            Canvas canvas = mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }
            foreach (Transform t in canvas.rootCanvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "InventoryPanel")
                {
                    _vanillaSharedBag = t.gameObject;
                    // log what else rides on this object: if the health bar or
                    // anything unrelated is a child, hiding it would take that
                    // with it and the screenshot alone would not say why
                    var sb = new System.Text.StringBuilder(
                        "Inventory: driving the vanilla bottom bar as the Shared Bag; children:");
                    foreach (Transform child in t)
                    {
                        sb.Append(' ').Append(child.name);
                    }
                    Plugin.Log.LogInfo(sb.ToString());
                    return _vanillaSharedBag;
                }
            }
            return null;
        }

        // The bag icon and the I key arrive here now (WI-4): vanilla's two
        // container icons are one icon, and it opens this.
        internal static void Toggle()
        {
            _ownOpen = !_ownOpen;
        }

        // What the player is dealing with, if it is something we draw. The
        // pocket_01 re-implementation needs the same answer the window shows.
        internal static object Focus()
        {
            if (_mn == null || _mn.gameMN == null)
            {
                return null;
            }
            object focus = ResolveTarget(_mn);
            return focus != null && IsReplaceable(focus) ? focus : null;
        }

        // The setup panel changed a setting. Same rebuild as Invalidate, plus
        // the scroll anchor: a family switched off can take the container the view
        // was pinned to out of the list with it.
        internal static void OnConfigChanged()
        {
            _reachAnchor = null;
            // the family modes decide what the scan admits, so its answer is stale
            InvalidateReachScan();
            Invalidate();
        }

        // 10Hz is free with two containers in the pool and is not free with
        // twenty: a rebind lays out every cell of every band, so its cost is
        // linear in what is on screen while the interval was a constant. Scaling
        // the interval with the pool keeps the WORK per second roughly flat
        // instead of the rate, which is the thing that actually stalls.
        //
        // The floor is the old constant, so nothing that was fast gets slower.
        // The ceiling is half a second, past which a window that is meant to
        // track what you are standing next to stops feeling attached to you.
        private static float RefreshInterval()
        {
            return Mathf.Clamp(0.1f + 0.012f * _nearbyShown, 0.1f, 0.5f);
        }

        // Says so when a rebind is expensive, because "it feels slow in a big
        // base" and "the rebind is what is slow" are different claims and only
        // one of them can be acted on. Throttled to once every few seconds: a
        // stall that logs every frame is its own stall.
        private static float _nextSlowReport;

        private static void ReportSlowRefresh(float seconds)
        {
            if (seconds < 0.008f || Time.realtimeSinceStartup < _nextSlowReport)
            {
                return;
            }
            _nextSlowReport = Time.realtimeSinceStartup + 3f;
            Plugin.Log.LogWarning(
                $"InventoryWindow: rebind took {seconds * 1000f:0.0} ms "
                + $"({_nearbyShown} in reach of {_nearbyTotal}, "
                + $"{_platesUsed} plate(s) + {_reachPlatesUsed} pooled) "
                + $"| reach-bind {_reachMs:0.0} ms, cells {_cellMs:0.0} "
                + $"(icon {_iconMs:0.0} / text {_textMs:0.0}) ms, "
                + $"layout {_layoutMs:0.0} ms, {_cellsBound} cell(s), "
                + $"{_reshapes} reshape(s), pre-reach {_preReachMs:0.0} ms, "
                + $"built {_platesMade} plate(s)/{_cellsMade} cell(s) "
                + $"- next in {RefreshInterval() * 1000f:0} ms");
        }

        // A rebind that costs 18 ms is a dropped frame; knowing WHICH 18 ms is
        // the difference between a fix and another guess. The last stall was found
        // by measuring rather than reasoning and this is the same instrument one
        // level down: the phases inside a rebind, and how many cells it touched.
        private static int _cellsBound;
        private static int _itemLookups;
        private static float _reachMs;
        private static float _cellMs;
        // Inside a cell there are two suspects and they want opposite fixes: the
        // ICON half (sprite lookup, Image.sprite, enabled) and the TEXT half (a
        // formatted string handed to a TMP, which regenerates a mesh). The same
        // cell count costing 25x more after the craft panel has existed says one
        // of them scales with something outside this window.
        private static float _iconMs;
        // 84.7 ms with nothing in reach, five plates and thirty-seven cells says
        // the cost is neither per container nor per cell - it is fixed work, and
        // the only fixed work that could be that large is BUILDING the objects.
        private static int _platesMade;
        private static int _cellsMade;
        private static float _preReachMs;
        private static float _textMs;
        private static int _reshapes;
        private static float _layoutMs;
        private static int _signature;
        private static bool _forceRefresh = true;

        // UI.LocLabels: the language changed - every open plate, sheet and row re-renders
        internal static void LanguageChanged()
        {
            _forceRefresh = true;
            _nextRefresh = 0f;
        }
        private static float _fullRefreshBy;

        // What the window would DRAW, as an int. Reads the same slots a rebind
        // reads and touches no RectTransform, so it is the cheap half of the
        // work and answers the only question that matters: has anything changed.
        //
        // Contents AND shape: a stack going 4 -> 3 changes a number on screen and
        // a container gaining a slot changes the layout, so both are in. Nothing
        // about geometry is - CellBox and the settings live behind Invalidate,
        // which forces regardless.
        private static int Mix(int h, int v)
        {
            unchecked { return (h * 16777619) ^ v; }
        }

        private static int HashSlots(int h, ItemSlot[] slots, int count)
        {
            return HashSlots(h, slots, 0, count);
        }

        private static int HashSlots(int h, ItemSlot[] slots, int from, int count)
        {
            if (slots == null)
            {
                return Mix(h, -1);
            }
            h = Mix(h, count);
            for (int i = 0; i < count && from + i < slots.Length; i++)
            {
                ItemSlot s = slots[from + i];
                if (s == null)
                {
                    h = Mix(h, 0);
                    continue;
                }
                h = Mix(h, s.itemKey != null ? s.itemKey.GetHashCode() : 0);
                h = Mix(h, s.stack);
                // quantised: durability and happiness are DRAWN as integers, so
                // a change too small to show must not cost a rebind
                h = Mix(h, (int)s.itemLife);
            }
            return h;
        }

        private static int HashOwner(int h, CommonStates who)
        {
            if (who == null)
            {
                return Mix(h, -2);
            }
            h = Mix(h, who.GetInstanceID());
            h = Mix(h, (int)who.employ);   // Favorite is an employ role: the star follows it
            if (who.bag != null)
            {
                h = HashSlots(h, who.bag.slots,
                    who.bag.slots != null ? who.bag.slots.Length : 0);
            }
            return HashSlots(h, who.equip, who.equip != null ? who.equip.Length : 0);
        }

        private static int PoolSignature(ManagersScript mn)
        {
            unchecked
            {
                int h = (int)2166136261;
                // Assignment can change without changing any slot's contents.
                h = Mix(h, ToolBelt.Revision);
                h = Mix(h, (_ownOpen ? 1 : 0) | (_focusShown ? 2 : 0)
                        | (_reachOpen ? 4 : 0) | (_barShown ? 8 : 0));
                h = Mix(h, Mathf.RoundToInt(_reachScroll));
                // The signature is "what would be drawn", and the query changes
                // that in two ways at once — which containers appear, and how
                // every cell in them is painted. Version rather than the string:
                // one int, and it already accounts for the language flipping.
                // Only the slot's own query: typing in the crafting filter
                // no longer rebinds this window.
                h = Mix(h, _reachOpen ? _filter.Version : 0);
                InventoryManager inv = mn.inventory;
                h = HashSlots(h, inv.itemSlot, inv.baseCount);
                h = HashOwner(h, mn.gameMN.pMove != null ? mn.gameMN.pMove.common : null);

                if (_target is CommonStates npc)
                {
                    h = HashOwner(h, npc);
                }
                else if (_target is InventorySlot chest && chest != null)
                {
                    h = Mix(h, chest.GetInstanceID());
                    h = HashSlots(h, chest.slots,
                        chest.slots != null ? chest.slots.Length : 0);
                    // a viewed rack's contents are in the MIRROR while its panel
                    // is up and the real array reads empty: hash what is drawn,
                    // or the plate never learns the NPC has left the wall
                    if (chest.slots != null && VanillaContainerUI.MirrorsLive(inv)
                        && ReferenceEquals(inv.tmpSubInventory, chest))
                    {
                        h = HashSlots(h, inv.itemSlot, 50, chest.slots.Length);
                    }
                }
                for (int i = 0; i < ReachScan.Count; i++)
                {
                    InventorySlot c = ReachScan[i];
                    if (c == null)
                    {
                        h = Mix(h, -3);
                        continue;
                    }
                    h = Mix(h, c.GetInstanceID());
                    h = HashSlots(h, c.slots, c.slots != null ? c.slots.Length : 0);
                }
                return h;
            }
        }

        // Something changed a container's contents or SHAPE behind our back.
        internal static void Invalidate()
        {
            _forceRefresh = true;
            _nextRefresh = 0f;
            if (IsOpen)
            {
                Refresh();
            }
        }

        // What CloseOwnedWindows would take off the screen, asked before it runs: a
        // press that closes something is not also vanilla's attack (CloseAll.Spent).
        // Keep the two in step. The target's sheet counts only on a person - the
        // flags outlive a target for a tick (LapseDoors) - and a
        // container's window is the target's own.
        internal static bool OwnedWindowsShown()
        {
            return SplitDialog.IsOpen || MatrixShown || _moreActsOpen
                || _ownOpen || _ownSheetOpen || _reachOpen || HudOpen("craft")
                || ((_sheetOpen || _bagOpen) && Focus() is CommonStates);
        }

        // The world-focus key's half (CloseAll.WorldFocus): every window of this
        // window system at once - your bag and sheet, the target's bag and sheet
        // (remembered as closed), the left slot, and what hangs off them. The target
        // itself stays: ending it calls vanilla's NPC close, which is what turned
        // Close All into a softlock in the middle of a scene.
        internal static void CloseOwnedWindows()
        {
            if (SplitDialog.IsOpen)
            {
                SplitDialog.Close();
            }
            if (MatrixShown)
            {
                MatrixClose();
            }
            if (_moreActsOpen)
            {
                _moreActsOpen = false;
                PaintMoreActs();
            }
            _sheetOpen = false;
            _bagOpen = false;
            _ownSheetOpen = false;
            CloseReach();
            if (_mn != null && _mn.uiMN != null && HudOpen("craft"))
            {
                _mn.uiMN.LeftPanelState(0);
            }
            if (_ownOpen)
            {
                Close();
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        internal static bool TryEscapeClose()
        {
            if (SplitDialog.IsOpen)
            {
                SplitDialog.Close();
                return true;
            }
            if (MatrixShown)
            {
                MatrixClose();
                return true;
            }
            if (_ownSheetOpen)
            {
                ToggleOwnDetails();
                return true;
            }
            // the target windows' flags can outlive a target for a beat
            // (CloseTargetDoors), so this answers to them only while a person is
            // up - otherwise it would fold a window nobody can see and eat the key
            bool person = _focusShown && _target is CommonStates;
            if (_sheetOpen && person)
            {
                CloseGroup(GroupCharacter);
                return true;
            }
            if (_bagOpen && person && PocketsOpen((CommonStates)_target))
            {
                CloseGroup(GroupBag);
                return true;
            }
            if (!_ownOpen)
            {
                return false;
            }
            Close();
            return true;
        }

        internal static void OnPlayerMoved()
        {
            if (IsOpen)
            {
                Refresh();
            }
        }

        // A held item follows the cursor on the vanilla hoverSlot. Our root is the
        // last child of the canvas, so our plates drew OVER it and the thing you
        // were dragging disappeared behind a bag. Whatever is in hand must be the
        // topmost thing on screen — raise the hoverSlot's branch above ours rather
        // than lowering ourselves, which would put us under every vanilla panel.
        // The held item is vanilla's hoverSlot, and driving it rather than inventing
        // our own is what lets one grab cross our plates and vanilla's slots alike
        // (WI-3).
        //
        // This once STRIPPED the slot's background, on the reasoning that a black
        // square makes the thing in your hand read as a tile prised out of a grid,
        // and only the art and the count should travel with the cursor. That is
        // reversed, because the premise expired: back then a
        // filled slot and an empty one looked alike, so the carried item losing its
        // backing cost nothing. Now that an occupied slot has a dark shade,
        // an item lifted out sheds the one thing that was making it legible and
        // becomes a bare sprite over the world — worst exactly for the thin, dark
        // art that the shade existed to rescue. So the cell travels WITH the item.
        //
        // Not vanilla's black square, though: the hoverSlot's own Image is retargeted
        // to OUR frame and given the same interior shade a filled cell gets, so what
        // you are dragging is the cell you picked it out of, not a second look.
        //
        // Alpha, not `enabled`, and never a raycastTarget change: this Image may be
        // what the game's own OnUI raycast sees, and whether a held item counts as
        // "over the UI" decides whether the next click drops it into the world.
        // That behaviour stays exactly as it was through both versions of this.
        // THE ITEM TILE — the one look for "here is an item", now worn by three
        // things: a filled bag cell, whatever is on the cursor, and the pickup
        // toast. It is our harvested slot frame plus the occupancy shade, and
        // the shade goes on as a CHILD at sibling index 0 because children draw
        // after their parent: that lands it over the frame and under the art and
        // the count, which is exactly the stack a cell has.
        //
        // Takes the frame Image the caller already owns rather than building a
        // GameObject, so it can restyle something vanilla made (the hoverSlot's
        // backdrop, the toast prefab's imageBG) as easily as something of ours.
        // Returns the shade so a caller that needs to toggle it — a cell, whose
        // slot may be empty — can, while callers that are always full ignore it.
        internal static Image ApplyItemTile(Image frame)
        {
            if (frame == null || Theme.SlotFrame == null)
            {
                return null;
            }
            frame.sprite = Theme.SlotFrame;
            frame.type = Image.Type.Simple;
            frame.color = Color.white;
            Transform found = frame.transform.Find(TileShadeName);
            Image shade = found != null ? found.GetComponent<Image>() : null;
            if (shade == null)
            {
                var shadeGo = Owned.New(TileShadeName);
                shadeGo.transform.SetParent(frame.transform, false);
                shadeGo.transform.SetAsFirstSibling();
                shade = shadeGo.AddComponent<Image>();
                shade.sprite = Theme.RoundedFill;
                shade.type = Image.Type.Sliced;
                shade.raycastTarget = false;
                var rect = (RectTransform)shadeGo.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(FillInset, FillInset);
                rect.offsetMax = new Vector2(-FillInset, -FillInset);
            }
            shade.color = TileShadeColor;
            return shade;
        }

        private const string TileShadeName = "TileShade";

        // Where a picked-up item should look like it landed. The exact cell
        // now holding it, because we HAVE that — the Shared Bag is ours and its
        // bar is sticky, so the common case is a real slot rather than a gesture
        // at a panel. The Shared Bag wins over the character bag when both hold
        // the key: that is vanilla's own fill order, so it is where the new one
        // most likely went.
        //
        // ALWAYS A SLOT, never a container. Aiming at a panel's middle was the
        // first version and it is a worse answer than it looks: the Shared Bag IS
        // the always-visible bottom bar, so "somewhere in there" is available
        // precisely when the exact slot also is. A flight that points at a whole
        // panel reads as the effect not knowing.
        //
        // Our own cells first; vanilla's bar slots (`inv.itemSlot[0..baseCount]`)
        // are the fallback, though with its bar hidden they are never on screen.
        //
        // Returns null when the item landed somewhere not on screen (the character
        // bag, with our window shut); the caller then aims at the bag button, which
        // is the honest answer — "into your pack, over there". Deliberately not a
        // search of every container in reach: a chest across the camp is not where
        // your pickup went, and pointing at one would be a lie told confidently.
        internal static RectTransform LandingSpot(ManagersScript mn, string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey))
            {
                return null;
            }
            RectTransform other = null;
            for (int p = 0; p < _platesUsed && p < Plates.Count; p++)
            {
                Plate plate = Plates[p];
                // your own column only: a chest in the vicinity or target window
                // holding the same item is not where the pickup went
                if (plate.IsTitle || (plate.Column != OwnColumn && !plate.IsSharedBag) || plate.Window == null
                    || plate.Window.Go == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int c = 0; c < plate.Cells.Count; c++)
                {
                    CellView cell = plate.Cells[c];
                    if (cell.Go == null || !cell.Go.activeInHierarchy || cell.Slot == null
                        || cell.Slot.itemKey != itemKey)
                    {
                        continue;
                    }
                    var rect = (RectTransform)cell.Go.transform;
                    if (plate.IsSharedBag)
                    {
                        return rect;    // first choice, and vanilla fills it first
                    }
                    if (other == null)
                    {
                        other = rect;
                    }
                }
            }
            return other ?? VanillaSharedBagSlot(mn, itemKey);
        }

        // The cell showing this item in THIS container's grid, when the grid is on
        // screen - the flight's other end (pickups also fly towards the cells in
        // the target window). Cells hold the container's real
        // slots by reference, so a stack that just landed is found before the
        // next repaint. Null when the container is not drawn or the item is not in it.
        internal static RectTransform CellFor(InventorySlot container, ItemData item)
        {
            if (container == null || item == null || _mn == null || _mn.itemMN == null)
            {
                return null;
            }
            for (int p = 0; p < _platesUsed && p < Plates.Count; p++)
            {
                Plate plate = Plates[p];
                if (plate.Container != container || plate.IsTitle || plate.Window == null
                    || plate.Window.Go == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int c = 0; c < plate.Cells.Count; c++)
                {
                    CellView cell = plate.Cells[c];
                    // the item record is cached per key, so the same key is the same
                    // record - no key on ItemData is needed to compare
                    if (cell.Go != null && cell.Go.activeInHierarchy && cell.Slot != null
                        && !string.IsNullOrEmpty(cell.Slot.itemKey)
                        && ReferenceEquals(_mn.itemMN.FindItem(cell.Slot.itemKey), item))
                    {
                        return (RectTransform)cell.Go.transform;
                    }
                }
            }
            return null;
        }

        // Hover preview for the quick-stack button: mark the Shared Bag subset
        // that would leave, and dim the occupied cells that would stay. Called
        // AFTER Tick's highlight clear, so it wins for the rest of the frame.
        internal static void PaintStackPreview(List<ItemSlot> moving, List<ItemSlot> staying)
        {
            if (moving == null)
            {
                moving = new List<ItemSlot>();
            }
            if (staying == null)
            {
                staying = new List<ItemSlot>();
            }
            for (int p = 0; p < _platesUsed && p < Plates.Count; p++)
            {
                Plate plate = Plates[p];
                if (!plate.IsSharedBag)
                {
                    continue;
                }
                for (int i = 0; i < plate.Shown && i < plate.Cells.Count; i++)
                {
                    PaintCell(plate.Cells[i], moving, staying);
                }
            }
            PaintVanillaBar(_mn, moving, staying);
        }

        internal static void ClearStackPreview()
        {
            for (int p = 0; p < _platesUsed && p < Plates.Count; p++)
            {
                List<CellView> cells = Plates[p].Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].Icon != null)
                    {
                        cells[i].Icon.color = Color.white;
                    }
                    if (cells[i].Highlight != null)
                    {
                        cells[i].Highlight.enabled = false;
                    }
                }
            }
            ClearVanillaBarPreview(_mn);
        }

        private static void PaintCell(CellView cell, List<ItemSlot> moving, List<ItemSlot> staying)
        {
            if (cell == null || cell.Go == null || !cell.Go.activeInHierarchy)
            {
                return;
            }
            // painted from outside the bind, so the bind must not believe its memo
            cell.DrewKey = null;
            bool will = cell.Slot != null && moving.Contains(cell.Slot);
            bool wont = cell.Slot != null && staying.Contains(cell.Slot);
            if (will)
            {
                cell.Highlight.effectColor = Theme.Positive;
                cell.Highlight.enabled = true;
                if (cell.Icon != null)
                {
                    cell.Icon.color = Color.white;
                }
            }
            else if (wont)
            {
                cell.Highlight.enabled = false;
                if (cell.Icon != null)
                {
                    cell.Icon.color = StayTint;
                }
            }
        }

        private static Image SlotArt(ItemSlot slot)
        {
            if (slot == null)
            {
                return null;
            }
            Transform child = slot.transform.Find("itemImage");
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static readonly Color StayTint = new Color(1f, 1f, 1f, 0.38f);
        private static readonly List<Outline> VanillaPreview = new List<Outline>();

        private static void PaintVanillaBar(ManagersScript mn, List<ItemSlot> moving,
            List<ItemSlot> staying)
        {
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null || inv.itemSlot == null)
            {
                return;
            }
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot slot = inv.itemSlot[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }
                bool will = moving.Contains(slot);
                bool wont = staying.Contains(slot);
                Image art = SlotArt(slot);
                if (art != null)
                {
                    art.color = wont && !will ? StayTint : Color.white;
                }
                Outline outline = slot.GetComponent<Outline>();
                if (will)
                {
                    if (outline == null)
                    {
                        outline = slot.gameObject.AddComponent<Outline>();
                        outline.effectDistance = new Vector2(2f, -2f);
                        VanillaPreview.Add(outline);
                    }
                    outline.effectColor = Theme.Positive;
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }

        private static void ClearVanillaBarPreview(ManagersScript mn)
        {
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv != null && inv.itemSlot != null)
            {
                for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
                {
                    ItemSlot slot = inv.itemSlot[i];
                    Image art = SlotArt(slot);
                    if (art != null)
                    {
                        art.color = Color.white;
                    }
                }
            }
            for (int i = 0; i < VanillaPreview.Count; i++)
            {
                if (VanillaPreview[i] != null)
                {
                    VanillaPreview[i].enabled = false;
                }
            }
        }

        // The vanilla bottom bar's own slot objects. These are real UI slots —
        // StackTextChange(i) drives slot i's on-screen number — so slot i is a
        // thing with a rect we can point at.
        private static RectTransform VanillaSharedBagSlot(ManagersScript mn, string itemKey)
        {
            InventoryManager inv = mn != null ? mn.inventory : null;
            if (inv == null || inv.itemSlot == null)
            {
                return null;
            }
            for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                ItemSlot slot = inv.itemSlot[i];
                if (slot != null && slot.itemKey == itemKey
                    && slot.gameObject.activeInHierarchy)
                {
                    return (RectTransform)slot.transform;
                }
            }
            return null;
        }

        // the black wash over an occupied tile: none, since the slot sprite's
        // interior is already near-opaque
        internal static readonly Color TileShadeColor = Color.clear;

        // THE ITEM ON THE CURSOR IS DRAWN AS OURS: its icon and its count as every
        // window prints it (BarCount, SetCellReadout), with no cell under it, half
        // as large again for being lifted. The game's hoverSlot still carries the
        // item and follows the pointer; its frame, its art and its number
        // (StackTextChange's own colours) are hidden.
        private const float HeldScale = 1.5f;
        private static CellView _held;

        private static void StyleHeldItem(ManagersScript mn)
        {
            ItemSlot hover = mn.inventory != null ? mn.inventory.hoverSlot : null;
            if (hover == null || !hover.gameObject.activeSelf)
            {
                return;
            }
            if (_held == null || _held.Go == null)
            {
                _held = NewHeldCell(hover.transform);
            }
            HideVanillaHeld(hover);
            bool filled = !string.IsNullOrEmpty(hover.itemKey);
            int life = filled ? (int)hover.itemLife : 0;
            int stack = filled ? hover.stack : 0;
            if (ReferenceEquals(_held.DrewKey, hover.itemKey) && _held.DrewStack == stack
                && _held.DrewLife == life)
            {
                return;
            }
            _held.DrewKey = hover.itemKey;
            _held.DrewStack = stack;
            _held.DrewLife = life;
            Sprite sprite = null;
            if (filled)
            {
                sprite = hover.itemImage;
                if (sprite == null && mn.itemMN != null)
                {
                    ItemData data = mn.itemMN.FindItem(hover.itemKey);
                    sprite = data != null ? data.itemSprite : null;
                }
            }
            _held.Icon.sprite = sprite;
            _held.Icon.enabled = sprite != null;
            _held.Icon.color = Color.white;
            // the item and its count, never the cell's bed: the bed is where an item
            // lies in a window, and this one is in your hand
            _held.Box.enabled = false;
            BarCount(_held, true);
            if (filled)
            {
                SetCellReadout(_held, hover, true);
            }
            else
            {
                _held.Count.text = string.Empty;
            }
        }

        // centred on its parent, which follows the pointer
        private static CellView NewHeldCell(Transform parent)
        {
            CellView cell = NewCellView(parent);
            cell.Rect.anchorMin = cell.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            cell.Rect.pivot = new Vector2(0.5f, 0.5f);
            cell.Rect.anchoredPosition = Vector2.zero;
            cell.Rect.sizeDelta = new Vector2(CellBox, CellBox);
            cell.Rect.localScale = new Vector3(HeldScale, HeldScale, 1f);
            cell.Go.SetActive(true);
            return cell;
        }

        // A BINDING LIFTED OFF THE ACTION BAR is drawn as the held item is: the
        // same cell, scale and outlined count, centred on the pointer (Forgejo
        // #173). It is not routed through hoverSlot - a binding is not an item -
        // so the bar places it; this only paints it, with what the bar's cell
        // showed when it was lifted.
        private static CellView _barGhost;

        internal static RectTransform PaintBarGhost(Transform parent, Sprite sprite, Color tint,
            string count, Color countColor)
        {
            if (_barGhost == null || _barGhost.Go == null)
            {
                _barGhost = NewHeldCell(parent);
                _barGhost.Box.enabled = false;
                BarCount(_barGhost, true);
            }
            else if (_barGhost.Rect.parent != parent)
            {
                _barGhost.Rect.SetParent(parent, false);
            }
            _barGhost.Icon.sprite = sprite;
            _barGhost.Icon.enabled = sprite != null;
            _barGhost.Icon.color = tint;
            _barGhost.Count.text = count ?? string.Empty;
            _barGhost.Count.color = countColor;
            if (!_barGhost.Go.activeSelf)
            {
                _barGhost.Go.SetActive(true);
            }
            return _barGhost.Rect;
        }

        internal static void HideBarGhost()
        {
            if (_barGhost != null && _barGhost.Go != null && _barGhost.Go.activeSelf)
            {
                _barGhost.Go.SetActive(false);
            }
        }

        // the game's own parts of the held item: its frame, its art and its number
        // (and the occupancy shade an older build of ours gave it)
        private static void HideVanillaHeld(ItemSlot hover)
        {
            Image frame = hover.GetComponent<Image>();
            if (frame != null && frame.enabled)
            {
                frame.enabled = false;
            }
            for (int i = 0; i < hover.transform.childCount; i++)
            {
                Transform child = hover.transform.GetChild(i);
                if (child == _held.Rect)
                {
                    continue;
                }
                Graphic art = child.GetComponent<Graphic>();
                if (art != null && art.enabled)
                {
                    art.enabled = false;
                }
            }
        }

        private static void KeepHeldItemOnTop(ManagersScript mn)
        {
            ItemSlot hover = mn.inventory != null ? mn.inventory.hoverSlot : null;
            if (hover == null || !hover.gameObject.activeSelf || _root == null)
            {
                return;
            }
            Transform rootParent = _root.transform.parent;
            Transform branch = hover.transform;
            while (branch != null && branch.parent != rootParent)
            {
                branch = branch.parent;
            }
            if (branch != null && branch.GetSiblingIndex() < _root.transform.GetSiblingIndex())
            {
                branch.SetAsLastSibling();
            }
        }

        // ---------- slot resolution ----------

        // While the vanilla panel owns a container its contents live in mirror
        // slots 50+ and the real array reads EMPTY (recon §7.2). This is the
        // game's own rule (InventoryManager.GetCurrentItemSlot) applied per cell
        // so no array is allocated per frame — without it a targeted NPC's bag
        // would always render empty, since NPCMenuOpen mirrors it on contact.
        // WI-4 intercepts those loads for plain storage, so this path now only
        // catches the props that keep the vanilla panel; MirrorsLive is the test
        // because an intercepted panel is active but holds nothing.
        private static ItemSlot Resolve(ItemSlot[] array, int[] map, InventorySlot mirrorOf,
            int i, out int vanillaSlotId)
        {
            vanillaSlotId = -1;
            int index = map != null ? map[i] : i;
            if (mirrorOf != null && VanillaContainerUI.MirrorsLive(_mn.inventory)
                && ReferenceEquals(_mn.inventory.tmpSubInventory, mirrorOf)
                && 50 + index < _mn.inventory.itemSlot.Length)
            {
                vanillaSlotId = 50 + index;
                return _mn.inventory.itemSlot[vanillaSlotId];
            }
            return array != null && index < array.Length ? array[index] : null;
        }

        // The cells are the harvested vanilla size, as the action bar's are. They
        // were once drawn at 85%, to fit three columns of containers up the side
        // of the screen; the downscaling was a mistake and is gone.
        private static void ApplySlotScale()
        {
            _cellsSized = true;
            CellBox = Theme.SlotSize;
            // The gap is CHOSEN, not derived. Scaling the pitch alongside the box
            // meant the space between cells moved whenever the cells did, and it
            // is the space between them that decides whether a row reads as a row
            // or as a scatter of squares.
            CellPitch = CellBox + SlotGap;
        }
    }
}
