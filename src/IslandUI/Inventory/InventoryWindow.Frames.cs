using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Close chips, scroll mark, reach content layout, group frames.
    internal static partial class InventoryWindow
    {
        private static void EnsureClose(Plate plate, float rise = 0f)
        {
            if (plate.Close == null)
            {
                plate.Close = CloseChip.Create(plate.Window.Go.transform);
            }
            CloseChip.Place(plate.Close, rise);
        }

        // The chips: hit-tested BEFORE the cells, since a chip overhangs its frame
        // and the cell scan has no idea anything is on top of it. Returns the
        // plate whose chip is under the pointer, or null.
        private static Plate TickCloseChips(Camera cam)
        {
            Plate hot = null;
            for (int pass = 0; pass < 2 && hot == null; pass++)
            {
                List<Plate> pool = pass == 0 ? Plates : ReachPlates;
                int used = pass == 0 ? _platesUsed : _reachPlatesUsed;
                for (int i = 0; i < used && i < pool.Count; i++)
                {
                    Plate p = pool[i];
                    bool carries = p.IsGroupFrame || (p.IsTitle && p.TabTitle);   // the head carries its group's close
                    if (!carries || p.Close == null || !p.Close.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            p.Close.rectTransform, Input.mousePosition, cam))
                    {
                        hot = p;
                        break;
                    }
                }
            }
            if (Input.GetMouseButtonDown(0) && !_dragging)
            {
                _pressedClose = hot;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _pressedClose != null && ReferenceEquals(_pressedClose, hot) && !_dragging;
                int group = hot != null ? hot.Group : GroupNone;
                _pressedClose = null;
                if (fire)
                {
                    CloseGroup(group);
                    return hot;
                }
            }
            else if (_pressedClose != null && !Input.GetMouseButton(0))
            {
                _pressedClose = null;
            }
            // paint every visible chip the one way: hover lights, a press sinks
            for (int pass = 0; pass < 2; pass++)
            {
                List<Plate> pool = pass == 0 ? Plates : ReachPlates;
                int used = pass == 0 ? _platesUsed : _reachPlatesUsed;
                for (int i = 0; i < used && i < pool.Count; i++)
                {
                    Plate p = pool[i];
                    if (p.Close == null || !p.Close.gameObject.activeSelf)
                    {
                        continue;
                    }
                    bool over = ReferenceEquals(p, hot);
                    CloseChip.Paint(p.Close, over, over && ReferenceEquals(p, _pressedClose));
                }
            }
            return hot;
        }

        // What a frame's chip closes: your own bags (what B does), the vicinity
        // window (what V does), or the focus - the interaction the game began,
        // ended the way vanilla's own close button ends it.
        private static void CloseGroup(int group)
        {
            switch (group)
            {
                case GroupOwn:
                    Close();
                    break;
                case GroupReach:
                    if (ReachOpen)
                    {
                        ToggleReach();
                    }
                    break;
                case GroupCharacter:
                    if (Focus() is CommonStates)
                    {
                        _sheetOpen = false;
                        _forceRefresh = true;
                        _nextRefresh = 0f;
                    }
                    else if (Settings.TheUnit.Value)
                    {
                        // D156: a thing's window is its Inventory door's, as a
                        // person's bag window is - the chip folds it, the target stays
                        _bagOpen = false;
                        _forceRefresh = true;
                        _nextRefresh = 0f;
                    }
                    else { CloseFocus(_mn); }
                    break;
                case GroupOwnSheet:
                    _ownSheetOpen = false;
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    break;
                case GroupBag:
                    _bagOpen = false;   // the bag window folds; the target stays
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    break;
            }
        }

        // Vanilla's close for a container is SubInventoryVisible (drops the target,
        // unloads the mirror slots, refills crafting) and for an NPC menu
        // NPCMenuClose; a chest focus also lives in buildMN.tmpProp and a talk in
        // pMove.searchNPC, which PlayerMove clears itself when the interaction
        // ends (PlayerMove.cs:867, :922). tmpProp is cleared only when it IS a
        // container, so a prop being placed is never touched.
        private static void CloseFocus(ManagersScript mn)
        {
            if (mn == null)
            {
                return;
            }
            NoteEnd("closed");
            InventoryManager inv = mn.inventory;
            if (inv != null && inv.subInventory != null && inv.subInventory.activeSelf)
            {
                inv.SubInventoryVisible();
            }
            if (mn.uiMN != null && mn.uiMN.npcPanel != null && mn.uiMN.npcPanel.activeSelf)
            {
                mn.uiMN.NPCMenuClose();
            }
            if (mn.gameMN != null)
            {
                mn.gameMN.menuNPC = null;
                if (mn.gameMN.pMove != null)
                {
                    mn.gameMN.pMove.target = null;
                    mn.gameMN.pMove.searchNPC = null;
                }
            }
            if (mn.buildMN != null && mn.buildMN.tmpProp != null
                && mn.buildMN.tmpProp.GetComponent<InventorySlot>() != null)
            {
                mn.buildMN.tmpProp = null;
            }
            Invalidate();
        }

        // How far the heading's mark overshoots what it backs. Paint does not stop
        // where the letters do. Y is the smaller of the two because a title sits
        // under the frame's own padding and has less room above it than beside it.
        private const float TitleMarkPadX = 9f;
        private const float TitleMarkPadY = 4f;

        // the brush mark behind a heading, and its colour on a container's
        private const string TitleSprite = "brush_5";
        private static readonly Color TitleSplashColor = Theme.ParseColor("#6E5138D9", Color.black);
        // Man and Yona's headings: off the friends-list palette, whose entry for
        // them is black
        private static readonly Color MainCharacterTint = Theme.ParseColor("#0D1A44", Color.black);
        // the target's frame lit, the others dimmed beneath it (on the fallback frames)
        private static readonly Color TargetFrameTint = Theme.ParseColor("#FFF6E2", Color.white);
        private static readonly Color UntargetedFrameTint = Theme.ParseColor("#A79C90", Color.white);

        // Shared slot gutter: a visible track and draggable, proportional thumb.
        private const float KitBarW = Slot.BarW;

        // ONE NOTCH OF THE WHEEL, and the only place the scroll is moved by hand.
        // Extracted from Tick's wheel block so the harness can take the SAME step
        // rather than a copy of it: Input.mouseScrollDelta cannot be faked from
        // managed code (docs/test-harness.md §1), so a scroll bug is otherwise
        // only reproducible by a person with a mouse.
        //
        // MINUS, and it was right all along. A wheel pushed up (positive) walks
        // the view back toward the top of the list; pushed down, it reveals what
        // is below. Both lists want that and both always did.
        //
        // It was once flipped to plus and flipped straight back, because
        // the report it answered - "the scroll is inversed" - was about the THUMB,
        // not the content. The mark was drawn at `trackH - fromTop - markH`, so an
        // unscrolled list put it at the BOTTOM of the track and it walked UP as
        // you went down: measured at scroll 0, thumb y -143 of a 168-unit track.
        // Content going one way and the mark the other reads as an inverted
        // scroll, and flipping the content merely swapped which half was lying -
        // which is what the vicinity list then showed: its scroll now inverted.
        //
        // The lesson worth keeping: a direction complaint about a scroll is two
        // claims, and the mark is the one that had never been checked.
        //
        // Only the floor is clamped here. The ceiling is LayoutReachContent's,
        // because it is the only place that knows the content's height.
        internal static bool ScrollReach(float notches)
        {
            if (!_reachOpen)
            {
                return false;
            }
            float before = _reachScroll;
            _reachScroll = Mathf.Max(0f, _reachScroll - notches * CellPitch);
            // the pin follows the pointer, not the other way round: a deliberate
            // scroll re-decides what the view is anchored to
            _reachAnchor = null;
            if (Mathf.Approximately(before, _reachScroll))
            {
                return false;
            }
            _nextRefresh = 0f;
            Refresh();
            return true;
        }

        // A ROW SCROLLED OUT OF THE VIEW IS HIDDEN BY A MASK, NOT BY BEING GONE
        // (a row that was not visible lay under the controls and could still be
        // clicked).
        //
        // RectMask2D clips the DRAWING and leaves the rect exactly where it is,
        // and every control in this window is hit-tested by rect arithmetic rather
        // than by a raycast - which is what makes the pointer see through the
        // control row into a row scrolled up behind it. The mask was never part of
        // the hit test because until the roster there was nothing much to scroll.
        //
        // So the viewport is asked as well: a plate parented into the masked
        // content is only hittable while the pointer is inside that content. The
        // parent is the test rather than a flag, because it is the same thing the
        // mask itself uses - anything the mask can clip, this clips.
        private static int _maskFrame = -1;
        private static bool _maskInside;

        private static bool PointerInReachView(Camera cam)
        {
            if (_maskFrame != Time.frameCount)
            {
                _maskFrame = Time.frameCount;
                _maskInside = _reachContent != null
                    && _reachContent.gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(
                        _reachContent, Input.mousePosition, cam);
            }
            return _maskInside;
        }

        private static bool ClippedAway(Plate plate, Camera cam)
        {
            if (plate != null && plate.SheetMask != null && plate.SheetMask.enabled
                && !RectTransformUtility.RectangleContainsScreenPoint(plate.Window.Rect, Input.mousePosition, cam))
            {
                return true;
            }
            return _reachContent != null && plate != null && plate.Window.Go != null
                && plate.Window.Go.transform.parent == _reachContent
                && !PointerInReachView(cam);
        }

        // THE BAR IS A CONTROL, NOT ONLY A MARK: it can be dragged with the left
        // mouse button. It was built as a readout -
        // "one mark whose LENGTH is the fraction on screen and whose POSITION is
        // where that fraction sits" - and a readout is all it ever was.
        //
        // Polled and rect-tested like every other control in this window, not
        // wired to the EventSystem (§ the note on FilterField: focus is the
        // window's to give). The grab band is wider than the 6.5-unit bar, since
        // the art is the thing you aim at and 6.5 units is not a target.
        private static bool _scrollDrag;
        private static float _scrollGrab;    // where in the thumb it was taken, downward from its top
        private const float ScrollGrabSlop = 5f;

        private static bool TickScrollBar(Camera cam)
        {
            Plate w = _reachSpacer;
            bool live = _reachOpen && w != null && w.Scroll != null
                && w.Scroll.gameObject.activeInHierarchy
                && w.ScrollThumb != null && w.ScrollThumb.gameObject.activeSelf;
            if (!live)
            {
                _scrollDrag = false;
                return false;
            }
            RectTransform track = w.Scroll.rectTransform;
            float trackH = track.rect.height;
            float markH = w.ScrollThumb.rectTransform.rect.height;
            float span = trackH - markH;
            float range = _reachContentH - _reachViewH;
            if (span <= 0.01f || range <= 0.01f)
            {
                _scrollDrag = false;
                return false;
            }
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track, Input.mousePosition, cam, out local);
            // Theme.Place pivots top-left, so local.y is 0 at the top and negative
            // going down; the whole control thinks in downward units
            float py = -local.y;
            bool over = py >= -ScrollGrabSlop && py <= trackH + ScrollGrabSlop
                && local.x >= -ScrollGrabSlop && local.x <= track.rect.width + ScrollGrabSlop;
            float thumbY = -w.ScrollThumb.rectTransform.anchoredPosition.y;

            if (Input.GetMouseButtonDown(0) && over && !_dragging)
            {
                // on the thumb, keep the grip where it was taken; on the bare
                // track, jump so the thumb centres under the pointer
                _scrollGrab = py >= thumbY && py <= thumbY + markH
                    ? py - thumbY : markH * 0.5f;
                _scrollDrag = true;
            }
            if (_scrollDrag && !Input.GetMouseButton(0))
            {
                _scrollDrag = false;
            }
            if (_scrollDrag)
            {
                float want = Mathf.Clamp(py - _scrollGrab, 0f, span) / span * range;
                if (!Mathf.Approximately(want, _reachScroll))
                {
                    _reachScroll = want;
                    _reachAnchor = null;   // a deliberate move re-decides the pin
                    _nextRefresh = 0f;
                    Refresh();
                }
            }
            // hover and press paint, so it looks like the control it now is
            Color accent = ShedThumb;
            w.ScrollThumb.color = _scrollDrag ? Color.Lerp(accent, Color.black, 0.18f)
                : over ? Color.Lerp(accent, Color.white, 0.2f) : accent;
            return _scrollDrag || over;
        }

        private static void PlaceScrollMark(Plate window)
        {
            if (window == null)
            {
                return;
            }
            bool want = _reachContentH > _reachViewH + 0.5f && _reachViewH > 0f;
            if (!want)
            {
                if (window.Scroll != null && window.Scroll.gameObject.activeSelf)
                {
                    window.Scroll.gameObject.SetActive(false);
                }
                return;
            }
            if (window.Scroll == null)
            {
                var go = Owned.New("Scroll");
                go.transform.SetParent(window.Window.Go.transform, false);
                window.Scroll = go.AddComponent<Image>();
                window.Scroll.raycastTarget = true;
                window.ScrollThumb = Theme.KitImage(go.transform, "Thumb", "kit_cross", 0, Color.white);
                window.ScrollThumb.raycastTarget = false;
            }
            window.Scroll.sprite = Theme.RoundedFill;
            window.Scroll.type = Image.Type.Sliced;
            window.Scroll.color = ShedTrack;
            window.ScrollThumb.sprite = Theme.RoundedFill;
            window.ScrollThumb.type = Image.Type.Sliced;
            window.ScrollThumb.color = ShedThumb;
            window.ScrollThumb.gameObject.SetActive(true);

            // Measured against the WINDOW, which is the whole scrollable extent -
            // the focused container included, since it is now a member like any
            // other rather than a panel parked beside them.
            Vector2 size = window.Window.Rect.sizeDelta;
            float trackH = Mathf.Max(1f, size.y);
            float markH = Mathf.Min(trackH, Mathf.Max(ScrollMarkMin,
                trackH * Mathf.Clamp01(_reachViewH / Mathf.Max(1f, _reachContentH))));
            // THE THUMB RIDES DOWN THE TRACK AS THE LIST DOES.
            // It was `trackH - fromTop - markH`, which put the thumb at the BOTTOM
            // of the track for an unscrolled list and walked it UP as you went
            // down - measured at scroll 0, thumb y -143 of a 168-unit track. The
            // mark was saying the opposite of where you were, which is most of
            // why the whole control read as backwards.
            //
            // Theme.Place measures y DOWNWARD from the top, so fromTop is already
            // the answer and the arithmetic that reversed it was the bug.
            float span = Mathf.Max(0f, trackH - markH);
            float range = Mathf.Max(1f, _reachContentH - _reachViewH);
            float fromTop = span * Mathf.Clamp01(_reachScroll / range);
            float y = Mathf.Clamp(fromTop, 0f, span);
            // A dedicated gutter keeps both lists' full content width intact.
            float x = _slotKnown && _reachOpen
                ? _slotW - GroupPadX() - Slot.Inset - KitBarW
                : size.x + Slot.BarGap;
            Theme.Place(window.Scroll, x, 0f, KitBarW, trackH);
            Theme.Place(window.ScrollThumb, 0f, y, KitBarW, markH);
            if (!window.Scroll.gameObject.activeSelf)
            {
                window.Scroll.gameObject.SetActive(true);
            }
            window.Scroll.transform.SetAsLastSibling();
        }

        // Wider and darker than it started. It was tuned while
        // it still sat INSIDE the panel, against parchment, where three faint units
        // were plenty. Outside it stands over the world instead - lit sand, wet
        // rock, night - and a hint that only reads against one background is not a
        // hint. Ink at high alpha is the one value that survives all of them, and
        // it is the same ink the headings are painted in.
        private const float ScrollMarkW = 5f;
        // it sits in the gap BETWEEN columns, so it may not be wider than one
        private const float ScrollMarkGap = 2f;
        private const float ScrollMarkMin = 12f;

        // The pool inside its window. Everything is already bound; this decides
        // only WHERE it sits, which is what makes the scroll continuous.
        private static void LayoutReachContent()
        {
            if (_reachContent == null)
            {
                return;
            }
            bool live = _reachSpacer != null && _reachPlatesUsed > 0
                && _reachSpacer.Window.Go.activeInHierarchy;
            if (!live)
            {
                if (_reachContent.gameObject.activeSelf)
                {
                    _reachContent.gameObject.SetActive(false);
                }
                return;
            }
            // the window sits exactly where the spacer reserved: same parent, so
            // the rect copies across directly
            RectTransform spacer = _reachSpacer.Window.Rect;
            _reachContent.anchorMin = spacer.anchorMin;
            _reachContent.anchorMax = spacer.anchorMax;
            _reachContent.pivot = spacer.pivot;
            // ...widened LEFT by the frame's inset, so the mask's edge is the
            // frame's inner edge rather than the members' own: a gutter glyph now
            // sits centred in that band, partly left of its member
            float bandX = GroupPadX();
            _reachContent.sizeDelta = spacer.sizeDelta + new Vector2(bandX, 0f);
            _reachContent.anchoredPosition = spacer.anchoredPosition
                - new Vector2(bandX * (1f - spacer.pivot.x), 0f);
            if (!_reachContent.gameObject.activeSelf)
            {
                _reachContent.gameObject.SetActive(true);
            }

            // The roster and the vicinity share this pool and want different
            // air between members, so the gap is asked for rather than read (ReachGap).
            float memberGap = ReachGap();
            // Restore the scroll from what it is PINNED to before clamping. A pixel
            // offset does not survive a rescan - walk two steps and the pool
            // re-sorts by distance, so the same number points at different chests -
            // but "this container, this far into it" does.
            //
            // ONE-SIDED: walking through a base, the view ended up somewhere in
            // the middle, where it should hold still and drift towards the top.
            // A symmetric pin
            // is what put it in the middle: a nearer chest arriving above the
            // anchor pushed the scroll down by its height and hid itself above
            // the edge, one leaving pulled it up by the same, and churn left the
            // view wherever it left it. So the pin may only ever pull the view
            // TOWARD the top: a departure above still lifts it, an arrival above
            // no longer pushes it down - the anchor slides one band and the nearer
            // chest takes the top of the view instead. Under churn the scroll is
            // non-increasing (only the wheel moves it down), no single event moves
            // the view by more than one band, and enough walking ends at the top.
            if (_reachAnchor != null)
            {
                float cum = 0f;
                for (int i = 0; i < _reachPlatesUsed && i < ReachOrder.Count; i++)
                {
                    if (ReferenceEquals(ReachOrder[i], _reachAnchor))
                    {
                        _reachScroll = Mathf.Min(_reachScroll, cum + _reachAnchorOffset);
                        break;
                    }
                    cum += ReachPlates[i].Height + memberGap;
                }
            }
            float maxScroll = Mathf.Max(0f, _reachContentH - _reachViewH);
            _reachScroll = Mathf.Clamp(_reachScroll, 0f, maxScroll);

            // Stacked from the TOP, the closest inventory first: index
            // 0 is the nearest chest and sits highest, under the header, and the
            // scroll counts down from there - the window hangs from the slot's
            // top now, so that is where the eye starts. Members are pivoted
            // bottom-left inside the window; y walks down from its top edge.
            float y = _reachViewH + _reachScroll;
            _reachAnchor = null;
            for (int i = 0; i < _reachPlatesUsed; i++)
            {
                Plate m = ReachPlates[i];
                RectTransform r = m.Window.Rect;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.zero;
                r.pivot = Vector2.zero;
                y -= m.Height;
                r.anchoredPosition = new Vector2(bandX, y);
                if (_reachAnchor == null && y < _reachViewH && i < ReachOrder.Count)
                {
                    // the highest member still on screen: what the scroll is
                    // pinned to until the pointer moves it again, and how far its
                    // top stands above the window's edge
                    _reachAnchor = ReachOrder[i];
                    _reachAnchorOffset = y + m.Height - _reachViewH;
                }
                y -= memberGap;
            }
            // The wheel and the mark both used to hang off the group FRAME, which
            // no longer exists now that each container carries its own. They belong
            // to the WINDOW anyway: the mark measures what the window shows of the
            // content, and the wheel is what moves it.
            _reachPanel = _reachContent;
            PlaceScrollMark(_reachSpacer);

            // above the frame it sits in, below the hover readout
            _reachContent.SetAsLastSibling();
            if (_hover != null)
            {
                _hover.transform.SetAsLastSibling();
            }
        }

        // The ramp below the solid head. Short: its only job is to stop the head
        // itself having a hard bottom edge.
        // Which group holds what your actions land on. _focusShown is settled in
        // Tick, before any binding, so every frame in a refresh gets the same
        // answer rather than depending on whether it was bound before or after the
        // target was resolved.
        private static int TargetGroup()
        {
            return _focusShown ? GroupCharacter : GroupOwn;
        }

        // The colour the FRIENDS LIST gives this character, asked of the game
        // rather than reproduced. UIManager.GetNPCColor is public and indexes the
        // same fSlotColor table the list slots use, so a heading cannot drift from
        // the roster it names - and when the game adds a type, this follows.
        //
        // blackIsWhite stays FALSE: it exists for slots drawn on a dark panel, and
        // a heading here sits on parchment, where the black entry is the one that
        // reads.
        private static Color? NPCColor(CommonStates who)
        {
            if (who == null || _mn == null || _mn.uiMN == null)
            {
                return null;
            }
            LogSlotPalette();
            LogWho(who);
            // Man and Yona (npcID 0 and 1) are given the BLACK entry by the game,
            // which is the right choice for a list slot and a poor one for a
            // heading: it is the only pair whose colour says nothing about what they
            // are. So they wear a colour of their OWN. They are never in the friends
            // list, so nothing there can disagree - and black, which IS their
            // identity colour elsewhere, sits a shade from the container brown at
            // heading size, which is the one distinction this window has to make.
            if (who.npcID == 0 || who.npcID == 1)
            {
                return MainCharacterTint;
            }
            return _mn.uiMN.GetNPCColor(who.npcID);
        }

        // Who is asking, and what the game calls them. The heading colour depends
        // on npcID matching 0 or 1 for the two main characters - which is read off
        // GetNPCColorID having a case for that pair, and an inference, not a fact.
        // One line per distinct id, so this says whether the assumption holds.
        private static readonly System.Collections.Generic.HashSet<int> LoggedWho
            = new System.Collections.Generic.HashSet<int>();

        private static void LogWho(CommonStates who)
        {
            if (!LoggedWho.Add(who.npcID))
            {
                return;
            }
            Color c = _mn.uiMN.GetNPCColor(who.npcID);
            Plugin.Log.LogInfo($"Theme: character '{who.charaName}' npcID={who.npcID} "
                + $"colourID={_mn.uiMN.GetNPCColorID(who.npcID)} #{ColorUtility.ToHtmlStringRGBA(c)}");
        }

        private static bool _paletteLogged;

        // Printed once, because the palette is serialised scene data - it exists
        // nowhere in the decompiled source, so the only way to know which entry is
        // which colour is to ask the running game.
        private static void LogSlotPalette()
        {
            if (_paletteLogged || _mn.uiMN.fSlotColor == null)
            {
                return;
            }
            _paletteLogged = true;
            var sb = new System.Text.StringBuilder("Theme: friends-list palette ");
            for (int i = 0; i < _mn.uiMN.fSlotColor.Length; i++)
            {
                sb.Append(i).Append('=').Append('#')
                  .Append(ColorUtility.ToHtmlStringRGBA(_mn.uiMN.fSlotColor[i]))
                  .Append("  ");
            }
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static bool OrnateFrames()
        {
            return Theme.BagSkin.Valid;
        }

        // How far inside the frame the field starts. Vanilla shows a thin band of
        // the rim between the brown edge and the cream, so the field is inset past
        // the edge and not past the whole ornament.
        private const float FieldInset = 6f;

        private static void BindGroupFrame(int group, int column)
        {
            Plate plate = TakePlate();
            plate.Column = column;
            plate.Group = group;
            plate.IsGroupFrame = true;
            plate.IsTitle = false;
            plate.IsSharedBag = false;
            plate.IsLoadout = false;
            plate.Owner = null;
            plate.Shown = 0;
            plate.Height = 0f;
            plate.LaidOutCols = -1;
            for (int i = 0; i < plate.Cells.Count; i++)
            {
                if (plate.Cells[i].Go.activeSelf)
                {
                    plate.Cells[i].Go.SetActive(false);
                }
            }
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            if (plate.Badge != null && plate.Badge.gameObject.activeSelf)
            {
                plate.Badge.gameObject.SetActive(false);
            }
            // The harvested `ui_frame` is a decorated 9-slice built to wrap ONE
            // panel. Three of them stacked down a rail is three sets of corner
            // ornament competing for the same eye, and at this padding the
            // corners land on the slots. A flat parchment field with a hairline
            // edge says "one surface" without saying it three times.
            Theme.Skin skin = Theme.BagSkin;
            Sprite kitField = Theme.Kit("kit_field", 12);
            Sprite kitRail = kitField != null ? Theme.Kit("kit_rail", 12) : null;
            bool kit = kitField != null && kitRail != null;
            if (kit && IsDossier(group))
            {
                // a character sheet is a dossier: kraft folder, paper on it
                SkinDossierFrame(plate, group);
            }
            else if (kit && (IsCrate(group) || IsCork(group)))
            {
                // the tool shed: a container is a crate, the roster a corkboard
                SkinShedFrame(plate, group);
            }
            else if (kit)
            {
                // THE KIT'S FRAME, as on the settings window (F5): the field's own
                // organic parchment as the back, a hairline walnut rail over the
                // whole rect. No title plate here - a group's
                // heading stays inside on its brush mark. The field carries its
                // own colour, so Back.color stays white for the target light to
                // multiply into.
                plate.Back.sprite = kitField;
                plate.Back.type = Image.Type.Sliced;
                plate.Back.pixelsPerUnitMultiplier = Theme.KitPpu;
                plate.Back.color = group == GroupReach && RosterView ? Theme.RosterFrameTint : Color.white;
                plate.Edge.enabled = false;
                if (plate.Field != null && plate.Field.gameObject.activeSelf)
                {
                    plate.Field.gameObject.SetActive(false);
                }
                if (plate.Mark != null && plate.Mark.gameObject.activeSelf)
                {
                    plate.Mark.gameObject.SetActive(false);   // a pooled dossier's stamp or clip
                }
                EnsureRail(plate);
                // The target used to be lit by its RAIL - pine where the rest were
                // walnut - and that read as nothing: the target highlight was too
                // weak to notice.
                // So every frame wears the kit rail, and the target's NAME PLATE
                // is the lit thing - see BindStyledTitle.
                plate.Rail.sprite = kitRail;
                plate.Rail.enabled = true;
                // A CLOSE CHIP ON EVERY FRAME, as vanilla puts one on its target
                // panel. The one close (CloseChip),
                // half in over the top-right corner; what it closes is decided by
                // the group - see CloseGroup.
                EnsureClose(plate);
                if (!plate.Close.gameObject.activeSelf)
                {
                    plate.Close.gameObject.SetActive(true);
                }
            }
            else if (OrnateFrames())
            {
                plate.Back.sprite = skin.Sprite;
                plate.Back.type = skin.Type;
                plate.Back.pixelsPerUnitMultiplier = skin.PixelsPerUnit;
                Color color = skin.Color;
                color.a = 1f;
                plate.Back.color = color;
                plate.Edge.enabled = false;
                EnsureField(plate);
                plate.Field.sprite = Theme.RoundedFill;
                plate.Field.type = Image.Type.Sliced;
                plate.Field.color = group == GroupReach && RosterView
                    ? Theme.ParchmentFill * Theme.RosterFrameTint : Theme.ParchmentFill;
                if (!plate.Field.gameObject.activeSelf)
                {
                    plate.Field.gameObject.SetActive(true);
                }
            }
            else
            {
                plate.Back.sprite = Theme.RoundedFill;
                plate.Back.type = Image.Type.Sliced;
                plate.Back.pixelsPerUnitMultiplier = 1f;
                plate.Back.color = group == GroupReach && RosterView
                    ? Theme.ParchmentFill * Theme.RosterFrameTint : Theme.ParchmentFill;
                plate.Edge.enabled = true;
                plate.Edge.effectColor = Theme.EdgeDefault;
                if (plate.Field != null && plate.Field.gameObject.activeSelf)
                {
                    plate.Field.gameObject.SetActive(false);
                }
            }
            plate.Back.enabled = true;
            // WHOSE TURN IT IS, as a place rather than a mode. Vanilla never says
            // who your next action lands on - the complaint that produced D25 and
            // outlived every attempt to answer it with behaviour - and the honest
            // answer is not a rule about which container acts, it is a light on the
            // one that will. No target lights YOUR bags, because with nothing
            // focused you are what your actions reach.
            //
            // Multiplied into whatever the branch above chose rather than replacing
            // it: the frame is harvested art with its own colour, and this is meant
            // to read as that frame LIT, not as a different frame.
            // Lit by CONTRAST, because Image.color multiplies and multiplication
            // cannot brighten anything. Pushing the target tint harder only makes it
            // more orange, never more lit - so the target keeps (near) full strength
            // and everything else is dimmed beneath it. Same trick a stage uses: you
            // do not brighten the actor, you drop the house lights.
            Color mul = kit ? Color.white : group == TargetGroup()
                ? TargetFrameTint : UntargetedFrameTint;
            Color was = plate.Back.color;
            plate.Back.color = new Color(was.r * mul.r, was.g * mul.g,
                was.b * mul.b, was.a);
            // Draw order is settled in Layout, once every plate has been bound and
            // the whole roster is known. It used to be settled HERE, by parking each
            // frame in a reserved slot (own 0, character 1, reach 2) - which is
            // correct only while members occupy index 3 and up. With the vicinity
            // window open on its own they do not: plate 0 takes slot 0 as the own
            // frame, the reach frame is forced to slot 2, and that shove pushes the
            // FIRST MEMBER down to slot 1 - behind the frame, under its opaque
            // field. One row of every vicinity window has been painted over since
            // the reservation was written, and which row moved as the pool changed
            // shape, which is what made it read as four unrelated bugs.
            plate.Window.Go.SetActive(true);
        }
    }
}
