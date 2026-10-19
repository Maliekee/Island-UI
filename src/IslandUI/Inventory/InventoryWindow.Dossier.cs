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
    // Three layers, each its own part of the window:
    //   the FRAME  - the group frame's back is the front folder (kraft), its field
    //                the sheet (paper with the printed double border baked in);
    //   the TABS   - DossierTabs draws the folders behind and every page's tab;
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
                || kind == LineField || kind == LineActions || kind == LineMore
                || kind == LineMoodNow || kind == LineMoodCause;
        }

        // ---------- the frame ----------

        // The paper's sprite carries a 4 px shadow margin; its edge sits 3.5 units in
        // from the folder's, which is the kraft rim you see around the sheet
        private const float DossierPaperInset = 2.25f;
        private const float ClipW = 6.5f;
        private const float ClipH = 20f;

        private static void SkinDossierFrame(Plate plate, int group)
        {
            plate.Back.sprite = Theme.Kit("kit_dossier_folder", 16);
            plate.Back.type = Image.Type.Sliced;
            plate.Back.pixelsPerUnitMultiplier = Theme.KitPpu;
            plate.Back.color = Color.white;
            plate.Edge.enabled = false;
            EnsureField(plate);
            Theme.KitSkin(plate.Field, "kit_dossier_paper", 24);
            plate.Field.preserveAspect = false;
            plate.Field.color = Color.white;
            if (!plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(true);
            }
            EnsureRail(plate);
            plate.Rail.enabled = false;
            // the close on the stack's top edge, not the front folder's:
            // the folders behind stand a Lift each above it
            Aspect[] pages = _footSets[group] ?? OwnAspects;
            EnsureClose(plate, DossierTabs.Lift * Mathf.Max(0, pages.Length - 1));
            if (!plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(true);
            }
            // THEIRS IS PAPERCLIPPED. Yours wore a PERSONAL stamp for a while,
            // and carries no mark now: the name and the portrait say whose it is.
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

        // Once the frame has its size: the sheet inside the folder, and a target's
        // paperclip over its top edge, in the gap between the last tab and the close
        // chip
        private static void PlaceDossierFrame(Plate frame, int group)
        {
            Vector2 size = frame.Window.Rect.sizeDelta;
            if (frame.Field != null)
            {
                var fr = (RectTransform)frame.Field.transform;
                fr.anchorMin = fr.anchorMax = Vector2.zero;
                fr.pivot = Vector2.zero;
                fr.anchoredPosition = new Vector2(DossierPaperInset, DossierPaperInset);
                fr.sizeDelta = new Vector2(Mathf.Max(0f, size.x - 2f * DossierPaperInset),
                    Mathf.Max(0f, size.y - 2f * DossierPaperInset));
            }
            if (frame.Mark == null || group == GroupOwnSheet)
            {
                return;
            }
            var mr = frame.Mark.rectTransform;
            mr.sizeDelta = new Vector2(ClipW, ClipH);
            mr.anchoredPosition = new Vector2(size.x - 42f, size.y - ClipH + 6f);
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
            float frameLeft = _colPivotX[c] > 0.5f ? _colX[c] - size.x : _colX[c];
            row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(_colAnchorX[c], 0f);
            row.Rect.pivot = Vector2.zero;
            row.Rect.anchoredPosition = new Vector2(frameLeft, _groupHi[group]);
            row.Set(icons, labels, active, size.x, size.y, tags, kinds);
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
                    WriteFormSkills(lines);
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
            TextManager tm = _mn != null ? _mn.textMN : null;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            bool surgeries = SaveManager.SaveSettingStatic.trackSurgeries == 1 && who.status != null && who.status.Length > 6;
            string kind = KindName(npcs, who);
            var captions = new List<string>();
            var values = new List<string>();
            var fracs = new List<float>();
            var tips = new List<string>();
            if (!own && !string.IsNullOrEmpty(kind)
                && !string.Equals(kind, who.charaName, System.StringComparison.OrdinalIgnoreCase))
            {
                captions.Add("KIND"); values.Add(kind); fracs.Add(0.62f); tips.Add(null);
            }
            captions.Add("AGE"); values.Add(who.age.ToString()); fracs.Add(captions.Count > 1 ? 0.38f : 0.5f); tips.Add(null);
            if (own && surgeries)
            {
                captions.Add("SURGERIES"); values.Add(who.status[6].ToString()); fracs.Add(0.5f); tips.Add(TipSurgeries);
            }
            // the fields' tips, then the medallion's and the exp's
            tips.Add(TipLevel);
            tips.Add(own ? TipExpOwn : TipExpTheirs);
            string name = !string.IsNullOrEmpty(who.charaName) ? who.charaName : (kind ?? "?");
            lines.Add(new SheetLine
            {
                Kind = LineFormHead, Label = name, Label2 = "LEVEL", Value = who.level.ToString(),
                Value2 = who.exp.ToString("0") + " / " + who.maxExp.ToString("0"),
                Frac = who.maxExp > 0 ? (float)(who.exp / who.maxExp) : 0f,
                Options = captions.ToArray(), Values = values.ToArray(), Fracs = fracs.ToArray(),
                Tips = tips.ToArray(), Icon = AspectIcon(Aspect.Profile), Enabled = true,
            });

            WriteFormVitals(lines, who, own);

            // what you have set about them: two checkboxes on one line
            var checks = new SheetLine { Kind = LineChecks, Enabled = true };
            if (own)
            {
                checks.Options = new[] { "Always run", "Show name in world" };
                checks.Cans = new[] { _mn != null && _mn.gameMN != null && _mn.gameMN.toggleRun, who.nameVisible != null };
                checks.Args = new[] { 91, ToggleNameShown };
                checks.Glyphs = new[] { GlyphCheck, GlyphEye };
                checks.Tips = new[] { TipAlwaysRun, TipShowNameOwn };
            }
            // the main characters have no favourite star in vanilla (UIManager hides
            // favButton for npcID 0 and 1), and the other one reaches this sheet
            // from our roster now (Partner)
            else if ((who.employ == CommonStates.Employ.Friend || who.employ == CommonStates.Employ.Favorite)
                && who.npcID > 1)
            {
                checks.Options = new[] { "Show name in world", "Favorite in roster" };
                checks.Cans = new[] { who.nameVisible != null, who.employ == CommonStates.Employ.Favorite };
                checks.Args = new[] { ToggleNameShown, ToggleFavourite };
                checks.Glyphs = new[] { GlyphEye, GlyphStar };
                checks.Tips = new[] { TipShowName, TipFavourite };
            }
            else
            {
                checks.Options = new[] { "Show name in world" };
                checks.Cans = new[] { who.nameVisible != null };
                checks.Args = new[] { ToggleNameShown };
                checks.Glyphs = new[] { GlyphEye };
                checks.Tips = new[] { TipShowName };
            }
            lines.Add(checks);

            if (who.status != null && who.status.Length >= StatNames.Length)
            {
                SheetLine points = Section("ATTRIBUTES", who.statusPoint + " points");
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
                    caps[i] = StatNames[i].ToUpperInvariant();
                    vals[i] = who.status[i].ToString();
                    foots[i] = "next " + cost + " pt";
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

            lines.Add(Section("COMBAT"));
            string attack = who.attack.ToString("F1");
            if (who.equip != null && who.equip.Length > 1 && who.equip[1] != null)
            {
                attack += " (" + who.equip[1].attack.ToString("0") + ")";
            }
            bool klass = who.status != null && who.status.Length > 7 && who.status[7] > 0;
            lines.Add(new SheetLine
            {
                Kind = LineBoxes, Style = StyleReading, Enabled = true,
                Options = klass ? new[] { "ATTACK", "GUARD", "SPEED", "CLASS" } : new[] { "ATTACK", "GUARD", "SPEED" },
                Values = klass
                    ? new[] { attack, GuardOf(who).ToString("0.0"), who.MoveSpeed(false).ToString("0.00"), who.status[7].ToString() }
                    : new[] { attack, GuardOf(who).ToString("0.0"), who.MoveSpeed(false).ToString("0.00") },
                Tips = new[] { TipAttack, own ? TipGuardOwn : TipGuardTheirs, TipSpeed, TipClass },
            });

            // the facts, as the blanks of a form: filled where the game knows them;
            // each carries its tip third
            lines.Add(Section("PROFILE"));
            var facts = new List<string[]>
            {
                new[] { "Personality", PersonText(tm, who.person, 0), TipPersonality },
                new[] { "Likes", PersonText(tm, who.person, 1), TipLikes },
                new[] { "Dislikes", PersonText(tm, who.person, 2), TipDislikes },
            };
            if (!own)
            {
                string father = FriendName(npcs, who.status, 8);
                string mother = FriendName(npcs, who.status, 9);
                if (!string.IsNullOrEmpty(father)) { facts.Add(new[] { "Father", father, TipParent }); }
                if (!string.IsNullOrEmpty(mother)) { facts.Add(new[] { "Mother", mother, TipParent }); }
                if (surgeries) { facts.Add(new[] { "Surgeries", who.status[6].ToString(), TipSurgeries }); }
            }
            for (int i = 0; i < facts.Count; i += 2)
            {
                bool two = i + 1 < facts.Count;
                SheetLine field = two
                    ? Field(facts[i][0], facts[i][1], facts[i + 1][0], facts[i + 1][1])
                    : Field(facts[i][0], facts[i][1]);
                field.Tips = two ? new[] { facts[i][2], facts[i + 1][2] } : new[] { facts[i][2] };
                lines.Add(field);
            }
        }

        // HOW THEY ARE: the six boxes, HP · stun · food over water · libido · moral,
        // theirs as well as yours (the indicator is there too, but a target's
        // vitals read like the player's). HP's and stun's bars carry the unit's
        // pool marks - a thin one per 100 of max, a heavy one per 1000 - and
        // nothing else about the boxes changed. A pregnancy takes a third row.
        // Their love for you is not here: it is the Social page's first row,
        // because it belongs to the relations.
        private static void WriteFormVitals(List<SheetLine> lines, CommonStates who, bool own)
        {
            lines.Add(Section("VITALS"));
            SheetLine first = Boxes(StyleVital,
                new[] { "HP", "STUN", "FOOD" },
                new[] { who.life.ToString("0") + "/" + who.maxLife.ToString("0"), who.faint.ToString("0"), who.food.ToString("0") },
                new[] { Frac((float)who.life, (float)who.maxLife), Frac((float)who.faint, (float)who.maxFaint), Frac(who.food, who.maxFood) },
                new[] { LifeTint, StunTint, FoodTint });
            first.Pools = new[] { (float)who.maxLife, (float)who.maxFaint, 0f };
            first.Tips = own ? new[] { TipHpOwn, TipStunOwn, TipFoodOwn } : new[] { TipHpTheirs, TipStunTheirs, TipFoodTheirs };
            lines.Add(first);
            SheetLine second = Boxes(StyleVital,
                new[] { "WATER", "LIBIDO", "MORAL" },
                new[] { who.water.ToString("0"), who.libido.ToString("0"), who.moral.ToString("0") },
                new[] { Frac(who.water, who.maxWater), Frac(who.libido, who.maxLibido), Mathf.Clamp01(Mathf.Abs(who.moral) / 100f) },
                new[] { WaterTint, LibidoTint, MoralTint(who) });
            // your libido: nothing in the game reads the player's, so no tip says it does
            second.Tips = own ? new[] { TipWaterOwn, null, TipMoralOwn } : new[] { TipWaterTheirs, TipLibidoTheirs, TipMoralTheirs };
            lines.Add(second);
            if (who.pregnant != null && who.pregnant.Length > 1 && who.pregnant[0] != -1)
            {
                float months = 12f - who.pregnant[1];
                SheetLine baby = Boxes(StyleVital, new[] { "PREGNANT" },
                    new[] { (100f * months / 12f).ToString("F0") + "%" }, new[] { months / 12f }, new[] { BabyTint });
                baby.Tips = new[] { TipPregnant };
                lines.Add(baby);
            }
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

        // WHO THEY KNOW, AND WHAT THEY HAVE DONE: the plain sheet's relation rows
        // under the form's heading - their count in its pill, the gate marks on a
        // line of their own - then the history as a blank and four counts.
        private static void WriteFormSocial(List<SheetLine> lines, CommonStates who, bool own)
        {
            // MOOD LEADS THE SOCIAL PAGE: most of what drains a villager is who they live with,
            // and those are the people listed under it
            if (!own && who.friendID >= 0 && who.Employed())
            {
                WriteMoodForm(lines, who);
            }
            int start = lines.Count;
            WriteRelationEdges(lines, who, own, RelShowLoved, RelShowHostile);
            if (lines.Count > start && lines[start].Kind == LineHead)
            {
                SheetLine gates = lines[start];
                lines[start] = Section("RELATIONS", gates.Value);
                gates.Label = string.Empty;
                gates.Value = string.Empty;
                gates.Icon = null;
                lines.Insert(start + 1, gates);
            }
            TextManager tm = _mn != null ? _mn.textMN : null;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            HistoryCounts(who, out int sex, out int pregnancies, out int deliveries);
            string masturbation = who.sexInfo != null && who.sexInfo.Length > 2 ? who.sexInfo[2].ToString() : "0";
            lines.Add(new SheetLine { Kind = LineSpace });
            lines.Add(Section("HISTORY"));
            SheetLine partner = Field("First partner", FirstSexText(tm, npcs, who));
            partner.Tips = new[] { TipFirstPartner };
            lines.Add(partner);
            lines.Add(new SheetLine
            {
                Kind = LineBoxes, Style = StyleCount, Enabled = true,
                Options = new[] { "SEX", "PREGNANCIES", "DELIVERIES", "MASTURBATION" },
                Values = new[] { sex.ToString(), pregnancies.ToString(), deliveries.ToString(), masturbation },
                Tips = TipHistory,
            });
        }

        // THE SKILL BOOK: its level as the header's medallion, then the twelve as
        // boxes three to a row - a level, the pips toward its cap, the price of the
        // next one and its +.
        private static void WriteFormSkills(List<SheetLine> lines)
        {
            SkillManager sk = _mn != null ? _mn.skillMN : null;
            if (sk == null || sk.skills == null)
            {
                lines.Add(Row("Skills", "—"));
                return;
            }
            lines.Add(new SheetLine
            {
                Kind = LineFormHead, Label = null, Label2 = "SKILL LV", Value = sk.skillLevel.ToString(),
                Value2 = sk.exp.ToString("F0") + " / " + sk.maxExp.ToString("F0"),
                Frac = sk.maxExp > 0.0 ? (float)(sk.exp / sk.maxExp) : 0f, Enabled = true,
                Tips = new[] { TipSkillLevel, TipSkillExp },
            });
            SheetLine points = Section("SKILLS", sk.skillPoint + " points");
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
                    line.Values[k] = "Lv " + sk.skills[i];
                    line.Foots[k] = maxed ? "MAX" : "next " + cost + " pt";
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

        // WHAT YOU HAVE TOLD THEM: the plain Orders writer, re-set as a form - its
        // headings become sections, the follow toggle and the permissions become
        // checkboxes (two to a line), and the last row's Banish is printed in red.
        // Re-set rather than rewritten, because that writer reads vanilla's own
        // panels and sliders and arms the territory preview, and one copy of that is
        // one copy to keep right.
        private static void WriteFormOrders(List<SheetLine> lines, CommonStates who)
        {
            _formScratch.Clear();
            WriteOrders(_formScratch, who);
            bool first = true;
            for (int i = 0; i < _formScratch.Count; i++)
            {
                SheetLine line = _formScratch[i];
                switch (line.Kind)
                {
                    case LineHead:
                        lines.Add(Section(first ? "ASSIGNMENT" : (line.Label ?? string.Empty).ToUpperInvariant()));
                        first = false;
                        break;
                    case LineToggle:
                        lines.Add(new SheetLine
                        {
                            Kind = LineChecks, Enabled = true, Options = new[] { line.Label },
                            Cans = new[] { line.On }, Args = new[] { line.Arg }, Glyphs = new[] { GlyphCheck },
                            Tips = new[] { line.Tip },
                        });
                        break;
                    case LineOption:
                    {
                        // the permissions, two to a line
                        bool pair = i + 1 < _formScratch.Count && _formScratch[i + 1].Kind == LineOption;
                        SheetLine next = pair ? _formScratch[i + 1] : default(SheetLine);
                        lines.Add(new SheetLine
                        {
                            Kind = LineChecks, Enabled = true,
                            Options = pair ? new[] { line.Label, next.Label } : new[] { line.Label },
                            Cans = pair ? new[] { line.On, next.On } : new[] { line.On },
                            Args = pair ? new[] { CheckOption + line.Arg, CheckOption + next.Arg } : new[] { CheckOption + line.Arg },
                            Glyphs = pair ? new[] { GlyphCheck, GlyphCheck } : new[] { GlyphCheck },
                            Tips = pair ? new[] { line.Tip, next.Tip } : new[] { line.Tip },
                        });
                        if (pair)
                        {
                            i++;
                        }
                        break;
                    }
                    case LineActions:
                        // Return · Call · Banish: the one act on the sheet that cannot be taken back
                        if (line.Arg == 0 && line.Options != null && line.Options.Length > 2)
                        {
                            line.Danger = 3;
                        }
                        lines.Add(line);
                        break;
                    default:
                        lines.Add(line);
                        break;
                }
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
            PressToggle(who, code);
        }

        // ---------- the form's measures (canvas units) ----------

        private const float FormCaptionH = 8f;                               // a caption over a field
        private const float FormDropRowH = FormCaptionH + KitDropdown.Height + 3f;
        private const float FormScaleRowH = 16f;
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
                case LineMoodNow: return FormMoodNowH;
                case LineMoodCause: return FormMoodCauseH;
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
            if (!img.gameObject.activeSelf) { img.gameObject.SetActive(true); }
            return img;
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
            if (t.enableAutoSizing != fit) { t.enableAutoSizing = fit; }
            if (fit)
            {
                t.fontSizeMin = size * 0.6f;
                t.fontSizeMax = size;
            }
            Theme.Place(t, x, y, w, h);
            if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); }
            return t;
        }

        private static float TextWidth(TextMeshProUGUI t)
        {
            return t.GetPreferredValues(t.text, 400f, 40f).x;
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
            float w = row.FormW;
            float h = row.FormH;
            BeginForm(row);
            if (line.Kind != LineFormHead || line.Label == null)
            {
                HidePortrait(row);
            }
            int planks = 0;
            switch (line.Kind)
            {
                case LineFormHead: DrawFormHead(row, line, w, h); break;
                case LineSection: DrawSection(row, line, w, h); break;
                case LineBoxes: planks = DrawBoxes(row, line, w, h); break;
                case LineChecks: planks = DrawChecks(row, line, w, h); break;
                case LineField: DrawField(row, line, w, h); break;
                case LineActions: planks = DrawButtons(row, line, w, h); break;
                case LineMore: planks = DrawMore(row, line, w, h); break;
                case LineMoodNow: DrawMoodNow(row, line, w, h); break;
                case LineMoodCause: planks = DrawMoodCause(row, line, w, h); break;
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
        private static void DrawFormHead(SheetRow row, SheetLine line, float w, float h)
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
                FormText(row, "EXP", CaptionSize, soft, TextAlignmentOptions.MidlineLeft, true, ex, ey - 4f, 20f, 8f, CaptionSpacing);
                FormBar(row, ex + 14f, ey, right - 46f - (ex + 14f), 3.2f, line.Frac, SkillExpTint());
                FormText(row, line.Value2 ?? string.Empty, 6.4f, ink, TextAlignmentOptions.MidlineRight, false, right - 44f, ey - 4f, 44f, 8f);
                return;
            }
            // the portrait: an inked oval with the character in it, rendered live
            // (D125); the head glyph while no render can be had
            const float ow = 36f, oh = 43f;
            FormImage(row, "kit_dossier_oval", 0, ink, left, 1f, ow, oh);
            bool live = PlacePortrait(row, left + 3.2f, 4.2f, ow - 6.4f, oh - 6.4f);
            if (!live && line.Icon != null)
            {
                FormSprite(row, line.Icon, ink, left + ow * 0.18f, 1f + oh * 0.2f, ow * 0.64f, oh * 0.6f);
            }
            float nx = left + ow + 9f;
            float n1 = right - medal - 8f;
            FormText(row, (line.Label ?? string.Empty).ToUpperInvariant(), NameSize, ink,
                TextAlignmentOptions.BottomLeft, true, nx, 0f, n1 - nx, 19f, 2f, fit: true);
            FormRule(row, ink, nx, 20f, n1 - nx);
            FormText(row, "NAME", CaptionSize, soft, TextAlignmentOptions.TopLeft, true, nx, 21f, 40f, 6f, CaptionSpacing);
            // the fields, sharing the name line's width
            float fx = nx;
            int count = line.Options != null ? line.Options.Length : 0;
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
            FormText(row, "EXP", CaptionSize, soft, TextAlignmentOptions.MidlineLeft, true, left, y - 4f, 20f, 8f, CaptionSpacing);
            FormBar(row, left + 14f, y, n1 - (left + 14f), 3.2f, line.Frac, ExpTint());
            FormText(row, line.Value2 ?? string.Empty, 6.4f, ink, TextAlignmentOptions.MidlineRight, false, n1, y - 4f, right - n1, 8f);
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
            w = Mathf.Max(2f, w);
            FormImage(row, "kit_dossier_chip", 8, Theme.DossierTrack, x, cy - h * 0.5f, w, h);
            float f = Mathf.Clamp01(frac) * w;
            if (f > h * 0.5f)
            {
                FormImage(row, "kit_dossier_chip", 8, fill, x, cy - h * 0.5f, f, h);
            }
        }

        // SPACED CAPITALS, A RULE TO THE MARGIN, AND WHAT THE SECTION COUNTS IN A PILL
        private static void DrawSection(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            float cy = h - 6f;
            TextMeshProUGUI title = FormText(row, (line.Label ?? string.Empty).ToUpperInvariant(), SectionSize,
                Theme.DossierInk, TextAlignmentOptions.MidlineLeft, true, left, cy - 4f, right - left, 8f, CaptionSpacing + 6f);
            float ruleX = left + TextWidth(title) + 4f;
            float ruleEnd = right;
            if (line.PointsKind != 0 && !string.IsNullOrEmpty(line.Value))
            {
                // points that buy something: the label is the tag (PointsTag)
                EnsureFormLayers(row);
                if (row.Points == null || row.Points.Rect == null)
                {
                    row.Points = PointsTag.Create(row.FormType, "Points");
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
                TextMeshProUGUI pill = FormText(row, line.Value, 6f, Theme.DossierInk, TextAlignmentOptions.Center, false,
                    0f, 0f, 10f, 9f);
                float pw = TextWidth(pill) + 9f;
                Theme.Place(pill, right - pw, cy - 4.5f, pw, 9f);
                FormImage(row, "kit_dossier_pill", 15, Theme.DossierInk, right - pw, cy - 4.5f, pw, 9f);
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
            for (int i = 0; i < n; i++)
            {
                float x = left + i * (bw + gap);
                const float y = 0f;
                string caption = line.Options[i] ?? string.Empty;
                string value = line.Values != null && i < line.Values.Length ? line.Values[i] : string.Empty;
                bool bold = line.Style == StyleAttribute;
                FormImage(row, bold ? "kit_dossier_box_bold" : "kit_dossier_box", bold ? 7 : 6, ink, x, y, bw, bh);
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
                        FormText(row, caption, CaptionSize, soft, TextAlignmentOptions.TopLeft, true, x + 3f, y + 2.2f, bw - 6f, 6f, CaptionSpacing * 0.6f);
                        FormText(row, value, BoxValueSize, ink, TextAlignmentOptions.MidlineRight, true, x + 3f,
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
                    bool maxed = foot == "MAX";
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

        private static void DrawOneField(SheetRow row, string label, string value, float x, float w, float h)
        {
            float baseY = h - 3f;
            TextMeshProUGUI name = FormText(row, label ?? string.Empty, FieldLabelSize, Theme.DossierLabel,
                TextAlignmentOptions.BottomLeft, false, x, 0f, w, baseY + 0.5f);
            float lw = TextWidth(name);
            FormRule(row, Theme.DossierSoft, x + lw + 2.5f, baseY + 0.8f, w - lw - 2.5f);
            if (!string.IsNullOrEmpty(value))
            {
                FormText(row, value, FormTextSize, Theme.DossierInk, TextAlignmentOptions.BottomLeft, false,
                    x + lw + 4.5f, 0f, Mathf.Max(4f, w - lw - 5f), baseY + 0.8f);
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

        private static void DrawButton(SheetRow row, int k, string label, bool danger, float x, float y, float bw)
        {
            Color ink = danger ? Theme.DossierRed : Theme.DossierInk;
            bool hot = Hot(row, k);
            bool down = Down(row, k);
            if (hot)
            {
                FormImage(row, "kit_dossier_chip", 8, Alpha(ink, down ? 0.22f : 0.1f), x, y, bw, FormButtonH);
            }
            FormImage(row, "kit_dossier_button", 12, ink, x, y, bw, FormButtonH);
            FormText(row, label ?? string.Empty, FormTextSize, ink, TextAlignmentOptions.Center, false,
                x, y + (down ? 0.4f : 0f), bw, FormButtonH);
            FormHit(row, k, x, y, bw, FormButtonH);
        }

        // "… N more": the one button that opens the whole matrix, at the row's end
        private static int DrawMore(SheetRow row, SheetLine line, float w, float h)
        {
            string label = line.Options != null && line.Options.Length > 0 ? line.Options[0] : "…";
            float bw = SheetMoreW;
            DrawButton(row, 0, label, false, w - SheetPadX - bw, (h - FormButtonH) * 0.5f, bw);
            return 1;
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
                minX = Mathf.Min(minX, _headVerts[k]);
                maxX = Mathf.Max(maxX, _headVerts[k]);
                minY = Mathf.Min(minY, _headVerts[k + 1]);
                maxY = Mathf.Max(maxY, _headVerts[k + 1]);
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
            // THE SKULL FIRST: every humanoid rig here carries a "Head" slot - the
            // head's own shape, under the hair - and it is the one measure that is
            // the same thing on every rig. Back hair is bound straight to the head
            // bone on some (Aira's braids spread the box sideways, the Giant's hair
            // stretched it to 14 units), so the vertex rule below is the fallback.
            Spine.Slot skull = skeleton.FindSlot("Head");
            if (skull != null && skull.A > 0.01f && SlotBox(skull, out centre, out size))
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
                    float x = _headVerts[v * 2];
                    float y = _headVerts[v * 2 + 1];
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
        private static readonly RenderTexture[] _portraitTex = new RenderTexture[GroupCount];
        private static readonly SheetRow[] _portraitRow = new SheetRow[GroupCount];
        private static readonly bool[] _portraitOk = new bool[GroupCount];
        private static readonly List<Renderer> _portraitRenderers = new List<Renderer>();
        private static readonly List<int> _portraitLayers = new List<int>();

        // The oval's stencil and the render in it, placed by the header; true when
        // the last render of this group's character succeeded
        private static bool PlacePortrait(SheetRow row, float x, float y, float w, float h)
        {
            int group = row.Host != null ? row.Host.Group : GroupNone;
            if (group <= GroupNone || group >= GroupCount)
            {
                HidePortrait(row);
                return false;
            }
            if (row.PortraitMask == null)
            {
                var go = Owned.New("Portrait");
                go.transform.SetParent(row.Go.transform, false);
                row.PortraitMask = go.AddComponent<Image>();
                row.PortraitMask.raycastTarget = false;
                Theme.KitSkin(row.PortraitMask, "kit_dossier_dot", 0);
                row.PortraitMask.preserveAspect = false;
                var mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var face = Owned.New("Face");
                face.transform.SetParent(go.transform, false);
                row.PortraitRaw = face.AddComponent<RawImage>();
                row.PortraitRaw.raycastTarget = false;
                var fr = row.PortraitRaw.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = Vector2.zero;
                fr.offsetMax = Vector2.zero;
            }
            // under the ring, which is on the art layer - moved only when it is above
            // it: re-slotting at the layer's index every paint swapped the two each
            // time, and the ring flickered over and under the face
            if (row.FormArt != null)
            {
                int art = row.FormArt.GetSiblingIndex();
                if (row.PortraitMask.transform.GetSiblingIndex() > art)
                {
                    row.PortraitMask.transform.SetSiblingIndex(art);
                }
            }
            Theme.Place(row.PortraitMask, x, y, w, h);
            if (!row.PortraitMask.gameObject.activeSelf)
            {
                row.PortraitMask.gameObject.SetActive(true);
            }
            _portraitRow[group] = row;
            bool ok = _portraitOk[group] && _portraitTex[group] != null;
            row.PortraitRaw.texture = ok ? _portraitTex[group] : null;
            row.PortraitRaw.enabled = ok;
            return ok;
        }

        private static void HidePortrait(SheetRow row)
        {
            if (row.PortraitMask != null && row.PortraitMask.gameObject.activeSelf)
            {
                row.PortraitMask.gameObject.SetActive(false);
            }
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
                    || row.DrewKind != LineFormHead || row.Host == null || row.Host.Group != g)
                {
                    _portraitRow[g] = null;
                    continue;
                }
                bool ok = row.Host.Owner != null && RenderPortrait(g, row.Host.Owner);
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
                + " | bones: " + BoneList(sk);
        }

        // For the harness: every named character in the scene rendered as the
        // dossier would, side by side into one PNG - the check for a framing
        // across rigs without opening a sheet per character. "path" or
        // "path|Name;Name" to pick whom (the harness splits its args on commas).
        internal static string PortraitSheet(string arg)
        {
            string[] parts = (arg ?? string.Empty).Split('|');
            string path = parts[0];
            var only = parts.Length > 1 ? new HashSet<string>(parts[1].Split(';'), System.StringComparer.OrdinalIgnoreCase) : null;
            var who = new List<CommonStates>();
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c != null && !string.IsNullOrEmpty(c.charaName) && c.anim != null
                    && (only == null || only.Contains(c.charaName))) { who.Add(c); }
            }
            who.Sort((a, b) => string.CompareOrdinal(a.charaName, b.charaName));
            int cols = 8, n = Mathf.Min(who.Count, 48);
            int rows = Mathf.Max(1, (n + cols - 1) / cols);
            var sheet = new Texture2D(cols * PortraitPxW, rows * PortraitPxH, TextureFormat.RGBA32, false);
            var clear = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < clear.Length; i++) { clear[i] = new Color32(236, 226, 204, 255); }
            sheet.SetPixels32(clear);
            var names = new System.Text.StringBuilder();
            var cell = new Texture2D(PortraitPxW, PortraitPxH, TextureFormat.RGBA32, false);
            for (int i = 0; i < n; i++)
            {
                bool ok = RenderPortrait(GroupNone, who[i]);
                names.Append(i).Append(':').Append(who[i].charaName).Append(ok ? "" : "(none)").Append(' ');
                if (!ok) { continue; }
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _portraitTex[GroupNone];
                cell.ReadPixels(new Rect(0, 0, PortraitPxW, PortraitPxH), 0, 0);
                cell.Apply();
                RenderTexture.active = was;
                Color[] px = cell.GetPixels();
                for (int k = 0; k < px.Length; k++)
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

        private static bool RenderPortrait(int group, CommonStates who)
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
            if (head == null || scale < 1e-5f
                || !HeadBox(skeleton, head, out Vector2 boxCentre, out Vector2 boxSize))
            {
                return false;   // a creature without a head to frame: the glyph
            }
            float aspect = PortraitPxW / (float)PortraitPxH;
            float half = Mathf.Max(boxSize.y, boxSize.x / aspect) * PortraitFill;
            // a little under the box's middle, so the chin and the neck have room
            Vector3 centre = anim.transform.TransformPoint(new Vector3(boxCentre.x, boxCentre.y - boxSize.y * PortraitDrop, 0f));
            Transform view = main.transform;
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
                keyGo.transform.localRotation = Quaternion.Euler(12f, -18f, 0f);   // a little above and to the side
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
            _portraitCam.transform.rotation = view.rotation;
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

        // ---------- the rows the form shares with the plain sheet ----------

        private const float FormScaleCaptionW = 46f;
        private const float FormScaleValueW = 24f;

        private static void LayoutFormScale(SheetRow row, float width, float h)
        {
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
            switch (line.Kind)
            {
                case LineSlider:
                {
                    // the printed scale: a caption, the ruled line with its ticks, the
                    // reading in a box; the knob is the plain row's own
                    StyleCaption(row.Label);
                    if (row.Track.sprite != null) { row.Track.sprite = null; }
                    row.Track.type = Image.Type.Simple;
                    row.Track.color = Theme.DossierInk;
                    if (row.Fill.gameObject.activeSelf) { row.Fill.gameObject.SetActive(false); }
                    float cy = h * 0.5f + 0.5f;
                    for (int k = 0; k <= 10; k++)
                    {
                        float tx = row.TrackX + row.TrackW * k / 10f;
                        float t = k % 5 == 0 ? 3f : 1.8f;
                        FormImage(row, null, 0, Theme.DossierInk, tx - 0.2f, cy - t, 0.4f, t);
                    }
                    float valueX = w - SheetPadX - FormScaleValueW;
                    FormImage(row, "kit_dossier_box", 6, Theme.DossierInk, valueX, cy - 5.5f, FormScaleValueW, 11f);
                    if (row.Value.color != Theme.DossierInk) { row.Value.color = Theme.DossierInk; }
                    if (row.Knob != null) { row.Knob.transform.SetAsLastSibling(); }
                    break;
                }
                case LineDropdown:
                    StyleCaption(row.Label);
                    break;
                case LineRelation:
                    // the ruling under each name
                    FormRule(row, Theme.DossierRule, SheetPadX, h - 0.5f, w - 2f * SheetPadX);
                    break;
            }
            EndForm(row, row.Planks != null && (line.Kind == LineActs || line.Kind == LinePlace
                || line.Kind == LinePose || line.Kind == LineRosterBar) ? row.Planks.Length : 0);
        }

        private static void StyleCaption(TextMeshProUGUI label)
        {
            if (label.fontSize != CaptionSize) { label.fontSize = CaptionSize; }
            if (label.fontStyle != (FontStyles.Bold | FontStyles.UpperCase)) { label.fontStyle = FontStyles.Bold | FontStyles.UpperCase; }
            if (label.characterSpacing != CaptionSpacing) { label.characterSpacing = CaptionSpacing; }
            if (label.color != Theme.DossierSoft) { label.color = Theme.DossierSoft; }
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }
}
