using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Gauge tints and the vitals/drives/growth/profile writers.
    internal static partial class InventoryWindow
    {
        // THE GAUGES, drawn as the HUD and the roster draw them: a glyph, a band
        // filled in the value's own colour, the number. Colours are the HUD's
        // (life's pink, the stamina bar's cyan, the food and water icons' tints);
        // libido wears the heart, moral the roster's colour rule, a pregnancy
        // the baby with its twelve-month fill.
        private static readonly Color LifeTint = new Color(1f, 0.22f, 0.42f, 1f);
        private static readonly Color StunTint = new Color(0.96f, 0.96f, 0.96f, 1f);   // the HUD's stamina is white; its cyan is experience
        private static readonly Color FoodTint = new Color(1f, 0.69f, 0.26f, 1f);
        private static readonly Color WaterTint = new Color(0.10f, 0.64f, 0.94f, 1f);
        private static readonly Color LibidoTint = new Color(1f, 0.45f, 0.70f, 1f);
        private static readonly Color BabyTint = new Color(0.98f, 0.85f, 0.75f, 1f);

        private static void WriteVitals(List<SheetLine> lines, CommonStates who)
        {
            GameManager gm = _mn != null ? _mn.gameMN : null;
            lines.Add(Gauge(Shared.Loc.Get("vitals.hp", "HP"), (float)who.life, (float)who.maxLife, LifeTint));
            lines.Add(Gauge(Shared.Loc.Get("vitals.stun", "Stun"), (float)who.faint, (float)who.maxFaint, StunTint));
            lines.Add(Gauge(Shared.Loc.Get("vitals.food", "Food"), who.food, who.maxFood, FoodTint,
                gm != null && gm.foodImage != null ? gm.foodImage.sprite : null, tintIcon: true,
                text: who.food.ToString("0") + " / " + who.maxFood.ToString("0")));
            lines.Add(Gauge(Shared.Loc.Get("vitals.water", "Water"), who.water, who.maxWater, WaterTint,
                gm != null && gm.waterImage != null ? gm.waterImage.sprite : null, tintIcon: true,
                text: who.water.ToString("0") + " / " + who.maxWater.ToString("0")));
        }

        private static void WriteDrives(List<SheetLine> lines, CommonStates who)
        {
            bool flameTinted;
            Sprite flame = LibidoIcon(out flameTinted);
            lines.Add(Gauge(Shared.Loc.Get("vitals.libido", "Libido"), who.libido, who.maxLibido, LibidoTint, flame, tintIcon: flameTinted));
            // the roster's rule: green while content, red when discontent or below zero
            Color moral = MoralTint(who);
            lines.Add(Gauge(Shared.Loc.Get("vitals.moral", "Moral"), Mathf.Abs(who.moral), 100f, moral,
                text: who.moral.ToString("0") + " / 100"));
        }

        // Discontent is RED here, not the game's orange, which did not read as
        // negative. relationColor[4] is #FF9300 and the food pear two icons along
        // is #FFB042 - one hue, two meanings, side by side on the same plate. Only
        // the middle tier is overridden: content keeps the palette's green, and the
        // worst tier keeps its #EE0000, which now reads as the escalation of a red
        // rather than a jump to a different hue.
        private static readonly Color MoralBad = Theme.ParseColor("#E0392B", new Color(0.88f, 0.22f, 0.17f, 1f));

        private static Color MoralTint(CommonStates who)
        {
            Color[] palette = _mn != null && _mn.uiMN != null ? _mn.uiMN.relationColor : null;
            Color good = palette != null && palette.Length > 3 ? palette[3] : new Color(0.35f, 0.8f, 0.35f, 1f);
            Color bad = MoralBad;
            Color worst = palette != null && palette.Length > 5 ? palette[5] : bad;
            int discontent = who.debuff != null ? who.debuff.discontent : 0;
            if (discontent == 4) { return worst; }
            if (discontent != 0) { return bad; }
            return who.moral < 0f ? bad : good;
        }

        // The Condition aspect: everything the header's gauge shows, as numbers.
        // The set must MATCH the icon run - HP · stun | food · water | moral · love
        // · libido | baby - or an icon has no readout anywhere.
        // D75: what OVERVIEW adds beyond the gauges - the facts the roster row
        // taught us are worth having, now with room to be words instead of a
        // glyph and two abbreviations.
        // FOUR FACTS, TWO LINES. Eight short words down four rows
        // left the sheet's right half empty and the block twice as tall as it
        // needs - and the sheet is as wide as a bag grid, because D49 keeps the
        // column from moving when you switch tabs. Two columns fill the measure,
        // which is the roster row's own lesson about density.
        //
        // The pairing is not arbitrary: WHERE they are reads under WHAT they do,
        // and how they FEEL under whether they follow you - the left column is
        // their work, the right column is their relationship to you.
        private static void WriteStanding(List<SheetLine> lines, CommonStates who)
        {
            NPCMove move = who.nMove;
            lines.Add(new SheetLine { Kind = LineSpace });
            int job = move != null ? (int)move.workType : 0;
            bool following = move != null && move.npcType == NPCMove.NPCType.Follow;
            lines.Add(new SheetLine
            {
                Kind = LinePair,
                Label = Shared.Loc.Get("vitals.job", "Job"), Value = job != 0 ? JobName(job) : "—",
                Label2 = Shared.Loc.Get("vitals.following", "Following"), Value2 = following ? Shared.Loc.Get("sheet.yes", "yes") : Shared.Loc.Get("sheet.no", "no"),
            });
            // The mood left this pair for a block of its own under it (WriteMood,
            // the mood census): every cause with its cost and its fix, where one
            // word beside Where could only name the first.
            lines.Add(new SheetLine
            {
                Kind = LinePair,
                Label = Shared.Loc.Get("vitals.where", "Where"), Value = WhereWord(who, move),
                Label2 = string.Empty, Value2 = string.Empty,
            });
        }

        private static void WriteCondition(List<SheetLine> lines, CommonStates who, bool own)
        {
            WriteVitals(lines, who);
            if (!own)
            {
                WriteLove(lines, who);
            }
            WriteDrives(lines, who);
            if (who.pregnant != null && who.pregnant.Length > 1 && who.pregnant[0] != -1)
            {
                float months = 12f - who.pregnant[1];
                lines.Add(Gauge(Shared.Loc.Get("vitals.pregnant", "Pregnant"), months, 12f, BabyTint, Emotion(7),
                    text: (100f * months / 12f).ToString("F0") + "%"));
            }
        }

        // Their love for you, the header's heart as a number. One direction only:
        // the game keeps no edge from the player outward (§6.20), so there is
        // nothing to state back. An unrecorded edge is not a zero, and says so.
        private static void WriteLove(List<SheetLine> lines, CommonStates who)
        {
            CommonStates me = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            if (me == null || _mn.npcMN == null || ReferenceEquals(me, who))
            {
                return;
            }
            Sprite heart = Theme.Kit("kit_heart", 0) ?? Emotion(0);
            float love;
            if (!LoveBetween(_mn.npcMN, who, me, out love))
            {
                lines.Add(Gauge(Shared.Loc.Get("vitals.love", "Love"), 0f, 100f, LoveWidget.Pale, heart, tintIcon: true, text: Shared.Loc.Get("vitals.love.not_met", "not met")));
                return;
            }
            // the number bare, for the same reason: 100 is where the band fills, not
            // where the value stops - a devoted follower reads 110 and the game is
            // right, not the label
            lines.Add(Gauge(Shared.Loc.Get("vitals.love", "Love"), Mathf.Abs(love), 100f, LoveWidget.ClassColor(love), heart,
                tintIcon: true, text: love.ToString("0")));
        }

        // an attribute's name on the Growth sheet, in the player's language
        private static string StatLabel(int i)
        {
            switch (i)
            {
                case 0: return Shared.Loc.Get("vitals.health", "Health");
                case 1: return Shared.Loc.Get("vitals.strength", "Strength");
                case 2: return Shared.Loc.Get("vitals.speed", "Speed");
                default: return StatNames[i];
            }
        }

        // The Growth aspect, both: the level and its exp, the points and where
        // they go, and beside them what they buy - attack, guard, speed
        private static void WriteGrowth(List<SheetLine> lines, CommonStates who)
        {
            lines.Add(new SheetLine { Label = Shared.Loc.Get("vitals.character_level", "Character level {0}", who.level), Kind = LineBar,
                Value = who.exp.ToString("0") + " / " + who.maxExp.ToString("0"),
                Frac = who.maxExp > 0 ? (float)(who.exp / who.maxExp) : 0f, Enabled = true,
                Tinted = true, Tint = ExpTint() });
            lines.Add(Head(Shared.Loc.Get("sheet.points", "Points"), who.statusPoint.ToString(), VanillaTabIcon(0)));
            if (who.status != null)
            {
                for (int i = 0; i < StatNames.Length && i < who.status.Length; i++)
                {
                    int cost = StatCost(who, i);
                    lines.Add(new SheetLine { Label = StatLabel(i), Kind = LineStat, Arg = i,
                        Value = Shared.Loc.Get("sheet.lv", "Lv {0}", who.status[i]) + "   " + Shared.Loc.Get("sheet.cost", "{0} pt", cost),
                        Enabled = who.statusPoint >= cost });
                }
            }
            lines.Add(Head(Shared.Loc.Get("vitals.combat", "Combat"), string.Empty, Theme.FindSprite("ui_buff_attack")));
            string attack = who.attack.ToString("F1");
            if (who.equip != null && who.equip.Length > 1 && who.equip[1] != null)
            {
                attack += " (" + who.equip[1].attack.ToString("0") + ")";
            }
            lines.Add(Row(Shared.Loc.Get("vitals.attack", "Attack"), attack));
            lines.Add(Row(Shared.Loc.Get("vitals.guard", "Guard"), GuardOf(who).ToString("0.0")));
            lines.Add(Row(Shared.Loc.Get("vitals.speed", "Speed"), who.MoveSpeed(false).ToString("0.00"), Theme.FindSprite("ui_buff_speed")));
            if (who.status != null && who.status.Length > 7 && who.status[7] > 0)
            {
                lines.Add(Row(Shared.Loc.Get("vitals.class", "Class"), who.status[7].ToString()));
            }
        }

        // The Profile aspect, both: how they ARE, then who they are. An earlier
        // pass retired Condition as a tab and gave the header its icons - but an
        // icon is a glance, not a readout, and the numbers were left with nowhere
        // to be read, and every stat on the gauge belongs somewhere in the sheets.
        // Profile is where they land, above the traits, for BOTH columns.
        private static void WriteProfile(List<SheetLine> lines, CommonStates who, bool own)
        {
            TextManager tm = GameWords.Text(_mn);
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            // An earlier pass parked the condition block here because retiring the Condition
            // tab orphaned it. D75 gives it a tab again, so Profile hands it back
            // and returns to fixed facts - EXCEPT on your own column, which has no
            // Overview tab: two live gauges do not earn one (§6.21), so yours stay.
            if (own)
            {
                WriteCondition(lines, who, own);
            }
            lines.Add(Head(Shared.Loc.Get("vitals.preferences", "Preferences"), string.Empty, null));
            lines.Add(new SheetLine { Kind = LineToggle, Arg = ToggleNameShown,
                Label = Shared.Loc.Get("vitals.show_name", "Show name in world"), On = who.nameVisible != null, Enabled = true,
                Tip = own ? TipShowNameOwn : TipShowName });
            if (!own && (who.employ == CommonStates.Employ.Friend || who.employ == CommonStates.Employ.Favorite))
                lines.Add(new SheetLine { Kind = LineToggle, Arg = ToggleFavourite,
                    Label = Shared.Loc.Get("vitals.favourite", "Favorite in roster"), On = who.employ == CommonStates.Employ.Favorite, Enabled = true,
                    Tip = TipFavourite });
            lines.Add(new SheetLine { Kind = LineSpace });
            lines.Add(Row(Shared.Loc.Get("vitals.age", "Age"), who.age.ToString()));
            // GetLocalizedCharaName returns the character TYPE's name, which for a
            // unique character IS their own - "Type  Takumi" under a plate saying
            // Takumi. A kind that only repeats the plate is not said twice.
            string kind = KindName(npcs, who);
            if (!string.IsNullOrEmpty(kind)
                && !string.Equals(kind, who.charaName, System.StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(Row(Shared.Loc.Get("vitals.type", "Type"), kind));
            }
            lines.Add(Row(Shared.Loc.Get("vitals.personality", "Personality"), PersonText(tm, who.person, 0)));
            lines.Add(Row(Shared.Loc.Get("vitals.likes", "Likes"), PersonText(tm, who.person, 1)));
            lines.Add(Row(Shared.Loc.Get("vitals.dislikes", "Dislikes"), PersonText(tm, who.person, 2)));
            // A parent nobody recorded is no row at all. FriendName returns empty
            // for status -1, and a generated villager has both, so the two blank
            // rows were on almost every follower's sheet saying nothing (seen on
            // the merged sheet). An absent fact is absent, which is
            // the same rule the Type row above already follows.
            string father = FriendName(npcs, who.status, 8);
            string mother = FriendName(npcs, who.status, 9);
            if (!string.IsNullOrEmpty(father))
            {
                lines.Add(Row(Shared.Loc.Get("vitals.father", "Father"), father));
            }
            if (!string.IsNullOrEmpty(mother))
            {
                lines.Add(Row(Shared.Loc.Get("vitals.mother", "Mother"), mother));
            }
            if (who.status != null && who.status.Length > 7 && who.status[7] > 0)
            {
                lines.Add(Row(Shared.Loc.Get("vitals.class", "Class"), who.status[7].ToString()));
            }
            if (SaveManager.SaveSettingStatic.trackSurgeries == 1 && who.status != null && who.status.Length > 6)
            {
                lines.Add(Row(Shared.Loc.Get("vitals.surgeries", "Surgeries"), who.status[6].ToString()));
            }
            // RELATIONS MERGES INTO PROFILE. Six tabs on a follower is one or
            // two too many, and the set was inherited from vanilla's status window
            // rather than derived - and vanilla's design is broken at its core
            // there. Profile and Relations both answer WHO THIS PERSON IS; the
            // others answer what you can SET. Merged, the five that remain each
            // have a distinct verb - read about them, read their state, change
            // their things, change their orders, spend their growth.
            //
            // Brief is the point: the player edge, the strongest few, the hostile
            // few, and "… N more" for the rest, which opens the matrix window that
            // already exists. The full listing is one press further out, where a
            // listing belongs.
            // THE CHARACTER PAGE: who they are, then how they grow -
            // level, points, the attributes with the spend at the foot, and the
            // combat numbers, which are the character's and not the loadout's.
            // Relations and history have gone to the Social page.
            lines.Add(new SheetLine { Kind = LineSpace });
            WriteGrowth(lines, who);
        }

        // The kind behind the name: "Type-B", "Nyan" - the game's own stats row
        // for an npcID, localised. Cut from the name plate along with the age,
        // so this sheet is where it belongs.
        private static string KindName(NPCManager npcs, CommonStates who)
        {
            if (npcs == null || who == null)
            {
                return string.Empty;
            }
            StatsData stats = npcs.GetStatsData(who.npcID);
            return stats != null ? stats.GetLocalizedCharaName() : string.Empty;
        }

        // SkillManager.CostCheckStatus, stated here so the sheet needs no panel
        // to read it off
        private static int StatCost(CommonStates who, int id)
        {
            return Mathf.FloorToInt(who.status[id] / 10) + 1;
        }

        // ---- the sheet's controls: polled like every other surface here ----

        private static SheetRow _sheetHot;
        private static SheetRow _sheetPressed;
        private static Plate _sheetHotPlate;
        private static int _sheetHotSub = -1;      // which plank of an actions row
        private static int _sheetPressedSub = -1;
    
        // THE ACTS, AT THE FOOT OF OVERVIEW (target-matrix.md G7).
        //
        // Two attempts put these on the FRAME - chips on the rim, then a band of
        // their own - and both were dropped. What they had in common is the
        // mistake: acts are not frame furniture, they are contents. This sheet is
        // where you ask how a thing is and what you can do with it, and those are
        // one question asked at one moment.
        //
        // The SET is read from vanilla and never derived (D65): TargetVerbs asks
        // NPCPanel what it would have lit and reads activeSelf off the hidden
        // panel, so a verb added by a patch turns up here wearing its own label
        // rather than vanishing. Only the LABELS are copied into the line; the
        // press re-reads, because the set can change between bind and click.
        //
        // Placement acts - Move, Remove, Repair - are NOT here. They belong to the
        // glyph row top right (G6), and they arrive with the prop work.
        private static void WriteActs(System.Collections.Generic.List<SheetLine> lines, CommonStates who)
        {
            if (who == null || _mn == null)
            {
                return;
            }
            TargetVerbs.Read(_mn, who);
            System.Collections.Generic.IList<TargetVerbs.Verb> live = TargetVerbs.Current;
            if (live.Count == 0)
            {
                return;
            }
            bool near = InReach(who);
            lines.Add(new SheetLine { Kind = LineSpace });
            for (int i = 0; i < live.Count; i += ActsPerRow)
            {
                int n = Mathf.Min(ActsPerRow, live.Count - i);
                var labels = new string[n];
                Sprite[] icons = null;
                bool anyFree = false;
                for (int k = 0; k < n; k++)
                {
                    TargetVerbs.Verb v = live[i + k];
                    labels[k] = v.Label;
                    anyFree = anyFree || ReachFree(v.Name);
                    // a button with no words of its own is drawn as its art
                    if (TargetVerbs.Wordless(v) && v.Icon != null)
                    {
                        if (icons == null) { icons = new Sprite[n]; }
                        icons[k] = v.Icon;
                    }
                }
                lines.Add(new SheetLine
                {
                    Kind = LineActs,
                    Arg = i,                       // where this row starts in the live set
                    Options = labels,
                    Icons = icons,
                    Enabled = near || anyFree,
                });
            }
        }

        // Three to a row: the sheet is ~245 units, so three planks run ~75 each,
        // which holds "Give birth" without shrinking the type. Five would not.
        private const int ActsPerRow = 3;

        // Pressed THROUGH vanilla's own button, so its handler runs and whatever
        // it opens opens the way it always did (D65). The set is re-read first:
        // it was resolved at bind, and a follower can start following between the
        // bind and the click.
        private static void ActPress(CommonStates who, int index)
        {
            if (who == null || _mn == null || index < 0)
            {
                return;
            }
            TargetVerbs.Read(_mn, who);
            System.Collections.Generic.IList<TargetVerbs.Verb> live = TargetVerbs.Current;
            if (index >= live.Count)
            {
                return;
            }
            if (!InReach(who) && !ReachFree(live[index].Name))
            {
                return;
            }
            TargetVerbs.Press(live[index]);
        }

        // A PROP'S OVERVIEW (target-matrix.md G3/G7). Wear, then the acts -
        // and for a fixture that is the whole page, which is the point: the frame
        // IS the indicator that you have a target, and without one there is
        // nowhere for the act to sit.
        //
        // Wear is NOT health. One field on an ItemInfo, another on a CommonStates,
        // and the code sharing a name for them does not make them one thing to a
        // player: a building at zero is rubble and is repaired, a person at zero
        // is dying and is healed (target-matrix.md D1).
        private static void WritePropSheet(System.Collections.Generic.List<SheetLine> lines, ItemInfo prop)
        {
            if (prop == null)
            {
                return;
            }
            // ITEMINFO.LIFE IS NOT ALWAYS WEAR (found on the
            // pig farm). On a FactoryBreed it is BREEDING PROGRESS, and the code
            // says so exactly (NPCManager:6279, WorkType.Breed): a worker on the
            // Breed job adds 2 per tick while life < 100, dropping poop_01 into
            // the prop's own chest one tick in four; at 100 it resets to 0 and
            // puts an animal there instead - animal_pig_01 four times in five,
            // animal_pig_02 the fifth.
            //
            // So it is how far along the next pig is. Calling that Wear would say
            // a farm about to give birth is undamaged and one that just did is a
            // wreck. Same field, opposite meaning - the rule this whole design is
            // built on (target-matrix.md, the opening rule).
            //
            // The 100 is vanilla's, hardcoded in both FactoryBreedUse's readout
            // and the work tick, so it is hardcoded here rather than read from
            // lifeMax - which is 0 on this prop anyway.
            var slot = prop.GetComponent<InventorySlot>();
            // A WELL AND A FOOD STORE: the level is slots[0].itemLife, and
            // the maximum is per item KEY, not a field - vanilla hardcodes it in
            // WaterBucketUse (well_01 1000, well_02 2000, anything else 300) and
            // FoodStorageUse (foodstorage_02 2000, else 1000). So it is copied
            // here, which is the one place this design copies rather than reads:
            // there is no field to read it from.
            if (InventoryWindow.IsLevelProp(slot))
            {
                bool water = slot.type == InventorySlot.Type.Water;
                float max = water
                    ? (prop.itemKey == "well_01" ? 1000f : prop.itemKey == "well_02" ? 2000f : 300f)
                    : (prop.itemKey == "foodstorage_02" ? 2000f : 1000f);
                float now = slot.slots != null && slot.slots.Length > 0 ? slot.slots[0].itemLife : 0f;
                lines.Add(new SheetLine
                {
                    Label = water ? Shared.Loc.Get("vitals.water", "Water") : Shared.Loc.Get("vitals.food", "Food"), Kind = LineBar, Enabled = true,
                    Tinted = true, Tint = water ? new Color(0.20f, 0.57f, 0.75f) : new Color(0.75f, 0.48f, 0.18f),
                    Value = now.ToString("0") + " / " + max.ToString("0"),
                    Frac = max > 0f ? Mathf.Clamp01(now / max) : 0f,
                });
            }
            else if (slot != null && slot.type == InventorySlot.Type.FactoryBreed)
            {
                lines.Add(new SheetLine
                {
                    Label = Shared.Loc.Get("vitals.breeding", "Breeding"), Kind = LineBar, Enabled = true,
                    Tinted = true, Tint = new Color(0.76f, 0.48f, 0.24f),
                    Value = prop.life.ToString("0") + " / 100",
                    Frac = Mathf.Clamp01(prop.life / 100f),
                });
            }
            // WEAR IS A READING, AND READINGS ARE THE BODY'S (on the rim band it
            // lived at the top of the window like a glyph, but it is a progress
            // bar). One line, the same form as Water, on every thing
            // that can be damaged - after the thing's own level, and never on a
            // farm, whose life is progress (G9). The acts are the row's.
            if (prop.lifeMax > 0f && !(slot != null && slot.type == InventorySlot.Type.FactoryBreed))
            {
                lines.Add(new SheetLine
                {
                    Label = Shared.Loc.Get("vitals.wear", "Wear"), Kind = LineBar, Enabled = true,
                    Tinted = true, Tint = prop.life / prop.lifeMax < 0.25f
                        ? new Color(0.77f, 0.30f, 0.22f) : new Color(0.40f, 0.58f, 0.32f),
                    Value = prop.life.ToString("0") + " / " + prop.lifeMax.ToString("0"),
                    Frac = Mathf.Clamp01(prop.life / prop.lifeMax),
                });
            }
        }

        // MOVE, REMOVE AND REPAIR, IN THE WINDOW. They were chips on the top rail
        // for one build, and that was the wrong place: they belong beside the
        // wear, in the body, as small buttons inside the window that also
        // displays the wear.
        //
        // Which is consistent with where the other acts went. G6 split them off
        // because they are a different KIND - about the object as something you
        // built - and the split still holds; what it does not license is a
        // different PLACE. Both kinds are contents. Their difference is now said
        // by drawing them smaller and by their own row, not by exile to the rim.
        //
        // Read by NAME off buildPanel, so vanilla's own swap comes for free: Move
        // and Remove normally, Repair alone once the thing is broken.
        private static void WritePlacement(System.Collections.Generic.List<SheetLine> lines)
        {
            if (_mn == null)
            {
                return;
            }
            PropVerbs.ReadPlacement(_mn);
            System.Collections.Generic.IList<PropVerbs.Act> live = PropVerbs.Placement;
            if (live.Count == 0)
            {
                return;
            }
            var labels = new string[live.Count];
            var icons = new Sprite[live.Count];
            for (int i = 0; i < live.Count; i++)
            {
                labels[i] = live[i].Label;
                icons[i] = live[i].Icon;
            }
            lines.Add(new SheetLine
            {
                Kind = LinePlace, Arg = 0, Options = labels, Icons = icons, Enabled = true,
            });
        }

        // Pressed through vanilla's own button (D65), re-read first because Move
        // and Remove swap to Repair the moment the thing breaks.
        private static void PlacePress(int index)
        {
            if (_mn == null || index < 0)
            {
                return;
            }
            PropVerbs.ReadPlacement(_mn);
            if (index < PropVerbs.Placement.Count)
            {
                PropVerbs.Press(PropVerbs.Placement[index]);
            }
        }

        // The acts vanilla lit for this prop, read and never re-derived (D82/D85).
        // If vanilla has lit nothing - because it never opened its panel for this
        // object - we say nothing, which is the price of never calling
        // PropPanelTypeChange ourselves. See PropVerbs for why that price is worth
        // paying.
        private static void WritePropActs(System.Collections.Generic.List<SheetLine> lines, ItemInfo prop)
        {
            if (_mn == null)
            {
                return;
            }
            PropVerbs.Read(_mn, prop);
            System.Collections.Generic.IList<PropVerbs.Act> live = PropVerbs.Current;
            if (live.Count == 0)
            {
                return;
            }
            if (lines.Count > 0)
            {
                lines.Add(new SheetLine { Kind = LineSpace });
            }
            for (int i = 0; i < live.Count; i += ActsPerRow)
            {
                int n = Mathf.Min(ActsPerRow, live.Count - i);
                var labels = new string[n];
                for (int k = 0; k < n; k++)
                {
                    labels[k] = live[i + k].Label;
                }
                lines.Add(new SheetLine { Kind = LineActs, Arg = i, Options = labels, Enabled = true });
            }
        }

        // Pressed through vanilla's own button (D65). Re-read first, for the same
        // reason a person's acts are: the panel's contents belong to whatever
        // vanilla last opened, and that can change under us.
        private static void PropActPress(ItemInfo prop, int index)
        {
            if (prop == null || _mn == null || index < 0)
            {
                return;
            }
            PropVerbs.Read(_mn, prop);
            System.Collections.Generic.IList<PropVerbs.Act> live = PropVerbs.Current;
            if (index >= live.Count)
            {
                return;
            }
            PropVerbs.Press(live[index]);
        }

        // Has this prop anything for a sheet at all? A pen with no durability, no
        // level and no act vanilla has lit has nothing to say, and a sheet bound
        // with zero lines still draws its rule and its padding - a divider under
        // the grid with empty parchment below it. A target with only an inventory
        // and nothing else to display draws no separator.
        //
        // Asked by running the same writers, so it can never disagree with what
        // WritePropSheet would produce.
        private static readonly System.Collections.Generic.List<SheetLine> _propProbe
            = new System.Collections.Generic.List<SheetLine>();

        private static bool PropSheetEmpty(ItemInfo prop)
        {
            _propProbe.Clear();
            WritePropSheet(_propProbe, prop);
            return _propProbe.Count == 0;
        }

        // The prop's own name, from its item record - the same source the vicinity
        // list's chest titles use.
        private static string PropTitle(ItemInfo prop)
        {
            if (prop == null || _mn == null || _mn.itemMN == null)
            {
                return Shared.Loc.Get("vitals.prop", "Prop");
            }
            try
            {
                ItemData data = _mn.itemMN.FindItem(prop.itemKey);
                if (data != null)
                {
                    string name = data.GetItemName();
                    if (!string.IsNullOrEmpty(name)) { return name; }
                }
            }
            catch (System.Exception) { }
            return prop.itemKey;
        }
}
}
