using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Titles, the name plate and its icon run, type icons.
    internal static partial class InventoryWindow
    {
        // the last tab plate's width, published for whatever floats beside it
        private static float _titlePlateW;

        private static void BindTitle(string text, int column,
            Sprite badge = null, int group = GroupNone, Color? splash = null,
            bool starred = false, CommonStates who = null)
        {
            // A name floating in a dark box beside a parchment panel is a label
            // ABOUT the panel. Inside it, in the panel's own ink, it is the
            // panel's heading — which is what it was always meant to be. The
            // group frame then wraps it, so the panel starts with what it is.
            if (group != GroupNone)
            {
                BindStyledTitle(text, column, PanelTitleStyle, badge, group, splash, starred, who);
                return;
            }
            BindStyledTitle(text, column, LooseTitleStyle, badge, GroupNone, splash, starred);
        }

        // Style 5, icon and text on a dark strip, is a title beside a panel; style
        // 6 is what a title becomes when it lives ON a panel rather than beside one.
        // Styles 1 to 4 are the other treatments the design was chosen from.
        private const int LooseTitleStyle = 5;
        private const int PanelTitleStyle = 6;

        private static void BindStyledTitle(string text, int column, int style,
            Sprite badge, int group, Color? splash = null, bool starred = false, CommonStates who = null)
        {
            Plate plate = TakePlate();
            plate.Column = column;
            plate.Group = group;
            _lastGroupBound = group;
            plate.Owner = null;
            plate.IsLoadout = false;
            plate.IsSharedBag = false;
            plate.IsTitle = true;
            plate.Shown = 0;
            plate.LaidOutCols = -1;

            float h = style == 2 ? TitleH + 6f : TitleH;
            float columnWidth = Mathf.Max(70f, _lastColumnWidth[column]);

            // A boxed title should be as wide as what it says, not as wide as the
            // column: a full-width bar over a short name reads as an empty plate
            // with a word in the corner. Frameless styles keep the column width,
            // because their rules are meant to span it.
            EnsureTitleParts(plate);
            plate.Header.fontSize = Theme.FontBody;
            plate.Header.text = text;
            // A panel heading spans its panel — it is the top band of a surface,
            // not a tag pinned near one.
            bool boxed = (style == 1 || style == 2 || style == 5) && group == GroupNone;
            // columnWidth, not _groupW: the group's measured width is computed in
            // Layout, which has not run yet this pass, and grouped members set
            // _lastColumnWidth to exactly that same number on the way in.
            float width = columnWidth;
            // the star sits after the name, so a plate cut to its name grows by it
            float starW = starred ? TitleStarSize + TitleStarGap : 0f;
            if (boxed)
            {
                float badgeW = style == 5 && badge != null ? Theme.SectionIconSize + 3f : 0f;
                float textOnly = plate.Header.GetPreferredValues(text).x;
                width = Mathf.Clamp(badgeW + textOnly + starW + (badgeW > 0f ? 8f : 10f),
                    40f, columnWidth);
            }
            bool tab = style == PanelTitleStyle && TabTitle(group);
            plate.TabTitle = tab;
            // THE PLATE, EXTENDED BY THE ICONS (the old plate anchored on the rail,
            // only extended with the five icons): as wide as its name, then the
            // food · water | moral · libido | baby run, for a person in focus. The
            // bars are PARKED - where the health bar goes is not decided yet.
            // D66: with the card under it the plate keeps D53's one row and its NAME,
            // and nothing else - the icon run moved into the card. Off, the target
            // column's header stands.
            //
            // AND THE RUN IS OFF THE PLATE ENTIRELY (D88, target-matrix.md G7):
            // Overview is the tab a target opens on and it now draws the same run
            // at page size, so the plate would be saying it twice. What that buys
            // is ~90 units of top rail, which is the room the placement acts need
            // when props arrive (G6). The plate keeps its name, which is what D53
            // asked of it in the first place.
            if (tab)
            {
                // as wide as its name: badge, text, and a shoulder past each nail
                float badgeW = badge != null ? Theme.SectionIconSize + 4f : 0f;
                float textOnly = plate.Header.GetPreferredValues(text).x;
                float tail = PlateShoulder + 10f;   // past the nail, and no more
                width = Mathf.Clamp(badgeW + textOnly + starW + PlateShoulder + tail, 48f,
                    Mathf.Max(columnWidth, PlateMaxW()));
                // where the plate ENDS, so anything that wants to sit beside it can
                // (the placement glyphs float left, against the plate)
                _titlePlateW = width;
            }
            plate.Height = tab ? PlateH : h;
            plate.Window.Rect.sizeDelta = new Vector2(width, plate.Height);
            Theme.Place(plate.Window.Content, 0f, 0f, width, plate.Height);

            for (int i = 0; i < plate.Cells.Count; i++)
            {
                if (plate.Cells[i].Go.activeSelf) plate.Cells[i].Go.SetActive(false);
            }
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                if (plate.TypeIcons[i].gameObject.activeSelf) plate.TypeIcons[i].gameObject.SetActive(false);
            }
            // on a panel the heading reads left to right like everything under it
            bool leftAligned = group != GroupNone || column != OwnColumn;
            plate.Back.enabled = false;
            plate.Edge.enabled = false;
            plate.Rule.gameObject.SetActive(false);
            plate.Badge.gameObject.SetActive(false);
            plate.Header.gameObject.SetActive(true);
            plate.Header.text = text;
            float textX = 0f;
            float textW = width;

            switch (style)
            {
                case 1:
                    plate.Back.enabled = true;
                    plate.Back.sprite = null;
                    plate.Back.color = new Color(0f, 0f, 0f, 0.85f);
                    plate.Header.color = Theme.TextCream;
                    textX = 4f; textW = width - 8f;
                    break;
                case 2:
                    plate.Back.enabled = true;
                    if (Theme.BagSkin.Valid)
                    {
                        plate.Back.sprite = Theme.BagSkin.Sprite;
                        plate.Back.type = Theme.BagSkin.Type;
                        plate.Back.pixelsPerUnitMultiplier = Theme.BagSkin.PixelsPerUnit;
                        plate.Back.color = Color.white;
                    }
                    else
                    {
                        plate.Back.sprite = null;
                        plate.Back.color = Theme.PlateDark;
                    }
                    plate.Header.color = Theme.Ink;
                    textX = 8f; textW = width - 16f;
                    break;
                case 3:
                    plate.Header.color = Theme.AccentGold;
                    plate.Rule.gameObject.SetActive(true);
                    plate.Rule.color = new Color(0.62f, 0.50f, 0.30f, 0.9f);
                    Theme.Place(plate.Rule, 0f, h - 1.5f, width, 1.5f);
                    break;
                case 4:
                    plate.Header.color = Theme.AccentGold;
                    plate.Rule.gameObject.SetActive(true);
                    plate.Rule.color = new Color(0.62f, 0.50f, 0.30f, 0.55f);
                    Theme.Place(plate.Rule, 0f, h * 0.5f, width, 1f);
                    plate.Header.transform.SetAsLastSibling();
                    break;
                case 5:
                    plate.Back.enabled = true;
                    plate.Back.sprite = null;
                    plate.Back.color = new Color(0.16f, 0.12f, 0.08f, 0.92f);
                    plate.Edge.enabled = true;
                    plate.Edge.effectColor = Theme.EdgeDefault;
                    plate.Header.color = Theme.AccentGold;
                    if (badge != null)
                    {
                        plate.Badge.gameObject.SetActive(true);
                        plate.Badge.sprite = badge;
                        float bs = Theme.SectionIconSize;
                        float by = (h - bs) * 0.5f;
                        Theme.Place(plate.Badge, leftAligned ? 3f : width - bs - 3f, by, bs, bs);
                        if (leftAligned) { textX = bs + 6f; textW = width - bs - 9f; }
                        else { textW = width - bs - 9f; }
                    }
                    else { textX = 4f; textW = width - 8f; }
                    break;
                case PanelTitleStyle:
                    // No plate of its own: it is already on one. It can still carry
                    // a MARK though - the heading is the one thing in the panel with
                    // nothing behind it, which is why it reads as loose text.
                    plate.Header.color = Theme.TextCream;
                    if (tab)
                    {
                        // THE TARGET'S PLATE IS LIT: the same hand-cut plate in
                        // PINE - the kit's own word for "lit", the hover and active
                        // wood - where every other plate is oak, and its name in
                        // gold. A pine ring round a whole frame's edge was too thin
                        // a thing to notice; one plate in another wood, with the
                        // name on it, is where the eye lands.
                        // The lit plate is OFF: how to indicate the current focus
                        // target is still being considered. The pine and the gold name stay a
                        // flip away for when that is decided.
                        bool lit = false && group == TargetGroup()
                            && Theme.Kit("kit_plate_handcut_pine", 40) != null;
                        // the hand-cut plate (486x102, nails in its 40px border),
                        // its full height the plate's 16 units
                        plate.Back.enabled = true;
                        Theme.KitSkin(plate.Back, lit ? "kit_plate_handcut_pine" : "kit_plate_handcut", 40);
                        plate.Back.pixelsPerUnitMultiplier = 102f / PlateH;
                        plate.Back.color = Color.white;
                        if (lit)
                        {
                            plate.Header.color = Theme.AccentGold;
                        }
                        // the tool shed: a crate's name is on a shipping label
                        if (IsCrate(group))
                        {
                            ShedNamePlate(plate);
                        }
                        else
                        {
                            Unstencil(plate);
                        }
                        // THE PLATE CROPS ITS PAINT (the brush behind the name is
                        // larger, and must not overflow the title plate): a stencil from the wood's own
                        // alpha, so the splash can fill the whole rect and the
                        // hand-cut edge is where it stops
                        if (plate.PlateMask == null)
                        {
                            plate.PlateMask = plate.Window.Go.AddComponent<Mask>();
                            plate.PlateMask.showMaskGraphic = true;
                        }
                        plate.PlateMask.enabled = true;
                    }
                    float shoulder = tab ? PlateShoulder : 1f;
                    if (badge != null)
                    {
                        plate.Badge.gameObject.SetActive(true);
                        plate.Badge.sprite = badge;
                        float bs = Theme.SectionIconSize;
                        float rowH = tab ? PlateH : plate.Height;   // the head's name row, not its whole height
                        Theme.Place(plate.Badge, shoulder, (rowH - bs) * 0.5f, bs, bs);
                        textX = shoulder + bs + 3f;
                        textW = width - textX - (tab ? 6f : 10f);
                    }
                    else
                    {
                        textX = shoulder;
                        textW = width - textX - (tab ? 6f : 11f);
                    }
                    break;
                default:
                    plate.Header.color = Theme.TextDim;
                    break;
            }
            if (style == 4)
            {
                plate.Header.alignment = TMPro.TextAlignmentOptions.Midline;
            }
            else
            {
                plate.Header.alignment = leftAligned
                    ? TMPro.TextAlignmentOptions.MidlineLeft
                    : TMPro.TextAlignmentOptions.MidlineRight;
            }
            // State Content's geometry here rather than inheriting whatever the
            // plate's previous life left - the same lesson the tint learned: a
            // pooled plate changes JOB, and everything measured against a rect it
            // did not set is measured against the last job's answer.
            Theme.Place(plate.Window.Content, 0f, 0f, width, plate.Height);
            // the text gives up the star's room whichever way it is aligned; on a
            // plate cut to its name that room was added to the width above
            textW -= starW;
            Theme.Place(plate.Header, textX, tab ? (PlateH - h) * 0.5f : 0f, textW, h);
            if (starred)
            {
                // right after the ink (or before it, on a right-aligned heading):
                // it marks the NAME, not the plate
                float ink = Mathf.Min(plate.Header.GetPreferredValues(text).x, textW);
                float sx = leftAligned
                    ? textX + ink + TitleStarGap
                    : textX + textW - ink - TitleStarGap - TitleStarSize;
                float plateH = tab ? PlateH : h;
                Theme.Place(plate.Star, sx, (plateH - TitleStarSize) * 0.5f,
                    TitleStarSize, TitleStarSize);
                plate.Star.gameObject.SetActive(true);
            }

            // The heading's brush mark (D36). Sized to the INK, not to the rect: a
            // panel title spans its whole column, so a mark cut to the rect would be
            // a ten-slot-wide slab behind two words. It hugs the badge and the text
            // and overshoots a little, because a mark that stops exactly where the
            // text does reads as a highlighter rather than as paint.
            // NOT FOR AN EMPTY HEADING. The mark is sized to the INK, so with no
            // text it collapses to leftPad + TitleMarkPadX of brush hanging off the
            // left edge -- a dark smudge beside the filter well, which is what the
            // vicinity heading looked like the moment the filter took its row.
            // A mark with nothing to mark is not a smaller mark, it is a mistake.
            // ON A PLATE the splash is the character's colour or nothing (only NPCs
            // and the main characters wear the paintbrush in the title): a chest gets the plain wood, not the fallback wash.
            if (style == PanelTitleStyle && !string.IsNullOrEmpty(text)
                && (!tab || splash.HasValue))
            {
                Sprite mark = Theme.FindMask(TitleSprite);
                if (mark != null)
                {
                    float ink = plate.Header.GetPreferredValues(text).x;
                    float right = textX + Mathf.Min(ink, textW);
                    EnsureField(plate);
                    plate.Field.sprite = mark;
                    plate.Field.type = Image.Type.Simple;
                    // The character's own colour when there is one, so a heading
                    // says WHICH KIND of person this is without spending a word on
                    // it. The configured mark keeps its ALPHA either way - the
                    // friends-list palette is opaque, and a heading laid on at full
                    // strength stops looking like paint.
                    Color mood = splash ?? TitleSplashColor;
                    mood.a = TitleSplashColor.a;
                    plate.Field.color = mood;
                    // Asymmetric, because the two sides have different room. To
                    // the RIGHT the mark has the rest of the column, so it takes
                    // the full overshoot. To the LEFT it has only the distance to
                    // the parchment's edge - a grouped member sits GroupPadX inside
                    // the frame RECT, but the drawn surface starts FieldInset in,
                    // so anything past the difference leaves the panel. Same limit
                    // the container tint's bleed answers to, and the same mistake
                    // if it is ignored: paint outside the window.
                    float leftPad = group != GroupNone
                        ? Mathf.Min(TitleMarkPadX, Mathf.Max(0f, GroupPadX() - FieldInset))
                        : TitleMarkPadX;
                    if (tab)
                    {
                        // the paint hugs the name and overshoots a little; the mask
                        // crops it to the wood. On the head the plate is the frame's
                        // width, so a wash over the whole row would drown the chips
                        // lifted a little: the brush's weight sits low in its mask, so
                        // centred on the rect it read as sagging under the name
                        Theme.Place(plate.Field, 0f, -1.2f, Mathf.Min(width, right + TitleMarkPadX + 10f), PlateH);
                    }
                    else
                    {
                        Theme.Place(plate.Field, -leftPad, -TitleMarkPadY,
                            right + leftPad + TitleMarkPadX, h + TitleMarkPadY * 2f);
                    }
                    if (!plate.Field.gameObject.activeSelf)
                    {
                        plate.Field.gameObject.SetActive(true);
                    }
                }
            }

            if (plate.Rows != null && plate.Rows.Count > 0 && plate.Rows[0].Go.activeSelf)
            {
                plate.Rows[0].Go.SetActive(false);
            }

            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
        }

        private const float PlateIconAir = 2.4f;   // between drawn icons on the plate
        // how much of each icon's box its ink actually fills, side to side: the
        // drop is a narrow sprite in a square box, the face fills its disc
        private static readonly float[] StripIconInk = { 0.94f, 0.55f, 1.0f, 1.0f, 0.80f, 0.95f };   // pear · drop · moral · heart · flame · baby
        private const float StripSepW = 1.5f;
        private const float PlateSepExtra = 1.3f;   // the extra air a hairline takes beside the icons' own

        private const float PlateIconPitch = 12f;

        // (the head's two-row strip, kept for the day the bars find their place)
        private static void BindHeadStrip(Plate plate, CommonStates who, float width)
        {
            if (plate.Rows == null)
            {
                plate.Rows = new List<SheetRow>();
            }
            while (plate.Rows.Count < 1)
            {
                plate.Rows.Add(CreateSheetRow(plate));
            }
            SheetRow row = plate.Rows[0];
            SheetLines.Clear();
            WriteHeader(SheetLines, who);
            SheetLine line = SheetLines[0];
            EnsureRowExtras(row, line);
            row.Bare = true;
            float inset = 4f;
            float rowW = width - 2f * inset;
            Theme.Place(row.Go.transform, inset, PlateH - 3f, rowW, HeadGaugeH);
            LayoutStrip(row, line, rowW, HeadGaugeH);
            PaintSheetRow(row, line);
            if (!row.Go.activeSelf)
            {
                row.Go.SetActive(true);
            }
            plate.RowsShown = 1;
            plate.IsSheet = true;
            plate.IsHeader = true;
            plate.Owner = who;
            SheetPlates.Add(plate);
        }

        private static void EnsureTitleParts(Plate plate)
        {
            if (plate.Rule != null)
            {
                return;
            }
            plate.Rule = NewChildImage(plate, "Rule");
            plate.Badge = NewChildImage(plate, "Badge");
            // the same star a favourite ITEM wears in its cell: one glyph
            // for "favourite", whichever kind of thing is marked
            plate.Star = NewChildImage(plate, "Star");
            plate.Star.sprite = Theme.Star;
            plate.Star.color = Theme.AccentGold;
        }

        private static Image NewChildImage(Plate plate, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(plate.Window.Content, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            return image;
        }

        // Vanilla draws a type icon over every loadout slot — nine identical
        // frames cannot tell you which one is the hat. These are the game's own
        // icons, harvested from the equip strip (Theme.SlotTypeIcons).
        private static void BindTypeIcons(Plate plate, bool show, int[] map, int cols, int count,
            bool leather = false)
        {
            if (!show)
            {
                for (int i = 0; i < plate.TypeIcons.Count; i++)
                {
                    if (plate.TypeIcons[i].gameObject.activeSelf)
                    {
                        plate.TypeIcons[i].gameObject.SetActive(false);
                    }
                }
                return;
            }
            while (plate.TypeIcons.Count < count)
            {
                var go = Owned.New("TypeIcon");
                go.transform.SetParent(plate.Window.Content, false);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                plate.TypeIcons.Add(image);
            }
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                Image icon = plate.TypeIcons[i];
                int equipIndex = map != null && i < map.Length ? map[i] : i;
                // one icon strip, over the first row only — as vanilla draws it.
                // The loadout is always 9 slots in a single row, but don't let a
                // hypothetical wrap silently misplace the strip.
                Sprite sprite = i < count && i < cols && equipIndex < Theme.SlotTypeIcons.Length
                    ? Theme.SlotTypeIcons[equipIndex] : null;
                if (sprite == null)
                {
                    if (icon.gameObject.activeSelf)
                    {
                        icon.gameObject.SetActive(false);
                    }
                    continue;
                }
                icon.sprite = sprite;
                // measured from the same origin the CELLS are, so the strip cannot
                // drift from the row it names; on the leather the proof's 22 px
                // glyph, 5 px over its cell, in cream (Leather.cs)
                float size = leather ? LeatherGlyphH : TypeIconH;
                float rise = leather ? LeatherGlyphRow : TypeIconH;
                Theme.Place(icon,
                    plate.CellLeft + i * CellPitch + (CellBox - size) * 0.5f,
                    plate.CellTop - rise, size, size);
                LeatherGlyph(icon, leather);
                if (!icon.gameObject.activeSelf)
                {
                    icon.gameObject.SetActive(true);
                }
            }
        }

        private static float PlateWidth(int cols)
        {
            return cols * CellPitch - (CellPitch - CellBox) + 2f * GroupPadX();
        }

        // How many columns the target's containers may use before they would run
        // into your own column. Bags should be as LONG as they can be, since every
        // extra row costs height the stack does not have.
        private static int TargetMaxCols(float ownWidth)
        {
            var rootRect = _root.transform as RectTransform;
            if (rootRect == null)
            {
                return OwnMaxCols;
            }
            float left = SharedBagLeftEdge();
            if (float.IsNaN(left))
            {
                left = MarginX;
            }
            float available = rootRect.rect.width - MarginX - ownWidth - ColumnGap - left;
            int cols = Mathf.FloorToInt(
                (available - 2f * GroupPadX() + (CellPitch - CellBox))
                / CellPitch);
            return Mathf.Clamp(cols, 4, AbsoluteMaxCols);
        }
    }
}
