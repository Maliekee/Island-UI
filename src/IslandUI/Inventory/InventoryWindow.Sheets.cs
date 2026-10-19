using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Aspects, foot tabs, sheet rows and their paint (§6.20).
    internal static partial class InventoryWindow
    {
        // ---------- §6.20: aspects, their tabs, and the sheets ----------

        private static int ColumnGroup(int column)
        {
            switch (column)
            {
                case OwnColumn: return GroupOwn;
                case CharacterColumn: return GroupCharacter;
                default: return GroupReach;
            }
        }

        private static Aspect AspectOf(int group)
        {
            switch (group)
            {
                case GroupOwnSheet: return _ownAspect;
                case GroupCharacter: return _focusAspect;
                default: return Aspect.Inventory;
            }
        }

        // The glyphs, all the game's own: the bag for Inventory; vanilla's four
        // tab icons off the (inactive) status panel - the face for Profile, the
        // skill star, the pair for Relations, the hand for Orders; the heart the
        // game draws over a character's head for Condition; the sword of the
        // attack buff for Growth.
        private static readonly string[] VanillaAspectButtons =
            { "statusButton", "skillButton", "erostaButton", "orderButton" };
        private static Sprite[] _vanillaAspectIcons;

        private static Sprite VanillaTabIcon(int i)
        {
            if (_vanillaAspectIcons == null)
            {
                _vanillaAspectIcons = new Sprite[VanillaAspectButtons.Length];
                Transform panel = _mn != null && _mn.uiMN != null && _mn.uiMN.statusPanel != null
                    ? _mn.uiMN.statusPanel.transform : null;
                for (int k = 0; panel != null && k < VanillaAspectButtons.Length; k++)
                {
                    Transform glyph = panel.Find(VanillaAspectButtons[k] + "/Image");
                    Image img = glyph != null ? glyph.GetComponent<Image>() : null;
                    _vanillaAspectIcons[k] = img != null ? img.sprite : null;
                }
            }
            return i >= 0 && i < _vanillaAspectIcons.Length ? _vanillaAspectIcons[i] : null;
        }

        // FXManager.emotionImages: the marks the game floats over heads and puts
        // on roster rows - heart 0, the job glyphs 1-6/8/9/11/15, baby 7, star
        // 10, anger 12, water 14, attention 16
        private static Sprite Emotion(int i)
        {
            Sprite[] set = _mn != null && _mn.fxMN != null ? _mn.fxMN.emotionImages : null;
            return set != null && i >= 0 && i < set.Length ? set[i] : null;
        }

        // D76: THE TAB SET COMES FROM WHAT A TARGET HAS, NOT WHAT KIND IT IS.
        // Ask each question of the target; the aspect appears when the answer is
        // yes. A chest has a bag and nothing else, a corpse a bag and spent
        // vitals, a shopkeeper no orders - and none of them is named here, which
        // is the point: they fall out of presence, so nothing needs a kind test.
        //
        // The arrays above give the ORDER; this gives the membership.
        // PERMISSION: ALLEGIANCE ALONE DECIDES WHAT MAY BE SHOWN, AND STATE
        // NEVER TOUCHES IT (target-matrix.md D2/D89). A dead follower keeps every
        // column - it is the moment you most need their condition, since it
        // decides whether you revive, carry or leave them - and a hostile gets
        // none of it, alive or dead. Death revokes nothing a friend showed you
        // and grants nothing an enemy withheld.
        //
        // Read from vanilla's OWN hostility tests rather than a list of ours
        // (D65): charaType 100 is what InteractNPC attacks on sight, and
        // debuff.discontent == 4 is what makes a follower attack you back. Outlaw
        // is the third, and it is the employ value, not a kind.
        //
        // Animals are deliberately absent. They are charaType 10 and not hostile
        // by this test, so presence keeps giving them whatever they have - the
        // penned-animal question is deferred, with no special logic for now
        // (target-matrix.md F5.2).
        private static bool Hostile(CommonStates who)
        {
            if (who == null)
            {
                return false;
            }
            if (who.charaType == 100 || who.employ == CommonStates.Employ.Outlaw)
            {
                return true;
            }
            return who.debuff != null && who.debuff.discontent == 4;
        }

        // A BODY THAT IS NOT A FRIEND'S HAS NO SHEET: a dead enemy pulls up no
        // sheet, but a friendly being who is downed still does. Down in any of vanilla's states - 1 on the
        // ground, 5 carried, 100 the end state a killed follower's and a dissected
        // body's parts sit in - with no life left, and not employed: a follower who
        // died is Employ.Dead, which Employed() refuses, and a downed friend is still
        // Friend. The unit still names the body and offers its acts.
        private static bool DeadStranger(CommonStates who)
        {
            return who != null && who.dead != 0 && who.life <= 0.0 && !who.Employed();
        }

        // For the harness: who in the scene is down, and whether each would get a sheet
        internal static string SheetDoorDump()
        {
            var sb = new System.Text.StringBuilder();
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c == null || c.dead == 0)
                {
                    continue;
                }
                sb.Append(string.IsNullOrEmpty(c.charaName) ? "?" : c.charaName)
                    .Append(" dead=").Append(c.dead).Append(" life=").Append(c.life.ToString("0"))
                    .Append(" employ=").Append(c.employ).Append(" type=").Append(c.charaType)
                    .Append(" loot=").Append(c.GetComponent<DropItem>() is DropItem d && d.enabled)
                    .Append(" sheet=").Append(TargetHasSheet(c))
                    .Append(" pockets=").Append(PocketsOpen(c)).Append(" | ");
            }
            return sb.Length > 0 ? sb.ToString() : "nobody down";
        }

        // whether a target has any page of the dossier - the Details door's question
        private static bool TargetHasSheet(CommonStates who)
        {
            for (int i = 0; i < TheirAspects.Length; i++)
            {
                if (HasAspect(TheirAspects[i], who, false))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasAspect(Aspect aspect, CommonStates who, bool own)
        {
            if (aspect != Aspect.Inventory && aspect != Aspect.Overview && DeadStranger(who))
            {
                return false;
            }
            switch (aspect)
            {
                case Aspect.Inventory:
                    return (who.bag != null && who.bag.slots != null && who.bag.slots.Length > 0)
                        || HasLoadout(who);
                case Aspect.Overview:
                    // Overview is the INTERACTION sheet (G7): it exists when there
                    // is something to read OR something to do. A hostile shows no
                    // readings at all, but its acts - carry, capture, dissect -
                    // still need the page, so the gate is not applied here.
                    return who.maxLife > 0.0;
                case Aspect.Fight:
                    return !Hostile(who) && who.status != null && who.status.Length > 2;
                case Aspect.Relations:
                    return !Hostile(who) && who.lovers != null;
                case Aspect.Orders:
                    // you order the people you employ, and you do not order yourself
                    return !own && !Hostile(who) && who.Employed();
                case Aspect.Skills:
                    return own;      // one global book, and it is the survivor's (§6.21)
                case Aspect.Profile:
                    // everything has fixed facts, even a corpse - but a hostile's
                    // name, kind and personality are exactly what D89 withholds
                    return !Hostile(who);
                default:
                    return false;
            }
        }

        private static readonly List<Aspect> _aspectBuf = new List<Aspect>();

        // Rebuilt only when the membership actually changes, since the tab row is
        // rebound from this array on every refresh and a fresh one each time would
        // allocate for nothing.
        private static Aspect[] AspectsFor(CommonStates who, bool own, Aspect[] order, Aspect[] last)
        {
            if (who == null)
            {
                return order;
            }
            _aspectBuf.Clear();
            for (int i = 0; i < order.Length; i++)
            {
                if (HasAspect(order[i], who, own))
                {
                    _aspectBuf.Add(order[i]);
                }
            }
            if (last != null && last.Length == _aspectBuf.Count)
            {
                bool same = true;
                for (int i = 0; i < last.Length && same; i++)
                {
                    same = last[i] == _aspectBuf[i];
                }
                if (same)
                {
                    return last;
                }
            }
            return _aspectBuf.ToArray();
        }

        private static Sprite AspectIcon(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Inventory: return OwnBagIcon;
                case Aspect.Overview: return Emotion(0);
                case Aspect.Fight: return Theme.FindSprite("ui_buff_attack") ?? VanillaTabIcon(0);
                case Aspect.Skills: return VanillaTabIcon(1);
                case Aspect.Profile: return VanillaTabIcon(0);
                case Aspect.Relations: return VanillaTabIcon(2);
                case Aspect.Orders: return VanillaTabIcon(3);
                default: return null;
            }
        }

        // UIManager.StatusView's toggle, landed on the column (StatusOpeners):
        // `focus` is the employed NPC vanilla would have shown, or null for you.
        // Returns false when the window cannot serve it, so vanilla's sheet can.
        internal static bool ToggleStatusSheet(ManagersScript mn, CommonStates focus)
        {
            if (mn == null || !mn.allSet || mn.uiMN == null || !EnsureBuilt(mn))
            {
                // vanilla's sheet serves this one, so the door is never walked
                // through - spend it, or it waits here for an unrelated target
                _doorArmed = Door.None;
                return false;
            }
            Plugin.Log.LogInfo("Status sheet: " + (focus != null ? focus.charaName : "you")
                + " (own " + _ownOpen + "/" + _ownAspect + ", focus " + _focusShown + "/" + _focusAspect + ")");
            if (focus == null)
            {
                // yours: the same key-toggle as B, landing on Growth - what the
                // status button was for is the points - and a second press with
                // it up closes, as vanilla's does
                _ownSheetOpen = !_ownSheetOpen;
                _ownAspect = Aspect.Profile;
            }
            else
            {
                // theirs: the Focus follows menuNPC on its own (ResolveTarget), so
                // the column is already theirs or about to be; only the aspect is
                // ours to set, and §6.25 D93 is what sets it - this is a DOOR, and
                // which one depends on who armed it. The roster arms its own before
                // calling StatusView; anything else that reaches here is the Status
                // verb or its key.
                //
                // It used to land on Fight unconditionally, and that was a
                // residue: Condition had just been retired and the points were what
                // the Status button was for. D75 un-retired it soon after and G7
                // made Overview the interaction sheet, so the line had been opening
                // a roster pick on combat statistics ever since - nobody's intention
                // and nobody's decision.
                Door door = _doorArmed != Door.None ? _doorArmed : Door.StatusVerb;
                // Pressed again with it up, it lets the Focus go, which is what
                // closing vanilla's sheet did to menuNPC. "Up" means up at the page
                // THIS door lands on - and the set is safe to read here, because
                // this branch only runs when the target is already the shown one.
                if (_focusShown && ReferenceEquals(_target, focus)
                    && _focusAspect == LandOn(door, _footSets[GroupCharacter]))
                {
                    // ...it folds the SHEET and keeps the target (D107): the
                    // unit stays, as walking away is what ends a target (D100)
                    _doorArmed = Door.None;
                    _sheetOpen = false;
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    return true;
                }
                _sheetOpen = true;
                // A carrier the column has not shown yet resolves its landing page
                // on its first bind (D93); the opener is that first bind's reason,
                // so the carrier is taken as known - and as SHOWN, since Tick has
                // not yet seen menuNPC change and a Refresh before it has would
                // resolve no target at all.
                _target = focus;
                _focusShown = true;
                _doorArmed = door;
                _doorPending = true;
            }
            // the next Tick rebinds: it re-resolves the Focus first, which is the
            // order this depends on
            _forceRefresh = true;
            _nextRefresh = 0f;
            return true;
        }

        // For the harness. The foot tabs are POLLED, not IPointerClickHandler, so
        // /ui/click cannot reach them - the same reason RosterAct and SheetAct exist.
        internal static bool AspectAct(int group, int index)
        {
            if (group < 0 || group >= GroupCount || _footSets[group] == null
                || index < 0 || index >= _footSets[group].Length)
            {
                return false;
            }
            PickAspect(group, index);
            return true;
        }

        private static void PickAspect(int group, int index)
        {
            Aspect[] set = _footSets[group];
            if (set == null || index < 0 || index >= set.Length)
            {
                return;
            }
            if (group == GroupOwnSheet)
            {
                _ownAspect = set[index];
            }
            else if (group == GroupCharacter)
            {
                _focusAspect = set[index];
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
            Refresh();
        }

        // The row hangs from the frame's bottom-left, TitlePlateX in like the name
        // plate over its top, and is drawn BEHIND the frame so the feet tuck under
        // the field. Placed in the column's own anchor space, as the frame is.
        private static void PlaceFootTabs(int group, Plate frame, bool show)
        {
            // a dossier's pages are its folders' tabs; the kit row steps aside
            bool dossier = show && frame != null && IsDossier(group);
            PlaceDossierTabs(group, frame, dossier);
            if (dossier)
            {
                show = false;
            }
            KitFootTabs row = _footTabs[group];
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
                row = KitFootTabs.Create(_root.transform);
                int g = group;
                row.OnPick = index => PickAspect(g, index);
                _footTabs[group] = row;
            }
            Aspect[] set = _footSets[group] ?? OwnAspects;
            Sprite[] icons = _footIcons[group];
            if (icons == null || icons.Length != set.Length)
            {
                icons = new Sprite[set.Length];
                _footIcons[group] = icons;
            }
            int active = 0;
            Aspect current = AspectOf(group);
            for (int i = 0; i < set.Length; i++)
            {
                icons[i] = AspectIcon(set[i]);
                if (set[i] == current)
                {
                    active = i;
                }
            }
            // THE TABS STAND ON THE TOP RAIL while the sheet hangs from the unit
            // (tabs at the bottom jump up and down as a consequence of dynamic
            // height): the top edge is the fixed one, so
            // the tabs sit there, where the name plate was - the unit names them
            bool top = group == GroupOwnSheet || group == GroupCharacter;
            row.Top = top;
            string[] labels = new string[set.Length];
            for (int i = 0; i < set.Length; i++) { labels[i] = SheetTabLabel(set[i]); }
            row.Tooltips = labels;
            row.Set(icons, active);
            int c = frame.Column;
            float w = frame.Window.Rect.sizeDelta.x;
            float frameLeft = _colPivotX[c] > 0.5f ? _colX[c] - w : _colX[c];
            row.Rect.anchorMin = new Vector2(_colAnchorX[c], 0f);
            row.Rect.anchorMax = new Vector2(_colAnchorX[c], 0f);
            row.Rect.pivot = new Vector2(0f, top ? 0f : 1f);
            row.Rect.anchoredPosition = new Vector2(frameLeft + TitlePlateX, top ? _groupHi[group] : _groupLo[group]);
            if (!row.Rect.gameObject.activeSelf)
            {
                row.Rect.gameObject.SetActive(true);
            }
            row.Rect.SetAsFirstSibling();
        }

        private const float SheetRowH = 11f;
        private const float SheetHeadH = 12f;
        private const float SheetPadX = 4f;
        private const float SheetPadY = 3f;
        private const float SheetTextSize = 8f;      // the kit's label size, in canvas units
        private const float SheetSplit = 0.6f;       // where the label column ends
        // ...and where it ends for a PLAIN FACT, whose value is left-aligned.
        // 0.6 was measured for a right-aligned value, where the
        // number lands at the far edge whatever the split is; left-aligned, the
        // split IS the gutter, and 0.6 of a 380-unit sheet put "Idle" a hundred
        // units from "Job". A label column wide enough for "Following" is all the
        // pair needs.
        private const float SheetPairSplit = 0.42f;

        // The sheet's width is the Inventory aspect's (D49). Measured by Layout
        // once that aspect has been on screen; until then, what the bags would
        // measure - the same arithmetic Bind does for a full row.
        // `full` is the PROP case: a thing has no bag to size from, so the
        // slot count is zero and the clamp hands back PlateMinCells - a window a
        // third the width of every other one, with the acts running off its edge.
        // A window is the same width whatever is in it: the same width as the
        // other windows, which is the same as a bag window of ten cells.
        private static float SheetFallbackWidth(CommonStates who, int maxCols, bool own, bool full = false)
        {
            int slots = full ? maxCols : 0;
            if (who != null && who.bag != null && who.bag.slots != null)
            {
                slots = who.bag.slots.Length;
            }
            if (who != null && HasLoadout(who))
            {
                slots = Mathf.Max(slots, LoadoutOrder.Length);
            }
            if (own && _mn != null && _mn.inventory != null)
            {
                slots = Mathf.Max(slots, _mn.inventory.baseCount);
            }
            int cols = Mathf.Clamp(Mathf.Min(slots, maxCols), PlateMinCells, AbsoluteMaxCols);
            float gutter = GutterWidth;
            return gutter + cols * CellPitch - (CellPitch - CellBox);
        }

        private const float SheetChip = 10f;       // kit_chip_oak, as the checkbox draws it
        private const float SheetGlyph = 6f;
        private const float SheetKnob = 7f;
        private const float SheetTrackH = 2.4f;
        private const float SheetSliderValueW = 26f;

        private static SheetRow CreateSheetRow(Plate plate)
        {
            var row = new SheetRow { Host = plate };
            row.Go = Owned.New("Row");
            row.Go.transform.SetParent(plate.Window.Content, false);
            row.Label = Theme.MakeKitText(row.Go.transform, "Label", SheetTextSize, Theme.Ink,
                TextAlignmentOptions.MidlineLeft);
            row.Label.enableWordWrapping = false;
            // Overflow, not the ellipsis MakeText defaults to: a line taller than
            // its 11-unit row is dropped whole under Ellipsis, which drew the sheet
            // as one bold heading and nothing else
            row.Label.overflowMode = TextOverflowModes.Overflow;
            row.Value = Theme.MakeKitText(row.Go.transform, "Value", SheetTextSize, Theme.Ink,
                TextAlignmentOptions.MidlineRight);
            row.Value.enableWordWrapping = false;
            row.Value.overflowMode = TextOverflowModes.Overflow;
            row.Value.richText = true;     // a relation's two numbers, each in the ink of its class
            // THE CONTROLS, built once and shown by kind. The chip is the kit's
            // checkbox body; its glyph is whatever the row's kind says - the
            // spinner's + for a stat, vanilla's own eye or star for a toggle.
            row.Chip = Theme.KitImage(row.Go.transform, "Chip", "kit_chip_oak", 0, Color.white);
            row.Chip.raycastTarget = true;
            row.Glyph = Theme.KitImage(row.Chip.transform, "Glyph", "kit_plus", 0, Theme.KitCream);
            row.Chip.gameObject.SetActive(false);
            // ...and the slider, the kit's own: an ink hairline, a cream hairline
            // to the value, the wooden knob
            row.Track = Theme.KitImage(row.Go.transform, "Track", "kit_hairline", 4, Theme.KitInk);
            row.Track.raycastTarget = true;
            row.Fill = Theme.KitImage(row.Go.transform, "Fill", "kit_hairline", 4, Theme.KitCream);
            row.Track.gameObject.SetActive(false);
            row.Fill.gameObject.SetActive(false);
            var igo = Owned.New("Icon");
            igo.transform.SetParent(row.Go.transform, false);
            row.Icon = igo.AddComponent<Image>();
            row.Icon.raycastTarget = false;
            row.Icon.preserveAspect = true;
            igo.SetActive(false);
            return row;
        }

        // The rarer parts - a slider's knob, a relation's second band, an actions
        // row's planks - are made on first need, so a warmed row costs six
        // objects rather than sixteen and a sheet of value rows builds nothing
        // it will not show.
        private static void EnsureRowExtras(SheetRow row, SheetLine line)
        {
            int kind = line.Kind;
            if (kind == LineSlider && row.Knob == null)
            {
                row.Knob = Theme.KitImage(row.Go.transform, "Knob", "kit_knob", 0, Color.white);
                row.Knob.gameObject.SetActive(false);
            }
            if (kind == LinePair && row.Label2 == null)
            {
                row.Label2 = Theme.MakeKitText(row.Go.transform, "Label2", SheetTextSize, Theme.InkDim,
                    TextAlignmentOptions.MidlineLeft);
                row.Label2.enableWordWrapping = false;
                row.Label2.overflowMode = TextOverflowModes.Overflow;
                row.Value2 = Theme.MakeKitText(row.Go.transform, "Value2", SheetTextSize, Theme.Ink,
                    TextAlignmentOptions.MidlineLeft);
                row.Value2.enableWordWrapping = false;
                row.Value2.overflowMode = TextOverflowModes.Overflow;
            }
            if (kind == LineRelation && row.Love == null)
            {
                row.Love = LoveWidget.Create(row.Go.transform);
                row.Love.Show(false);
            }
            if (line.Gates && row.Gates == null)
            {
                row.Gates = GateHeader.Create(row.Go.transform);
                row.Gates.Show(false);
            }
            // a roster row wears the same strip the header does (D55), so
            // it asks for the same parts
            if ((kind == LineStrip || kind == LineRoster) && row.Ticks == null)
            {
                // the boxes and the stamina band are the kit hairline, as the
                // sheet's gauges are; the icons plain images that FILL from the
                // bottom - the HUD's own idiom for food and water, applied to all
                // flat quads, as the HUD's own bars and Teamfight Tactics' are:
                // a black rim around both bars, dark tracks, the fills, and a thin
                // dark tick per 100 of max drawn OVER the fill
                // A PLATE, not a background, like the plate for the title: the
                // name plate's own hand-cut oak, the strip
                // laid on it, its type in cream as the name is
                row.Well = Theme.KitImage(row.Go.transform, "Well", StripPlateSprite, 40, Color.white, stretch: true);
                row.Well.gameObject.SetActive(false);
                row.Well.transform.SetAsFirstSibling();   // behind everything on the row
                row.BarRim = PlainImage(row.Go.transform, "BarRim", StripRim);
                row.BarTrack = PlainImage(row.Go.transform, "BarTrack", StripBarTrack);
                row.BarFill = PlainImage(row.Go.transform, "BarFill", LifeTint);
                row.Stam = PlainImage(row.Go.transform, "Stam", StripBarTrack);
                row.StamFill = PlainImage(row.Go.transform, "StamFill", StunTint);
                row.Ticks = new Image[StripMaxBoxes];
                for (int i = 0; i < StripMaxBoxes; i++)
                {
                    row.Ticks[i] = PlainImage(row.Go.transform, "Tick" + i, StripTick);
                }
                row.IconRims = new Image[StripIcons];
                row.IconBacks = new Image[StripIcons];
                row.IconFills = new Image[StripIcons];
                for (int i = 0; i < StripIcons; i++)
                {
                    row.IconRims[i] = StripIcon(row.Go.transform, "IconRim" + i);
                    row.IconBacks[i] = StripIcon(row.Go.transform, "IconBack" + i);
                    row.IconFills[i] = StripIcon(row.Go.transform, "Icon" + i);
                    row.IconFills[i].type = Image.Type.Filled;
                    row.IconFills[i].fillMethod = Image.FillMethod.Vertical;
                    row.IconFills[i].fillOrigin = (int)Image.OriginVertical.Bottom;
                }
                // pink and pale ride over the heart's deep fill, in that order, so a
                // love of 60 reads pale to 50 and pink from there - the Relations bar
                // standing on end
                row.LoveMid = StripIcon(row.Go.transform, "LoveMid");
                row.LoveTop = StripIcon(row.Go.transform, "LoveTop");
                foreach (Image band in new[] { row.LoveMid, row.LoveTop })
                {
                    band.type = Image.Type.Filled;
                    band.fillMethod = Image.FillMethod.Vertical;
                    band.fillOrigin = (int)Image.OriginVertical.Bottom;
                }
                row.Seps = new Image[2];
                for (int i = 0; i < 2; i++)
                {
                    // a standing hairline: the kit's soft dot stretched tall, since the
                    // hairline sprite is drawn lying down and rotating it moved its pivot
                    row.Seps[i] = Theme.KitImage(row.Go.transform, "Sep" + i, "kit_dot", 0, Theme.KitInk, stretch: true);
                    row.Seps[i].gameObject.SetActive(false);
                }
                row.LevelSep = Theme.KitImage(row.Go.transform, "LevelSep", "kit_dot", 0, Theme.KitInk, stretch: true);
                row.LevelSep.gameObject.SetActive(false);
                row.LevelIcon = Theme.KitImage(row.Go.transform, "LevelUp", "kit_emoji_levelup", 0, Color.white);
                row.LevelIcon.gameObject.SetActive(false);
            }
            if (kind == LineRoster && row.Stripe == null)
            {
                EnsureRosterParts(row);
            }
            if ((kind == LineActions || kind == LineActs || kind == LinePlace
                 || kind == LinePose || kind == LineMore || kind == LineRosterBar
                 || kind == LineBoxes || kind == LineChecks || kind == LineMoodCause)
                && row.Planks == null)
            {
                // the actions row's planks: the kit's button, oak with cream type
                // - and "… N more", which IS a button, so it has to look like one
                row.Planks = new Image[SheetPlankCount];
                row.PlankTexts = new TextMeshProUGUI[SheetPlankCount];
                row.PlankIcons = new Image[SheetPlankCount];
                for (int i = 0; i < SheetPlankCount; i++)
                {
                    row.Planks[i] = Theme.KitImage(row.Go.transform, "Plank" + i, "kit_plank_oak", 18, Color.white);
                    row.Planks[i].raycastTarget = true;
                    var iconGo = Owned.New("PlankIcon");
                    iconGo.transform.SetParent(row.Planks[i].transform, false);
                    row.PlankIcons[i] = iconGo.AddComponent<Image>();
                    row.PlankIcons[i].raycastTarget = false;
                    row.PlankIcons[i].preserveAspect = true;
                    row.PlankIcons[i].enabled = false;
                    row.PlankTexts[i] = Theme.MakeKitText(row.Planks[i].transform, "Text", SheetTextSize,
                        Theme.KitCream, TextAlignmentOptions.Center, bold: true);
                    row.PlankTexts[i].enableWordWrapping = false;
                    row.PlankTexts[i].overflowMode = TextOverflowModes.Overflow;
                    row.Planks[i].gameObject.SetActive(false);
                }
            }
        }

        // three for an Orders row; five since the roster's control bar shares
        // them - the three sorts and the two camp-wide verbs (D53)
        private const int SheetPlankCount = 6;   // the roster bar: four sorts and two verbs
        private const float SheetStripH = 27f;
        private const int StripMaxBoxes = 10;   // the tick images a row starts with; PlaceTicks grows them
        private static readonly Color StripRim = new Color(0.06f, 0.04f, 0.03f, 1f);
        private const string StripPlateSprite = "kit_plate_handcut";   // the name plate's oak; _pine is the lit one
        private static readonly Color StripBarTrack = new Color(0.22f, 0.17f, 0.13f, 1f);
        private static readonly Color StripTick = new Color(0.06f, 0.04f, 0.03f, 0.75f);
        // each icon's size against the slot: the kit's dot sits inside padding, the anger mark is small
        // evened by DRAWN height, not by box, so the libido and relationship icons read the
        // same size: each kit
        // sprite fills its 48px square differently - the flame's ink is 0.979 of its box
        // tall, the heart's 0.875, the face's 0.917 - so equal scales draw unequal glyphs.
        // The three here all draw ~8.7 units tall at the plate's pitch.
        private static readonly float[] StripIconScale = { 1.12f, 1.05f, 0.86f, 0.90f, 0.80f, 0.84f };   // pear · drop · moral · heart · flame · baby
        private static readonly bool[] StripIconRim = { false, false, true, true, true, true };      // the HUD's pear and drop carry their own contour
        // food · water | moral · love · libido | baby: the heart is THEIR love for you, filled and
        // banded by the Relations tab's own classes, cracked below zero; the flame is libido,
        // cut from a reference image (two tones baked). The heart leads the pair
        private const int StripIcons = 6;
        private static readonly Color StripTrack = new Color(0.62f, 0.55f, 0.46f, 1f);   // an icon's empty part, on parchment
        private const float SheetPlankH = 13f;
        // the placement acts are the same button, in a smaller variant
        private const float SheetPlaceH = 10f;
        private const float SheetPlankGap = 4f;
        private const float SheetDropdownRowH = KitDropdown.Height + 2f;

        private static void PaintSheetRow(SheetRow row, SheetLine line)
        {
            // outside the kind guard: a row can go from list size to card size
            // without changing kind, and the input side reads this every frame
            row.DrewCard = line.Card;
            // a pooled row can move between a dossier and a plain sheet; the plain
            // styling is re-applied from scratch when it leaves the form
            bool form = FormRow(row);
            if (row.DrewForm != form)
            {
                row.DrewForm = form;
                row.DrewKind = -1;
                if (!form)
                {
                    ClearForm(row);
                }
            }
            if (row.DrewKind != line.Kind)
            {
                row.DrewKind = line.Kind;
                bool head = line.Kind == LineHead;
                row.Label.fontStyle = head ? FontStyles.Bold : FontStyles.Normal;
                row.Label.characterSpacing = 0f;   // a form caption's spacing, off a pooled row
                row.Label.color = head ? Theme.Ink : Theme.InkDim;
                row.Value.fontStyle = head ? FontStyles.Bold : FontStyles.Normal;
                // A PLAIN FACT READS AS A PAIR, SO ITS VALUE IS LEFT-ALIGNED.
                // Right-aligning it drove "Job" and "Idle" to
                // opposite ends of a 380-unit sheet with a hundred units of
                // nothing between them, and four of those rows read as two
                // unrelated columns rather than four facts. The roster row solves
                // the same problem the same way: its where/cause sits in a column
                // that starts at a fixed x.
                //
                // Right alignment survives where it earns its keep - anything with
                // a TRACK beside it, and the stat rows, where digits line up in a
                // column and the eye compares them down the page.
                bool pair = line.Kind == LineValue || line.Kind == LinePair;
                row.Value.alignment = line.Kind == LineStrip || pair
                    ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                if (row.Label2 != null)
                {
                    bool two = line.Kind == LinePair;
                    if (row.Label2.gameObject.activeSelf != two)
                    {
                        row.Label2.gameObject.SetActive(two);
                        row.Value2.gameObject.SetActive(two);
                    }
                    if (two)
                    {
                        if (row.Label2.text != (line.Label2 ?? string.Empty))
                        {
                            row.Label2.text = line.Label2 ?? string.Empty;
                        }
                        if (row.Value2.text != (line.Value2 ?? string.Empty))
                        {
                            row.Value2.text = line.Value2 ?? string.Empty;
                        }
                        row.Label2.color = Theme.InkDim;
                        row.Value2.color = line.Value2Tinted ? line.Value2Tint : Theme.Ink;
                    }
                }
                if (line.Kind != LineStrip && line.Kind != LineRoster && row.Ticks != null)
                {
                    ShowStrip(row, false);
                }
                if (line.Kind != LineRoster)
                {
                    ShowRosterParts(row, false);
                }
                row.Label.fontSize = line.Kind == LineStrip ? SheetTextSize * 0.8f : SheetTextSize;
                bool chip = line.Kind == LineStat || line.Kind == LineToggle || line.Kind == LineSkill
                    || line.Kind == LineOption;
                bool track = line.Kind == LineSlider || line.Kind == LineBar || line.Kind == LineGauge;
                bool knob = line.Kind == LineSlider;
                bool relation = line.Kind == LineRelation;
                if (row.Chip.gameObject.activeSelf != chip) { row.Chip.gameObject.SetActive(chip); }
                if (row.Track.gameObject.activeSelf != track)
                {
                    row.Track.gameObject.SetActive(track);
                    row.Fill.gameObject.SetActive(track);
                }
                if (row.Love != null) { row.Love.Show(relation); }
                if (row.Knob != null && row.Knob.gameObject.activeSelf != knob) { row.Knob.gameObject.SetActive(knob); }
                if (line.Kind != LineDropdown && row.Drop != null)
                {
                    row.Drop.Destroy();   // the row changed job
                    row.Drop = null;
                    row.DropKey = null;
                }
                // a row that is not the actions row shows no planks; the actions
                // row shows as many as it has labels, placed by BindSheet
                if (line.Kind != LineActions && line.Kind != LineActs && line.Kind != LinePlace
                    && line.Kind != LinePose && line.Kind != LineMore
                    && line.Kind != LineRosterBar && line.Kind != LineBoxes && line.Kind != LineChecks
                    && line.Kind != LineMoodCause && row.Planks != null)
                {
                    for (int i = 0; i < row.Planks.Length; i++)
                    {
                        if (row.Planks[i].gameObject.activeSelf) { row.Planks[i].gameObject.SetActive(false); }
                    }
                }
                // the text follows the kind: forced, since a spacer's null label
                // would otherwise equal a null memo and keep the last row's words
                row.DrewLabel = "\0";
                row.DrewValue = "\0";
            }
            // the printed form draws its own rows whole (InventoryWindow.Dossier.cs)
            if (form && FormOwns(line.Kind))
            {
                if (row.Gates != null) { row.Gates.Show(false); }
                PaintForm(row, line);
                return;
            }
            // the gate marks belong to one heading, not to a kind
            if (row.Gates != null) { row.Gates.Show(line.Gates); }
            if (!string.Equals(row.DrewLabel, line.Label))
            {
                row.DrewLabel = line.Label;
                row.Label.text = line.Label ?? string.Empty;
            }
            // a name in its roster colour, else the sheet's ink
            Color ink = form ? Theme.DossierInk : Theme.Ink;
            Color softInk = form ? Theme.DossierLabel : Theme.InkDim;
            Color labelColor = line.Kind == LineHead ? ink : line.LabelTinted ? line.LabelTint : softInk;
            if (row.Label.color != labelColor) { row.Label.color = labelColor; }
            if (!string.Equals(row.DrewValue, line.Value))
            {
                row.DrewValue = line.Value;
                row.Value.text = line.Value ?? string.Empty;
            }
            // the label names the question, so where the ANSWER is the news it is the
            // answer that carries the colour - "Unhappy: bad company" in 55f's red
            // Set BOTH ways: a pooled row that last drew a red cause must not arrive
            // wearing it - the same rule TakePlate follows for every other decoration.
            Color valueCol = line.ValueTinted ? line.ValueTint : ink;
            if (row.Value.color != valueCol)
            {
                row.Value.color = valueCol;
            }
            row.Arg = line.Arg;
            row.On = line.On;
            row.Enabled = line.Enabled;
            row.Frac = line.Frac;
            row.Tip = line.Tip;
            row.IconTips = line.IconTips;
            row.BarTip = line.BarTip;
            // A GLYPH BEFORE THE LABEL, any kind: a skill's own, a heading's tab
            // glyph, the heart beside the most loved. A mask sprite (the HUD's
            // food and water) takes the line's tint; a drawn one stays as drawn.
            bool hasIcon = line.Icon != null;
            if (row.Icon.gameObject.activeSelf != hasIcon) { row.Icon.gameObject.SetActive(hasIcon); }
            if (hasIcon)
            {
                if (row.Icon.sprite != line.Icon) { row.Icon.sprite = line.Icon; }
                row.Icon.color = line.TintIcon ? line.Tint : Color.white;
            }
            switch (line.Kind)
            {
                case LineRelation:
                {
                    row.Love.Paint(line.Love, line.Back, line.BackKnown);
                    break;
                }
                case LineStrip:
                {
                    PaintStrip(row, line);
                    break;
                }
                case LineRoster:
                {
                    PaintStrip(row, line);
                    PaintRosterRow(row, line);
                    // a roster row's level is a number at the row's right end; the
                    // card's kind is a word running on after it, so the alignment
                    // is the line's, not the kind's, and DrewKind cannot gate it
                    row.Value.alignment = line.Card
                        ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                    Color kindCol = line.Card ? Theme.InkDim : Theme.Ink;
                    if (row.Value.color != kindCol) { row.Value.color = kindCol; }
                    // A NAME OR A KIND CAN BE LONGER THAN ITS BOX. TMP overflows its rect
                    // by default, so "Old Man Native" ran straight over "at home" beside
                    // it. Both texts here are one line of a row that has other things on
                    // it, so the honest end is an ellipsis, not a second line.
                    // ...and only on the CARD. Ellipsis makes TMP judge the rect's HEIGHT
                    // as well as its width, and a list row's name box is short enough that
                    // it judged the whole name unfittable and drew nothing - a roster with
                    // no names on it. The card's boxes are tall enough to be safe.
                    TextOverflowModes flow = line.Card
                        ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
                    row.Value.overflowMode = flow;
                    row.Label.overflowMode = flow;
                    ShedRosterRow(row, line);   // the tool shed's corkboard
                    break;
                }
                case LineRosterBar:
                {
                    PaintRosterBar(row, line);
                    break;
                }
                case LineGauge:
                {
                    var fr = row.Fill.rectTransform;
                    fr.sizeDelta = new Vector2(Mathf.Clamp01(line.Frac) * row.TrackW, fr.sizeDelta.y);
                    row.Fill.color = line.Tinted ? line.Tint : Theme.KitCream;
                    break;
                }
                case LineStat:
                case LineSkill:
                {
                    // affordable at full strength, otherwise dimmed like a chip
                    // the quick-stack cannot press (D44's family rule)
                    bool down = ReferenceEquals(_sheetPressed, row) && ReferenceEquals(_sheetHot, row)
                        && Input.GetMouseButton(0);
                    float k = !line.Enabled ? 0.45f : down ? Theme.PressTint.r : 1f;
                    row.Chip.color = new Color(k, k, k, 1f);
                    row.Glyph.sprite = Theme.Kit("kit_plus", 0);
                    row.Glyph.color = Theme.KitCream;
                    break;
                }
                case LineBar:
                {
                    var fr = row.Fill.rectTransform;
                    fr.sizeDelta = new Vector2(Mathf.Clamp01(line.Frac) * row.TrackW, fr.sizeDelta.y);
                    row.Fill.color = line.Tinted ? line.Tint : Theme.KitCream;
                    break;
                }
                case LineOption:
                {
                    // the kit's checkbox: the chip, a check when allowed
                    bool down = ReferenceEquals(_sheetPressed, row) && ReferenceEquals(_sheetHot, row)
                        && Input.GetMouseButton(0);
                    float k = down ? Theme.PressTint.r : 1f;
                    row.Chip.color = new Color(k, k, k, 1f);
                    row.Glyph.sprite = Theme.Kit("kit_check", 0);
                    row.Glyph.color = Theme.KitCream;
                    row.Glyph.enabled = line.On;
                    break;
                }
                case LineDropdown:
                {
                    if (row.Drop != null && row.Drop.Index != line.Index && !row.Drop.IsOpen)
                    {
                        row.Drop.SetIndex(line.Index);   // changed elsewhere: show it
                    }
                    break;
                }
                case LineActs:
                case LinePlace:
                case LinePose:
                case LineActions:
                case LineMore:
                {
                    for (int i = 0; row.Planks != null && i < row.Planks.Length; i++)
                    {
                        bool shown = line.Options != null && i < line.Options.Length;
                        if (row.Planks[i].gameObject.activeSelf != shown)
                        {
                            row.Planks[i].gameObject.SetActive(shown);
                        }
                        if (!shown)
                        {
                            continue;
                        }
                        bool hot = ReferenceEquals(_sheetHot, row) && _sheetHotSub == i;
                        bool down = hot && ReferenceEquals(_sheetPressed, row) && _sheetPressedSub == i
                            && Input.GetMouseButton(0);
                        Theme.KitSkin(row.Planks[i], hot ? "kit_plank_pine" : "kit_plank_oak", 18);
                        float k = down ? 0.8f : 1f;
                        // D73: REACH decides which acts are live, not how you picked
                        // the target - so the same person reads the same either way.
                        // An act out of reach is drawn dim and does not answer.
                        float a = line.Kind == LineActs && !line.Enabled ? 0.45f : 1f;
                        row.Planks[i].color = new Color(k, k, k, a);
                        row.PlankTexts[i].alpha = a;
                        // A ROW THAT CARRIES AN INDEX IS A RADIO SET, and the chosen
                        // plank wears pine as an active tab does. The pose row and
                        // the orientation pair are both that; an ordinary actions
                        // row leaves Index at -1 and is unaffected.
                        // ...and it must OPT IN, because Index defaults to 0: a
                        // plain actions row would otherwise wear pine on its first
                        // plank and put Return in the pressed state for ever.
                        bool radio = line.Kind == LinePose
                            || (line.Kind == LineActions && line.Arg == ActionsRot);
                        if (radio && line.Index == i)
                        {
                            Theme.KitSkin(row.Planks[i], "kit_plank_pine", 18);
                        }
                        // A PLACEMENT act is drawn as VANILLA'S OWN ICON, and its
                        // "label" is an object name that must never reach the screen.
                        Sprite art = (line.Kind == LinePlace || line.Kind == LineActs
                                || line.Kind == LinePose)
                            && line.Icons != null && i < line.Icons.Length ? line.Icons[i] : null;
                        if (row.PlankIcons != null && i < row.PlankIcons.Length)
                        {
                            Image ic = row.PlankIcons[i];
                            if (ic.sprite != art) { ic.sprite = art; }
                            if (ic.enabled != (art != null)) { ic.enabled = art != null; }
                        }
                        string word = art != null ? string.Empty : line.Options[i];
                        if (row.PlankTexts[i].text != word)
                        {
                            row.PlankTexts[i].text = word;
                        }
                    }
                    break;
                }
                case LineToggle:
                {
                    bool down = ReferenceEquals(_sheetPressed, row) && ReferenceEquals(_sheetHot, row)
                        && Input.GetMouseButton(0);
                    float k = down ? Theme.PressTint.r : 1f;
                    row.Chip.color = new Color(k, k, k, 1f);
                    Sprite mark = line.Arg == 91 ? (line.On ? Theme.Kit("kit_check", 0) : null) : ToggleSprite(line.Arg, line.On);
                    row.Glyph.sprite = mark;
                    row.Glyph.enabled = mark != null;
                    // vanilla's own rule for the favourite: white when set, a
                    // dark ghost when not; the eye swaps sprites instead
                    row.Glyph.color = line.Arg == ToggleFavourite && !line.On
                        ? new Color(0.2f, 0.2f, 0.2f, 1f) : Color.white;
                    break;
                }
                case LineSlider:
                {
                    float x = row.TrackX + Mathf.Clamp01(line.Frac) * row.TrackW;
                    var fr = row.Fill.rectTransform;
                    fr.sizeDelta = new Vector2(Mathf.Max(0f, x - row.TrackX), fr.sizeDelta.y);
                    row.Fill.color = Theme.KitCream;
                    var kr = row.Knob.rectTransform;
                    kr.anchoredPosition = new Vector2(x - SheetKnob * 0.5f, kr.anchoredPosition.y);
                    break;
                }
            }
            if (form)
            {
                PaintFormHybrid(row, line);
            }
        }

        // The eye is vanilla's own pair (ui_name_visible / ui_name_invisible);
        // the star is the one the vanilla sheet wears
        private static Sprite ToggleSprite(int which, bool on)
        {
            if (_mn == null || _mn.uiMN == null)
            {
                return null;
            }
            if (which == ToggleNameShown)
            {
                Sprite[] eyes = _mn.uiMN.visibleSprite;
                return eyes != null && eyes.Length > 1 ? eyes[on ? 1 : 0] : null;
            }
            return _mn.uiMN.favButton != null ? _mn.uiMN.favButton.sprite : Theme.Star;
        }

        // A sheet is a pooled plate like any other member: it takes the column's
        // width, stacks bottom-up inside the group's frame, and is cleared by
        // TakePlate on its way to the next job. Only what is ON it differs.
        private static void BindSheet(int group, int column, CommonStates who, Aspect aspect, int maxCols, bool header = false, ItemInfo prop = null)
        {
            Plate plate = TakePlate();
            plate.Column = column;
            plate.Owner = who;
            if (who != null)
            {
                _dossierWho[group] = who;
            }
            plate.Prop = prop;
            plate.Container = null;
            plate.IsLoadout = false;
            plate.IsSharedBag = false;
            plate.IsSheet = true;
            plate.IsHeader = header;
            // the card is re-derived by CardLine, not by WriteHeader - flagged on the
            // PLATE, so a bound card is never repainted by the other writer
            plate.IsCard = header;
            bool grouped = group != GroupNone;
            plate.Group = grouped ? group : GroupNone;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            _lastGroupBound = plate.Group;
            // A SHEET IS A GROUP MEMBER TOO, and it has to say so for the member
            // AFTER it. LoadoutEdge - which turns the seam into a row of dots -
            // asks whether this member's loadout-ness differs from the previous
            // one, and _prevMemberLoadout is only written on the cells path. A
            // sheet does not go down that path, so the value survived from
            // whatever bound last ANYWHERE: open your own window, whose loadout
            // plate leaves it true, and the grid under a prop's sheet compared
            // itself against that and drew dots, turning the flat line separator
            // in the target window into a dotted one. A sheet is not a loadout, and now says so.
            _prevMemberLoadout = false;
            plate.Shown = 0;
            plate.Cols = 0;
            plate.LaidOutCols = -1;
            HideCells(plate);
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                if (plate.TypeIcons[i].gameObject.activeSelf)
                {
                    plate.TypeIcons[i].gameObject.SetActive(false);
                }
            }
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            plate.Back.enabled = false;
            plate.Edge.enabled = false;

            float width = _inventoryW[group] > 0f
                ? _inventoryW[group]
                : SheetFallbackWidth(who, maxCols, column == OwnColumn, full: prop != null);
            if (plate.Rows == null)
            {
                plate.Rows = new List<SheetRow>();
            }
            SheetLines.Clear();
            if (prop != null)
            {
                // a thing, not a person: its own writer, the same layout below
                WritePropSheet(SheetLines, prop);
            }
            else if (header)
            {
                // D66: the head is the CARD - the roster row at frame width - not
                // the icons-only strip, which stays on the plate when the card is off
                SheetLines.Add(CardLine(who));
            }
            else
            {
                WriteSheet(SheetLines, who, aspect, column == OwnColumn, group);
            }
            bool scrollable = who != null && !header;
            if (scrollable) { width -= SheetScrollGutter; }
            float y = SheetPadY;
            for (int i = 0; i < SheetLines.Count; i++)
            {
                SheetLine line = SheetLines[i];
                while (plate.Rows.Count <= i)
                {
                    plate.Rows.Add(CreateSheetRow(plate));
                }
                SheetRow row = plate.Rows[i];
                EnsureRowExtras(row, line);
                // a printed form's own rows lay themselves out whole
                bool formRow = plate.Group != GroupNone && IsDossier(plate.Group) && !header && prop == null;
                if (formRow && FormOwns(line.Kind))
                {
                    float fh = FormRowHeight(line);
                    Theme.Place(row.Go.transform, 0f, y, width, fh);
                    row.FormW = width;
                    row.FormH = fh;
                    PaintSheetRow(row, line);
                    if (!row.Go.activeSelf)
                    {
                        row.Go.SetActive(true);
                    }
                    y += fh;
                    continue;
                }
                float h = line.Kind == LineHead ? (line.Gates ? SheetGatesHeadH : SheetHeadH)
                    : line.Kind == LineSpace ? SheetRowH * 0.5f
                    : formRow && line.Kind == LineDropdown ? FormDropRowH
                    : formRow && line.Kind == LineSlider ? FormScaleRowH
                    : line.Kind == LineDropdown ? SheetDropdownRowH
                    : line.Kind == LinePlace ? SheetPlaceH + 3f
                    : line.Kind == LinePose ? SheetPlankH + 4f
                    : line.Kind == LineActions || line.Kind == LineActs || line.Kind == LineMore ? SheetPlankH + 4f
                    : line.Kind == LineRelation ? LoveWidget.RowH
                    : line.Kind == LineStrip ? SheetStripH
                    : line.Kind == LineRoster ? (line.Card ? CardRowH : RosterRowH)
                    : SheetRowH;
                Theme.Place(row.Go.transform, 0f, y, width, h);
                float split = width * (line.Kind == LineValue ? SheetPairSplit : SheetSplit);
                float right = width - SheetPadX;   // where the value column ends
                float labelX = SheetPadX;
                if (line.Icon != null)
                {
                    // the glyph leads the label, whatever the row is
                    Theme.Place(row.Icon, SheetPadX, (h - SheetChip) * 0.5f, SheetChip, SheetChip);
                    labelX += SheetChip + 3f;
                }
                if (line.Kind == LineRelation)
                {
                    // name, then the widget on the shared scale, then the two numbers
                    float trackX = RelTrackX(width);
                    row.Love.Place(trackX, 0f, RelTrackW(width, right), h);
                    split = trackX;
                }
                else if (line.Kind == LineHead && line.Gates)
                {
                    // the gates over the relation column, once, on its heading
                    float trackX = RelTrackX(width);
                    row.Gates.Place(trackX + 8f, RelTrackW(width, right) - 8f, 0f, h);
                    split = trackX;
                }
                else if (line.Kind == LineMore && row.Planks != null)
                {
                    // one plank at the row's right end, as wide as its label needs
                    Theme.Place(row.Planks[0], right - SheetMoreW, (h - SheetPlankH) * 0.5f, SheetMoreW, SheetPlankH);
                    Theme.Place(row.PlankTexts[0], 0f, 0f, SheetMoreW, SheetPlankH);
                    row.Planks[0].enabled = true;   // ...the same rule (see LineActions)
                }
                else if (line.Kind == LineDropdown)
                {
                    // the dropdown takes the value column; rebuilt only when its
                    // choices or its width change, since the kit's is built once
                    // - on the printed form it is a field under its caption instead
                    float dropX = formRow ? SheetPadX : width * 0.4f;
                    float dropY = formRow ? FormCaptionH : (h - KitDropdown.Height) * 0.5f;
                    float dropW = right - dropX;
                    string key = line.Arg + "|" + dropW.ToString("0") + "|" + (formRow ? "p|" : string.Empty)
                        + (line.Options != null ? string.Join("", line.Options) : string.Empty);
                    if (row.Drop != null && row.DropKey != key)
                    {
                        row.Drop.Destroy();
                        row.Drop = null;
                    }
                    if (row.Drop == null && line.Options != null && line.Options.Length > 0)
                    {
                        Plate owner = plate;
                        int which = line.Arg;
                        // the open list lives on the root, above every plate and
                        // row it may fall over
                        row.Drop = KitDropdown.Create(row.Go.transform, "Drop", dropX,
                            dropY, dropW, line.Options, line.Index,
                            index => ChooseOrder(owner, which, index), line.Icons, _root.transform, formRow);
                        row.DropKey = key;
                    }
                    split = dropX;
                    if (formRow)
                    {
                        // the field's name is its caption, printed over it
                        Theme.Place(row.Label, SheetPadX, 0f, right - SheetPadX, FormCaptionH);
                        split = -1f;
                    }
                }
                else if (line.Kind == LinePair)
                {
                    // two pairs, each in half the row, each with the plain row's
                    // own gutter inside its half - so the four labels sit on two
                    // left edges and the four values on two more, and the block
                    // reads as a grid rather than as four ragged lines
                    float half = Mathf.Round((right - labelX) * 0.5f);
                    float gutter = Mathf.Round(half * (SheetPairSplit / 0.5f) * 0.5f);
                    Theme.Place(row.Label, labelX, 0f, gutter, h);
                    Theme.Place(row.Value, labelX + gutter, 0f, Mathf.Max(10f, half - gutter - 4f), h);
                    if (row.Label2 != null)
                    {
                        float x2 = labelX + half;
                        Theme.Place(row.Label2, x2, 0f, gutter, h);
                        Theme.Place(row.Value2, x2 + gutter, 0f, Mathf.Max(10f, half - gutter - 4f), h);
                    }
                    split = -1f;   // placed here; the shared tail must not move them
                }
                else if (line.Kind == LineStrip)
                {
                    LayoutStrip(row, line, width, h, sheet: true);
                }
                else if (line.Kind == LineRoster)
                {
                    // the same writer the list uses, at the column's width
                    LayoutRosterRow(row, line, width, h);
                }
                else if ((line.Kind == LineActions || line.Kind == LineActs
                          || line.Kind == LinePlace || line.Kind == LinePose)
                         && line.Options != null)
                {
                    // the planks share the row, evenly, from the left - EXCEPT the
                    // placement acts, which are square icon buttons floated to the
                    // right, at the lower right side inside the window. They are the same button
                    // in a smaller variant, and being a run rather than a spread is
                    // part of saying they are a different kind of act.
                    bool place = line.Kind == LinePlace;
                    float plankH = place ? SheetPlaceH : SheetPlankH;
                    int n = Mathf.Min(line.Options.Length, row.Planks.Length);
                    float plankW = place ? plankH
                        : (n > 0 ? (right - SheetPadX - (n - 1) * SheetPlankGap) / n : 0f);
                    float runX = line.Kind == LinePlace
                        ? right - n * plankW - (n - 1) * SheetPlankGap
                        : SheetPadX;
                    for (int k = 0; k < n; k++)
                    {
                        Theme.Place(row.Planks[k], runX + k * (plankW + SheetPlankGap),
                            (h - plankH) * 0.5f, plankW, plankH);
                        Theme.Place(row.PlankTexts[k], 0f, 0f, plankW, plankH);
                        if (row.PlankIcons != null && k < row.PlankIcons.Length)
                        {
                            float ih = plankH - 2f;
                            Theme.Place(row.PlankIcons[k], (plankW - ih) * 0.5f, 1f, ih, ih);
                        }
                        // the roster's sort words leave their tile UNDRAWN and this
                        // pool is shared, so a plank opts its wood back in
                        row.Planks[k].enabled = true;
                    }
                }
                if (line.Kind == LineStat || line.Kind == LineToggle || line.Kind == LineSkill
                    || line.Kind == LineOption)
                {
                    // the chip takes the row's right end; the value stops short of it
                    Theme.Place(row.Chip, right - SheetChip, (h - SheetChip) * 0.5f, SheetChip, SheetChip);
                    Theme.Place(row.Glyph, (SheetChip - SheetGlyph) * 0.5f, (SheetChip - SheetGlyph) * 0.5f,
                        SheetGlyph, SheetGlyph);
                    right -= SheetChip + 3f;
                }
                else if (formRow && line.Kind == LineSlider)
                {
                    // the printed scale: caption, a ruled line with ticks, the knob,
                    // the reading in a box (Dossier.cs draws the ticks and the box)
                    LayoutFormScale(row, width, h);
                    split = -1f;
                }
                else if (line.Kind == LineSlider || line.Kind == LineBar || line.Kind == LineGauge)
                {
                    // label, then the track across the middle, then the value; a
                    // gauge's label is one word, so its band starts sooner
                    float trackX = line.Kind == LineGauge ? width * 0.3f : split - width * 0.2f;
                    // "4941 / 11200" at label size runs ~50 units; the slider's "3.00" far less
                    float valueW = line.Kind == LineBar ? 58f : line.Kind == LineGauge ? 48f : SheetSliderValueW;
                    float trackW = right - valueW - 4f - trackX;
                    float trackH = line.Kind == LineSlider ? SheetTrackH : SheetTrackH * 1.6f;
                    row.TrackX = trackX;
                    row.TrackW = Mathf.Max(10f, trackW);
                    Theme.Place(row.Track, trackX, (h - trackH) * 0.5f, row.TrackW, trackH);
                    Theme.Place(row.Fill, trackX, (h - trackH) * 0.5f, 0f, trackH);
                    if (row.Knob != null) { Theme.Place(row.Knob, trackX, (h - SheetKnob) * 0.5f, SheetKnob, SheetKnob); }
                    split = trackX;
                }
                PaintSheetRow(row, line);
                if (line.Kind != LineStrip && line.Kind != LineRoster && split >= 0f)   // these place their own
                {
                    Theme.Place(row.Label, labelX, 0f, split - labelX, h);
                    Theme.Place(row.Value, split, 0f, Mathf.Max(0f, right - split), h);
                }
                if (!row.Go.activeSelf)
                {
                    row.Go.SetActive(true);
                }
                y += h;
            }
            for (int i = SheetLines.Count; i < plate.Rows.Count; i++)
            {
                if (plate.Rows[i].Go.activeSelf)
                {
                    plate.Rows[i].Go.SetActive(false);
                }
            }
            plate.RowsShown = SheetLines.Count;
            if (scrollable) { width += SheetScrollGutter; }
            plate.Height = y + SheetPadY;
            plate.Window.Rect.sizeDelta = new Vector2(width, plate.Height);
            Theme.Place(plate.Window.Content, 0f, 0f, width, plate.Height);
            FitSheetViewport(plate, group, aspect);
            plate.LaidOutW = width;
            plate.LaidOutH = plate.Height;
            _lastColumnWidth[column] = Mathf.Max(_lastColumnWidth[column], width);
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
            SheetPlates.Add(plate);
        }

        // Repaint the type on every live sheet. A sheet whose SHAPE changed - a
        // class that appeared, a row that came or went - asks for a rebind
        // instead, since only Bind lays rows out.
        private static void RepaintSheets()
        {
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                Plate plate = SheetPlates[p];
                if (!plate.IsSheet || plate.Rows == null
                    || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                // every other sheet is ABOUT someone; the roster's control row is
                // the one that is not, and it still has to answer the pointer
                // ...and a PROP sheet is about a thing rather than a someone, so
                // it has no Owner and still has to answer the pointer
                if (plate.Owner == null && !plate.IsRoster && plate.Prop == null)
                {
                    continue;
                }
                SheetLines.Clear();
                if (plate.Prop != null)
                {
                    // wear drains and vanilla relights the acts, so the prop's
                    // lines are re-derived rather than remembered
                    WritePropSheet(SheetLines, plate.Prop);
                }
                else if (plate.IsRoster)
                {
                    // a roster row's facts are all live - where they stand, what
                    // they are doing, how they are - so the line is re-derived
                    // rather than remembered
                    SheetLines.Add(plate.Owner != null ? RosterLine(plate.Owner) : RosterBarLine());
                }
                else if (plate.IsCard)
                {
                    // the card's facts are as live as a roster row's, and for the same
                    // reason: it IS a roster row (D66)
                    SheetLines.Add(CardLine(plate.Owner));
                }
                else if (plate.IsHeader)
                {
                    WriteHeader(SheetLines, plate.Owner);
                }
                else
                {
                    WriteSheet(SheetLines, plate.Owner, AspectOf(plate.Group), plate.Column == OwnColumn, plate.Group);
                }
                if (SheetLines.Count != plate.RowsShown)
                {
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    continue;
                }
                for (int i = 0; i < SheetLines.Count && i < plate.Rows.Count; i++)
                {
                    PaintSheetRow(plate.Rows[i], SheetLines[i]);
                }
            }
        }

        private static SheetLine Row(string label, string value, Sprite icon = null)
        {
            return new SheetLine { Label = label, Value = value, Kind = LineValue, Icon = icon };
        }

        private static SheetLine Head(string label, string value, Sprite icon = null)
        {
            return new SheetLine { Label = label, Value = value, Kind = LineHead, Icon = icon };
        }

        // `from` is the carrier as the tip names them, `name` the other: the
        // numbers are not on the row, they are the tip over its bar
        private static SheetLine Relation(string from, string name, Color? nameColor, float love, float back,
            bool backKnown, Sprite icon = null)
        {
            return new SheetLine
            {
                Label = name, Kind = LineRelation, Icon = icon, Love = love, Back = back, BackKnown = backKnown,
                LabelTint = nameColor ?? Theme.InkDim, LabelTinted = nameColor.HasValue,
                Value = string.Empty,
                Tip = LoveWidget.TipText(from, name, love, back, backKnown),
                Enabled = true,
            };
        }

        // where the relation column sits in a sheet row: after the name, to the
        // row's end - the same for the rows and for the gates over them
        private static float RelTrackX(float width) => width * 0.36f;
        private static float RelTrackW(float width, float right) => right - 2f - RelTrackX(width);
        private const float SheetGatesHeadH = 15f;   // a heading with the gate marks and their numbers
        private const float SheetMoreW = 52f;        // "… 16 more" on its plank

        private static SheetLine Gauge(string label, float value, float max, Color tint, Sprite icon = null,
            bool tintIcon = false, string text = null)
        {
            float frac = max > 0f ? value / max : 0f;
            // the HUD's food and water are white masks the HUD tints; a drawn
            // sprite (the heart) keeps its own colours
            return new SheetLine
            {
                Label = label, Kind = LineGauge, Icon = icon, Tint = tint, Tinted = true,
                TintIcon = tintIcon, Frac = Mathf.Clamp01(frac),
                Value = text ?? (value.ToString("0") + " / " + max.ToString("0")),
                Enabled = true,
            };
        }

        private static void WriteSheet(List<SheetLine> lines, CommonStates who, Aspect aspect, bool own, int group)
        {
            // a character sheet's pages are the printed form
            if (IsDossier(group) && WriteDossier(lines, who, aspect, own))
            {
                return;
            }
            if ((group == GroupOwnSheet || group == GroupCharacter) && aspect != Aspect.Orders)
                lines.Add(Head(SheetTabLabel(aspect), string.Empty, null));
            switch (aspect)
            {
                case Aspect.Overview:
                    // Overview is the interaction sheet (G7): how is this thing,
                    // and what can I do with it - the two questions you ask at the
                    // same moment. A hostile answers only the second (D89).
                    if (!Hostile(who))
                    {
                        // THE GAUGE OPENS THE PAGE (D88): the run that used to ride
                        // the name plate, at page size, so the glance and the
                        // reading are one surface instead of two saying the same
                        // thing. The plate keeps its name (Titles).
                        // THE OPENING LINE IS THE CARD, NOT THE BARE GAUGE: the
                        // overview's gauge part looks the same as in the roster, and
                        // the job, the where and the following are not plain text.
                        //
                        // Two readings of one person, in two vocabularies, one under
                        // the other: the gauge said condition in bars and glyphs, and
                        // then a text pair said job, where and following in words. The
                        // words are the same four facts a ROSTER ROW carries as a job
                        // glyph, a place, a cause and the follow pointer - so the list
                        // was already showing them the way this page should, and the
                        // page had invented a second style for them.
                        //
                        // D55's one writer at three sizes had the answer sitting there:
                        // CardLine IS the roster row at frame width. Overview opens with
                        // it, and the standing lines go, because the row says them.
                        lines.Add(own ? StripLine(who) : CardLine(who));
                        // THE GAUGE TAKES OVER, AND HOVER IS THE DETAIL: the stats
                        // already in the gauge are not written again; hovering the
                        // gauge reveals them, so nothing is displayed twice in two
                        // formats.
                        //
                        // The strip already answers icon by icon - the plate's run
                        // built that hover for the plate's run, and SheetInput's LineStrip
                        // branch is generic, so it follows the strip into the sheet
                        // for free. Writing HP, stun, food, water, love, libido and
                        // moral again underneath was the same reading in a second
                        // format, which is what D75 un-retired Overview to avoid,
                        // not to create.
                    }
                    if (!own)
                    {
                        WriteActs(lines, who);
                    }
                    break;
                case Aspect.Fight:
                    WriteGrowth(lines, who);
                    break;
                case Aspect.Profile:
                    WriteProfile(lines, who, own);
                    break;
                case Aspect.Skills:
                    WriteSkills(lines);
                    break;
                case Aspect.Relations:
                    WriteRelations(lines, who, own, group);
                    break;
                case Aspect.Orders:
                    WriteOrders(lines, who);
                    break;
                default:
                    lines.Add(Row(aspect.ToString(), "—"));
                    break;
            }
        }
    }
}
