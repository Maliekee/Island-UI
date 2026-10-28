using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Target and tick.
    internal static partial class InventoryWindow
    {
        // ---------- target ----------

        // Only PLAIN STORAGE can be replaced by the Focus. Racks, poles, toilets,
        // planters, breed props and the rest drive their gameplay THROUGH the
        // vanilla mirror grid (recon §7.2) - hiding it would make it impossible to
        // put a plant in a planter or an NPC in a rack. Those keep vanilla's UI,
        // and the Focus stays out of their way.
        // A slot that is a LEVEL rather than a bag. Read from vanilla's own
        // dispatch: UIManager's interact switch sends Type.Water to WaterBucketUse
        // and Type.FoodStorage to FoodStorageUse, and both do nothing but display
        // slots[0].itemLife against a maximum. Nothing puts items in or takes them
        // out, so the grid was never a bag.
        internal static bool IsLevelProp(InventorySlot slot)
        {
            return slot != null
                && (slot.type == InventorySlot.Type.Water
                    || slot.type == InventorySlot.Type.FoodStorage);
        }

        private static bool IsReplaceable(object target)
        {
            if (target is CommonStates)
            {
                return true;   // a character's bag is always plain storage
            }
            // A PROP WITH NO CONTAINER IS STILL A TARGET (target-matrix.md
            // G3/F5.1). A signboard, a bed, a defence structure - you click them,
            // vanilla lights an act, and until now our window said nothing at all
            // because the focus only admitted things with slots. The rule: we
            // can't act on it if we don't have a target frame.
            //
            // ABOVE the container guard, not below it: the guard returns false for
            // anything that is not an InventorySlot, so a check placed after it can
            // never run. It was written after it, and a bed showed vanilla's panel
            // and nothing of ours.
            //
            // Safe by construction: these props resolve to null today, so nothing
            // that works can break. Containers are untouched - a chest still
            // arrives as its InventorySlot, and the racks, closets and milkers
            // vanilla owns are still refused below.
            if (target is ItemInfo)
            {
                return Settings.PropWindow.Value;
            }
            // one predicate, shared with the entry-point interception: what we
            // draw and what we intercept must be the same set of containers...
            if (!(target is InventorySlot chest))
            {
                return false;
            }
            // ...plus the rack props we VIEW rather than intercept: vanilla
            // keeps the mirror and its poll, and the plate draws the mirror
            return VanillaContainerUI.IsPlainStorage(chest) || ContainerPolicy.IsViewedRack(chest);
        }

        // What a prop the player is dealing with resolves to: its chest, or the prop
        // itself. This was the prop half of ResolveTarget, which read vanilla's three
        // variables afresh on every ask; the target is a record of ours now
        // (InventoryWindow.Target.cs, window-system.md 6.31), and this is what the
        // record's prop means.
        private static object PropTargetOf(ManagersScript mn, GameObject tmpProp)
        {
            if (tmpProp != null)
            {
                InventorySlot chest = tmpProp.GetComponent<InventorySlot>();
                // A wreck is what you are dealing with, but not as a container:
                // vanilla's UseProp never loads one, showing Repair and no grid,
                // and tmpProp is set all the same - so the focus follows the
                // game's refusal rather than the variable. Repair puts
                // the life back, and the chest with it.
                // A LEVEL PROP IS NOT A CONTAINER (target-matrix.md D3).
                // A well and a food store carry one InventorySlot, so "has slots"
                // made them containers and drew a one-cell grid for a slot that is
                // a QUANTITY - the wrong-reason case the prop census found. They
                // resolve to the PROP instead, and the sheet draws the level.
                if (chest != null && chest.slots != null && chest.slots.Length > 0
                    && !ContainerPolicy.IsWrecked(chest) && !IsLevelProp(chest))
                {
                    return chest;
                }
                // No container, or one vanilla keeps: the PROP itself is the
                // target. A wreck arrives here too, which is right - it has
                // wear to show and a Repair to offer, and it was only ever refused
                // AS A CONTAINER.
                //
                // Deliberately not guarded against the racks and closets: they
                // have containers, so they were returned above or refused by
                // IsReplaceable, and either way they never reach this line.
                if (chest == null || chest.slots == null || chest.slots.Length == 0
                    || ContainerPolicy.IsWrecked(chest) || IsLevelProp(chest))
                {
                    ItemInfo prop = tmpProp.GetComponent<ItemInfo>();
                    if (prop != null && !string.IsNullOrEmpty(prop.itemKey))
                    {
                        return prop;
                    }
                }
            }
            return null;
        }

        // The game activates its container panel from ITS Update, which can run
        // after ours — so hiding it in Update leaves one visible frame (the flash
        // when pressing "I"). Doing it again in LateUpdate closes that window.
        internal static void LateSync()
        {
            if (_mn == null || _mn.inventory == null)
            {
                return;
            }
            if (_pendingProp != null)
            {
                // the worst case of the frame order: vanilla's interact after our Update
                ItemInfo prop = _pendingProp;
                _pendingProp = null;
                _mn.gameMN.pMove.target = prop.gameObject;
                _mn.gameMN.pMove.StartCoroutine(_mn.gameMN.pMove.UseProp(_mn.itemMN.FindItem(prop.itemKey)));
            }
            ReconcileTarget(_mn);
            ReleaseOrphanMenu(_mn);
            SyncVanillaContainerPanel(_mn);
            // ...and the prop and build panels the unit veils, for the same frame
            // (TickUnit veils them in Update, before vanilla may have raised them)
            if (Settings.TheUnit.Value && _mn.allSet && _mn.uiMN != null)
            {
                object focus = Focus();
                VeilVanilla(_mn, focus as CommonStates, FocusProp(focus));
            }
            NormalizeHover();
            TickPlaceFactors();   // the Set place pointer's factors, after vanilla moved its point
            ProbeFlash();
        }

        // For the harness: open a prop the way vanilla's interact does
        // (PlayerMove.UseProp on the nearest prop of that key), and record, frame by
        // frame, which vanilla panels are VISIBLE as the frame ends - a flash is a
        // frame where one is.
        private static int _probeFrames;
        private static readonly System.Text.StringBuilder Probe = new System.Text.StringBuilder();

        internal static string OpenPropNamed(string key)
        {
            PlayerMove pMove = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (pMove == null)
            {
                return "no player";
            }
            ItemInfo best = null;
            float bestD = float.MaxValue;
            foreach (ItemInfo info in Object.FindObjectsOfType<ItemInfo>())
            {
                if (info == null || info.itemKey != key)
                {
                    continue;
                }
                float d = (info.transform.position - pMove.transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = info;
                }
            }
            ItemData data = best != null ? _mn.itemMN.FindItem(best.itemKey) : null;
            if (data == null)
            {
                return "no " + key;
            }
            Probe.Length = 0;
            _probeFrames = 8;
            _pendingProp = best;
            return best.itemKey + " " + data.subType + " at " + Mathf.Sqrt(bestD).ToString("0.0");
        }

        private static ItemInfo _pendingProp;

        internal static string FlashReport()
        {
            return Probe.ToString();
        }

        private static void ProbeFlash()
        {
            if (_probeFrames <= 0)
            {
                return;
            }
            _probeFrames--;
            Probe.Append('f').Append(Time.frameCount % 1000).Append(':');
            ProbeOne("sub", _mn.inventory.subInventory);
            ProbeOne("bar", FindSharedBag(_mn));
            ProbeOne("prop", _mn.uiMN.propPanel);
            ProbeOne("build", _mn.uiMN.buildPanel);
            ProbeOne("apparel", _mn.uiMN.apparelPanel);
            Probe.Append(" focus=").Append(_focusShown ? 1 : 0)
                .Append(" icpt=").Append(VanillaContainerUI.Intercepted != null ? 1 : 0).Append(" | ");
        }

        private static void ProbeOne(string name, GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
            {
                return;
            }
            float a = 1f;
            for (Transform t = go.transform; t != null; t = t.parent)
            {
                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg != null) { a *= cg.alpha; }
            }
            if (a > 0.01f)
            {
                Probe.Append(' ').Append(name).Append("(").Append(a.ToString("0.#")).Append(')');
            }
        }

        // ---------- tick ----------

        private static bool _targetInReach;

        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null)
            {
                return;
            }
            _mn = mn;
            // two presses that must outlast vanilla's own unwinding: a follow's
            // target, and a roster pick made during a body search
            RestoreKeptTarget();
            LandPendingPick();
            TickRepairRelease();
            ReconcileTarget(mn);
            ReleaseOrphanMenu(mn);
            TickTabTarget(mn);
            ArcIdle();   // before any return: an arc nobody draws this frame goes

            KeyCode key = Settings.InventoryKey.Value;
            if (key != KeyCode.None && Input.GetKeyDown(key) && !GameManager.nowEvent)
            {
                Toggle();
            }

            // The Focus follows what you are dealing with, on its own: clicking a
            // container raises it whether or not your own bags are up. That is what
            // replaces the vanilla container UI rather than merely hiding it.
            object focus = ResolveTarget(mn);
            TickWorldArc(focus);   // your target and the villager under the pointer (Links.cs)
            // The target ending HIDES the sheet and the bag window but no longer
            // closes them (D107 amended: an inventory open with the last NPC
            // starts open for the next as well):
            // the two flags are the player's standing choice, and only a chip, a
            // door or Escape changes it.
            // the column is the SHEET now: it shows when a door has opened it,
            // or when the target is a container; the unit says the rest
            _focusShown = focus != null && IsReplaceable(focus) && SheetOpenFor(focus);
            // Engaging a PERSON used to close the vicinity window: it
            // sat in the middle column then, where the person's panel and dialog
            // land. In the left slot (D48) nothing collides with it, so it stays.
            // a jump's blink waits here, until what you walked to is actually open

            // The Shared Bag is a plate in your column, so it opens with the rest of
            // your bags - the action bar is what is always reachable.
            _barShown = _ownOpen;
            // _reachOpen stands on its own: the vicinity window is no longer a
            // section of your bag, so it can be the only thing on screen.
            bool anything = _ownOpen || _ownSheetOpen || _focusShown || _barShown || _reachOpen;
            SyncSharedBag(mn);
            SyncVanillaContainerPanel(mn);
            // the territory preview's lifetime is the Orders sheet's
            SyncTerritoryPreview(mn);
            StyleHeldItem(mn);
            KeepHeldItemOnTop(mn);
            NormalizeHover();
            if (!anything)
            {
                if (_root != null && _root.activeSelf)
                {
                    _root.SetActive(false);
                }
                // the card's lifetime is ours while the window drives it, and a
                // window that closes under a motionless pointer raises no exit
                HoverCard.HideExternal();
                return;
            }
            if (!EnsureBuilt(mn))
            {
                return;
            }
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            // Contents change from crafting, NPCs and vanilla moves; sizes change
            // from pocket_01. Rebind at 10Hz rather than per frame — that is well
            // inside human reaction time and keeps live config edits feeling
            // immediate — but rebind at ONCE when the target changes, since that
            // is a direct response to the player's own action.
            // THE SAME TARGET REFRESH ASKS FOR. This compared the RAW resolve
            // against the value Refresh stores, and Refresh stores the FILTERED
            // one - `_focusShown ? target : null`. Stand next to anything the
            // focus refuses (a rack, a milker, a closet) and the two disagree
            // permanently: the raw resolve is the rack, `_target` is null, so the
            // "target changed" test fired on EVERY FRAME and rebuilt the whole
            // window each time. That is a full rebind at 60Hz beside half the
            // props in a base, which is most of what the stutter near your own
            // props was. The earlier rate work could not have found it, because the
            // interval was never consulted on this path.
            object resolved = ResolveTarget(mn);
            object target = resolved != null && IsReplaceable(resolved) ? resolved : null;
            // the target's bag shows only within reach, and walking in or
            // out of it changes no slot the signature reads, so it is watched here
            // ...a thing's window too, now that an armed door opens it on arrival
            ItemInfo reachProp = target is CommonStates ? null : FocusProp(target);
            bool reach = target is CommonStates near ? InReach(near)
                : reachProp != null && ReachRule.LiveProp(mn, reachProp);
            if (reach != _targetInReach)
            {
                _targetInReach = reach;
                _forceRefresh = true;
                _nextRefresh = 0f;
            }
            if (!ReferenceEquals(target, _target) || Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval();
                // ...and even when it IS due, most ticks have nothing to say. A
                // rebind lays out every cell of every band; a signature reads the
                // same slots and compares an int. Standing still with the window
                // open is the common case and it now costs the read alone.
                int sig = PoolSignature(mn);
                bool due = _forceRefresh
                    || !ReferenceEquals(target, _target)
                    || sig != _signature
                    // A safety net, not a schedule: the signature cannot see
                    // everything (a live config edit, a sprite that finished
                    // loading), so a full rebind lands at least this often
                    // whatever it thinks. Cheap insurance against a blind spot
                    // that would otherwise present as "the window went stale".
                    || Time.unscaledTime >= _fullRefreshBy;
                if (due)
                {
                    _signature = sig;
                    // a forced refresh is a door opened or closed: the bar re-reads
                    // now, so the door's strip goes green with the window, not on
                    // the bar's next quarter-second read
                    if (_forceRefresh) { _unitBoundAt = 0f; }
                    _forceRefresh = false;
                    _fullRefreshBy = Time.unscaledTime + 2f;
                    float began = Time.realtimeSinceStartup;
                    Refresh();
                    ReportSlowRefresh(Time.realtimeSinceStartup - began);
                }
            }

            // a dossier's portrait is the character as they are this frame (D125),
            // so it renders before anything below can return out of the tick
            TickPortraits();
            TickSnapshots();
            TickFloat();
            // the leather's rims from what the last input pass found, before anything
            // below can return; the pass sets the pointer's cell again if it gets there
            PaintLeatherRims();
            _leatherHover = null;
            Canvas canvas = _root.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;
            // a name being retyped on a sheet reads its keys first, and the sheet
            // repaints every frame while it does, so each letter shows as typed
            _renameCam = cam;
            if (UI.NameEdit.Tick())
            {
                _nextSheetPaint = 0f;
            }
            // the split dialog is modal over the window while it is up (ClickMap)
            if (SplitDialog.Tick(cam))
            {
                return;
            }
            // First refusal, for the same reason as the crafting window: a press
            // on the field must not also press the cell underneath it.
            if (Input.GetMouseButtonDown(0) && _filter.HandleClick(Input.mousePosition, cam))
            {
                return;
            }
            LoveTipIdle();
            KitDropdown.CloseOrphans();
            // an open dropdown list hangs over whatever is under it - the foot
            // tabs included - so it is asked before anything else
            if (KitDropdown.TickOpen(cam))
            {
                return;
            }
            // an item released on someone is used on them (D141) - above the sheets,
            // which would otherwise swallow a release over a roster strip
            if (TickDropOnPerson(cam))
            {
                return;
            }
            // THE WHEEL BELONGS TO THE WINDOW, NEVER TO WHAT IS UNDER IT. It used to move one item per notch over a cell, which
            // reads well in isolation and is wrong here: the panel it sits on
            // scrolls, so the same gesture meant two things a few pixels apart,
            // and the one you wanted was whichever you were not hovering.
            //
            // IT IS ASKED HIGH IN THE CHAIN, and that is the whole of the fix for
            // the roster not scrolling. It used to sit at the
            // BOTTOM of this method, below `TickSheets`, which returns true - and
            // returns out of here - whenever the pointer is over a sheet row. A
            // roster row IS a sheet plate (BindRosterPlate), so the pointer being
            // over the list was exactly the condition that swallowed the wheel:
            // the one place you would ever point while scrolling it.
            //
            // Only the open dropdown is asked first, because its list hangs over
            // the window and a wheel there is the list's, not the pool's.
            // ...and the bar it moves, asked in the same place and for the same
            // reason: it belongs to the window, and it sits over the frame where
            // a row would otherwise claim the press.
            if (TickSheetScroll(cam) || TickScrollBar(cam))
            {
                return;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && _reachOpen && _reachPanel != null
                && _reachPanel.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    _reachPanel, Input.mousePosition, cam))
            {
                ScrollReach(Mathf.Sign(wheel));
                return;   // a notch is not also a click on whatever it happened over
            }
            // the slot's tabs, then the close chips: both overhang the frames
            if (_slotTabs != null && _slotTabs.Tick(cam))
            {
                return;
            }
            // ...and the vicinity's foot, a band of its own under the list
            if (TickReachFoot(cam))
            {
                return;
            }
            if (TickMatrix(cam))
            {
                return;
            }
            // the sheet's dock chip, beside its close on the rim, before the sheet's rows
            if (TickFloatTab(cam))
            {
                return;
            }
            // ...and the sheet dragged by its tabs, asked before the tabs themselves
            if (TickSheetDrag(cam))
            {
                return;
            }
            // ...and the columns' aspect tabs, hanging under the frames (§6.20)
            for (int g = 1; g < GroupCount; g++)
            {
                if (_footTabs[g] != null && _footTabs[g].Tick(cam))
                {
                    return;
                }
                if (_dossierTabs[g] != null && _dossierTabs[g].Tick(cam))
                {
                    return;
                }
            }
            // A sheet's numbers move on their own clock, not the pool's: exp and
            // life change without a slot changing, so the signature never sees
            // them. Repainting the type is cheap; a rebind is not.
            if (SheetPlates.Count > 0 && Time.unscaledTime >= _nextSheetPaint)
            {
                _nextSheetPaint = Time.unscaledTime + 0.25f;
                RepaintSheets();
            }
            // ...and its controls, before the cells: a chip on a sheet and a cell
            // never share a spot, but a press must not fall through to the world
            if (SheetPlates.Count > 0 && TickSheets(cam))
            {
                return;
            }
            if (TickTiles(cam))
            {
                return;
            }
            Plate closeHot = TickCloseChips(cam);
            CellView hoveredCell = closeHot != null ? null : HitCell(cam);
            _leatherHover = hoveredCell;
            ItemSlot hoveredSlot = hoveredCell != null && hoveredCell.Slot != null
                && !string.IsNullOrEmpty(hoveredCell.Slot.itemKey) ? hoveredCell.Slot : null;
            _hover.text = ApplyEquipPreview(hoveredSlot, hoveredCell);
            // Our cells are ours and our input is polled, so vanilla's
            // ItemDescOpen — the card's only trigger — is never reached here. Drive
            // it directly. The one-line `_hover` readout answers "what is it"; the
            // card answers "what is it worth", and both belong on a hover.
            HoverCard.ShowForSlot(mn, mn.uiMN, hoveredSlot, hoveredCell != null ? hoveredCell.Rect : null);
            OfferBadge(hoveredCell, hoveredSlot);
            if (hoveredSlot != null && Butchers(hoveredCell) && Input.GetKeyDown(KeyCode.T)
                && _mn.inventory.useSlotID == -1)
            {
                Butcher(hoveredCell);
                return;
            }

            // A CLICK is press and release over the same thing, and it acts on
            // RELEASE. That is what vanilla does — every ItemSlot is an
            // IPointerClickHandler, and Unity raises OnPointerClick on pointer-up
            // — and acting on press instead made our cells feel a beat ahead of
            // the vanilla slots they sit next to. It is also the grammar
            // drag-and-drop needs: press and release are already separate events.
            // Alt+click MARKS instead of moving. Read on press and consumed
            // there, so no part of the move grammar ever starts — vanilla unlocks
            // the cursor on LeftAlt (GameManager.cs:426) and takes Alt+drag for the
            // camera (Camera3DControl.cs:54/75), so the gesture must be over and
            // done with on the button-down, never a drag.
            if (Input.GetMouseButtonDown(0) && ClickMap.Alt
                && hoveredCell != null && hoveredSlot != null && HandEmpty())
            {
                // consumed anywhere, marking only where the click map offers it
                ClickMap.Verb mark = ClickMap.ForCell(hoveredSlot, true, WearsFavourites(PlateOf(hoveredCell)), false);
                if (mark != ClickMap.Verb.Favourite && mark != ClickMap.Verb.Unmark)
                {
                    return;
                }
                bool on = Favourites.Toggle(hoveredSlot);
                Plugin.Log.LogInfo($"Favourite {(on ? "set" : "cleared")}: {hoveredSlot.itemKey}");
                _nextRefresh = 0f;
                Refresh();
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                _pressedCell = hoveredCell;
                _pressDownPos = Input.mousePosition;
                _dragging = false;
                SetPressed(_pressedCell, true);
            }
            if (Input.GetMouseButtonDown(1))
            {
                _pressedCellRight = hoveredCell;
            }
            if (Input.GetMouseButtonUp(1))
            {
                if (hoveredCell != null && ReferenceEquals(hoveredCell, _pressedCellRight))
                {
                    RightClick(hoveredCell);
                }
                _pressedCellRight = null;
            }
            // Hold and move far enough and the press becomes a DRAG — the grab
            // happens here rather than on release, so the item is already in hand
            // and following the cursor for the rest of the gesture. Click-click is
            // untouched: without the movement this branch never runs.
            if (!_dragging && Input.GetMouseButton(0) && CanStartDrag()
                && ((Vector2)Input.mousePosition - _pressDownPos).sqrMagnitude
                    > DragThreshold * DragThreshold)
            {
                _dragging = true;
                SetPressed(_pressedCell, false);
                LeftClick(_pressedCell);
            }
            if (Input.GetMouseButtonUp(0))
            {
                SetPressed(_pressedCell, false);
                if (_dragging)
                {
                    if (hoveredCell != null)
                    {
                        LeftClick(hoveredCell);
                    }
                    else
                    {
                        DropHeldIntoWorld();
                    }
                }
                else if (hoveredCell != null && ReferenceEquals(hoveredCell, _pressedCell))
                {
                    Plate pressedOn = PlateOf(hoveredCell);
                    ClickMap.Verb verb = ClickMap.ForCell(hoveredSlot, HandEmpty(), WearsFavourites(pressedOn),
                        pressedOn != null && pressedOn.IsLoadout, MoveKind(pressedOn));
                    if (verb == ClickMap.Verb.Split)
                    {
                        OpenSplit(hoveredCell, hoveredSlot);
                    }
                    else if (verb == ClickMap.Verb.Move || verb == ClickMap.Verb.Drop)
                    {
                        CtrlMove(hoveredCell);
                    }
                    else if (verb == ClickMap.Verb.Kept)
                    {
                        FavouriteRules.Refuse();   // a favourite stays in your bags
                    }
                    else
                    {
                        LeftClick(hoveredCell);
                    }
                }
                _pressedCell = null;
                _dragging = false;
            }
            // The filter chips, hit-tested after the cells because they cannot
            // collide: a chip sits in the heading and a cell never does. Skipped
            // mid-drag, where a click already means "place this".
            Image overButton = _reachOpen ? HitButton(cam) : null;
            _hotButton = overButton;
            if (overButton != null && Input.GetMouseButtonDown(0) && !_dragging)
            {
                _pressedButton = overButton;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressedButton != null && ReferenceEquals(_pressedButton, overButton)
                    && !_dragging)
                {
                    PressButton(overButton);
                }
                _pressedButton = null;
            }
            // ...and repaint them THIS frame. Every line above changed something
            // the eye is waiting for.
            PaintButtons();
            PaintTiles();
            PaintActRows();
            PaintLeatherRims();

            // Refresh repaints every cell from Bind's rule, so the press tint has
            // to be restated after it rather than set once on mouse-down.
            if (_pressedCell != null && !_dragging && Input.GetMouseButton(0))
            {
                SetPressed(_pressedCell, true);
            }
        }

        // Releasing a drag out over the world drops the item there. Vanilla only
        // ever drops on a mouse-DOWN over the world (InventoryManager.Hover), which
        // a drag never issues — the button went down over a cell — so the gesture
        // used to end with the item stuck to the cursor and nothing to show for it.
        // Note the drop lands at the PLAYER's feet, not under the cursor: that is
        // ItemDrop's own behaviour (MakeDropItem spawns at player.transform), and
        // matching vanilla matters more here than matching the gesture.
        private static void DropHeldIntoWorld()
        {
            InventoryManager inv = _mn.inventory;
            if (inv == null || inv.hoverSlot == null || !inv.hoverSlot.gameObject.activeSelf
                || string.IsNullOrEmpty(inv.hoverSlot.itemKey))
            {
                return;
            }
            // The real "is the pointer over UI" answer: gameMN.OnUI is forced true
            // for the length of a drag (DragOwnsThePointer), so asking the flag here
            // would say "over UI" every time and nothing would ever drop.
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && events.IsPointerOverGameObject())
            {
                return;   // over a panel, merely not over a cell — keep it in hand
            }
            if (Favourites.IsFavourite(inv.hoverSlot))
            {
                FavouriteRules.Refuse();   // a favourite is not dropped; home it goes
                HomeHeld();
                return;
            }
            inv.ItemDrop(inv.hoverSlot);
            NormalizeHover();
            _nextRefresh = 0f;
            Refresh();
        }

        private static bool AltHeld()
        {
            return ClickMap.Alt;
        }

        // Alt+click marks, but only with nothing in hand: mid-gesture the click
        // already means "place this", and one click may not mean two things.
        private static bool HandEmpty()
        {
            return _mn.inventory != null && _mn.inventory.hoverSlot != null
                && !_mn.inventory.hoverSlot.gameObject.activeSelf;
        }

        // A drag only makes sense when the press landed on an item and the hand is
        // empty. Pressing an EMPTY cell and moving must not become a drag, or the
        // release would grab from whatever cell it happened to land on.
        private static bool CanStartDrag()
        {
            return !AltHeld()
                && _pressedCell != null
                && _pressedCell.Slot != null
                && !string.IsNullOrEmpty(_pressedCell.Slot.itemKey)
                && _mn.inventory != null
                && !_mn.inventory.hoverSlot.gameObject.activeSelf;
        }

        // Acting on release left the press itself silent, which read as lag rather
        // than as care. The cell darkens under the finger and returns on release —
        // reapplied every frame because Refresh repaints the cell out from under it.
        private static void SetPressed(CellView cell, bool pressed)
        {
            if (cell == null || cell.Box == null)
            {
                return;
            }
            // Anything that paints a cell from OUTSIDE the bind must forget what
            // the bind thinks it drew, or the memo will skip the repaint that
            // puts the cell back. The tint is cheap; being wrong about it is not.
            cell.DrewKey = null;
            // the vicinity's cell wears its pressed face instead (PaintVicinityCells)
            if (cell.Vicinity)
            {
                return;
            }
            // on the leather a press is the proof's selection, a lit rim
            // (PaintLeatherRims): the bed keeps its colour
            if (cell.Leather)
            {
                cell.Box.color = Color.white;
                return;
            }
            // a crate's compartment is drawn whole, empty or not (ShedCell): the
            // parchment's half-alpha empty slot would let the crate's boards through
            Color under = Color.white;
            // the press MULTIPLIES whatever the cell already is, so a filled cell
            // still darkens under the finger instead of brightening back up to the
            // empty-cell tint
            cell.Box.color = pressed
                ? new Color(under.r * Theme.PressTint.r, under.g * Theme.PressTint.g,
                    under.b * Theme.PressTint.b, under.a)
                : under;
        }

        // Read-only this pass: hovering a cell reports what the REAL slot holds,
        // which is the whole verification surface until WI-3 adds movement.
        private static CellView HitCell(Camera cam)
        {
            CellView hit = ScanPlates(Plates, _platesUsed, cam, null);
            return hit ?? ScanPlates(ReachPlates, _reachPlatesUsed, cam, _reachContent);
        }

        // `clip` is the window a pool is seen through, and the pointer must be
        // inside it as well as inside a cell. Without that, a row scrolled out of
        // the viewport stays pickable: it is still active and still exactly where
        // the arithmetic says it is - the mask only stops it being DRAWN. Same
        // class as the plates check below, one level up.
        private static CellView ScanPlates(List<Plate> pool, int used, Camera cam,
            RectTransform clip)
        {
            if (clip != null && (!clip.gameObject.activeInHierarchy
                || !RectTransformUtility.RectangleContainsScreenPoint(
                        clip, Input.mousePosition, cam)))
            {
                return null;
            }
            for (int p = 0; p < used && p < pool.Count; p++)
            {
                Plate plate = pool[p];
                // You cannot pick out of what you cannot see. This asks the SCENE,
                // not the arithmetic: HitCell answers from geometry, and geometry
                // has no idea whether the thing it is measuring is switched on.
                if (!plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        plate.Window.Content, Input.mousePosition, cam, out Vector2 local))
                {
                    continue;
                }
                for (int i = 0; i < plate.Shown && i < plate.Cells.Count; i++)
                {
                    CellView cell = plate.Cells[i];
                    // empty cells count: placing into one is the whole point
                    if (cell.Slot == null || !cell.Go.activeInHierarchy)
                    {
                        continue;
                    }
                    float cx = plate.CellLeft + (i % plate.Cols) * CellPitch;
                    float cy = plate.CellTop + (i / plate.Cols) * CellPitch;
                    if (local.x >= cx && local.x <= cx + CellBox
                        && local.y <= -cy && local.y >= -cy - CellBox)
                    {
                        return cell;
                    }
                }
            }
            return null;
        }
    }
}
