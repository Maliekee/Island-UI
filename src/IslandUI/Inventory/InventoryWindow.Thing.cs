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
    //   - the DOCK holds Move, Remove and Repair in the enamel icon family, Craft on a
    //     bench, the acts vanilla lit for it (Drink, Read, Feed), and on a container
    //     the Inventory door - the person's Bag door, one standing choice for both;
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
            string text = PropTitle(prop) + (yours ? "  ·  yours" : string.Empty);
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

        // The dock for a thing, socket by socket: the building's own three, Craft,
        // vanilla's acts, and the Inventory door on the right.
        private static void ReadThingActs(ItemInfo prop, object focus, bool yours)
        {
            _unitRow.Clear();
            MoreActs.Clear();
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
            // A THING OF YOURS ALWAYS HAS ITS PLACEMENT ACTS, opened by vanilla or
            // not (a Tab-picked milker had nothing on its dock):
            // every building has Move and Remove, and Repair alone once it breaks -
            // vanilla's own swap (UseProp), so the same rule draws them before it
            // has lit its panel, and a press opens it and presses through
            if (yours && !KeptActs.Exists(a => a.Placement))
            {
                bool wreck = prop.lifeMax > 0f && prop.life <= 0f && prop.GetComponent<DefenceInfo>() != null;
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
            if (!KeptActs.Exists(a => !a.Placement))
            {
                InventorySlot store = prop.GetComponent<InventorySlot>();
                int textId = store == null ? -1
                    : store.type == InventorySlot.Type.Water ? 12
                    : store.type == InventorySlot.Type.FoodStorage ? 13 : -1;
                string[] words = _mn.textMN != null ? _mn.textMN.actButtonTexts : null;
                if (textId >= 0 && words != null && textId < words.Length)
                {
                    int state = textId == 12 ? 3 : 4;
                    KeptActs.Add(new KeptAct { Name = "act" + state, Label = words[textId], State = state });
                }
            }
            int next = 0;
            for (int i = 0; i < KeptActs.Count; i++)
            {
                if (!KeptActs[i].Placement)
                {
                    continue;
                }
                KeptAct act = KeptActs[i];
                _unitRow.Set(next++, new ActRow.Slot
                {
                    Name = act.Name,
                    Label = act.Label,
                    Icon = PlacementDockIcon(act.Name),
                    OwnColors = true,
                    Live = building,
                    Press = () => PressThingAct(prop, act),
                });
            }
            if (BenchPanel(prop) > 0)
            {
                _unitRow.Set(next++, new ActRow.Slot
                {
                    Name = "craft", Label = "Craft", Icon = HudGlyphs.Get("craft"), Live = live,
                    Press = () => OpenBenchCraft(prop),
                });
            }
            for (int i = 0; i < KeptActs.Count; i++)
            {
                if (KeptActs[i].Placement)
                {
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
                    Press = () => PressThingAct(prop, act),
                });
            }
            if (focus is InventorySlot)
            {
                _unitRow.Set(ActRow.Sockets - 2, BagDoor("Inventory", !live));
            }
            LayoutUnit();
            // a stranger's thing with nothing to do has no dock
            _unitRow.Show(_unitRow.Any);
        }

        private static string PlacementWord(string button)
        {
            return button == "MoveButton" ? "Move" : button == "RemoveButton" ? "Remove"
                : button == "RepairButton" ? "Repair" : button;
        }

        private static Sprite PlacementDockIcon(string button)
        {
            // Custom dock art; the underlying vanilla buttons and
            // their reach/repair logic are still the source of the actions.
            string glyph = button == "MoveButton" ? "move" : button == "RemoveButton" ? "remove" : "repair";
            return Theme.Kit("kit_emblem_" + glyph, 0);
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
            PaintGroundRing(prop.transform, EmblemThing, ThingRadius(prop));
            Vector3 screen = camera.WorldToScreenPoint(prop.transform.position + Vector3.up * (_thingTop + 0.25f));
            bool visible = screen.z > 0;
            _worldMarker.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            int n = Mathf.Min(2, ThingLines.Count);
            _worldName.text = PropTitle(prop);
            bool mine = _mn.buildMN != null && _mn.buildMN.OwnCheck(prop);
            _worldSubtitle.text = n == 1 ? ThingLines[0].Value ?? string.Empty : mine ? "Yours" : "Building";
            float width = MarkerWidth();
            float height = n == 0 ? 29f : n == 1 ? 35f : 42f;
            PlaceWorldMarker(screen, width, height, false);
            LayoutMarkerWords(width, true);
            _worldMarker.enabled = true;
            // the emblem: a chest's box or a building's hammer, brass when it is yours
            PaintEmblem(prop.GetComponent<InventorySlot>() != null ? "chest" : "craft",
                mine ? EmblemThing : EmblemTheirThing, 0, width, height, false);
            ShowThingBars(n);
            for (int i = 0; i < n; i++)
            {
                SheetLine line = ThingLines[i];
                float barW = MarkerBarWidth - (n > 1 ? 34f : 0f);
                PaintMarkerBar(ThingBack[i], ThingFill[i], EmblemPad, 26f + i * 7f, barW, 4.5f,
                    line.Frac, line.Tinted ? line.Tint : MarkerCream);
                if (ThingValue[i].text != (line.Value ?? string.Empty)) { ThingValue[i].text = line.Value ?? string.Empty; }
                ThingValue[i].gameObject.SetActive(n > 1);
                Theme.Place(ThingValue[i], MarkerMinWidth - MarkerRightPad - 32f, 23.5f + i * 7f, 32f, 9f);
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
