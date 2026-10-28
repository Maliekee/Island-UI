using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;

namespace IslandUI
{
    // InventoryWindow — WHO THEY ARE TO OTHERS, ON THE PRINTED FORM (Forgejo #132).
    //   PERSONALITY     the Character page's ten types in a strip: their own with its
    //                word under it, the liked and the disliked tagged on the
    //                corner (green arrow up, red down), the rest faded.
    //   RECORD       the Social page's foot: the life's counts, removed from the
    //                Character page as HISTORY for want of room.
    //   PARTNERS     after it: each partner and the game's six kinds of count
    //                between the two (CommonStates.Lovers.sexCount, written by
    //                SexManager.SexCountChange; vanilla's lover list behind the
    //                status window's Sex button prints them, UIManager.LoverInfoSex).
    // RECORD and PARTNERS are never shown for anyone under 18, and no one under
    // 18 is listed as a partner (Minor).
    internal static partial class InventoryWindow
    {
        private const int LineTraits = 46;        // PERSONALITY: the ten types in a strip (Index their own, Arg the liked, Style the disliked)
        private const int LinePartner = 48;       // a partner: token · name · the six counts (Arg PartnerHead: the columns' captions)

        private const int PartnerHead = 1;
        private const float FormPartnerH = 13f;   // a bond row's height: one table with the rest of the page
        private const float FormPartnerHeadH = 8f;
        private const float PartnerNameW = 46f;   // the person, before the six columns
        private const float FormTraitsH = 26f;   // room for a tag over the token, the token, their type's word

        private static bool RecordKind(int kind)
        {
            return kind == LineTraits || kind == LinePartner;
        }

        private static float RecordRowHeight(SheetLine line)
        {
            switch (line.Kind)
            {
                case LineTraits: return FormTraitsH;
                default: return line.Arg == PartnerHead ? FormPartnerHeadH : FormPartnerH;
            }
        }

        private static int DrawRecordRow(SheetRow row, SheetLine line, float w, float h)
        {
            switch (line.Kind)
            {
                case LineTraits: return DrawTraits(row, line, w, h);
                default: return DrawPartner(row, line, w, h);
            }
        }

        private static void PressRecordRow(Plate plate, CommonStates who, SheetRow row, int sub)
        {
            int code = row.FormArgs != null && sub >= 0 && sub < row.FormArgs.Length ? row.FormArgs[sub] : -1;
            switch (row.DrewKind)
            {
                case LineTraits:
                    OpenRosterAt(code);
                    break;
                case LinePartner:
                    if (sub == 0) { OpenRosterAt(row.LinkType); }
                    else if (sub == 1) { PressLink(who, row.Link); }
                    break;
            }
        }

        private static bool Minor(CommonStates c)
        {
            // All characters are adult
            return false;
        }

        private static bool ChildKind(int npcID)
        {
            // All characters are adult
            return false;
            // return npcID == 14 || npcID == 16 || (npcID >= 140 && npcID <= 145);
        }

        // ---------- PERSONALITY ----------

        // Whom they take to and whom they can't, as facts - all ten types and the
        // rule's number under each, no people. The strip is printed filled or not
        // on every sheet - the one you play's too: the game deals personalities
        // only to the NPCs it makes (NPCManager.AddPersonalities), so Man's and
        // Yona's are all faded, as they are when the other one is your target.
        private static void WriteFormPersonality(List<SheetLine> lines, CommonStates who)
        {
            int[] person = who.person;
            int typeOf(int slot) => person != null && slot < person.Length ? person[slot] : -1;
            lines.Add(Section(Shared.Loc.Get("dossier.personality", "PERSONALITY")));
            // every type leads to its people: plank k is type k
            var types = new int[TraitTints.Length];
            var typeTips = new string[TraitTints.Length];
            for (int k = 0; k < types.Length; k++)
            {
                types[k] = k;
                typeTips[k] = k == typeOf(0) ? TypeTip(who)
                    : k == typeOf(1) ? "<b>" + TintedAround(TraitLike, TypeWithIcon(k), a => Shared.Loc.Get("dossier.personality.likes", "Likes {0}", a)) + "</b>\n" + PersonalityNear("+10")
                    : k == typeOf(2) ? "<b>" + TintedAround(TraitDislike, TypeWithIcon(k), a => Shared.Loc.Get("dossier.personality.dislikes", "Dislikes {0}", a)) + "</b>\n" + PersonalityNear("-5")
                    : "<b>" + TypeWithIcon(k) + "</b>\n" + PersonalityNear("+5");
            }
            lines.Add(new SheetLine
            {
                Kind = LineTraits, Enabled = true, Index = typeOf(0), Arg = typeOf(1), Style = typeOf(2),
                Args = types, Tips = typeTips, Label = typeOf(0) >= 0 ? TypeText(typeOf(0)) : null,
            });
        }

        // THE TEN TYPES IN A STRIP: their own type whole, the rest faded; their
        // own type's word under it. The liked and the disliked, faded too, carry
        // a tag on the token's top right corner - a green
        // square with an arrow up, a red one with an arrow down (love rises near
        // one, falls near the other), ringed in the paper so it lifts off the
        // token. No numbers: what a meeting is worth is the Social page's to
        // teach, and the tips say it. The strip is the same on every sheet, so
        // the eye learns where to look. Every type leads to its people (plank k
        // is type k, the roster at that section).
        private static int DrawTraits(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            int n = TraitTints.Length;
            float cw = (right - left) / n;
            const float ts = 15f;
            const float ty = 3.5f;                      // room over the token for its tag
            const float tag = 6.6f, rim = 0.6f;         // the tag, and the paper ring round it
            int likes = line.Arg, dislikes = line.Style;
            for (int k = 0; k < n; k++)
            {
                float x = left + k * cw;
                float cx = x + cw * 0.5f;
                bool hot = Hot(row, k);
                bool own = k == line.Index, liked = k == likes, disliked = k == dislikes;
                // faded, and whole under the pointer - its only hover: it leads
                // somewhere too
                DrawTraitToken(row, k, cx - ts * 0.5f, ty, ts, own || hot ? 1f : 0.35f);
                if (liked || disliked)
                {
                    // centred on the token's top right corner, just inside its rim
                    float gx = cx + ts * 0.5f - 0.6f - tag * 0.5f, gy = ty + 0.6f - tag * 0.5f;
                    FormImage(row, null, 0, TagRim, gx - rim, gy - rim, tag + rim * 2f, tag + rim * 2f);
                    FormImage(row, null, 0, liked ? TraitLike : TraitDislike, gx, gy, tag, tag);
                    FormSprite(row, liked ? Theme.ArrowUp : Theme.ArrowDown, TraitCream,
                        gx + tag * 0.15f, gy + tag * 0.15f, tag * 0.7f, tag * 0.7f);
                }
                FormHit(row, k, x + 1f, 0f, cw - 2f, h);
                FormTip(row, line.Tips, k, x + 1f, 0f, cw - 2f, h);
                if (own && !string.IsNullOrEmpty(line.Label))
                {
                    // the word is wider than its column: centred close under the
                    // token, and pushed inward where it would run past the strip's
                    // ends - smaller and in the label's brown: the token says it first
                    float wy = ty + ts + 0.5f;
                    TextMeshProUGUI word = FormText(row, line.Label, FormTextSize * 0.8f, Theme.DossierLabel, TextAlignmentOptions.Top, false,
                        left, wy, right - left, 6.5f);
                    float ww = Mathf.Min(TextWidth(word) + 1f, right - left);
                    Theme.Place(word, Mathf.Clamp(cx - ww * 0.5f, left, right - ww), wy, ww, 6.5f);
                }
            }
            return n;
        }

        private static readonly Color TagRim = Theme.ParseColor("#F4ECD8", Color.white);   // the sheet's paper

        // everyone of yours with this person as a parent (the Social page's FAMILY) - none for someone with
        // no friendID, whom no child's record can name
        private static List<CommonStates> ChildrenOf(NPCManager npcs, CommonStates who)
        {
            var children = new List<CommonStates>();
            if (npcs == null || who.friendID < 0)
            {
                return children;
            }
            foreach (CommonStates c in People(npcs))
            {
                if (c != null && !ReferenceEquals(c, who) && c.status != null && c.status.Length > 9
                    && (c.status[8] == who.friendID || c.status[9] == who.friendID))
                {
                    children.Add(c);
                }
            }
            return children;
        }

        // ---------- RECORD and PARTNERS, on the Social page ----------

        private static bool RecordShown(CommonStates who)
        {
            return who != null && Observe.Known(who) && !Minor(who);
        }

        // RECORD: what they have done, as a blank and four counts. Surgeries is a
        // count of a life, as these are: beside the first partner, and only where
        // the game's setting tracks them.
        private static void WriteFormRecord(List<SheetLine> lines, CommonStates who)
        {
            if (!RecordShown(who))
            {
                return;
            }
            TextManager tm = GameWords.Text(_mn);
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            HistoryCounts(who, out int sex, out int pregnancies, out int deliveries);
            string masturbation = who.sexInfo != null && who.sexInfo.Length > 2 ? who.sexInfo[2].ToString() : "0";
            lines.Add(Section(Shared.Loc.Get("dossier.record", "RECORD")));
            bool surgeries = SaveManager.SaveSettingStatic.trackSurgeries == 1 && who.status != null && who.status.Length > 6;
            SheetLine partner = surgeries
                ? Field(Shared.Loc.Get("relations.history.first_partner", "First partner"), FirstSexText(tm, npcs, who), Shared.Loc.Get("vitals.surgeries", "Surgeries"), who.status[6].ToString())
                : Field(Shared.Loc.Get("relations.history.first_partner", "First partner"), FirstSexText(tm, npcs, who));
            partner.Tips = surgeries ? new[] { TipFirstPartner, TipSurgeries } : new[] { TipFirstPartner };
            lines.Add(partner);
            lines.Add(new SheetLine
            {
                Kind = LineBoxes, Style = StyleCount, Enabled = true,
                Options = new[]
                {
                    Shared.Loc.Get("dossier.sex", "SEX"), Shared.Loc.Get("dossier.pregnancies", "PREGNANCIES"),
                    Shared.Loc.Get("dossier.deliveries", "DELIVERIES"), Shared.Loc.Get("dossier.masturbation", "MASTURBATION"),
                },
                Values = new[] { sex.ToString(), pregnancies.ToString(), deliveries.ToString(), masturbation },
                Tips = TipHistory,
            });
        }

        // the six kinds, as sexCount's ten slots pair them (SexManager.SexCountChange:
        // the one who does it counts the first of a pair, the one it is done to the
        // second): sex 0, rape 1 and 2, toilet 3 and 4, creampie 5 and 6, a
        // pregnancy 7 and 8, a delivery 9
        private static readonly int[][] PartnerKinds =
        {
            new[] { 0 }, new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }, new[] { 7, 8 }, new[] { 9 },
        };

        private static string PartnerKindCaption(int kind)
        {
            switch (kind)
            {
                case 0: return Shared.Loc.Get("dossier.partners.sex", "SEX");
                case 1: return Shared.Loc.Get("dossier.partners.rape", "RAPE");
                case 2: return Shared.Loc.Get("dossier.partners.toilet", "TOILET");
                case 3: return Shared.Loc.Get("dossier.partners.creampie", "CREAMPIE");
                case 4: return Shared.Loc.Get("dossier.partners.pregnancy", "PREGNANCY");
                default: return Shared.Loc.Get("dossier.partners.delivery", "DELIVERY");
            }
        }

        // a count's tip: for a pair, who did it to whom, each way
        private static string PartnerKindTip(int kind, int[] counts, string me, string them)
        {
            int[] slots = PartnerKinds[kind];
            int Count(int slot) => counts != null && slot < counts.Length ? counts[slot] : 0;
            switch (kind)
            {
                case 0: return Shared.Loc.Get("dossier.partners.sex.help", "Consenting sex between {0} and {1}", me, them);
                case 1:
                    return Shared.Loc.Get("dossier.partners.rape.help", "{0} raped {1}", me, them) + "  " + Count(slots[0]) + "\n"
                        + Shared.Loc.Get("dossier.partners.rape.help", "{0} raped {1}", them, me) + "  " + Count(slots[1]);
                case 2:
                    return Shared.Loc.Get("dossier.partners.toilet.help", "{0} used {1} as a toilet", me, them) + "  " + Count(slots[0]) + "\n"
                        + Shared.Loc.Get("dossier.partners.toilet.help", "{0} used {1} as a toilet", them, me) + "  " + Count(slots[1]);
                case 3:
                    return Shared.Loc.Get("dossier.partners.creampie.help", "{0} came inside {1}", me, them) + "  " + Count(slots[0]) + "\n"
                        + Shared.Loc.Get("dossier.partners.creampie.help", "{0} came inside {1}", them, me) + "  " + Count(slots[1]);
                case 4:
                    return Shared.Loc.Get("dossier.partners.pregnancy.help", "{0} made {1} pregnant", me, them) + "  " + Count(slots[0]) + "\n"
                        + Shared.Loc.Get("dossier.partners.pregnancy.help", "{0} made {1} pregnant", them, me) + "  " + Count(slots[1]);
                default: return Shared.Loc.Get("dossier.partners.delivery.help", "Children {0} gave birth to by {1}", me, them);
            }
        }

        // PARTNERS: everyone they have a count with, the most first, and the six
        // counts between the two. Whoever is under 18, or no longer anywhere the
        // game can say their age, is left out.
        private static void WriteFormPartners(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            if (!RecordShown(who) || npcs == null || who.lovers == null)
            {
                return;
            }
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            var rows = new List<KeyValuePair<SheetLine, int>>();
            for (int i = 0; i < who.lovers.Count; i++)
            {
                CommonStates.Lovers l = who.lovers[i];
                int[] counts = l != null ? l.sexCount : null;
                int total = 0;
                for (int j = 0; counts != null && j < counts.Length; j++) { total += counts[j]; }
                if (total <= 0)
                {
                    continue;
                }
                CommonStates other = Friend(npcs, l.friendID);
                // a negative id is a kind of native, and the kind says the age
                bool adult = other != null ? !Minor(other) : l.friendID < -1 && !ChildKind(-l.friendID);
                if (!adult)
                {
                    continue;
                }
                string name = other != null ? other.charaName : FriendNameOf(npcs, l.friendID);
                if (string.IsNullOrEmpty(name)) { name = "?"; }
                var values = new string[PartnerKinds.Length];
                var tips = new string[PartnerKinds.Length];
                for (int k = 0; k < PartnerKinds.Length; k++)
                {
                    int sum = 0;
                    foreach (int slot in PartnerKinds[k]) { if (counts != null && slot < counts.Length) { sum += counts[slot]; } }
                    values[k] = sum.ToString();
                    tips[k] = PartnerKindTip(k, counts, me, name);
                }
                bool linked = other != null && !Played(other);
                var line = new SheetLine
                {
                    Kind = LinePartner, Enabled = true, Arg = -1, Link = linked ? other : null,
                    Label2 = name, Away = linked ? AwayTip(other) : null,
                    Style = linked && other.person != null && other.person.Length > 0 ? other.person[0] : -1,
                    LabelTint = Theme.DossierInk,
                    Icons = linked ? new[] { MoodFace(other, out Color _) } : null,
                    Frac = linked ? MoodFill(other) : 0f,
                    Tint = linked ? MoralTint(other) : Color.clear,
                    Values = values, Tips = tips,
                };
                rows.Add(new KeyValuePair<SheetLine, int>(line, total));
            }
            if (rows.Count == 0)
            {
                lines.Add(Section(Shared.Loc.Get("dossier.partners", "PARTNERS")));
                lines.Add(NoneLine());
                return;
            }
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            lines.Add(Section(Shared.Loc.Get("dossier.partners", "PARTNERS"), rows.Count.ToString()));
            var captions = new string[PartnerKinds.Length];
            for (int k = 0; k < captions.Length; k++) { captions[k] = PartnerKindCaption(k); }
            lines.Add(new SheetLine { Kind = LinePartner, Enabled = true, Arg = PartnerHead, Options = captions });
            // a long list folds as RELATIONS does: a first page, then a page more
            // a press, or back to the first
            int first = Mathf.Min(rows.Count, PartnersFirstPage);
            int shown = Mathf.Clamp(PartnersShown(who, first), first, rows.Count);
            for (int i = 0; i < shown; i++)
            {
                lines.Add(rows[i].Key);
            }
            int rest = rows.Count - shown;
            if (rest > 0 || shown > first)
            {
                lines.Add(new SheetLine
                {
                    Kind = LineMetFold, Enabled = true, Arg = PartnerFoldArg + first,
                    Options = new[]
                    {
                        rest > 0 ? Shared.Loc.Get("social.show_more", "Show {0} more", Mathf.Min(MetPage, rest)) : null,
                        shown > first ? Shared.Loc.Get("social.show_less", "Show less") : null,
                    },
                    Label = rest > 0 ? Shared.Loc.Get("social.not_shown", "{0} not shown", rest) : null,
                });
            }
        }

        // how many of a person's partner rows are shown; -1 the first page. Its
        // own, so PARTNERS and RELATIONS each fold apart
        private const int PartnersFirstPage = 8;
        private const int PartnerFoldArg = 2000000;   // the fold line's Arg, less its first page: past All relations'
        private static CommonStates _partnersShownWho;
        private static int _partnersShown = -1;

        private static int PartnersShown(CommonStates who, int first)
        {
            return ReferenceEquals(_partnersShownWho, who) && _partnersShown >= 0 ? _partnersShown : first;
        }

        // the more-or-less line's press: 0 a page more, 1 back to the first
        private static void PressPartnersMore(CommonStates who, int first, int sub)
        {
            int now = PartnersShown(who, first);
            _partnersShownWho = who;
            _partnersShown = sub == 0 ? now + MetPage : first;
        }

        private static void PartnerColumns(float w, out float left, out float right, out float x0, out float colW)
        {
            left = SheetPadX;
            right = w - SheetPadX;
            x0 = left + PartnerNameW;
            colW = (right - x0) / PartnerKinds.Length;
        }

        private static int DrawPartner(SheetRow row, SheetLine line, float w, float h)
        {
            PartnerColumns(w, out float left, out float right, out float x0, out float colW);
            if (line.Arg == PartnerHead)
            {
                float cs = CaptionSize * 0.86f;
                for (int k = 0; line.Options != null && k < line.Options.Length; k++)
                {
                    FormText(row, line.Options[k], cs, Theme.DossierSoft, TextAlignmentOptions.Center, true,
                        x0 + k * colW + 0.5f, 0f, colW - 1f, h, 0f, fit: true);
                }
                FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
                return 0;
            }
            if (line.Link != null)
            {
                DrawCompactPerson(row, line, line.Label2, left, h, x0 - 3f - left - 8f, 0, 1);
            }
            else
            {
                // no one to press - you, or someone gone - their name alone
                FormNoHit(row, 0);
                FormNoHit(row, 1);
                FormText(row, line.Label2 ?? "?", FormTextSize, Theme.DossierInk, TextAlignmentOptions.MidlineLeft, false,
                    left + 9f, 0f, x0 - 3f - left - 9f, h, 0f, fit: true);
            }
            for (int k = 0; line.Values != null && k < line.Values.Length; k++)
            {
                string v = line.Values[k];
                FormText(row, v, FormTextSize * 0.85f, v == "0" ? Theme.DossierSoft : Theme.DossierInk,
                    TextAlignmentOptions.Center, false, x0 + k * colW, 0f, colW, h);
                FormTip(row, line.Tips, k, x0 + k * colW, 0f, colW, h);
            }
            FormRule(row, Theme.DossierRule, left, h - 0.5f, right - left);
            return 2;
        }

        // harness: whoever is named's partners, every slot of each ledger
        internal static string PartnersDump(string name)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            var all = npcs != null ? People(npcs) : new List<CommonStates>();
            all.AddRange(Object.FindObjectsOfType<CommonStates>());
            foreach (CommonStates c in all)
            {
                if (c == null || c.lovers == null || !string.Equals(c.charaName, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                sb.Append("friendID ").Append(c.friendID).Append(" npc").Append(c.npcID).Append(" age ").Append(c.age)
                  .Append(" minor ").Append(Minor(c)).Append(" children ").Append(ChildrenOf(npcs, c).Count).Append(": ");
                foreach (CommonStates.Lovers l in c.lovers)
                {
                    if (l == null || l.sexCount == null || !c.HaveSexLog(l)) { continue; }
                    CommonStates f = Friend(npcs, l.friendID);
                    sb.Append(f != null ? f.charaName + (Minor(f) ? "(minor)" : string.Empty) : FriendNameOf(npcs, l.friendID) + "#" + l.friendID)
                      .Append('=').Append(string.Join(",", System.Array.ConvertAll(l.sexCount, x => x.ToString()))).Append("; ");
                }
                return sb.ToString();
            }
            return "no one named '" + name + "'";
        }
    }
}
