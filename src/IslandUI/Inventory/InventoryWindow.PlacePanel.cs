using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE PLACEMENT PANEL (Forgejo #20, panel 2). While "Set place" is armed the
    // Orders window stops being the page and becomes the pick's own panel: what
    // the spot under the pointer would mean, with the room the tip at the
    // pointer never had - the keys, the two rings and their numbers, food,
    // water and the house, then every neighbour with love both ways. It sits
    // beside the world and its arcs, reads the same PlaceReading the arcs are
    // drawn from (TickPlaceFactors, as the pointer moves), and is the Orders
    // page again when the pick ends: placed, cancelled, or the target gone.
    internal static partial class InventoryWindow
    {
        private const int LinePlaceHead = 41;   // the walnut banner: who is being placed, and the keys
        private const int LinePlaceRings = 42;  // the two rings to scale, territory and chase range in numbers
        private const int LinePlaceRow = 43;    // a factor: swatch, label, what it gives, a note under it
        private const int LinePlaceNear = 44;   // neighbours side by side (Style 1: their column heads)
        private const int LinePlaceFoot = 45;   // Cancel, and the right-click note

        private const int CodePlaceCancel = 2200;

        private static bool PlaceKind(int kind)
        {
            return kind >= LinePlaceHead && kind <= LinePlaceFoot;
        }

        private static bool PlaceCode(int code)
        {
            return code == CodePlaceCancel;
        }

        private const float FormPlaceHeadH = 27f;
        private const float FormPlaceRingsH = 40f;
        private const float FormPlaceRowH = 11f;
        private const float FormPlaceNoteH = 6.5f;
        private const float FormPlaceFootH = 21f;
        private const float PlaceNearW = 112f;      // one neighbour's block: a RELATIONS name, the two numbers
        private const int PlaceNearOneColumn = 8;   // past this many, two to a line
        private const int PlaceNearMost = 24;       // ...and past this, the rest folded into a count
        private const float PlaceRingsD = 32f;

        private static float PlaceRowHeight(SheetLine line)
        {
            return string.IsNullOrEmpty(line.Value2) ? FormPlaceRowH : FormPlaceRowH + FormPlaceNoteH;
        }

        private static float PlaceKindHeight(SheetLine line)
        {
            switch (line.Kind)
            {
                case LinePlaceHead: return FormPlaceHeadH;
                case LinePlaceRings: return FormPlaceRingsH;
                case LinePlaceRow: return PlaceRowHeight(line);
                case LinePlaceNear: return line.Style == 1 ? FormCaptionH : FormLaneH;
                case LinePlaceFoot: return FormPlaceFootH;
            }
            return FormPlaceRowH;
        }

        // ---------- writing ----------

        private static void WriteFormPlacing(List<SheetLine> lines, CommonStates who)
        {
            NPCMove move = who.nMove;
            Slider ter = _mn != null && _mn.uiMN != null ? _mn.uiMN.territorySlider : null;
            lines.Add(new SheetLine
            {
                Kind = LinePlaceHead, Enabled = true, Label = Shared.Loc.Get("place.head", "Placing {0}", who.charaName),
                // the keys are caps; the mouse's two are words
                Options = new[] { "WASD", Shared.Loc.Get("place.key.wheel", "wheel"), "Esc", Shared.Loc.Get("place.key.click", "click") },
                Values = new[]
                {
                    Shared.Loc.Get("place.head.walk", "walk"),
                    Shared.Loc.Get("place.head.size", "size"),
                    Shared.Loc.Get("place.head.cancel", "cancel"),
                    Shared.Loc.Get("place.head.place", "place"),
                },
            });
            float rad = move != null ? move.territoryRad : 0f;
            float chase = move != null ? move.chaseDist : 0f;
            lines.Add(new SheetLine
            {
                Kind = LinePlaceRings, Enabled = true,
                Values = new[] { Meters(rad), "+" + Meters(chase) },
                Fracs = new[] { rad, chase },
                Value2 = ter != null
                    ? Shared.Loc.Get("place.rings.wheel", "the wheel over the world, {0} a notch, {1} to {2}",
                        ter.wholeNumbers ? Shared.Loc.Get("settings.unit.metres", "{0} m", 1) : Shared.Loc.Get("place.rings.a_step", "a step"),
                        ter.minValue.ToString("0"), ter.maxValue.ToString("0"))
                    : null,
                Tips = new[] { TipTerritory, TipChaseRange },
            });
            PlaceReading read = _placeReading;
            lines.Add(Section(Shared.Loc.Get("place.living_here", "LIVING HERE")));
            if (read == null)
            {
                lines.Add(new SheetLine { Kind = LinePlaceRow, Enabled = true, Value = Shared.Loc.Get("place.point_ground", "Point at the ground to read a spot") });
                AddPlaceFoot(lines);
                return;
            }
            foreach (PlaceRow row in read.Rows)
            {
                lines.Add(new SheetLine
                {
                    Kind = LinePlaceRow, Enabled = true, Label = row.Label, Value = row.Text, Value2 = row.Note,
                    Tint = row.Tint, Tinted = true,
                });
            }
            lines.Add(Section(Shared.Loc.Get("place.neighbours", "NEIGHBOURS"), read.NearNote == null ? read.Near.Count.ToString() : null));
            if (read.NearNote != null)
            {
                lines.Add(new SheetLine { Kind = LinePlaceRow, Enabled = true, Value = read.NearNote });
            }
            else
            {
                int cols = read.Near.Count > PlaceNearOneColumn ? 2 : 1;
                int shown = Mathf.Min(read.Near.Count, PlaceNearMost);
                NPCManager npcs = _mn != null ? _mn.npcMN : null;
                CommonStates pick = DesiredOf(who);
                lines.Add(new SheetLine { Kind = LinePlaceNear, Style = 1, Enabled = true, Number = cols });
                for (int i = 0; i < shown; i += cols)
                {
                    int n = Mathf.Min(cols, shown - i);
                    var line = new SheetLine
                    {
                        Kind = LinePlaceNear, Enabled = true, Number = cols,
                        Options = new string[n], Values = new string[n], Tints = new Color[n],
                        Args = new int[3 * n], Tips = new string[n],
                    };
                    for (int k = 0; k < n; k++)
                    {
                        // each a RELATIONS name - their type, the name, the words
                        // under it as Social prints them - then what a tick here does
                        PlaceNear near = read.Near[i + k];
                        CommonStates other = near.Who;
                        line.Options[k] = other.charaName;
                        line.Values[k] = PairWords(npcs, who, other, pick, out line.Tints[k]);
                        line.Args[3 * k] = near.There;
                        line.Args[3 * k + 1] = near.Back;
                        line.Args[3 * k + 2] = other.person != null && other.person.Length > 0 ? other.person[0] : -1;
                        line.Tips[k] = PlaceNearTip(who, near);
                    }
                    lines.Add(line);
                }
                if (read.Near.Count > shown)
                {
                    bool plain = true;
                    for (int i = shown; i < read.Near.Count; i++)
                    {
                        plain &= read.Near[i].There == 5 && read.Near[i].Back == 5;
                    }
                    lines.Add(new SheetLine
                    {
                        Kind = LinePlaceRow, Enabled = true,
                        Value = plain
                            ? Shared.Loc.Plural("place.near.more_plain", "and {0} more, all +5 both ways", "and {0} more, all +5 both ways", read.Near.Count - shown)
                            : Shared.Loc.Plural("place.near.more", "and {0} more", "and {0} more", read.Near.Count - shown),
                    });
                }
            }
            AddPlaceFoot(lines);
        }

        private static void AddPlaceFoot(List<SheetLine> lines)
        {
            lines.Add(new SheetLine
            {
                Kind = LinePlaceFoot, Enabled = true, Options = new[] { Shared.Loc.Get("place.cancel", "Cancel") + "   Esc" }, Args = new[] { CodePlaceCancel },
                Value = Shared.Loc.Get("place.cancel.note", "or right-click, as the game has it"),
                Tips = new[] { Shared.Loc.Get("place.cancel.help", "Leaves their place, house and area as they were.") },
            });
        }

        private static string PlaceNearTip(CommonStates who, PlaceNear near)
        {
            string they = Shared.Loc.Get("place.near.they", "they");
            string name = near.Who.charaName ?? they;
            int type = near.Who.person != null && near.Who.person.Length > 0 ? near.Who.person[0] : -1;
            return "<b>" + name + "</b>  " + TypeWithIcon(type) + "\n" + Shared.Loc.Get("place.near.tick", "A love tick, six a day while their areas meet:") + "\n"
                + Shared.Loc.Get("place.near.for", "{0} for {1}", who.charaName ?? they, name) + "  " + PlaceSigned(near.There) + "\n"
                + Shared.Loc.Get("place.near.for", "{0} for {1}", name, who.charaName ?? Shared.Loc.Get("place.near.them", "them"))
                + "  " + PlaceSigned(near.Back);
        }

        // ---------- drawing ----------

        private static int DrawPlacePanel(SheetRow row, SheetLine line, float w, float h)
        {
            switch (line.Kind)
            {
                case LinePlaceHead: DrawPlaceHead(row, line, w, h); return 0;
                case LinePlaceRings: DrawPlaceRings(row, line, w, h); return 0;
                case LinePlaceRow: DrawPlaceRow(row, line, w, h); return 0;
                case LinePlaceNear: DrawPlaceNear(row, line, w, h); return 0;
                case LinePlaceFoot: return DrawPlaceFoot(row, line, w, h);
            }
            return 0;
        }

        // the walnut banner: who, and the keys as keycaps
        private static void DrawPlaceHead(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            FormImage(row, "kit_dossier_field", 6, SegWalnut, left, 1f, right - left, h - 2f);
            FormText(row, line.Label ?? string.Empty, FormTextSize, Theme.KitCream, TextAlignmentOptions.MidlineLeft, true,
                left + 5f, 2.5f, right - left - 10f, 10f);
            float x = left + 5f;
            float ky = 15f;
            for (int i = 0; line.Options != null && i < line.Options.Length; i++)
            {
                TextMeshProUGUI cap = FormText(row, line.Options[i], 5.4f, Theme.DossierInk, TextAlignmentOptions.Center, true,
                    x, ky, 30f, 7f);
                float cw = TextWidth(cap) + 4f;
                Theme.Place(cap, x, ky, cw, 7f);
                // the keycap goes under its word: the art layer draws under the type
                FormImage(row, "kit_dossier_field", 6, Theme.KitCream, x, ky, cw, 7f);
                TextMeshProUGUI word = FormText(row, line.Values != null && i < line.Values.Length ? line.Values[i] : string.Empty,
                    6f, Theme.KitCream, TextAlignmentOptions.MidlineLeft, false, x + cw + 2f, ky, 40f, 7f);
                x += cw + 2f + TextWidth(word) + 7f;
            }
        }

        // THE RINGS TO SCALE: the world's circles in small (PlaceRings, which
        // Set place carries too), so the wheel's notch shows here as well; the
        // two readings in their rings' colours
        private static void DrawPlaceRings(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float rad = line.Fracs != null && line.Fracs.Length > 0 ? line.Fracs[0] : 0f;
            float chase = line.Fracs != null && line.Fracs.Length > 1 ? line.Fracs[1] : 0f;
            float d = PlaceRingsD;
            float cx = left + 3f + d * 0.5f;
            float cy = 3f + d * 0.5f;
            PlaceRings(row, cx, cy, d, rad, chase, 8f, 1f);
            float x0 = left + d + 14f;
            float colW = 62f;
            string[] caps = { Shared.Loc.Get("orders.territory", "Territory"), Shared.Loc.Get("orders.chase_range", "Chase range") };
            Color[] inks = { TerritoryInk, ChaseInk };
            for (int i = 0; i < 2; i++)
            {
                float x = x0 + i * colW;
                FormText(row, caps[i], 6f, Theme.DossierLabel, TextAlignmentOptions.MidlineLeft, false, x, 3f, colW, 7f);
                FormText(row, line.Values != null && i < line.Values.Length ? line.Values[i] : string.Empty, 12f, inks[i],
                    TextAlignmentOptions.MidlineLeft, true, x, 10f, colW, 13f);
                FormTip(row, line.Tips, i, x, 2f, colW, 22f);
            }
            if (!string.IsNullOrEmpty(line.Value2))
            {
                FormText(row, line.Value2, 5.6f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                    x0, 25f, w - SheetPadX - x0, 7f);
            }
        }

        // a ring of short strokes round (cx, cy): spaced, it is dashed; overlapping, solid
        private static void PlaceRing(SheetRow row, float cx, float cy, float d, int strokes, float len, Color ink)
        {
            for (int i = 0; i < strokes; i++)
            {
                float a = i * Mathf.PI * 2f / strokes;
                Image stroke = FormImage(row, null, 0, ink, 0f, 0f, len, 0.9f);
                Tilt(stroke, cx + Mathf.Cos(a) * d * 0.5f, cy - Mathf.Sin(a) * d * 0.5f, a * Mathf.Rad2Deg + 90f);
            }
        }

        private static readonly Color PlacePointGreen = Theme.ParseColor("#5AC83C", Color.green);

        // a factor: its swatch in the arcs' colour, its name, what the spot
        // gives, and under that what is nearest or what would fix it. With no
        // label it is a line of its own, in the caption's ink.
        private static void DrawPlaceRow(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            if (string.IsNullOrEmpty(line.Label))
            {
                FormText(row, line.Value ?? string.Empty, 6.6f, Theme.DossierLabel, TextAlignmentOptions.MidlineLeft, false,
                    left, 0f, right - left, FormPlaceRowH, fit: true);
                return;
            }
            const float sw = 5f;
            FormImage(row, null, 0, Theme.DossierInk, left, (FormPlaceRowH - sw) * 0.5f - 0.4f, sw + 0.8f, sw + 0.8f);
            FormImage(row, null, 0, line.Tint, left + 0.4f, (FormPlaceRowH - sw) * 0.5f, sw, sw);
            FormText(row, line.Label, FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, true,
                left + sw + 4f, 0f, 30f, FormPlaceRowH);
            float tx = left + sw + 36f;
            FormText(row, line.Value ?? string.Empty, 7f, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                tx, 0f, right - tx, FormPlaceRowH, fit: true);
            if (!string.IsNullOrEmpty(line.Value2))
            {
                FormText(row, line.Value2, 5.8f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                    tx, FormPlaceRowH - 1.5f, right - tx, FormPlaceNoteH, fit: true);
            }
        }

        // NEIGHBOURS, one or two to a line: each named as a RELATIONS lane names
        // them (DrawLaneName: the type's token, the name, the words under it),
        // then what a love tick does - to them, and back. Style 1 is their heads.
        private static void DrawPlaceNear(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            int cols = Mathf.Max(1, line.Number);
            float slot = (right - left) / cols;
            float bw = Mathf.Min(PlaceNearW, slot - 4f);
            const float numW = 24f;   // a column as wide as its head, the numbers centred under it
            if (line.Style == 1)
            {
                for (int c = 0; c < cols; c++)
                {
                    float bx = left + c * slot;
                    FormText(row, Shared.Loc.Get("place.near.to_them", "TO THEM"), CaptionSize, Theme.DossierSoft, TextAlignmentOptions.Center, true,
                        bx + bw - 2f * numW, 0f, numW, FormCaptionH, CaptionSpacing);
                    FormText(row, Shared.Loc.Get("place.near.back", "BACK"), CaptionSize, Theme.DossierSoft, TextAlignmentOptions.Center, true,
                        bx + bw - numW, 0f, numW, FormCaptionH, CaptionSpacing);
                }
                return;
            }
            int n = line.Options != null ? line.Options.Length : 0;
            for (int k = 0; k < n; k++)
            {
                float bx = left + k * slot;
                int there = line.Args[3 * k], back = line.Args[3 * k + 1];
                DrawLaneName(row, line.Args[3 * k + 2], line.Options[k], Theme.DossierInk, null,
                    line.Values[k], line.Tints[k], bx, bx + bw - 2f * numW - 2f, false);
                FormText(row, PlaceSigned(there), 7f, NearInk(there), TextAlignmentOptions.Center, true,
                    bx + bw - 2f * numW, 0f, numW, h);
                FormText(row, PlaceSigned(back), 7f, NearInk(back), TextAlignmentOptions.Center, true,
                    bx + bw - numW, 0f, numW, h);
                FormTip(row, line.Tips, k, bx, 0f, bw, h);
            }
            FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
        }

        private static Color NearInk(int v)
        {
            return v < 0 ? Theme.DossierRed : v > 5 ? PaperGood : Theme.DossierInk;
        }

        // Cancel, as Escape or vanilla's right-click
        private static int DrawPlaceFoot(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            const float bw = 60f;
            float by = (h - FormButtonH) * 0.5f;
            DrawButton(row, 0, line.Options[0], false, left, by, bw);
            FormTip(row, line.Tips, 0, left, by, bw, FormButtonH);
            FormText(row, line.Value ?? string.Empty, 6f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                left + bw + 6f, 0f, w - SheetPadX - left - bw - 6f, h);
            return 1;
        }

        // ---------- pressing ----------

        // THE PANEL OWNS THE POINTER WHILE THE PICK IS UP (DragOwnsThePointer).
        // Vanilla's loop places on any left click the game does not count as over
        // the UI, and the game asks the EventSystem - which answers nothing while
        // the game is unfocused, so in testing a click on the panel's Cancel fell
        // through and placed. The window's own rect decides instead.
        internal static bool PlacePanelUnderPointer
        {
            get
            {
                if (!PlacingNow || _root == null)
                {
                    return false;
                }
                Canvas canvas = _root.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera : null;
                for (int p = 0; p < SheetPlates.Count; p++)
                {
                    Plate plate = SheetPlates[p];
                    if (plate.Group == GroupCharacter && plate.Window != null && plate.Window.Go.activeInHierarchy
                        && RectTransformUtility.RectangleContainsScreenPoint(plate.Window.Rect, Input.mousePosition, cam))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // the panel's Cancel ends the pick as Escape does: the wrapper stops
        // stepping vanilla's loop, puts the place back, and hands back control
        private static void PressPlacePanel(CommonStates who, int code)
        {
            if (code == CodePlaceCancel && PlacingNow && ReferenceEquals(_placing, who))
            {
                _placeEscaped = true;
            }
        }
    }
}
