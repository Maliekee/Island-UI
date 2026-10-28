using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // Selected characters have a compact world reading and a fixed action dock.
    // The sheet replaces the reading; Bag and Details remain independent doors.
    // Props use their capability-driven windows and never create a world indicator.
    // All acts are still read from vanilla rather than inferred from target type.
    internal static partial class InventoryWindow
    {
        private static GameObject _unit;
        private static RectTransform _unitRect;
        private static Image _unitHead;       // the oak band the reading sits on
        private static TextMeshProUGUI _unitName;
        private static TextMeshProUGUI _unitKind;
        private static Image _unitAlertPlate;
        private static Image _unitAlertFace;
        private static TextMeshProUGUI _unitAlert;
        private static SheetRow _unitStrip;   // the gauge, the roster's own writer at k = 1
        private static SheetLine _unitLine;
        private static ActRow _unitRow;       // the acts: the one component, in its first home
        private static Image _unitChevron;    // the sheet's door, made visible:
        private static Image _unitChevronGlyph; // a chip hanging under the unit, pointing where the sheet goes
        private static KitTooltip _unitTip;
        private static CommonStates _unitFor;  // who the unit last bound to
        private static ItemInfo _unitThing;    // ...or what (D156)
        private static float _unitBoundAt;
        private static bool _unitHeadPressed; // a press begun on the reading band
        private const int CardPressNone = 0, CardPressPortrait = 1, CardPressStar = 2, CardPressManage = 3;
        private static int _cardPressed;      // what a press on the portrait card began on
        private static bool _unitDead;        // the strip stays down while the target is a corpse

        private const float UnitPadX = 5f;
        private const float UnitPadY = 3f;
        private const float UnitNameH = 10f;
        private const float UnitStripH = 11f;
        private const float UnitGap = 1f;      // between the two bands
        private const float UnitChip = 14f;    // the chevron chip, half of it below the unit's edge
        private const float UnitRebind = 0.25f; // how often the acts are re-read, in seconds

        private static float UnitHeadH { get { return UnitPadY * 2f + UnitNameH + UnitStripH; } }
        private static float UnitFootH { get { return _unitRow != null ? _unitRow.Height : ActRow.H; } }

        // The thing in focus as a prop, whatever wrapper it came in: a bare prop,
        // or a container's. For the veils, which cover vanilla's prop panels while
        // OUR window shows the same prop.
        private static ItemInfo FocusProp(object focus)
        {
            var prop = focus as ItemInfo;
            if (prop != null)
            {
                return prop;
            }
            var slot = focus as InventorySlot;
            return slot != null ? slot.GetComponent<ItemInfo>() : null;
        }

        internal static void TickUnit(ManagersScript mn)
        {
            if (!Settings.TheUnit.Value)
            {
                ShowUnit(false);
                return;
            }
            if (mn == null || !mn.allSet || mn.uiMN == null)
            {
                return;
            }
            _mn = mn;
            object focus = Focus();
            CommonStates who = focus as CommonStates;
            VeilVanilla(mn, who, FocusProp(focus));
            if (FocusProp(focus) != null) HideCursorName(mn);
            // D156: a thing has the indicator and the dock too - everything that is
            // targetable has a target indicator
            ItemInfo thing = who == null ? FocusProp(focus) : null;
            bool show = (who != null || thing != null) && !GameManager.nowEvent && !InScene(mn);
            if (!show)
            {
                if (_unit != null && _unit.activeSelf)
                {
                    ShowUnit(false);
                    if (_unitTip != null) { _unitTip.Hide(); }
                    if (_unitRow != null) { _unitRow.Show(false); _unitRow.Show(true); }
                }
                _unitFor = null;
                _unitThing = null;
                _unitHeadPressed = false;
                _cardPressed = CardPressNone;
                return;
            }
            if (thing != null)
            {
                if (!BuildUnit(mn))
                {
                    return;
                }
                PlaceUnit();
                ShowUnit(true);
                bool fresh =!ReferenceEquals(_unitThing, thing) || _unitFor != null;
                if (fresh || Time.unscaledTime - _unitBoundAt > UnitRebind)
                {
                    BindThing(thing, focus, fresh);
                }
                _unitRow.Paint();
                if (_markerHit != null)
                {
                    // the marker is the thing's now: no offer on it
                    _markerUnder.gameObject.SetActive(false);
                }
                PaintThingMarker(thing);
                ReadUnitInput();
                HideCursorName(mn);
                return;
            }
            _unitThing = null;
            // the sheet key: the same door as a click on the head (D107)
            KeyCode sheetKey = Settings.SheetKey.Value;
            if (sheetKey != KeyCode.None && Input.GetKeyDown(sheetKey))
            {
                ToggleSheet();
            }
            if (!BuildUnit(mn))
            {
                return;
            }
            PlaceUnit();
            ShowUnit(true);
            bool changed = !ReferenceEquals(_unitFor, who);
            if (changed || Time.unscaledTime - _unitBoundAt > UnitRebind)
            {
                BindUnit(who, changed);
            }
            PaintUnit(who);
            PaintWorldMarker(who);
            PaintMoreActs();
            ReadUnitInput();
            HideCursorName(mn);
        }

        // the unit and the world marker's layer come and go together: the marker
        // lives under the windows (EnsureWorldMarker), not inside the unit
        private static void ShowUnit(bool on)
        {
            if (_unit != null && _unit.activeSelf != on)
            {
                _unit.SetActive(on);
            }
            if (_worldLayer != null && _worldLayer.activeSelf != on)
            {
                _worldLayer.SetActive(on);
            }
            if (!on)
            {
                HideGroundRing();
            }
        }

        internal static void ResetUnitForSceneLoad()
        {
            _unit = null;
            _worldLayer = null;
            _unitRect = null;
            _unitStrip = null;
            _unitRow = null;
            _unitTip = null;
            _unitFor = null;
            _markerHit = null;
            _markerUnder = null;
            _underPlate = _underFooter = _underEmblem = null;
            _offerRing = null;
            _worldMarker = null;
            _ring = null;
            CombatList.Clear();
            ResetCombatMarkers();
            ResetThingForSceneLoad();
            _moreActsPanel = null; _moreActsOpen = false; MoreActs.Clear(); MoreButtons.Clear(); MoreLabels.Clear();
            _veils.Clear();
            // the vicinity's foot lived under the same root
            _reachFoot = null;
            _reachFootRow = null;
            _reachFootTip = null;
            _reachFootBound = false;
            FootChips.Clear();
        }

        // VANILLA NAMES THE HELD TARGET AT THE POINTER. PlayerMove's hover pass
        // calls CursorName(target) every frame while pMove.target is set - not the
        // thing under the cursor, the thing you are dealing with - and it stops
        // only when the interaction ends. Vanilla's interactions end at the next
        // press; ours hold the target until you walk away (D100), so the label
        // stuck to the cursor for as long as the unit was up. The unit
        // already says the name, so the label goes while the unit shows it.
        // CursorName returns early while `cursored` is unchanged, so one SetActive
        // holds until the pointer finds something new - which is exactly when the
        // label should speak again.
        private static void HideCursorName(ManagersScript mn)
        {
            UIManager ui = mn.uiMN;
            if (ui == null || ui.cursorText == null || !ui.cursorText.gameObject.activeSelf)
            {
                return;
            }
            PlayerMove pm = mn.gameMN != null ? mn.gameMN.pMove : null;
            if (pm != null && pm.target != null && ReferenceEquals(ui.cursored, pm.target))
            {
                ui.cursorText.gameObject.SetActive(false);
            }
        }

        // VANILLA'S OWN PANELS FOR THE SAME TARGET ARE VEILED, NOT CLOSED.
        // NPCPanel's verbs, the search panel's carry and dissect, buildPanel's
        // Move · Remove · Repair and propPanel's acts are what the act rows are
        // read from (D65) - so the panels must stay ACTIVE, or there is nothing to
        // read - but drawn twice they are the fourth health bar.
        // A CanvasGroup at alpha 0 keeps every activeSelf true and takes the pixels
        // and the clicks away; teardown puts them back.
        private static readonly List<CanvasGroup> _veils = new List<CanvasGroup>();

        private static void Veil(GameObject panel, bool on)
        {
            if (panel == null)
            {
                return;
            }
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                if (!on) { return; }
                cg = panel.AddComponent<CanvasGroup>();
            }
            if (on && !_veils.Contains(cg)) { _veils.Add(cg); }
            float a = on ? 0f : 1f;
            if (!Mathf.Approximately(cg.alpha, a))
            {
                cg.alpha = a;
                cg.blocksRaycasts = !on;
                cg.interactable = !on;
            }
        }

        private static void VeilVanilla(ManagersScript mn, CommonStates who, ItemInfo prop)
        {
            UIManager ui = mn.uiMN;
            GameManager gm = mn.gameMN;
            if (ui == null || gm == null)
            {
                return;
            }
            bool person = who != null && ReferenceEquals(gm.menuNPC, who);
            bool body = who != null && gm.pMove != null && ReferenceEquals(gm.pMove.searchNPC, who);
            // in a scene the prop panel is the SCENE'S (SexManager lights its controls
            // there), not the thing's: never veiled then
            bool thing = prop != null && !InScene(mn);
            Veil(ui.npcPanel, person);
            Veil(ui.npcSearchPanel, body);
            Veil(ui.buildPanel, thing);
            Veil(ui.propPanel, thing);
            Veil(ui.propValueText != null ? ui.propValueText.gameObject : null, thing);
        }

        // A SCENE IS THE GAME'S (the Gray Cushion Sofa started a scene that could not
        // be navigated, because the vanilla menu displayed in the scene was hidden;
        // pressing Escape brought it forward). A sex scene
        // drives its controls through propPanel (SexManager: PropPanelStateChange),
        // and the sofa you used was still our target, so the thing's veil hid them;
        // Escape untargeted it and lifted the veil. While the player is in a scene -
        // not controllable, or in one of the game's sex states - the prop panels are
        // left to the game and the unit steps aside.
        private static bool InScene(ManagersScript mn)
        {
            PlayerMove p = mn != null && mn.gameMN != null ? mn.gameMN.pMove : null;
            return p != null && (!p.controllable
                || (p.common != null && p.common.sex == CommonStates.SexState.Playing));
        }

        internal static void UnveilVanilla(ManagersScript mn)
        {
            for (int i = 0; i < _veils.Count; i++)
            {
                CanvasGroup cg = _veils[i];
                if (cg == null) { continue; }
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }
            _veils.Clear();
        }

        // ---- building --------------------------------------------------------

        private static bool BuildUnit(ManagersScript mn)
        {
            if (_unit != null)
            {
                return true;
            }
            if (mn.uiMN.itemDescPanel == null || !Theme.Ensure(mn, mn.inventory))
            {
                return false;
            }
            // the same parent as the action bar, so the two stack in one space
            Transform parent = mn.uiMN.itemDescPanel.transform.parent;
            if (parent == null)
            {
                return false;
            }
            _unit = Owned.New("IslandUI_Unit");
            _unit.transform.SetParent(parent, false);
            _unitRect = (RectTransform)_unit.transform;
            _unitRect.anchorMin = new Vector2(0.5f, 0f);
            _unitRect.anchorMax = new Vector2(0.5f, 0f);
            _unitRect.pivot = new Vector2(0.5f, 0f);
            _unitRect.sizeDelta = new Vector2(UnitWidth(), UnitHeadH + UnitGap + UnitFootH);
            CanvasIsolation.Isolate(_unit);

            _unitRow = ActRow.Create(_unit.transform, "Acts");
            _unitRow.RightCount = 2;
            _unitRow.IconMode = true;
            _unitRow.Enamel = true;
            _unitRow.Compact = true;
            _unitHead = Theme.KitImage(_unit.transform, "Head", "kit_plank_oak", 18, Color.white);
            // a raycast target: a click on the head must not reach the world, or the
            // walk it starts ends the target the click meant to open
            _unitHead.raycastTarget = true;
            // THE DOOR IS VISIBLE: a chip hanging under the unit's middle, its chevron
            // pointing down while the sheet is folded and up while it is open - the
            // expand/collapse, so the sheet's existence is said
            _unitChevron = Theme.KitImage(_unit.transform, "Chevron", "kit_chip_walnut_round", 0, Color.white);
            _unitChevron.raycastTarget = true;
            _unitChevronGlyph = Theme.KitImage(_unitChevron.transform, "Glyph", "kit_chevron", 0, Theme.KitCream);
            _unitChevronGlyph.raycastTarget = false;

            _unitName = Theme.MakeKitText(_unit.transform, "Name", SheetTextSize, Theme.KitCream,
                TextAlignmentOptions.MidlineLeft, bold: true);
            _unitName.enableWordWrapping = false;
            _unitName.overflowMode = TextOverflowModes.Overflow;
            _unitKind = Theme.MakeKitText(_unit.transform, "Kind", RosterSmall, Theme.KitCream,
                TextAlignmentOptions.MidlineLeft);
            _unitKind.enableWordWrapping = false;
            _unitKind.overflowMode = TextOverflowModes.Overflow;
            _unitKind.alpha = 0.75f;
            // A LIT DOOR STANDS IN FRONT OF THE TITLE: an open door rises out of the
            // row into the head's band, and the head, made after the row, drew over
            // it. The row goes in front of the head, its name and kind - not of
            // what comes after (the alert, the gauge)
            _unitRow.Go.transform.SetSiblingIndex(_unitKind.transform.GetSiblingIndex());
            // the alert: the cause word on a paper slip with the frown, present only
            // when true - a condition, not a field (§6.26's one surviving badge idea)
            _unitAlertPlate = Theme.KitImage(_unit.transform, "AlertPlate", "kit_paper", 8, Color.white);
            _unitAlertPlate.raycastTarget = false;
            _unitAlertFace = Theme.KitImage(_unitAlertPlate.transform, "Face", "kit_frown", 0, MoralBad);
            _unitAlertFace.raycastTarget = false;
            _unitAlert = Theme.MakeKitText(_unitAlertPlate.transform, "Word", RosterSmall, MoralBad,
                TextAlignmentOptions.MidlineLeft);
            _unitAlert.enableWordWrapping = false;
            _unitAlert.overflowMode = TextOverflowModes.Overflow;
            _unitAlertPlate.gameObject.SetActive(false);

            // the gauge: a sheet row's strip parts under the unit, laid out by the
            // roster's writer at k = 1, which is the roster's own density
            _unitStrip = new SheetRow();
            _unitStrip.Go = Owned.New("Strip");
            _unitStrip.Go.transform.SetParent(_unit.transform, false);
            var stripRect = (RectTransform)_unitStrip.Go.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = Vector2.one;
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = Vector2.zero;
            EnsureRowExtras(_unitStrip, new SheetLine { Kind = LineStrip });

            _unitTip = KitTooltip.Create((RectTransform)parent);
            LayoutUnit();
            Plugin.Log.LogInfo("The unit built");
            return true;
        }

        // the action bar's width when it is up - the unit is the bar's own width, so
        // the two read as one stack - else the bar's arithmetic
        private static float UnitWidth()
        {
            if (_root != null && _colWidth[CharacterColumn] > 50f)
            {
                return _colWidth[CharacterColumn];
            }
            return BarWidth();
        }

        // THE TARGET DOCK IS PLACED ON THE BAR ALONE (Forgejo #46): its left end
        // is the bar's. UnitWidth's sheet column exists only once the window has first been built,
        // and outlives its closing - so the first press of a door moved the dock 7
        // right (and could reflow its rows: six acts went from 3 + 3 to one row),
        // and nothing ever moved it back
        private static float BarWidth()
        {
            RectTransform bar = ActionBar.Rect;
            if (bar != null && bar.sizeDelta.x > 50f)
            {
                return bar.sizeDelta.x;
            }
            int n = Mathf.Clamp(Settings.ActionBarSlots.Value, 1, 10);
            return n * Theme.SlotPitch - (Theme.SlotPitch - Theme.SlotSize) + 4f;
        }

        // how far the portrait card reaches left of the plank (PaintAnimeIdentity
        // places it at board x -170)
        private static float UnitCardLead() => 170f / AnimePieces.Px;

        private static void LayoutUnit()
        {
            // the row alone sizes the plank (Forgejo #129): a name wider than the
            // acts sticks out past its end on the tab (PaintAnimeIdentity). However
            // many acts there are they stay one row: two rows break the board's design
            float w = Mathf.Max(100, _unitRow.AnimeWidth);
            _unitRow.Layout(0f, 0f, w);
            _unitRect.sizeDelta = new Vector2(w, UnitFootH);
            PaintAnimeIdentity(w);
            _unitChevron.gameObject.SetActive(false);
            _unitChevron.rectTransform.sizeDelta = new Vector2(UnitChip, UnitChip);
            Theme.Place(_unitChevronGlyph, 3f, 4f, UnitChip - 6f, UnitChip - 8f);
        }

        // THE DOCK IS ANCHORED AT ITS LEFT END (Forgejo #47, #52): the portrait and
        // the doors beside it hold still from target to target,
        // and the dock grows to the right, past the bar's end when its acts need it.
        // Its left end is the portrait card's, not the plank's: the
        // card, left of the plank, starts where the bar and the window above do.
        private static void PlaceUnit()
        {
            RectTransform bar = ActionBar.Rect;
            float y = ActionBar.Lift + (bar != null ? bar.sizeDelta.y : 25f) + 6f;
            _unitRect.anchorMin = _unitRect.anchorMax = new Vector2(.5f, 0f);
            _unitRect.pivot = new Vector2(0f, 0f);
            _unitRect.anchoredPosition = new Vector2(-BarWidth() * .5f + UnitCardLead(), y);
            LayoutUnit();
        }

        // Whether the unit is up for the current focus - the one question the
        // layout asks of it, so the sheet knows what stands above it.
        private static bool UnitUp()
        {
            object focus = Focus();
            return Settings.TheUnit.Value && !GameManager.nowEvent
                && (focus is CommonStates || FocusProp(focus) != null);
        }

        // Where the target column's TOP must land while the unit is the head: just
        // under the unit's bottom edge and the chevron that straddles it. NaN when
        // the unit is not up or not the head.
        private static float UnitHang() { return float.NaN; }
        private static float UnitStanding()
        {
            if (!UnitUp()) return 0f;
            return UnitFootH + 12f + ActionBar.Lift + (ActionBar.Rect != null ? ActionBar.Rect.sizeDelta.y : 25f);
        }

        // ---- binding: the reading and the acts, from vanilla, never derived ----

        private static void BindUnit(CommonStates who, bool changed)
        {
            if (changed) _moreActsOpen = false;
            _unitFor = who;
            _unitBoundAt = Time.unscaledTime;
            bool hostile = Hostile(who);
            // D89: a hostile withholds its name and condition - the bars and the acts
            // against it are all it shows. Not one of yours in revolt: you know
            // their name, and the marker's red frame says what they are now.
            bool withheld = hostile && !who.Employed();
            string name = withheld ? string.Empty : (who.charaName ?? string.Empty);
            if (_unitName.text != name) { _unitName.text = name; }
            string kind = withheld ? string.Empty : KindName(_mn != null ? _mn.npcMN : null, who);
            if (string.Equals(kind, who.charaName, System.StringComparison.OrdinalIgnoreCase))
            {
                kind = string.Empty;
            }
            string lv = Shared.Loc.Get("sheet.lv", "Lv {0}", Observe.Sees(who, Observe.Level) ? who.level.ToString("0") : Observe.Unknown);
            string apposition = string.IsNullOrEmpty(kind) ? lv : lv + Shared.Loc.Separator + kind;
            // THE STATE AXIS (target-matrix.md D3): dead is 1 with life gone, knocked
            // out is 1 with life left, 5 is on someone's shoulder. A corpse has no
            // condition to read - its needs and bars say nothing a player can act on
            // - so the
            // word takes the reading's place; knocked out keeps the bars, since the
            // revive is what you are watching, and drops the needs.
            bool dead = who.dead == 1 && who.life <= 0.0;
            bool downed = who.dead == 1 && !dead;
            bool carried = who.dead == 5;
            string state = dead ? Shared.Loc.Get("unit.state.corpse", "corpse")
                : downed ? Shared.Loc.Get("unit.state.knocked_out", "knocked out")
                : carried ? Shared.Loc.Get("unit.state.carried", "carried") : string.Empty;
            if (!string.IsNullOrEmpty(state))
            {
                apposition = withheld ? state : apposition + Shared.Loc.Separator + state;
            }
            if (_unitKind.text != apposition) { _unitKind.text = apposition; }
            bool warn = false;
            string cause = withheld || dead ? string.Empty : DiscontentWord(who, out warn);
            bool alert = !string.IsNullOrEmpty(cause);
            if (_unitAlertPlate.gameObject.activeSelf != alert) { _unitAlertPlate.gameObject.SetActive(alert); }
            if (alert && _unitAlert.text != cause) { _unitAlert.text = cause; }
            if (alert)
            {
                // red for why they are unhappy, amber for what is coming
                Color tint = warn ? MoodWarn : MoralBad;
                if (_unitAlert.color != tint) { _unitAlert.color = tint; _unitAlertFace.color = tint; }
            }

            _unitLine = StripLine(who);
            if (changed || !Mathf.Approximately(_unitStrip.DrewMax, _unitLine.Max)
                || _unitStrip.DrewBaby != (_unitLine.Fracs[7] >= 0f))
            {
                LayoutRosterStrip(_unitStrip, _unitLine, 6f, 150f, 18f, 12f);
            }
            else
            {
                ShowStrip(_unitStrip, true);
            }
            if (dead)
            {
                ShowStrip(_unitStrip, false);   // a corpse: the word is the whole reading
            }
            // the reading's icons hide on a hostile stranger - no condition (D89) -
            // and on a body that is down: the bars stay, the needs go. One of yours
            // in revolt keeps theirs: why they revolted is the point
            bool needs = !withheld && !dead && !downed && !carried;
            for (int i = 0; _unitStrip.IconFills != null && i < _unitStrip.IconFills.Length; i++)
            {
                bool show = needs && (i < StripIcons - 1 || _unitStrip.DrewBaby);
                if (_unitStrip.IconRims[i].gameObject.activeSelf != show)
                {
                    _unitStrip.IconRims[i].gameObject.SetActive(show);
                    _unitStrip.IconBacks[i].gameObject.SetActive(show);
                    _unitStrip.IconFills[i].gameObject.SetActive(show);
                }
            }
            for (int i = 0; _unitStrip.Seps != null && i < _unitStrip.Seps.Length; i++)
            {
                bool show = needs && (i == 0 || _unitStrip.DrewBaby);
                if (_unitStrip.Seps[i].gameObject.activeSelf != show) { _unitStrip.Seps[i].gameObject.SetActive(show); }
            }
            _unitDead = dead;
            ReadUnitActs(who);
        }

        // THE SOCKETS ARE FIXED (D105). Shape acts - talk, carry, follow - own the
        // first three, in that order, whether or not each is lit, so a word means
        // the same place on every person. The thing acts fill from the fourth; the
        // doors come last. Everything is vanilla's own lit button, read by NAME
        // (D65); the order is the only thing that is ours.
        private static readonly string[] UnitShapeOrder = { "talk", "carry", "follow" };
        private static readonly string[] UnitDoors = { "craft", "shop", "token", "shop_sweets", "vacuum" };

        private static bool UnitElsewhere(string name)
        {
            // the standing settings live on Orders; Status is the indicator's own door
            return name == "status" || name == "position" || name == "order" || name == "talkedit";
        }

        private static void ReadUnitActs(CommonStates who)
        {
            _unitRow.Clear();
            ManageActs.Clear();
            if (_mn == null)
            {
                return;
            }
            // NOT ONE OF YOURS STANDING (D159): an enemy or a stranger has the
            // indicator and nothing to press - enemies get no dock; a body has the game's own acts, drawn whether or not the
            // game is searching it
            if (!(who.Employed() && who.life > 0.0) && _mn.gameMN != null && _mn.gameMN.pMove != null
                && !ReferenceEquals(who, _mn.gameMN.pMove.common))
            {
                if (ReachRule.Body(who))
                {
                    ReadBodyActs(who);
                }
                LayoutUnit();
                _unitRow.Show(_unitRow.Any);
                return;
            }
            // THE BAG DOOR: our own, in the last socket, lit while the
            // bag window is up - a person's bag is a window on the bar, not a page
            bool hasBag = (who.bag != null && who.bag.slots != null && who.bag.slots.Length > 0) || HasLoadout(who);
            if (hasBag)
            {
                _unitRow.Set(ActRow.Sockets - 1, BagDoor(Shared.Loc.Get("unit.bag", "Bag"), !InReach(who),
                    Shared.Loc.Get("unit.caption.inventory", "INVENTORY")));
            }
            // ...and the sheet's door left of it, or last when there is no bag, where
            // there is a sheet: a stranger's corpse has none
            if (TargetHasSheet(who))
            {
                _unitRow.Set(ActRow.Sockets - (hasBag ? 2 : 1), new ActRow.Slot
                {
                    Name = "details",
                    Label = Shared.Loc.Get("unit.details", "Details"),
                    Caption = Shared.Loc.Get("unit.caption.character", "CHARACTER"),
                    Icon = HudGlyphs.Get("details"),
                    Lit = _sheetOpen,
                    Press = ToggleSheet,
                    // their attribute points to spend, on the door to their sheet
                    Tag = AttributesToSpend(who) ? who.statusPoint.ToString() : null,
                    TagKind = PointsTag.Kind.Attribute,
                });
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            IList<TargetVerbs.Verb> live = TargetVerbs.Current;
            bool near = InReach(who);
            var rest = new List<TargetVerbs.Verb>();
            var doors = new List<TargetVerbs.Verb>();
            var foreign = new List<TargetVerbs.Verb>();
            for (int i = 0; i < live.Count; i++)
            {
                TargetVerbs.Verb v = live[i];
                if (UnitElsewhere(v.Name))
                {
                    continue;
                }
                int slot = UnitShapeSlot(v.Name);
                if (slot >= 0)
                {
                    _unitRow.Set(slot, UnitSlot(who, v, near));
                }
                else if (System.Array.IndexOf(UnitDoors, v.Name) >= 0)
                {
                    doors.Add(v);
                }
                else if (v.Foreign)
                {
                    foreign.Add(v);
                }
                else
                {
                    rest.Add(v);
                }
            }
            // THE REST TAKE THE ROW (with space on the bar, acts no longer wait inside
            // a More actions button). Every
            // act with a glyph takes the next free socket after the shape's three -
            // the rarer acts first, the doors after them, a mod's acts last (Forgejo
            // #52: Purpose trails Talk, Carry and Follow on a person, vanilla's before
            // another mod's) - and More holds only what has no glyph to stand on, or no
            // room left before the bag and details doors.
            rest.AddRange(doors);
            rest.AddRange(foreign);
            var placed = new List<TargetVerbs.Verb>();
            MoreActs.Clear();
            for (int i = 0; i < rest.Count; i++)
            {
                (ActGlyph(rest[i]) != null ? placed : MoreActs).Add(rest[i]);
            }
            int next = ActRow.ShapeSockets;
            if (UnitOffersPlace(who))
            {
                _unitRow.Set(next++, PlaceSlot(who));
            }
            int room = ActRow.Sockets - 2 - next;
            while (placed.Count > (MoreActs.Count > 0 || placed.Count > room ? room - 1 : room))
            {
                MoreActs.Insert(0, placed[placed.Count - 1]);
                placed.RemoveAt(placed.Count - 1);
            }
            for (int i = 0; i < placed.Count; i++)
            {
                _unitRow.Set(next++, UnitSlot(who, placed[i], near));
            }
            if (MoreActs.Count > 0) _unitRow.Set(next, new ActRow.Slot { Name = "more", Label = Shared.Loc.Get("unit.more_actions", "More actions"), Icon = HudGlyphs.Get("more"), Lit = _moreActsOpen, Press = ToggleMoreActs });
            ApplyDockPreview();
            LayoutUnit();
            // an enemy's dock has nothing on it, and an empty dock is not drawn
            _unitRow.Show(_unitRow.Any);
        }

        // SET PLACE ON THE DOCK (Forgejo #166): the Orders page's Set place, for
        // whom that page offers it, in the socket after Follow - an order, as
        // Follow is, so a Character act, and like Follow pressed from anywhere. A
        // press arms the same pick (TogglePlacing), a second ends it, and the tile
        // wears Follow's check while it is up, whichever gate armed it.
        private const string UnitPlace = "place";

        private static bool UnitOffersPlace(CommonStates who)
        {
            PlayerMove p = _mn.gameMN != null ? _mn.gameMN.pMove : null;
            return who.nMove != null && p != null && !ReferenceEquals(who, p.common) && HasAspect(Aspect.Orders, who, false)
                && _mn.uiMN != null && _mn.uiMN.territorySlider != null && _mn.uiMN.chaseSlider != null;
        }

        private static ActRow.Slot PlaceSlot(CommonStates who)
        {
            return new ActRow.Slot
            {
                Name = UnitPlace,
                Label = Shared.Loc.Get("orders.set_place", "Set place"),
                Icon = HudGlyphs.Get(UnitPlace),
                Lit = PlacingFor(who),
                Kind = ActRow.ActKind.Character,
                Press = () => TogglePlacing(who),
            };
        }

        // A BODY'S DOCK (D159): Carry and Dissect where the game would light them -
        // its own two checks, the ones SearchNPC asks before lighting the buttons -
        // the pockets' door, and the sheet's where there is a sheet (a friend's).
        // All of it within 1 m of the body's edge (ReachRule.Live); a press there
        // starts the game's search and presses its button (PressBodyAct).
        private static readonly string[] BodyActs = { "CarryButton", "DissectButton" };

        private static void ReadBodyActs(CommonStates who)
        {
            // a body the game turns into loot, or never searches, has no dock at all:
            // Carry and Dissect begin the game's search, and that shows the pockets too
            if (!PocketsOpen(who))
            {
                return;
            }
            bool near = InReach(who);
            NPCManager npcs = _mn.npcMN;
            for (int i = 0; i < BodyActs.Length; i++)
            {
                string name = BodyActs[i];
                bool offered = npcs != null && (name == "CarryButton"
                    ? npcs.CarriableNPCCheck(who.npcID, who) : npcs.DissectableCheck(who));
                if (!offered)
                {
                    continue;
                }
                Transform t = _mn.uiMN != null && _mn.uiMN.npcSearchPanel != null
                    ? _mn.uiMN.npcSearchPanel.transform.Find(name) : null;
                var v = new TargetVerbs.Verb { Name = name, Label = name,
                    Icon = t != null ? BodyIcon(t) : null };
                _unitRow.Set(UnitShapeSlot(name), new ActRow.Slot
                {
                    Name = name,
                    Label = BodyActWord(v),
                    Icon = ActGlyph(v),
                    Live = near,
                    Kind = ActRow.ActKind.Character,
                    Press = () => PressBodyAct(who, name),
                });
            }
            bool pockets = who.bag != null && who.bag.slots != null && who.bag.slots.Length > 0;
            if (pockets)
            {
                _unitRow.Set(ActRow.Sockets - 1, BagDoor(Shared.Loc.Get("unit.pockets", "Pockets"), !near));
            }
            if (TargetHasSheet(who))
            {
                _unitRow.Set(ActRow.Sockets - (pockets ? 2 : 1), new ActRow.Slot
                {
                    Name = "details", Label = Shared.Loc.Get("unit.details", "Details"), Icon = HudGlyphs.Get("details"),
                    Caption = Shared.Loc.Get("unit.caption.character", "CHARACTER"),
                    Lit = _sheetOpen, Press = ToggleSheet,
                    Tag = AttributesToSpend(who) ? who.statusPoint.ToString() : null,
                    TagKind = PointsTag.Kind.Attribute,
                });
            }
        }

        // WHOSE POCKETS ARE YOURS TO OPEN (some enemies, once killed, disappear into
        // loot when their corpse is clicked; targeted with Tab instead, they exposed a
        // backpack holding items vanilla never shows). Yours, living, and the player's own; anyone else's only where the game's
        // own click searches them (PlayerMove.InteractNPC, by charaType): an enemy
        // (10) knocked out, or dead with no loot to become or already dissected -
        // one dead with its DropItem on turns INTO that loot and its bag is never
        // shown, one with no bag slots opens nothing; an animal (100) only knocked
        // out and carriable; the harvestable kind (11) once harvested; a follower's
        // body (1) as before. A trader (2) or a living stranger shows no bag.
        private static bool PocketsOpen(CommonStates who)
        {
            if (who == null)
            {
                return false;
            }
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if ((p != null && ReferenceEquals(who, p.common)) || (who.Employed() && who.life > 0.0))
            {
                return true;
            }
            if (!ReachRule.Body(who))
            {
                return false;
            }
            bool knockedOut = who.life > 0.0 && who.faint <= 0.0;
            switch (who.charaType)
            {
                case 1:
                    return true;
                case 10:
                    if (who.bagSlot == 0)
                    {
                        return false;
                    }
                    DropItem loot = who.GetComponent<DropItem>();
                    return knockedOut || loot == null || !loot.enabled || who.IsDissected();
                case 11:
                    ItemInfo crop = who.GetComponent<ItemInfo>();
                    return crop != null && crop.life <= 0f;
                case 100:
                    return knockedOut && _mn.npcMN != null && _mn.npcMN.CarriableNPCCheck(who.npcID);
                default:
                    return false;
            }
        }

        private static Sprite BodyIcon(Transform t)
        {
            Image own = t.GetComponent<Image>();
            foreach (Image child in t.GetComponentsInChildren<Image>(true))
            {
                if (child.transform != t && child.sprite != null)
                {
                    return child.sprite;
                }
            }
            return own != null ? own.sprite : null;
        }

        // The game's search, begun as a click on the body begins it, then its button
        // pressed through (D65). While another interaction of the game's is still
        // unwinding, the press waits for it (LandBodyAct). Carry puts the body on
        // your back, and that ends the target: the act used it up.
        private static CommonStates _bodyActFor;
        private static string _bodyActName;
        private static float _bodyActUntil;

        private static void PressBodyAct(CommonStates who, string name)
        {
            if (who == null || _mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null || !InReach(who)
                || !PocketsOpen(who))
            {
                return;
            }
            PlayerMove p = _mn.gameMN.pMove;
            if (!ReferenceEquals(p.searchNPC, who))
            {
                if (_mn.gameMN.nowAction)
                {
                    p.target = null;   // let the game's own loop go first
                    _bodyActFor = who;
                    _bodyActName = name;
                    _bodyActUntil = Time.unscaledTime + 1.5f;
                    return;
                }
                GameObject hit = who.gameObject;
                foreach (Collider c in who.GetComponentsInChildren<Collider>())
                {
                    if (c.gameObject.layer == 14 && c.transform.parent == who.transform)
                    {
                        hit = c.gameObject;
                        break;
                    }
                }
                p.target = hit;
                p.StartCoroutine(p.SearchNPC(who));   // lights the panel before its first yield
            }
            Transform t = _mn.uiMN.npcSearchPanel != null ? _mn.uiMN.npcSearchPanel.transform.Find(name) : null;
            Button button = t != null && t.gameObject.activeSelf ? t.GetComponent<Button>() : null;
            if (button == null || !button.interactable)
            {
                return;
            }
            button.onClick.Invoke();
            _unitBoundAt = 0f;
            if (name == "CarryButton" && _heldVar == Held.Person && ReferenceEquals(_heldRef, who) && who.dead == 5)
            {
                Drop("carried");
            }
        }

        // Tick: a body's act that waited for the game to let go
        private static void LandBodyAct()
        {
            CommonStates who = _bodyActFor;
            if (who == null)
            {
                return;
            }
            if (Time.unscaledTime > _bodyActUntil || _mn == null || _mn.gameMN == null)
            {
                _bodyActFor = null;
                return;
            }
            if (_mn.gameMN.nowAction)
            {
                return;
            }
            _bodyActFor = null;
            PressBodyAct(who, _bodyActName);
        }

        // THE INVENTORY DOOR ARMS AT ANY DISTANCE (out of reach, a press opens
        // nothing: it only shows that it was accepted, and the window appears once
        // you are inside the vicinity). Out of
        // reach the door is drawn faded but still toggles; lit, it is armed, and the
        // window opens by itself once you are in reach (the Tick watches it). The
        // "Out of reach" caption window it replaces (D155) is gone.
        // A container whose dock offers nothing else keeps its own door, which
        // starts open; every other door is the other kind's (SeedDoor, §6.37).
        // A thing's door wears a chest (ActRow), a person's the backpack.
        private static ActRow.Slot BagDoor(string label, bool far, string caption = null, bool container = false, bool thing = false)
        {
            SeedDoor(container);
            return new ActRow.Slot
            {
                Name = thing ? "chest" : "bag",
                Label = label,
                Caption = caption,
                Icon = HudGlyphs.Get(thing ? "vicinity" : "bag"),
                Lit = _bagOpen,
                Faded = far,
                Press = () =>
                {
                    ToggleBag();
                    if (far && _bagOpen)
                    {
                        CursorBadge.Show(Shared.Loc.Get("unit.opens_when_in_reach", "opens when you're in reach"));
                    }
                },
            };
        }

        // What a dimmed act says on hover: how much closer, in the target's own rule
        private static readonly string[] PlacementActs = { "MoveButton", "RemoveButton", "RepairButton" };

        // THE NUMBER HOLDS A BEAT: read every frame it ran with each step and the
        // tip twitched as you walked; it is read again twice a second, or at once
        // for another target or rule
        private const float CloserEvery = 0.5f;
        // the language's separator, set wide: "out of reach  ·  2.0 m closer"
        private static string CloserJoin => " " + Shared.Loc.Separator + " ";
        private static string _closerSaid;
        private static string _closerGapSaid;   // "N m closer" alone, or null: a faded door's tip
        private static object _closerFor;
        private static bool _closerPlacement;
        private static float _closerAt = -1f;

        private static string CloserBy(ActRow.Slot slot)
        {
            object focus = Focus();
            bool placement = !(focus is CommonStates) && slot != null && System.Array.IndexOf(PlacementActs, slot.Name) >= 0;
            if (_closerSaid != null && ReferenceEquals(focus, _closerFor) && placement == _closerPlacement
                && Time.unscaledTime - _closerAt < CloserEvery)
            {
                return _closerSaid;
            }
            float gap = focus is CommonStates who ? ReachRule.Gap(_mn, who) : ReachRule.Gap(_mn, FocusProp(focus), placement);
            // the two phrases are kept apart, so the faded door's tip can say the
            // distance without the "out of reach" (it used to cut the words out)
            _closerGapSaid = gap > 0.05f ? Shared.Loc.Get("unit.closer_by", "{0} m closer", gap.ToString("0.0")) : null;
            string outOfReach = Shared.Loc.Get("unit.out_of_reach", "out of reach");
            _closerSaid = _closerGapSaid != null ? outOfReach + CloserJoin + _closerGapSaid : outOfReach;
            _closerFor = focus;
            _closerPlacement = placement;
            _closerAt = Time.unscaledTime;
            return _closerSaid;
        }

        // HARNESS PREVIEW of the target dock's states, presentation only, for 15 s:
        // following (the check), a birth due (the sparkle), the bag out of reach
        // (the footprints) and the sheet's window open (the lift). Presses do what
        // they always do; nothing in the game or the save changes.
        // PreviewTargetDock(n) adds n more acts with no board tile (the game's
        // glyph on the blank tile, pressing does nothing), to see a row wrap.
        private static float _dockPreviewUntil = -1f;
        private static int _dockPreviewExtra;

        internal static string PreviewTargetDock()
        {
            return PreviewTargetDock(0);
        }

        internal static string PreviewTargetDock(int extra)
        {
            _dockPreviewUntil = Time.unscaledTime + 15f;
            _dockPreviewExtra = Mathf.Clamp(extra, 0, 8);
            _unitBoundAt = 0f;
            return "target dock preview for 15 s" + (extra > 0 ? ", " + _dockPreviewExtra + " more acts" : "");
        }

        private static void ApplyDockPreview()
        {
            if (Time.unscaledTime > _dockPreviewUntil) { return; }
            ActRow.Slot follow = _unitRow.Get(2);
            if (follow != null) { follow.Name = "goodbye"; follow.Lit = true; }
            int free = ActRow.ShapeSockets;
            bool due = false;
            while (free < ActRow.Sockets - 2 && _unitRow.Get(free) != null) { due |= _unitRow.Get(free).Name == "delivery"; free++; }
            if (free < ActRow.Sockets - 2 && !due)
            {
                _unitRow.Set(free, new ActRow.Slot { Name = "delivery", Label = "Give birth", Due = true, Kind = ActRow.ActKind.Purpose, Press = () => { } });
            }
            string[] words = { "Bathe", "Sleep", "Massage", "Hug", "Pose", "Feed", "Gift", "Dress" };
            for (int i = 0, at = free + 1; i < _dockPreviewExtra && at < ActRow.Sockets - 2; i++, at++)
            {
                if (_unitRow.Get(at) == null) { _unitRow.Set(at, new ActRow.Slot { Name = "preview" + i, Label = words[i], Kind = ActRow.ActKind.Purpose, Press = () => { } }); }
            }
            ActRow.Slot bag = _unitRow.Get(ActRow.Sockets - 1);
            if (bag != null && bag.Name == "bag") { bag.Faded = true; }
            ActRow.Slot sheet = _unitRow.Get(ActRow.Sockets - 2);
            if (sheet != null && sheet.Name == "details") { sheet.Lit = true; }
        }

        private static ActRow.Slot UnitSlot(CommonStates who, TargetVerbs.Verb v, bool near)
        {
            string name = v.Name;
            // TALK OPENS ITS CHOICES IN A BUBBLE (Forgejo #58): still vanilla's Talk
            // pressed through, by TalkBubble, which then reads the panel it opened
            bool talk = name == "talk";
            // CRAFT IS A WINDOW'S KEY, as Character and Inventory are: drawn open
            // while the crafting window is up, whoever opened it - the player
            // panel's button shows it too - and pressed again it puts it away
            bool craft = name == "craft";
            return new ActRow.Slot
            {
                Name = name,
                Label = BodyActWord(v),
                Icon = ActGlyph(v),
                Lit = name == "goodbye",             // following: the toggle is on
                Due = name == "delivery",            // she is due: the act stands out
                Live = near || ReachFree(name),       // D73: reach decides
                // Forgejo #52: Talk, Carry, Follow are what every person offers; the rest
                // is what this one is for
                Kind = UnitShapeSlot(name) >= 0 ? ActRow.ActKind.Character : ActRow.ActKind.Purpose,
                Held = talk ? () => TalkBubble.OpenFor(who) : (System.Func<bool>)null,
                Open = craft ? () => HudOpen("craft") : (System.Func<bool>)null,
                Press = talk ? () => TalkBubble.Toggle(_mn, who, () => PressUnitAct(who, name))
                    : craft ? () => { if (HudOpen("craft")) _mn.uiMN.LeftPanelState(0); else PressUnitAct(who, name); }
                    : (System.Action)(() => PressUnitAct(who, name)),
            };
        }

        // ...and what the bubble asks of the dock: that this person is still the one
        // up on it and in reach, where their Talk stands, and the focus given back
        internal static bool TalkHeld(CommonStates who)
        {
            return who != null && _unit != null && _unit.activeInHierarchy && ReferenceEquals(_unitFor, who)
                && InReach(who);
        }

        internal static RectTransform TalkSocket()
        {
            return _unitRow != null && _unit != null && _unit.activeInHierarchy ? _unitRow.SocketRect("talk") : null;
        }

        internal static void FocusTalk(bool keyboard)
        {
            RectTransform socket = TalkSocket();
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (socket == null || es == null)
            {
                return;
            }
            es.SetSelectedGameObject(socket.gameObject);
            if (keyboard) { AnimeCell.KeyboardFocus(); }
        }

        private static int UnitShapeSlot(string name)
        {
            // a body's carry is the search panel's; leave (goodbye) is follow's other
            // face - vanilla lights one or the other, never both
            if (name == "CarryButton") { return 1; }
            if (name == "goodbye") { return 2; }
            // ...and a body's dissect takes follow's socket rather than a More
            // menu of one: a body never follows, since vanilla lights the search
            // panel or NPCPanel for a target, never both
            if (name == "DissectButton") { return 2; }
            return System.Array.IndexOf(UnitShapeOrder, name);
        }

        // What an act wears on the row: the shape's three by their socket, a body's
        // dissect, the doors and rarer acts by name (the census, window-system.md
        // §6.23), then vanilla's own art for a wordless button. Null means the act
        // has nothing to stand on in an icon row, and it waits in More.
        private static Sprite ActGlyph(TargetVerbs.Verb v)
        {
            string name = v.Name;
            if (name == "DissectButton") { return HudGlyphs.Get("dissect"); }
            int shape = UnitShapeSlot(name);
            if (shape >= 0) { return HudGlyphs.Get(UnitShapeOrder[shape]); }
            switch (name)
            {
                case "craft": return HudGlyphs.Get("craft");
                case "shop": return HudGlyphs.Get("trade");
                case "token": return HudGlyphs.Get("token");
                case "shop_sweets": return HudGlyphs.Get("sweets");
                case "vacuum": return HudGlyphs.Get("vacuum");
                case "delivery": return HudGlyphs.Get("birth");
                case "change": return HudGlyphs.Get("swap");
            }
            return TargetVerbs.Wordless(v) ? v.Icon : null;
        }

        // A body's two acts are vanilla's wordless 25px icons on npcSearchPanel,
        // so there is no caption to read (D65) and the tip said "DissectButton";
        // these are the words they wear instead.
        private static string BodyActWord(TargetVerbs.Verb v)
        {
            if (!TargetVerbs.Wordless(v)) { return v.Label; }
            return v.Name == "CarryButton" ? Shared.Loc.Get("unit.act.carry", "Carry")
                : v.Name == "DissectButton" ? Shared.Loc.Get("unit.act.dissect", "Dissect") : v.Label;
        }

        // Pressed THROUGH vanilla's own button (D65), re-read first as ActPress does:
        // a follower can start following between the read and the click.
        private static void PressUnitAct(CommonStates who, string name)
        {
            if (who == null || _mn == null || (!InReach(who) && !ReachFree(name)))
            {
                return;
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            IList<TargetVerbs.Verb> live = TargetVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Name == name)
                {
                    PressVerb(who, live[i]);
                    _unitBoundAt = 0f;   // re-read next tick: the press changed what is lit
                    return;
                }
            }
        }

        // FOLLOW KEEPS THE WINDOW (the follow act on an NPC does not close the
        // window). Pressed through vanilla (D65),
        // Follow and Leave both end in NPCMenuClose - NPCManager.FollowMe and
        // GoodBye close the menu behind the order - and that ended the target. The
        // order is wanted, the close is not, so the target is put back once
        // vanilla has unwound, held the way it was held: a world click's by
        // CheckNPC again, so walking away still ends it (D100), a roster pick's
        // by menuNPC, as the pick itself did.
        private static CommonStates _keepWho;
        private static GameObject _keepVia;
        private static bool _keepPanel;
        private static float _keepUntil;

        internal static void PressVerb(CommonStates who, TargetVerbs.Verb verb)
        {
            if (verb == null || (verb.Name != "follow" && verb.Name != "goodbye"))
            {
                TargetVerbs.Press(verb);
                return;
            }
            GameManager game = _mn != null ? _mn.gameMN : null;
            PlayerMove pMove = game != null ? game.pMove : null;
            UIManager ui = _mn != null ? _mn.uiMN : null;
            // vanilla's own menu is up for them - a world click, lit by NPCMenuOpen,
            // which is the only thing that lights the npcID acts (craft, trade, vacuum)
            bool panel = ui != null && ui.npcPanel != null && ui.npcPanel.activeSelf
                && game != null && ReferenceEquals(game.menuNPC, who);
            // ...and CheckNPC holds it: vanilla's target is a child of the NPC
            GameObject via = pMove != null && pMove.target != null
                && pMove.target.transform.parent != null
                && pMove.target.transform.parent.gameObject == who.gameObject
                ? pMove.target : null;
            if (TargetVerbs.Press(verb))
            {
                _keepWho = who;
                _keepVia = via;
                _keepPanel = panel;
                _keepUntil = Time.unscaledTime + 1f;
            }
        }

        // Tick, before the target resolves
        private static void RestoreKeptTarget()
        {
            CommonStates who = _keepWho;
            if (who == null)
            {
                return;
            }
            GameManager game = _mn.gameMN;
            if (Time.unscaledTime > _keepUntil || game == null || !who.gameObject.activeInHierarchy)
            {
                _keepWho = null;
                return;
            }
            if (game.nowAction)
            {
                return;   // vanilla's CheckNPC is still unwinding
            }
            _keepWho = null;
            // anything engaged in the meantime wins
            if (game.menuNPC != null || (game.pMove != null && game.pMove.searchNPC != null))
            {
                return;
            }
            // REOPENED AS IT WAS OPENED. A world click is vanilla's menu, and only
            // NPCMenuOpen lights the npcID acts: restoring menuNPC alone left the
            // verb reader to ask NPCPanelStateChange, which clears every button, so
            // a crafter lost Craft until clicked again. CheckNPC
            // is NPCMenuOpen plus the lifetime, so walking away still ends it.
            string how;
            if (_keepPanel && _keepVia != null && game.pMove != null && who.nMove != null)
            {
                game.pMove.target = _keepVia;
                game.pMove.StartCoroutine(game.pMove.CheckNPC(who.nMove));
                how = "CheckNPC";
            }
            else if (_keepPanel && _mn.uiMN != null)
            {
                ArmPick(who);   // no CheckNPC lifetime: ours to end (6.31)
                _mn.uiMN.NPCMenuOpen(who);
                how = "NPCMenuOpen";
            }
            else
            {
                // a roster pick held them by menuNPC alone, and so does this
                ArmPick(who);
                game.menuNPC = who;
                _mn.inventory.EquipSlotLoad();
                how = "menuNPC";
            }
            Plugin.Log.LogInfo("Follow/Leave kept the target: " + who.charaName + " (" + how + ")");
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // ---- painting ----------------------------------------------------------

        private static void PaintUnit(CommonStates who)
        {
            // the kind trails the name by the name's own width
            float nameW = _unitName.preferredWidth;
            float w = _unitRect.sizeDelta.x;
            float kx = UnitPadX + (string.IsNullOrEmpty(_unitName.text) ? 0f : nameW + 3f);
            var kr = _unitKind.rectTransform;
            if (!Mathf.Approximately(kr.anchoredPosition.x, kx))
            {
                Theme.Place(_unitKind, kx, UnitPadY, Mathf.Max(10f, w * 0.6f - kx), UnitNameH);
            }
            if (_unitAlertPlate.gameObject.activeSelf)
            {
                float tw = _unitAlert.preferredWidth + 14f;
                float ph = UnitNameH - 1f;
                Theme.Place(_unitAlertPlate, w - UnitPadX - tw, UnitPadY + 0.5f, tw, ph);
                Theme.Place(_unitAlertFace, 2f, (ph - 7f) * 0.5f, 7f, 7f);
                Theme.Place(_unitAlert, 10f, 0f, tw - 11f, ph);
            }
            // the strip's fills, from a fresh reading - HP moves by the second
            _unitLine = StripLine(who);
            if (!_unitDead && Mathf.Approximately(_unitStrip.DrewMax, _unitLine.Max)
                && _unitStrip.DrewBaby == (_unitLine.Fracs[7] >= 0f))
            {
                PaintStrip(_unitStrip, _unitLine);
            }
            _unitRow.Paint();
            // the chevron points where the sheet would go: towards the sheet's side
            // to open it, back into the unit to fold it (the sprite points down at 0)
            // the sheet hangs below the unit, which sits up at the top
            float angle = _sheetOpen ? 180f : 0f;
            var gr = _unitChevronGlyph.rectTransform;
            if (!Mathf.Approximately(gr.localEulerAngles.z, angle))
            {
                gr.localEulerAngles = new Vector3(0f, 0f, angle);
            }
            bool overDoor = _unitChevron != null && RectTransformUtility.RectangleContainsScreenPoint(
                _unitChevron.rectTransform, Input.mousePosition, null);
            float k = overDoor && Input.GetMouseButton(0) ? 0.8f : 1f;
            _unitChevron.color = new Color(k, k, k, 1f);
            _unitChevronGlyph.color = overDoor ? Color.white : Theme.KitCream;
        }

        // ---- input: the row presses through vanilla; the icons tell their number --

        private static void ReadUnitInput()
        {
            bool onRow = _unitRow.ReadInput();
            // THE PORTRAIT (Forgejo #49): the star on its corner is the favourite's
            // switch, and the rest of the card a door that only opens the sheet, on
            // its first page. A press on the one is never the other's, and each acts
            // on a release over what it began on.
            // A THING'S CARD wears the Manage key there instead (Forgejo #52)
            CommonStates cardWho = Focus() as CommonStates;
            bool onStar = !onRow && CanFavourite(cardWho) && OnCardStar;
            bool onManage = !onRow && !onStar && ManageKeyUp && OnManageKey;
            bool onCard = !onRow && !onStar && !onManage && _animeIdentity != null && _animeIdentity.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_animeIdentity.rectTransform, Input.mousePosition, null);
            if (Input.GetMouseButtonDown(0))
            {
                _cardPressed = onStar ? CardPressStar : onManage ? CardPressManage : onCard ? CardPressPortrait : CardPressNone;
            }
            PaintCardStar(cardWho, onStar, onStar && _cardPressed == CardPressStar && Input.GetMouseButton(0));
            PaintManageKey(onManage, onManage && _cardPressed == CardPressManage && Input.GetMouseButton(0));
            if (Input.GetMouseButtonUp(0))
            {
                if (_cardPressed == CardPressStar && onStar)
                {
                    ToggleMark(cardWho, ToggleFavourite);
                }
                else if (_cardPressed == CardPressManage && onManage)
                {
                    ToggleManage();
                }
                else if (_cardPressed == CardPressPortrait && onCard && cardWho != null && TargetHasSheet(cardWho))
                {
                    OpenSheetOnFirstPage();
                }
                _cardPressed = CardPressNone;
            }
            // THE HEAD IS THE SHEET'S DOOR (D107): a press-and-release on the
            // reading band, off any socket, folds or opens the target's sheet
            bool onHead = !onRow && !onStar && !onCard && _unitHead != null && _unitHead.gameObject.activeInHierarchy
                && (RectTransformUtility.RectangleContainsScreenPoint(_unitHead.rectTransform, Input.mousePosition, null)
                    || (_unitChevron.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_unitChevron.rectTransform, Input.mousePosition, null)));
            if (Input.GetMouseButtonDown(0) && onHead)
            {
                _unitHeadPressed = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_unitHeadPressed && onHead)
                {
                    ToggleSheet();
                }
                _unitHeadPressed = false;
            }
            // the number lives one hover away (D103): the icon's own tip, or the bars'
            // - and an act beyond reach says so, on hover and on a press it refused
            ActRow.Slot hotSlot = _unitRow.Hot >= 0 ? _unitRow.Get(_unitRow.Hot) : null;
            string tip = hotSlot == null ? null
                : hotSlot.Faded ? "<b>" + hotSlot.Label + "</b>\n"
                    + (hotSlot.Lit ? Shared.Loc.Get("unit.opens_when_in_reach", "opens when you're in reach") : Shared.Loc.Get("unit.opens_in_reach", "opens in reach"))
                    + (CloserBy(hotSlot) != null && _closerGapSaid != null ? CloserJoin + _closerGapSaid : string.Empty)
                : hotSlot.Live ? hotSlot.Label : "<b>" + hotSlot.Label + "</b>\n" + CloserBy(hotSlot);
            // an act that can run by itself says how to switch it (Forgejo #119)
            if (hotSlot != null && hotSlot.Auto != null)
            {
                tip = (hotSlot.Live && !hotSlot.Faded ? "<b>" + hotSlot.Label + "</b>" : tip) + "\n" + hotSlot.Auto.Tip;
            }
            // Set place says what a press does, as its button on Orders does
            if (hotSlot != null && hotSlot.Name == UnitPlace)
            {
                tip = "<b>" + hotSlot.Label + "</b>\n" + (hotSlot.Lit
                    ? Shared.Loc.Get("unit.place.armed", "Click the ground to set their place. Press again, right-click or Esc to cancel.")
                    : TipStanding[0]);
            }
            if (_unitRow.RefusedRecently)
            {
                CursorBadge.Show(CloserBy(_unitRow.RefusedSlot));
            }
            if (!onRow && _unitFor != null && _unitStrip.IconFills != null && _unitLine.IconTips != null)
            {
                for (int i = 0; i < _unitStrip.IconFills.Length && i < _unitLine.IconTips.Length; i++)
                {
                    var rect = _unitStrip.IconBacks[i].rectTransform;
                    if (_unitStrip.IconBacks[i].gameObject.activeInHierarchy
                        && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null))
                    {
                        tip = _unitLine.IconTips[i];
                        break;
                    }
                }
                if (tip == null && _unitStrip.BarRim != null && _unitStrip.BarRim.gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(_unitStrip.BarRim.rectTransform, Input.mousePosition, null))
                {
                    tip = _unitLine.BarTip;
                }
            }
            if (!onRow && tip == null) { tip = MarkerStatusTip(); }
            if (tip == null && onStar)
            {
                tip = cardWho.employ == CommonStates.Employ.Favorite
                    ? Shared.Loc.Get("dossier.favourite.unmark", "Favourite: click to unmark")
                    : Shared.Loc.Get("dossier.favourite.mark", "Click to make them a favourite");
            }
            if (tip == null && onManage)
            {
                tip = "<b>" + Shared.Loc.Get("unit.manage", "Manage") + "</b>\n"
                    + Shared.Loc.Get("unit.manage.tip", "Move or remove it");
            }
            if (tip == null && onCard)
            {
                tip = cardWho != null && TargetHasSheet(cardWho)
                    ? "<b>" + _unitName.text + "</b>\n" + Shared.Loc.Get("unit.portrait.sheet", "Click for their character sheet")
                    : _unitName.text;
            }
            // an item held over the card is a give, and the cursor badge says so;
            // the sheet's door is not what a release there does (Forgejo #89)
            if (onCard && OfferUnderPointer) { tip = null; }
            if (_unitTip != null)
            {
                if (!string.IsNullOrEmpty(tip)) { _unitTip.Tick(tip, null); }
                else if (_unitTip.Visible) { _unitTip.Hide(); }
            }
        }

        // For the harness: open the roster from a script, and end the target
        // as walking away would. Both are polled paths a /ui/click cannot reach.
        internal static bool RosterOpen()
        {
            return ToggleRoster(_mn);
        }

        internal static void EndTarget()
        {
            CloseFocus(_mn);
        }

        // ...and what the unit shows, socket by socket, so a script can assert on it
        internal static string UnitDump()
        {
            if (_unit == null || !_unit.activeSelf)
            {
                return "unit: hidden";
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("unit: ").Append(_unitName.text).Append(" | ").Append(_unitKind.text);
            if (_unitAlertPlate.gameObject.activeSelf) { sb.Append(" | alert ").Append(_unitAlert.text); }
            for (int i = 0; i < ActRow.Sockets; i++)
            {
                ActRow.Slot s = _unitRow.Get(i);
                sb.Append(" | ").Append(i).Append(':').Append(s != null ? s.Name : "-");
                if (s != null && !s.Live) { sb.Append(" (out of reach)"); }
            }
            return sb.ToString();
        }
    }
}
