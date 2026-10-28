using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // A THING HAS THE INDICATOR AND THE DOCK A PERSON HAS (window-system.md 6.32,
    // D156: target indicators for everything that is targetable, and a similar
    // panel that can toggle the inventory as well). D112 made a thing its window because a chest's grid sat
    // in the sheet while a well sat on the unit - two shapes for one relationship.
    // This is one shape for every target instead:
    //   - the INDICATOR floats over the thing, as a person's does: its name, whose
    //     it is, and its readings as bars - a level, breeding, wear (WritePropSheet,
    //     the same writer the window used), or its name alone when it has none;
    //   - the DOCK holds what you picked it for (Forgejo #52): Craft on a bench, the
    //     acts vanilla lit for it (Drink, Eat, Make into Meat), a wreck's Repair, and on
    //     a container the Inventory door - the person's Bag door, one standing choice
    //     for both. Move, Scale, Edit and Remove are the card's Manage key
    //     (InventoryWindow.Manage.cs);
    //   - a container's grid is the door's WINDOW (SheetOpenFor), and beyond reach
    //     the window is its caption and the acts are dim (D155).
    // Everything is still read off vanilla's own panels and pressed through them
    // (D65), which is why the target still ends when vanilla's UseProp does.
    internal static partial class InventoryWindow
    {
        private static readonly List<SheetLine> ThingLines = new List<SheetLine>();
        private static float _thingTop = 1.5f;
        private static readonly Image[] ThingBack = new Image[2];
        private static readonly Image[] ThingFill = new Image[2];
        private static readonly TextMeshProUGUI[] ThingValue = new TextMeshProUGUI[2];

        private static void BindThing(ItemInfo prop, object focus, bool fresh)
        {
            if (fresh)
            {
                _moreActsOpen = false;
            }
            _unitThing = prop;
            _unitFor = null;
            _unitDead = false;
            _unitBoundAt = Time.unscaledTime;
            bool yours = _mn.buildMN != null && _mn.buildMN.OwnCheck(prop);
            string text = PropTitle(prop) + (yours ? "  ·  " + Shared.Loc.Get("inv.thing.yours_lower", "yours") : string.Empty);
            if (_unitName.text != text) { _unitName.text = text; }
            ThingLines.Clear();
            WritePropSheet(ThingLines, prop);
            _thingTop = TopOf(prop);
            ReadThingActs(prop, focus, yours);
        }

        // WHAT VANILLA LIT, REMEMBERED. The dock reads its acts off vanilla's own
        // panels (D65), and those close the moment vanilla's UseProp lets the
        // thing go - on your first step. A thing we keep past that (D153) keeps
        // the acts vanilla last lit for it, and a press re-opens vanilla for the
        // thing before it presses through (PressThingAct).
        private sealed class KeptAct
        {
            public string Name;
            public string Label;
            public Sprite Icon;
            public int State;
            public bool Placement;
        }

        private static readonly List<KeptAct> KeptActs = new List<KeptAct>();
        private static ItemInfo _keptActsFor;

        private static bool VanillaShowing(ItemInfo prop)
        {
            return prop != null && _mn != null && _mn.buildMN != null && _mn.buildMN.tmpProp != null
                && ReferenceEquals(_mn.buildMN.tmpProp.GetComponent<ItemInfo>(), prop);
        }

        private static void RememberThingActs(ItemInfo prop, bool yours)
        {
            KeptActs.Clear();
            _keptActsFor = prop;
            // MOVE, REMOVE, REPAIR off buildPanel, which vanilla fills only
            // for what you built - its buttons keep their last state after it
            // closes, so a stranger's thing is never asked
            if (yours && _mn.uiMN.buildPanel != null && _mn.uiMN.buildPanel.activeSelf)
            {
                PropVerbs.ReadPlacement(_mn);
                IList<PropVerbs.Act> placement = PropVerbs.Placement;
                for (int i = 0; i < placement.Count; i++)
                {
                    KeptActs.Add(new KeptAct { Name = placement[i].Name, Label = PlacementWord(placement[i].Name),
                        Icon = placement[i].Icon, Placement = true });
                }
            }
            PropVerbs.Read(_mn, prop);
            IList<PropVerbs.Act> acts = PropVerbs.Current;
            for (int i = 0; i < acts.Count; i++)
            {
                KeptActs.Add(new KeptAct { Name = acts[i].Name ?? ("act" + acts[i].State), Label = acts[i].Label,
                    State = acts[i].State });
            }
        }

        // The dock for a thing, socket by socket (Forgejo #52): PURPOSE LEADS - a
        // wreck's Repair, Craft, the touch, vanilla's own acts - then the Inventory door
        // on the right. Move, Scale, Edit and Remove go to the card's Manage key.
        private static void ReadThingActs(ItemInfo prop, object focus, bool yours)
        {
            _unitRow.Clear();
            MoreActs.Clear();
            ManageActs.Clear();
            bool live = ReachRule.LiveProp(_mn, prop);
            bool building = ReachRule.LivePlacement(_mn, prop);   // Move · Remove · Repair: build range
            if (VanillaShowing(prop))
            {
                RememberThingActs(prop, yours);
            }
            else if (!ReferenceEquals(_keptActsFor, prop))
            {
                KeptActs.Clear();
                _keptActsFor = prop;
            }
            bool wreck = prop.lifeMax > 0f && prop.life <= 0f && prop.GetComponent<DefenceInfo>() != null;
            // A REPAIR CHANGES WHAT IT OFFERS: what vanilla lit while it was a wreck
            // was Repair alone, and kept for the same thing it stood in for the acts
            // after the repair - a repaired milker kept Repair where Show face goes -
            // until vanilla was asked again. Kept from the other side of a repair (or
            // a break), the placement acts are dropped and drawn by the rule below
            if (KeptActs.Exists(a => a.Placement) && KeptActs.Exists(a => a.Name == "RepairButton") != wreck)
            {
                KeptActs.RemoveAll(a => a.Placement);
            }
            // A THING OF YOURS ALWAYS HAS ITS PLACEMENT ACTS, opened by vanilla or
            // not (a Tab-picked milker had nothing on its dock):
            // every building has Move and Remove, and Repair alone once it breaks -
            // vanilla's own swap (UseProp), so the same rule draws them before it
            // has lit its panel, and a press opens it and presses through
            if (yours && !KeptActs.Exists(a => a.Placement))
            {
                string[] names = wreck ? new[] { "RepairButton" } : new[] { "MoveButton", "RemoveButton" };
                for (int i = 0; i < names.Length; i++)
                {
                    KeptActs.Insert(i, new KeptAct { Name = names[i], Label = PlacementWord(names[i]),
                        Icon = PropVerbs.PlacementIcon(_mn, names[i]), Placement = true });
                }
            }
            // THE WELL DRINKS AND THE FOOD BOX FEEDS, opened by vanilla or not:
            // tabbed from afar they showed no eat or drink buttons, up close they did.
            // Beyond reach Tab holds the thing as ours and vanilla is never asked, so
            // it has lit nothing to read. For these two its rule is unconditional
            // (UIManager.PropPanelTypeChange: Type.Water -> text 12, state 3;
            // Type.FoodStorage -> text 13, state 4), so the same rule draws them
            // before it has lit its panel: greyed until you are in reach, and a press
            // opens vanilla and presses through by the state (PressThingAct).
            // THE SAME FOR EVERY CONTAINER WHOSE ACT IS UNCONDITIONAL (Forgejo #60,
            // PredictActs): a pig farm Tabbed from afar never showed Make into
            // Meat, not even once you had walked up, since nothing asks vanilla on
            // the way. Yours only: vanilla lights a container's panel for its owner.
            if (!KeptActs.Exists(a => !a.Placement) && yours)
            {
                PredictActs(prop);
                PredictManage(prop);
            }
            int next = 0;
            for (int i = 0; i < KeptActs.Count; i++)
            {
                KeptAct act = KeptActs[i];
                if (!act.Placement)
                {
                    continue;
                }
                if (act.Name != "RepairButton")
                {
                    ManageActs.Add(act);
                    continue;
                }
                // A WRECK'S REPAIR LEADS: vanilla swaps Move and Remove for it, it is the
                // one thing to do and the reason you picked it - in the birth's pink
                // enamel and spark, until it is repaired and the Manage key is back
                _unitRow.Set(next++, new ActRow.Slot
                {
                    Name = act.Name,
                    Label = act.Label,
                    Icon = PlacementDockIcon(act.Name),
                    OwnColors = true,
                    Live = building,
                    Due = true,
                    Kind = ActRow.ActKind.Purpose,
                    Press = () => PressThingAct(prop, act),
                });
            }
            if (BenchPanel(prop) > 0)
            {
                _unitRow.Set(next++, new ActRow.Slot
                {
                    Name = "craft", Label = Shared.Loc.Get("inv.act.craft", "Craft"), Icon = HudGlyphs.Get("craft"), Live = live,
                    Kind = ActRow.ActKind.Purpose,
                    // a window's key, as a crafter's Craft is: drawn open while the
                    // crafting window is up, and pressed again it puts it away
                    Open = () => HudOpen("craft"),
                    Press = () => { if (HudOpen("craft")) _mn.uiMN.LeftPanelState(0); else OpenBenchCraft(prop); },
                });
            }
            string touch = VanillaShowing(prop) ? null : TouchWord(prop, yours);
            int touchAt = touch != null ? _unitRow.NextFree(next) : -1;
            if (touchAt >= 0)
            {
                _unitRow.Set(touchAt, new ActRow.Slot
                {
                    Name = TouchName(prop),
                    Label = touch,
                    Live = live,
                    Kind = ActRow.ActKind.Purpose,
                    Press = () => PressThingTouch(prop),
                });
                next = touchAt + 1;
            }
            for (int i = 0; i < KeptActs.Count; i++)
            {
                if (KeptActs[i].Placement)
                {
                    continue;
                }
                if (ManageState(KeptActs[i].State))
                {
                    ManageActs.Add(KeptActs[i]);
                    continue;
                }
                next = _unitRow.NextFree(next);
                if (next < 0)
                {
                    break;
                }
                KeptAct act = KeptActs[i];
                _unitRow.Set(next++, new ActRow.Slot
                {
                    Name = act.Name,
                    Label = act.Label,
                    Live = live,
                    Kind = ActRow.ActKind.Purpose,
                    Press = () => PressThingAct(prop, act),
                    Auto = AutoUse.SwitchFor(prop, act.State),   // Forgejo #119: Drink, Eat
                    Look = PurposeLook(prop, act.State),          // Forgejo #130: a glyph, not its initial
                    Due = act.State == 5,                         // Give Birth: due, as on a follower
                });
            }
            SortManage();
            if (focus is InventorySlot)
            {
                // nothing on the row before the door: the grid is all it is for, a
                // container's door - a bench (Craft), a wreck (Repair) are not
                bool onlyGrid = !_unitRow.Any;
                _unitRow.Set(ActRow.Sockets - 1, BagDoor(Shared.Loc.Get("inv.act.inventory", "Inventory"), !live,
                    container: onlyGrid, thing: true));
            }
            LayoutUnit();
            // a stranger's thing with nothing to do has no dock; a thing whose row is
            // empty and that has a Manage key is its card and its name, no plank
            _unitRow.Show(_unitRow.Any);
        }

        // What vanilla's panel lights for a container of yours whatever is in it,
        // by its Type (UIManager.PropPanelTypeChange: text id, then the state
        // PropAction switches on). Only the kinds Tab opens from reach - a press
        // opens vanilla there and presses through (PressThingAct) - and only for
        // the owner, as UseProp lights a container's panel for its owner alone.
        // Left to vanilla's own lighting: the acts that hang on who is in the
        // container (a pole, a toilet, a rack, a restraint, a milker), which it
        // reads off the container it has loaded, and the raft, whose use boards
        // it - a walk to touch the reach kinds do not have.
        private static void PredictActs(ItemInfo prop)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
            InventorySlot store = prop.GetComponent<InventorySlot>();
            if (store == null || !OpensFromReach(data))
            {
                return;
            }
            switch (store.type)
            {
                case InventorySlot.Type.Water: Predict(12, 3); break;
                case InventorySlot.Type.FoodStorage: Predict(13, 4); break;
                case InventorySlot.Type.FleshCollector: Predict(24, 11); break;
                case InventorySlot.Type.FactoryBreed: Predict(15, 7); break;
                case InventorySlot.Type.Bath: Predict(0, 12); break;
                case InventorySlot.Type.Block: Predict(0, 17); break;
                case InventorySlot.Type.Light: Predict(0, 20); break;
                case InventorySlot.Type.Capsule: Predict(30, 15); break;
                case InventorySlot.Type.Dye: Predict(31, 16); break;
                case InventorySlot.Type.BuildManager: Predict(0, 24); break;
                case InventorySlot.Type.Assault: Predict(0, 26); break;
                case InventorySlot.Type.Slave:
                    if (GameManager.selectPlayer == 1) { Predict(0, 10); }
                    break;
                case InventorySlot.Type.Bench:
                    if (prop.itemKey == "bench_workshop"
                        && SaveManager.GameModeStatic.gameMode == SaveManager.GameMode.Adventure)
                    {
                        Predict(38, 28);
                        Predict(39, 29);
                    }
                    break;
            }
        }

        private static void Predict(int textId, int state)
        {
            TextManager tm = GameWords.Text(_mn);
            string[] words = tm != null ? tm.actButtonTexts : null;
            if (words != null && textId < words.Length)
            {
                KeptActs.Add(new KeptAct { Name = "act" + state, Label = words[textId], State = state });
            }
        }

        private static string PlacementWord(string button)
        {
            return button == "MoveButton" ? Shared.Loc.Get("inv.act.move", "Move")
                : button == "RemoveButton" ? Shared.Loc.Get("inv.act.remove", "Remove")
                : button == "RepairButton" ? Shared.Loc.Get("inv.act.repair", "Repair") : button;
        }

        private static Sprite PlacementDockIcon(string button)
        {
            // Custom dock art; the underlying vanilla buttons and
            // their reach/repair logic are still the source of the actions.
            string glyph = button == "MoveButton" ? "move" : button == "RemoveButton" ? "remove" : "repair";
            return Theme.Kit("kit_emblem_" + glyph, 0);
        }

        // THE TOUCH (Forgejo #60). A bed, a play prop and an interactive one keep
        // vanilla's walk to touch (OpensFromReach), so Tab holds them without
        // asking vanilla - and whatever its touch does never came: a targeted bed
        // had Move and Remove and no way to lie in it. The dock offers the touch
        // itself. A press walks you up as a click on the thing does, and vanilla
        // uses it at contact (PlayerMove.InteractAction -> UseProp): a bed lays you
        // down (PlayerMove.Sleep, any bed, refused when someone is in it), the
        // others open their panel - and what vanilla lights there, a bed's sleep
        // till morning or night among them, joins the dock (RememberThingActs).
        // Offered only where the touch does something: any bed; a play prop of
        // yours whose panel has a Purpose act (UseProp lights it for its owner alone,
        // PlayLights for what it lights); an interactive
        // one whose container vanilla's panel has acts for - a house's body has
        // none, its door does (UIManager.PropPanelTypeChange). Not while vanilla
        // holds the thing: in the bed, the touch is done.
        private static string TouchWord(ItemInfo prop, bool yours)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
            if (data == null || data.itemType != ItemData.ItemType.Prop)
            {
                return null;
            }
            InventorySlot slot = prop.GetComponent<InventorySlot>();
            switch (data.subType)
            {
                case ItemData.SubType.Bed:
                    return Shared.Loc.Get("inv.act.sleep", "Sleep");
                case ItemData.SubType.Play:
                    return yours && PlayLights(prop) ? Shared.Loc.Get("inv.act.use", "Use") : null;
                case ItemData.SubType.Interact:
                    return slot != null && slot.type != InventorySlot.Type.None
                        ? Shared.Loc.Get("inv.act.use", "Use") : null;
                default:
                    return null;
            }
        }

        // Whether a play prop's panel has a Purpose act, by vanilla's own conditions
        // (UIManager.PropPanelTypeChange, the branch for a thing with no
        // container that is not a bed): one of the keys it names. Its size and its
        // place to edit are Manage acts, predicted for the Manage key (PredictManage)
        // with no walk. A decoration with none, or the delivery bed before the day,
        // lights nothing - a touch there would walk you up for nothing.
        private static bool PlayLights(ItemInfo prop)
        {
            switch (prop.itemKey)
            {
                case "deco_ball_beach_01":
                case "deco_ball_beach_01_suika":
                case "deco_signboard_01":
                case "deco_signboard_02":
                case "deco_signboard_03":
                case "deco_signboard_04":
                case "deco_tomb_01":
                case "flag_01":
                case "deco_fancy_melody_01":
                    return true;
                case "flag_02":
                    return !_mn.gameMN.ChangePlayerEventLimit();
                case "bed_delivery_01":
                    return GameManager.selectPlayer == 0 && _mn.sexMN != null
                        && _mn.sexMN.IsDueDate(_mn.gameMN.playerCommons[0]);
                default:
                    return false;
            }
        }

        private static string TouchName(ItemInfo prop)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
            return data != null && data.subType == ItemData.SubType.Bed ? "sleep" : "use";
        }

        // THE GLYPH A THING'S ACT WEARS (Forgejo #130): the common acts by the state
        // PropAction switches on, as auto-use knows them; ActRow.BoardGlyph draws the
        // look. The state names the act but for 1 and 2, which PropPanelTypeChange
        // gives by the container: a door's Peek and Enter, a toilet's and a chair's
        // Use, a pole's or a rack's Execute and the daruma machine's Startup. Give
        // Birth wears the tile a due follower's dock wears for it. The rest (the
        // dress-ups, Execute, Startup, Visit, the one-building acts, a mod's) keep
        // their initial.
        private static string PurposeLook(ItemInfo prop, int state)
        {
            switch (state)
            {
                case 3: return "drink";
                case 4: return "eat";
                case 5: return "delivery";
                case 7: return "meat";
                case 8: return "sleep_morning";
                case 9: return "sleep_night";
                case 10: case 12: case 17: case 20: case 21: case 22: case 24: case 26: case 30:
                    return "use";
                case 1: case 2:
                    InventorySlot store = prop != null ? prop.GetComponent<InventorySlot>() : null;
                    switch (store != null ? store.type : InventorySlot.Type.None)
                    {
                        case InventorySlot.Type.Door: return state == 1 ? "peek" : "enter";
                        case InventorySlot.Type.Toilet: return "use";
                        case InventorySlot.Type.BodyRack:
                            return _mn.itemMN != null && _mn.itemMN.IsChair(prop.itemKey) ? "use" : null;
                        default: return null;
                    }
                default: return null;
            }
        }

        // Vanilla's click, made from the dock: its target set, and its own walk
        // and its own use at contact. Refused while vanilla is busy, as a press
        // through it is (PressThingAct) - its hold on something else would end
        // by clearing the target this sets.
        private static void PressThingTouch(ItemInfo prop)
        {
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (prop == null || p == null || _mn.gameMN.nowAction || VanillaShowing(prop))
            {
                return;
            }
            p.target = prop.gameObject;
            Note("touch: walking to " + prop.itemKey);
            _unitBoundAt = 0f;
        }

        // Pressed through vanilla (D65). A thing kept past vanilla's end is opened
        // again first - UseProp's own opening runs before its first yield, so its
        // panels are lit by the time StartCoroutine returns - then the act is
        // re-read and pressed: vanilla swaps Move and Remove for Repair the moment
        // a thing breaks, so the remembered list is only the dock's picture.
        private static void PressThingAct(ItemInfo prop, KeptAct act)
        {
            if (prop == null || _mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return;
            }
            if (!VanillaShowing(prop))
            {
                ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
                if (data == null || _mn.gameMN.nowAction)
                {
                    return;
                }
                PlayerMove p = _mn.gameMN.pMove;
                p.target = prop.gameObject;
                p.StartCoroutine(p.UseProp(data));
            }
            if (act.Placement)
            {
                PropVerbs.ReadPlacement(_mn);
                IList<PropVerbs.Act> placement = PropVerbs.Placement;
                for (int i = 0; i < placement.Count; i++)
                {
                    if (placement[i].Name == act.Name)
                    {
                        PropVerbs.Press(placement[i]);
                        break;
                    }
                }
                if (act.Name == "RepairButton")
                {
                    _repairFor = prop.gameObject;
                    _repairPressedAt = Time.unscaledTime;
                    _repairBegan = false;
                }
            }
            else
            {
                PressPropAct(prop, act.State);
            }
            _unitBoundAt = 0f;
        }

        // A REPAIR ENDS WHERE YOU STAND: after a repair the player ran towards the
        // building by itself. Pressing Repair points the
        // game's own target at the building (above, or vanilla's click before it), and
        // UseProp's exit keeps that target while Repair was on offer (PlayerMove.cs:
        // `else if (!repairButton.activeSelf) target = null`). When the repair lets go
        // of the player, PlayerMove.InteractAction finds a target still set and walks
        // you up to it until you touch it, then opens it - which the dock, pressed from
        // up to a metre away, left you short of. So once the repair is over - it ran
        // and ended, or never began (no water) - the game's target on that building is
        // let go. A Tab target stays: it is held by us, not by the game's (D158); a
        // clicked one ends with the game's hold, as a clicked thing does.
        private static GameObject _repairFor;
        private static float _repairPressedAt;
        private static bool _repairBegan;
        private const float RepairStartWait = 0.5f;   // Repair lights toolAction in the press's own frame

        private static void TickRepairRelease()
        {
            if (_repairFor == null)
            {
                return;
            }
            PlayerMove p = _mn != null && _mn.gameMN != null ? _mn.gameMN.pMove : null;
            if (p == null)
            {
                _repairFor = null;
                return;
            }
            if (p.toolAction)
            {
                _repairBegan = true;
                return;
            }
            if (!_repairBegan && Time.unscaledTime - _repairPressedAt < RepairStartWait)
            {
                return;
            }
            GameObject building = _repairFor;
            _repairFor = null;
            GameObject held = p.target;
            if (held != null && (held == building || held.transform.parent == building.transform))
            {
                p.target = null;
                Note("repair over: the game's target let go of " + building.name);
            }
        }

        // How high the thing stands above its own origin, for the indicator to
        // float over it - a chest is knee-high, a tent is a house.
        private static float TopOf(ItemInfo prop)
        {
            float top = 1.5f;
            Renderer[] parts = prop.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && parts[i].enabled)
                {
                    top = Mathf.Max(top, parts[i].bounds.max.y - prop.transform.position.y);
                }
            }
            return Mathf.Min(top, 12f);
        }

        // The indicator over a thing: the person's plate, its name line, and up to
        // two readings as bars with their number (a thing's are few and fixed, so
        // they are said outright rather than one hover away).
        private static void PaintThingMarker(ItemInfo prop)
        {
            EnsureWorldMarker();
            ShowMarkerBars(false);
            PaintMarkerStatus(0f, false);
            _unitHead.gameObject.SetActive(false);
            _unitName.gameObject.SetActive(false);
            _unitKind.gameObject.SetActive(false);
            _unitAlertPlate.gameObject.SetActive(false);
            _unitStrip.Go.SetActive(false);
            _unitChevron.gameObject.SetActive(false);
            Camera camera = Camera.main;
            if (camera == null || prop == null)
            {
                _worldMarker.gameObject.SetActive(false);
                HideGroundRing();
                return;
            }
            // a door between you: the marker on that door (WorldMarker.Door.cs)
            bool behind = DoorBetween(prop.gameObject, out Vector3 door, out bool inside);
            Vector3 screen;
            if (behind)
            {
                HideGroundRing();
                screen = camera.WorldToScreenPoint(door + Vector3.up * DoorTagLift);
            }
            else
            {
                DoorTagOff();
                PaintGroundRing(prop.transform, EmblemThing, ThingRadius(prop));
                screen = camera.WorldToScreenPoint(prop.transform.position + Vector3.up * (_thingTop + 0.25f));
            }
            bool off = OffScreen(camera, behind ? door : prop.transform.position, ref screen);
            bool shown = !MenuOverMarker();
            _worldMarker.gameObject.SetActive(shown);
            if (!shown)
            {
                return;
            }
            int n = Mathf.Min(2, ThingLines.Count);
            _worldName.text = PropTitle(prop);
            bool mine = _mn.buildMN != null && _mn.buildMN.OwnCheck(prop);
            _worldSubtitle.text = n == 1 ? ThingLines[0].Value ?? string.Empty : mine ? Shared.Loc.Get("inv.thing.yours", "Yours") : Shared.Loc.Get("inv.thing.building", "Building");
            float height = n == 0 ? 33f : n == 1 ? 36f : 42f;
            MarkerCard(height);
            float width = MarkerWidth();
            PlaceWorldMarker(screen, width, height, behind ? DoorTagExtra : 0f, FarWords(off, prop.transform.position));
            if (behind)
            {
                PaintDoorTag(inside, width, height, 0f);
            }
            LayoutMarkerWords(width);
            PaintMarkerPlate(width, height);
            // the emblem: a chest's box or a building's hammer, brass when it is yours
            PaintEmblem(prop.GetComponent<InventorySlot>() != null ? "chest" : "craft",
                mine ? EmblemThing : EmblemTheirThing, 0, width, height);
            // the card shows the thing itself, as the target's card does: its item
            // art on a pale enamel, the gold or brown kept as the enamel's warmth
            ItemData art = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
            if (art != null && art.itemSprite != null)
            {
                _emblemFill.color = ThingEnamel(mine);
                if (_emblemGlyph.sprite != art.itemSprite) { _emblemGlyph.sprite = art.itemSprite; }
                _emblemGlyph.preserveAspect = true;
                _emblemGlyph.color = Color.white;
            }
            ShowThingBars(n);
            for (int i = 0; i < n; i++)
            {
                SheetLine line = ThingLines[i];
                float barW = width - EmblemPad - MarkerRightPad - (n > 1 ? 34f : 0f);
                PaintMarkerBar(ThingBack[i], ThingFill[i], EmblemPad, 27f + i * 7f, barW, 4.5f,
                    line.Frac, line.Tinted ? line.Tint : MarkerCream);
                if (ThingValue[i].text != (line.Value ?? string.Empty)) { ThingValue[i].text = line.Value ?? string.Empty; }
                ThingValue[i].gameObject.SetActive(n > 1);
                Theme.Place(ThingValue[i], width - MarkerRightPad - 32f, 24.5f + i * 7f, 32f, 9f);
            }
        }

        private static void ShowThingBars(int n)
        {
            for (int i = 0; i < ThingBack.Length; i++)
            {
                bool on = i < n;
                if (on && ThingBack[i] == null && _worldMarker != null)
                {
                    ThingBack[i] = MarkerTrack("ThingBar" + i, out ThingFill[i]);
                    ThingValue[i] = Theme.MakeKitText(_worldMarker.transform, "ThingValue" + i, 6f, Theme.KitCream,
                        TextAlignmentOptions.MidlineRight);
                    SetMarkerFont(ThingValue[i], false);
                    ThingValue[i].enableWordWrapping = false;
                    ThingValue[i].overflowMode = TextOverflowModes.Overflow;
                    ThingValue[i].raycastTarget = false;
                }
                if (ThingBack[i] != null && ThingBack[i].gameObject.activeSelf != on)
                {
                    ThingBack[i].gameObject.SetActive(on);
                    ThingValue[i].gameObject.SetActive(on);
                }
            }
        }

        private static void ResetThingForSceneLoad()
        {
            for (int i = 0; i < ThingBack.Length; i++)
            {
                ThingBack[i] = null;
                ThingFill[i] = null;
                ThingValue[i] = null;
            }
            _unitThing = null;
            ThingLines.Clear();
            KeptActs.Clear();
            _keptActsFor = null;
        }
    }
}
