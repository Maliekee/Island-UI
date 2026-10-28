using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — the Orders page as a printed form (Forgejo #20, variant A).
    //
    // EVERY TOGGLE IS ITS ICON. The page was tick boxes beside words, while the
    // UI already had a picture for most of what they switch: the heart for love,
    // the libido flame for sex, a house for houses. So a toggle is that picture
    // on a tile - lit on a wash of its colour when on, grey and struck through
    // when off - with the word under it naming the thing, never the state, and
    // a sentence saying the state as its tip. Following is the one toggle the
    // dock already draws, so it is the dock's own tile (Forgejo #133). Combat
    // is one row of four rather than a dropdown: four short words
    // fit, and the one in force reads at a glance. Facing is two arrows beside
    // the pose it goes with.
    //
    // FOLLOWING DIMS THE PLACE, because in vanilla it overrides it (NPCMove):
    // a follower's idle becomes Follow (ActionCheck: `actType == Idle && npcType
    // == Follow`), so Travel, Live and Sleep inside the territory are never
    // picked, and the chase leash from the territory is skipped
    // (`npcType != Follow && Distance(territory, ...) >= tmpChaseDist`);
    // WarpToTerritory refuses a follower with a caution. Nothing clears the place
    // either - GoodBye only sets npcType back - so the dimmed controls still
    // press, and what they set takes over when following ends.
    internal static partial class InventoryWindow
    {
        private const int LineSegments = 38; // a caption over one row of choices, the one in force inked (Index)
        private const int LineStand = 39;    // the Follow tile beside Set place and Warp to place (On = following)
        private const int LineTiles = 40;    // icon tiles side by side, each lit or struck (Cans); Args say which

        // the kinds this page and the placement panel draw, for the shared lists
        // of pressable and form-owned kinds
        private static bool OrdersKind(int kind)
        {
            return kind == LineSegments || kind == LineStand || kind == LineTiles || PlaceKind(kind);
        }

        // what a press on the page does, as a form code (FormArgs). Below 2000
        // the form's own: CheckOption + a sexInfo flag, or ToggleFollow.
        private const int CodeSetPlace = 2000;
        private const int CodeWarp = 2001;
        private const int CodeFacing = 2100;   // + 0 left, 1 right
        private const int CodeCombat = 3000;   // + the order type

        private const int StyleDropFacing = 1;  // a dropdown with the facing arrows beside it

        private const float FormStandH = 34f;
        private const float StandTileS = 22f;       // the dock's Follow tile, as wide as a dock's
        private const float StandPlaceX = 37f;      // Set place starts here, past the tile and its word
        private const float StandPlaceH = 28f;
        private const float StandWarpW = 50f;
        private const float StandGap = 4.5f;
        // the permission tiles as wide as the dock's (StandTileS): at 28 they were
        // huge, and the Orders page scrolled for a few units
        private const float FormTilesH = 36f;
        private const float PermTileS = 22f;
        private const float PermTileGap = 9f;
        private const float PermTileInset = 5f;     // a tile's word is wider than the tile; this keeps it on the paper
        private const float FacingGap = 3f;
        private const float FacingRoomW = 2f * KitDropdown.Height + FacingGap + 4f;

        private static readonly Color PermTileFaint = Theme.ParseColor("#BEB4A0", Theme.DossierSoft);   // an off tile's rim and icon
        private static readonly Color PermTilePaper = Theme.ParseColor("#F5EDD9", Color.white);         // the paper a lit tile's wash is mixed into
        private static readonly Color SegWalnut = Theme.ParseColor("#583C28", Theme.DossierInk);
        // the world circles' colours, deepened to hold on paper, and both solid as
        // the rings on the ground are: the territory cyan, the chase range red
        // (Forgejo #133: it was gold and dashed, which nothing in the world is)
        private static readonly Color TerritoryInk = Theme.ParseColor("#1FA3CC", Color.cyan);
        private static readonly Color ChaseInk = Theme.ParseColor("#CC2020", Color.red);
        // the rim round Set place's disc
        private static readonly Color StandDiscRim = Theme.ParseColor("#3C281A", Theme.DossierInk);

        // ---------- the page ----------

        // WHAT YOU HAVE TOLD THEM: the plain Orders writer, re-set as a form - its
        // headings become sections, combat a row of four, Following and the two
        // place acts one block, the facing pair arrows beside the pose, the
        // permissions icon tiles, and the last row's Banish printed in red.
        // Re-set rather than rewritten, because that writer reads vanilla's own
        // panels and sliders and arms the territory preview, and one copy of that
        // is one copy to keep right. While "Set place" is armed the same window is
        // the placement panel instead (PlacePanel.cs).
        private static void WriteFormOrders(List<SheetLine> lines, CommonStates who)
        {
            WriteFormBand(lines, who);
            _formScratch.Clear();
            WriteOrders(_formScratch, who);   // ...even under the panel: it keeps the territory preview armed
            if (PlacingNow && ReferenceEquals(_placing, who))
            {
                WriteFormPlacing(lines, who);   // the window is the placement panel while the pick is up
                return;
            }
            bool first = true;
            int count = _formScratch.Count;
            for (int i = 0; i < count; i++)
            {
                SheetLine line = _formScratch[i];
                SheetLine next = i + 1 < count ? _formScratch[i + 1] : default(SheetLine);
                switch (line.Kind)
                {
                    case LineHead:
                        lines.Add(Section(first ? Shared.Loc.Get("orders.section.assignment", "ASSIGNMENT") : FormSectionWord(line.Label)));
                        first = false;
                        break;
                    case LineDropdown when line.Arg == 0:
                        lines.Add(CombatSegments(line));
                        break;
                    case LineDropdown when line.Arg == DropPose:
                        if (next.Kind == LineActions && next.Arg == ActionsRot)
                        {
                            // the facing pair, beside the pose it belongs to
                            line.Style = StyleDropFacing;
                            line.Args = new[] { CodeFacing, CodeFacing + 1 };
                            line.Cans = new[] { next.Enabled };
                            line.Number = next.Index + 1;
                            line.Tips = new[] { line.Tip, TipFacingLeft, TipFacingRight };
                            i++;
                        }
                        lines.Add(line);
                        break;
                    case LineToggle when line.Arg == ToggleFollow:
                    {
                        bool acts = next.Kind == LineActions && next.Arg == ActionsStanding;
                        if (acts)
                        {
                            i++;
                        }
                        lines.Add(StandBlock(who, line, acts ? next : default(SheetLine)));
                        break;
                    }
                    case LineOption:
                    {
                        var options = new List<SheetLine>();
                        while (i < count && _formScratch[i].Kind == LineOption)
                        {
                            options.Add(_formScratch[i]);
                            i++;
                        }
                        i--;
                        lines.Add(PermissionTiles(options));
                        break;
                    }
                    case LineActions:
                        // Call · Banish: the one act on the sheet that cannot be taken back
                        if (line.Arg == 0 && line.Options != null && line.Options.Length > 1)
                        {
                            line.Danger = 2;
                        }
                        lines.Add(line);
                        break;
                    default:
                        lines.Add(line);
                        break;
                }
            }
        }

        // A HEAD OF THE ORDERS PAGE AS THE FORM'S SECTION, in capitals the translator
        // wrote: never ToUpper on a translation. The head's label is matched against
        // the same call that wrote it (Orders.cs), so both sides are in one language.
        private static string FormSectionWord(string label)
        {
            if (label == Shared.Loc.Get("orders.standing", "Where they stand"))
            {
                return Shared.Loc.Get("orders.section.standing", "WHERE THEY STAND");
            }
            if (label == Shared.Loc.Get("orders.permissions", "Permissions"))
            {
                return Shared.Loc.Get("orders.section.permissions", "PERMISSIONS");
            }
            return (label ?? string.Empty).ToUpperInvariant();
        }

        private static SheetLine CombatSegments(SheetLine drop)
        {
            int n = drop.Options != null ? drop.Options.Length : 0;
            var args = new int[n];
            var tips = new string[n];
            for (int i = 0; i < n; i++)
            {
                args[i] = CodeCombat + i;
                tips[i] = i < CombatTips.Length ? CombatTips[i] : null;
            }
            return new SheetLine
            {
                Kind = LineSegments, Label = drop.Label, Enabled = true, Options = drop.Options, Index = drop.Index,
                Args = args, Tips = tips,
            };
        }

        // Follow in the dock's word; Set place carrying the place itself - the two
        // rings to scale, as the placement panel leads with them - and what a
        // press does under its word; Warp to place beside it
        private static SheetLine StandBlock(CommonStates who, SheetLine follow, SheetLine acts)
        {
            bool following = follow.On;
            string[] words = acts.Options != null && acts.Options.Length > 1 ? acts.Options : new[] { Shared.Loc.Get("orders.set_place", "Set place"), Shared.Loc.Get("orders.warp_to_place", "Warp to place") };
            NPCMove move = who != null ? who.nMove : null;
            return new SheetLine
            {
                Kind = LineStand, Enabled = true, On = following,
                Options = new[] { Shared.Loc.Get("unit.act.follow", "Follow"), words[0], words[1] },
                Value2 = Shared.Loc.Get("orders.set_place.line", "click the ground to move it"),
                Fracs = new[] { move != null ? move.territoryRad : 0f, move != null ? move.chaseDist : 0f },
                Args = new[] { ToggleFollow, CodeSetPlace, CodeWarp },
                Tips = new[]
                {
                    (following ? TipFollowingOn : TipFollowingOff) + "\n" + TipFollowing,
                    following ? TipStanding[0] + "\n" + TipPlaceWhileFollowing : TipStanding[0],
                    TipStanding[1],
                },
            };
        }

        private static SheetLine PermissionTiles(List<SheetLine> options)
        {
            int n = options.Count;
            var line = new SheetLine
            {
                Kind = LineTiles, Enabled = true,
                Options = new string[n], Cans = new bool[n], Args = new int[n], Tips = new string[n],
            };
            for (int i = 0; i < n; i++)
            {
                int id = options[i].Arg;
                bool on = options[i].On;
                line.Options[i] = PermissionWord(id, options[i].Label);
                line.Cans[i] = on;
                line.Args[i] = CheckOption + id;
                string state = PermissionState(id, on);
                line.Tips[i] = (state != null ? "<b>" + state + "</b>\n" : string.Empty) + options[i].Tip;
            }
            return line;
        }

        // the word under a tile names the thing, never the state; vanilla's own
        // label for a flag this does not know
        private static string PermissionWord(int id, string vanilla)
        {
            switch (id)
            {
                case 7: return Shared.Loc.Get("orders.permission.house", "House");
                case 8: return Shared.Loc.Get("orders.permission.love", "Love");
                case 9: return Shared.Loc.Get("orders.permission.sex", "Sex");
                case 10: return Shared.Loc.Get("orders.permission.force", "Force");
            }
            return vanilla;
        }

        // ...and the tip's first line says the state, as a sentence
        private static string PermissionState(int id, bool on)
        {
            switch (id)
            {
                case 7: return on ? Shared.Loc.Get("orders.permission.house.on", "Goes in and out of houses") : Shared.Loc.Get("orders.permission.house.off", "Stays out of houses");
                case 8: return on ? Shared.Loc.Get("orders.permission.love.on", "Builds love with neighbours") : Shared.Loc.Get("orders.permission.love.off", "No love, and no grudges");
                case 9: return on ? Shared.Loc.Get("orders.permission.sex.on", "Has sex when the mood takes") : Shared.Loc.Get("orders.permission.sex.off", "No sex");
                case 10: return on ? Shared.Loc.Get("orders.permission.force.on", "Forces sex when the mood takes") : Shared.Loc.Get("orders.permission.force.off", "Forces no one");
            }
            return null;
        }

        // ---------- the pulse: a cause's fix landing on its tile ----------

        // A CAUSE LANE'S "Orders" OPENS THIS PAGE AND POINTS (Mood.cs): what the
        // cause names glows for a moment - the place for an area or a house,
        // the flame for sex - so the press lands on the control and not on a page.
        private const int PulsePlace = 1;
        private const int PulseSex = 2;
        private const float OrdersPulseFor = 2.4f;
        private static int _ordersPulse;
        private static float _ordersPulseUntil;

        private static void PulseOrders(int what)
        {
            _ordersPulse = what;
            _ordersPulseUntil = Time.unscaledTime + OrdersPulseFor;
        }

        private static bool Pulsing(int what)
        {
            return (_ordersPulse & what) != 0 && Time.unscaledTime < _ordersPulseUntil;
        }

        // drawn under the control it rings, and every frame while it lasts
        private static void PulseGlow(SheetRow row, float x, float y, float w, float h)
        {
            float a = 0.25f + 0.55f * (0.5f + 0.5f * Mathf.Sin((Time.unscaledTime - _ordersPulseUntil) * 8f));
            FormImage(row, "kit_dossier_chip", 8, Alpha(StarShine, a), x - 2f, y - 2f, w + 4f, h + 4f);
            _nextSheetPaint = 0f;
        }

        // ---------- drawing ----------

        // ONE ROW OF CHOICES: the field the dropdowns are printed on, split in
        // equal parts, the one in force a walnut chip with cream type
        private static int DrawSegments(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            FormText(row, (line.Label ?? string.Empty).ToUpperInvariant(), CaptionSize, Theme.DossierSoft,
                TextAlignmentOptions.MidlineLeft, true, left, 0f, right - left, FormCaptionH, CaptionSpacing);
            int n = line.Options != null ? Mathf.Min(line.Options.Length, SheetPlankCount) : 0;
            if (n == 0)
            {
                return 0;
            }
            float y = FormCaptionH;
            float sh = KitDropdown.Height;
            float cw = (right - left) / n;
            FormImage(row, "kit_dossier_field", 6, Color.white, left, y, right - left, sh);
            for (int i = 0; i < n; i++)
            {
                float x = left + i * cw;
                bool chosen = i == line.Index;
                bool down = Down(row, i);
                if (chosen)
                {
                    // the field's own sprite, inked: the chip's corners are too round at this height
                    FormImage(row, "kit_dossier_field", 6, SegWalnut, x + 1.5f, y + 1.5f, cw - 3f, sh - 3f);
                }
                else
                {
                    if (Hot(row, i))
                    {
                        FormImage(row, null, 0, Alpha(Theme.DossierInk, down ? 0.16f : 0.07f),
                            x + 1.5f, y + 1.5f, cw - 3f, sh - 3f);
                    }
                    if (i > 0 && i - 1 != line.Index)
                    {
                        FormImage(row, null, 0, PermTileFaint, x - 0.25f, y + 4f, 0.5f, sh - 8f);
                    }
                }
                FormText(row, line.Options[i] ?? string.Empty, FormTextSize, chosen ? Theme.KitCream : Theme.DossierInk,
                    TextAlignmentOptions.Center, chosen, x + 1f, y + (down ? 0.4f : 0f), cw - 2f, sh, fit: true);
                FormHit(row, i, x, y, cw, sh);
                FormTip(row, line.Tips, i, x, y, cw, sh);
            }
            return n;
        }

        // FOLLOWING, AND WHERE THEY STAND WHEN THEY DO NOT (Forgejo #133, B): the
        // dock's own Follow tile, its green check while they follow; Set place the
        // big walnut button that shows the place; Warp to place beside it, small.
        // Warp to place dims while following, which refuses it; Set place stays
        // live - what they set takes over when following ends.
        private static int DrawStand(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            bool following = line.On;
            float tx = left + PermTileInset;
            DrawFollowTile(row, 0, following, line.Options[0], tx, 2f);
            FormTip(row, line.Tips, 0, left, 0f, StandPlaceX - 2f, h);
            float wx = right - StandWarpW;
            float bx = left + StandPlaceX;
            float bw = wx - StandGap - bx;
            if (Pulsing(PulsePlace))
            {
                PulseGlow(row, bx, 2f, bw, StandPlaceH);
            }
            float rad = line.Fracs != null && line.Fracs.Length > 0 ? line.Fracs[0] : 0f;
            float chase = line.Fracs != null && line.Fracs.Length > 1 ? line.Fracs[1] : 0f;
            DrawPlaceButton(row, 1, line.Options[1], line.Value2, rad, chase, bx, 2f, bw, StandPlaceH);
            FormTip(row, line.Tips, 1, bx, 2f, bw, StandPlaceH);
            float wy = 2f + (StandPlaceH - FormButtonH) * 0.5f;
            DrawWarpButton(row, 2, line.Options[2], following, wx, wy, StandWarpW);
            FormTip(row, line.Tips, 2, wx, wy, StandWarpW, FormButtonH);
            return 3;
        }

        // FOLLOW, PRINTED: the permission tiles' paper tile with the walkers'
        // line glyph - lit on a green wash with an ink rim and the dock's check
        // while they follow, faint on the paper when not. The dock's painted
        // wooden tile was tried here, and read too dark and brown on the paper.
        private static void DrawFollowTile(SheetRow row, int k, bool on, string word, float x, float y)
        {
            bool down = Down(row, k);
            bool hot = Hot(row, k);
            float s = StandTileS;
            float dy = down ? 0.4f : 0f;
            if (on)
            {
                FormImage(row, "kit_dossier_chip", 8, Color.Lerp(PermTilePaper, PlacePointGreen, 0.22f), x, y, s, s);
            }
            if (hot)
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(Theme.DossierInk, down ? 0.14f : 0.06f), x, y, s, s);
            }
            FormImage(row, "kit_dossier_button", 12, on ? Theme.DossierInk : PermTileFaint, x, y, s, s);
            float ic = s * 0.62f;
            FormSprite(row, HudGlyphs.Get("follow"), on ? Theme.DossierInk : PermTileFaint,
                x + (s - ic) * 0.5f, y + (s - ic) * 0.5f + dy, ic, ic);
            if (on)
            {
                const float c = 9f;
                FormSprite(row, AnimePieces.Get("mark_check"), Color.white, x + s - c * 0.62f, y - c * 0.38f + dy, c, c);
            }
            FormText(row, word ?? string.Empty, FieldLabelSize, on ? Theme.DossierInk : Theme.DossierSoft,
                TextAlignmentOptions.Center, on, x - 2f * PermTileInset, y + s + 0.5f, s + 4f * PermTileInset, 8f);
            FormHit(row, k, x, y, s, s + 8.5f);
        }

        // SET PLACE CARRIES THE PLACE: a printed button on the paper, as Warp to
        // place is, a light wash inside its ink rim; on it a paper disc with the
        // two rings to scale (PlaceRings, the placement panel's picture), then the
        // word and, under it, what a press does. It was the kit's walnut plank,
        // too dark and brown on the paper.
        private static void DrawPlaceButton(SheetRow row, int k, string label, string sub, float rad, float chase,
            float x, float y, float bw, float bh)
        {
            bool down = Down(row, k);
            bool hot = Hot(row, k);
            float dy = down ? 0.4f : 0f;
            Color ink = Theme.DossierInk;
            FormImage(row, "kit_dossier_chip", 8, PermTilePaper, x, y, bw, bh);
            if (hot)
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(Theme.DossierInk, down ? 0.14f : 0.06f), x, y, bw, bh);
            }
            FormImage(row, "kit_dossier_button", 12, ink, x, y, bw, bh);
            float r = bh * 0.39f;
            float cx = x + 3f + r;
            float cy = y + bh * 0.5f + dy;
            FormImage(row, "kit_dossier_dot", 0, Alpha(StandDiscRim, 0.55f), cx - r - 0.6f, cy - r - 0.6f, 2f * r + 1.2f, 2f * r + 1.2f);
            FormImage(row, "kit_dossier_dot", 0, Color.white, cx - r, cy - r, 2f * r, 2f * r);
            PlaceRings(row, cx, cy, r * 1.62f, rad, chase, 5f, 1f);
            float lx = cx + r + 5f;
            float lw = x + bw - 3f - lx;
            FormText(row, label ?? string.Empty, FormTextSize, ink, TextAlignmentOptions.MidlineLeft, true,
                lx, cy - 3.7f - 4.5f, lw, 9f, fit: true);
            FormText(row, sub ?? string.Empty, 4.9f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                lx, cy + 4.6f - 3.5f, lw, 7f, fit: true);
            FormHit(row, k, x, y, bw, bh);
        }

        // WARP TO PLACE: a paper button, the pin arriving at once before the word
        private static void DrawWarpButton(SheetRow row, int k, string label, bool dim, float x, float y, float bw)
        {
            Color ink = dim ? Theme.DossierSoft : Theme.DossierInk;
            bool down = Down(row, k);
            if (Hot(row, k))
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(ink, down ? 0.22f : 0.1f), x, y, bw, FormButtonH);
            }
            FormImage(row, "kit_dossier_button", 12, ink, x, y, bw, FormButtonH);
            float dy = down ? 0.4f : 0f;
            const float gh = 6f;
            const float gw = gh * 37f / 32f;
            const float gap = 2f;
            // measured, then centred with the glyph; a longer word shrinks to the room
            TextMeshProUGUI t = FormText(row, label ?? string.Empty, 5.8f, ink, TextAlignmentOptions.MidlineLeft, false,
                x, y + dy, bw, FormButtonH, fit: true);
            float tw = Mathf.Min(TextWidth(t), bw - gw - gap - 5f);
            float gx = x + (bw - gw - gap - tw) * 0.5f;
            Theme.Place(t, gx + gw + gap, y + dy, tw + 1f, FormButtonH);
            FormImage(row, "kit_warp", 0, ink, gx, y + (FormButtonH - gh) * 0.5f + dy, gw, gh);
            FormHit(row, k, x, y, bw, FormButtonH);
        }

        // THE PLACE IN SMALL: the world's two circles, the chase range red at
        // territory + chase as the outer ring, the territory cyan inside it to
        // scale - never so small it stops reading as a ring - and the place a
        // green dot. Set place's picture, and the placement panel's.
        private static void PlaceRings(SheetRow row, float cx, float cy, float d, float rad, float chase, float least, float alpha)
        {
            if (chase > 0f)
            {
                PlaceRing(row, cx, cy, d, 30, Mathf.PI * d / 30f + 0.3f, Alpha(ChaseInk, alpha));
            }
            if (rad > 0f)
            {
                float inner = chase > 0f ? Mathf.Max(least, d * rad / (rad + chase)) : d;
                PlaceRing(row, cx, cy, inner, 28, Mathf.PI * inner / 28f + 0.3f, Alpha(TerritoryInk, alpha));
            }
            float dot = Mathf.Max(1.8f, d * 0.06f);
            FormImage(row, "kit_dossier_dot", 0, Alpha(PlacePointGreen, alpha), cx - dot * 0.5f, cy - dot * 0.5f, dot, dot);
        }

        // THE PERMISSIONS, AS THEIR ICONS: one tile each, from the left
        private static int DrawTiles(SheetRow row, SheetLine line, float w, float h)
        {
            int n = line.Options != null ? Mathf.Min(line.Options.Length, SheetPlankCount) : 0;
            float x = SheetPadX + PermTileInset;
            for (int i = 0; i < n; i++)
            {
                int code = line.Args != null && i < line.Args.Length ? line.Args[i] : 0;
                bool on = line.Cans != null && i < line.Cans.Length && line.Cans[i];
                DrawToggleTile(row, i, code, on, line.Options[i], x, 2f, PermTileS,
                    code == CheckOption + 9 && Pulsing(PulseSex));
                FormTip(row, line.Tips, i, x - 4f, 0f, PermTileS + 8f, h);
                x += PermTileS + PermTileGap;
            }
            return n;
        }

        // ONE TILE: lit on a wash of its colour with an ink rim, or grey on the
        // paper behind a faint one and struck through; the word under it
        private static void DrawToggleTile(SheetRow row, int k, int code, bool on, string word,
            float x, float y, float s, bool pulse)
        {
            bool hot = Hot(row, k);
            bool down = Down(row, k);
            float dy = down ? 0.4f : 0f;
            if (pulse)
            {
                PulseGlow(row, x, y, s, s);
            }
            if (on)
            {
                FormImage(row, "kit_dossier_chip", 8, Color.Lerp(PermTilePaper, PermTileTint(code), 0.22f), x, y, s, s);
            }
            if (hot)
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(Theme.DossierInk, down ? 0.14f : 0.06f), x, y, s, s);
            }
            FormImage(row, "kit_dossier_button", 12, on ? Theme.DossierInk : PermTileFaint, x, y, s, s);
            float ic = s * 0.58f;
            DrawTileIcon(row, code, on, x + (s - ic) * 0.5f, y + (s - ic) * 0.5f + dy, ic);
            if (!on)
            {
                Image strike = FormImage(row, null, 0, Theme.DossierSoft, 0f, 0f, s * 0.62f, 0.9f);
                Tilt(strike, x + s * 0.5f, y + s * 0.5f + dy, 45f);
            }
            FormText(row, word ?? string.Empty, FormTextSize, on ? Theme.DossierInk : Theme.DossierSoft,
                TextAlignmentOptions.Center, on, x - 2f * PermTileInset, y + s + 0.5f, s + 4f * PermTileInset, 8.5f);
            FormHit(row, k, x, y, s, s + 9f);
        }

        private static Color PermTileTint(int code)
        {
            switch (code)
            {
                case CheckOption + 7: return PlaceHouseTint;
                case CheckOption + 8: return LoveWidget.Pink;
                case CheckOption + 9: return LibidoTint;
            }
            return Theme.DossierRed;
        }

        // the icons the UI already has: the house, the roster's heart and the
        // libido flame (hollow and grey when off)
        private static void DrawTileIcon(SheetRow row, int code, bool on, float x, float y, float s)
        {
            switch (code)
            {
                case CheckOption + 7:
                    FormSprite(row, HudGlyphs.Get("home"), on ? PlaceHouseTint : PermTileFaint, x, y, s, s);
                    break;
                case CheckOption + 8:
                    FormImage(row, on ? "kit_heart" : "kit_heart_hollow", 0, on ? LoveWidget.Pink : PermTileFaint, x, y, s, s);
                    break;
                case CheckOption + 9:
                    FormImage(row, on ? "kit_flame_pink" : "kit_flame", 0, on ? Color.white : PermTileFaint, x, y, s, s);
                    break;
                default:
                    FormImage(row, "kit_flame", 0, on ? Theme.DossierRed : PermTileFaint, x, y, s, s);
                    break;
            }
        }

        // THE FACING PAIR beside the pose's field: two arrows, the set one inked;
        // grey where it does not bite (territory above 0, or following)
        private static int DrawFacing(SheetRow row, SheetLine line, float w)
        {
            float right = w - SheetPadX;
            float s = KitDropdown.Height;
            float y = FormCaptionH;
            bool bites = line.Cans != null && line.Cans.Length > 0 && line.Cans[0];
            int set = line.Number - 1;
            for (int i = 0; i < 2; i++)
            {
                float x = right - (2 - i) * s - (1 - i) * FacingGap;
                bool chosen = i == set;
                bool down = Down(row, i);
                Color ink = bites ? Theme.DossierInk : Theme.DossierSoft;
                if (chosen)
                {
                    FormImage(row, "kit_dossier_field", 6, bites ? SegWalnut : Theme.DossierSoft, x, y, s, s);
                }
                else
                {
                    FormImage(row, "kit_dossier_field", 6, Color.white, x, y, s, s);
                    if (Hot(row, i))
                    {
                        FormImage(row, "kit_dossier_chip", 8, Alpha(Theme.DossierInk, down ? 0.16f : 0.07f), x, y, s, s);
                    }
                }
                Image chevron = FormImage(row, "kit_chevron", 0, chosen ? Theme.KitCream : ink, 0f, 0f, 7.5f, 5f);
                Tilt(chevron, x + s * 0.5f, y + s * 0.5f + (down ? 0.4f : 0f), i == 0 ? -90f : 90f);
                FormHit(row, i, x, y, s, s);
                FormTip(row, line.Tips, i + 1, x, y, s, s);
            }
            FormTip(row, line.Tips, 0, SheetPadX, 0f, right - FacingRoomW - SheetPadX, FormDropRowH);
            return 2;
        }

        // THE PRINTED SCALE'S FILL, in its world circle's colour and solid, as the
        // rings on the ground are drawn
        private static void DrawScaleFill(SheetRow row, SheetLine line, float cy, bool dim)
        {
            float end = row.TrackX + Mathf.Clamp01(line.Frac) * row.TrackW;
            const float thick = 1.6f;
            FormImage(row, null, 0, Alpha(ScaleInk(line), dim ? 0.35f : 1f), row.TrackX, cy - thick * 0.5f,
                Mathf.Max(0f, end - row.TrackX), thick);
        }

        private static Color ScaleInk(SheetLine line)
        {
            return line.Arg == SliderTerritory ? TerritoryInk : ChaseInk;
        }

        // ...and under it, its ring as the key, and what that ring on the ground is
        private static void DrawScaleLine(SheetRow row, SheetLine line, bool dim)
        {
            if (string.IsNullOrEmpty(line.Value2))
            {
                return;
            }
            const float ring = 3.6f;
            const float lh = 6f;
            float y = FormScaleRowH + FormScaleLineH - lh;   // into the scale's own room under its track
            FormImage(row, "kit_ring", 0, Alpha(ScaleInk(line), dim ? 0.4f : 1f), row.TrackX, y + (lh - ring) * 0.5f, ring, ring);
            float tx = row.TrackX + ring + 2f;
            FormText(row, line.Value2, 4.9f, dim ? PermTileFaint : Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                tx, y, row.TrackX + row.TrackW - tx, lh, fit: true);
        }

        // ---------- pressing ----------

        private static void PressOrdersForm(Plate plate, SheetRow row, int sub)
        {
            CommonStates who = plate.Owner;
            if (who == null || row.FormArgs == null || sub < 0 || sub >= row.FormArgs.Length)
            {
                return;
            }
            int code = row.FormArgs[sub];
            if (code >= CodeCombat)
            {
                ChooseOrder(plate, 0, code - CodeCombat);
            }
            else if (code >= CodeFacing && code < CodeFacing + 2)
            {
                RotAction(who, code - CodeFacing);
            }
            else if (code == CodeSetPlace || code == CodeWarp)
            {
                StandingAction(who, code - CodeSetPlace);
            }
            else if (PlaceCode(code))
            {
                PressPlacePanel(who, code);
            }
            else
            {
                PressForm(who, row, sub);   // the permissions and Following, as the checkboxes pressed them
            }
        }

        // For the harness: the Orders page's toggles and acts by name, as a press
        // on them does - "follow", "house", "love", "sex", "force", "set place",
        // "warp" (or "go there", its old name), "combat N", "left", "right", and
        // the placement panel's "cancel"
        internal static string OrdersPress(string what)
        {
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                Plate plate = SheetPlates[p];
                if (plate.Group != GroupCharacter || plate.Rows == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                int want = OrdersCodeNamed(what);
                for (int i = 0; i < plate.RowsShown && i < plate.Rows.Count; i++)
                {
                    SheetRow row = plate.Rows[i];
                    for (int k = 0; row.FormArgs != null && k < row.FormArgs.Length; k++)
                    {
                        if (row.FormArgs[k] == want && (OrdersKind(row.DrewKind) || row.DrewKind == LineDropdown))
                        {
                            PressSheetRow(plate, row, k);
                            return "pressed " + what + " (row " + i + ", " + k + ")";
                        }
                    }
                }
                return "no " + what + " on the sheet";
            }
            return "no sheet";
        }

        private static int OrdersCodeNamed(string what)
        {
            string w = (what ?? string.Empty).Trim().ToLowerInvariant();
            if (w.StartsWith("combat ") && int.TryParse(w.Substring(7), out int c)) { return CodeCombat + c; }
            switch (w)
            {
                case "follow": return ToggleFollow;
                case "house": return CheckOption + 7;
                case "love": return CheckOption + 8;
                case "sex": return CheckOption + 9;
                case "force": return CheckOption + 10;
                case "set place": return CodeSetPlace;
                case "warp":
                case "warp to place":
                case "go there": return CodeWarp;
                case "left": return CodeFacing;
                case "right": return CodeFacing + 1;
                case "cancel": return CodePlaceCancel;
            }
            return -1;
        }

        // For the harness: everyone of yours whose mood causes carry a fix, with
        // their age and kind (a screenshot's subject must be an adult) - "name
        // (age kind): cause = fix, fix; ..."
        internal static string MoodFixList()
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; npcs != null && npcs.friendList != null && i < npcs.friendList.Count; i++)
            {
                CommonStates who = npcs.friendList[i];
                if (who == null || !npcs.IsActiveFriend(who))
                {
                    continue;
                }
                var parts = new List<string>();
                foreach (MoodFinding f in MoodFindings(who))
                {
                    var labels = new List<string>();
                    foreach (MoodFix fix in FixesFor(who, f)) { labels.Add(fix.Label); }
                    if (labels.Count > 0) { parts.Add(f.Key + " = " + string.Join(", ", labels.ToArray())); }
                }
                if (parts.Count > 0)
                {
                    sb.Append(who.charaName).Append(" (").Append(who.age).Append(' ').Append(KindName(npcs, who)).Append("): ")
                        .Append(string.Join("; ", parts.ToArray())).Append('\n');
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no fixes anywhere";
        }

        // For the harness: a cause lane's fix pressed, "Name|cause|label" - the
        // act its button runs, on their sheet
        internal static string MoodFixPress(string arg)
        {
            string[] p = (arg ?? string.Empty).Split('|');
            CommonStates who = p.Length == 3 ? FriendNamed(p[0]) : null;
            if (who == null)
            {
                return "usage: Name|cause|label";
            }
            foreach (MoodFinding f in MoodFindings(who))
            {
                if (f.Key != p[1]) { continue; }
                foreach (MoodFix fix in FixesFor(who, f))
                {
                    if (fix.Label == p[2])
                    {
                        fix.Act();
                        return "pressed " + p[2] + " on " + who.charaName + "'s " + p[1] + ", sheet "
                            + (_focusShown ? _focusAspect.ToString() : "closed");
                    }
                }
            }
            return "no " + p[2] + " on " + who.charaName + "'s " + p[1];
        }

        // For the harness: a scale to a value in metres, "territory 3" or "chase 10",
        // through the slider's own path - to put a test's person back
        internal static string OrdersScale(string arg)
        {
            CommonStates who = Focus() as CommonStates;
            string[] p = (arg ?? string.Empty).Split(' ');
            if (who == null || who.nMove == null || p.Length != 2 || _mn == null || _mn.uiMN == null
                || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
            {
                return "usage: territory|chase <metres>, with a villager on the sheet";
            }
            bool ter = p[0] == "territory";
            Slider s = ter ? _mn.uiMN.territorySlider : _mn.uiMN.chaseSlider;
            SetStandingSlider(who, ter ? SliderTerritory : SliderChase, Mathf.InverseLerp(s.minValue, s.maxValue, v));
            return OrdersState();
        }

        // For the harness: everything the Orders page and Set place may change
        // on the person on the sheet, to record before a test and compare after
        internal static string OrdersState()
        {
            CommonStates who = Focus() as CommonStates;
            NPCMove m = who != null ? who.nMove : null;
            if (m == null)
            {
                return "no villager on the sheet";
            }
            var sb = new System.Text.StringBuilder();
            sb.Append(who.charaName).Append(": order ").Append(m.orderType).Append(", work ").Append(m.workType)
                .Append(", type ").Append(m.npcType).Append(", territory ").Append(m.territory.ToString("F4"))
                .Append(" pre ").Append(m.preTerritory.ToString("F4")).Append(" rad ").Append(m.territoryRad.ToString("0.000"))
                .Append(" chase ").Append(m.chaseDist.ToString("0.000")).Append(" house ").Append(m.fixedHouseID)
                .Append(", pose ").Append(m.orderAnim).Append(" rot ").Append(m.fixedRot).Append(", sexInfo");
            for (int i = 7; who.sexInfo != null && i < who.sexInfo.Length && i <= 10; i++)
            {
                sb.Append(' ').Append(i).Append('=').Append(who.sexInfo[i]);
            }
            return sb.ToString();
        }
    }
}
