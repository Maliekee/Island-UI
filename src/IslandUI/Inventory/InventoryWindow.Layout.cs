using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Layout of the columns, the shared bag corner.
    internal static partial class InventoryWindow
    {
        // Bottom-anchored columns, packed right to left: your own containers at the
        // right edge, the focused character next, every container in reach last.
        // All grow upward from the same baseline, so a container with more rows
        // pushes what sits above it higher instead of shifting the whole layout.
        //
        // An EMPTY column costs nothing and is not skipped over with a gap — with
        // no character in focus the reach column simply takes the slot next to
        // yours, so what is on screen never drifts sideways for what is not.
        // Does this group hold nothing but its title plate?
        private static bool GroupIsTitleOnly(int g)
        {
            int members = 0;
            for (int i = 0; i < _platesUsed; i++)
            {
                Plate p = Plates[i];
                if (p.Group != g || p.IsGroupFrame)
                {
                    continue;
                }
                if (!p.IsTitle)
                {
                    return false;
                }
                members++;
            }
            return members > 0;
        }

        private static void Layout()
        {
            const float margin = MarginX;
            const float baseY = MarginY;
            const float gap = PlateGap;
            const float columnGap = ColumnGap;

            for (int c = 0; c < ColumnCount; c++)
            {
                _colWidth[c] = 0f;
                // §6.20: a column with aspect tabs starts its frame a row higher,
                // so the tabs hang in the margin under it. The foot is the fixed
                // point either way.
                _colY[c] = baseY;
            }
            // THE SHEET STANDS ON THE UNIT (D108): while the unit is up, the
            // target column's foot is the unit's top rather than the screen's edge,
            // its tabs hanging in the air between. Moving the tabs to the top rim
            // is a separate step; without it this is the whole of the re-anchoring.
            float standing = UnitStanding();
            if (standing > 0f)
            {
                // ...and no room for foot tabs that sit on the sheet's top rail
                // instead (TabTitle): reserving it anyway lifted the target's bag
                // by a tab row whenever Details opened
                int footGroup = ColumnGroup(CharacterColumn);
                bool footTabs = _footShown[footGroup] && !TabTitle(footGroup);
                _colY[CharacterColumn] = Mathf.Max(_colY[CharacterColumn],
                    standing + gap + (footTabs ? KitFootTabs.RowH : 0f));
            }
            // the vicinity column stacks from the SLOT's floor, not the screen's
            bool slot = _reachOpen && ReadSlot();
            // how much shorter than the slot the vicinity is this refresh: the
            // bands' shortfall against the slot's full view, and nothing else
            // ...and the roster's control row is CONTENT, not shortfall: it sits
            // in the frame above the viewport, so the frame is short by what the
            // list lacks, not by what the bar takes.
            float reachShrink = slot
                ? Mathf.Max(0f, SlotViewHeight() - _reachViewH - RosterExtraH()) : 0f;
            if (slot)
            {
                // Keep the dock edge fixed as the content changes height.
                _colY[ReachColumn] = _slotBottom;
            }
            float padX = GroupPadX();
            float padY = GroupPadY();
            const float memberGap = GroupMemberGap;
            for (int g = 0; g < GroupCount; g++)
            {
                _groupFrame[g] = -1;
                _groupMembers[g] = 0;
                _groupLo[g] = 0f;
                _groupHi[g] = 0f;
                _groupW[g] = 0f;
            }
            for (int i = 0; i < _platesUsed; i++)
            {
                Plate plate = Plates[i];
                if (plate.IsGroupFrame)
                {
                    _groupFrame[plate.Group] = i;
                    continue;   // sized from what it spans, not from itself
                }
                // a grouped member is narrower than its column by the frame it
                // sits inside, so the COLUMN's width is the frame's
                float w = plate.Window.Rect.sizeDelta.x
                    + (plate.Group != GroupNone ? 2f * padX : 0f);
                _colWidth[plate.Column] = Mathf.Max(_colWidth[plate.Column], w);
                if (plate.Group != GroupNone)
                {
                    _groupW[plate.Group] =
                        Mathf.Max(_groupW[plate.Group], plate.Window.Rect.sizeDelta.x);
                    if (plate.IsTitle && plate.TabTitle)
                    {
                        // the plate's frame: four cells at least, the plate and the
                        // chip's corner at least, and the column with it
                        float need = Mathf.Max(PlateFrameMinW(),
                            plate.Window.Rect.sizeDelta.x + HeaderControlReserve(plate.Group));
                        _groupW[plate.Group] = Mathf.Max(_groupW[plate.Group], need);
                        _colWidth[plate.Column] = Mathf.Max(_colWidth[plate.Column], need + 2f * padX);
                    }
                }
            }
            // What the column measures on its Inventory aspect is what every other
            // aspect is cut to (§6.20), so a sheet reads the width its bags set.
            for (int g = 1; g < GroupCount; g++)
            {
                if ((g == GroupOwn || g == GroupBag) && _groupW[g] > 0f)
                {
                    _inventoryW[g] = _groupW[g];
                    _inventoryW[g == GroupOwn ? GroupOwnSheet : GroupCharacter] = _groupW[g];
                }
            }
            // A FIXED home for the target/vicinity column, rather than a place that
            // falls out of how wide your own bags happen to be. Packed right-to-left
            // it moves whenever your loadout does, so the same chest is somewhere
            // new every time you pick something up.
            float barX = ActionBarLeftEdge();
            bool pinOther = !float.IsNaN(barX);

            float x = -margin;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (c == ReachColumn && slot)
                {
                    // pinned to the slot's left edge, out of the packing flow
                    _colAnchorX[c] = 0f;
                    _colPivotX[c] = 0f;
                    _colX[c] = _slotLeft;
                    continue;
                }
                if (c == OwnColumn)
                {
                    _colAnchorX[c] = 1f;
                    _colPivotX[c] = 1f;
                    _colX[c] = x;
                }
                else if (pinOther)
                {
                    // anchored to the canvas LEFT and pivoted there too, so _colX is
                    // the column's own left edge rather than its right
                    _colAnchorX[c] = 0f;
                    _colPivotX[c] = 0f;
                    _colX[c] = barX;
                }
                else
                {
                    _colAnchorX[c] = 1f;
                    _colPivotX[c] = 1f;
                    _colX[c] = x;
                }
                // A pinned column is out of the packing flow entirely: it has an
                // address of its own, so it must not consume the space the packed
                // ones are measuring against.
                if (pinOther && c != OwnColumn)
                {
                    continue;
                }
                // an empty column advances nothing, so the next one takes its
                // place rather than its offset
                if (_colWidth[c] > 0f)
                {
                    x -= _colWidth[c] + columnGap;
                }
            }

            for (int c = 0; c < ColumnCount; c++)
            {
                _colGroup[c] = GroupNone;
                _colStarted[c] = false;
            }

            for (int i = 0; i < _platesUsed; i++)
            {
                Plate plate = Plates[i];
                if (plate.IsGroupFrame)
                {
                    continue;
                }
                int c = plate.Column;
                int g = plate.Group;
                // The spacing BEFORE this plate, decided by what it follows: a
                // sibling inside the same frame gets the member gap, anything else
                // gets the plate gap plus whatever frame padding is being crossed.
                if (_colStarted[c])
                {
                    if (g != GroupNone && g == _colGroup[c])
                    {
                        // the loadout boundary breathes a little wider
                        _colY[c] += memberGap + (plate.LoadoutEdge ? LoadoutAir : 0f);
                    }
                    else
                    {
                        if (_colGroup[c] != GroupNone)
                        {
                            _colY[c] += padY;   // the frame we are leaving, on top
                        }
                        _colY[c] += gap;
                        if (g != GroupNone)
                        {
                            _colY[c] += padY;   // the frame we are entering, below
                        }
                    }
                }
                else if (g != GroupNone)
                {
                    _colY[c] += padY;
                }
                _colStarted[c] = true;
                _colGroup[c] = g;

                // Members sit inside their frame, LEFT-aligned to its inner edge.
                // Aligning them to the column's own edge instead right-aligned
                // them, so a 9-wide loadout and a 10-wide bag in one frame had
                // staggered left edges — the ragged look, not a spacing problem.
                // A NAME PLATE hangs half in over the frame's top rail, TitlePlateX
                // in from the frame's left like the settings window's; the frame's
                // top is the plate's middle, so the members below keep their padY.
                bool tabTitle = plate.IsTitle && plate.TabTitle && g != GroupNone;
                if (tabTitle)
                {
                    _colY[c] += Mathf.Max(0f, padY - memberGap - PlateSink);
                }
                float px = _colX[c];
                if (tabTitle)
                {
                    bool rightPivot = _colPivotX[c] > 0.5f;
                    float frameLeft = rightPivot ? _colX[c] - 2f * padX - _groupW[g] : _colX[c];
                    px = frameLeft + TitlePlateX + (rightPivot ? plate.Window.Rect.sizeDelta.x : 0f);
                }
                else if (g != GroupNone)
                {
                    px = _colPivotX[c] > 0.5f
                        ? _colX[c] - padX - _groupW[g] + plate.Window.Rect.sizeDelta.x
                        : _colX[c] + padX;
                }
                plate.Window.Rect.anchorMin = new Vector2(_colAnchorX[c], 0f);
                plate.Window.Rect.anchorMax = new Vector2(_colAnchorX[c], 0f);
                plate.Window.Rect.pivot = new Vector2(_colPivotX[c], 0f);
                plate.Window.Rect.anchoredPosition = new Vector2(px, _colY[c]);
                if (g != GroupNone)
                {
                    if (_groupMembers[g]++ == 0)
                    {
                        _groupLo[g] = _colY[c] - padY;
                    }
                    if (tabTitle)
                    {
                        _groupHi[g] = _colY[c] + PlateSink;   // the frame's top is the plate's middle
                    }
                    else
                    {
                        _groupHi[g] = _colY[c] + plate.Height + padY;
                    }
                }
                // The separator spans the GROUP, not the band that draws it. Bind
                // sizes it to contentW because that is all Bind knows - and a
                // four-slot chest then ruled a line a quarter of the panel wide,
                // which reads as a broken line rather than a short one. _groupW is
                // settled in the pass above, and members share a left edge inside
                // the frame, so widening from x=0 to the group width lines every
                // stroke up with every other.
                if (g != GroupNone && plate.Separator != null
                    && plate.Separator.gameObject.activeSelf)
                {
                    var sep = (RectTransform)plate.Separator.transform;
                    if (plate.LoadoutEdge)
                    {
                        // the dotted seam runs closer to the frame's edge than the
                        // others: ScoreInset from the rail on each side, where an
                        // ordinary seam stops padX short at the member's edge
                        sep.anchoredPosition = new Vector2(-padX + ScoreInset, sep.anchoredPosition.y);
                        sep.sizeDelta = new Vector2(_groupW[g] + 2f * (padX - ScoreInset), sep.sizeDelta.y);
                    }
                    else
                    {
                        sep.sizeDelta = new Vector2(_groupW[g], sep.sizeDelta.y);
                    }
                }
                _colY[c] += plate.Height;
            }
            // a group that ran to the top of its column still owes its frame's lid
            for (int c = 0; c < ColumnCount; c++)
            {
                if (_colGroup[c] != GroupNone)
                {
                    _colY[c] += padY;
                }
            }

            // THE SHEET HANGS FROM THE UNIT: the sheets appear under the
            // indicator. The column was stacked bottom-up from
            // the bar like every other; while the unit is the head, the whole stack
            // is moved so its TOP meets the unit's bottom edge, the chevron on that
            // seam, and the foot tabs hang under it wherever the page ends. One
            // shift of every plate in the column and of the group's span, before
            // the frames and tabs are cut from that span.
            LayoutSheetGroups();

            // now the frames, once what they span has its geometry
            // DRAW ORDER, stated once over the finished roster. A frame is the
            // surface its members sit on, so every frame must precede every member -
            // and that cannot be decided while binding, because bind order is not
            // sibling order (the pool grows on demand, so a frame can be handed a
            // plate created after its own members).
            //
            // Descending, so after the loop the frames sit at 0..n-1 in ASCENDING
            // group order. Walking down and pushing to the front is what makes this
            // deterministic rather than three frames fighting over one slot.
            for (int g = GroupCount - 1; g >= 1; g--)
            {
                if (_groupFrame[g] >= 0)
                {
                    Plates[_groupFrame[g]].Window.Rect.SetAsFirstSibling();
                }
            }

            // and the readout stays on top of all of it - it used to hold index 0
            // by accident of being built first, which the reservation then took
            if (_hover != null)
            {
                _hover.transform.SetAsLastSibling();
            }

            LayoutReachContent();

            _reachPanel = null;
            for (int g = 1; g < GroupCount; g++)
            {
                int index = _groupFrame[g];
                if (index < 0)
                {
                    PlaceFootTabs(g, null, false);
                    continue;
                }
                Plate frame = Plates[index];
                if (_groupMembers[g] == 0)
                {
                    // nothing was bound into it — an empty frame is a box saying
                    // nothing, which is worse than no box
                    if (frame.Window.Go.activeSelf)
                    {
                        frame.Window.Go.SetActive(false);
                    }
                    PlaceFootTabs(g, null, false);
                    continue;
                }
                int c = frame.Column;
                frame.Window.Rect.anchorMin = new Vector2(_colAnchorX[c], 0f);
                frame.Window.Rect.anchorMax = new Vector2(_colAnchorX[c], 0f);
                frame.Window.Rect.pivot = new Vector2(_colPivotX[c], 0f);
                if (g == GroupReach && slot)
                {
                    // THE SLOT'S RECT EXACTLY, not the bands' extent: the vicinity
                    // window is the crafting window's frame with a different field,
                    // integrated into the left window rather than a separate one.
                    // The bands sit inside it
                    // from the bottom; whatever the pool holds, the frame is the slot.
                    frame.Window.Rect.sizeDelta = new Vector2(_slotW, _slotH - reachShrink);
                    frame.Window.Rect.anchoredPosition = new Vector2(_slotLeft, _slotBottom);
                }
                else
                {
                    frame.Window.Rect.sizeDelta =
                        new Vector2(_groupW[g] + 2f * padX, _groupHi[g] - _groupLo[g]);
                    frame.Window.Rect.anchoredPosition = new Vector2(_colX[c], _groupLo[g]);
                }
                // THE BAND ALONE (a flower totem, say): a thing with no
                // bag, no reading and no act has nothing for a body, and a frame
                // around nothing said so badly - a squashed strip, then a floating
                // plate. So the window collapses to one oak band, like the unit's
                // head: the name plate on it, Move · Remove · Repair and the close
                // chip on it, and no field under it. Everything the thing offers,
                // on one strip.
                bool band = g == GroupCharacter && _target is ItemInfo && GroupIsTitleOnly(g);
                if (band)
                {
                    if (frame.Band == null)
                    {
                        frame.Band = Theme.KitImage(frame.Window.Go.transform, "Band", "kit_plank_oak", 18, Color.white);
                        frame.Band.raycastTarget = true;
                    }
                    frame.Band.transform.SetAsFirstSibling();
                    if (!frame.Band.gameObject.activeSelf) { frame.Band.gameObject.SetActive(true); }
                    // the unit's width, not the name plate's: the band is the same
                    // strip the unit is, pinned to the same left edge, and its chips
                    // anchor to its right end - which is where they get their room
                    float bandW = Mathf.Max(frame.Window.Rect.sizeDelta.x, UnitWidth());
                    frame.Window.Rect.sizeDelta = new Vector2(bandW, frame.Window.Rect.sizeDelta.y);
                    // the name plate and the chips are centred on the frame's top edge;
                    // the band is centred on that same line, so the plate sits on the
                    // band and the glyphs have a spot at its right end instead of
                    // hanging off an edge with no spot of their own
                    float rise = Mathf.Max(PlateSink, CloseChipSize * 0.5f) + 5f;
                    Theme.Place(frame.Band, 0f, -rise, frame.Window.Rect.sizeDelta.x, 2f * rise);
                    if (frame.Rail != null) { frame.Rail.enabled = false; }
                    if (frame.Field != null && frame.Field.gameObject.activeSelf)
                    {
                        frame.Field.gameObject.SetActive(false);
                    }
                    frame.Back.enabled = false;
                    frame.Edge.enabled = false;
                }
                else if (frame.Band != null && frame.Band.gameObject.activeSelf)
                {
                    frame.Band.gameObject.SetActive(false);
                }
                if (IsDossier(g))
                {
                    // the sheet inside its folder, and yours stamped or theirs clipped
                    PlaceDossierFrame(frame, g);
                }
                else if (frame.Field != null && frame.Field.gameObject.activeSelf)
                {
                    Vector2 size = frame.Window.Rect.sizeDelta;
                    var fr = (RectTransform)frame.Field.transform;
                    fr.anchorMin = Vector2.zero;
                    fr.anchorMax = Vector2.zero;
                    fr.pivot = Vector2.zero;
                    fr.anchoredPosition = new Vector2(FieldInset, FieldInset);
                    fr.sizeDelta = new Vector2(
                        Mathf.Max(0f, size.x - 2f * FieldInset),
                        Mathf.Max(0f, size.y - 2f * FieldInset));
                }
                if (!frame.Window.Go.activeSelf)
                {
                    frame.Window.Go.SetActive(true);
                }
                PlaceFootTabs(g, frame, _footShown[g]);
                if (g == GroupReach)
                {
                    _reachPanel = frame.Window.Rect;
                }
            }
            PlaceSlotTabs(false);
            // The header follows the final frame, including filtering and shrink.
            PlaceFilter();

            float top = _colY[0];
            for (int c = 1; c < ColumnCount; c++)
            {
                top = Mathf.Max(top, _colY[c]);
            }
            var hoverRect = (RectTransform)_hover.transform;
            hoverRect.anchorMin = new Vector2(1f, 0f);
            hoverRect.anchorMax = new Vector2(1f, 0f);
            hoverRect.pivot = new Vector2(1f, 0f);
            hoverRect.sizeDelta = new Vector2(300f, 12f);
            hoverRect.anchoredPosition = new Vector2(-margin, top + 2f);
        }

        // Distance from the canvas's left edge to the Shared Bag bar's left edge,
        // in our root's local units. NaN when the bar cannot be resolved, which
        // makes the caller fall back to hanging left of your own column.
        private static float SharedBagLeftEdge()
        {
            return SharedBagCorner().x;
        }

        // The vanilla bar's bottom-left corner, measured from our root's own
        // bottom-left. The bar is deactivated once we draw the Shared Bag
        // ourselves, but an inactive RectTransform still reports its geometry —
        // so it keeps serving as the layout reference it always was.
        // The action bar's left edge, in this window's own space. Same conversion
        // SharedBagCorner does, against a different widget: the vanilla
        // strip holds the action bar rather than the shared bag, so "the bar" is
        // now ours to ask directly instead of inferring from vanilla's leftovers.
        private static float ActionBarLeftEdge()
        {
            RectTransform bar = ActionBar.Rect;
            var rootRect = _root != null ? _root.transform as RectTransform : null;
            if (bar == null || rootRect == null || !bar.gameObject.activeInHierarchy)
            {
                return float.NaN;
            }
            var corners = new Vector3[4];
            bar.GetWorldCorners(corners);   // 0 = bottom-left
            Vector3 local = rootRect.InverseTransformPoint(corners[0]);
            return local.x - rootRect.rect.xMin;
        }

        private static Vector2 SharedBagCorner()
        {
            var nowhere = new Vector2(float.NaN, float.NaN);
            GameObject bar = FindSharedBag(_mn);
            if (bar == null)
            {
                return nowhere;
            }
            var barRect = bar.transform as RectTransform;
            var rootRect = _root.transform as RectTransform;
            if (barRect == null || rootRect == null)
            {
                return nowhere;
            }
            var corners = new Vector3[4];
            barRect.GetWorldCorners(corners); // 0 = bottom-left
            Vector3 local = rootRect.InverseTransformPoint(corners[0]);
            return new Vector2(local.x - rootRect.rect.xMin, local.y - rootRect.rect.yMin);
        }
    }
}
