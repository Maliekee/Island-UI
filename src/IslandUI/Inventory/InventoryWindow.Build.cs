using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The build: plates, members, the rebind.
    internal static partial class InventoryWindow
    {
        // ---------- build ----------

        private static void Refresh()
        {
            float refreshBegan = Time.realtimeSinceStartup;
            long spanAt = UI.FirstOpenTimer.Now();
            ApplySlotScale();
            _lastGroupBound = GroupNone;
            if (_mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null
                || _mn.inventory == null)
            {
                return;
            }
            _platesUsed = 0;
            _reachPlatesUsed = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                _lastColumnWidth[c] = 0f;
            }
            _nearbyTotal = 0;
            _nearbyMatched = 0;
            _nearbyShown = 0;
            _cellsBound = 0;
            _itemLookups = 0;
            _reachMs = 0f;
            _cellMs = 0f;
            _iconMs = 0f;
            _platesMade = 0;
            _cellsMade = 0;
            _preReachMs = 0f;
            _textMs = 0f;
            _layoutMs = 0f;
            _reshapes = 0;
            InventoryManager inv = _mn.inventory;
            CommonStates player = _mn.gameMN.pMove.common;

            // ---- your own stack, bottom-up in the configured order ----
            // the frame first: pooled plates keep creation order as sibling order,
            // so bound first is drawn first is behind
            BindGroupFrame(GroupOwn, OwnColumn);
            NameLeather(GroupOwn, player != null ? player.charaName : null);
            SheetPlates.Clear();
            _tilePlates.Clear();   // re-shown by each frame that carries a person
            _actRowPlates.Clear(); // ...and the act rows, re-bound by each thing (D105)
            HideReachFoot();       // ...and the vicinity's foot, by BindReach
            _tileWhos.Clear();
            _tileProps.Clear();

            // Bags and details have independent lifetimes and separate frames.
            _footShown[GroupOwn] = false;
            _footShown[GroupOwnSheet] = _ownSheetOpen;
            _footSets[GroupOwnSheet] = AspectsFor(player, true, OwnAspects, _footSets[GroupOwnSheet]);
            _ownAspect = ClampAspect(_ownAspect, _footSets[GroupOwnSheet]);
            string[] order = OwnStackOrder;
            for (int i = 0; i < order.Length; i++)
            {
                string token = order[i].Trim().ToLowerInvariant();
                if (token != "sharedbag" && !_ownOpen)
                {
                    continue;   // your bag/loadout follow the key; the bar does not
                }
                switch (token)
                {
                    case "sharedbag":
                        if (_barShown)
                        {
                            // IN THE COLUMN: it is a container like any other,
                            // so it takes the column's own budget and fills rows to
                            // it - 18 slots land on 10 + 8, sharing its left and
                            // right edges with every other band in the column.
                            Bind(Shared.Loc.Get("inv.shared_bag", "Shared Bag"), inv.itemSlot, inv.baseCount, null, null,
                                false, OwnColumn, 0, null, 0,
                                sharedBag: true, group: GroupOwn,
                                icon: ContainerIcon(SharedBagIcon));
                        }
                        break;
                    case "bag":
                        if (player != null && player.bag != null && player.bag.slots != null)
                        {
                            Bind(Shared.Loc.Get("inv.bag", "Bag"), player.bag.slots, player.bag.slots.Length, null,
                                player.bag, false, OwnColumn, 0, player, group: GroupOwn,
                                icon: ContainerIcon(OwnBagIcon));
                        }
                        break;
                    case "loadout":
                        if (player != null && HasLoadout(player))
                        {
                            Bind(Shared.Loc.Get("inv.loadout", "Loadout"), player.equip, LoadoutOrder.Length, LoadoutOrder,
                                null, true, OwnColumn, 0, player, group: GroupOwn,
                                icon: ContainerIcon(LoadoutIcon));
                        }
                        break;
                }
            }

            if (_ownSheetOpen && player != null)
            {
                BindGroupFrame(GroupOwnSheet, OwnColumn);
                BindSheet(GroupOwnSheet, OwnColumn, player, _ownAspect, OwnMaxCols);
            }

            // your own containers are bound first: their width is what decides
            // how much room the target's column has left. The Shared Bag is one
            // of them, and no wider than the bag.
            float ownWidth = 0f;
            for (int i = 0; i < _platesUsed; i++)
            {
                ownWidth = Mathf.Max(ownWidth, Plates[i].Window.Rect.sizeDelta.x);
            }
            // TargetMaxCols answers "how much room is LEFT on screen", which is a
            // clamp, not a target — a 27-slot bag given the whole screen renders
            // 27 across, and the panel around it swallows the rail. MaxPlateColumns
            // is the shape rule; the screen fit only ever narrows it further.
            int targetCols = Mathf.Min(TargetMaxCols(ownWidth), OwnMaxCols);
            // ONE WIDTH FOR EVERY TARGET (user): the widest bag the column allows,
            // whoever it is and whatever their bag - a followed name must not move
            // the sheet under the pointer
            _targetSheetW = SheetFallbackWidth(null, targetCols, false, full: true);

            // ---- the focused CHARACTER, in the middle column ----
            // A character is more than a container — bag, loadout, and in time a
            // verb list and vitals — which is why it keeps a column of its own
            // while a focused container joins the pool it belongs to.
            object nextTarget = _focusShown ? ResolveTarget(_mn) : null;
            if (!ReferenceEquals(nextTarget, _target))
            {
                // §6.25 D93: a new carrier opens at the page its DOOR asked for -
                // resolved below, once the page set is known, since the set is what
                // decides whether the preference can be honoured at all.
                _doorPending = true;
            }
            _target = nextTarget;
            _footShown[GroupCharacter] = false;
            string targetTitle = null;
            CommonStates targetWho = null;
            Color? targetSplash = null;
            Sprite targetBadge = Theme.CharacterBadge;
            bool targetStarred = false;
            _focusedContainer = null;
            Plate tileHost = null;
            ItemInfo tileProp = null;
            if (_target is CommonStates npc && npc != player)
            {
                string name = string.IsNullOrEmpty(npc.charaName) ? Shared.Loc.Get("inv.character", "Character") : npc.charaName;
                targetTitle = name;
                // THE FAVOURITE'S STAR: an NPC that has been favorited carries a
                // star.
                // Vanilla keeps the mark as an EMPLOY role - the friends list's
                // heart button flips Friend <-> Favorite - so the plate reads the
                // same flag the game does, and nothing of ours has to be kept in
                // step with it.
                // The star on the name plate is retired: the star TILE on the
                // frame carries the mark now
                targetStarred = false;
                targetWho = npc;
                // THE BAG WINDOW, first so it stands on the bar: the
                // person's bag and loadout as the grid window a chest gets, its own
                // frame and plate, closed by its chip; the sheet hangs from the unit
                bool hasBag = (npc.bag != null && npc.bag.slots != null && npc.bag.slots.Length > 0) || HasLoadout(npc);
                // ...only within reach, like the vicinity window: a roster pick
                // reaches anyone, and their bag was open to a
                // drag from across the island. Hidden, not closed - it returns when
                // you walk up (the Tick watches InReach)
                // BEYOND REACH THERE IS NO WINDOW, rather than an empty inventory
                // with a caption: the door stays lit - armed - and the window appears
                // when you walk up (the Tick watches InReach). The caption window of
                // D155 is gone.
                // ...and only a bag the game would show you: the flag outlives the
                // target, and a corpse that turns into loot keeps its own (PocketsOpen)
                if (_bagOpen && hasBag && PocketsOpen(npc) && InReach(npc))
                {
                    _footSets[GroupBag] = PropAspects;
                    _footShown[GroupBag] = false;
                    BindGroupFrame(GroupBag, CharacterColumn);
                    NameLeather(GroupBag, name);
                    bool hasSlots = npc.bag != null && npc.bag.slots != null && npc.bag.slots.Length > 0;
                    {
                        // the bag's three at the window's foot, bound first so the
                        // bottom-up stack puts it under the grid
                        if (hasSlots)
                        {
                            BindBagActRow(GroupBag, CharacterColumn, npc.bag, targetCols);
                        }
                        if (hasSlots)
                        {
                            Bind(Shared.Loc.Get("inv.bag", "Bag"), npc.bag.slots, npc.bag.slots.Length, null,
                                npc.bag, false, CharacterColumn, targetCols, npc,
                                group: GroupBag, icon: ContainerIcon(OwnBagIcon));
                        }
                        if (HasLoadout(npc))
                        {
                            Bind(Shared.Loc.Get("inv.loadout", "Loadout"), npc.equip, LoadoutOrder.Length, LoadoutOrder,
                                null, true, CharacterColumn, targetCols, npc,
                                group: GroupBag, icon: ContainerIcon(LoadoutIcon));
                        }
                    }
                }
                // the dossier only when a door opened it; the bag alone binds no
                // frame for it, and no name plate for it either
                // no name plate on the dossier: the unit above names them, and the
                // tabs take the plate's place on the top rail
                targetTitle = null;
                if (!_sheetOpen)
                {
                    goto doors;
                }
                // D76: the tab set is this target's, derived from what it has
                _footSets[GroupCharacter] = AspectsFor(npc, false, TheirAspects,
                    _footSets[GroupCharacter]);
                // no page at all - a stranger's corpse - is no sheet, not an empty one
                if (_footSets[GroupCharacter].Length == 0)
                {
                    goto doors;
                }
                // §6.25 D93/D94: the set is known, so the door's preference can be
                // honoured or fall back to the first page. The door is consumed
                // here and not before, so one arming serves exactly one open.
                if (_doorPending)
                {
                    _focusAspect = LandOn(_doorArmed, _footSets[GroupCharacter]);
                    _doorPending = false;
                    _doorArmed = Door.None;
                }
                else
                {
                    _focusAspect = ClampAspect(_focusAspect, _footSets[GroupCharacter]);
                }
                BindGroupFrame(GroupCharacter, CharacterColumn);
                _footShown[GroupCharacter] = TabTitle(GroupCharacter);
                targetSplash = NPCColor(npc);
                BindSheet(GroupCharacter, CharacterColumn, npc, _focusAspect, targetCols);
            }
            // A focused CONTAINER gets a panel of its own too.
            // It used to sit first-and-marked INSIDE the pool, which made the thing
            // you are dealing with a highlighted row among the things you merely
            // could deal with - while a focused character, which is the same
            // relationship, got a panel. Same relationship, same shape.
            //
            // It lands in the character group because that group is not "a
            // character": it is THE ONE IN FOCUS, and a chest in focus is as much
            // that as a person is. The two can never collide, since the focus is
            // one thing at a time.
            else if (_target is InventorySlot chest
                && chest.slots != null && chest.slots.Length > 0)
            {
                _focusedContainer = chest;
                // A window of its OWN, always - the same treatment a person gets.
                // Folding it into the pool as a marked member
                // was tried and reverted: it made the thing you are ACTING ON a row
                // among the things you merely could act on, and the mark had to
                // carry that difference alone. A container you have opened is not a
                // narrowing of the set in reach, it is the other end of every
                // transfer you are about to make. It gets a frame, and the frame is
                // what gets highlighted.
                // D156: with the unit up the window is the Inventory door's, as a
                // person's bag window is - no name plate (the indicator names it), no
                // readings (the indicator reads it), no Move · Remove (the dock has
                // them); the grid, and the container's own three at its foot
                bool docked = Settings.TheUnit.Value;
                if (!docked)
                {
                    targetTitle = ChestTitle(chest);
                    targetBadge = ChestIcon(chest) ?? Theme.ContainerBadge;
                }
                _footSets[GroupCharacter] = PropAspects;   // empty: a thing has no tabs
                BindGroupFrame(GroupCharacter, CharacterColumn);
                NameLeather(GroupCharacter, ChestTitle(chest));
                ItemInfo chestProp = chest.GetComponent<ItemInfo>();
                if (docked && !ReachRule.LiveProp(_mn, chestProp))
                {
                    // beyond reach there is no window - the door is armed until you
                    // walk up (SheetOpenFor keeps the column shut); nothing binds
                    goto doors;
                }
                // A TREASURE CHEST HAS NO LOOT UNTIL VANILLA OPENS IT (a
                // stranger's chest picked by Tab showed nothing, and its items
                // appeared once it was clicked): TreasureScript.TreasureSet
                // fills its slots on the first open, and Tab holds a thing without
                // opening it - so the grid would be empty, and a write into it
                // overwritten on that first open. Its window is its caption until then.
                TreasureScript loot = docked && chest.type == InventorySlot.Type.Treasure
                    ? chest.GetComponent<TreasureScript>() : null;
                if (loot != null && (!loot.opened || !loot.unlocked || loot.isCollection))
                {
                    Bind(!loot.unlocked ? Shared.Loc.Get("inv.chest.locked", "Locked")
                        : Shared.Loc.Get("inv.chest.unopened", "Unopened") + "  ·  " + Shared.Loc.Get("inv.chest.click_to_open", "click it to open"), chest.slots, chest.slots.Length,
                        null, null, false, CharacterColumn, targetCols, null, forceLabel: true,
                        group: GroupCharacter, collapsed: true, captionRow: true);
                    goto doors;
                }
                if (docked)
                {
                    BindActRow(GroupCharacter, CharacterColumn, chestProp, targetCols);
                    Bind(string.Empty, chest.slots, chest.slots.Length, null, chest,
                        false, CharacterColumn, targetCols, null, -1, false,
                        group: GroupCharacter, icon: ChestIcon(chest));
                    goto doors;
                }
                // ONE PAGE FOR A THING. A pig pen has a bag AND acts,
                // and under the presence rule that owed it two tabs - which is
                // absurd on a fence, and the sign that the rule was being applied
                // past its subject. So the two merge: the wear and the acts as a
                // sheet, the contents as the grid, on one surface.
                //
                // The sheet is bound FIRST because plates stack bottom-up, so what
                // is bound last sits on top: the contents lead and the acts sit
                // under them.
                // ...and only when it has something to say. A pen that is just a
                // bag gets the grid alone, with no rule under it.
                // A THING'S WINDOW (D112/D113): the grid is what you came
                // for and the window is the indicator. The ACT ROW is bound first so
                // it lies at the foot under the grid; wear rides the rim.
                if (chestProp != null)
                {
                    tileHost = Plates[_platesUsed - 1];
                    tileProp = chestProp;
                    BindActRow(GroupCharacter, CharacterColumn, chestProp, targetCols);
                }
                Bind(string.Empty, chest.slots, chest.slots.Length, null, chest,
                    false, CharacterColumn, targetCols, null, -1, false,
                    group: GroupCharacter, icon: ChestIcon(chest));
                // ...and its readings ABOVE the grid, bound last so they sit on top:
                // wear, the one line every damageable thing has
                if (chestProp != null && !PropSheetEmpty(chestProp))
                {
                    BindSheet(GroupCharacter, CharacterColumn, null, Aspect.Overview,
                        targetCols, prop: chestProp);
                }
            }
            // A PROP, WITH OR WITHOUT A CONTAINER. It lands in the same
            // group for the same reason a chest does: the group is not "a
            // character", it is THE ONE IN FOCUS, and only one thing is ever in
            // focus. A fixture's page is deliberately thin - the name, the wear
            // and its one act - because the frame itself is what says you have a
            // target, and a thing with nothing to read still needs somewhere to
            // put the act (target-matrix.md F5.1).
            else if (_target is ItemInfo prop)
            {
                targetTitle = PropTitle(prop);
                targetBadge = Theme.ContainerBadge;
                _footSets[GroupCharacter] = PropAspects;   // empty: a thing has no tabs
                BindGroupFrame(GroupCharacter, CharacterColumn);
                // the glyphs float against the title PLATE, whose width is not known
                // until BindTitle has run - so they are hung after it, below
                tileHost = Plates[_platesUsed - 1];
                tileProp = prop;
                // the same window as a chest's: the act row at the foot, the thing's
                // own reading above it when it has one (a well's water, a farm's
                // progress) - a signboard is rail and row
                BindActRow(GroupCharacter, CharacterColumn, prop, targetCols);
                if (!PropSheetEmpty(prop))
                {
                    BindSheet(GroupCharacter, CharacterColumn, null, Aspect.Overview,
                        targetCols, prop: prop);
                }
            }
            // A thing has one page (D92), so there is no page to choose and the
            // door is moot - but it must still be spent here, or an arming would
            // survive a chest and land on the next PERSON the window opens.
            doors:
            if (_doorPending)
            {
                _doorPending = false;
                _doorArmed = Door.None;
            }
            if (targetTitle != null)
            {
                BindTitle(targetTitle, CharacterColumn, badge: targetBadge,
                    group: GroupCharacter, splash: targetSplash, starred: targetStarred, who: targetWho);
            }
            // ...and only now is the plate's width known
            if (tileHost != null && tileProp != null)
            {
                ShowPropTiles(tileHost, tileProp);
            }

            // ---- every container in reach, in the leftmost column ----
            _preReachMs = (Time.realtimeSinceStartup - refreshBegan) * 1000f;
            if (RosterView)
            {
                BindRoster();
            }
            else
            {
                BindReach(targetCols, player);
            }
            // The field is a permanent object placed only by BindReach, so with
            // the window closed it kept its last place and showed alone whenever
            // the root was up for something else (a lone "Filter.. 11").
            if (!_reachOpen)
            {
                UI.SlotFilter.Hide(_root.transform);   // only if this window still holds it
            }
            for (int i = _platesUsed; i < Plates.Count; i++)
            {
                if (Plates[i].Window.Go.activeSelf)
                {
                    Plates[i].Window.Go.SetActive(false);
                }
            }
            // ONE CALL, NOT THREE HUNDRED AND SIXTY. Every reach plate is a
            // child of the viewport, so closing the window can switch off the
            // viewport instead of switching off each plate - and a plate is not
            // one object, it is a plate and every cell under it. At eighteen
            // containers that was ~360 GameObject activations to close and ~360
            // to open, which is the same Graphic register/unregister cost that
            // was making the steady state stutter, now landing entirely on the
            // toggle. Close and reopen quickly and you pay both within a frame or
            // two of each other, which is exactly how it was reported.
            //
            // Deactivating the PARENT leaves the children's own activeSelf alone,
            // so nothing below is touched and nothing has to be restored.
            if (_reachContent != null
                && _reachContent.gameObject.activeSelf != _reachOpen)
            {
                _reachContent.gameObject.SetActive(_reachOpen);
            }
            if (_reachOpen)
            {
                for (int i = _reachPlatesUsed; i < ReachPlates.Count; i++)
                {
                    if (ReachPlates[i].Window.Go.activeSelf)
                    {
                        ReachPlates[i].Window.Go.SetActive(false);
                    }
                }
            }
            ReleaseUnclaimed();
            float layoutBegan = Time.realtimeSinceStartup;
            long layoutAt = UI.FirstOpenTimer.Now();
            Layout();
            _layoutMs = (Time.realtimeSinceStartup - layoutBegan) * 1000f;
            UI.FirstOpenTimer.Span("inventory layout", layoutAt);
            UI.FirstOpenTimer.Span("inventory refresh", spanAt);
        }

        // The rule lives in Theme, beside LoadoutOrder, so this gate and the
        // browser's cannot drift apart again.
        private static bool HasLoadout(CommonStates common)
        {
            return Theme.HasLoadout(common);
        }

        // Off by default: valuable, but a separate problem from "my bags and the
        // one I am dealing with". Kept behind a toggle so it costs no rebuild.
        private static string PlayerName(CommonStates player)
        {
            return player != null && !string.IsNullOrEmpty(player.charaName)
                ? player.charaName : Shared.Loc.Get("sheet.you", "You");
        }

        // Is the vicinity window up? Read by the HUD button, which has to say
        // which way a toggle is set.
        internal static bool ReachOpen
        {
            get { return _reachOpen; }
        }

        // V and the HUD chest icon arrive here. The pool is a window of its own
        // now, not a section folded into your bag: it used to
        // ride _ownOpen, so reaching for your bags requested a survey of the whole
        // base as a side effect. Opening it is an act; closing it is an act.
        // D53: swap the list in the slot without closing the window. The scroll
        // belongs to the LIST, not to the frame, so it goes back to the top - the
        // two lists have nothing in common to hold a position in.
        internal static void ShowSlotView(int view)
        {
            if (!_reachOpen)
            {
                ToggleReach();
            }
            if (_slotView == view)
            {
                return;
            }
            _slotView = view;
            _reachScroll = 0f;
            _reachAnchor = null;
            ReachOrder.Clear();
            _forceRefresh = true;
            _nextRefresh = 0f;
            if (IsOpen)
            {
                Refresh();
            }
        }

        internal static void ToggleReach()
        {
            _reachOpen = !_reachOpen;
            // AT ONCE, not at the safety net. With your bags closed nothing refreshed
            // here, and the next tick's signature saw no change, so the window
            // waited on the two-second fallback rebind before it appeared.
            // Forced, it lands on
            // the next tick.
            _forceRefresh = true;
            // ONE OCCUPANT OF THE SLOT: opening the vicinity window closes
            // whatever vanilla has there - crafting, collections, the dissect
            // panel, the shop - through vanilla's own close so its
            // state unwinds
            if (_reachOpen && _mn != null && _mn.uiMN != null && VanillaHoldsLeftSlot(_mn.uiMN))
            {
                _mn.uiMN.LeftPanelState(0);
            }
            _reachScroll = 0f;
            _reachAnchor = null;
            _nextRefresh = 0f;
            if (IsOpen)
            {
                Refresh();
            }
        }

        private static bool VanillaHoldsLeftSlot(UIManager ui)
        {
            return (ui.craftPanel != null && ui.craftPanel.activeSelf)
                || (ui.collectionPanel != null && ui.collectionPanel.activeSelf)
                || (ui.dissectPanel != null && ui.dissectPanel.activeSelf)
                || (ui.shopPanel != null && ui.shopPanel.activeSelf);
        }

        // ...and the crafting window, opening, closes this one
        internal static void CloseReach()
        {
            if (_reachOpen)
            {
                ToggleReach();
            }
        }

        // V, the vicinity button and the HUD chest ALWAYS MEAN THE VICINITY
        // (Forgejo #180). ToggleReach opens the slot with whichever list was last
        // in it, so V after the roster brought the roster back. The vicinity
        // showing closes; anything else - closed, or the roster - shows it.
        internal static void ToggleVicinity()
        {
            if (_reachOpen && _slotView == ViewReach)
            {
                CloseReach();
            }
            else
            {
                ShowSlotView(ViewReach);
            }
        }

        // Every container in reach, in a column of its own — the pool, with the
        // one you have OPEN sitting first in it and marked (D30). Before this the
        // same chest was drawn in your column when it was merely nearby and in the
        // focus column when you opened it, which made "opened" a place rather than
        // a state and left the pool borrowing the identity of your own column.
        //
        // What bounds the column is the CAP, as a scrolling window: NearbyMaxPlates
        // bands are drawn at a time and the wheel moves which ones, so a base with
        // fourteen storages is a list you page through rather than a wall. The
        // panel is also CLOSED by default — opening your bags should not dump
        // a base's worth of chests on screen — and its heading is what opens it.
        //
        // A search field over the pool was built and removed:
        // it dragged in a keyboard grab, an Escape step, and a focus-versus-collapse
        // policy that all had to be reasoned about together, which is too much
        // machinery to carry while the LAYOUT is still being settled.
        private static void BindReach(int cols, CommonStates player)
        {
            if (!_reachOpen)
            {
                return;
            }
            var found = new List<InventorySlot>();
            if (Settings.ShowNearby.Value && _mn.gameMN.pMove != null)
            {
                ScanReach(found);
            }
            // THE TARGET DOES NOT THIN THE VICINITY (user): the chest
            // you have open, or a person whose bag window is up, stays in the list
            // where it stands. Taking it out made the vicinity's contents depend on
            // what the target window showed, and the chest you had just opened
            // vanished from the list you found it in.
            _nearbyTotal = found.Count;
            // A query narrows the POOL to containers holding a match, and the
            // marking below narrows it again to the cells inside them. Neither
            // alone is the answer: filtering still leaves you scanning forty slots,
            // and marking still leaves you scrolling fourteen chests.
            //
            // _nearbyTotal is captured BEFORE this, so the heading can say "2 of
            // 14" rather than quietly claiming there are two containers near you.
            if (_filter.Active)
            {
                for (int i = found.Count - 1; i >= 0; i--)
                {
                    if (!HoldsMatch(found[i]))
                    {
                        found.RemoveAt(i);
                    }
                }
            }
            _nearbyMatched = found.Count;
            // An empty pool still draws. Returning early was
            // right while this was a SECTION of your bag - an empty frame there
            // said nothing and cost a band - and is wrong now that it is a window
            // you opened on purpose: asking "what is near me" and being shown
            // nothing at all cannot be told from the window failing to open.
            // "In reach 0" is an answer; a blank screen is not.
            // ONE shared surface, separators and all. Per-chest frames were tried
            // and regretted: a frame around every band turns a
            // list into a stack of boxes, and at seven containers the borders cost
            // more attention than the contents they were separating. The hairline
            // is the cheaper mark and it was already enough.
            //
            // The UNIFICATION stays - the focused container is still a member of
            // this window rather than a panel beside it. That was never about the
            // frames; it is about there being one region, with one of its members
            // marked. See below.
            BindGroupFrame(GroupReach, ReachColumn);

            // EVERY chest is bound; the WINDOW decides what is seen.
            //
            // The cheap version cropped by not drawing - a band admitted whole rows
            // only - and it was the wrong path for a reason worth recording: what
            // you see is then a function of what FITS, so the thing on screen keeps
            // changing shape as you move through it. Rendering everything and
            // showing a window over it makes scrolling a question about the VIEW
            // instead of about the content, which is the only version that can move
            // a pixel at a time.
            //
            // Members live under _reachContent, which is permanent and masked. The
            // SPACER below is what the rail sees: a plate with a height and no
            // cells, so the column stacking, the group extent and the frame all
            // size themselves without knowing a viewport exists.
            // In the slot the window is STATIC: columns from the slot's width, the
            // viewport the slot's height, whatever the pool holds. Off the slot
            // (no crafting panel to measure) it caps as before.
            bool slot = ReadSlot();
            if (slot)
            {
                cols = SlotCols();
            }
            float memberGap = ReachGap();
            // the reach binds only off the roster, so it always wears look 02
            bool compact = Settings.VicinityCompact.Value;
            // the foot sits under the list and does not scroll with it, so the
            // viewport gives up its height, as the roster's control row does
            EnsureReachFoot();
            float capH = Mathf.Max(CellPitch, (slot ? SlotViewHeight()
                : NearbyMaxHeight) - ReachFootH() - memberGap);
            EnsureReachContent();

            // Which bands the window can actually reach, from the heights the
            // LAST bind measured. A height this code has never seen is treated as
            // visible, so a new container is always painted in full on its first
            // frame and measured on the way - the cache corrects itself rather
            // than needing to be right in advance.
            float lo = _reachScroll;
            float hi = _reachScroll + capH;
            float walk = 0f;
            var lean = new bool[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                float h;
                if (!ReachHeights.TryGetValue(found[i].GetInstanceID(), out h))
                {
                    lean[i] = false;
                    walk += CellPitch + memberGap;   // a guess, corrected below
                    continue;
                }
                // one band of slack each way, so a row entering the view is
                // already painted when it gets there
                lean[i] = walk + h < lo - CellPitch || walk > hi + CellPitch;
                walk += h + memberGap;
            }

            float reachBegan = Time.realtimeSinceStartup;
            _bindingReach = true;
            ReachOrder.Clear();
            for (int i = 0; i < found.Count; i++)
            {
                InventorySlot chest = found[i];
                if (chest.slots == null || chest.slots.Length == 0)
                {
                    continue;
                }
                bool thin = lean[i];
                // FACELIFT LOOK 02: a person's portrait or the thing's picture, with
                // no caption under it (Vicinity.cs)
                CommonStates holder = VicHolder(chest);
                ItemData thing = holder == null ? ChestItem(chest) : null;
                Bind(string.Empty, chest.slots, chest.slots.Length, null, chest,
                    false, ReachColumn, cols, null, -1, false,
                    forceLabel: true,
                    group: GroupReach,
                    icon: thing != null ? thing.itemSprite : null, lean: thin,
                    compact: compact, person: holder);
                ReachHeights[chest.GetInstanceID()] =
                    ReachPlates[_reachPlatesUsed - 1].Height;
                ReachOrder.Add(chest);
                _nearbyShown++;
            }
            _bindingReach = false;
            _reachMs = (Time.realtimeSinceStartup - reachBegan) * 1000f;

            float contentH = 0f;
            float contentW = 0f;
            for (int i = 0; i < _reachPlatesUsed; i++)
            {
                Plate m = ReachPlates[i];
                contentH += m.Height + (i > 0 ? memberGap : 0f);
                contentW = Mathf.Max(contentW, m.Window.Rect.sizeDelta.x);
            }
            _reachContentH = contentH;
            // ALWAYS AS TALL AS IT CAN GET, however little it holds: a window that
            // shrank with its content moved its own heading controls about, and a
            // static one keeps them where the hand expects them
            _reachViewH = capH;

            // bound before the spacer: the column stacks bottom-up, so first is lowest
            BindReachFoot(slot ? VicFootW : Mathf.Max(160f, contentW));

            _reachSpacer = TakePlate();
            _reachSpacer.Column = ReachColumn;
            _reachSpacer.Group = GroupReach;
            _reachSpacer.IsViewport = true;
            _reachSpacer.Shown = 0;
            HideCells(_reachSpacer);
            _reachSpacer.Height = _reachViewH;
            _reachSpacer.LaidOutCols = -1;
            _reachSpacer.Back.enabled = false;
            _reachSpacer.Edge.enabled = false;
            _reachSpacer.Header.gameObject.SetActive(false);
            _reachSpacer.Window.Rect.sizeDelta = new Vector2(contentW, _reachViewH);
            if (!_reachSpacer.Window.Go.activeSelf)
            {
                _reachSpacer.Window.Go.SetActive(true);
            }
            _lastColumnWidth[ReachColumn] =
                Mathf.Max(_lastColumnWidth[ReachColumn], contentW);
            _lastGroupBound = GroupReach;

            // unconditional: it is what an empty window says, and the only thing
            // keeping the frame from being a box with nothing in it
            BindTitle(ReachCaption(), ReachColumn, group: GroupReach);
            // The heading's own plate, which BindTitle has just taken: one plate,
            // the single-plate branch a grouped title takes.
            BindHeadingControls(Plates[_platesUsed - 1]);
        }

        // The masked window the pool is seen through. Permanent, and deliberately
        // NOT a pooled plate: a plate carrying a RectMask2D would clip the cells of
        // whatever it was handed to next, and the members parented into it would
        // travel with it. One object that never changes job can do neither.
        private static void EnsureReachContent()
        {
            if (_reachContent != null)
            {
                return;
            }
            var go = Owned.New("ReachViewport");
            go.transform.SetParent(_root.transform, false);
            _reachContent = (RectTransform)go.transform;
            go.AddComponent<RectMask2D>();
        }

        // The frame a group of containers shares. Bound BEFORE its members, because
        // plates are pooled in creation order and that order is the sibling order:
        // bound first means drawn first means behind. It carries no cells, and
        // Layout gives it its rect once the members it spans have theirs.
        // One padding for a group frame, ornate or flat, read through here because
        // FOUR places need the same answer - the layout, the frame's own rect,
        // PlateWidth and TargetMaxCols - and a width estimate that disagrees with
        // the frame runs the rail off the screen.
        //
        // The ornate frame briefly forced this to 17/15, on the reading that
        // ui_frame's 64/60/70/61 border is sixteen units of ornament a side and
        // the corner would otherwise land on the slots. Half right: sixteen units
        // is the 9-SLICE CORNER REGION, and the drawn border along the edges
        // measures 4 to 6 units - the rest of that region is transparent air
        // around small knots. The original complaint came from the frame being
        // scaled by the wrong number entirely (sprite ppu instead of the Image's
        // multiplier), which drew the corners oversized. With that fixed, 17 is
        // three times what the edge needs and reads as a wide empty margin.
        private static float GroupPadX()
        {
            return FrameInsetX;
        }

        private static float GroupPadY()
        {
            return FrameInsetY;
        }

        // a frame's padding over its top member and under its lowest: the leather
        // keeps its head above the first section and its shadow below the last
        private static float FramePadTop(int group)
        {
            return IsLeather(group) ? LeatherPadTop : GroupPadY();
        }

        private static float FramePadFoot(int group)
        {
            return IsLeather(group) ? LeatherPadFoot : GroupPadY();
        }

        // The layer between a plate's Back and its cells. A group frame paints its
        // parchment here; a grouped member paints its type tint here. Parented to
        // the WINDOW, not to Content: Content's geometry is set by Bind and by the
        // title path, and a group frame runs through neither, so a field anchored
        // inside Content would sit at whatever offset the plate's previous life
        // left there.
        private static void EnsureField(Plate plate)
        {
            if (plate.Field != null)
            {
                return;
            }
            var go = Owned.New("Field");
            go.transform.SetParent(plate.Window.Go.transform, false);
            go.transform.SetSiblingIndex(0);   // above Back, under Content
            plate.Field = go.AddComponent<Image>();
            plate.Field.raycastTarget = false;
            plate.Field.preserveAspect = false;
        }

        // The rail: a full-rect child of the window, above everything the frame
        // holds. It never overlaps a cell - the cells sit inside the frame's
        // padding - so its place in the order only has to be above the back.
        private static void EnsureRail(Plate plate)
        {
            if (plate.Rail != null)
            {
                return;
            }
            var go = Owned.New("Rail");
            go.transform.SetParent(plate.Window.Go.transform, false);
            plate.Rail = go.AddComponent<Image>();
            plate.Rail.raycastTarget = false;
            plate.Rail.type = Image.Type.Sliced;
            plate.Rail.pixelsPerUnitMultiplier = Theme.KitPpu;
            plate.Rail.color = Color.white;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
