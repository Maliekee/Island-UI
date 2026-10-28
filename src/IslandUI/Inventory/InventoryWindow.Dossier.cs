using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — THE DOSSIER: your character sheet and a
    // target's, drawn as a printed form in kraft folders.
    //
    // The sage tint was meant to make your sheet stand out and read as toothpaste;
    // the goal was to make it stand out, not necessarily by colour - by shape, by
    // reading as a sheet. Of six drawn vessels the printed form was taken for the
    // pages and the dossier for the frame, for both sheets;
    // a target's is paperclipped, yours unmarked (the PERSONAL stamp came off).
    //
    // Look 04 (Forgejo #146, art/ref/window-facelift-v1/sheets) took the kraft off:
    // a slim pale clipboard behind ivory paper, cream tabs, a light close key -
    // pieces cut from the approved proof,
    // placed where the proof has them.
    //
    // Three layers, each its own part of the window:
    //   the FRAME  - the group frame's back is the clipboard board, its field the
    //                sheet (paper with the printed double border baked in);
    //   the TABS   - DossierTabs draws the sheets stacked behind and every page's tab;
    //   the FORM   - the pages' rows: the form's own kinds (a header, sections,
    //                boxes, checkboxes, fill-in fields, printed buttons) drawn from
    //                per-row pools, and the kinds it shares with the plain sheet
    //                (relations, dropdowns, sliders) restyled in place.
    internal static partial class InventoryWindow
    {
        // ---------- which sheets are dossiers ----------

        // Your sheet, and a PERSON's: the target group also frames a thing's
        // window (a chest, a well), which is not a character and not a sheet.
        private static bool IsDossier(int group)
        {
            return group == GroupOwnSheet || (group == GroupCharacter && _target is CommonStates);
        }

        private static bool FormRow(SheetRow row)
        {
            Plate p = row.Host;
            return p != null && p.IsSheet && !p.IsHeader && !p.IsRoster && p.Prop == null && IsDossier(p.Group);
        }

        // the kinds the form draws whole; the rest keep the plain row and gain extras
        private static bool FormOwns(int kind)
        {
            return kind == LineFormHead || kind == LineSection || kind == LineBoxes || kind == LineChecks
                || kind == LineField || kind == LineActions || kind == LineMore || RecordKind(kind)
                || kind == LineMoodNow || kind == LineMoodCause || kind == LineMoodCaps
                || kind == LineMetHead || kind == LineMetPerson || kind == LineMetFold || kind == LineMetWhy || kind == LineBond || kind == LineFormBand
                || OrdersKind(kind);
        }

        // ---------- the frame ----------

        // The proof's pixels, 1:1 at 1440p (Theme.KitPpu screen pixels a unit). The
        // board is the frame rect; the paper sits in it 9.2 px from the left (the
        // frame's left edge falls between pixels, its right edge on one), 11 from the
        // right, 13 from the top and 17.4 from the foot, the board's lip and its
        // backing showing round it.
        private const float SheetPx = 1f / Theme.KitPpu;
        private const string SheetBoard = "kit_sheet_board";
        private const string SheetPaper = "kit_sheet_paper";
        internal const string SheetCloseFace = "kit_sheet_close";
        private static readonly Vector4 SheetBoardBorder = new Vector4(9f, 18f, 11f, 14f);   // left, bottom, right, top
        private static readonly Vector4 SheetPaperBorder = new Vector4(20f, 17f, 17f, 19f);
        private const float PaperLeft = 9.2f * SheetPx;
        private const float PaperRight = 11f * SheetPx;
        private const float PaperTop = 13f * SheetPx;
        private const float PaperFoot = 17.4f * SheetPx;
        // the paper's printed double rule ends 17 px in from its top edge and 16 from
        // its foot (kit_sheet_paper); the page stands a unit clear of it (two
        // overflowed the Character page by a unit, so it scrolled)
        private const float DossierPadTop = PaperTop + 17f * SheetPx + 1f;
        private const float DossierPadFoot = PaperFoot + 16f * SheetPx + 1f;
        // the close key: 44 px, its centre 32 px in from the right as every close
        // is, and 16 above the frame's top, on the stacked sheets beside the tabs
        private const float SheetCloseSize = 44f * SheetPx;
        private const float SheetCloseRise = 16f * SheetPx;
        private const float ClipW = 6.5f;
        private const float ClipH = 20f;
        // the relations matrix (Relations.cs) still wears the kraft folder and its
        // paper, 3.5 units in: its own window, its own ticket
        private const float DossierPaperInset = 2.25f;

        private static void SkinDossierFrame(Plate plate, int group)
        {
            plate.Back.sprite = Theme.Kit(SheetBoard, SheetBoardBorder);
            plate.Back.type = Image.Type.Sliced;
            plate.Back.pixelsPerUnitMultiplier = Theme.KitPpu;
            plate.Back.color = Color.white;
            plate.Edge.enabled = false;
            EnsureField(plate);
            Theme.KitSkin(plate.Field, SheetPaper, SheetPaperBorder);
            plate.Field.color = Color.white;
            if (!plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(true);
            }
            EnsureRail(plate);
            plate.Rail.enabled = false;
            EnsureClose(plate, SheetCloseRise, SheetCloseSize);
            if (!plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(true);
            }
            // THEIRS IS PAPERCLIPPED. Yours wore a PERSONAL stamp for a while,
            // and carries no mark now: the name and the portrait say whose it is.
            // Look 04 is yours alone so far; a target's clip stays until its own
            // proof says otherwise.
            bool clipped = group != GroupOwnSheet;
            if (plate.Mark == null && clipped)
            {
                plate.Mark = Theme.KitImage(plate.Window.Go.transform, "Mark", "kit_dossier_clip", 0, Color.white);
                var r = plate.Mark.rectTransform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = Vector2.zero;
                plate.Mark.preserveAspect = true;
            }
            if (plate.Mark != null && plate.Mark.gameObject.activeSelf != clipped)
            {
                plate.Mark.gameObject.SetActive(clipped);
            }
            if (clipped)
            {
                plate.Mark.transform.SetAsLastSibling();
            }
            plate.Close.transform.SetAsLastSibling();
        }

        // Once the frame has its size: the paper on the board, from the frame's top
        // right (both on whole pixels, so the paper's pixels land on the screen's),
        // and a target's paperclip over its top edge, in the gap between the last
        // tab and the close chip
        private static void PlaceDossierFrame(Plate frame, int group)
        {
            Vector2 size = frame.Window.Rect.sizeDelta;
            if (frame.Field != null)
            {
                var fr = (RectTransform)frame.Field.transform;
                fr.anchorMin = fr.anchorMax = Vector2.one;
                fr.pivot = Vector2.one;
                fr.anchoredPosition = new Vector2(-PaperRight, -PaperTop);
                fr.sizeDelta = new Vector2(Mathf.Max(0f, size.x - PaperLeft - PaperRight),
                    Mathf.Max(0f, size.y - PaperTop - PaperFoot));
            }
            if (frame.Mark == null || group == GroupOwnSheet)
            {
                return;
            }
            var mr = frame.Mark.rectTransform;
            mr.sizeDelta = new Vector2(ClipW, ClipH);
            mr.anchoredPosition = new Vector2(size.x - 42f - DockReserve(group), size.y - ClipH + 6f);
        }

        private static void PlaceDossierTabs(int group, Plate frame, bool show)
        {
            DossierTabs row = _dossierTabs[group];
            if (!show || frame == null)
            {
                if (row != null && row.Rect.gameObject.activeSelf)
                {
                    row.Rect.gameObject.SetActive(false);
                }
                return;
            }
            if (row == null)
            {
                row = DossierTabs.Create(_root.transform);
                int g = group;
                row.OnPick = index => PickAspect(g, index);
                _dossierTabs[group] = row;
            }
            Aspect[] set = _footSets[group] ?? OwnAspects;
            var icons = new Sprite[set.Length];
            var labels = new string[set.Length];
            var tags = new string[set.Length];
            var kinds = new PointsTag.Kind[set.Length];
            int active = 0;
            Aspect current = AspectOf(group);
            CommonStates who = _dossierWho[group];
            for (int i = 0; i < set.Length; i++)
            {
                icons[i] = AspectIcon(set[i]);
                labels[i] = SheetTabLabel(set[i]);
                if (set[i] == current)
                {
                    active = i;
                }
                // the points each page has to spend: attributes on Character, the
                // skill book's on Skills - yours alone, so only on your own sheet
                if (set[i] == Aspect.Profile && AttributesToSpend(who))
                {
                    tags[i] = who.statusPoint.ToString();
                    kinds[i] = PointsTag.Kind.Attribute;
                }
                else if (set[i] == Aspect.Skills && ReferenceEquals(who, You()) && SkillsToSpend(out int sp))
                {
                    tags[i] = sp.ToString();
                    kinds[i] = PointsTag.Kind.Skill;
                }
            }
            int c = frame.Column;
            Vector2 size = frame.Window.Rect.sizeDelta;
            float frameLeft = (_colPivotX[c] > 0.5f ? _colX[c] - size.x : _colX[c]) + FloatShiftX(group);
            row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(_colAnchorX[c], 0f);
            row.Rect.pivot = Vector2.zero;
            row.Rect.anchoredPosition = new Vector2(frameLeft, _groupHi[group]);
            // ...and beside the close, room for the dock chip (Float.cs)
            row.Set(icons, labels, active, size.x, size.y, tags, kinds, DossierTabs.Reserve + DockReserve(group));
            if (!row.Rect.gameObject.activeSelf)
            {
                row.Rect.gameObject.SetActive(true);
            }
            row.Rect.SetAsFirstSibling();
        }

        // ---------- the pages ----------

        private const int BoxNone = 0;
        private const int BoxStat = 1;
        private const int BoxSkill = 2;
        private const int CheckOption = 1000;   // a checkbox's code at and above this is an option flag
        private const int GlyphCheck = 0;
        private const int GlyphEye = 1;
        private const int GlyphStar = 2;
        private const int GlyphRun = 3;
        private const int StyleVital = 0;       // caption, reading, a bar
        private const int StyleAttribute = 1;   // the big number, a foot with its +
        private const int StyleCount = 2;       // a count, centred
        private const int StyleSkill = 3;       // glyph, name, level and pips, a foot with its +
        private const int StyleReading = 4;     // caption, reading

        private static readonly List<SheetLine> _formScratch = new List<SheetLine>();

        private static bool WriteDossier(List<SheetLine> lines, CommonStates who, Aspect aspect, bool own)
        {
            switch (aspect)
            {
                case Aspect.Profile:
                    WriteFormCharacter(lines, who, own);
                    break;
                case Aspect.Relations:
                    WriteFormSocial(lines, who, own);
                    break;
                case Aspect.Skills:
                    WriteFormSkills(lines, who);
                    break;
                case Aspect.Orders:
                    WriteFormOrders(lines, who);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static SheetLine Section(string title, string pill = null)
        {
            return new SheetLine { Kind = LineSection, Label = title, Value = pill ?? string.Empty, Enabled = true };
        }

        // an attribute box's caption, written in capitals as the language writes a heading
        private static string StatCaption(int i)
        {
            switch (i)
            {
                case 0: return Shared.Loc.Get("dossier.attr.health", "HEALTH");
                case 1: return Shared.Loc.Get("dossier.attr.strength", "STRENGTH");
                case 2: return Shared.Loc.Get("dossier.speed", "SPEED");
                default: return StatNames[i].ToUpperInvariant();
            }
        }

        // an PERSONALITY type's tip below its title: the "Near one" row, then the door
        private static string PersonalityNear(string worth)
        {
            return Shared.Loc.Get("dossier.personality.near_one", "Near one") + "  "
                + Shared.Loc.Get("dossier.personality.a_tick", "{0} a tick", worth) + "\n"
                + Shared.Loc.Get("dossier.personality.click", "Click for everyone who is");
        }

        private static SheetLine Field(string label, string value, string label2 = null, string value2 = null)
        {
            return new SheetLine
            {
                Kind = LineField, Label = label, Value = value ?? string.Empty,
                Label2 = label2 ?? string.Empty, Value2 = value2 ?? string.Empty, Enabled = true,
            };
        }

        // WHO THEY ARE, AND WHAT THEY ARE MADE OF: the header, then yours alone -
        // how you are right now (a target's is on the unit under their sheet) - then
        // the settings that are about you, what your points buy, what you hit with,
        // and the facts.
        private static void WriteFormCharacter(List<SheetLine> lines, CommonStates who, bool own)
        {
            TextManager tm = GameWords.Text(_mn);
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            string kind = KindName(npcs, who);
            var captions = new List<string>();
            var values = new List<string>();
            var fracs = new List<float>();
            var tips = new List<string>();
            // A STORY CHARACTER HAS NO KIND OR AGE TO TELL: the game's friends list
            // puts them in one colour group, the orange one (UIManager.GetNPCColorID
            // 5: npcID 2-8, 71, 110, 111, 113-118, 121, 162, 163), and in it every
            // kind is the character's own name and every age 0 - the game never
            // assigns them one (NPCManager gives ages to generated kinds only;
            // read from a save: Reika, Takumi, Cassie, Merry... all
            // kind = name, age 0). So their head prints no age - but the kind, even
            // where it is their name ("Cassie" / "Cassie"): the head keeps one
            // layout for everyone.
            // ...and the same for the two main characters, Man and Yona (npcID 0
            // and 1), on your sheet or theirs: no kind but themselves, age 0
            bool story = who.npcID == 0 || who.npcID == 1
                || (_mn != null && _mn.uiMN != null && _mn.uiMN.GetNPCColorID(who.npcID) == StoryColorGroup);
            if (!own && !string.IsNullOrEmpty(kind))
            {
                // the same width with or without an age beside it: the head keeps one layout
                captions.Add(Shared.Loc.Get("dossier.kind", "KIND")); values.Add(kind); fracs.Add(0.8f); tips.Add(null);
            }
            if (!story)
            {
                // AN AGE OF 0 IS NO AGE: the game assigns ages to generated kinds
                // only, and leaves the rest - Type-A to C, mummies, Bigfoot, native
                // boys, underground women - at 0, so their blank is left out
                bool aged = who.age > 0;
                // the age is two digits: a narrow blank, so a long kind keeps its size
                if (aged)
                {
                    captions.Add(Shared.Loc.Get("dossier.age", "AGE")); values.Add(Observe.Sees(who, Observe.Age) ? who.age.ToString() : Observe.Unknown); fracs.Add(captions.Count > 1 ? 0.2f : 0.5f); tips.Add(null);
                }
            }
            // the fields' tips, then the medallion's and the exp's; the marks' and
            // the stamp's follow below
            tips.Add(TipLevel);
            tips.Add(own ? TipExpOwn : TipExpTheirs);
            string name = !string.IsNullOrEmpty(who.charaName) ? who.charaName : (kind ?? "?");
            // A STRANGER'S SHEET IS WHAT THE OBSERVATION SKILL TELLS YOU (Observe.cs):
            // the level, the age and the type one rank at a time, a "???" where it
            // has not; their exp, attributes, combat and personality never
            bool known = Observe.Known(who);
            var head = new SheetLine
            {
                Kind = LineFormHead, Label = name, Label2 = Shared.Loc.Get("dossier.level", "LEVEL"),
                Value = Observe.Sees(who, Observe.Level) ? who.level.ToString() : Observe.UnknownShort,
                Value2 = known ? who.exp.ToString("0") + " / " + who.maxExp.ToString("0") : Observe.Unknown,
                Frac = known && who.maxExp > 0 ? (float)(who.exp / who.maxExp) : 0f,
                Icon = AspectIcon(Aspect.Profile), Enabled = true,
                LabelTint = Theme.DossierInk,
                // NO TYPE ON THE PHOTO (user): the token stuck on its corner is
                // gone, and none leads the name - PERSONALITY marks their type
                Index = -1,
                Style = -1,
            };
            // WHAT YOU HAVE SET ABOUT THEM, AS MARKS ON THE HEAD: the eye in the
            // name's caption, the favourite a star stuck on the photo's corner.
            // As a row of checkboxes they cost the page a line and read like
            // settings. The main characters have no favourite star in vanilla
            // (UIManager hides favButton for npcID 0 and 1), and the other one
            // reaches this sheet from our roster now (Partner). Your run is the
            // SPEED caption's, under COMBAT (Forgejo #134).
            if (!own && (who.employ == CommonStates.Employ.Friend || who.employ == CommonStates.Employ.Favorite)
                && who.npcID > 1)
            {
                head.Cans = new[] { who.nameVisible != null, who.employ == CommonStates.Employ.Favorite };
                head.Args = new[] { ToggleNameShown, ToggleFavourite };
                head.Glyphs = new[] { GlyphEye, GlyphStar };
                tips.Add(TipShowName); tips.Add(TipFavourite);
            }
            else
            {
                head.Cans = new[] { who.nameVisible != null };
                head.Args = new[] { ToggleNameShown };
                head.Glyphs = new[] { GlyphEye };
                tips.Add(own ? TipShowNameOwn : TipShowName);
            }
            // A PREGNANCY IS A STAMP on the photo: occasional and significant, so
            // it is the one thing on the form in red, and it takes no line - as a
            // vitals box it was a row of its own with two places empty
            if (who.pregnant != null && who.pregnant.Length > 1 && who.pregnant[0] != -1)
            {
                head.Baby = Mathf.Max(0.01f, (12f - who.pregnant[1]) / 12f);
                tips.Add(TipPregnant);
            }
            // THE NAME IS A DOOR: the plank after the marks renames (NameEdit),
            // its tip last of all; the type's token before it is a door to the
            // roster at that type's section, the plank after, its tip before last
            var headArgs = new List<int>(head.Args) { RenameCode };
            head.Args = headArgs.ToArray();
            tips.Add(TipRename);
            head.Options = captions.ToArray();
            head.Values = values.ToArray();
            head.Fracs = fracs.ToArray();
            head.Tips = tips.ToArray();
            lines.Add(head);

            WriteFormVitals(lines, who, own);
            if (!known)
            {
                return;
            }

            if (who.status != null && who.status.Length >= StatNames.Length)
            {
                SheetLine points = Section(Shared.Loc.Get("dossier.attributes", "ATTRIBUTES"), Shared.Loc.Plural("dossier.points", "{0} point", "{0} points", (int)who.statusPoint));
                points.Tips = new[] { TipAttributePoints };
                points.PointsKind = AttributesToSpend(who) ? 2 : 0;
                lines.Add(points);
                var caps = new string[StatNames.Length];
                var vals = new string[StatNames.Length];
                var foots = new string[StatNames.Length];
                var cans = new bool[StatNames.Length];
                var args = new int[StatNames.Length];
                var boxTips = new string[StatNames.Length];
                for (int i = 0; i < StatNames.Length; i++)
                {
                    int cost = StatCost(who, i);
                    caps[i] = StatCaption(i);
                    vals[i] = who.status[i].ToString();
                    foots[i] = Shared.Loc.Get("dossier.next_cost", "next {0} pt", cost);
                    cans[i] = who.statusPoint >= cost;
                    args[i] = i;
                    boxTips[i] = i < TipAttributes.Length ? TipAttributes[i] + TipAttributeCost : null;
                }
                lines.Add(new SheetLine
                {
                    Kind = LineBoxes, Style = StyleAttribute, Arg = BoxStat, Options = caps, Values = vals,
                    Foots = foots, Cans = cans, Args = args, Tips = boxTips, Enabled = true,
                });
            }

            lines.Add(Section(Shared.Loc.Get("dossier.combat", "COMBAT")));
            string attack = who.attack.ToString("F1");
            if (who.equip != null && who.equip.Length > 1 && who.equip[1] != null)
            {
                attack += " (" + who.equip[1].attack.ToString("0") + ")";
            }
            bool klass = who.status != null && who.status.Length > 7 && who.status[7] > 0;
            var combat = new SheetLine
            {
                Kind = LineBoxes, Style = StyleReading, Enabled = true,
                Options = klass
                    ? new[] { Shared.Loc.Get("dossier.attack", "ATTACK"), Shared.Loc.Get("dossier.guard", "GUARD"), Shared.Loc.Get("dossier.speed", "SPEED"), Shared.Loc.Get("dossier.class", "CLASS") }
                    : new[] { Shared.Loc.Get("dossier.attack", "ATTACK"), Shared.Loc.Get("dossier.guard", "GUARD"), Shared.Loc.Get("dossier.speed", "SPEED") },
                Values = klass
                    ? new[] { attack, GuardOf(who).ToString("0.0"), who.MoveSpeed(false).ToString("0.00"), who.status[7].ToString() }
                    : new[] { attack, GuardOf(who).ToString("0.0"), who.MoveSpeed(false).ToString("0.00") },
                Tips = klass
                    ? new[] { TipAttack, own ? TipGuardOwn : TipGuardTheirs, TipSpeed, TipClass }
                    : new[] { TipAttack, own ? TipGuardOwn : TipGuardTheirs, TipSpeed },
            };
            // YOUR RUN IS THE SPEED CAPTION'S (Forgejo #134): "SPEED · ALWAYS RUN"
            // with the game's green runner, "SPEED · WALK" with the figure faded,
            // and the words are the switch, as the name's are for SHOWN IN WORLD.
            // As a chip on the name line it sat on the one line that is about who
            // you are, not how you move.
            if (own)
            {
                int boxes = combat.Options.Length;
                combat.Glyphs = new int[boxes];
                combat.Cans = new bool[boxes];
                combat.Args = new int[boxes];
                combat.Glyphs[2] = GlyphRun;
                combat.Cans[2] = _mn != null && _mn.gameMN != null && _mn.gameMN.toggleRun;
                combat.Args[2] = ToggleRun;
                combat.Tips = new List<string>(combat.Tips) { TipAlwaysRunKey }.ToArray();
            }
            lines.Add(combat);

            // PERSONALITY's strip closes the page (Record.cs); the family and the
            // life's counts are the Social page's FAMILY and RECORD
            WriteFormPersonality(lines, who);
        }

        // a tip's words in PERSONALITY's green or red
        private static string Tinted(string text, Color c)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";
        }

        // a sentence tinted around its {0}, which keeps its own colours (a token)
        private static string TintedAround(Color c, string arg, System.Func<string, string> say)
        {
            string open = "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">";
            return open + say("</color>" + arg + open) + "</color>";
        }

        // the type token's tip: the type, which type they take to and which they
        // can't, with what a tick near one of them is worth; Likes and Dislikes
        // in PERSONALITY's green and red
        private static string TypeTip(CommonStates who)
        {
            int[] person = who.person;
            int type = person != null && person.Length > 0 ? person[0] : -1;
            int likes = person != null && person.Length > 1 ? person[1] : -1;
            int dislikes = person != null && person.Length > 2 ? person[2] : -1;
            var sb = new System.Text.StringBuilder("<b>").Append(TypeWithIcon(type)).Append("</b>");
            if (likes >= 0)
            {
                sb.Append('\n').Append(Tinted(Shared.Loc.Get("vitals.likes", "Likes"), TraitLike)).Append("  ")
                    .Append(Shared.Loc.Get("dossier.type.worth_near", "{0}, {1} near one", TypeWithIcon(likes), "+10"));
            }
            if (dislikes >= 0)
            {
                sb.Append('\n').Append(Tinted(Shared.Loc.Get("vitals.dislikes", "Dislikes"), TraitDislike)).Append("  ")
                    .Append(Shared.Loc.Get("dossier.type.worth_near", "{0}, {1} near one", TypeWithIcon(dislikes), "-5"));
            }
            if (likes >= 0 || dislikes >= 0)
            {
                sb.Append('\n').Append(Shared.Loc.Get("dossier.type.anyone_else", "Anyone else")).Append("  ")
                    .Append(Shared.Loc.Get("dossier.type.near_one", "{0} near one", "+5"));
            }
            return sb.Append("\n\n").Append(Shared.Loc.Get("dossier.type.click", "Click for everyone who is {0}", TypeText(type))).ToString();
        }

        // HOW THEY ARE: the six boxes, HP · stun · food over water · libido · moral,
        // theirs as well as yours (the indicator is there too, but a target's
        // vitals read like the player's). HP's and stun's bars carry the unit's
        // pool marks - a thin one per 100 of max, a heavy one per 1000 - and
        // nothing else about the boxes changed. A pregnancy is the head's stamp.
        // Their love for you is not here: it is the Social page's first row,
        // because it belongs to the relations.
        private static void WriteFormVitals(List<SheetLine> lines, CommonStates who, bool own)
        {
            lines.Add(Section(Shared.Loc.Get("dossier.vitals", "VITALS")));
            if (!Observe.Known(who))
            {
                // A STRANGER'S are the combat bar's: health and stun as bars, their
                // numbers and pool marks the Observation skill's; the needs never
                bool life = Observe.Sees(who, Observe.Life), faint = Observe.Sees(who, Observe.Faint);
                SheetLine bars = Boxes(StyleVital,
                    new[] { Shared.Loc.Get("dossier.hp", "HP"), Shared.Loc.Get("dossier.stun", "STUN") },
                    new[] { life ? who.life.ToString("0") + "/" + who.maxLife.ToString("0") : Observe.Unknown, faint ? who.faint.ToString("0") : Observe.Unknown },
                    new[] { Frac((float)who.life, (float)who.maxLife), Frac((float)who.faint, (float)who.maxFaint) },
                    new[] { LifeTint, StunTint });
                bars.Pools = new[] { life ? (float)who.maxLife : 0f, faint ? (float)who.maxFaint : 0f };
                bars.Tips = new[] { TipHpTheirs, TipStunTheirs };
                lines.Add(bars);
                return;
            }
            SheetLine first = Boxes(StyleVital,
                new[] { Shared.Loc.Get("dossier.hp", "HP"), Shared.Loc.Get("dossier.stun", "STUN"), Shared.Loc.Get("dossier.food", "FOOD") },
                new[] { who.life.ToString("0") + "/" + who.maxLife.ToString("0"), who.faint.ToString("0"), who.food.ToString("0") },
                new[] { Frac((float)who.life, (float)who.maxLife), Frac((float)who.faint, (float)who.maxFaint), Frac(who.food, who.maxFood) },
                new[] { LifeTint, StunTint, FoodTint });
            first.Pools = new[] { (float)who.maxLife, (float)who.maxFaint, 0f };
            first.Tips = own ? new[] { TipHpOwn, TipStunOwn, TipFoodOwn } : new[] { TipHpTheirs, TipStunTheirs, TipFoodTheirs };
            lines.Add(first);
            SheetLine second = Boxes(StyleVital,
                new[] { Shared.Loc.Get("dossier.water", "WATER"), Shared.Loc.Get("dossier.libido", "LIBIDO"), Shared.Loc.Get("dossier.moral", "MORAL") },
                new[] { who.water.ToString("0"), who.libido.ToString("0"), who.moral.ToString("0") },
                new[] { Frac(who.water, who.maxWater), Frac(who.libido, who.maxLibido), Mathf.Clamp01(Mathf.Abs(who.moral) / 100f) },
                new[] { WaterTint, LibidoTint, MoralTint(who) });
            // your libido: nothing in the game reads the player's, so no tip says it does
            second.Tips = own ? new[] { TipWaterOwn, null, TipMoralOwn } : new[] { TipWaterTheirs, TipLibidoTheirs, TipMoralTheirs };
            second.Warns = new[] { false, false, who.moral < 0f };
            lines.Add(second);
        }

        private static float Frac(float value, float max)
        {
            return max > 0f ? Mathf.Clamp01(value / max) : 0f;
        }

        private static SheetLine Boxes(int style, string[] captions, string[] values, float[] fracs, Color[] tints)
        {
            return new SheetLine
            {
                Kind = LineBoxes, Style = style, Options = captions, Values = values, Fracs = fracs, Tints = tints,
                Enabled = true,
            };
        }

        // WHO THEY MEET: the mood, the last day's meetings from the love ledger,
        // the bonds as one table, every relation, then the family - the same
        // rows again, theirs only - and at the foot the life's RECORD and its
        // PARTNERS (Record.cs). Whom they take to is the band's two marks.
        private static void WriteFormSocial(List<SheetLine> lines, CommonStates who, bool own)
        {
            // whose page this is, and the way back when a name led here (Links.cs)
            WriteFormBand(lines, who);
            // MOOD COMES FIRST: most of what drains a villager is who they live with,
            // and those are the people listed under it
            bool company = !own && who.friendID >= 0 && who.Employed();
            if (company)
            {
                // MOOD, the chart and a lane a cause under it, then FAMILY and
                // RELATIONS, a lane a person (Company.cs) - in
                // place of WHO THEY MEET, BONDS and ALL RELATIONS
                WriteMoodForm(lines, who, company: true);
            }
            // WHAT HAPPENED LEADS, NOT THE NUMBERS: the last day's meetings, from
            // the love ledger (MetToday.cs). The love bars said too little - most
            // values sit in the hundreds, and the rule reads two lines - so they
            // live behind All relations at the page's foot. You are in no one's
            // love tick, so your own page has none.
            if (!company && !own && who.friendID >= 0)
            {
                WriteMetToday(lines, who);
                WriteBonds(lines, who);
            }
            // FAMILY, parents and children as person rows, then every relation,
            // a section of its own (Bonds.cs) - where RELATIONS is, every
            // relation is a lane already
            if (!company)
            {
                WriteFamily(lines, who);
                WriteAllRelations(lines, who);
            }
            WriteFormRecord(lines, who);
            WriteFormPartners(lines, who);
        }

        // THE SKILL BOOK: its level as the header's medallion, then the twelve as
        // boxes three to a row - a level, the pips toward its cap, the price of the
        // next one and its +. The compact head over it, as on Social: the book is
        // yours, and the page says so.
        private static void WriteFormSkills(List<SheetLine> lines, CommonStates who)
        {
            WriteFormBand(lines, who);
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null || sk.skills == null)
            {
                lines.Add(Row(Shared.Loc.Get("skills.head", "Skills"), "—"));
                return;
            }
            lines.Add(new SheetLine
            {
                Kind = LineFormHead, Label = null, Label2 = Shared.Loc.Get("dossier.skill_lv", "SKILL LV"), Value = sk.skillLevel.ToString(),
                Value2 = sk.exp.ToString("F0") + " / " + sk.maxExp.ToString("F0"),
                Frac = sk.maxExp > 0.0 ? (float)(sk.exp / sk.maxExp) : 0f, Enabled = true,
                Tips = new[] { TipSkillLevel, TipSkillExp },
            });
            SheetLine points = Section(Shared.Loc.Get("dossier.skills", "SKILLS"), Shared.Loc.Plural("dossier.points", "{0} point", "{0} points", sk.skillPoint));
            points.Tips = new[] { TipSkillPoints };
            points.PointsKind = SkillsToSpend(out _) ? 1 : 0;
            lines.Add(points);
            for (int start = 0; start < sk.skills.Length; start += 3)
            {
                int n = Mathf.Min(3, sk.skills.Length - start);
                var line = new SheetLine
                {
                    Kind = LineBoxes, Style = StyleSkill, Arg = BoxSkill, Enabled = true,
                    Options = new string[n], Values = new string[n], Foots = new string[n], Cans = new bool[n],
                    Args = new int[n], Pips = new int[n], PipMax = new int[n], Icons = new Sprite[n],
                    Tips = new string[n],
                };
                for (int k = 0; k < n; k++)
                {
                    int i = start + k;
                    int max = SkillMax(i);
                    bool maxed = max > 0 && sk.skills[i] >= max;
                    int cost = SkillCost(sk, i);
                    line.Options[k] = SkillName(i);
                    line.Values[k] = Shared.Loc.Get("sheet.lv", "Lv {0}", sk.skills[i]);
                    line.Foots[k] = maxed ? Shared.Loc.Get("dossier.max", "MAX") : Shared.Loc.Get("dossier.next_cost", "next {0} pt", cost);
                    line.Cans[k] = !maxed && sk.skillPoint >= cost;
                    line.Args[k] = i;
                    line.Pips[k] = sk.skills[i];
                    line.PipMax[k] = max;
                    line.Icons[k] = SkillIcon(i);
                    line.Tips[k] = SkillTip(i);
                }
                lines.Add(line);
            }
        }

        // the counts of a life, from every edge's ledger (sexCount: 0-4 the acts,
        // 8 pregnancies, 9 deliveries) - the Social page's and the plain sheet's
        private static void HistoryCounts(CommonStates who, out int sex, out int pregnancies, out int deliveries)
        {
            sex = 0;
            pregnancies = 0;
            deliveries = 0;
            if (who.lovers == null)
            {
                return;
            }
            for (int i = 0; i < who.lovers.Count; i++)
            {
                int[] counts = who.lovers[i] != null ? who.lovers[i].sexCount : null;
                for (int j = 0; counts != null && j < counts.Length; j++)
                {
                    if (j <= 4) { sex += counts[j]; }
                    else if (j == 8) { pregnancies += counts[j]; }
                    else if (j == 9) { deliveries += counts[j]; }
                }
            }
        }

        // ---------- pressing ----------

        private static void PressForm(CommonStates who, SheetRow row, int sub)
        {
            if (who == null || row.FormArgs == null || sub < 0 || sub >= row.FormArgs.Length)
            {
                return;
            }
            int code = row.FormArgs[sub];
            if (row.DrewKind == LineBoxes)
            {
                if (row.Arg == BoxStat) { AllocateStat(who, code); }
                else if (row.Arg == BoxSkill) { RaiseSkill(code); }
                else if (code == ToggleRun) { PressToggle(who, code); }   // the SPEED caption
                return;
            }
            if (code >= CheckOption)
            {
                // UIManager.NPCOptionChange, on this carrier: 0 is ALLOWED
                int id = code - CheckOption;
                if (who.sexInfo != null && id >= 0 && id < who.sexInfo.Length)
                {
                    who.sexInfo[id] = who.sexInfo[id] == 0 ? 1 : 0;
                }
                return;
            }
            if (code == RenameCode)
            {
                BeginRename(who, row);
                return;
            }
            if (code >= TypeCode && code < TypeCode + TraitTints.Length)
            {
                OpenRosterAt(code - TypeCode);
                return;
            }
            PressToggle(who, code);
        }

        // ---------- the form's measures (canvas units) ----------

        private const float FormCaptionH = 8f;                               // a caption over a field
        private const float FormDropRowH = FormCaptionH + KitDropdown.Height + 3f;
        private const float FormScaleRowH = 16f;
        private const float FormScaleLineH = 2f;                             // a scale's line under it (Value2), tucked under the track
        private const float FormHeadH = 64f;
        private const float FormHeadBookH = 44f;
        private const float FormSectionH = 16f;
        private const float FormChecksH = 14f;
        private const float FormFieldH = 14f;
        private const float FormButtonRowH = 17f;
        private const float FormButtonH = 13f;
        private const float FormBoxGap = 4f;
        private const float CaptionSize = 4.4f;
        private const float SectionSize = 5.4f;
        private const float FieldLabelSize = 6.6f;
        private const float FormTextSize = 7.6f;
        private const float BoxValueSize = 8.4f;
        private const float BigSize = 14f;
        private const float NameSize = 14f;
        private const float MedalSize = 11.5f;
        private const float CaptionSpacing = 14f;   // em/100: the form's spaced capitals
        private const float Hair = 0.45f;            // a printed rule

        private static float BoxHeight(int style)
        {
            switch (style)
            {
                case StyleAttribute: return 44f;
                case StyleSkill: return 36f;
                case StyleCount: return 25f;
                case StyleReading: return 21f;
                default: return 23f;
            }
        }

        private static float FormRowHeight(SheetLine line)
        {
            switch (line.Kind)
            {
                case LineFormHead: return line.Label != null ? FormHeadH : FormHeadBookH;
                case LineSection: return FormSectionH;
                case LineBoxes: return BoxHeight(line.Style) + FormBoxGap;
                case LineChecks: return FormChecksH;
                case LineField: return FormFieldH;
                case LineMoodNow: return FormMoodNowH - (line.Arg == 1 ? MoodRugH : 0f) + (line.Columns != null ? FormMoodCapsH : 0f);
                case LineMoodCause: return line.Band == 1 ? CauseLaneH(line) : FormMoodCauseH;
                case LineMoodCaps: return line.Style == 2 ? FormMoodCapsH + 3f
                    : line.Style == 1 ? (line.Band == 1 ? FormWhatMovesH : FormSectionH) : FormLoveHeadH;
                case LineMetHead: return line.Cans != null ? FormMetHeadH : FormMetHeadH - MetTimeH - MetMoonH;
                case LineMetPerson: return line.SeriesX != null ? FormLaneH : FormMetPersonH;
                case LineMetFold: return MetFoldHeight(line);
                case LineMetWhy: return MetWhyHeight(line);
                case LineBond: return FormBondH;
                case LineFormBand: return FormBandH;
                case LineSegments: return FormDropRowH;
                case LineStand: return FormStandH;
                case LineTiles: return FormTilesH;
                case LinePlaceHead:
                case LinePlaceRings:
                case LinePlaceRow:
                case LinePlaceNear:
                case LinePlaceFoot: return PlaceKindHeight(line);
                case LineTraits:
                case LinePartner: return RecordRowHeight(line);
                case LineActions: return line.Arg == ActionsRot ? FormChecksH : FormButtonRowH;
                case LineMore: return FormButtonRowH;
                default: return SheetRowH;
            }
        }

        // ---------- the pools ----------

        private static void BeginForm(SheetRow row)
        {
            row.FormImagesUsed = 0;
            row.FormTextsUsed = 0;
            row.TipZones.Clear();
            row.TipTexts.Clear();
            row.PointsDrawn = false;
        }

        // this part of the row says tips[i] under the pointer
        private static void FormTip(SheetRow row, string[] tips, int i, float x, float y, float w, float h)
        {
            string tip = tips != null && i >= 0 && i < tips.Length ? tips[i] : null;
            if (!string.IsNullOrEmpty(tip))
            {
                row.TipZones.Add(new Rect(x, y, w, h));
                row.TipTexts.Add(tip);
            }
        }

        private static void EndForm(SheetRow row, int planks)
        {
            if (!row.PointsDrawn && row.Points != null)
            {
                row.Points.Hide();
            }
            for (int i = row.FormImagesUsed; row.FormImages != null && i < row.FormImages.Count; i++)
            {
                if (row.FormImages[i].gameObject.activeSelf) { row.FormImages[i].gameObject.SetActive(false); }
            }
            for (int i = row.FormTextsUsed; row.FormTexts != null && i < row.FormTexts.Count; i++)
            {
                if (row.FormTexts[i].gameObject.activeSelf) { row.FormTexts[i].gameObject.SetActive(false); }
            }
            for (int i = planks; row.Planks != null && i < row.Planks.Length; i++)
            {
                if (row.Planks[i].gameObject.activeSelf) { row.Planks[i].gameObject.SetActive(false); }
            }
        }

        // a row leaving the form: its pools go dark and the plain row's parts get
        // back what the form changed on them
        private static void ClearForm(SheetRow row)
        {
            BeginForm(row);
            EndForm(row, 0);
            HidePortrait(row);
            row.Label.characterSpacing = 0f;
            row.Label.fontSize = SheetTextSize;
            row.Value.fontSize = SheetTextSize;
            Theme.KitSkin(row.Track, "kit_hairline", 4);
            row.Track.color = Theme.KitInk;
        }

        // Two layers over the row, made together so the type always lies over the
        // art: a box drawn after its caption must not cover it
        private static void EnsureFormLayers(SheetRow row)
        {
            if (row.FormArt != null)
            {
                return;
            }
            row.FormArt = FormLayer(row.Go.transform, "FormArt");
            row.FormType = FormLayer(row.Go.transform, "FormType");
        }

        private static Transform FormLayer(Transform parent, string name)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0f, 1f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Image FormImage(SheetRow row, string sprite, int border, Color color,
            float x, float y, float w, float h)
        {
            if (row.FormImages == null)
            {
                row.FormImages = new List<Image>();
            }
            Image img;
            if (row.FormImagesUsed < row.FormImages.Count)
            {
                img = row.FormImages[row.FormImagesUsed];
            }
            else
            {
                EnsureFormLayers(row);
                var go = Owned.New("Form");
                go.transform.SetParent(row.FormArt, false);
                img = go.AddComponent<Image>();
                img.raycastTarget = false;
                row.FormImages.Add(img);
            }
            row.FormImagesUsed++;
            if (sprite == null)
            {
                if (img.sprite != null) { img.sprite = null; }
                img.type = Image.Type.Simple;
            }
            else
            {
                Theme.KitSkin(img, sprite, border);
                img.preserveAspect = false;
            }
            if (img.color != color) { img.color = color; }
            Theme.Place(img, x, y, w, h);
            Untilt(img);
            if (!img.gameObject.activeSelf) { img.gameObject.SetActive(true); }
            return img;
        }

        // A STAMP LANDS ASKEW: a piece turned about the point (cx, cy), which is
        // where its centre ends up. The pools are shared, so every place undoes it.
        private static void Tilt(Component c, float cx, float cy, float degrees)
        {
            var r = (RectTransform)c.transform;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(cx, -cy);
            r.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private static void Untilt(Component c)
        {
            if (c.transform.localRotation != Quaternion.identity) { c.transform.localRotation = Quaternion.identity; }
        }

        // where a point (ox, oy) from a stamp's centre lands once the stamp is
        // turned by `degrees` (y down, as the form lays out)
        private static Vector2 Turned(float ox, float oy, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(ox * c + oy * s, -ox * s + oy * c);
        }

        private static Image FormSprite(SheetRow row, Sprite sprite, Color color, float x, float y, float w, float h)
        {
            Image img = FormImage(row, null, 0, color, x, y, w, h);
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        // `fit`: shrink to the box rather than run past it - a field on a line of
        // its own width ("Female Large Native" ran into the age beside it)
        private static TextMeshProUGUI FormText(SheetRow row, string text, float size, Color color,
            TextAlignmentOptions align, bool bold, float x, float y, float w, float h, float spacing = 0f,
            bool fit = false)
        {
            if (row.FormTexts == null)
            {
                row.FormTexts = new List<TextMeshProUGUI>();
            }
            TextMeshProUGUI t;
            if (row.FormTextsUsed < row.FormTexts.Count)
            {
                t = row.FormTexts[row.FormTextsUsed];
            }
            else
            {
                EnsureFormLayers(row);
                t = Theme.MakeKitText(row.FormType, "FormText", size, color, align);
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Overflow;
                row.FormTexts.Add(t);
            }
            row.FormTextsUsed++;
            if (t.text != text) { t.text = text; }
            if (t.fontSize != size) { t.fontSize = size; }
            if (t.color != color) { t.color = color; }
            if (t.alignment != align) { t.alignment = align; }
            FontStyles style = bold ? FontStyles.Bold : FontStyles.Normal;
            if (t.fontStyle != style) { t.fontStyle = style; }
            if (t.characterSpacing != spacing) { t.characterSpacing = spacing; }
            // the sheets are measured to the English: a longer language shrinks to the
            // box instead of running into its neighbour, and English stays as drawn
            fit = fit || Shared.Loc.Language != "en";
            if (t.enableAutoSizing != fit) { t.enableAutoSizing = fit; }
            if (fit)
            {
                t.fontSizeMin = size * 0.6f;
                t.fontSizeMax = size;
            }
            Theme.Place(t, x, y, w, h);
            Untilt(t);
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); }
            return t;
        }

        // the ink of the rim behind a mood face, so a pale face holds on the paper
        private static readonly Color ContourInk = new Color32(40, 27, 18, 255);

        private static float TextWidth(TextMeshProUGUI t)
        {
            return t.GetPreferredValues(t.text, 400f, 40f).x;
        }

        // the width of text being typed, its trailing spaces counted: a preferred
        // width leaves them out, so a space typed at the end did not move the
        // caret until the next letter came. Measured with a mark after it, less
        // the mark.
        private static float TypedWidth(TextMeshProUGUI t)
        {
            string text = t.text ?? string.Empty;
            if (text.Length == 0 || text[text.Length - 1] != ' ')
            {
                return TextWidth(t);
            }
            return t.GetPreferredValues(text + "|", 400f, 40f).x - t.GetPreferredValues("|", 400f, 40f).x;
        }

        // a pressable spot on the form: one of the row's planks, undrawn
        private static void FormHit(SheetRow row, int k, float x, float y, float w, float h)
        {
            if (row.Planks == null || k >= row.Planks.Length)
            {
                return;
            }
            Image plank = row.Planks[k];
            plank.enabled = false;
            Theme.Place(plank, x, y, w, h);
            if (!string.IsNullOrEmpty(row.PlankTexts[k].text)) { row.PlankTexts[k].text = string.Empty; }
            if (row.PlankIcons[k].enabled) { row.PlankIcons[k].enabled = false; }
            if (!plank.gameObject.activeSelf) { plank.gameObject.SetActive(true); }
        }

        private static bool Hot(SheetRow row, int k)
        {
            return ReferenceEquals(_sheetHot, row) && _sheetHotSub == k;
        }

        private static bool Down(SheetRow row, int k)
        {
            return Hot(row, k) && ReferenceEquals(_sheetPressed, row) && _sheetPressedSub == k
                && Input.GetMouseButton(0);
        }

        private static Color Alpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        // ---------- painting the form's rows ----------

        private static void PaintForm(SheetRow row, SheetLine line)
        {
            // the plain row's own type stays blank; the form prints its own
            if (!string.IsNullOrEmpty(row.Label.text)) { row.Label.text = string.Empty; }
            if (!string.IsNullOrEmpty(row.Value.text)) { row.Value.text = string.Empty; }
            row.DrewLabel = "\0";
            row.DrewValue = "\0";
            if (row.Icon.gameObject.activeSelf) { row.Icon.gameObject.SetActive(false); }
            row.Arg = line.Arg;
            row.On = line.On;
            row.Enabled = line.Enabled;
            row.Tip = line.Tip;
            row.FormArgs = line.Args;
            row.Link = line.Link;
            row.LinkType = line.Kind == LineMetPerson || line.Kind == LineBond || line.Kind == LineFormBand || line.Kind == LinePartner
                ? line.Style : -1;
            float w = row.FormW;
            float h = row.FormH;
            BeginForm(row);
            if ((line.Kind != LineFormHead || line.Label == null) && line.Kind != LineFormBand)
            {
                HidePortrait(row);
            }
            int planks = 0;
            switch (line.Kind)
            {
                case LineFormHead: planks = DrawFormHead(row, line, w, h); break;
                case LineSection: DrawSection(row, line, w, h); break;
                case LineBoxes: planks = DrawBoxes(row, line, w, h); break;
                case LineChecks: planks = DrawChecks(row, line, w, h); break;
                case LineField: DrawField(row, line, w, h); break;
                case LineTraits:
                case LinePartner: planks = DrawRecordRow(row, line, w, h); break;
                case LineActions: planks = DrawButtons(row, line, w, h); break;
                case LineMore: planks = DrawMore(row, line, w, h); break;
                case LineMoodNow: DrawMoodNow(row, line, w, h); break;
                case LineMoodCause: planks = DrawMoodCause(row, line, w, h); break;
                case LineMoodCaps: DrawMoodCaps(row, line, w, h); break;
                case LineMetHead: planks = DrawMetHead(row, line, w, h); break;
                case LineMetPerson: planks = DrawMetPerson(row, line, w, h); break;
                case LineMetWhy: planks = DrawMetWhy(row, line, w, h); break;
                case LineBond: planks = DrawBond(row, line, w, h); break;
                case LineFormBand: planks = DrawFormBand(row, line, w, h); break;
                case LineMetFold: planks = DrawMetFold(row, line, w, h); break;
                case LineSegments: planks = DrawSegments(row, line, w, h); break;
                case LineStand: planks = DrawStand(row, line, w, h); break;
                case LineTiles: planks = DrawTiles(row, line, w, h); break;
                case LinePlaceHead:
                case LinePlaceRings:
                case LinePlaceRow:
                case LinePlaceNear:
                case LinePlaceFoot: planks = DrawPlacePanel(row, line, w, h); break;
            }
            EndForm(row, planks);
        }

        // A POOL'S SIZE ON ITS BAR, in the unit's marks (the same visualization of
        // a hitpoint pool in groups of 100 and 1000): a thin mark per 100 of `max` and a heavy one per 1000 - the
        // ruler's rule (PlaceMarks), so a mark is always worth what it says and a
        // rank that would crowd is dropped whole
        private static void PoolBar(SheetRow row, float x, float cy, float w, float h, float frac, Color fill, float max)
        {
            float r = StripRimW;
            float top = cy - h * 0.5f;
            FormImage(row, null, 0, StripRim, x - r, top - r, w + 2f * r, h + 2f * r);
            FormImage(row, null, 0, StripBarTrack, x, top, w, h);
            float f = Mathf.Clamp01(frac) * w;
            if (f > 0.1f)
            {
                FormImage(row, null, 0, fill, x, top, f, h);
            }
            PoolMarks(row, x, w, top, h, max);
        }

        private static void PoolMarks(SheetRow row, float x0, float barW, float y, float barH, float max)
        {
            max = Mathf.Max(1f, max);
            PoolRank(row, BoxWorth, StripTick, StripRimW, x0, barW, y, barH, max);
            PoolRank(row, MajorWorth, StripMajor, MajorW, x0, barW, y, barH, max);
        }

        private static void PoolRank(SheetRow row, float worth, Color ink, float thickness,
            float x0, float barW, float y, float barH, float max)
        {
            int units = Mathf.Max(1, Mathf.CeilToInt(max / worth));
            if (barW / units < MinBoxW * 1.5f)
            {
                return;
            }
            for (int k = 1; k < units; k++)
            {
                float tx = x0 + barW * (worth * k) / max;
                FormImage(row, null, 0, ink, tx - thickness * 0.5f, y, thickness, barH);
            }
        }

        // PORTRAIT · NAME LINE · FIELDS · MEDALLION, then the exp. Without a name it
        // is the skill book's head: the medallion and the exp alone.
        private static int DrawFormHead(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            Color ink = Theme.DossierInk;
            Color soft = Theme.DossierSoft;
            const float medal = 40f;
            if (line.Label == null)
            {
                DrawMedallion(row, left, 1f, medal, line.Label2, line.Value);
                FormTip(row, line.Tips, 0, left, 1f, medal, medal);
                float ex = left + medal + 8f;
                float ey = 12f;
                FormTip(row, line.Tips, 1, ex, ey - 5f, right - ex, 10f);
                FormText(row, Shared.Loc.Get("dossier.exp", "EXP"), CaptionSize, soft, TextAlignmentOptions.MidlineLeft, true, ex, ey - 4f, 20f, 8f, CaptionSpacing);
                FormBar(row, ex + 14f, ey, right - 46f - (ex + 14f), 3.2f, line.Frac, SkillExpTint());
                FormText(row, line.Value2 ?? string.Empty, 6.4f, ink, TextAlignmentOptions.MidlineRight, false, right - 44f, ey - 4f, 44f, 8f);
                return 0;
            }
            // the portrait: the roster's snapshot of them as a passport photo on the
            // sheet; the head glyph until one has been taken
            const float ow = 36f, oh = 43f;
            bool live = PlacePortrait(row, left + 1f, 1f, ow - 2f, oh);
            if (!live && line.Icon != null)
            {
                FormSprite(row, line.Icon, ink, left + ow * 0.18f, 1f + oh * 0.2f, ow * 0.64f, oh * 0.6f);
            }
            float nx = left + ow + 9f;
            float n1 = right - medal - 8f;
            int count = line.Options != null ? line.Options.Length : 0;
            int marks = line.Glyphs != null ? Mathf.Min(line.Glyphs.Length, SheetPlankCount) : 0;
            float nameEnd = n1;
            int eye = -1;
            for (int i = marks - 1; i >= 0; i--)
            {
                if (line.Glyphs[i] == GlyphStar)
                {
                    DrawHeadStar(row, line, i, left - 4f, -3f, count + 2 + i);
                }
                else if (line.Glyphs[i] == GlyphEye)
                {
                    eye = i;
                }
            }
            // the name, unless it is being retyped right here (the field lies over it)
            bool renaming = UI.NameEdit.Open && ReferenceEquals(_renameRow, row);
            if (renaming && (row.Host == null || !ReferenceEquals(row.Host.Owner, _renameWho)))
            {
                UI.NameEdit.Cancel();   // the row was reused for someone else: their name stays theirs
                renaming = false;
            }
            bool nameHot = Hot(row, marks) && !renaming;
            // their type's token before the name, as the roster has it; the rule
            // and the caption under it keep the line's start
            float tx = nx;
            int typePlank = -1;
            if (line.Index >= 0 && line.Index < TraitTints.Length)
            {
                const float ts = 11f;
                float ty = 19f - ts - 3f;
                typePlank = marks + 1;
                DrawTraitToken(row, line.Index, nx, ty, ts);
                if (Hot(row, typePlank))
                {
                    HoverFrame(row, nx - 1f, ty - 1f, ts + 2f, ts + 2f);
                }
                FormHit(row, typePlank, nx - 1f, ty - 1f, ts + 2f, ts + 2f);
                FormTip(row, line.Tips, line.Tips != null ? line.Tips.Length - 2 : -1, nx - 1f, ty - 1f, ts + 2f, ts + 2f);
                nx += ts + 3f;
            }
            // while it is being retyped, the name IS the edit: what has been typed
            // so far, as it will be printed, and a text cursor after its last letter
            // A NAME IS PRINTED AS IT IS WRITTEN, never in capitals: it is someone's
            // name, the one you type when renaming, and it reads the same here as
            // on the tag over the head and in the roster. (Captions and headings
            // are the form's capitals; a name is not one.)
            TextMeshProUGUI nameText = FormText(row, renaming ? UI.NameEdit.Text : line.Label ?? string.Empty,
                NameSize, line.LabelTint, TextAlignmentOptions.BottomLeft, true, nx, 0f, nameEnd - nx, 19f, 2f, fit: true);
            if (renaming && UI.NameEdit.CaretShown)
            {
                float typedW = UI.NameEdit.Text.Length > 0 ? Mathf.Min(TypedWidth(nameText), nameEnd - nx) : 0f;
                FormImage(row, null, 0, ink, nx + typedW + 1f, 3f, 0.9f, 14.5f);
            }
            int planks = marks;
            if (line.Args != null && marks < line.Args.Length && line.Args[marks] == RenameCode)
            {
                FormHit(row, marks, nx, 0f, nameEnd - nx, 19f);
                FormTip(row, line.Tips, line.Tips != null ? line.Tips.Length - 1 : -1, nx, 0f, nameEnd - nx, 19f);
                planks = marks + 1;
            }
            if (typePlank >= 0)
            {
                planks = typePlank + 1;
            }
            row.NameArea = new Rect(nx, 0f, nameEnd - nx, 19f);
            nx = tx;
            // UNDER THE POINTER THE NAME IS A FIELD to write in: a faint wash and
            // its rule drawn heavier. The name keeps its ink - it went pale here
            // once, and the name changing colour read as something wrong with it
            if (nameHot)
            {
                FormImage(row, null, 0, Alpha(ink, 0.06f), nx, 1f, n1 - nx, 19f);
            }
            FormRule(row, ink, nx, 20f, n1 - nx, nameHot ? Hair * 2.5f : Hair);
            TextMeshProUGUI caption = FormText(row, Shared.Loc.Get("dossier.name", "NAME"), CaptionSize, soft, TextAlignmentOptions.TopLeft, true, nx, 21f, 40f, 6f, CaptionSpacing);
            if (eye >= 0)
            {
                DrawNameShown(row, line, eye, nx + TextWidth(caption), 21f, count + 2 + eye);
            }
            // the fields, sharing the name line's width
            float fx = nx;
            for (int i = 0; i < count; i++)
            {
                float frac = line.Fracs != null && i < line.Fracs.Length ? line.Fracs[i] : 1f / count;
                float fw = (n1 - nx) * frac;
                bool last = i == count - 1;
                string value = line.Values != null && i < line.Values.Length ? line.Values[i] : string.Empty;
                FormText(row, value, FormTextSize, ink, TextAlignmentOptions.BottomLeft, false, fx, 27f, fw - 6f, 11f, fit: true);
                FormRule(row, ink, fx, 38.5f, fw - (last ? 0f : 6f));
                FormText(row, line.Options[i], CaptionSize, soft, TextAlignmentOptions.TopLeft, true, fx, 39.5f, fw, 6f, CaptionSpacing);
                FormTip(row, line.Tips, i, fx, 27f, fw, 19f);
                fx += fw;
            }
            DrawMedallion(row, right - medal, 1f, medal, line.Label2, line.Value);
            FormTip(row, line.Tips, count, right - medal, 1f, medal, medal);
            float y = 55f;
            FormTip(row, line.Tips, count + 1, left, y - 5f, right - left, 10f);
            FormText(row, Shared.Loc.Get("dossier.exp", "EXP"), CaptionSize, soft, TextAlignmentOptions.MidlineLeft, true, left, y - 4f, 20f, 8f, CaptionSpacing);
            FormBar(row, left + 14f, y, n1 - (left + 14f), 3.2f, line.Frac, ExpTint());
            FormText(row, line.Value2 ?? string.Empty, 6.4f, ink, TextAlignmentOptions.MidlineRight, false, n1, y - 4f, right - n1, 8f);
            if (line.Baby > 0f)
            {
                DrawPregnantStamp(row, line, left + ow * 0.5f + 1f, 38f, count + 2 + marks);
            }
            return planks;
        }

        // ---------- renaming ----------

        private const int RenameCode = 93;
        private const int TypeCode = 100;   // the head's type token: the roster at type (code - TypeCode)
        private static SheetRow _renameRow;
        private static CommonStates _renameWho;

        // vanilla's rename (UIManager.NameInput), from the sheet: the field over
        // the name, and on Return the same three writes - the name, the tag over
        // the head, the friends list
        private static void BeginRename(CommonStates who, SheetRow row)
        {
            if (who == null || row == null || _mn == null) { return; }
            _renameRow = row;
            _renameWho = who;
            Rect area = row.NameArea;
            var rect = (RectTransform)row.Go.transform;
            UI.NameEdit.Begin(who.charaName,
                screen => RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, _renameCam, out Vector2 local)
                    && local.x - rect.rect.xMin >= area.xMin && local.x - rect.rect.xMin <= area.xMax
                    && rect.rect.yMax - local.y >= area.yMin && rect.rect.yMax - local.y <= area.yMax,
                name =>
                {
                    who.charaName = name;
                    if (who.nameVisible != null)
                    {
                        var tag = who.nameVisible.GetComponent<TextMeshProUGUI>();
                        if (tag != null) { tag.text = name; }
                    }
                    if (_mn != null && _mn.uiMN != null) { _mn.uiMN.FriendStatusChange(who); }
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                });
            _nextSheetPaint = 0f;
        }

        private static Camera _renameCam;   // the canvas camera, for the click-away test

        // PREGNANT, inked on the photo askew: a pink block with the word and how
        // far along - the form's one loud mark, for a thing that is occasional
        // and matters. The pregnancy's own pink, the birth act's enamel, and let
        // a little of the photo through: red read as an alarm.
        private static readonly Color StampPink = new Color(0.84f, 0.40f, 0.56f, 0.78f);

        // `scale` sets it on a smaller photo: the band's (Social, Orders) at 16/36
        private static void DrawPregnantStamp(SheetRow row, SheetLine line, float cx, float cy, int tip, float scale = 1f)
        {
            const float tilt = 8f;
            float sw = 40f * scale, sh = 15f * scale, off = 3.4f * scale, size = 5.6f * scale;
            Image block = FormImage(row, null, 0, StampPink, cx - sw * 0.5f, cy - sh * 0.5f, sw, sh);
            Tilt(block, cx, cy, tilt);
            Vector2 a = Turned(0f, -off, tilt);
            TextMeshProUGUI word = FormText(row, Shared.Loc.Get("dossier.pregnant", "PREGNANT"), size, Theme.KitCream, TextAlignmentOptions.Center, true,
                0f, 0f, sw, 8f * scale, 8f);
            Tilt(word, cx + a.x, cy + a.y, tilt);
            Vector2 b = Turned(0f, off, tilt);
            TextMeshProUGUI pct = FormText(row, (100f * line.Baby).ToString("F0") + "%", size, Theme.KitCream,
                TextAlignmentOptions.Center, true, 0f, 0f, sw, 8f * scale);
            Tilt(pct, cx + b.x, cy + b.y, tilt);
            FormTip(row, line.Tips, tip, cx - sw * 0.5f, cy - sh * 0.5f, sw, sh);
        }

        // SHOWN IN WORLD IS PART OF THE NAME'S CAPTION: "NAME · SHOWN IN WORLD"
        // with the game's eye, or "NAME · HIDDEN" with it shut - printed in the
        // caption's own type, and the words are the switch. A chip beside the
        // name read as a button stuck on the form.
        private static void DrawNameShown(SheetRow row, SheetLine line, int k, float x, float y, int tip)
        {
            bool on = line.Cans != null && k < line.Cans.Length && line.Cans[k];
            DrawCaptionSwitch(row, k, x, y, ToggleSprite(ToggleNameShown, on), 5.6f, on,
                on ? Shared.Loc.Get("dossier.shown_in_world", "SHOWN IN WORLD") : Shared.Loc.Get("dossier.hidden", "HIDDEN"),
                CaptionSpacing, float.MaxValue, line.Tips, tip);
        }

        // A CAPTION THAT IS A SWITCH (plank k), after the caption ending at x:
        // "·", the game's own mark (es wide) - faded when off - and the state's
        // words, in the caption's type, the ink darkening under the pointer. The
        // words end by right; past it they shrink rather than run over a rim.
        private static void DrawCaptionSwitch(SheetRow row, int k, float x, float y, Sprite mark, float es, bool on, string words,
            float spacing, float right, string[] tips, int tip)
        {
            Color col = Down(row, k) ? Theme.DossierFaded : Hot(row, k) ? Theme.DossierInk : Theme.DossierSoft;
            TextMeshProUGUI dot = FormText(row, "·", CaptionSize, col, TextAlignmentOptions.TopLeft, true, x + 2f, y, 6f, 6f);
            float ex = x + 2f + TextWidth(dot) + 2f;
            if (mark != null)
            {
                FormSprite(row, mark, on ? Color.white : new Color(1f, 1f, 1f, 0.55f), ex, y + (5.6f - es) * 0.5f - 0.4f, es, es);
            }
            float wx = ex + es + 1.5f;
            float room = Mathf.Clamp(right - wx, 1f, 80f);
            TextMeshProUGUI text = FormText(row, words, CaptionSize, col,
                TextAlignmentOptions.TopLeft, true, wx, y, room, 6f, spacing, fit: true);
            float end = wx + Mathf.Min(TextWidth(text), room);
            FormHit(row, k, x, y - 1.5f, end - x + 1f, 8f);
            FormTip(row, tips, tip, x, y - 1.5f, end - x + 1f, 8f);
        }

        // THE FAVOURITE, a star on the photo's corner: unset, a hollow star - a
        // dark well under a printed rim, something to fill; set, gold under the
        // same rim, and no glow - a gold halo round the rim read as a smudge on
        // the photo it sits on; under the pointer, the gold it
        // would be, faintly. The kit's own star pair (make_dossier.py), cut from
        // one shape, so the points stay sharp.
        private static void DrawHeadStar(SheetRow row, SheetLine line, int k, float x, float y, int tip)
        {
            const float s = 14f;
            bool on = line.Cans != null && k < line.Cans.Length && line.Cans[k];
            bool hot = Hot(row, k);
            // unset and at rest it is faint: an empty mark, not a call to look
            bool rest = !on && !hot && !Down(row, k);
            Color well = Down(row, k) ? Alpha(StarShine, 0.7f) : on ? StarShine : hot ? StarHover : Alpha(StripTrack, 0.45f);
            FormImage(row, "kit_dossier_star", 0, well, x, y, s, s);
            FormImage(row, "kit_dossier_star_rim", 0, rest ? Alpha(Theme.DossierLabel, 0.55f) : Theme.DossierLabel, x, y, s, s);
            FormHit(row, k, x - 1f, y - 1f, s + 2f, s + 2f);
            FormTip(row, line.Tips, tip, x - 1f, y - 1f, s + 2f, s + 2f);
        }

        // THE FAVOURITE ON A SMALLER PHOTO: the head's star where it sits on the
        // head's 34-wide photo, and the favourite's switch as there (plank k): set,
        // the star; unset, its outline, gold under the pointer. A half again its share of the photo: at
        // the head's share it was a speck on a photo this small. Drawn as the world
        // marker's star (WorldMarker.cs): a gold glow, a dark edge, the gold, no
        // printed rim - the edge and glow at the marker's 13 and 16 to its 11
        private const float HeadPhotoW = 34f, HeadStarS = 14f, HeadStarX = -5f, HeadStarY = -4f;
        private const float SmallStarGrow = 1.5f, SmallStarEdge = 2f / 11f, SmallStarGlow = 5f / 11f;

        private static void DrawPhotoStar(SheetRow row, float px, float py, float pw, bool on, int k)
        {
            float f = pw / HeadPhotoW, s = HeadStarS * f * SmallStarGrow;
            float x = px + HeadStarX * f, y = py + HeadStarY * f;
            float e = s * SmallStarEdge, g = s * SmallStarGlow;
            bool hot = Hot(row, k);
            if (on || hot)
            {
                FormImage(row, "kit_dossier_star", 0, Alpha(StarShine, on ? 0.35f : 0.2f), x - g * 0.5f, y - g * 0.5f, s + g, s + g);
                FormImage(row, "kit_dossier_star", 0, EmblemInk, x - e * 0.5f, y - e * 0.5f, s + e, s + e);
                FormImage(row, "kit_dossier_star", 0, Down(row, k) ? Alpha(StarShine, 0.7f) : on ? StarShine : StarHover, x, y, s, s);
            }
            else
            {
                FormImage(row, "kit_dossier_star_rim", 0, Alpha(EmblemInk, 0.6f), x, y, s, s);
            }
            FormHit(row, k, x - 1f, y - 1f, s + 2f, s + 2f);
            row.TipZones.Add(new Rect(x - 1f, y - 1f, s + 2f, s + 2f));
            row.TipTexts.Add(on ? Shared.Loc.Get("dossier.favourite.unmark", "Favourite: click to unmark") : Shared.Loc.Get("dossier.favourite.mark", "Click to make them a favourite"));
        }

        private static readonly Color StarShine = Theme.ParseColor("#FFC93C", Color.yellow);
        private static readonly Color StarHover = Theme.ParseColor("#F7E2A6", Color.yellow);

        private static Sprite RunSprite(bool on)
        {
            Sprite[] run = _mn != null && _mn.gameMN != null ? _mn.gameMN.toggleRunSprite : null;
            return run != null && run.Length > 1 ? run[on ? 1 : 0] : null;
        }

        // EXPERIENCE IS THE HUD'S BLUE (the blue of the vanilla bar at the bottom
        // left that holds the experience progress): that bar's own hue, read live so
        // the two can never disagree - and DAMPED, since at full strength on paper it
        // shone too bright: the same hue at three quarters of its saturation and less
        // light.
        private const float ExpSaturation = 0.75f;
        private const float ExpValue = 0.85f;

        private static Color ExpTint()
        {
            Image hud = _mn != null && _mn.gameMN != null ? _mn.gameMN.expImage : null;
            return DampExp(hud != null ? hud.color : Theme.DossierExp);
        }

        // THE SKILL LEVEL'S EXP IS GREEN (a lighter green for skills, the blue
        // kept for the character's own level): the
        // character level and the skill level are two tracks fed by the same EXP,
        // and one colour made them read as one. Damped as the blue is.
        private static Color SkillExpTint() => DampExp(Theme.DossierSkillExp);

        private static Color DampExp(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s * ExpSaturation, v * ExpValue);
        }

        private static void DrawMedallion(SheetRow row, float x, float y, float d, string caption, string value)
        {
            FormImage(row, "kit_dossier_ring", 0, Theme.DossierInk, x, y, d, d);
            FormText(row, caption ?? string.Empty, CaptionSize * 0.9f, Theme.DossierSoft,
                TextAlignmentOptions.Center, true, x, y + d * 0.18f, d, 6f, CaptionSpacing);
            FormText(row, value ?? string.Empty, MedalSize, Theme.DossierInk,
                TextAlignmentOptions.Center, true, x, y + d * 0.34f, d, d * 0.44f);
        }

        private static void FormRule(SheetRow row, Color color, float x, float y, float w, float thick = Hair)
        {
            FormImage(row, null, 0, color, x, y - thick * 0.5f, Mathf.Max(0f, w), thick);
        }

        private static void FormBar(SheetRow row, float x, float cy, float w, float h, float frac, Color fill)
        {
            // square ends: the chip sprite sliced this thin read as a pill
            w = Mathf.Max(2f, w);
            FormImage(row, null, 0, Theme.DossierTrack, x, cy - h * 0.5f, w, h);
            float f = Mathf.Clamp01(frac) * w;
            if (f > 0.1f)
            {
                FormImage(row, null, 0, fill, x, cy - h * 0.5f, f, h);
            }
        }

        // SPACED CAPITALS, A RULE TO THE MARGIN, AND WHAT THE SECTION COUNTS IN A PILL
        private static void DrawSection(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            float cy = h - 6f;
            TextMeshProUGUI title = FormText(row, line.Label ?? string.Empty, SectionSize,
                Theme.DossierInk, TextAlignmentOptions.MidlineLeft, true, left, cy - 4f, right - left, 8f, CaptionSpacing + 6f);
            float ruleX = left + TextWidth(title) + 4f;
            float ruleEnd = right;
            if (line.PointsKind != 0 && !string.IsNullOrEmpty(line.Value))
            {
                // points that buy something: the label is the tag (PointsTag)
                EnsureFormLayers(row);
                if (row.Points == null || row.Points.Rect == null)
                {
                    row.Points = PointsTag.Create(row.FormType, Shared.Loc.Get("sheet.points", "Points"));
                }
                const float th = 9f;
                row.Points.Show(line.PointsKind == 1 ? PointsTag.Kind.Skill : PointsTag.Kind.Attribute, line.Value, th);
                float tw = row.Points.Width;
                Theme.Place(row.Points.Rect, right - tw, cy - th * 0.5f, tw, th);
                row.PointsDrawn = true;
                FormTip(row, line.Tips, 0, right - tw, cy - th * 0.5f, tw, th);
                ruleEnd = right - tw - 3f;
            }
            else if (!string.IsNullOrEmpty(line.Value))
            {
                // the pill is ink unless the line says otherwise (MOOD's change)
                Color pillInk = line.ValueTint.a > 0f ? line.ValueTint : Theme.DossierInk;
                TextMeshProUGUI pill = FormText(row, line.Value, 6f, pillInk, TextAlignmentOptions.Center, false,
                    0f, 0f, 10f, 9f);
                float pw = TextWidth(pill) + 9f;
                Theme.Place(pill, right - pw, cy - 4.5f, pw, 9f);
                FormImage(row, "kit_dossier_pill", 15, pillInk, right - pw, cy - 4.5f, pw, 9f);
                FormTip(row, line.Tips, 0, right - pw, cy - 4.5f, pw, 9f);
                ruleEnd = right - pw - 3f;
            }
            FormRule(row, Theme.DossierInk, ruleX, cy, ruleEnd - ruleX);
        }

        // A ROW OF BOXES: three across (four for the counts), each a caption and a
        // reading, and per style a bar, a big number, a skill's glyph and pips, or
        // a foot with the price and the +
        private static int DrawBoxes(SheetRow row, SheetLine line, float w, float h)
        {
            int n = line.Options != null ? line.Options.Length : 0;
            int cols = n > 3 ? n : 3;
            float left = SheetPadX;
            float gap = 4f;
            float bw = (w - 2f * SheetPadX - gap * (cols - 1)) / cols;
            float bh = BoxHeight(line.Style);
            Color ink = Theme.DossierInk;
            Color soft = Theme.DossierSoft;
            int planks = 0;
            int switches = 0;
            for (int i = 0; i < n; i++)
            {
                float x = left + i * (bw + gap);
                const float y = 0f;
                string caption = line.Options[i] ?? string.Empty;
                string value = line.Values != null && i < line.Values.Length ? line.Values[i] : string.Empty;
                bool bold = line.Style == StyleAttribute;
                // A READING'S CAPTION CAN BE A SWITCH (your SPEED · ALWAYS RUN): its
                // tip follows the boxes' own, one per switch
                bool flips = line.Style == StyleReading && line.Glyphs != null && i < line.Glyphs.Length && line.Glyphs[i] == GlyphRun;
                // A BOX IN ALARM (a negative moral): a red wash, a bold red rim
                // and its number in red - the one box on the page that is
                // telling you something is wrong
                bool warn = line.Warns != null && i < line.Warns.Length && line.Warns[i];
                if (warn)
                {
                    FormImage(row, null, 0, Alpha(Theme.DossierRed, 0.1f), x, y, bw, bh);
                }
                FormImage(row, bold || warn ? "kit_dossier_box_bold" : "kit_dossier_box", bold || warn ? 7 : 6,
                    warn ? Theme.DossierRed : ink, x, y, bw, bh);
                // the switch's zone before the box's: the first zone under the pointer answers
                if (flips && planks < SheetPlankCount)
                {
                    bool on = line.Cans != null && i < line.Cans.Length && line.Cans[i];
                    TextMeshProUGUI cap = FormText(row, caption, CaptionSize, soft, TextAlignmentOptions.TopLeft, true, x + 3f, y + 2.2f, bw - 6f, 6f, CaptionSpacing * 0.6f);
                    // the runner at the caption's height: the game's button art is a
                    // dark tile, and at the eye's size it stood taller than the words
                    DrawCaptionSwitch(row, planks, x + 3f + TextWidth(cap), y + 2.2f, RunSprite(on), 4.6f, on,
                        on ? Shared.Loc.Get("dossier.always_run", "ALWAYS RUN") : Shared.Loc.Get("dossier.walk", "WALK"),
                        CaptionSpacing * 0.6f, x + bw - 3f, line.Tips, n + switches);
                    _formBoxOf[planks] = i;
                    planks++;
                    switches++;
                }
                FormTip(row, line.Tips, i, x, y, bw, bh);
                switch (line.Style)
                {
                    case StyleAttribute:
                        FormText(row, caption, CaptionSize, soft, TextAlignmentOptions.Center, true, x, y + 2.5f, bw, 6f, CaptionSpacing);
                        FormText(row, value, BigSize, ink, TextAlignmentOptions.Center, true, x, y + 9f, bw, 19f);
                        break;
                    case StyleCount:
                        FormText(row, caption, CaptionSize, soft, TextAlignmentOptions.Center, true, x, y + 2.5f, bw, 6f, CaptionSpacing * 0.6f);
                        FormText(row, value, 10f, ink, TextAlignmentOptions.Center, true, x, y + 9.5f, bw, 13f);
                        break;
                    case StyleSkill:
                    {
                        Sprite icon = line.Icons != null && i < line.Icons.Length ? line.Icons[i] : null;
                        if (icon != null)
                        {
                            FormSprite(row, icon, Color.white, x + 2.5f, y + 2.5f, 9f, 9f);
                        }
                        FormText(row, caption, 6f, Theme.DossierLabel, TextAlignmentOptions.MidlineLeft, false, x + 14f, y + 2f, bw - 16f, 10f);
                        FormText(row, value, 8.4f, ink, TextAlignmentOptions.MidlineLeft, true, x + 3f, y + 12f, bw * 0.5f, 11f);
                        int pips = line.Pips != null && i < line.Pips.Length ? line.Pips[i] : 0;
                        int max = line.PipMax != null && i < line.PipMax.Length ? line.PipMax[i] : 0;
                        if (max > 0)
                        {
                            float pd = max > 5 ? 2.4f : 3.2f;
                            float step = max > 5 ? 3.3f : 4.4f;
                            float px = x + bw - 3f - pd - step * (max - 1);
                            for (int k = 0; k < max; k++)
                            {
                                FormImage(row, k < pips ? "kit_dossier_dot" : "kit_dossier_radio", 0, ink,
                                    px + k * step, y + 17.5f - pd * 0.5f, pd, pd);
                            }
                        }
                        break;
                    }
                    default:
                        if (!flips)
                        {
                            FormText(row, caption, CaptionSize, soft, TextAlignmentOptions.TopLeft, true, x + 3f, y + 2.2f, bw - 6f, 6f, CaptionSpacing * 0.6f);
                        }
                        FormText(row, value, BoxValueSize, warn ? Theme.DossierRed : ink, TextAlignmentOptions.MidlineRight, true, x + 3f,
                            y + (line.Style == StyleReading ? 8f : 5.5f), bw - 6f, 11f);
                        if (line.Style == StyleVital && line.Fracs != null && i < line.Fracs.Length && line.Fracs[i] >= 0f)
                        {
                            Color tint = line.Tints != null && i < line.Tints.Length ? line.Tints[i] : Theme.DossierExp;
                            float barY = y + bh - 4.5f;
                            float pool = line.Pools != null && i < line.Pools.Length ? line.Pools[i] : 0f;
                            if (pool > 0f)
                            {
                                // HP and stun are POOLS, drawn as the unit draws its
                                // own: a black rim, the dark track, a flat fill and
                                // the marks over it (bare, they looked poor next to
                                // the indicator's bordered bars)
                                PoolBar(row, x + 3.5f, barY, bw - 7f, 3f, line.Fracs[i], tint, pool);
                            }
                            else
                            {
                                FormBar(row, x + 3f, barY, bw - 6f, 2.6f, line.Fracs[i], tint);
                            }
                        }
                        break;
                }
                // the foot: the next level's price, and the + when it can be paid
                string foot = line.Foots != null && i < line.Foots.Length ? line.Foots[i] : null;
                if (foot != null)
                {
                    bool can = line.Cans != null && i < line.Cans.Length && line.Cans[i];
                    float fy = y + bh - 11f;
                    FormRule(row, ink, x, fy, bw);
                    // a capped skill: read from its pips, not from the word its foot prints
                    bool maxed = line.PipMax != null && i < line.PipMax.Length && line.PipMax[i] > 0
                        && line.Pips != null && i < line.Pips.Length && line.Pips[i] >= line.PipMax[i];
                    FormText(row, foot, maxed ? CaptionSize : 5.8f, can ? Theme.DossierLabel : soft,
                        maxed ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft, maxed,
                        x + 3f, fy + 0.5f, bw - (maxed ? 6f : 16f), 10f, maxed ? CaptionSpacing : 0f);
                    if (can)
                    {
                        const float cs = 8f;
                        float cx = x + bw - cs - 2f;
                        float cy = fy + (11f - cs) * 0.5f;
                        bool down = Down(row, planks);
                        bool hot = Hot(row, planks);
                        Color chip = down ? Theme.DossierFaded : hot ? Theme.DossierLabel : ink;
                        FormImage(row, "kit_dossier_chip", 8, chip, cx, cy, cs, cs);
                        FormImage(row, "kit_plus", 0, Theme.KitCream, cx + 1.8f, cy + 1.8f, cs - 3.6f, cs - 3.6f);
                        FormHit(row, planks, cx - 1.5f, cy - 1.5f, cs + 3f, cs + 3f);
                        if (line.Args != null && planks < row.Planks.Length)
                        {
                            // the plank's index is the sub the press reads; map it back to the box
                            _formBoxOf[planks] = i;
                        }
                        planks++;
                    }
                }
            }
            if (line.Args != null && planks > 0)
            {
                // FormArgs is indexed by plank, not by box: only a box that can be
                // paid for has a +
                var args = new int[planks];
                for (int k = 0; k < planks; k++)
                {
                    int box = _formBoxOf[k];
                    args[k] = box < line.Args.Length ? line.Args[box] : -1;
                }
                row.FormArgs = args;
            }
            return planks;
        }

        private static readonly int[] _formBoxOf = new int[SheetPlankCount];

        // CHECKBOXES SIDE BY SIDE: an inked box, ticked when set - or vanilla's eye
        // or star on the ink for the two marks the game draws that way
        private static int DrawChecks(SheetRow row, SheetLine line, float w, float h)
        {
            int n = line.Options != null ? Mathf.Min(line.Options.Length, SheetPlankCount) : 0;
            float left = SheetPadX;
            float colW = (w - 2f * SheetPadX) / Mathf.Max(2, n);
            const float bs = 8.5f;
            float by = (h - bs) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = left + i * colW;
                bool on = line.Cans != null && i < line.Cans.Length && line.Cans[i];
                int glyph = line.Glyphs != null && i < line.Glyphs.Length ? line.Glyphs[i] : GlyphCheck;
                bool hot = Hot(row, i);
                bool down = Down(row, i);
                Color body = down ? Theme.DossierFaded : hot ? Theme.DossierLabel : Theme.DossierInk;
                if (glyph == GlyphCheck && !on)
                {
                    FormImage(row, "kit_dossier_check", 0, body, x, by, bs, bs);
                }
                else
                {
                    FormImage(row, "kit_dossier_chip", 8, body, x, by, bs, bs);
                    if (glyph == GlyphCheck)
                    {
                        FormImage(row, "kit_check", 0, Theme.KitCream, x + 1.2f, by + 1.2f, bs - 2.4f, bs - 2.4f);
                    }
                    else
                    {
                        Sprite mark = ToggleSprite(glyph == GlyphEye ? ToggleNameShown : ToggleFavourite, on);
                        if (mark != null)
                        {
                            FormSprite(row, mark, glyph == GlyphStar && !on ? new Color(1f, 1f, 1f, 0.35f) : Color.white,
                                x + 1f, by + 1f, bs - 2f, bs - 2f);
                        }
                    }
                }
                TextMeshProUGUI label = FormText(row, line.Options[i] ?? string.Empty, FormTextSize, Theme.DossierLabel,
                    TextAlignmentOptions.MidlineLeft, false, x + bs + 3.5f, 0f, colW - bs - 4f, h);
                FormHit(row, i, x - 1f, by - 2f, bs + 4.5f + Mathf.Min(colW - bs - 4f, TextWidth(label)), bs + 4f);
                FormTip(row, line.Tips, i, x - 1f, 0f, colW, h);
            }
            return n;
        }

        // A FILL-IN FIELD: its name, a ruled line, the answer written on it; two to
        // a row when there are two
        // THE PERSONALITY TOKENS: one mark per type, a shape and a colour each,
        // the same wherever the type is - IS, LIKES or DISLIKES, on any sheet. The
        // game's rule is plain matching (GameManager's love check: near someone
        // whose type you like, +10 a check; one you dislike, -5; anyone else +5),
        // so a match is two equal marks, read at a glance. ONE FAMILY: every token
        // is the same rounded tile in the type's colour, its pictogram knocked out
        // in cream, under a thin ink rim - so ten different pictures read as one
        // set, and at the smallest size the colour still says which. The other
        // looks that were weighed stay one switch away (TraitStyle). The masks
        // from make_dossier.py (kit_trait_*), colours here, in the game's type
        // order.
        private const int StoryColorGroup = 5;   // fSlotColor's orange: the story characters

        // For the harness: every follower's id, colour group, kind and age -
        // what the head's KIND and AGE are read from
        internal static string FollowerFacts()
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (npcs == null || npcs.friendList == null || _mn.uiMN == null) { return "no friend list"; }
            var sb = new System.Text.StringBuilder();
            foreach (CommonStates f in npcs.friendList)
            {
                if (f == null) { continue; }
                sb.Append(f.npcID).Append(" g").Append(_mn.uiMN.GetNPCColorID(f.npcID))
                    .Append(" '").Append(f.charaName).Append("' kind='").Append(KindName(npcs, f))
                    .Append("' age=").Append(f.age).Append("; ");
            }
            return sb.ToString();
        }

        private static readonly Color[] TraitTints =
        {
            Theme.ParseColor("#328F83", Color.gray),   // 0 laid-back: teal, a sun lounger
            Theme.ParseColor("#648A3D", Color.gray),   // 1 good-natured: green, a gift
            Theme.ParseColor("#AA6036", Color.gray),   // 2 crude: rust, a club
            Theme.ParseColor("#A54F84", Color.gray),   // 3 narcissist: magenta, a hand mirror
            Theme.ParseColor("#606975", Color.gray),   // 4 negative: slate, a half-full glass
            Theme.ParseColor("#BE536A", Color.gray),   // 5 shameless: rose, a low neckline
            Theme.ParseColor("#CF9B33", Color.gray),   // 6 selfish: amber, a crown
            Theme.ParseColor("#4376AA", Color.gray),   // 7 acts tough: blue, a flexed arm
            Theme.ParseColor("#7E7847", Color.gray),   // 8 sloppy: olive, a die
            Theme.ParseColor("#76589A", Color.gray),   // 9 mean: violet, angry eyes
        };
        private static readonly Color TraitCream = Theme.ParseColor("#FFF1CB", Color.white);

        // THE TOKENS' LOOK: every one weighed is exported, so changing it is this
        // value (or the harness's TraitStyleSet, to compare in a running game).
        //   Plain     the pictogram in the colour under a printed ink rim
        //   Tile      a rounded tile in the colour, lit at the top and shaded at the
        //             foot, its rim the colour at a third, the pictogram in cream
        //             (drawn by hand: art/trait-tile-a)
        //   Outlined  a cream tile, its rim and the pictogram in the colour
        //   Line      no frame: one size, one baseline, rimmed in a darker colour
        //   Tag       the tile with square corners and one clipped
        internal enum TraitStyle { Plain, Tile, Outlined, Line, Tag }
        private static TraitStyle _traitStyle = TraitStyle.Tile;

        // harness: show the tokens in another look until the next reload
        internal static string TraitStyleSet(string name)
        {
            try
            {
                _traitStyle = (TraitStyle)System.Enum.Parse(typeof(TraitStyle), name, true);
            }
            catch (System.ArgumentException)
            {
                return "styles: " + string.Join(", ", System.Enum.GetNames(typeof(TraitStyle)));
            }
            _forceRefresh = true;
            return "tokens: " + _traitStyle;
        }

        // a token's layers in the current look, bottom up: a kit mask and its
        // colour. The sheets stack them as Images; the tooltips' inline icons
        // (InlineIcons) are painted from the same list, so the two always agree.
        private static KeyValuePair<string, Color>[] TraitLayers(int type)
        {
            Color tint = TraitTints[type];
            Color ink = Theme.DossierInk;
            switch (_traitStyle)
            {
                case TraitStyle.Tile:
                    return new[] { Layer("kit_trait_tile", tint), Layer("kit_trait_tile_light", Color.white),
                        Layer("kit_trait_tile_shade", Color.black),
                        Layer("kit_trait_tile_rim", new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 1f)),
                        Layer("kit_trait_tile_" + type, TraitCream) };
                case TraitStyle.Tag:
                    return new[] { Layer("kit_trait_tag", tint), Layer("kit_trait_tag_" + type, TraitCream),
                        Layer("kit_trait_tag_rim", Alpha(ink, 0.78f)) };
                case TraitStyle.Outlined:
                    return new[] { Layer("kit_trait_tile", TraitCream), Layer("kit_trait_outline_" + type, tint),
                        Layer("kit_trait_outline_rim", tint) };
                case TraitStyle.Line:
                    return new[] { Layer("kit_trait_line_" + type, tint),
                        Layer("kit_trait_line_" + type + "_rim", Color.Lerp(ink, tint, 0.55f)) };
                default:
                    return new[] { Layer("kit_trait_" + type, tint), Layer("kit_trait_" + type + "_rim", Alpha(ink, 0.7f)) };
            }
        }

        private static KeyValuePair<string, Color> Layer(string mask, Color color)
        {
            return new KeyValuePair<string, Color>(mask, color);
        }

        // `fade`: the whole token at that strength
        private static void DrawTraitToken(SheetRow row, int type, float x, float y, float s, float fade = 1f)
        {
            if (type < 0 || type >= TraitTints.Length) { return; }
            foreach (KeyValuePair<string, Color> layer in TraitLayers(type))
            {
                FormImage(row, layer.Key, 0, Alpha(layer.Value, layer.Value.a * fade), x, y, s, s);
            }
        }

        // A TYPE IN A TOOLTIP: its token inline, then its name. The icons are
        // defined for the look the sheets draw, again whenever that changes.
        private static TraitStyle _traitIconsFor = (TraitStyle)(-1);

        private static string TypeWithIcon(int type)
        {
            if (type < 0 || type >= TraitTints.Length)
            {
                return TypeText(type);
            }
            if (_traitIconsFor != _traitStyle)
            {
                for (int k = 0; k < TraitTints.Length; k++)
                {
                    InlineIcons.Define("type" + k, TraitLayers(k));
                }
                _traitIconsFor = _traitStyle;
            }
            return InlineIcons.Tag("type" + type) + " " + TypeText(type);
        }

        private static readonly Color TraitLike = Theme.ParseColor("#4E8A3E", Color.green);
        private static readonly Color TraitDislike = Theme.ParseColor("#A82622", Color.red);

        private static void DrawField(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            bool two = !string.IsNullOrEmpty(line.Label2);
            float half = two ? (right - left - 8f) * 0.5f : right - left;
            DrawOneField(row, line.Label, line.Value, left, half, h);
            FormTip(row, line.Tips, 0, left, 0f, half, h);
            if (two)
            {
                DrawOneField(row, line.Label2, line.Value2, left + half + 8f, half, h);
                FormTip(row, line.Tips, 1, left + half + 8f, 0f, half, h);
            }
        }

        // `ink`: the value's (ink unless given); `hot`: a value that leads
        // somewhere, under the pointer - its line inked
        private static void DrawOneField(SheetRow row, string label, string value, float x, float w, float h,
            Color? ink = null, bool hot = false)
        {
            float baseY = h - 3f;
            TextMeshProUGUI name = FormText(row, label ?? string.Empty, FieldLabelSize, Theme.DossierLabel,
                TextAlignmentOptions.BottomLeft, false, x, 0f, w, baseY + 0.5f);
            float lw = TextWidth(name);
            FormRule(row, hot ? Theme.DossierInk : Theme.DossierSoft, x + lw + 2.5f, baseY + 0.8f, w - lw - 2.5f, hot ? 0.6f : Hair);
            if (!string.IsNullOrEmpty(value))
            {
                FormText(row, value, FormTextSize, ink ?? Theme.DossierInk, TextAlignmentOptions.BottomLeft, false,
                    x + lw + 4.5f, 0f, Mathf.Max(4f, w - lw - 5f), baseY + 0.8f, 0f, fit: true);
            }
        }

        // PRINTED BUTTONS: an inked outline and its word, shaded under the pointer -
        // red for the one that cannot be taken back. The orientation pair is a set
        // of radios instead, since it is a choice of one.
        private static int DrawButtons(SheetRow row, SheetLine line, float w, float h)
        {
            int n = line.Options != null ? Mathf.Min(line.Options.Length, SheetPlankCount) : 0;
            float left = SheetPadX;
            float right = w - SheetPadX;
            if (line.Arg == ActionsRot)
            {
                float colW = (right - left) / 2f;
                Color ink = line.Enabled ? Theme.DossierInk : Theme.DossierSoft;
                for (int i = 0; i < n; i++)
                {
                    float x = left + i * colW;
                    const float rs = 8f;
                    float ry = (h - rs) * 0.5f;
                    FormImage(row, "kit_dossier_radio", 0, Hot(row, i) ? Theme.DossierLabel : ink, x, ry, rs, rs);
                    if (line.Index == i)
                    {
                        FormImage(row, "kit_dossier_dot", 0, ink, x + 2.2f, ry + 2.2f, rs - 4.4f, rs - 4.4f);
                    }
                    TextMeshProUGUI label = FormText(row, line.Options[i], FormTextSize,
                        line.Enabled ? Theme.DossierLabel : Theme.DossierSoft, TextAlignmentOptions.MidlineLeft, false,
                        x + rs + 3.5f, 0f, colW - rs - 4f, h);
                    FormHit(row, i, x - 1f, ry - 2f, rs + 4.5f + TextWidth(label), rs + 4f);
                    FormTip(row, line.Tips, i, x - 1f, 0f, colW, h);
                }
                return n;
            }
            float gap = 5f;
            float bw = n > 0 ? (right - left - gap * (n - 1)) / n : 0f;
            float by = (h - FormButtonH) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = left + i * (bw + gap);
                DrawButton(row, i, line.Options[i], line.Danger == i + 1, x, by, bw);
                FormTip(row, line.Tips, i, x, by, bw, FormButtonH);
            }
            return n;
        }

        private static void DrawButton(SheetRow row, int k, string label, bool danger, float x, float y, float bw,
            bool dim = false, bool bold = false)
        {
            Color ink = danger ? Theme.DossierRed : dim ? Theme.DossierSoft : Theme.DossierInk;
            bool hot = Hot(row, k);
            bool down = Down(row, k);
            if (hot)
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(ink, down ? 0.22f : 0.1f), x, y, bw, FormButtonH);
            }
            FormImage(row, "kit_dossier_button", 12, ink, x, y, bw, FormButtonH);
            FormText(row, label ?? string.Empty, FormTextSize, ink, TextAlignmentOptions.Center, bold,
                x, y + (down ? 0.4f : 0f), bw, FormButtonH);
            FormHit(row, k, x, y, bw, FormButtonH);
        }

        // "… N more": the one button that opens the whole matrix, at the row's end
        private static int DrawMore(SheetRow row, SheetLine line, float w, float h)
        {
            string label = line.Options != null && line.Options.Length > 0 ? line.Options[0] : "…";
            float bw = 0f;
            if (label != null)
            {
                bw = Mathf.Max(SheetMoreW, MeasureForm(row, label, FormTextSize) + 12f);
                DrawButton(row, 0, label, false, w - SheetPadX - bw, (h - FormButtonH) * 0.5f, bw);
                FormTip(row, line.Tips, 0, w - SheetPadX - bw, 0f, bw, h);
            }
            else
            {
                FormNoHit(row, 0);
            }
            if (line.Cans == null || line.Cans.Length < 2)
            {
                return 1;
            }
            // a checkbox on the left, the Social page's foot (plank 1, its code in Args)
            const float bs = 8.5f;
            float x = SheetPadX, by = (h - bs) * 0.5f;
            bool on = line.Cans[1];
            Color body = Down(row, 1) ? Theme.DossierFaded : Hot(row, 1) ? Theme.DossierLabel : Theme.DossierInk;
            if (on)
            {
                FormImage(row, "kit_dossier_chip", 8, body, x, by, bs, bs);
                FormImage(row, "kit_check", 0, Theme.KitCream, x + 1.2f, by + 1.2f, bs - 2.4f, bs - 2.4f);
            }
            else
            {
                FormImage(row, "kit_dossier_check", 0, body, x, by, bs, bs);
            }
            float lw = w - 2f * SheetPadX - bw - bs - 8f;
            TextMeshProUGUI text = FormText(row, line.Label ?? string.Empty, FormTextSize, Theme.DossierLabel,
                TextAlignmentOptions.MidlineLeft, false, x + bs + 3.5f, 0f, lw, h, 0f, fit: true);
            float hw = bs + 3.5f + Mathf.Min(TextWidth(text), lw) + 1f;
            FormHit(row, 1, x - 1f, 0f, hw + 1f, h);
            FormTip(row, line.Tips, 1, x - 1f, 0f, hw + 1f, h);
            return 2;
        }

        // ---------- the portrait (D125) ----------

        // The game ships no portrait art, so the oval shows the character's own
        // Spine skeleton, rendered this frame by a camera of ours: looking the way
        // the main camera looks, orthographic, framed on the head bone in the
        // skeleton's own units, lit by a key light of ours, seeing one layer -
        // which the character's renderers are moved onto for the length of the
        // render and straight back, so nobody standing beside them gets into the
        // picture. WorkshopNPCManager.CaptureNPCImage renders an NPC to a texture
        // the same way; that one is a still for the workshop, this one is live.
        private const int PortraitLayer = 31;
        private const int PortraitPxW = 160;
        private const int PortraitPxH = 192;
        private const float PortraitFill = 0.72f;   // the frame's half-height, against the head's box
        private const float PortraitDrop = 0.06f;   // the frame's centre below the box's, against its height
        // the skull under the hair, grown to the head with its hair, and lifted to
        // match: the box the vertex rule gives on a rig whose hair is short (Man)
        private const float PortraitSkullGrow = 1.25f;
        private const float PortraitSkullLift = 0.08f;

        // every vertex of one slot's attachment, in skeleton space
        private static bool SlotBox(Spine.Slot slot, out Vector2 centre, out Vector2 size)
        {
            return SlotBox(slot, Vector2.right, Vector2.up, out centre, out size);
        }

        // ...or measured along two axes of it, the head's own when it lies (Upright)
        private static bool SlotBox(Spine.Slot slot, Vector2 right, Vector2 up, out Vector2 centre, out Vector2 size)
        {
            centre = Vector2.zero;
            size = Vector2.zero;
            int floats;
            if (slot.Attachment is Spine.RegionAttachment region)
            {
                floats = 8;
                region.ComputeWorldVertices(slot.Bone, _headVerts, 0, 2);
            }
            else if (slot.Attachment is Spine.MeshAttachment mesh)
            {
                floats = mesh.WorldVerticesLength;
                if (_headVerts.Length < floats) { _headVerts = new float[floats]; }
                mesh.ComputeWorldVertices(slot, _headVerts);
            }
            else
            {
                return false;
            }
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int k = 0; k + 1 < floats; k += 2)
            {
                float x = _headVerts[k] * right.x + _headVerts[k + 1] * right.y;
                float y = _headVerts[k] * up.x + _headVerts[k + 1] * up.y;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }
            centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            size = new Vector2(maxX - minX, maxY - minY);
            return true;
        }
        private static float[] _headVerts = new float[64];

        // The head's box in skeleton space: every drawn vertex the HEAD BONE moves
        // most - the face and the skull. Per VERTEX, because the face and the hair
        // are weighted meshes on slots that hang from the root, only their vertices
        // bound to the head, and a body mesh can reach into the neck. The head bone
        // itself and not its children, because long hair hangs from children: a
        // twin tail to the waist made the box a torso (the first sheet of
        // portraits). A vertex belongs to its strongest bone. False when the head
        // moves none.
        private static bool HeadBox(Spine.Skeleton skeleton, Spine.Bone head, out Vector2 centre, out Vector2 size)
        {
            return HeadBox(skeleton, head, Vector2.right, Vector2.up, out centre, out size);
        }

        private static bool HeadBox(Spine.Skeleton skeleton, Spine.Bone head, Vector2 right, Vector2 up, out Vector2 centre, out Vector2 size)
        {
            // THE SKULL FIRST: every humanoid rig here carries a "Head" slot - the
            // head's own shape, under the hair - and it is the one measure that is
            // the same thing on every rig. Back hair is bound straight to the head
            // bone on some (Aira's braids spread the box sideways, the Giant's hair
            // stretched it to 14 units), so the vertex rule below is the fallback.
            Spine.Slot skull = skeleton.FindSlot("Head");
            if (skull != null && skull.A > 0.01f && SlotBox(skull, right, up, out centre, out size))
            {
                centre.y += size.y * PortraitSkullLift;
                size *= PortraitSkullGrow;
                return true;
            }
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var slots = skeleton.Slots;
            var bones = skeleton.Bones.Items;
            for (int i = 0; i < slots.Count; i++)
            {
                Spine.Slot slot = slots.Items[i];
                if (slot == null || slot.A <= 0.01f)
                {
                    continue;
                }
                int count;           // vertices
                int[] weighted = null;
                float[] setup = null;
                if (slot.Attachment is Spine.RegionAttachment region)
                {
                    if (slot.Bone != head) { continue; }
                    count = 4;
                    region.ComputeWorldVertices(slot.Bone, _headVerts, 0, 2);
                }
                else if (slot.Attachment is Spine.MeshAttachment mesh)
                {
                    weighted = mesh.Bones;
                    setup = mesh.Vertices;
                    if (weighted == null && slot.Bone != head) { continue; }
                    int floats = mesh.WorldVerticesLength;
                    if (_headVerts.Length < floats) { _headVerts = new float[floats]; }
                    mesh.ComputeWorldVertices(slot, _headVerts);
                    count = floats / 2;
                }
                else
                {
                    continue;
                }
                int b = 0, w = 0;
                for (int v = 0; v < count; v++)
                {
                    if (weighted != null)
                    {
                        // [influences, bone, bone...] beside [x, y, weight] per influence
                        if (b >= weighted.Length) { break; }
                        int n = weighted[b++];
                        float best = -1f;
                        int strongest = -1;
                        for (int k = 0; k < n && b < weighted.Length; k++, b++, w += 3)
                        {
                            float weight = w + 2 < setup.Length ? setup[w + 2] : 0f;
                            if (weight > best) { best = weight; strongest = weighted[b]; }
                        }
                        if (strongest < 0 || strongest >= bones.Length || bones[strongest] != head)
                        {
                            continue;
                        }
                    }
                    float x = _headVerts[v * 2] * right.x + _headVerts[v * 2 + 1] * right.y;
                    float y = _headVerts[v * 2] * up.x + _headVerts[v * 2 + 1] * up.y;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            bool any = maxX > minX && maxY > minY;
            centre = any ? new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f) : Vector2.zero;
            size = any ? new Vector2(maxX - minX, maxY - minY) : Vector2.zero;
            return any;
        }

        // THE WHOLE RIG: every visible attachment, for a creature with no head bone
        // to frame (RenderPortrait's `whole`)
        private static bool RigBox(Spine.Skeleton skeleton, Vector2 right, Vector2 up, out Vector2 centre, out Vector2 size)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var slots = skeleton.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                Spine.Slot slot = slots.Items[i];
                if (slot == null || slot.A <= 0.01f)
                {
                    continue;
                }
                int count;
                if (slot.Attachment is Spine.RegionAttachment region)
                {
                    count = 4;
                    region.ComputeWorldVertices(slot.Bone, _headVerts, 0, 2);
                }
                else if (slot.Attachment is Spine.MeshAttachment mesh)
                {
                    int floats = mesh.WorldVerticesLength;
                    if (_headVerts.Length < floats) { _headVerts = new float[floats]; }
                    mesh.ComputeWorldVertices(slot, _headVerts);
                    count = floats / 2;
                }
                else
                {
                    continue;
                }
                for (int v = 0; v < count; v++)
                {
                    float x = _headVerts[v * 2] * right.x + _headVerts[v * 2 + 1] * right.y;
                    float y = _headVerts[v * 2] * up.x + _headVerts[v * 2 + 1] * up.y;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            bool any = maxX > minX && maxY > minY;
            centre = any ? new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f) : Vector2.zero;
            size = any ? new Vector2(maxX - minX, maxY - minY) : Vector2.zero;
            return any;
        }

        private static Camera _portraitCam;
        // one past the groups: the target's live portrait (LivePortrait), the dock's
        // and the world marker's, in a texture of its own so a roster snapshot
        // taken in GroupNone's never shows in it
        private const int PortraitLive = GroupCount;
        private static readonly RenderTexture[] _portraitTex = new RenderTexture[GroupCount + 1];
        private static readonly SheetRow[] _portraitRow = new SheetRow[GroupCount];
        private static readonly bool[] _portraitOk = new bool[GroupCount];
        private static readonly List<Renderer> _portraitRenderers = new List<Renderer>();
        private static readonly List<int> _portraitLayers = new List<int>();

        // The oval's stencil and the render in it, placed by the header; true when
        // the last render of this group's character succeeded
        // THE PORTRAIT IS A PHOTO: the roster's snapshot of them (Shed.cs), in the
        // same white border, on a light backdrop, with a shadow under it and set a
        // little askew - a still, attached to the sheet like a passport photo,
        // rather than the live render in an oval it was. The same snapshot as
        // their roster strip, so the two pictures of a person always agree - and
        // pinned to the sheet at its top as the strip is to the cork, the pin in
        // their kind's colour.
        private const float PortraitTilt = -1.6f;

        private static bool PlacePortrait(SheetRow row, float x, float y, float w, float h)
        {
            CommonStates who = row.Host != null ? row.Host.Owner : null;
            if (who == null)
            {
                HidePortrait(row);
                return false;
            }
            float side = Mathf.Clamp(w * 0.06f, 1.2f, 2.2f), foot = side * 2.2f;
            if (row.PortraitMask == null)
            {
                row.PortraitMask = Theme.KitImage(row.Go.transform, "Portrait", "kit_shed_paper", 8, Color.white);
                row.PortraitMask.raycastTarget = false;
                Shadow drop = row.PortraitMask.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.4f);
                drop.effectDistance = new Vector2(0.6f, -0.9f);
                var back = Theme.KitImage(row.PortraitMask.transform, "Back", "kit_dossier_chip", 0, SnapBack);
                back.sprite = null;
                back.raycastTarget = false;
                var br = back.rectTransform;
                br.anchorMin = Vector2.zero;
                br.anchorMax = Vector2.one;
                var face = Owned.New("Face");
                face.transform.SetParent(back.transform, false);
                row.PortraitRaw = face.AddComponent<RawImage>();
                row.PortraitRaw.raycastTarget = false;
                var fr = row.PortraitRaw.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = Vector2.zero;
                fr.offsetMax = Vector2.zero;
            }
            var backRect = (RectTransform)row.PortraitRaw.transform.parent;
            backRect.offsetMin = new Vector2(side, foot);
            backRect.offsetMax = new Vector2(-side, -side);
            // under the art layer, where the favourite's star is - moved only when it
            // is above it: re-slotting at the layer's index every paint swapped the
            // two each time, and they flickered over and under each other
            if (row.FormArt != null)
            {
                int art = row.FormArt.GetSiblingIndex();
                if (row.PortraitMask.transform.GetSiblingIndex() > art)
                {
                    row.PortraitMask.transform.SetSiblingIndex(art);
                }
            }
            Theme.Place(row.PortraitMask, x, y, w, h);
            row.PortraitMask.rectTransform.localRotation = Quaternion.Euler(0f, 0f, PortraitTilt);
            if (!row.PortraitMask.gameObject.activeSelf)
            {
                row.PortraitMask.gameObject.SetActive(true);
            }
            if (row.PortraitPin == null)
            {
                row.PortraitPin = ToolShed.MakePin(row.Go.transform, "PortraitPin");
            }
            Color kind = NPCColor(who) ?? (_mn != null && _mn.uiMN != null ? _mn.uiMN.GetNPCColor(who.npcID) : Theme.Ink);
            float pin = Mathf.Clamp(w * 0.32f, 7f, 11f);
            ToolShed.PlacePin(row.PortraitPin, x + (w - pin) * 0.5f, y - pin * 0.35f, pin, kind, true);
            Texture2D snap = Snapshot(who);
            row.PortraitRaw.texture = snap;
            row.PortraitRaw.enabled = snap != null;
            if (snap != null)
            {
                // the camera frames a head in 160x192: the photo shows the middle of it
                float aspect = (w - 2f * side) / Mathf.Max(1f, h - side - foot);
                float v = Mathf.Clamp01(aspect * PortraitPxH / PortraitPxW);
                row.PortraitRaw.uvRect = new Rect(0f, (1f - v) * 0.35f, 1f, v);
            }
            return snap != null;
        }

        private static void HidePortrait(SheetRow row)
        {
            if (row.PortraitMask != null && row.PortraitMask.gameObject.activeSelf)
            {
                row.PortraitMask.gameObject.SetActive(false);
            }
            ToolShed.PlacePin(row.PortraitPin, 0f, 0f, 0f, Color.white, false);
        }

        // Every frame, for each dossier whose header is showing: the character as
        // they stand now. A change between a render and none repaints the sheet, so
        // the glyph comes and goes with it.
        private static void TickPortraits()
        {
            for (int g = 0; g < GroupCount; g++)
            {
                SheetRow row = _portraitRow[g];
                if (row == null)
                {
                    continue;
                }
                if (row.PortraitMask == null || !row.PortraitMask.gameObject.activeInHierarchy
                    || (row.DrewKind != LineFormHead && row.DrewKind != LineFormBand) || row.Host == null || row.Host.Group != g)
                {
                    _portraitRow[g] = null;
                    continue;
                }
                bool ok = row.Host.Owner != null && RenderPortrait(g, row.Host.Owner, true);
                if (ok != _portraitOk[g])
                {
                    _portraitOk[g] = ok;
                    _nextSheetPaint = 0f;
                }
                row.PortraitRaw.texture = ok ? _portraitTex[g] : null;
                row.PortraitRaw.enabled = ok;
            }
        }

        // For the harness: the numbers the framing is made of - yours, or the first
        // character in the scene whose name is `name`
        internal static string PortraitDump(string name)
        {
            CommonStates who = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            if (!string.IsNullOrEmpty(name))
            {
                who = null;
                foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
                {
                    if (c != null && string.Equals(c.charaName, name, System.StringComparison.OrdinalIgnoreCase)) { who = c; break; }
                }
            }
            if (who == null || who.anim == null || who.anim.skeleton == null) { return "no skeleton for '" + name + "'"; }
            var anim = who.anim;
            var sk = anim.skeleton;
            Spine.Slot eye = sk.FindSlot("Eye");
            MeshRenderer body = anim.GetComponent<MeshRenderer>();
            Camera main = Camera.main;
            return "dataH=" + (sk.Data != null ? sk.Data.Height.ToString("0.###") : "?")
                + " lossy=" + anim.transform.lossyScale.ToString("0.####")
                + " skScale=" + sk.ScaleX.ToString("0.###") + "," + sk.ScaleY.ToString("0.###")
                + " eye=" + (eye != null ? eye.Bone.WorldX.ToString("0.##") + "," + eye.Bone.WorldY.ToString("0.##") : "none")
                + " eyeW=" + (eye != null ? anim.transform.TransformPoint(new Vector3(eye.Bone.WorldX, eye.Bone.WorldY, 0f)).ToString("0.###") : "-")
                + " root=" + anim.transform.position.ToString("0.###")
                + " bounds=" + (body != null ? body.bounds.size.ToString("0.###") + "@" + body.bounds.center.ToString("0.###") : "-")
                + " layer=" + anim.gameObject.layer
                + " main=" + (main != null ? (main.orthographic ? "ortho " + main.orthographicSize.ToString("0.##") : "persp " + main.fieldOfView.ToString("0.#"))
                    + " rot=" + main.transform.eulerAngles.ToString("0.#") : "none")
                + " shader=" + (body != null && body.sharedMaterial != null ? body.sharedMaterial.shader.name : "-")
                + " skA=" + sk.A.ToString("0.##") + " root=" + sk.RootBone.WorldX.ToString("0.#") + "," + sk.RootBone.WorldY.ToString("0.#")
                + " headBox=" + HeadBoxText(sk)
                + " turn=" + ((sk.FindBone("head") ?? sk.FindBone("Head")) is Spine.Bone hb ? HeadTurn(sk, hb).ToString("0.#") : "-")
                + " | bones: " + BoneList(sk);
        }

        // For the harness: every named character in the scene rendered as the
        // dossier would, side by side into one PNG - the check for a framing
        // across rigs without opening a sheet per character. "path" or
        // "path|Name;Name" to pick whom (the harness splits its args on commas);
        // "path|Name|clear" keeps the render's own alpha, for art drawn behind it;
        // "upright" among the flags renders as the stills do (Upright).
        internal static string PortraitSheet(string arg)
        {
            string[] parts = (arg ?? string.Empty).Split('|');
            string path = parts[0];
            var only = parts.Length > 1 && parts[1].Length > 0 ? new HashSet<string>(parts[1].Split(';'), System.StringComparer.OrdinalIgnoreCase) : null;
            bool clear = parts.Length > 2 && parts[2].Contains("clear");
            bool upright = parts.Length > 2 && parts[2].Contains("upright");
            var who = new List<CommonStates>();
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c != null && !string.IsNullOrEmpty(c.charaName) && c.anim != null
                    && (only == null || only.Contains(c.charaName))) { who.Add(c); }
            }
            who.Sort((a, b) => string.CompareOrdinal(a.charaName, b.charaName));
            int cols = 8, n = Mathf.Min(who.Count, 96);
            int rows = Mathf.Max(1, (n + cols - 1) / cols);
            var sheet = new Texture2D(cols * PortraitPxW, rows * PortraitPxH, TextureFormat.RGBA32, false);
            var paper = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < paper.Length; i++) { paper[i] = clear ? new Color32(0, 0, 0, 0) : new Color32(236, 226, 204, 255); }
            sheet.SetPixels32(paper);
            var names = new System.Text.StringBuilder();
            var cell = new Texture2D(PortraitPxW, PortraitPxH, TextureFormat.RGBA32, false);
            for (int i = 0; i < n; i++)
            {
                bool ok = RenderPortrait(GroupNone, who[i], upright);
                names.Append(i).Append(':').Append(who[i].charaName).Append(ok ? "" : "(none)");
                Spine.Skeleton sk = who[i].anim.skeleton;
                Spine.Bone hb = sk != null ? sk.FindBone("head") ?? sk.FindBone("Head") : null;
                if (upright && hb != null) { names.Append(" turn ").Append(HeadTurn(sk, hb).ToString("0")); }
                names.Append(' ');
                if (!ok) { continue; }
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _portraitTex[GroupNone];
                cell.ReadPixels(new Rect(0, 0, PortraitPxW, PortraitPxH), 0, 0);
                cell.Apply();
                RenderTexture.active = was;
                Color[] px = cell.GetPixels();
                for (int k = 0; k < px.Length && !clear; k++)
                {
                    // over the paper, as the oval shows it
                    Color bg = new Color(236 / 255f, 226 / 255f, 204 / 255f, 1f);
                    px[k] = new Color(px[k].r + bg.r * (1f - px[k].a), px[k].g + bg.g * (1f - px[k].a), px[k].b + bg.b * (1f - px[k].a), 1f);
                }
                sheet.SetPixels((i % cols) * PortraitPxW, (rows - 1 - i / cols) * PortraitPxH, PortraitPxW, PortraitPxH, px);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.Destroy(sheet);
            Object.Destroy(cell);
            return names.ToString();
        }

        private static string HeadBoxText(Spine.Skeleton sk)
        {
            Spine.Bone head = sk.FindBone("head") ?? sk.FindBone("Head");
            if (head == null) { return "no head bone"; }
            var s = new System.Text.StringBuilder();
            s.Append("head@").Append(head.WorldX.ToString("0.#")).Append(',').Append(head.WorldY.ToString("0.#"));
            Spine.Slot skull = sk.FindSlot("Head");
            if (skull != null && SlotBox(skull, out Vector2 sc, out Vector2 sz))
            {
                s.Append(" skull c=").Append(sc.x.ToString("0.#")).Append(',').Append(sc.y.ToString("0.#"))
                    .Append(" size=").Append(sz.x.ToString("0.#")).Append('x').Append(sz.y.ToString("0.#"));
            }
            if (HeadBox(sk, head, out Vector2 c, out Vector2 z))
            {
                s.Append(" box c=").Append(c.x.ToString("0.#")).Append(',').Append(c.y.ToString("0.#"))
                    .Append(" size=").Append(z.x.ToString("0.#")).Append('x').Append(z.y.ToString("0.#"));
            }
            else { s.Append(" box none"); }
            // which slots contribute, and how many of their vertices
            var slots = sk.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                Spine.Slot slot = slots.Items[i];
                if (slot == null || !(slot.Attachment is Spine.MeshAttachment mesh) || mesh.Bones == null) { continue; }
                int[] wb = mesh.Bones; float[] sv = mesh.Vertices; int b = 0, w = 0, hits = 0, total = 0;
                while (b < wb.Length)
                {
                    int n = wb[b++]; float best = -1f; int strongest = -1;
                    for (int k = 0; k < n && b < wb.Length; k++, b++, w += 3)
                    {
                        float weight = w + 2 < sv.Length ? sv[w + 2] : 0f;
                        if (weight > best) { best = weight; strongest = wb[b]; }
                    }
                    total++;
                    if (strongest >= 0 && strongest < sk.Bones.Count && sk.Bones.Items[strongest] == head) { hits++; }
                }
                if (hits > 0) { s.Append(" [").Append(slot.Data.Name).Append(' ').Append(hits).Append('/').Append(total).Append(']'); }
            }
            return s.ToString();
        }

        private static string BoneList(Spine.Skeleton sk)
        {
            var sb = new System.Text.StringBuilder();
            var bones = sk.Bones;
            for (int i = 0; i < bones.Count && i < 70; i++)
            {
                Spine.Bone b = bones.Items[i];
                sb.Append(b.Data.Name).Append('(').Append(b.WorldX.ToString("0")).Append(',').Append(b.WorldY.ToString("0")).Append(") ");
            }
            return sb.ToString();
        }

        // `whole`: a rig with no head bone is framed entire instead of refused
        private static bool RenderPortrait(int group, CommonStates who, bool upright = false, bool whole = false)
        {
            Spine.Unity.SkeletonAnimation anim = who.anim;
            Camera main = Camera.main;
            if (anim == null || anim.skeleton == null || main == null || !anim.gameObject.activeInHierarchy)
            {
                return false;
            }
            // FRAMED ON THE HEAD ITSELF: the box around every attachment hung on
            // the head bone or below it (face, eyes, hair), in the pose of this
            // frame. Constants in skeleton units framed one rig and missed the next
            // - Sally's units are half of Man's, so she came out whole and tiny, and
            // Takumi lying down put his head beside his root rather than above it
            // (for some characters the portrait on the sheets came out too small).
            // The box is each rig measuring itself.
            Spine.Skeleton skeleton = anim.skeleton;
            Spine.Bone head = skeleton.FindBone("head") ?? skeleton.FindBone("Head");
            float scale = Mathf.Abs(anim.transform.lossyScale.y);
            Transform view = main.transform;
            Vector2 right = Vector2.right, up = Vector2.up;
            Quaternion look = view.rotation;
            if (head != null && upright)
            {
                Upright(skeleton, head, anim.transform, view, ref right, ref up, ref look);
            }
            Vector2 boxCentre = Vector2.zero, boxSize = Vector2.zero;
            bool boxed = head != null ? HeadBox(skeleton, head, right, up, out boxCentre, out boxSize)
                : whole && RigBox(skeleton, right, up, out boxCentre, out boxSize);
            if (scale < 1e-5f || !boxed)
            {
                return false;   // a creature without a head to frame: the glyph
            }
            float aspect = PortraitPxW / (float)PortraitPxH;
            float half = Mathf.Max(boxSize.y, boxSize.x / aspect) * PortraitFill;
            // a little under the box's middle, so the chin and the neck have room
            Vector2 framed = right * boxCentre.x + up * (boxCentre.y - boxSize.y * PortraitDrop);
            Vector3 centre = anim.transform.TransformPoint(new Vector3(framed.x, framed.y, 0f));
            if (_portraitCam == null)
            {
                var go = Owned.New("IslandUI Portrait Camera");
                _portraitCam = go.AddComponent<Camera>();
                _portraitCam.enabled = false;
                _portraitCam.orthographic = true;
                _portraitCam.clearFlags = CameraClearFlags.SolidColor;
                _portraitCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _portraitCam.cullingMask = 1 << PortraitLayer;
                _portraitCam.nearClipPlane = 0.01f;
                _portraitCam.farClipPlane = 200f;
                _portraitCam.allowHDR = false;
                _portraitCam.useOcclusionCulling = false;
                var keyGo = Owned.New("IslandUI Portrait Light");
                keyGo.transform.SetParent(go.transform, false);
                _portraitKey = keyGo.AddComponent<Light>();
                _portraitKey.type = LightType.Directional;
                _portraitKey.color = new Color(1f, 0.97f, 0.92f, 1f);
                _portraitKey.intensity = 1.05f;
                _portraitKey.shadows = LightShadows.None;
                _portraitKey.renderMode = LightRenderMode.ForcePixel;
                _portraitKey.cullingMask = 1 << PortraitLayer;
                _portraitKey.enabled = false;
            }
            RenderTexture tex = _portraitTex[group];
            if (tex == null || !tex.IsCreated())
            {
                tex = new RenderTexture(PortraitPxW, PortraitPxH, 16, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "IslandUI portrait " + group,
                };
                tex.Create();
                _portraitTex[group] = tex;
            }
            // the face with the hair above it and a little of the neck, seen the way
            // the game's camera sees the character
            _portraitCam.orthographicSize = half * scale;
            _portraitCam.aspect = aspect;
            _portraitCam.transform.rotation = look;
            _portraitCam.transform.position = centre - view.forward * 60f;
            _portraitCam.targetTexture = tex;
            _portraitRenderers.Clear();
            anim.GetComponentsInChildren(false, _portraitRenderers);
            _portraitLayers.Clear();
            for (int i = 0; i < _portraitRenderers.Count; i++)
            {
                _portraitLayers.Add(_portraitRenderers[i].gameObject.layer);
                _portraitRenderers[i].gameObject.layer = PortraitLayer;
            }
            // A STUDIO LIGHT, NOT THE SCENE'S. The character's shader is per-pixel
            // lit (Spine/Outline/Sprite/Pixel Lit), so at night a portrait lit by the
            // world is the silhouette the world shows. A dossier's
            // photograph is taken in good light: one key light of ours, along the
            // camera's view, lighting only the portrait's layer and only while it
            // renders - the scene's own lights do not reach that layer.
            // a little above and to the side of the game's camera, whichever way
            // ours is turned: the sprite stands in the world, and a light turned
            // with an upright still lit a lying face darker than a standing one
            _portraitKey.transform.rotation = view.rotation * PortraitKeyTurn;
            _portraitKey.enabled = true;
            try
            {
                _portraitCam.Render();
            }
            finally
            {
                _portraitKey.enabled = false;
                for (int i = 0; i < _portraitRenderers.Count; i++)
                {
                    if (_portraitRenderers[i] != null)
                    {
                        _portraitRenderers[i].gameObject.layer = _portraitLayers[i];
                    }
                }
                _portraitCam.targetTexture = null;
            }
            return true;
        }

        private static Light _portraitKey;
        private static readonly Quaternion PortraitKeyTurn = Quaternion.Euler(12f, -18f, 0f);

        // UPRIGHT ON A STILL (Forgejo #53): a body lies as the game lays it, and its
        // face lay on its side on every photograph of it. A still - the target dock's
        // card, the combat plaque, the sheet, the roster, the map's pins: everything
        // Snapshot gives - turns its camera with the head instead, so a face lying
        // down stands up in the frame and the box is measured along the head's own
        // axes. The target's world marker renders live and keeps the pose. The head's
        // turn is its bone's from the rig's setup pose, which stands; a head turned
        // less than PortraitUprightFrom (an idle's nod, a stoop) keeps the camera and
        // the axes it had, so a standing person's photograph does not change.
        private const float PortraitUprightFrom = 45f;
        private static readonly Dictionary<Spine.SkeletonData, Spine.Skeleton> StandingRigs = new Dictionary<Spine.SkeletonData, Spine.Skeleton>();

        private static bool Upright(Spine.Skeleton skeleton, Spine.Bone head, Transform body, Transform view,
            ref Vector2 right, ref Vector2 up, ref Quaternion look)
        {
            float turn = HeadTurn(skeleton, head);
            if (float.IsNaN(turn)) { return false; }
            float rad = turn * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            var headUp = new Vector2(-sin, cos);
            // the head's up as the game's camera sees it: a lying pose may be the
            // bones' (a down animation) or the transform's, and this reads both
            Vector3 seen = Vector3.ProjectOnPlane(body.TransformVector(new Vector3(headUp.x, headUp.y, 0f)), view.forward);
            if (seen.sqrMagnitude < 1e-10f) { return false; }
            if (Vector3.Angle(seen, view.up) < PortraitUprightFrom) { return false; }
            right = new Vector2(cos, sin);
            up = headUp;
            look = Quaternion.LookRotation(view.forward, seen);
            return true;
        }

        // the head bone's turn now from its turn in the rig's setup pose, in degrees;
        // NaN when the rig has no such bone standing
        private static float HeadTurn(Spine.Skeleton skeleton, Spine.Bone head)
        {
            if (!StandingRigs.TryGetValue(skeleton.Data, out Spine.Skeleton standing))
            {
                standing = new Spine.Skeleton(skeleton.Data);
                StandingRigs[skeleton.Data] = standing;
            }
            // flipped as the live one is, so both turns are read the same way round
            standing.ScaleX = skeleton.ScaleX;
            standing.ScaleY = skeleton.ScaleY;
            standing.UpdateWorldTransform();
            Spine.Bone rest = standing.FindBone(head.Data.Name);
            return rest != null ? Mathf.DeltaAngle(rest.WorldRotationX, head.WorldRotationX) : float.NaN;
        }

        // ---------- the rows the form shares with the plain sheet ----------

        private const float FormScaleCaptionW = 46f;
        private const float FormScaleValueW = 27f;   // "+10.0", the chase range reading, fits

        private static void LayoutFormScale(SheetRow row, float width, float h)
        {
            h = FormScaleRowH;   // the scale keeps its height: a line under it (Value2) adds below
            float trackX = SheetPadX + FormScaleCaptionW;
            float valueX = width - SheetPadX - FormScaleValueW;
            float trackW = Mathf.Max(10f, valueX - 8f - trackX);
            float cy = h * 0.5f + 0.5f;
            row.TrackX = trackX;
            row.TrackW = trackW;
            Theme.Place(row.Track, trackX, cy - 0.35f, trackW, 0.7f);
            Theme.Place(row.Fill, trackX, cy - 0.35f, 0f, 0.7f);
            if (row.Knob != null)
            {
                Theme.Place(row.Knob, trackX, cy - SheetKnob * 0.5f, SheetKnob, SheetKnob);
            }
            Theme.Place(row.Label, SheetPadX, 0f, FormScaleCaptionW - 2f, h);
            Theme.Place(row.Value, valueX, cy - 5.5f, FormScaleValueW - 2.5f, 11f);
        }

        // After the plain paint: what the form adds to a shared row, and what it
        // changes on it. A kind with nothing added still ends the pools, so a row
        // that was a box row last time shows no stale boxes.
        private static void PaintFormHybrid(SheetRow row, SheetLine line)
        {
            BeginForm(row);
            var size = ((RectTransform)row.Go.transform).sizeDelta;
            float w = size.x;
            float h = size.y;
            int planks = row.Planks != null && (line.Kind == LineActs || line.Kind == LinePlace
                || line.Kind == LinePose || line.Kind == LineRosterBar) ? row.Planks.Length : 0;
            switch (line.Kind)
            {
                case LineSlider:
                {
                    // the printed scale: a caption, the ruled line with its ticks, the
                    // reading in a box; the knob is the plain row's own
                    // dimmed where it does not bite: the place's two scales while
                    // following, which in vanilla overrides the place
                    StyleCaption(row.Label, true);
                    bool dim = !line.Enabled;
                    Color ink = dim ? Theme.DossierSoft : Theme.DossierInk;
                    if (row.Track.sprite != null) { row.Track.sprite = null; }
                    row.Track.type = Image.Type.Simple;
                    row.Track.color = ink;
                    if (row.Fill.gameObject.activeSelf) { row.Fill.gameObject.SetActive(false); }
                    float cy = FormScaleRowH * 0.5f + 0.5f;
                    for (int k = 0; k <= 10; k++)
                    {
                        float tx = row.TrackX + row.TrackW * k / 10f;
                        float t = k % 5 == 0 ? 3f : 1.8f;
                        FormImage(row, null, 0, ink, tx - 0.2f, cy - t, 0.4f, t);
                    }
                    DrawScaleFill(row, line, cy, dim);
                    DrawScaleLine(row, line, dim);
                    float valueX = w - SheetPadX - FormScaleValueW;
                    FormImage(row, "kit_dossier_box", 6, ink, valueX, cy - 5.5f, FormScaleValueW, 11f);
                    if (row.Value.color != ink) { row.Value.color = ink; }
                    if (row.Knob != null)
                    {
                        row.Knob.transform.SetAsLastSibling();
                        row.Knob.color = dim ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                    }
                    break;
                }
                case LineDropdown:
                    StyleCaption(row.Label);
                    if (line.Style == StyleDropFacing)
                    {
                        row.FormArgs = line.Args;
                        planks = DrawFacing(row, line, w);
                    }
                    break;
                case LineRelation:
                    // the ruling under each name
                    FormRule(row, Theme.DossierRule, SheetPadX, h - 0.5f, w - 2f * SheetPadX);
                    break;
            }
            EndForm(row, planks);
        }

        // `fit`: the caption has a box of its own, the scale's left of its line, and a longer
        // language shrinks to it rather than run under the line (Forgejo #19)
        private static void StyleCaption(TextMeshProUGUI label, bool fit = false)
        {
            if (fit)
            {
                Theme.FitLanguage(label, CaptionSize);
            }
            else
            {
                if (label.enableAutoSizing) { label.enableAutoSizing = false; }
                if (label.fontSize != CaptionSize) { label.fontSize = CaptionSize; }
            }
            if (label.fontStyle != (FontStyles.Bold | FontStyles.UpperCase)) { label.fontStyle = FontStyles.Bold | FontStyles.UpperCase; }
            if (label.characterSpacing != CaptionSpacing) { label.characterSpacing = CaptionSpacing; }
            if (label.color != Theme.DossierSoft) { label.color = Theme.DossierSoft; }
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }
}
