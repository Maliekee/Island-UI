using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE CRAFTING WINDOW AS A BLUEPRINT (the catalogue sheet, of three mockups).
    // The recipe card's own material (`UI/Blueprint.cs`): Prussian blue
    // under a double rule, the name and the filter as a title block, a section as
    // a ruled band, and every recipe on one square of the sheet's grid - the cell
    // pitch IS the grid's major square.
    //
    // WHAT YOU CAN MAKE IS THERE IN COLOUR; WHAT YOU CANNOT IS ONLY A DRAWING.
    //
    // It paints over whatever ApplyCraftSurface decided.
    internal static partial class BrowserShell
    {
        private const float SheetHeaderSize = 8.5f;

        private static void ApplySheet()
        {
            if (_panelFill == null)
            {
                return;
            }
            _panelFill.sprite = null;
            _panelFill.type = Image.Type.Simple;
            _panelFill.color = UI.Blueprint.Ground;
            UI.Theme.KitSkin(_panelRail, "kit_bp_frame", 16);
            _panelRail.fillCenter = false;
            _panelRail.color = UI.Blueprint.Ink;
            if (_cover != null)
            {
                _cover.color = UI.Blueprint.Ground;
            }
            if (_scrollbar != null)
            {
                // square, as everything on the sheet: a ruled track, a solid thumb
                Image track = _scrollbar.GetComponent<Image>();
                Image thumb = _scrollbar.targetGraphic as Image;
                track.sprite = null;
                track.color = UI.Blueprint.WithAlpha(UI.Blueprint.Ink, 0.10f);
                if (thumb != null)
                {
                    thumb.sprite = null;
                    thumb.color = UI.Blueprint.Ink;
                }
            }
            if (_collapseAllPlate != null)
            {
                UI.Theme.KitSkin(_collapseAllPlate, "kit_bp_box", 6);
                _collapseAllPlate.color = UI.Blueprint.Ink;
                _collapseAllArrow.color = UI.Blueprint.Ink;
            }
            Letter(_noMatches, UI.Blueprint.Dim);
            if (_headers != null)
            {
                for (int i = 0; i < _headers.Length; i++)
                {
                    HeaderView h = _headers[i];
                    if (h == null)
                    {
                        continue;
                    }
                    Letter(h.Label, UI.Blueprint.Ink);
                    h.Label.fontSize = SheetHeaderSize;
                    h.Label.fontStyle = FontStyles.Normal;
                    if (h.Arrow != null)
                    {
                        h.Arrow.color = UI.Blueprint.Ink;
                    }
                }
            }
            if (Active && _mn != null && _cells != null)
            {
                BindVisible();
            }
        }

        private static void Letter(TMP_Text text, Color color)
        {
            if (text == null)
            {
                return;
            }
            TMP_FontAsset face = UI.Blueprint.Face;
            if (face != null && text.font != face)
            {
                text.font = face;
            }
            text.color = color;
            UI.Theme.FlattenType(text);
        }

        // A section is a ruled band: its name lettered in capitals, a rule running
        // from the name to the fold arrow, and the band closed by a rule beneath.
        private static void SheetHeader(HeaderView h, string text, bool hasIcon)
        {
            if (h.Rule == null)
            {
                h.Rule = UI.Blueprint.Rule(h.Go.transform, "Rule", 0.42f);
                h.Under = UI.Blueprint.Rule(h.Go.transform, "Under", 0.6f);
                h.Rule.transform.SetSiblingIndex(0);
                h.Under.transform.SetSiblingIndex(0);
            }
            if (h.Label.font != UI.Blueprint.Face || h.Label.fontSize != SheetHeaderSize)
            {
                Letter(h.Label, UI.Blueprint.Ink);
                h.Label.fontSize = SheetHeaderSize;
                h.Label.fontStyle = FontStyles.Normal;
            }
            h.Label.text = text.ToUpperInvariant();
            if (hasIcon && h.Icon.sprite != null)
            {
                Sprite drawn = UI.Blueprint.Drawing(h.Icon.sprite, 44);
                if (drawn != null)
                {
                    h.Icon.sprite = drawn;
                    h.Icon.color = UI.Blueprint.Ink;
                }
            }
            float width = h.Rect.sizeDelta.x;
            float textEnd = RowTextInset + (hasIcon ? 15f : 0f) + h.Label.GetPreferredValues(h.Label.text).x + 5f;
            float ruleEnd = width - CollapseAllW;
            float mid = UI.Theme.SectionRowHeight * 0.5f;
            UI.Theme.Place(h.Rule, textEnd, mid - 0.2f, Mathf.Max(0f, ruleEnd - textEnd), 0.4f);
            UI.Theme.Place(h.Under, -2.5f, UI.Theme.SectionRowHeight - 0.5f, width + 5f, 0.4f);
        }

        // A recipe on its square of the grid: in colour when you can make it, a
        // drawing when you cannot.
        private static void SheetCell(CellView c, bool craftable)
        {
            if (c.Square == null)
            {
                // the square is the cell's pitch, not its box: neighbours share edges
                c.Square = UI.Theme.KitImage(c.Go.transform, "Square", "kit_bp_grid", 0, UI.Blueprint.WithAlpha(UI.Blueprint.Ink, 0.6f));
                c.Square.preserveAspect = false;
                RectTransform sq = c.Square.rectTransform;
                sq.anchorMin = sq.anchorMax = sq.pivot = new Vector2(0.5f, 0.5f);
                sq.sizeDelta = new Vector2(CellPitch, CellPitch);
                sq.anchoredPosition = Vector2.zero;
                sq.SetSiblingIndex(0);

                GameObject drawnGo = Owned.New("Drawing");
                drawnGo.transform.SetParent(c.Go.transform, false);
                c.Drawn = drawnGo.AddComponent<Image>();
                c.Drawn.raycastTarget = false;
                c.Drawn.preserveAspect = true;
                if (c.Icon != null)
                {
                    RectTransform from = c.Icon.rectTransform, to = c.Drawn.rectTransform;
                    to.anchorMin = from.anchorMin;
                    to.anchorMax = from.anchorMax;
                    to.pivot = from.pivot;
                    to.anchoredPosition = from.anchoredPosition;
                    to.sizeDelta = from.sizeDelta;
                    // dark things would sink into the blue: a thin drawn edge under the colour
                    c.Halo = c.Icon.gameObject.AddComponent<Outline>();
                    c.Halo.effectColor = UI.Blueprint.WithAlpha(UI.Blueprint.Ink, 0.55f);
                    c.Halo.effectDistance = new Vector2(0.45f, -0.45f);
                }
                drawnGo.transform.SetSiblingIndex(1);

                c.Pin = UI.Theme.KitImage(c.Go.transform, "Pin", "kit_bp_box", 6, UI.Blueprint.Lit);
                RectTransform pin = c.Pin.rectTransform;
                pin.anchorMin = Vector2.zero;
                pin.anchorMax = Vector2.one;
                pin.offsetMin = new Vector2(-1.5f, -1.5f);
                pin.offsetMax = new Vector2(1.5f, 1.5f);
                pin.SetAsLastSibling();
                if (c.Count != null)
                {
                    c.VanillaCountSize = c.Count.fontSize;
                }
            }
            c.Square.enabled = true;
            c.Background.color = Color.clear;
            Sprite icon = c.Icon != null ? c.Icon.overrideSprite : null;
            if (c.Icon != null)
            {
                c.Icon.enabled = craftable;
                c.Icon.color = Color.white;
            }
            if (c.Halo != null)
            {
                c.Halo.enabled = craftable;
            }
            c.Drawn.sprite = craftable ? null : UI.Blueprint.Drawing(icon, 60);
            c.Drawn.enabled = c.Drawn.sprite != null;
            c.OwedDrawing = !craftable && c.Drawn.sprite == null && icon != null;
            c.Drawn.color = UI.Blueprint.WithAlpha(UI.Blueprint.Ink, 0.5f);
            c.Pin.enabled = c.Recipe >= 0 && c.Recipe == SelectedRecipe;
            if (c.Count != null)
            {
                c.Count.font = UI.Blueprint.LegacyFace;
                c.Count.color = UI.Blueprint.Ink;
                // a dimension on a drawing, not a label on a shelf
                c.Count.fontSize = Mathf.Max(1, Mathf.RoundToInt(c.VanillaCountSize * 0.72f));
            }
        }

        // Drawings are made a few per frame (Blueprint.Drawing): the cells still
        // owed one ask again until the page is whole. Every frame while any is owed.
        private static void SettleDrawings()
        {
            if (!UI.Blueprint.Pending || _cells == null)
            {
                return;
            }
            UI.Blueprint.Pending = false;
            for (int i = 0; i < _cells.Length; i++)
            {
                CellView c = _cells[i];
                if (!c.OwedDrawing || !c.Go.activeSelf || c.Drawn == null)
                {
                    continue;
                }
                Sprite icon = c.Icon != null ? c.Icon.overrideSprite : null;
                c.Drawn.sprite = UI.Blueprint.Drawing(icon, 60);
                c.Drawn.enabled = c.Drawn.sprite != null;
                c.OwedDrawing = c.Drawn.sprite == null && UI.Blueprint.Pending;
            }
        }
    }
}
