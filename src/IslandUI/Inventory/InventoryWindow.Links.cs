using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — LINKS (docs/social-links.md section 10, step 5): what
    // names a person or a type leads to it. A name targets that person, from any
    // distance, as a roster pick does (PressRosterRow) - the sheet keeps its page,
    // so a name on the Social page lands on their Social page. A personality token
    // opens the roster sorted by Personality, scrolled to that type's section.
    internal static partial class InventoryWindow
    {
        // the section the roster scrolls to on its next bind (BindRoster)
        private static string _rosterScrollTo;

        // THE WAY BACK: who each followed name was followed from, most recent last.
        // It holds while the sheet is on the last name followed; changing person
        // any other way (a click in the world, a roster pick) lets it go.
        private static readonly List<CommonStates> LinkBack = new List<CommonStates>();
        private static CommonStates _linkedTo;
        private const int LinkBackMax = 8;

        private static void PressLink(CommonStates from, CommonStates to)
        {
            if (to == null || ReferenceEquals(from, to))
            {
                return;
            }
            // SOMEONE NOT IN THE WORLD is not targeted - that dropped the target -
            // but what holds them is: the one whose bag they are in, or the chest,
            // cage or device. In your own bag, or a prisoner, nothing is
            if (AwayWord(to) != null)
            {
                CarriedWhere(to, out object holder);
                if (holder is GameObject prop)
                {
                    TargetFromTab(prop);
                    return;
                }
                to = holder as CommonStates;
                if (to == null || ReferenceEquals(from, to) || Played(to))
                {
                    return;
                }
            }
            if (from != null)
            {
                if (!ReferenceEquals(_linkedTo, from))
                {
                    LinkBack.Clear();   // a new trail starts here
                }
                LinkBack.Add(from);
                if (LinkBack.Count > LinkBackMax) { LinkBack.RemoveAt(0); }
            }
            _linkedTo = to;
            _linkPressedAt = Time.unscaledTime;
            PressRosterRow(to);
        }

        private static void PressLinkBack()
        {
            if (LinkBack.Count == 0)
            {
                return;
            }
            CommonStates back = LinkBack[LinkBack.Count - 1];
            LinkBack.RemoveAt(LinkBack.Count - 1);
            _linkedTo = back;
            _linkPressedAt = Time.unscaledTime;
            PressRosterRow(back);
        }

        // whom the band's back link returns to on this owner's sheet, or null. A
        // press repaints the sheet it was made on before the pick lands, so for a
        // moment after one the old owner is no sign the trail was left.
        private static CommonStates LinkBackFor(CommonStates owner)
        {
            if (!ReferenceEquals(_linkedTo, owner))
            {
                if (Time.unscaledTime - _linkPressedAt > LinkLanding)
                {
                    LinkBack.Clear();
                    _linkedTo = null;
                }
                return null;
            }
            return LinkBack.Count > 0 ? LinkBack[LinkBack.Count - 1] : null;
        }

        private static float _linkPressedAt = -10f;
        private const float LinkLanding = 2f;

        // ---------- the head band: whose page this is ----------

        private const float FormBandH = 24f;

        // THE COMPACT HEAD, on the pages that have no head of their own (Social,
        // Orders): the portrait, the name, their type, kind and level - so a page
        // reached by a link says whose it is - and the way back, "← Selsia", when
        // it was reached that way. No mood face: Social's MOOD opens with the
        // moral itself, and the face beside the name said it twice.
        private static void WriteFormBand(List<SheetLine> lines, CommonStates who)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            // the type and the level as the Observation skill lets you know them (Observe.cs)
            int type = who.person != null && who.person.Length > 0 && Observe.Sees(who, Observe.Personality) ? who.person[0] : -1;
            string kind = KindName(npcs, who);
            var facts = new List<string>();
            if (type >= 0) { facts.Add(TypeText(type)); }
            if (!string.IsNullOrEmpty(kind)) { facts.Add(kind); }   // even where it is their name, as on the head
            facts.Add(Shared.Loc.Get("links.level", "Lv {0}", Observe.Sees(who, Observe.Level) ? who.level.ToString() : Observe.Unknown));
            CommonStates back = LinkBackFor(who);
            lines.Add(new SheetLine
            {
                Kind = LineFormBand, Enabled = true,
                Label = string.IsNullOrEmpty(who.charaName) ? (kind ?? "?") : who.charaName,
                Label2 = string.Join(" · ", facts.ToArray()),
                Style = type, LabelTint = Theme.DossierInk,
                Link = back,
                Value = back != null ? "← " + back.charaName : null,
                Starred = who.employ == CommonStates.Employ.Favorite,
                Cans = new[] { CanFavourite(who) },   // the star is the switch, as on Character
                // the Character page's PREGNANT stamp, on this smaller photo
                Baby = who.pregnant != null && who.pregnant.Length > 1 && who.pregnant[0] != -1
                    ? Mathf.Max(0.01f, (12f - who.pregnant[1]) / 12f) : 0f,
                Tips = new[] { TipPregnant },
            });
        }

        private static int DrawFormBand(SheetRow row, SheetLine line, float w, float h)
        {
            float left = SheetPadX;
            float right = w - SheetPadX;
            Color ink = Theme.DossierInk;
            const float ow = 16f, oh = 19f;
            float oy = (h - 2f - oh) * 0.5f;
            PlacePortrait(row, left, oy, ow, oh);
            float x = left + ow + 5f;
            float end = right;
            int planks = 0;
            // the way back, right-aligned on the facts' line; the name gives way to it
            float factsEnd = right;
            if (!string.IsNullOrEmpty(line.Value))
            {
                float bw = MeasureForm(row, line.Value, FormTextSize * 0.8f) + 1f;
                float bx = right - bw;
                const float by = 14f;
                FormText(row, line.Value, FormTextSize * 0.8f, ink, TextAlignmentOptions.MidlineRight, false, bx, by, bw, 6.5f);
                FormRule(row, Hot(row, 0) ? ink : Theme.DossierSoft, bx, by + 6.2f, bw - 1f, Hot(row, 0) ? 0.5f : 0.3f);
                FormHit(row, 0, bx - 1f, by - 1f, bw + 2f, 9f);
                row.TipZones.Add(new Rect(bx - 1f, by - 1f, bw + 2f, 9f));
                row.TipTexts.Add(line.Link != null ? Shared.Loc.Get("links.back_to", "Back to {0}", line.Link.charaName) : Shared.Loc.Get("links.back", "Back to where you were"));
                planks = Mathf.Max(planks, 1);
                factsEnd = bx - 6f;
            }
            else
            {
                FormNoHit(row, 0);
            }
            const float nameSize = 10.5f;
            float nameW = Mathf.Min(MeasureForm(row, line.Label ?? string.Empty, nameSize) + 1f, end - x - 5f);
            FormText(row, line.Label ?? string.Empty, nameSize, line.LabelTint, TextAlignmentOptions.BottomLeft, true,
                x, 0f, nameW, 13f, 0f, fit: true);
            float fy = 14f;
            float fx = x;
            if (line.Style >= 0)
            {
                // the type leads to its people, as every token does (plank 1)
                const float ts = 5f;
                DrawTraitToken(row, line.Style, fx, fy + 0.6f, ts);
                if (Hot(row, 1))
                {
                    HoverFrame(row, fx - 1f, fy - 0.4f, ts + 2f, ts + 2f);
                }
                FormHit(row, 1, fx - 1f, fy - 1f, ts + 2f, ts + 3f);
                row.TipZones.Add(new Rect(fx - 1f, fy - 1f, ts + 2f, ts + 3f));
                row.TipTexts.Add(Shared.Loc.Get("links.type.help", "{0}: click for everyone who is", TypeText(line.Style)));
                planks = Mathf.Max(planks, 2);
                fx += ts + 2f;
            }
            else
            {
                FormNoHit(row, 1);
            }
            FormText(row, line.Label2 ?? string.Empty, FormTextSize * 0.8f, Theme.DossierSoft, TextAlignmentOptions.MidlineLeft,
                false, fx, fy, Mathf.Min(end, factsEnd) - fx, 6.5f, 0f, fit: true);
            FormRule(row, Theme.DossierRule, left, h - 1f, right - left);
            if (line.Baby > 0f)
            {
                // where the head's stamp sits on its 36-wide photo, at this one's size
                const float photo = ow / 36f;
                DrawPregnantStamp(row, line, left + ow * 0.5f + photo, oy + 37f * photo, 0, photo);
            }
            if (line.Cans != null && line.Cans.Length > 0 && line.Cans[0])
            {
                DrawPhotoStar(row, left, oy, ow, line.Starred, 2);   // the Character page's star, the same switch
                planks = 3;
            }
            else
            {
                FormNoHit(row, 2);
            }
            return planks;
        }

        private static string TypeText(int type)
        {
            return type >= 0 ? PersonText(GameWords.Text(_mn), new[] { type }, 0) : string.Empty;
        }

        internal static void OpenRosterAt(int type)
        {
            if (type < 0)
            {
                return;
            }
            string section = TypeText(type);
            ShowSlotView(ViewRoster);
            _rosterSort = SortPersonality;
            // the roster's own heading (SectionOf), in the same key, so the match holds in any language
            _rosterScrollTo = string.IsNullOrEmpty(section) ? Shared.Loc.Get("roster.section.no_personality", "No personality") : section;
            _reachAnchor = null;
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // BindRoster, once sorted: the scroll that puts the asked-for section's
        // heading at the top of the view (the layout clamps it to the end)
        private static void ScrollRosterToSection(float memberGap)
        {
            if (_rosterScrollTo == null)
            {
                return;
            }
            string want = _rosterScrollTo;
            _rosterScrollTo = null;
            float walk = 0f;
            string section = null;
            for (int i = 0; i < Roster.Count; i++)
            {
                string s = SectionOf(Roster[i]);
                if (!string.Equals(s, section))
                {
                    section = s;
                    if (string.Equals(s, want))
                    {
                        _reachScroll = walk;
                        _reachAnchor = null;
                        return;
                    }
                    walk += RosterHeadH + memberGap;
                }
                walk += RosterRowH + memberGap;
            }
        }

        // a token under the pointer: a square drawn round it, as a name is underlined
        private static void HoverFrame(SheetRow row, float x, float y, float w, float h)
        {
            const float t = 0.5f;
            Color ink = Theme.DossierInk;
            FormImage(row, null, 0, ink, x, y, w, t);
            FormImage(row, null, 0, ink, x, y + h - t, w, t);
            FormImage(row, null, 0, ink, x, y, t, h);
            FormImage(row, null, 0, ink, x + w - t, y, t, h);
        }

        // a pressable spot's plank taken back, when the row has nothing there
        private static void FormNoHit(SheetRow row, int k)
        {
            if (row.Planks != null && k < row.Planks.Length && row.Planks[k].gameObject.activeSelf)
            {
                row.Planks[k].gameObject.SetActive(false);
            }
        }

        // THE COMPACT PERSON, PRESSABLE: their type's token (plank kToken) opens the
        // roster at that type, the name (plank kName, underlined under the pointer)
        // targets them, the face says their mood. Returns where it ends.
        private static float DrawCompactPerson(SheetRow row, SheetLine line, string name, float x, float h, float maxNameW,
            int kToken, int kName)
        {
            int type = line.Style;
            float cy = h * 0.5f;
            const float ts = 6.5f;
            if (type >= 0)
            {
                DrawTraitToken(row, type, x, cy - ts * 0.5f, ts);
                if (Hot(row, kToken))
                {
                    HoverFrame(row, x - 1f, cy - ts * 0.5f - 1f, ts + 2f, ts + 2f);
                }
                FormHit(row, kToken, x - 0.5f, 0f, ts + 1f, h);
                row.TipZones.Add(new Rect(x - 0.5f, 0f, ts + 1f, h));
                row.TipTexts.Add(Shared.Loc.Get("links.type.help", "{0}: click for everyone who is", TypeText(type)));
            }
            else
            {
                FormNoHit(row, kToken);
            }
            float nx = x + ts + 2.5f;
            bool away = !string.IsNullOrEmpty(line.Away);
            float nameW = Mathf.Min(MeasureForm(row, name ?? string.Empty, FormTextSize) + 1f, maxNameW - (away ? AwayMarkW : 0f));
            FormText(row, name ?? string.Empty, FormTextSize, line.LabelTint, TextAlignmentOptions.MidlineLeft, false,
                nx, 0f, nameW, h, 0f, fit: true);
            if (Hot(row, kName))
            {
                FormRule(row, line.LabelTint, nx, cy + 3.4f, nameW - 1f, 0.5f);
            }
            FormHit(row, kName, nx, 0f, nameW, h);
            row.TipZones.Add(new Rect(nx, 0f, nameW, h));
            row.TipTexts.Add(away ? line.Away : Shared.Loc.Get("sheet.click_to_target", "Click to target {0}", name));
            float end = DrawMoodFace(row, line, nx + nameW + 1f, cy, 5.2f);
            if (away)
            {
                // the mark: they are not in the world; its tip says where
                FormText(row, "!", FormTextSize, PaperWarn, TextAlignmentOptions.Midline, true, end + 0.5f, 0f, AwayMarkW - 1f, h);
                row.TipZones.Add(new Rect(end, 0f, AwayMarkW, h));
                row.TipTexts.Add(line.Away);
                end += AwayMarkW;
            }
            return end;
        }

        private const float AwayMarkW = 4f;

        // what the mark after an away name says on hover: where they are, and what
        // a press does about it
        private static string AwayTip(CommonStates who)
        {
            string away = AwayWord(who);
            if (away == null)
            {
                return null;
            }
            string name = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            if (away == "prisoner")
            {
                return "<b>" + Shared.Loc.Get("links.away.head", "Not in the world") + "</b>\n"
                    + Shared.Loc.Get("links.away.prisoner", "{0} is held prisoner by enemies. A rescue comes at random, near you", name);
            }
            CarriedWhere(who, out object holder);
            if (away == "carried")
            {
                return "<b>" + Shared.Loc.Get("links.away.head", "Not in the world") + "</b>\n"
                    + Shared.Loc.Get("links.away.carried", "{0} is carried, in a container the game has not loaded", name);
            }
            return "<b>" + Shared.Loc.Get("links.away.head", "Not in the world") + "</b>\n"
                + Shared.Loc.Get("links.away.where", "{0} is {1}.", name, away)
                + (holder is CommonStates keeper ? "\n" + Shared.Loc.Get("sheet.click_to_target", "Click to target {0}", keeper.charaName)
                    : holder != null ? "\n" + Shared.Loc.Get("links.away.target_it", "Click to target it") : string.Empty);
        }

        // THE PERSON MARK: the type's token, the name in ink, and their mood as
        // the roster's own face - a smile while content, a frown otherwise, in
        // the moral's colour. One way to name a person on every page, in this
        // order. The sheets print no kind's colour: the roster sorts by kind, the
        // sheets are about the person - their type and their mood.
        private static Sprite MoodFace(CommonStates who, out Color tint)
        {
            tint = MoralTint(who);
            bool content = who.moral > 0f && (who.debuff == null || who.debuff.discontent == 0);
            return Theme.Kit(content ? "kit_smile" : "kit_frown", 0) ?? MoralFace(content);
        }

        // SOMEONE NOT IN THE WORLD: the game sets dead 5 on a person it carries as
        // an item (NPCManager.Carry) and on one it takes prisoner (NPCToPrisoner),
        // and puts their body away. A link to them has nothing to target - pressing
        // one dropped your target - so they are named, and said where they are.
        private static string AwayWord(CommonStates who)
        {
            if (who == null || who.dead != 5)
            {
                return null;
            }
            if (who.employ == CommonStates.Employ.Prisoner)
            {
                return "prisoner";
            }
            return CarriedWhere(who, out object _) ?? "carried";
        }

        // WHERE SOMEONE CARRIED IS: the item they became sits in a slot that names
        // them (ItemSlot.common) - your bag, someone's bag, or a chest, a cage or a
        // device. `holder` is what to target for them: the bag's owner, or the
        // prop; nothing for your own bag. Every container is walked, so a read is
        // kept a second - the walk, not the finding of them (SceneContainers).
        private sealed class Carried
        {
            public float At;
            public string Where;
            public object Holder;
        }

        private static readonly Dictionary<CommonStates, Carried> CarriedReads = new Dictionary<CommonStates, Carried>();

        private static readonly List<InventorySlot> CarriedScan = new List<InventorySlot>();

        private static string CarriedWhere(CommonStates who, out object holder)
        {
            float now = Time.unscaledTime;
            if (CarriedReads.TryGetValue(who, out Carried read) && now - read.At < 1f)
            {
                holder = read.Holder;
                return read.Where;
            }
            if (CarriedReads.Count > 64) { CarriedReads.Clear(); }
            read = new Carried { At = now };
            ItemSlot[] bag = _mn != null && _mn.inventory != null ? _mn.inventory.itemSlot : null;
            for (int i = 0; bag != null && i < bag.Length && read.Where == null; i++)
            {
                if (bag[i] != null && ReferenceEquals(bag[i].common, who)) { read.Where = Shared.Loc.Get("links.where.bag", "in your bag"); }
            }
            if (read.Where == null)
            {
                foreach (InventorySlot inv in SceneContainers.Live(CarriedScan))
                {
                    if (!Holds(inv, who)) { continue; }
                    CommonStates owner = inv.GetComponent<CommonStates>();
                    if (owner != null)
                    {
                        read.Where = Played(owner) ? Shared.Loc.Get("links.where.bag", "in your bag")
                            : Shared.Loc.Get("links.where.with", "with {0}", string.IsNullOrEmpty(owner.charaName) ? "?" : owner.charaName);
                        read.Holder = Played(owner) ? null : owner;
                    }
                    else
                    {
                        ItemInfo info = inv.GetComponent<ItemInfo>();
                        read.Where = info != null ? Shared.Loc.Get("links.where.in", "in {0}", PropTitle(info)) : Shared.Loc.Get("links.where.container", "in a container");
                        read.Holder = inv.gameObject;
                    }
                    break;
                }
            }
            CarriedReads[who] = read;
            holder = read.Holder;
            return read.Where;
        }

        // harness: where everyone of yours who is away is, and what holds them
        internal static string AwayDump()
        {
            var sb = new System.Text.StringBuilder();
            var all = new HashSet<CommonStates>(Resources.FindObjectsOfTypeAll<CommonStates>());
            foreach (CommonStates c in all)
            {
                string away = c != null && c.gameObject.scene.IsValid() ? AwayWord(c) : null;
                if (away == null) { continue; }
                CarriedWhere(c, out object holder);
                sb.Append(c.charaName).Append(": ").Append(away)
                  .Append(holder is Component k ? " [" + k.name + "]" : holder is GameObject g ? " [" + g.name + "]" : string.Empty).Append("; ");
            }
            return sb.Length > 0 ? sb.ToString() : "no one away";
        }

        private static bool Holds(InventorySlot inv, CommonStates who)
        {
            for (int i = 0; inv != null && inv.slots != null && i < inv.slots.Length; i++)
            {
                if (inv.slots[i] != null && ReferenceEquals(inv.slots[i].common, who)) { return true; }
            }
            return false;
        }

        private static readonly Color FaceWood = new Color32(42, 28, 21, 255);   // the marker's dark wood, read off it

        // how far the face fills: the moral's size either way, out of 100, as the
        // world marker's and the roster's face fill
        private static float MoodFill(CommonStates who)
        {
            return Mathf.Clamp01(Mathf.Abs(who.moral) / 100f);
        }

        // THE FACE AFTER A NAME (Icons[0], in Tint, filled to Frac), drawn as the
        // world marker's footer draws it: the face's silhouette as a track in an
        // ink rim, filled from the bottom in the moral's colour by how far it
        // reaches - not a solid face, so the sheet and the marker read alike.
        // Returns where it ends
        private static float DrawMoodFace(SheetRow row, SheetLine line, float x, float cy, float s)
        {
            Sprite face = line.Icons != null && line.Icons.Length > 0 ? line.Icons[0] : null;
            if (face == null)
            {
                return x;
            }
            const float rim = 0.55f;
            float y = cy - s * 0.5f;
            FormSprite(row, face, new Color(StripRim.r, StripRim.g, StripRim.b, 0.75f), x - rim, y - rim, s + rim * 2f, s + rim * 2f);
            // the eyes and the mouth are cut out of the face: on the marker the dark
            // wood shows through them, so here a disc of that wood sits behind it
            const float inset = 0.08f;
            FormImage(row, "kit_dossier_dot", 0, FaceWood, x + s * inset, y + s * inset, s * (1f - 2f * inset), s * (1f - 2f * inset));
            FormSprite(row, face, StripTrack, x, y, s, s);
            Image fill = FormSprite(row, face, line.Tint, x, y, s, s);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = Mathf.Clamp01(line.Frac);
            return x + s + rim;
        }

        // ---------- the arc: two people's feelings, over the world ----------

        // AN ARC between two of your people (FeelingArcs.cs draws it):
        //  - from a sheet's person to the one a hovered row names, and
        //  - from your target to the villager under the pointer in the world,
        //    while their Social tab is open, with a label: each one's
        //    change a tick, "Selsia -5 · Cora +10".
        // No switch (Forgejo #16): the hover is the context, and an arc lasts
        // as long as it - nothing draws one for good.
        // The colour is the rule both ways: red if either dislikes the other's
        // type, green if either likes it (and neither dislikes), paper for plain;
        // the pill over the target says which way, to them | back.
        // SOLID when their areas overlap - the next tick counts; DASHED when they
        // do not, or love is off for one of them: nothing happens at the next one.
        // Not drawn when either is indoors: interiors are 25 m under the houses.
        // Round 1 drew it as a copy of the game's LinkLineBlue; round 2 (the face
        // lift) put it on the HUD's canvas, with ends.
        private static int _arcFrame = -1;
        private static bool _arcDotted;
        private static GameObject _arcsRoot;
        private static ArcSet _socialArcs, _placeArcs;
        private static readonly List<ArcMark> SocialMarks = new List<ArcMark>(1);

        // what a tick does to a's feeling for b when they meet (GameManager.LoveCheck)
        private static int RuleOf(CommonStates a, CommonStates b)
        {
            if (a.person == null || b.person == null || a.person.Length < 3 || b.person.Length < 1)
            {
                return 5;
            }
            return a.person[1] == b.person[0] ? 10 : a.person[2] == b.person[0] ? -5 : 5;
        }

        private static bool LoveOff(CommonStates who)
        {
            return who.sexInfo != null && who.sexInfo.Length > 8 && who.sexInfo[8] == 1;
        }

        // TickSheets, every frame: a hovered row that names a person
        private static void TickArc(SheetRow hot, Plate plate)
        {
            if (hot == null && _arcHoverUntil >= Time.frameCount)
            {
                hot = ArcHoverRow(out plate);
            }
            CommonStates from = plate != null ? plate.Owner : null;
            CommonStates to = hot != null ? hot.Link : null;
            if (from != null && to != null)
            {
                DrawArc(from, to);
            }
        }

        private static bool DrawArc(CommonStates from, CommonStates to)
        {
            Camera cam = Camera.main;
            // whom a stranger takes to is never shown (Observe.cs), and the arc is that
            if (from == null || to == null || ReferenceEquals(from, to) || !ArcDrawable(from) || !ArcDrawable(to)
                || !Observe.Known(from) || !Observe.Known(to) || cam == null || (_socialArcs = ArcSetFor(_socialArcs, "Social")) == null)
            {
                return false;
            }
            if (MenuOverMarker())
            {
                _socialArcs.Hide();   // the pause menus stand under the layer, as under the marker
                return false;
            }
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            bool meet = npcs != null && !LoveOff(from) && !LoveOff(to) && npcs.NPCDistanceCheck(from, to);
            SocialMarks.Clear();
            SocialMarks.Add(new ArcMark
            {
                Kind = ArcKind.Strand, At = OverHead(to), There = RuleOf(from, to), Back = RuleOf(to, from), Dotted = !meet,
                Name = to.charaName, Metres = Flat(from.transform.position, to.transform.position),
            });
            _socialArcs.Draw(cam, OverHead(from), SocialMarks, ArcSafe());
            _arcDotted = !meet;
            _arcFrame = Time.frameCount;
            return true;
        }

        // just over their head: their body's top where it has one (the target
        // marker's measure), two metres up where it does not
        private static Vector3 OverHead(CommonStates who)
        {
            Vector3 at = who.transform.position;
            float top = at.y + 2f;
            Renderer body = who.GetComponentInChildren<Renderer>();
            if (body != null)
            {
                top = Mathf.Clamp(body.bounds.max.y + 0.3f, at.y + 1.2f, at.y + 3f);
            }
            return new Vector3(at.x, top, at.z);
        }

        // across the ground: an interior's 25 m down is no distance
        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // TICK, before the window's own early returns: your target and the
        // villager under the pointer in the world
        private static void TickWorldArc(object focus)
        {
            CommonStates from = focus as CommonStates;
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            CommonStates to = null;
            bool social = _focusShown && _focusAspect == Aspect.Relations && ReferenceEquals(_target, from);
            if (social && from != null && npcs != null && npcs.IsActiveFriend(from)
                && !OverAnyUi())
            {
                to = WorldHovered();
                if (to != null && (ReferenceEquals(to, from) || !npcs.IsActiveFriend(to)))
                {
                    to = null;
                }
            }
            if (to == null || !DrawArc(from, to))
            {
                ArcLabel(null);
                return;
            }
            int there = RuleOf(from, to), back = RuleOf(to, from);
            bool off = LoveOff(from) || LoveOff(to);
            bool meet = !off && npcs.NPCDistanceCheck(from, to);
            string text = "<b>" + Shared.Loc.Get("links.arc.head", "Each tick they meet") + "</b>\n" + Signed(from.charaName, there) + "\n" + Signed(to.charaName, back)
                + (off ? "\n" + Shared.Loc.Get("links.arc.love_off", "Love off: nothing changes") : meet ? string.Empty : "\n" + Shared.Loc.Get("links.arc.apart", "Not near: nothing at the next tick"));
            ArcLabel(text);
        }

        private static string Signed(string name, int v)
        {
            string ink = v < 0 ? "#E0392B" : v > 5 ? "#4A8A3A" : "#281A10";   // inks on the tip's paper
            // a row of the tip: the name, then the change in its ink
            return name + "  <color=" + ink + ">" + (v > 0 ? "+" + v : "−" + (-v)) + "</color>";
        }

        // the person under the pointer: the game's own ray (PlayerMove, the
        // camera through the cursor), against people's colliders
        private static CommonStates WorldHovered()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return null;
            }
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, LayerMask.GetMask("NPC", "BGNPC") | (1 << 14));
            CommonStates best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                CommonStates c = hits[i].collider != null ? hits[i].collider.GetComponentInParent<CommonStates>() : null;
                if (c != null && hits[i].distance < bestD)
                {
                    best = c;
                    bestD = hits[i].distance;
                }
            }
            return best;
        }

        private static bool OverAnyUi()
        {
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        // the label: the kit's dark tip at the pointer, on a root of its own so
        // it shows with every window closed
        private static GameObject _arcLabelRoot;
        private static KitTooltip _arcLabel;

        private static void ArcLabel(string text)
        {
            if (text == null)
            {
                if (_arcLabel != null) { _arcLabel.Hide(); }
                return;
            }
            if (_arcLabelRoot == null)
            {
                Canvas canvas = _mn != null && _mn.uiMN != null && _mn.uiMN.itemDescPanel != null
                    ? _mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                {
                    return;
                }
                _arcLabelRoot = Owned.New("IslandUI_ArcLabel");
                _arcLabelRoot.transform.SetParent(canvas.rootCanvas.transform, false);
                var r = (RectTransform)_arcLabelRoot.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
                CanvasIsolation.Isolate(_arcLabelRoot);
                _arcLabel = KitTooltip.Create(r);
            }
            Canvas root = _arcLabelRoot.GetComponentInParent<Canvas>();
            Camera cam = root != null && root.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? root.rootCanvas.worldCamera : null;
            _arcLabelRoot.transform.SetAsLastSibling();
            _arcLabel.Tick(text, cam);
        }

        private static bool ArcDrawable(CommonStates who)
        {
            return who != null && who.gameObject.activeInHierarchy && (who.nMove == null || who.nMove.house == null);
        }

        // THE ARCS' LAYER, on the canvas the target marker shares and just under
        // it: over the world and the game's top HUD, under every window. One set
        // for the Social arc, one for Set place's.
        private static ArcSet ArcSetFor(ArcSet set, string name)
        {
            if (set != null && set.Alive && _arcsRoot != null)
            {
                KeepArcsUnderMarker();
                return set;
            }
            if (_arcsRoot == null)
            {
                Transform parent = _worldLayer != null ? _worldLayer.transform.parent : null;
                if (parent == null)
                {
                    Canvas canvas = _mn != null && _mn.uiMN != null && _mn.uiMN.itemDescPanel != null
                        ? _mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>() : null;
                    parent = canvas != null ? canvas.rootCanvas.transform : null;
                }
                if (parent == null)
                {
                    return null;
                }
                _arcsRoot = Owned.New("IslandUI_Arcs");
                _arcsRoot.transform.SetParent(parent, false);
                var r = (RectTransform)_arcsRoot.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                CanvasIsolation.Isolate(_arcsRoot);   // a mesh rebuilt every frame stays out of the HUD's batch
            }
            KeepArcsUnderMarker();
            return ArcSet.Create(_arcsRoot.transform, name);
        }

        // just after the log, under the combat markers and the target marker: the
        // stack KeepMarkerOverHud keeps
        private static void KeepArcsUnderMarker()
        {
            KeepMarkerOverHud();
        }

        // WHERE AN EDGE TAG MAY STAND: the screen, inset, below the log and the
        // clock along the top and above the docks along the bottom (HudTop, the
        // target marker's own measure), in the layer's space
        private const float ArcInset = 6f;

        private static Rect ArcSafe()
        {
            var layer = (RectTransform)_arcsRoot.transform;
            Rect r = layer.rect;
            float top = r.yMax - ArcInset, bottom = r.yMin + ArcInset;
            RectTransform log = _mn != null && _mn.uiMN != null && _mn.uiMN.logPanel != null
                ? _mn.uiMN.logPanel.transform.parent as RectTransform : null;
            if (log != null && log.gameObject.activeInHierarchy)
            {
                log.GetWorldCorners(HudCorners);   // 0 = bottom-left
                top = Mathf.Min(top, layer.InverseTransformPoint(HudCorners[0]).y - ArcInset);
            }
            float hud = HudTop(layer);
            if (!float.IsNegativeInfinity(hud))
            {
                bottom = Mathf.Max(bottom, hud + ArcInset);
            }
            if (top - bottom < 40f)
            {
                top = r.yMax - ArcInset;
                bottom = r.yMin + ArcInset;
            }
            return Rect.MinMaxRect(r.xMin + ArcInset, bottom, r.xMax - ArcInset, top);
        }

        private static void HideArc()
        {
            if (_socialArcs != null) { _socialArcs.Hide(); }
        }

        // the window's own tick: an arc nobody drew this frame (the sheet closed,
        // the pointer left the row) goes
        private static void ArcIdle()
        {
            if (_arcShowUntil >= Time.frameCount && DrawArc(_arcShowA, _arcShowB))
            {
                return;   // the harness's pair, held: drawn again as the camera moves
            }
            if (_arcFrame < Time.frameCount - 1)
            {
                HideArc();
            }
        }

        private static CommonStates _arcShowA, _arcShowB;
        private static int _arcShowUntil = -1;

        // harness: hover the shown row that names this person for ten seconds,
        // as the pointer would - TickArc takes it when nothing else is hot
        private static string _arcHoverName;
        private static int _arcHoverUntil = -1;

        internal static string ArcHover(string name)
        {
            _arcHoverName = name;
            _arcHoverUntil = string.IsNullOrEmpty(name) || name == "-" ? -1 : Time.frameCount + 600;
            if (_arcHoverUntil < 0)
            {
                return "hover off";
            }
            SheetRow row = ArcHoverRow(out Plate plate);
            return row == null ? "no shown row names " + name
                : "hovering " + name + " on " + (plate.Owner != null ? plate.Owner.charaName : "?") + "'s sheet";
        }

        private static SheetRow ArcHoverRow(out Plate plate)
        {
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                plate = SheetPlates[p];
                if (!plate.IsSheet || plate.Rows == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < plate.RowsShown && i < plate.Rows.Count; i++)
                {
                    SheetRow row = plate.Rows[i];
                    if (row.Link != null && string.Equals(row.Link.charaName, _arcHoverName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return row;
                    }
                }
            }
            plate = null;
            return null;
        }

        // harness: the arc between two of yours by name ("A|B"), held ten seconds
        internal static string ArcShow(string arg)
        {
            string[] parts = (arg ?? string.Empty).Split('|');
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            CommonStates a = null, b = null;
            foreach (CommonStates c in People(npcs))
            {
                if (c == null) { continue; }
                if (a == null && parts.Length > 0 && c.charaName == parts[0]) { a = c; }
                else if (b == null && parts.Length > 1 && c.charaName == parts[1]) { b = c; }
            }
            if (!DrawArc(a, b))
            {
                return "no arc: " + (a == null || b == null ? "no such pair" : !ArcDrawable(a) || !ArcDrawable(b) ? "one is indoors or hidden" : "no canvas");
            }
            _arcShowA = a;
            _arcShowB = b;
            _arcShowUntil = Time.frameCount + 600;
            return "arc " + a.charaName + " -> " + b.charaName + ", " + Flat(a.transform.position, b.transform.position).ToString("0")
                + " m, " + (_arcDotted ? "dashed" : "solid");
        }

        // harness: the arcs drawn now, as the screen has them (top-left origin) -
        // the source's seal, each strand's ends and its pill (to them | back), each
        // edge tag; the mockup's ends came from the line this replaced
        internal static string ArcDump()
        {
            var sb = new System.Text.StringBuilder();
            if (_socialArcs != null && _socialArcs.Shown) { sb.Append("SOCIAL\n").Append(_socialArcs.Dump()).Append('\n'); }
            if (_placeArcs != null && _placeArcs.Shown) { sb.Append("PLACE\n").Append(_placeArcs.Dump()).Append('\n'); }
            return sb.Length == 0 ? "no arcs" : sb.ToString().TrimEnd('\n');
        }
    }
}
