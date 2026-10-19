using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Content height, member binding, cells.
    internal static partial class InventoryWindow
    {
        // How tall a band is, given its shape. Extracted because BindReach has to
        // know a container's height BEFORE binding it, to decide whether it fits -
        // and a fit test computing its own answer is exactly the drift that has
        // already cost this file two invisible-layout bugs. One formula, two
        // callers, no way for them to disagree.
        private static float ContentHeight(int rows, float headerH, float iconH,
            float gutterW, bool wantsText)
        {
            // a folded section is its header alone
            float h = headerH + iconH + (rows > 0 ? rows * CellPitch - (CellPitch - CellBox) : 0f);
            if (gutterW > 0f)
            {
                // a one-row container is shorter than the icon, and shorter again
                // than the icon plus a caption under it
                h = Mathf.Max(h, GutterGlyph + (wantsText ? Theme.CaptionH : 0f));
            }
            return h;
        }

        // Each container renders at its OWN natural width (capped), so it reads
        // as a whole shape: 18 -> 9x2, 10 -> 10x1, 9 -> 9x1, 30 -> 10x3. One
        // fixed column count cannot satisfy all three intrinsic sizes.
        private static void Bind(string label, ItemSlot[] array, int count, int[] map,
            InventorySlot mirrorOf, bool loadout, int column = 0, int maxCols = 0,
            CommonStates owner = null, int vanillaSlotBase = -1, bool sharedBag = false,
            bool forceLabel = false, bool marked = false, int group = GroupNone,
            Sprite icon = null, bool lean = false, bool section = false, bool collapsed = false,
            bool captionRow = false)
        {
            if (array == null || count <= 0)
            {
                return;
            }
            Plate plate = TakePlate();
            plate.Column = column;
            plate.Owner = owner;
            plate.Container = mirrorOf;
            plate.IsLoadout = loadout;
            plate.IsSharedBag = sharedBag;
            bool grouped = group != GroupNone;
            plate.Group = grouped ? group : GroupNone;
            plate.IsGroupFrame = false;
            // members of a group are bound back to back, so the first one is
            // simply the one whose group differs from the last bound
            bool firstOfGroup = grouped && _lastGroupBound != group;
            _lastGroupBound = plate.Group;

            if (maxCols <= 0)
            {
                maxCols = OwnMaxCols;
            }
            // FILL THE ROW, then let the last one be ragged.
            // This used to look for an even division first, so 18 slots became 9x2
            // rather than 10 + 8. An even split does make one container read as a
            // clean shape - but a container is never seen alone here. It is seen in
            // a column with two or three others, and the moment its width is chosen
            // by its own slot count, neighbours of different sizes get different
            // widths and the column's left edge goes ragged. Consistency between
            // bands beats symmetry within one, because the raggedness symmetry
            // removes is invisible and the raggedness it creates is not.
            // ...and the SAME argument settles the width of a small container. A
            // six-slot bag used to be six columns wide, so the whole column - and
            // the window with it - narrowed for one NPC and widened for the next.
            // The grid is
            // maxCols wide whatever it holds; a short container simply leaves the
            // rest of its one row empty. Nothing else moves: for count < maxCols
            // both rules already put every cell in a single row, so this changes
            // the plate's WIDTH and not one cell's place.
            int cols = maxCols;
            int rows = collapsed ? 0 : Mathf.CeilToInt(count / (float)cols);
            plate.Cols = cols;

            while (plate.Cells.Count < count)
            {
                plate.Cells.Add(CreateCell(plate));
            }

            // vanilla's own two container materials: cream parchment for bags,
            // translucent green + brass for the loadout. The material IS the
            // container-type signal, so we inherit the distinction rather than
            // invent one.
            Theme.Skin skin = loadout ? Theme.EquipSkin : Theme.BagSkin;
            // a grouped member carries no border of its own — the group's frame
            // does — so it spends no padding on one
            float insetX = grouped ? 0f : FrameInsetX;
            float insetY = grouped ? 0f : FrameInsetY;
            float iconH = loadout ? TypeIconH : 0f;
            // the label band cost a whole row of whitespace per plate, and the
            // harvested skins already say what a container is: cream = bag,
            // green + type icons = loadout
            // ...except in the reach column, where the skin says "a container" and
            // the question being asked is WHICH one. A column of five identical
            // cream plates cannot answer "where is the iron".
            // A container's identity used to cost a full-width BAND above it —
            // 13 units of chrome per container, and in a column of five chests
            // that is more height than half the slots. In a gutter it costs width
            // instead, in a strip narrow enough for the icon that says it best,
            // and the band becomes one line: what it is, then what is in it.
            // A gutter is earned by having something to PUT in it, and an icon
            // is enough. Your bag and the shared bag were two anonymous bands
            // before this — only the loadout said what it was, and only because
            // vanilla's type icons happen to sit above it.
            bool showLabel = forceLabel;
            bool wantsText = showLabel && !string.IsNullOrEmpty(label);
            // a section takes the header ROW, never the gutter
            bool gutter = !section && (icon != null || wantsText);
            float gutterW = gutter ? GutterWidth : 0f;
            // A CAPTION ROW keeps the gutter's icon and puts the words on a line of
            // their own over the cells: a person's bag in the vicinity, where the
            // backpack says what and only the name can say whose, and a name does
            // not fit under a gutter glyph
            bool rowCaption = captionRow && gutter && wantsText;
            float headerH = section || (!gutter && wantsText) || rowCaption ? PlateHeaderH : 0f;

            // Where the ROW starts inside the plate, settled here because every
            // placement below is measured against it: the gutter badge, its
            // caption, the type strip and the cells themselves. It is NOT the
            // plate's top-left corner - those two differ whenever something sits
            // above the cells, which on a loadout is vanilla's type strip - and
            // each thing that quietly assumed they were the same has come back as
            // a separate alignment bug.
            plate.CellLeft = gutterW;
            plate.CellTop = headerH + iconH;
            float contentW = gutterW + cols * CellPitch - (CellPitch - CellBox);
            float contentH = ContentHeight(rows, headerH, iconH, gutterW, wantsText && !rowCaption);
            // The SIZE is compared directly, not inferred from the inputs that
            // usually move it. Height is written every bind and the rect only on a
            // reshape, so anything that changes contentH without tripping one of
            // the checks below leaves Layout stacking by one number while Unity
            // draws another - and neither is visible on screen. That happened:
            // wantsText enters contentH through the gutter clause and appears in
            // none of these, so a pooled plate that had drawn a reach chest (label
            // "3m", caption, contentH 27) and came back as your bag (no label,
            // contentH 17) kept a 27-tall rect at cols, count, pitch, gutter,
            // iconH and headerH all identical. It overlapped the band beneath it
            // and put the separator inside the slots.
            //
            // Comparing the size itself means the next input added to contentH is
            // covered before anyone notices it was added.
            bool reshaped = plate.LaidOutCols != cols || plate.LaidOutCount != count
                || plate.LaidOutIconH != iconH || plate.LaidOutHeaderH != headerH
                || plate.LaidOutPitch != CellPitch || plate.LaidOutGutter != gutterW
                || plate.LaidOutW != contentW || plate.LaidOutH != contentH;
            // The plate's OWN box is stated every bind; only the cells inside it
            // are conditional. Two assignments cost nothing, and they are exactly
            // the pair a role change can leave stale: this plate may have spent the
            // last frame as a group FRAME, whose rect Layout sizes to everything it
            // spans, or as a TITLE, which sets a rect and never touches Content at
            // all. reshaped was written to skip the expensive per-cell placement
            // loop below - it was never meant to gate the plate's own geometry, and
            // gating it there means the box is only correct if the SHAPE changed,
            // when what actually changed was the JOB.
            plate.Window.Rect.sizeDelta =
                new Vector2(contentW + 2f * insetX, contentH + 2f * insetY);
            Theme.Place(plate.Window.Content, insetX, insetY, contentW, contentH);
            if (reshaped)
            {
                _reshapes++;
                plate.LaidOutCols = cols;
                plate.LaidOutCount = count;
                plate.LaidOutIconH = iconH;
                plate.LaidOutHeaderH = headerH;
                plate.LaidOutPitch = CellPitch;
                plate.LaidOutGutter = gutterW;
                plate.LaidOutW = contentW;
                plate.LaidOutH = contentH;
            }
            plate.Height = contentH + 2f * insetY;

            // LEAN: a band the viewport does not reach gets its GEOMETRY and
            // nothing else. Everything below this line is what the band looks
            // like - the tint, the type icons, the separator, and a cell loop
            // that touches an Image, a TMP and four colours per slot - and none
            // of it is visible through a mask that is clipping the whole band
            // away. The height IS visible, because the stack above it rests on
            // it, so that is computed for every band exactly as before.
            //
            // This is the crafting menu's answer, which measured
            // the same thing from the other side: "the only winning move is to
            // have less of it". There it was Unity's geometry rebuild and the
            // cure was disabling Graphics; here it is our own bind loop and the
            // cure is not running it. Same shape, different half of the frame.
            //
            // It does NOT reopen D28a. That decision rejected row-CROPPING,
            // where what you see is a function of what fits - the content
            // changing shape as you move through it. Nothing changes shape here:
            // every band is measured, positioned and stacked exactly as before,
            // and the scroll extent is identical. Only the paint is deferred to
            // the moment the band is in view.
            if (lean)
            {
                // A LEAN BIND LAYS OUT NOTHING, SO IT MUST NOT CLAIM TO HAVE. The
                // reshaped block above had already stamped this geometry as laid
                // out, so the first FULL bind after a band scrolled into view
                // found nothing to reshape and switched on cells that had never
                // been placed - Unity's default 100x100 rect, wearing the slot
                // sprite: a black square at the top of the screen.
                plate.LaidOutCols = -1;
                plate.Back.enabled = false;
                plate.Edge.enabled = false;
                plate.Shown = 0;
                plate.IsTitle = false;
                if (plate.Field != null && plate.Field.gameObject.activeSelf)
                {
                    plate.Field.gameObject.SetActive(false);
                }
                HideCells(plate);
                if (!plate.Window.Go.activeSelf)
                {
                    plate.Window.Go.SetActive(true);
                }
                _lastColumnWidth[column] = Mathf.Max(_lastColumnWidth[column],
                    plate.Window.Rect.sizeDelta.x);
                return;
            }

            if (grouped)
            {
                // The group's frame is the surface; a member drawing its own BORDER
                // would be a box inside a box.
                plate.Back.enabled = false;
                plate.Edge.enabled = false;
            }
            else if (skin.Valid)
            {
                plate.Back.sprite = skin.Sprite;
                plate.Back.type = skin.Type;
                plate.Back.pixelsPerUnitMultiplier = skin.PixelsPerUnit;
                // the vanilla equip strip ships translucent (alpha 0.68), which
                // reads fine as a thin bar over the world but muddies a stacked
                // panel you are meant to read items out of
                Color plateColor = skin.Color;
                plateColor.a = 1f;
                plate.Back.color = plateColor;
                plate.Back.enabled = true;
                plate.Edge.enabled = false;
            }
            else
            {
                plate.Back.enabled = true;
                plate.Back.sprite = null;
                plate.Back.color = loadout ? Theme.PlateEquip : Theme.PlateDark;
                plate.Edge.enabled = true;
                plate.Edge.effectColor = loadout ? Theme.EdgeEquip : Theme.EdgeDefault;
            }
            // The icon is the label, when a label would only repeat it: a chest
            // reads as a chest from its own sprite faster than from its name, and
            // the name is what the header row is spending height on.
            // the loadout is told by its hanger glyph and the dotted seam alone;
            // the green corner ribbon (M7) is gone
            bool wantIcon = icon != null && (gutter || section);
            if (wantIcon)
            {
                EnsureTitleParts(plate);
                plate.Badge.sprite = icon;
                if (gutter)
                {
                    // icon at the top of the gutter, its caption directly under -
                    // one narrow strip that says what this band is, level with the
                    // first row of what it names
                    // CENTRED ON THE FIRST ROW, not hung from its top (hung, every
                    // gutter glyph sat slightly too high): a 14-unit
                    // badge on a 22-unit cell drops four
                    float size = GutterGlyph;
                    Theme.Place(plate.Badge, GutterGlyphX(gutterW),
                        plate.CellTop + Mathf.Max(0f, (CellBox - size) * 0.5f), size, size);
                }
                else
                {
                    Theme.Place(plate.Badge, 0f, 1f, headerH - 2f, headerH - 2f);
                }
                if (!plate.Badge.gameObject.activeSelf)
                {
                    plate.Badge.gameObject.SetActive(true);
                }
            }
            else if (plate.Badge != null && plate.Badge.gameObject.activeSelf)
            {
                plate.Badge.gameObject.SetActive(false);
            }
            if (wantsText)
            {
                if (rowCaption)
                {
                    plate.Header.fontSize = Theme.FontCaption;
                    plate.Header.alignment = TextAlignmentOptions.MidlineLeft;
                    Theme.Place(plate.Header, gutterW, 0f, Mathf.Max(8f, contentW - gutterW), headerH);
                }
                else if (gutter)
                {
                    plate.Header.fontSize = Theme.FontCaption;
                    plate.Header.alignment = TextAlignmentOptions.Top;
                    // under the badge, which now sits centred on the first row
                    float badgeDrop = wantIcon ? Mathf.Max(0f, (CellBox - GutterGlyph) * 0.5f) : 0f;
                    Theme.Place(plate.Header, wantIcon ? GutterGlyphX(gutterW) : 0f,
                        plate.CellTop + badgeDrop + (wantIcon ? GutterGlyph + 1f : 0f),
                        wantIcon ? GutterGlyph : gutterW, Theme.CaptionH);
                }
                else
                {
                    // the crafting header's type: body size, ink, FLAT (the harvested
                    // font's black outline is what made "Spider Storage" read as a
                    // different face from "Basic"), room for the arrow
                    plate.Header.fontSize = section ? Theme.FontBody : Theme.FontCaption;
                    plate.Header.alignment = TextAlignmentOptions.MidlineLeft;
                    if (section)
                    {
                        Theme.FlattenType(plate.Header);
                    }
                    float labelX = wantIcon ? headerH + 2f : 0f;
                    float arrowRoom = section ? SectionArrowW + 4f : 0f;
                    Theme.Place(plate.Header, labelX, 0f,
                        Mathf.Max(8f, contentW - labelX - arrowRoom), headerH);
                }
            }
            plate.Header.text = label;
            // the section's disclosure triangle and the header's click target
            if (section)
            {
                EnsureSectionParts(plate);
                Theme.Place(plate.SectionArrow, contentW - SectionArrowW - 2f,
                    (headerH - SectionArrowW) * 0.5f, SectionArrowW, SectionArrowW);
                plate.SectionArrow.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, collapsed ? 90f : 0f);
                Theme.Place(plate.HeaderHit, 0f, 0f, contentW, headerH);
                if (!plate.SectionArrow.gameObject.activeSelf)
                {
                    plate.SectionArrow.gameObject.SetActive(true);
                    plate.HeaderHit.gameObject.SetActive(true);
                }
            }
            else if (plate.SectionArrow != null && plate.SectionArrow.gameObject.activeSelf)
            {
                plate.SectionArrow.gameObject.SetActive(false);
                plate.HeaderHit.gameObject.SetActive(false);
            }
            // the harvested plates are light parchment / mid green — ink reads on
            // them, cream does not
            plate.Header.color = grouped || skin.Valid
                ? Theme.Ink : (loadout ? Theme.EdgeEquip : Theme.TextDim);
            bool crate = IsCrate(group);
            ShedCaption(plate, crate && wantsText);
            if (plate.Header.gameObject.activeSelf != wantsText)
            {
                plate.Header.gameObject.SetActive(wantsText);
            }
            // Where the parts of one frame divide. A gap alone reads as a gap in a
            // grid; a hairline in the gap reads as a boundary.
            bool wantSeparator = grouped && !firstOfGroup;
            plate.LoadoutEdge = wantSeparator && loadout != _prevMemberLoadout;
            if (grouped)
            {
                _prevMemberLoadout = loadout;
            }
            if (wantSeparator && plate.Separator == null)
            {
                plate.Separator = NewChildImage(plate, "Separator");
            }
            if (plate.Separator != null)
            {
                if (wantSeparator)
                {
                    // A plain faint rule: it confirms the gap between two bands
                    // rather than announcing it.
                    plate.Separator.sprite = null;
                    plate.Separator.type = Image.Type.Simple;
                    // NewChildImage preserves aspect, which is right for a badge and
                    // wrong for this: a 4:1 mask letterboxed inside a 20:1 band drew
                    // the line across the middle fifth and left the rest empty. A
                    // separator is meant to be STRETCHED to what it separates.
                    plate.Separator.preserveAspect = false;
                    plate.Separator.color = SeparatorColor;
                    float th = SeparatorHeight;
                    float sepGap = GroupMemberGap;
                    if (plate.LoadoutEdge)
                    {
                        // A ROW OF DOTS (the tear-off idea retired, its round dots
                        // kept as a separator and made more solid): kit_score tiled
                        // across the seam, one hole per tile, near-solid ink, running
                        // closer to the frame's edge than the other seams do
                        sepGap += LoadoutAir;
                        Sprite score = Theme.Kit("kit_score", 0);
                        if (score != null)
                        {
                            plate.Separator.sprite = score;
                            plate.Separator.type = Image.Type.Tiled;
                            plate.Separator.pixelsPerUnitMultiplier = ScoreTilePx / ScorePeriod;
                            plate.Separator.color = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.9f);
                            th = ScoreH;
                        }
                        else
                        {
                            th += 0.5f;   // the heavier seam it replaced, as fallback
                            Color sc = plate.Separator.color;
                            plate.Separator.color = new Color(sc.r * 0.85f, sc.g * 0.85f, sc.b * 0.85f, 0.85f);
                        }
                    }
                    plate.SeamY = contentH + sepGap * 0.5f;
                    Theme.Place(plate.Separator, 0f, plate.SeamY - th * 0.5f, contentW, th);
                }
                if (plate.Separator.gameObject.activeSelf != wantSeparator)
                {
                    plate.Separator.gameObject.SetActive(wantSeparator);
                }
            }
            // The one you actually opened, sitting among the ones you merely could
            // — being focused is a STATE of a plate now, not a place on screen.
            if (marked)
            {
                plate.Edge.enabled = true;
                plate.Edge.effectColor = Theme.EdgeTarget;
            }

            // Where the grid starts, settled BEFORE anything is placed against it.
            // The type strip used to compute its own x from i * CellPitch alone,
            // which was right until the gutter arrived and moved the cells right by
            // GutterWidth without telling it - so vanilla's hat glyph sat over the
            // gutter badge and every icon after it named the slot to its left.
            BindTypeIcons(plate, loadout, map, cols, count);

            // Read ONCE per plate, not per cell: Active trims and measures a
            // string, and this loop runs for every cell of every band in reach.
            // ONLY THE VICINITY'S OWN BANDS are marked (the filter is specific to
            // the window and disconnected from your own and the target's bag). The
            // old cross-plate dim lit the match in your bag too; the window's query
            // now stays in the window.
            UI.FilterField reachFilter = UI.SlotFilter.Vicinity;
            bool searching = _bindingReach && reachFilter.Active;
            float cellBegan = Time.realtimeSinceStartup;
            for (int i = 0; i < plate.Cells.Count; i++)
            {
                CellView cell = plate.Cells[i];
                if (i >= count || collapsed)
                {
                    cell.Slot = null;
                    cell.DrewKey = null;
                    cell.DrewStack = -1;
                    if (cell.Go.activeSelf)
                    {
                        cell.Go.SetActive(false);
                    }
                    continue;
                }
                if (reshaped)
                {
                    Theme.Place(cell.Rect,
                        gutterW + (i % cols) * CellPitch,
                        headerH + iconH + (i / cols) * CellPitch,
                        CellBox, CellBox);
                    SizeCellParts(cell);
                }
                ItemSlot slot = Resolve(array, map, mirrorOf, i, out int vanillaSlotId);
                cell.Slot = slot;
                // The Shared Bag's slots ARE vanilla UI slots, with real indices -
                // so its cells can drive the full vanilla use path, consumables
                // included, rather than only the equip subset.
                cell.VanillaSlotId = vanillaSlotBase >= 0 ? vanillaSlotBase + i : vanillaSlotId;
                bool filled = slot != null && !string.IsNullOrEmpty(slot.itemKey);
                bool starred = filled && WearsFavourites(plate) && Favourites.IsFavourite(slot);
                bool assigned = filled && (plate.IsSharedBag || plate.Column == OwnColumn)
                    && ToolBelt.IsHung(slot);
                // The whole point: is this the same picture as last time. The key
                // by REFERENCE first - item keys come from a shared table, so a
                // reference match settles it without walking the string.
                string key = filled ? slot.itemKey : null;
                int life = filled ? (int)slot.itemLife : 0;
                int stack = filled ? slot.stack : 0;
                // ONE lookup per drawn cell, and it is a dictionary hit after the
                // first item of a kind: the matcher memoises per item key. That is
                // what makes marking affordable in a window that took four rounds
                // to make cheap.
                // An EMPTY slot is 0, not 2. It draws identically either way -- no
                // icon to dim, no count to fade -- so calling it a miss would flip
                // the memo on every empty cell in the base the moment you typed a
                // letter, and repaint several hundred of them to no visible effect.
                int mark = !searching || !filled ? 0
                    : reachFilter.Matches(_mn, key) ? 1 : 2;
                if (ReferenceEquals(cell.DrewKey, key) && cell.DrewStack == stack
                    && cell.DrewLife == life && cell.DrewStar == starred
                    && cell.DrewToolMark == assigned
                    && cell.DrewMark == mark
                    && cell.Go.activeSelf)
                {
                    continue;
                }
                cell.DrewKey = key;
                cell.DrewStack = stack;
                cell.DrewLife = life;
                cell.DrewStar = starred;
                cell.DrewToolMark = assigned;
                cell.DrewMark = mark;
                _cellsBound++;
                if (filled)
                {
                    Sprite sprite = slot.itemImage;
                    if (sprite == null)
                    {
                        _itemLookups++;
                        ItemData data = _mn.itemMN.FindItem(slot.itemKey);
                        sprite = data != null ? data.itemSprite : null;
                    }
                    float t0 = Time.realtimeSinceStartup;
                    cell.Icon.sprite = sprite;
                    cell.Icon.enabled = sprite != null;
                    float t1 = Time.realtimeSinceStartup;
                    SetCellReadout(cell, slot);
                    _iconMs += (t1 - t0) * 1000f;
                    _textMs += (Time.realtimeSinceStartup - t1) * 1000f;
                }
                else
                {
                    // vanilla's emptySprite was tried here and is a BLANK — it
                    // erases the previous icon rather than marking anything, so
                    // there is nothing to draw on the empty side
                    cell.Icon.enabled = false;
                    cell.Count.text = "";
                }
                cell.Icon.color = mark == 2 ? Theme.SearchMiss : Color.white;
                cell.Count.alpha = mark == 2 ? Theme.SearchMiss.a : 1f;
                if (cell.Star.gameObject.activeSelf != starred)
                {
                    cell.Star.gameObject.SetActive(starred);
                }
                cell.Box.color = BaseCellColor(filled);
                if (cell.ToolMark.gameObject.activeSelf != assigned)
                {
                    cell.ToolMark.gameObject.SetActive(assigned);
                }
                ShedCell(cell, filled, crate);
                // No wash: the slot frame's interior is black at alpha 216/255
                // already. Occupancy is carried by EmptySlotAlpha, which halves
                // the EMPTY ones; filled is twice as opaque as empty.
                if (cell.Fill != null)
                {
                    cell.Fill.color = TileShadeColor;
                    cell.Fill.enabled = filled && !crate;
                }
                if (!cell.Go.activeSelf)
                {
                    cell.Go.SetActive(true);
                }
            }
            _cellMs += (Time.realtimeSinceStartup - cellBegan) * 1000f;
            plate.Shown = count;
            plate.IsTitle = false;
            // A grouped member decides its own Back ABOVE - a type tint, or
            // nothing - so this must not speak for it. It said !grouped, which was
            // right while grouped meant "never draws a background" and silently
            // undid the tint the moment that stopped being true: the branch set it
            // on, and thirty lines later this set it off again. Now it is only what
            // it always was underneath - the fallback for an ungrouped plate whose
            // skin did not harvest.
            if (!grouped)
            {
                plate.Back.enabled = true;
            }
            plate.Header.gameObject.SetActive(wantsText);
            if (!sharedBag)
            {
                _lastColumnWidth[column] = Mathf.Max(_lastColumnWidth[column],
                    plate.Window.Rect.sizeDelta.x);
            }
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
        }

        // A FAVOURITE IS A MARK ON YOUR INVENTORY (the concept is specific to the
        // player's inventory): it says keep it in the bag and reach for it as the
        // tool. Your two bags and your own loadout wear the star and take the
        // Alt+click (it had been bags only, so an equipped item had to be taken off to be marked, and an
        // equipped favourite showed no star). The mark follows the item
        // (FavouriteMoves) between all three.
        private static bool WearsFavourites(Plate plate)
        {
            return plate != null && (plate.IsSharedBag || plate.Column == OwnColumn);
        }

        // A cell's parts are laid out against the box size in force when it was
        // built. The gap applies live, so a reshape re-states them rather than
        // leaving the icon and the count sized for the box the cell used to be.
        private static void SizeCellParts(CellView cell)
        {
            if (cell.ToolMark != null)
            {
                Theme.Place(cell.ToolMark, CellBox - StarSize - 0.5f, 0.5f, StarSize, StarSize);
            }
            if (cell.Icon != null)
            {
                Theme.PlaceSlotIcon(cell.Icon, CellBox);
            }
            if (cell.Count != null)
            {
                Theme.Place(cell.Count, 0f, CellBox - 10f, CellBox - 1f, 10f);
            }
            // Fill and Star need nothing: the shade is anchor-stretched to the box
            // and the mark is a fixed 7px in its corner.
        }

        // The corner readout is not always a stack count. Vanilla
        // (InventoryManager.StackTextChange) overloads it three ways, and our
        // cells only ever drew the first:
        //   stack count   plain, when more than one
        //   durability    green, for weapons and tools
        //   happiness     magenta, for a captured NPC or animal — "MAX" at 100,
        //                 which is the point it can join the tribe
        private static void SetCellReadout(CellView cell, ItemSlot slot)
        {
            if (_mn.inventory.IsWeaponOrTool(slot.toolType))
            {
                cell.Count.text = slot.itemLife.ToString("");
                cell.Count.color = Theme.Durability;
                return;
            }
            if (IsCreature(slot.toolType))
            {
                cell.Count.text = slot.itemLife >= 100f ? "MAX" : slot.itemLife.ToString("");
                cell.Count.color = Theme.Happiness;
                return;
            }
            cell.Count.text = slot.stack > 1 ? slot.stack.ToString() : "";
            cell.Count.color = CrateCount;
        }

        private static bool IsCreature(ItemData.ToolType toolType)
        {
            return toolType == ItemData.ToolType.NPC || toolType == ItemData.ToolType.Animal;
        }

        // One thin label row per GROUP — never per container. Containers say what
        // they are through their skin; a group needs to say WHOSE it is, and that
        // is a single line rather than a band on every plate.
        // TITLE LAB - six treatments, several lifted from the game's own UI, so a
        // choice could be made by looking rather than by describing. 5 is the one
        // chosen for a title beside a panel, 6 the one on a panel:
        //   1  black bar, cream text      the sub-inventory's own title bar
        //   2  parchment mini-plate       the bag frame, shrunk into a label
        //   3  gold text over a hairline  the craft panel's heading language
        //   4  centred between two rules  a section divider
        //   5  icon + text on a dark strip
        //   6  plain dim text, on the panel
    }
}
