using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE INVENTORY, LOOK 07 (Forgejo #150, art/ref/window-facelift-v1/inventory).
    //
    // Your inventory, a person's bag and a container's window - the grids you work
    // with your hands - are painted amber leather: the name on the window's head
    // ("Inventory | Man"), a section per container (WORN, BACKPACK, SHARED BACKPACK,
    // each with its rule and its capacity), dark hollow sockets for empty slots and
    // grey-brown beds for taken ones, worn positions told by their glyph above,
    // a faint one inside, a warmer mount and a baseline. Hover, a press and an equip
    // preview light a cell's rim and nothing else. The pieces are cut
    // at the proof's pixels, one sprite pixel
    // a screen pixel at 1440p; docs/ui-kit.md section 12.
    //
    // The proof is nine columns of 72 px cells; ours stay ten of 64 on the game's
    // pitch (a later change), so the panel is our grid with the proof's insets.
    // The vicinity is its own window and wears look 02's leather (Vicinity.cs).
    internal static partial class InventoryWindow
    {
        private const float LeatherPx = 1f / Theme.KitPpu;
        // the frame: the proof's padding, 29 px in from the painted edge (GroupPadX's
        // 10 units, less the shadow's 3 px the sprite carries past it), 65 above the
        // first section - the head's 17 + 36 and the 12 under its rule - and 21 under
        // the last, plus the shadow's 6
        private const float LeatherPadTop = 65f * LeatherPx;
        private const float LeatherPadFoot = 27f * LeatherPx;
        private const float LeatherShadowX = 3f * LeatherPx;
        private static readonly Vector4 LeatherBorder = new Vector4(28f, 31f, 28f, 25f);
        // a section: its head 24 px with 7 under it; a worn row's glyphs 22 px and 5
        // above the cells; 14 between sections, and the shared backpack's rule 14
        // under the backpack with 11 more under the rule
        private const float LeatherHeadH = 31f * LeatherPx;
        private const float LeatherHeadRow = 24f * LeatherPx;
        private const float LeatherGlyphH = 22f * LeatherPx;
        private const float LeatherGlyphRow = 27f * LeatherPx;
        private const float LeatherGap = 14f * LeatherPx;
        private const float LeatherSharedGap = 26f * LeatherPx;
        // the close key: 33 px, 16 in from the painted right edge and 13 down
        private const float LeatherCloseSize = 34f * LeatherPx;
        private const float LeatherCloseFromRight = (16f + 16.5f + 3f) * LeatherPx;
        private const float LeatherCloseDrop = (13f + 16.5f) * LeatherPx;
        internal const string LeatherCloseFace = "kit_inv_close";
        // the type: the head's 24 and 17 px, a section's 16 px capitals, the
        // capacity's 14, a count's 15 on its plate
        private const float LeatherTitleSize = 24f * LeatherPx;
        private const float LeatherOwnerSize = 17f * LeatherPx;
        private const float LeatherSectionSize = 16f * LeatherPx;
        private const float LeatherCapacitySize = 14f * LeatherPx;
        private static readonly Color LeatherCream = Theme.ParseColor("#F4E4C7", Color.white);
        private static readonly Color LeatherOwnerInk = Theme.ParseColor("#DFC7A8", Color.white);
        private static readonly Color LeatherCapacityInk = Theme.ParseColor("#E2C9A7", Color.white);
        private static readonly Color LeatherShade = Theme.ParseColor("#31180F", Color.black);
        private static readonly Color LeatherRuleInk = Theme.ParseColor("#D4A16477", Color.white);
        private static readonly Color LeatherBarInk = Theme.ParseColor("#C2946177", Color.white);
        private static readonly Color LeatherGlyphInk = Theme.ParseColor("#E6C89B", Color.white);
        private static readonly Color LeatherGlyphShade = Theme.ParseColor("#351B10", Color.black);
        private static readonly Color LeatherGhostInk = Theme.ParseColor("#B7A08347", Color.white);   // .28

        // A grid you work with your hands: yours, a person's bag, and a container's
        // window while the unit is up (its Inventory door's). The vicinity is not one
        // of them, and a thing's window without the unit carries readings and a name
        // plate the leather has no place for.
        private static bool IsLeather(int group)
        {
            return group == GroupOwn || group == GroupBag
                || (group == GroupCharacter && _target is InventorySlot && Settings.TheUnit.Value);
        }

        // ---------- the frame ----------

        private static void SkinLeatherFrame(Plate plate, int group)
        {
            Theme.KitSkin(plate.Back, "kit_inv_leather", LeatherBorder);
            plate.Back.color = Color.white;
            plate.Edge.enabled = false;
            if (plate.Field != null && plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(false);
            }
            if (plate.Mark != null && plate.Mark.gameObject.activeSelf)
            {
                plate.Mark.gameObject.SetActive(false);
            }
            if (plate.Rail != null)
            {
                plate.Rail.enabled = false;
            }
            EnsureClose(plate, -LeatherCloseDrop, LeatherCloseSize, LeatherCloseFromRight);
            if (!plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(true);
            }
            plate.Close.transform.SetAsLastSibling();
            EnsureLeatherHead(plate);
            plate.LeatherOwner = null;
        }

        // the head's parts, on the frame: "Inventory", the bar, the owner, the rule
        private static void EnsureLeatherHead(Plate plate)
        {
            if (plate.LeatherTitle != null)
            {
                return;
            }
            Transform at = plate.Window.Go.transform;
            plate.LeatherTitle = LeatherText(at, "LeatherTitle", true, LeatherTitleSize, LeatherCream,
                TextAlignmentOptions.MidlineLeft);
            plate.LeatherName = LeatherText(at, "LeatherOwner", false, LeatherOwnerSize, LeatherOwnerInk,
                TextAlignmentOptions.MidlineLeft);
            plate.LeatherBar = LeatherLine(at, "LeatherBar", LeatherBarInk);
            plate.LeatherRule = LeatherLine(at, "LeatherRule", LeatherRuleInk);
        }

        private static Image LeatherLine(Transform parent, string name, Color ink)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<Image>();
            line.raycastTarget = false;
            line.color = ink;
            go.SetActive(false);
            return line;
        }

        // The proof's type: the HUD's Noto Serif, cream, with its soft shade under it
        // (text-shadow 0 1px 2px #31180f) as the face's underlay. Flattened first: the
        // face's shared material wears the outline other HUD words put on it.
        private static TextMeshProUGUI LeatherText(Transform parent, string name, bool strong, float size,
            Color ink, TextAlignmentOptions align)
        {
            TextMeshProUGUI text = Theme.MakeText(parent, name, size, ink, align);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            LeatherFace(text, strong, true);
            text.gameObject.SetActive(false);
            return text;
        }

        private static void LeatherFace(TMP_Text text, bool strong, bool shade)
        {
            TMP_FontAsset face = strong ? HudStrongFont : HudBodyFont;
            if (face == null || text.font == face)
            {
                return;
            }
            text.font = face;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            Theme.FlattenType(text);
            if (shade && text.fontSharedMaterial != null)
            {
                Material m = text.fontMaterial;
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(LeatherShade.r, LeatherShade.g, LeatherShade.b, 0.85f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            }
        }

        // a pooled plate that wore the leather, back to what every other skin expects
        private static void ShedLeather(Plate plate)
        {
            SetShown(plate.LeatherTitle, false);
            SetShown(plate.LeatherName, false);
            SetShown(plate.LeatherBar, false);
            SetShown(plate.LeatherRule, false);
            SetShown(plate.SectionCount, false);
            SetShown(plate.SectionFade, false);
            plate.LeatherGap = 0f;
        }

        // whose window it is, said on its head
        private static void NameLeather(int group, string owner)
        {
            for (int i = _platesUsed - 1; i >= 0; i--)
            {
                Plate p = Plates[i];
                if (p.IsGroupFrame && p.Group == group)
                {
                    p.LeatherOwner = owner;
                    return;
                }
            }
        }

        // Once the frame has its size: the head across its top - the title 29 px in
        // from the painted edge on the 26 px line under the frame's 17, the owner
        // after its bar, the rule under the whole of it, as wide as the grid.
        private static void PlaceLeatherFrame(Plate frame, int group)
        {
            if (frame.LeatherTitle == null)
            {
                return;
            }
            float left = GroupPadX();
            float mid = 30f * LeatherPx;
            TextMeshProUGUI title = frame.LeatherTitle;
            string word = Shared.Loc.Get("inv.title", "Inventory");
            if (title.text != word)
            {
                title.text = word;
            }
            float tw = title.GetPreferredValues(word).x;
            Theme.Place(title, left, mid - 15f * LeatherPx, tw + 2f, 30f * LeatherPx);
            SetShown(title, true);
            string owner = frame.LeatherOwner;
            bool named = !string.IsNullOrEmpty(owner);
            if (named)
            {
                float barX = left + tw + 17f * LeatherPx;
                Theme.Place(frame.LeatherBar, barX, mid - 11.5f * LeatherPx, LeatherPx, 23f * LeatherPx);
                TextMeshProUGUI name = frame.LeatherName;
                if (name.text != owner)
                {
                    name.text = owner;
                }
                float nx = barX + 18f * LeatherPx;
                Theme.Place(name, nx, mid - 15f * LeatherPx,
                    Mathf.Max(8f, frame.Window.Rect.sizeDelta.x - nx - LeatherCloseFromRight - 20f * LeatherPx),
                    30f * LeatherPx);
            }
            SetShown(frame.LeatherBar, named);
            SetShown(frame.LeatherName, named);
            Theme.Place(frame.LeatherRule, left, 52f * LeatherPx, _groupW[group], LeatherPx);
            SetShown(frame.LeatherRule, true);
        }

        // ---------- the sections ----------

        // A section's head: its name in capitals, the rule fading to its right, and
        // what it holds at the right end. The worn row says no number: its positions
        // are the loadout, not a capacity.
        private static void LeatherSectionHead(Plate plate, string label, int capacity, float contentW,
            bool upper)
        {
            TextMeshProUGUI head = plate.Header;
            LeatherFace(head, true, true);
            head.fontSize = LeatherSectionSize;
            head.characterSpacing = 1.4f / LeatherSectionSize * LeatherPx * 100f;
            head.color = LeatherCream;
            head.alignment = TextAlignmentOptions.MidlineLeft;
            string text = upper ? label.ToUpperInvariant() : label;
            head.text = text;
            float lw = head.GetPreferredValues(text).x;
            Theme.Place(head, 0f, 0f, lw + 2f, LeatherHeadRow);
            SetShown(head, true);
            float cw = 0f;
            bool counted = capacity > 0;
            if (counted)
            {
                if (plate.SectionCount == null)
                {
                    plate.SectionCount = LeatherText(plate.Window.Content, "Capacity", false, LeatherCapacitySize,
                        LeatherCapacityInk, TextAlignmentOptions.MidlineRight);
                }
                string slots = Shared.Loc.Get("inv.slots", "{0} slots", capacity);
                if (plate.SectionCount.text != slots)
                {
                    plate.SectionCount.text = slots;
                }
                cw = plate.SectionCount.GetPreferredValues(slots).x;
                Theme.Place(plate.SectionCount, contentW - cw - 2f, 0f, cw + 2f, LeatherHeadRow);
            }
            SetShown(plate.SectionCount, counted);
            if (plate.SectionFade == null)
            {
                plate.SectionFade = Theme.KitImage(plate.Window.Content, "Rule", "kit_inv_fade", 0, Color.white, stretch: true);
            }
            float rx = lw + 9f * LeatherPx;
            float rw = contentW - rx - (counted ? cw + 9f * LeatherPx : 0f);
            Theme.Place(plate.SectionFade, rx, LeatherHeadRow * 0.5f, Mathf.Max(0f, rw), LeatherPx);
            SetShown(plate.SectionFade, rw > 0f);
        }

        // what a container is called on its leather head
        private static string LeatherSection(bool loadout, bool sharedBag, CommonStates owner)
        {
            if (loadout)
            {
                return Shared.Loc.Get("inv.section.worn", "Worn");
            }
            if (sharedBag)
            {
                return Shared.Loc.Get("inv.section.shared", "Shared backpack");
            }
            return owner != null ? Shared.Loc.Get("inv.section.backpack", "Backpack")
                : Shared.Loc.Get("inv.section.storage", "Storage");
        }

        // ---------- the cells ----------

        // A cell on the leather (ItemCellFace). Off it - a pooled cell going to a
        // crate - the leather's rim, glyph and count edge are taken off again.
        private static void LeatherCell(CellView cell, bool on, bool filled, bool worn, int equipIndex)
        {
            cell.Leather = on;
            if (!on)
            {
                if (cell.Rim != null && cell.Rim.gameObject.activeSelf)
                {
                    cell.Rim.gameObject.SetActive(false);
                }
                SetShown(cell.Ghost, false);
                if (cell.Highlight != null)
                {
                    cell.Highlight.effectDistance = new Vector2(2f, -2f);
                }
                BarCount(cell, false);
                return;
            }
            ItemCellFace(cell, filled, worn, equipIndex);
        }

        // ONE FACE FOR EVERY ITEM CELL we draw - your inventory, a person's bag, a
        // container's window, the vicinity, and the item on the cursor: the leather's
        // socket or bed (look 07), a worn mount's glyph faint in its empty socket, the
        // rim's layer (lit by PaintCellRim) and the action bar's count (BarCount).
        private static void ItemCellFace(CellView cell, bool filled, bool worn, int equipIndex)
        {
            Theme.KitSkin(cell.Box, worn ? (filled ? "kit_inv_worn_full" : "kit_inv_worn")
                : filled ? "kit_inv_cell_full" : "kit_inv_cell", 10);
            cell.Box.color = Color.white;
            // the equip preview's outline is the rim's colour now (PaintLeatherRims);
            // drawn under the cell at no distance it shows nothing of its own
            cell.Highlight.effectDistance = Vector2.zero;
            if (cell.Rim == null)
            {
                cell.Rim = Theme.KitImage(cell.Go.transform, "Rim", "kit_inv_rim_hot", 8, Color.white, stretch: true);
                cell.Rim.transform.SetAsFirstSibling();
                cell.Rim.gameObject.SetActive(false);
            }
            Theme.Place(cell.Rim, 0f, 0f, CellBox, CellBox);
            // the faint glyph in an empty worn socket: the type it takes
            Sprite ghost = worn && !filled && equipIndex >= 0 && equipIndex < Theme.SlotTypeIcons.Length
                ? Theme.SlotTypeIcons[equipIndex] : null;
            if (ghost != null)
            {
                if (cell.Ghost == null)
                {
                    var go = Owned.New("Ghost");
                    go.transform.SetParent(cell.Go.transform, false);
                    cell.Ghost = go.AddComponent<Image>();
                    cell.Ghost.raycastTarget = false;
                    cell.Ghost.preserveAspect = true;
                    go.transform.SetSiblingIndex(1);
                }
                cell.Ghost.sprite = ghost;
                cell.Ghost.color = LeatherGhostInk;
                float g = 30f * LeatherPx;
                Theme.Place(cell.Ghost, (CellBox - g) * 0.5f, (CellBox - g) * 0.5f, g, g);
            }
            SetShown(cell.Ghost, ghost != null);
            BarCount(cell, true);
        }

        // THE COUNT IS THE ACTION BAR'S (one count style across
        // the bar and the inventory; the black plate lived nowhere else): cream
        // figures in our face in the cell's corner, each with its own dark edge
        // (Theme.OutlineCount), which is near the leather's own shade - no plate.
        // Off the leather, a pooled cell's plain count, its edge taken off again.
        private static void BarCount(CellView cell, bool outlined)
        {
            TextMeshProUGUI count = cell.Count;
            if (count == null)
            {
                return;
            }
            if (Theme.Font != null && count.font != Theme.Font)
            {
                count.font = Theme.Font;
            }
            count.fontSize = Theme.FontCaption;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.enableWordWrapping = false;
            count.overflowMode = outlined ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            Theme.Place(count, 0f, CellBox - 10f, CellBox - 1f, 10f);
            if (outlined && !cell.CountOutlined)
            {
                cell.CountOutlined = Theme.OutlineCount(count);
            }
            else if (!outlined && cell.CountOutlined && count.font != null)
            {
                count.fontSharedMaterial = count.font.material;
                cell.CountOutlined = false;
            }
        }

        // ---------- the rims ----------

        // The cell under the pointer, as the last input tick found it.
        private static CellView _leatherHover;

        // HOVER, A PRESS AND A PREVIEW LIGHT THE RIM. Painted once a frame from what
        // the input tick decided: the equip and stack previews' colour (they set the
        // cell's Highlight, whose own outline the leather does not draw), the press
        // as the proof's selection, the pointer as its hover.
        private static void PaintLeatherRims()
        {
            PaintVicinityCells();   // ...and the vicinity's state faces (Vicinity.cs)
            for (int p = 0; p < _platesUsed && p < Plates.Count; p++)
            {
                Plate plate = Plates[p];
                if (plate.IsGroupFrame || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < plate.Shown && i < plate.Cells.Count; i++)
                {
                    CellView cell = plate.Cells[i];
                    if (cell.Leather)
                    {
                        PaintCellRim(cell);
                    }
                }
            }
        }

        // A cell's rim: an equip or stack preview in its colour (the cell's
        // Highlight, whose own outline the leather does not draw), a press as the
        // proof's selection, the pointer as its hover - on an empty cell as on a
        // taken one, in every window.
        private static void PaintCellRim(CellView cell)
        {
            if (cell.Rim == null)
            {
                return;
            }
            string rim = null;
            Color ink = Color.white;
            if (cell.Highlight.enabled)
            {
                rim = "kit_inv_rim_sel";
                ink = cell.Highlight.effectColor;
                ink.a = 1f;
            }
            else if (ReferenceEquals(cell, _pressedCell) && !_dragging && Input.GetMouseButton(0))
            {
                rim = "kit_inv_rim_sel";
            }
            else if (ReferenceEquals(cell, _leatherHover))
            {
                rim = "kit_inv_rim_hot";
            }
            bool on = rim != null;
            if (on)
            {
                Sprite face = Theme.Kit(rim, 8);
                if (cell.Rim.sprite != face)
                {
                    cell.Rim.sprite = face;
                }
                if (cell.Rim.color != ink)
                {
                    cell.Rim.color = ink;
                }
            }
            if (cell.Rim.gameObject.activeSelf != on)
            {
                cell.Rim.gameObject.SetActive(on);
            }
        }

        // ---------- the worn glyphs ----------

        // The worn row's glyphs above its cells: the game's own type icons, in the
        // proof's cream with a dark pixel under them.
        private static void LeatherGlyph(Image icon, bool on)
        {
            Shadow shade = icon.GetComponent<Shadow>();
            if (on)
            {
                icon.color = LeatherGlyphInk;
                if (shade == null)
                {
                    shade = icon.gameObject.AddComponent<Shadow>();
                }
                shade.effectColor = LeatherGlyphShade;
                shade.effectDistance = new Vector2(0f, -LeatherPx);
                shade.enabled = true;
            }
            else
            {
                icon.color = Color.white;
                if (shade != null)
                {
                    shade.enabled = false;
                }
            }
        }
    }
}
