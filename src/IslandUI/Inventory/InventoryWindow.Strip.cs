using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The strip row: bars, fill icons, the header run.
    internal static partial class InventoryWindow
    {
        private static Image PlainImage(Transform parent, string name, Color color)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            go.SetActive(false);
            return img;
        }

        private static Image StripIcon(Transform parent, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            go.SetActive(false);
            return img;
        }

        private static void ShowStrip(SheetRow row, bool on)
        {
            bool barsOn = on && !row.IconsOnly;
            if (row.BarRim != null && (row.BarRim.gameObject.activeSelf != barsOn || row.Well.gameObject.activeSelf != (on && !row.Bare)))
            {
                row.Well.gameObject.SetActive(on && !row.Bare);
                row.BarRim.gameObject.SetActive(barsOn);
                row.BarTrack.gameObject.SetActive(barsOn);
                row.BarFill.gameObject.SetActive(barsOn);
                row.Stam.gameObject.SetActive(barsOn);
                row.StamFill.gameObject.SetActive(barsOn);
            }
            for (int i = 0; row.Ticks != null && i < row.Ticks.Length; i++)
            {
                bool show = barsOn && i < row.DrewTicks;   // n boxes need n-1 ticks
                if (row.Ticks[i].gameObject.activeSelf != show) { row.Ticks[i].gameObject.SetActive(show); }
            }
            for (int i = 0; row.Majors != null && i < row.Majors.Length; i++)
            {
                bool show = barsOn && i < row.DrewMajors;
                if (row.Majors[i].gameObject.activeSelf != show) { row.Majors[i].gameObject.SetActive(show); }
            }
            for (int i = 0; row.IconBacks != null && i < row.IconBacks.Length; i++)
            {
                bool show = on && (i < StripIcons - 1 || row.DrewBaby);
                if (row.IconRims[i].gameObject.activeSelf != show) { row.IconRims[i].gameObject.SetActive(show); }
                if (row.IconBacks[i].gameObject.activeSelf != show) { row.IconBacks[i].gameObject.SetActive(show); }
                if (row.IconFills[i].gameObject.activeSelf != show) { row.IconFills[i].gameObject.SetActive(show); }
            }
            if (!on && row.LoveMid != null && row.LoveMid.gameObject.activeSelf)
            {
                row.LoveMid.gameObject.SetActive(false);
                row.LoveTop.gameObject.SetActive(false);
            }
            if (!on)
            {
                ShowLevelUp(row, false);
            }
            for (int i = 0; row.Seps != null && i < row.Seps.Length; i++)
            {
                bool show = on && (i == 0 || row.DrewBaby);
                if (row.Seps[i].gameObject.activeSelf != show) { row.Seps[i].gameObject.SetActive(show); }
            }
        }

        private const float StripBoxH = 4f;       // the health bar
        private const float StripStamH = 2f;      // the stamina bar under it, half the height
        private const float StripRimW = 0.7f;     // the rim's thickness, and a tick's

        // THE GAUGE ON A SHEET IS THE ROSTER ROW'S READING, WHICH MEANS BARE AND
        // COMPACT (D88, and it was neither at first). Two things were
        // wrong and both came from the run's old home:
        //
        // - It kept the WELL - "a sunk paper behind the whole strip" - which was
        //   right when the run rode the name plate, where a plate is what a name
        //   plate IS. On the sheet the well is a walnut slab, the darkest object
        //   in the window, sitting across the top of the page; the roster draws
        //   the identical instrument bare on parchment and reads better for it.
        // - It STRETCHED to the full width, so the bar took 40% of a 380-unit
        //   sheet and the icons floated off to the right with a hole between
        //   them. The bar and the icons are one instrument and have to stay
        //   together; a wider page should not pull them apart.
        //
        // `sheet` is what tells the two uses apart. The name plate's two calls
        // (Titles) want the old behaviour - they ARE a plate, and their width is
        // already the run's own - so they pass nothing.
        // Shrinking the run to a measure was tried on the way and reverted the
        // same pass: at 0.60 the bar and the icons both came out small and the
        // instrument read as an afterthought floating in the top-left. The slab
        // was the whole fault; the SIZE was never wrong. So the run keeps the
        // page's width and simply stops wearing a plate.
        private static void LayoutStrip(SheetRow row, SheetLine line, float width, float h,
            bool sheet = false)
        {
            if (sheet)
            {
                row.Bare = true;                       // no slab under the run
            }
            row.LevelSlot = false;        // a header's run has no room kept for the arrow
            float x0 = SheetPadX + 6f;    // in from the plate's hand-cut edge
            float barW = row.IconsOnly ? 0f : Mathf.Round(width * 0.4f);
            float barY = 5f;
            float stamY = barY + StripBoxH + StripRimW;   // the rim's own thickness parts the two bars
            float r = StripRimW;
            Theme.Place(row.BarRim, x0 - r, barY - r, barW + 2f * r, StripBoxH + r + StripStamH + 2f * r);
            Theme.Place(row.BarTrack, x0, barY, barW, StripBoxH);
            Theme.Place(row.BarFill, x0, barY, 0f, StripBoxH);
            PlaceTicks(row, line, x0, barW, barY);
            Theme.Place(row.Stam, x0, stamY, barW, StripStamH);
            Theme.Place(row.StamFill, x0, stamY, 0f, StripStamH);
            row.BarH = StripBoxH;
            row.StamH = StripStamH;
            Theme.Place(row.Value, 0f, 0f, 0f, h);   // no number: the bar and its ticks say it
            // the well: a sunk paper behind the whole strip, so the gauge reads as
            // embedded in the window rather than laid on it
            // the plate sits inset in the field (a subplate fused to the name plate
            // and a walnut band were both tried and reverted)
            Theme.Place(row.Well, 2f, 2f, width - 4f, h - 4f);
            // the subtitle under the bars, in smaller type
            float subY = stamY + StripStamH + r + 1f;
            Theme.Place(row.Label, x0, subY, width - 2f * SheetPadX, Mathf.Max(6f, h - subY - 1f));
            // the icons: two pairs and the baby, parted by hairlines; the pitch
            // comes from what is left, so a narrow column keeps every icon
            bool baby = line.Fracs != null && line.Fracs.Length > 7 && line.Fracs[7] >= 0f;
            // THE BABY'S SLOT IS ALWAYS RESERVED: kept as a placeholder and simply not
            // drawn, rather than shrinking the other icons when it appears. The pitch divides what is left
            // among the icons, so counting only the icons PRESENT made a pregnancy shrink
            // every other icon on the row - the run changed size for a reason that has
            // nothing to do with the icons that changed. Reserve the full set and the two
            // hairlines whatever this line carries; the baby is merely not drawn.
            const int icons = StripIcons;
            const int seps = 2;
            float iconsX = row.IconsOnly ? 0f : x0 + barW + 10f;
            float avail = width - SheetPadX - 10f - iconsX;
            float pitch = row.IconsOnly ? PlateIconPitch : Mathf.Clamp((avail - seps * 5f) / icons, 8f, 16f);
            float s = pitch - (row.IconsOnly ? 1f : 2f);
            float y = (h - s) * 0.5f;
            float x = iconsX;
            int sep = 0;
            for (int i = 0; i < StripIcons; i++)
            {
                if (i == StripIcons - 1 && !baby)
                {
                    break;
                }
                float k = StripIconScale[i];
                float si = s * k;
                if (i == 2 || i == 5)
                {
                    if (row.IconsOnly)
                    {
                        // ON THE PLATE the run is spaced by each icon's DRAWN width
                        // (the big pear and drop sat closer than
                        // the small face and heart), so a hairline is centred by
                        // construction: the same air on both sides of it
                        // a hairline wants more air than an icon does; the drop before
                        // the first already has it
                        x += sep == 0 ? 0f : PlateSepExtra;
                        Theme.Place(row.Seps[sep], x, (h - (s + 2f)) * 0.5f, StripSepW, s + 2f);
                        x += StripSepW + PlateIconAir + PlateSepExtra;
                    }
                    else
                    {
                        Theme.Place(row.Seps[sep], x - 0.1f, (h - (s + 2f)) * 0.5f, StripSepW, s + 2f);
                        x += 5f;
                    }
                    sep++;
                }
                float vi = si * StripIconInk[i];   // the ink's width on the plate
                float ix = row.IconsOnly ? x - (si - vi) * 0.5f : x + (s - si) * 0.5f;
                float iy = y + (s - si) * 0.5f;
                // the contour: the same silhouette a little larger, in the rim's ink
                float rim = 0.7f;   // a thinner, softer contour
                Theme.Place(row.IconRims[i], ix - rim, iy - rim, si + 2f * rim, si + 2f * rim);
                Theme.Place(row.IconBacks[i], ix, iy, si, si);
                Theme.Place(row.IconFills[i], ix, iy, si, si);
                if (i == LoveIcon)
                {
                    Theme.Place(row.LoveMid, ix, iy, si, si);
                    Theme.Place(row.LoveTop, ix, iy, si, si);
                }
                x += row.IconsOnly ? vi + PlateIconAir : pitch;
            }
            row.TrackX = x0;
            row.TrackW = barW;
            row.DrewBaby = baby;
            ShowStrip(row, true);
        }

        private static void PaintStrip(SheetRow row, SheetLine line)
        {
            bool baby = line.Fracs != null && line.Fracs.Length > 7 && line.Fracs[7] >= 0f;
            if (!Mathf.Approximately(row.DrewMax, line.Max) || row.DrewBaby != baby)
            {
                // the SHAPE changed - a level raised the max, a pregnancy began -
                // and only Bind lays boxes out
                _forceRefresh = true;
                _nextRefresh = 0f;
                return;
            }
            if (!row.IconsOnly)
            {
                var hr = row.BarFill.rectTransform;
                hr.sizeDelta = new Vector2(Mathf.Clamp01(line.Fracs[0]) * row.TrackW, row.BarH);
                row.BarFill.color = LifeTint;
                var sr = row.StamFill.rectTransform;
                sr.sizeDelta = new Vector2(Mathf.Clamp01(line.Fracs[1]) * row.TrackW, row.StamH);
                row.StamFill.color = StunTint;
            }
            for (int i = 0; i < StripIcons; i++)
            {
                if (!row.IconFills[i].gameObject.activeSelf)
                {
                    continue;
                }
                Sprite sprite = line.Icons != null && i < line.Icons.Length ? line.Icons[i] : null;
                // the track and the rim take the MASK when the icon has one: a flat
                // silhouette, so the unfilled part is the same parchment tan every other
                // icon shows, and the whole glyph - outer and inner - follows the fill
                Sprite mask = line.Masks != null && i < line.Masks.Length && line.Masks[i] != null ? line.Masks[i] : sprite;
                if (row.IconFills[i].sprite != sprite)
                {
                    row.IconFills[i].sprite = sprite;
                }
                if (row.IconBacks[i].sprite != mask)
                {
                    row.IconBacks[i].sprite = mask;
                    row.IconRims[i].sprite = mask;
                }
                row.IconFills[i].fillAmount = Mathf.Clamp01(line.Fracs[2 + i]);
                row.IconFills[i].color = line.Tints[2 + i];
                row.IconBacks[i].color = StripTrack;
                row.IconRims[i].color = new Color(StripRim.r, StripRim.g, StripRim.b, 0.75f);
                row.IconRims[i].enabled = StripIconRim[i];
                if (i != LoveIcon)
                {
                    continue;
                }
                bool banded = line.LoveBands != null && line.LoveBands.Length > 1;
                PaintLoveBand(row.LoveMid, sprite, banded, line, 0);
                PaintLoveBand(row.LoveTop, sprite, banded, line, 1);
            }
            row.LevelTip = line.LevelTip;
            ShowLevelUp(row, row.LevelSlot && line.LevelUp);
        }

        // THE LEVEL-UP ARROW (PointsTag; an icon on the target indicator, extending
        // the current emojis): shown while
        // the person has attribute points that buy something, after its own hairline
        private static void ShowLevelUp(SheetRow row, bool on)
        {
            if (row.LevelIcon == null)
            {
                return;
            }
            if (row.LevelIcon.gameObject.activeSelf != on) { row.LevelIcon.gameObject.SetActive(on); }
            if (row.LevelSep.gameObject.activeSelf != on) { row.LevelSep.gameObject.SetActive(on); }
        }

        private const float LevelIconScale = 1.05f;   // an arrow's ink is thin: this draws it as tall as the face

        // one band over the heart's own fill: pink at 0, pale at 1 - drawn in that
        // order, so the smaller reach sits on top and the three read as bands
        private static void PaintLoveBand(Image band, Sprite sprite, bool on, SheetLine line, int i)
        {
            if (band.gameObject.activeSelf != on) { band.gameObject.SetActive(on); }
            if (!on) { return; }
            if (band.sprite != sprite) { band.sprite = sprite; }
            band.fillAmount = Mathf.Clamp01(line.LoveBands[i]);
            band.color = line.LoveBandTints[i];
        }

        private const int LoveIcon = 3;   // food · water | moral · LOVE · libido | baby

        // The header's two lines: who they are, and how they are (§6.21)
        private static void WriteHeader(List<SheetLine> lines, CommonStates who)
        {
            lines.Add(StripLine(who));
        }

        // THE GAUGE, ONCE (D55: "the roster row, the column header and the plate
        // row are ONE component at three sizes"). The header takes it as it is;
        // the roster row takes it and adds a name line and the marks around it.
        // One writer, so a gauge cannot mean one thing on the plate and another
        // on the row.
        private static SheetLine StripLine(CommonStates who)
        {
            // no text on the gauge for now; the level's place is open
            string ident = string.Empty;
            GameManager gm = _mn != null ? _mn.gameMN : null;
            bool pregnant = who.pregnant != null && who.pregnant.Length > 1 && who.pregnant[0] != -1;
            float months = pregnant ? 12f - who.pregnant[1] : -1f;
            Color moral = MoralTint(who);
            bool content = who.moral > 0f && (who.debuff == null || who.debuff.discontent == 0);
            Sprite face = Theme.Kit(content ? "kit_smile" : "kit_frown", 0) ?? MoralFace(content);
            bool flameTinted;
            Sprite flame = LibidoIcon(out flameTinted);
            Color flameTint = flameTinted ? LibidoTint : Color.white;
            // the pink flame is drawn with its two tones baked in, so it needs a flat
            // mask of the same silhouette behind it; kit_flame IS that mask. A flame
            // we tint ourselves is already flat, so it is its own track.
            Sprite flameMask = flameTinted ? null : Theme.Kit("kit_flame", 0);
            // their love for you: the one edge the game reads (§6.20), on the
            // Relations tab's scale
            float love = 0f;
            CommonStates me = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            bool known = me != null && _mn.npcMN != null && LoveBetween(_mn.npcMN, who, me, out love);
            Sprite heart = Theme.Kit(known && love < 0f ? "kit_heart_broken" : "kit_heart", 0) ?? Emotion(0);
            // ONE COLOUR ON THE ICON: the heart in the gauge and in the roster is a
            // solid colour, reversing D57's bands. An
            // icon has one job here - it is eight units tall and says HOW MUCH by how
            // far it is filled - and three pinks stacked inside that height read as
            // texture rather than as classes. The Relations BAR keeps all three:
            // there the bands
            // run along a track long enough to tell them apart, which is what made
            // them worth having.
            //
            // The middle of the three, not the palest: the fill has to hold against
            // the tan track behind it and stay apart from the flame two icons along,
            // and #EEACCD does neither at this size.
            Color heartTint = LoveWidget.Pink;
            float loveFrac = known ? Mathf.Clamp01(Mathf.Abs(love) / 100f) : 0f;
            // (the two band images the strip still carries are parked, not deleted:
            // PaintStrip hides them while these stay null, and D57 is one line away)
            float[] loveBands = null;
            Color[] loveBandTints = null;
            Sprite babe = Emotion(7) ?? Theme.Kit("kit_dot", 0);
            bool levelUp = AttributesToSpend(who);
            return new SheetLine
            {
                Kind = LineStrip, Enabled = true, Label = ident, LabelTinted = true, LabelTint = Theme.KitCream,
                LevelUp = levelUp,
                LevelPoints = who.statusPoint,
                LevelTip = levelUp ? AttributeTip(who) : null,
                Cur = (float)who.life, Max = (float)Mathf.Max(1f, (float)who.maxLife),
                Value = string.Empty,
                Fracs = new[]
                {
                    who.maxLife > 0.0 ? (float)(who.life / who.maxLife) : 0f,
                    who.maxFaint > 0.0 ? (float)(who.faint / who.maxFaint) : 0f,
                    who.maxFood > 0f ? who.food / who.maxFood : 0f,
                    who.maxWater > 0f ? who.water / who.maxWater : 0f,
                    Mathf.Abs(who.moral) / 100f,
                    loveFrac,
                    who.maxLibido > 0f ? who.libido / who.maxLibido : 0f,
                    pregnant ? months / 12f : -1f,
                },
                LoveBands = loveBands,
                LoveBandTints = loveBandTints,
                Tints = new[]
                {
                    LifeTint, StunTint, FoodTint, WaterTint,
                    face == _faceGood || face == _faceBad ? Color.white : moral,   // the kit's face takes the roster's colour; a drawn one keeps its own
                    heartTint,
                    flameTint,
                    babe == Emotion(7) ? Color.white : BabyTint,
                },
                Masks = new[] { null, null, null, null, flameMask, null },
                BarTip = "HP  " + who.life.ToString("0") + " / " + who.maxLife.ToString("0")
                    + "\nStun  " + who.faint.ToString("0") + " / " + who.maxFaint.ToString("0"),
                IconTips = new[]
                {
                    Tip("Food", who.food, who.maxFood),
                    Tip("Water", who.water, who.maxWater),
                    Tip("Moral", who.moral, 100f),
                    // love has NO ceiling - nothing clamps CommonStates.Lovers.love, and
                    // the Relations track already runs to 2000 - so it is stated bare.
                    // Moral keeps its "/ 100": that one really is clamped, to ±100
                    // (CommonStates.MoralChange), so the top value is a fact there
                    // WHOSE love, and for whom: the one icon on the run that states a
                    // relation rather than a condition, and the game keeps only this
                    // direction (§6.20). The plate carries their name already, so the
                    // tip names the other end
                    known ? "Love for you\n" + love.ToString("0") : "Love for you\nnot met",
                    Tip("Libido", who.libido, who.maxLibido),
                    pregnant ? "Pregnant\n" + months.ToString("0") + " / 12 months" : string.Empty,
                },
                Icons = new[]
                {
                    gm != null && gm.foodImage != null ? gm.foodImage.sprite : null,
                    gm != null && gm.waterImage != null ? gm.waterImage.sprite : null,
                    face, heart, flame, babe,
                },
            };
        }

        // THE FLAME IS LIBIDO, ON EVERY SURFACE. The Profile sheet drew the game's
        // heart here (Emotion(0)) - the same glyph the Love row directly above it
        // wears - so the sheet said "heart" twice while the header run beside it
        // said flame for libido and heart for love. One reader for both, the
        // same rule D55 applies to the gauge itself: an icon cannot mean one thing
        // on the run and another on the row it explains.
        //
        // `tinted` says whether the caller must colour it: the pink flame carries
        // its two tones already, the plain one is a mask and takes LibidoTint.
        private static Sprite LibidoIcon(out bool tinted)
        {
            Sprite pink = Theme.Kit("kit_flame_pink", 0);
            if (pink != null)
            {
                tinted = false;
                return pink;
            }
            tinted = true;
            return Theme.Kit("kit_flame", 0) ?? Theme.Kit("kit_dot", 0);
        }

        // A BOX IS A HUNDRED OF MAX, AND THERE ARE AS MANY AS THE MAX HAS. On high
        // hitpoint NPCs the section indicators vanished part-way along the bar, and
        // a Giant (2000 max hp) showed 10 boxes where it should have 20.
        //
        // The box is a UNIT, not a division: reading a bar here is counting hundreds,
        // so twenty boxes is what two thousand looks like and nothing may be traded
        // away to keep the count small. The first bug was a count capped at ten with
        // the step left at a hundred, which drew the first nine marks and left the
        // rest of the bar bare - the bar lying about its own scale. The second was the
        // first fix for it: a step that climbed a ladder to keep the count under the cap,
        // which kept the marks spanning the bar and cost them their meaning. Both
        // were the cap. There is no cap.
        //
        // What the bar cannot do is draw a mark thinner than the gap beside it. Below
        // MinBoxW the ticks are dropped whole rather than smeared into a band: a
        // plain bar says less than a comb, but it does not say anything false. That
        // is roughly 2600 of max on a roster row and 5000 on the plate - past every
        // follower, and the day a boss passes it the bar still reads.
        private const float BoxWorth = 100f;   // what one stretch of the bar is worth
        private const float MinBoxW = 2f;      // ...and the narrowest it may be drawn, in units
        // A RULER, NOT A COMB: a special case for hitpoints over 2000, after what
        // other games do (Teamfight Tactics among them). League's bar - which TFT's is - notches every hundred
        // on a fixed width and simply lets a tank's read as a fine comb; it does not
        // solve this, it accepts it. Where UI does solve it, the answer is almost
        // always the ruler's: a thin mark for the small unit, a heavy one for the
        // large, and when the small marks stop fitting they are dropped and the
        // heavy ones carry on.
        //
        // Taken here because it is the only method that leaves the rule alone. A
        // heavy mark is a THOUSAND at every size and a thin mark is a HUNDRED
        // whenever it is drawn at all, so nothing the bar shows can ever be false -
        // which is what the step ladder got wrong. It also invents no threshold:
        // 1000 comes from the same place 100 does, and there is no magic 2000
        // anywhere. Past the point where the hundreds stop fitting (~2600 of max on
        // a roster row) the bar coarsens to thousands rather than going bare, so
        // "this one is five thousand" still reads at a glance.
        private const float MajorWorth = 1000f;
        private const float MajorW = 1.6f;     // the heavy mark's thickness, against the thin one's 0.7
        private static readonly Color StripMajor = new Color(0.06f, 0.04f, 0.03f, 1f);

        private static void PlaceTicks(SheetRow row, SheetLine line, float x0, float barW, float barY,
            float boxH = StripBoxH, float k = 1f)
        {
            float max = Mathf.Max(1f, line.Max);
            row.DrewTicks = PlaceMarks(row, ref row.Ticks, "Tick", StripTick, StripRimW * k,
                BoxWorth, max, x0, barW, barY, boxH);
            row.DrewMajors = PlaceMarks(row, ref row.Majors, "Major", StripMajor, MajorW * k,
                MajorWorth, max, x0, barW, barY, boxH);
            row.DrewMax = line.Max;
        }

        // One rank of the ruler: a mark every `worth` of max, as many as the max has,
        // dropped whole when they would be closer together than they are wide.
        private static int PlaceMarks(SheetRow row, ref Image[] pool, string name, Color ink,
            float thickness, float worth, float max, float x0, float barW, float barY, float boxH)
        {
            int units = Mathf.Max(1, Mathf.CeilToInt(max / worth));
            if (barW <= 0f || barW / units < MinBoxW)
            {
                return 0;
            }
            int marks = units - 1;
            EnsureMarks(row, ref pool, name, ink, marks);
            for (int k = 1; k <= marks; k++)
            {
                float tx = x0 + barW * (worth * k) / max;
                Theme.Place(pool[k - 1], tx - thickness * 0.5f, barY, thickness, boxH);
            }
            return marks;
        }

        // The mark images grow to the bar's need. Ten is only what a row starts with
        // - it was the cap once, and the cap is what was wrong.
        private static void EnsureMarks(SheetRow row, ref Image[] pool, string name, Color ink, int need)
        {
            if (pool != null && pool.Length >= need)
            {
                return;
            }
            var grown = new Image[need];
            int had = pool != null ? pool.Length : 0;
            if (had > 0)
            {
                System.Array.Copy(pool, grown, had);
            }
            for (int i = had; i < need; i++)
            {
                grown[i] = PlainImage(row.Go.transform, name + i, ink);
            }
            pool = grown;
        }

        // one icon's readout: its name over its number, the same two lines every
        // icon gives, so the run reads the same way whichever one is under the
        // pointer
        private static string Tip(string label, double value, double max)
        {
            return label + "\n" + value.ToString("0") + " / " + max.ToString("0");
        }

        // A face for moral: the game's own emotion sprite when one is named for
        // it, else the kit's disc in the roster's colour. The set's names are
        // logged once, so the right indices can be pinned after a look.
        private static Sprite _moralFallback;
        private static Sprite _faceGood;
        private static Sprite _faceBad;
        private static bool _facesLooked;

        private static Sprite MoralFace(bool good)
        {
            if (!_facesLooked)
            {
                _facesLooked = true;
                _moralFallback = Theme.Kit("kit_dot", 0);
                Sprite[] set = _mn != null && _mn.fxMN != null ? _mn.fxMN.emotionImages : null;
                var names = new System.Text.StringBuilder();
                for (int i = 0; set != null && i < set.Length; i++)
                {
                    string n = set[i] != null ? set[i].name : "null";
                    names.Append(i).Append('=').Append(n).Append(' ');
                    string l = n.ToLowerInvariant();
                    if (_faceGood == null && (l.Contains("smile") || l.Contains("happy") || l.Contains("joy") || l.Contains("laugh")))
                    {
                        _faceGood = set[i];
                    }
                    if (_faceBad == null && (l.Contains("angry") || l.Contains("anger") || l.Contains("sad") || l.Contains("cry")))
                    {
                        _faceBad = set[i];
                    }
                }
                Plugin.Log.LogInfo("Header: emotion sprites " + names);
            }
            Sprite pick = good ? _faceGood : _faceBad;
            return pick ?? _moralFallback;
        }
    }
}
