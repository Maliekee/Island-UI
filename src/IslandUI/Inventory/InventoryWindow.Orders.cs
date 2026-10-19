using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The Orders aspect.
    internal static partial class InventoryWindow
    {
        // ---- the Orders aspect: theirs only, the standing settings about an NPC ----
        //
        // Vanilla's order tab, redistributed: combat role and job as dropdowns
        // (a radio per option would be seventeen rows), the communication
        // toggles as check chips, and the three buttons vanilla hangs under
        // the sheet - Return, Call, Banish - as planks. Labels are the game's
        // own, read off the buttons that call OrderChange / WorkChange /
        // NPCOptionChange, which also says which options exist (there is no
        // Fishing button, though WorkType has the value).

        private static string[] _combatLabels;
        private static string[] _workLabels;
        private static int[] _workValues;
        private static string[] _optionLabels;
        private static int[] _optionValues;
        private static Sprite[] _workIcons;
        private static readonly string[] CombatFallback = { "Aggressor", "Defender", "Survivor", "Guarder" };
        private static readonly string[] WorkFallback =
            { "None", "Food", "Log", "Mine", "Harvest", "Farm", "Livestock", "Excavate", "Train", "Fishing", "Repair" };
        private static readonly string[] OptionFallback = { "Enter/Exit house", "Allow love", "Allow sex" };
        private static readonly string[] ActionLabels = { "Return", "Call", "Banish" };

        private static void HarvestOrderLabels()
        {
            if (_combatLabels != null)
            {
                return;
            }
            Canvas root = _mn != null && _mn.uiMN != null && _mn.uiMN.itemDescPanel != null
                ? _mn.uiMN.itemDescPanel.GetComponentInParent<Canvas>() : null;
            root = root != null ? root.rootCanvas : null;
            int combatCount = System.Enum.GetValues(typeof(NPCMove.OrderType)).Length;
            _combatLabels = new string[combatCount];
            for (int i = 0; i < combatCount; i++)
            {
                _combatLabels[i] = ButtonLabel(root, "OrderChange", i)
                    ?? (i < CombatFallback.Length ? CombatFallback[i] : "Role " + i);
            }
            int workCount = System.Enum.GetValues(typeof(NPCMove.WorkType)).Length;
            var works = new List<int>();
            var workLabels = new List<string>();
            for (int i = 0; i < workCount; i++)
            {
                string label = ButtonLabel(root, "WorkChange", i);
                if (label == null && root != null)
                {
                    continue;   // no button for it: the game does not offer this job
                }
                works.Add(i);
                workLabels.Add(label ?? (i < WorkFallback.Length ? WorkFallback[i] : "Job " + i));
            }
            if (works.Count == 0)
            {
                for (int i = 0; i < WorkFallback.Length; i++) { works.Add(i); workLabels.Add(WorkFallback[i]); }
            }
            _workValues = works.ToArray();
            _workLabels = workLabels.ToArray();
            var opts = new List<int>();
            var optLabels = new List<string>();
            for (int i = 0; i < 11; i++)
            {
                string label = ButtonLabel(root, "NPCOptionChange", i);
                if (label == null)
                {
                    continue;
                }
                opts.Add(i);
                optLabels.Add(label);
            }
            if (opts.Count == 0)
            {
                for (int i = 0; i < OptionFallback.Length; i++) { opts.Add(i); optLabels.Add(OptionFallback[i]); }
            }
            _optionValues = opts.ToArray();
            _optionLabels = optLabels.ToArray();
        }

        // The localised text on the button that calls `method(arg)`, or null
        // when there is no such button
        private static string ButtonLabel(Canvas root, string method, int arg)
        {
            // Scoped to the STATUS panel: the NPC menu and the follower panel
            // carry buttons with the same calls, and the canvas-wide search
            // prefers whichever is live - which handed back a prefab-Japanese
            // label from a panel that never localises
            Transform scope = _mn != null && _mn.uiMN != null && _mn.uiMN.statusPanel != null
                ? _mn.uiMN.statusPanel.transform : null;
            if (scope == null)
            {
                return null;
            }
            GameObject button = Theme.FindByPersistentCall(scope, method, arg);
            if (button == null)
            {
                return null;
            }
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            string label = text != null ? LocalisedLabel(text.gameObject) : null;
            if (string.IsNullOrEmpty(label))
            {
                label = LocalisedLabel(button);
            }
            if (string.IsNullOrEmpty(label) && text != null)
            {
                label = text.text;
            }
            return string.IsNullOrEmpty(label) ? null : label.Trim();
        }

        // what the chosen stance and job do, in vanilla's own words - the
        // status panel's order and work tips (TextManager.orderTips / workTips,
        // shown by UIManager.OrderInfo / WorkInfo), which ship only in Japanese,
        // translated. By NPCMove.OrderType and NPCMove.WorkType; Fishing has no
        // entry in vanilla's list.
        private static readonly string[] CombatTips =
        {
            "<b>Aggressive</b>\nAttacks every enemy on sight.",
            "<b>Defensive</b>\nFights back when attacked, and does not chase.\nWhile following: attacks whoever is hostile to you.",
            "<b>Survival</b>\nDoes not fight. Takes no damage.",
            "<b>Guard</b>\nAttacks scouts and raiders on sight, and does not sleep at night.\nWhile following: as Aggressive.",
        };

        private static readonly string[] WorkTips =
        {
            "<b>No job</b>\nDoes no work. Libido recovers faster.",
            "<b>Food</b> (needs a food box)\nGathers food and stores it in the food box.",
            "<b>Wood</b> (needs a lumber yard)\nFells wood and stores it at the lumber yard.",
            "<b>Stone</b> (needs a quarry)\nMines stone and stores it at the quarry.",
            "<b>Gathering</b> (needs a harvest box)\nGathers branches, leaves, fibre and the like into the harvest box.",
            "<b>Farming</b> (needs planters and a harvest box)\nTends the planters nearby and puts the harvest in the harvest box.",
            "<b>Breeding</b> (needs a pig farm or pen, a harvest box, a milker)\nLooks after the breeding facilities nearby.",
            "<b>Digging</b> (needs a dig site)\nDigs up soil and stores it at the dig site.",
            "<b>Training</b> (needs a sandbag)\nTrains on the sandbag and gains experience.",
            null,
            "<b>Repair</b>\nRepairs damaged buildings, nearest their waiting place first.",
        };

        private static void WriteOrders(List<SheetLine> lines, CommonStates who)
        {
            HarvestOrderLabels();
            NPCMove move = who != null ? who.GetComponent<NPCMove>() : null;
            lines.Add(Head("Orders", string.Empty, VanillaTabIcon(3)));
            int combat = move != null ? (int)move.orderType : 0;
            lines.Add(new SheetLine { Label = "Combat", Kind = LineDropdown, Arg = 0,
                Options = _combatLabels, Index = Mathf.Clamp(combat, 0, _combatLabels.Length - 1), Enabled = true,
                Tip = combat >= 0 && combat < CombatTips.Length ? CombatTips[combat] : null });
            int work = move != null ? System.Array.IndexOf(_workValues, (int)move.workType) : 0;
            // the job's glyph, as the roster row wears it (FriendWorkCheck)
            if (_workIcons == null || _workIcons.Length != _workValues.Length)
            {
                _workIcons = new Sprite[_workValues.Length];
                for (int i = 0; i < _workValues.Length; i++)
                {
                    var type = (NPCMove.WorkType)_workValues[i];
                    _workIcons[i] = type != NPCMove.WorkType.None && _mn != null && _mn.uiMN != null
                        ? Emotion(_mn.uiMN.GetWorkImage(type)) : null;
                }
            }
            int job = move != null ? (int)move.workType : 0;
            lines.Add(new SheetLine { Label = "Job", Kind = LineDropdown, Arg = 1,
                Options = _workLabels, Icons = _workIcons, Index = Mathf.Max(0, work), Enabled = true,
                Tip = job >= 0 && job < WorkTips.Length ? WorkTips[job] : null });
            // THE SPEED LIMIT IS GONE from target NPCs. It was moved here from
            // Status as a standing order, which it is - but it cannot do anything. Vanilla's
            // slider bottoms out at 1.0 and a follower's base speed IS about 1.0,
            // so Clamp(speed, 0, limit) returns the same number at every position
            // the control can reach; and runRate multiplies AFTER the clamp, so a
            // running follower ignores whatever it allowed anyway. A control that
            // cannot change what it names does not belong on a sheet.
            //
            // The reading is kept in the harness as SpeedDump, so this is
            // re-checkable if a patch moves the slider's floor.
            WriteStanding(lines, who, move);
            WritePoses(lines, who, move);
            lines.Add(Head("Communication", string.Empty));
            for (int i = 0; i < _optionValues.Length; i++)
            {
                int id = _optionValues[i];
                // StatusChange case 3: the tenth option exists only for a
                // character the game has the animation for
                if (id == 10 && (_mn == null || _mn.sexMN == null || !_mn.sexMN.HasRapeAnimation(who)))
                {
                    continue;
                }
                bool allowed = who.sexInfo != null && id < who.sexInfo.Length && who.sexInfo[id] == 0;
                lines.Add(new SheetLine { Label = _optionLabels[i], Kind = LineOption, Arg = id,
                    On = allowed, Enabled = true, Tip = TipPermission(id) });
            }
            // EDIT DIALOGUE, under the flags that decide what they may do -
            // what someone may SAY is the same kind of standing fact. Drawn only
            // when vanilla lights it (it needs a TalkEdit component), read rather
            // than predicted, and wearing vanilla's own words (D65/D82).
            string editLabel = TalkEditLabel(who);
            if (editLabel != null)
            {
                lines.Add(new SheetLine { Kind = LineActions, Enabled = true,
                    Arg = ActionsTalkEdit, Options = new[] { editLabel }, Tips = new[] { TipEditDialogue } });
            }
            lines.Add(new SheetLine { Kind = LineSpace });
            bool canFire = _mn != null && _mn.npcMN != null && _mn.npcMN.FireNPCCheck(who.npcID);
            lines.Add(new SheetLine { Kind = LineActions, Enabled = true,
                Options = canFire ? ActionLabels : new[] { ActionLabels[0], ActionLabels[1] }, Tips = TipOrderActions });
        }

        // UIManager.OrderChange / WorkChange, on the sheet's carrier
        private static void ChooseOrder(Plate plate, int which, int index)
        {
            CommonStates who = plate != null ? plate.Owner : null;
            NPCMove move = who != null ? who.GetComponent<NPCMove>() : null;
            if (move == null)
            {
                return;
            }
            if (which == 0)
            {
                move.tmpEnemy = null;
                move.orderType = (NPCMove.OrderType)Mathf.Clamp(index, 0, _combatLabels.Length - 1);
            }
            else if (which == DropPose)
            {
                PosePress(who, index);
                return;      // pressed through vanilla's own button, which does the rest
            }
            else if (_workValues != null && index >= 0 && index < _workValues.Length)
            {
                move.workType = (NPCMove.WorkType)_workValues[index];
                if (_mn != null && _mn.uiMN != null)
                {
                    _mn.uiMN.FriendWorkCheck(who);
                }
            }
            if (_mn != null && _mn.uiMN != null && _mn.gameMN != null && _mn.gameMN.menuNPC == who)
            {
                _mn.uiMN.OrderColorCheck();   // vanilla's own tab, should it be open
            }
            _nextSheetPaint = 0f;
        }

        // Return, Call and Banish are vanilla coroutines that read menuNPC as
        // they run - Banish across a yes/no dialog - so rather than swap the
        // field around a call, the Focus is made explicit: this column IS the
        // character in focus, and menuNPC is where vanilla keeps that.
        private static void OrderAction(CommonStates who, int which)
        {
            if (who == null || _mn == null || _mn.npcMN == null || _mn.gameMN == null)
            {
                return;
            }
            if (_mn.gameMN.menuNPC != who)
            {
                _mn.gameMN.menuNPC = who;
            }
            switch (which)
            {
                case 0: _mn.npcMN.WarpToTerritory(); break;
                case 1: _mn.npcMN.WarpToPlayer(); break;
                case 2: _mn.npcMN.FireNPCButton(); break;
            }
        }

        // The Relations aspect, both. The one edge that decides what you may do
        // - theirs for you, yours for them - leads, with the verbs it unlocks;
        // then the graph, each person on the same two-band scale, the loved
        // above a rule and the hostile below it, capped; then the ledger.
    
        // ---------- WHERE THEY STAND ----------

        // D81 sends everything STANDING to Orders: where they wait, how far they
        // roam, how far they will chase. Vanilla keeps these on a panel of its own
        // (territoryPanel), and the three write NPCMove.territory, territoryRad
        // and chaseDist.
        //
        // WE DRIVE VANILLA'S OWN SETTERS rather than write the fields (D65).
        // NPCManager.SetTerritoryRadius and SetChase each read vanilla's slider,
        // resize the world circles AND write the field - so setting the slider and
        // calling them gets the preview for free and cannot drift from what
        // vanilla would have done.
        //
        // The preview is the point. A radius you cannot see is not a setting, and
        // the circles are drawn by MoveSetting, which runs only while
        // territoryPanel is active. So the panel is OPENED and hidden while these
        // rows are up (TerritoryPreview).
        // ONE slider row kind, several subjects: Arg says which. It was
        // hardcoded to the speed limit, which was fine while that was the only
        // slider on any sheet.
        private static void SetSheetSlider(Plate plate, SheetRow row, float frac)
        {
            if (plate == null || plate.Owner == null)
            {
                return;
            }
            SetStandingSlider(plate.Owner, row.Arg, frac);
        }

        private const int SliderTerritory = 1;
        private const int SliderChase = 2;

        private static void WriteStanding(List<SheetLine> lines, CommonStates who, NPCMove move)
        {
            if (move == null || _mn == null || _mn.uiMN == null
                || _mn.uiMN.territorySlider == null || _mn.uiMN.chaseSlider == null)
            {
                return;
            }
            // FOLLOW IS ONE TOGGLE, NOT TWO VERBS. Vanilla lights npcPanelState[5]
            // or [6] and never both, so the pair is already a single state wearing
            // two buttons - which is why it reads as a toggle here and pressed
            // through whichever of the two vanilla currently offers.
            lines.Add(Head("Where they stand", string.Empty));
            NPCMove nm = move;
            bool following = nm != null && nm.npcType == NPCMove.NPCType.Follow;
            lines.Add(new SheetLine
            {
                Label = "Following", Kind = LineToggle, Arg = ToggleFollow,
                Value = following ? "yes" : "no", On = following, Enabled = true, Tip = TipFollowing,
            });
            // a click that arms a world pointer: vanilla's own coroutine, which
            // takes the controls for the length of the pick and gives them back
            lines.Add(new SheetLine
            {
                Kind = LineActions, Enabled = true, Arg = ActionsStanding,
                Options = new[] { "Set place", "Go there" }, Tips = TipStanding,
            });
            Slider ter = _mn.uiMN.territorySlider;
            Slider chase = _mn.uiMN.chaseSlider;
            lines.Add(new SheetLine
            {
                Label = "Territory", Kind = LineSlider, Arg = SliderTerritory, Enabled = true, Tip = TipTerritory,
                Value = move.territoryRad.ToString("0.0"),
                Frac = Mathf.InverseLerp(ter.minValue, ter.maxValue, move.territoryRad),
            });
            lines.Add(new SheetLine
            {
                Label = "Combat area", Kind = LineSlider, Arg = SliderChase, Enabled = true, Tip = TipCombatArea,
                Value = move.chaseDist.ToString("0.0"),
                Frac = Mathf.InverseLerp(chase.minValue, chase.maxValue, move.chaseDist),
            });
            _wantTerritoryPreview = who;
        }

        // Set vanilla's slider, then call vanilla's handler. It resizes the circles
        // and writes the field; we do neither ourselves.
        private static void SetStandingSlider(CommonStates who, int which, float frac)
        {
            if (_mn == null || _mn.uiMN == null || _mn.npcMN == null || who == null)
            {
                return;
            }
            // both handlers work on gameMN.menuNPC, so the target has to BE that -
            // which it is whenever the column is showing them, and is checked here
            // rather than assumed, since a roster pick sets it and a walk-away clears it
            if (_mn.gameMN == null || !ReferenceEquals(_mn.gameMN.menuNPC, who))
            {
                return;
            }
            if (which == SliderTerritory)
            {
                Slider ter = _mn.uiMN.territorySlider;
                ter.SetValueWithoutNotify(Mathf.Lerp(ter.minValue, ter.maxValue, Mathf.Clamp01(frac)));
                _mn.npcMN.SetTerritoryRadius();
            }
            else
            {
                Slider chase = _mn.uiMN.chaseSlider;
                chase.SetValueWithoutNotify(Mathf.Lerp(chase.minValue, chase.maxValue, Mathf.Clamp01(frac)));
                _mn.npcMN.SetChase();
            }
            _nextSheetPaint = 0f;
        }

        // The two standing acts, through vanilla's own buttons.
        private const int ActionsStanding = 1;
        private const int ActionsTalkEdit = 3;

        private static void StandingAction(CommonStates who, int which)
        {
            if (_mn == null || _mn.npcMN == null || _mn.gameMN == null
                || !ReferenceEquals(_mn.gameMN.menuNPC, who))
            {
                return;
            }
            if (which == 0)
            {
                _mn.npcMN.SetTerritoryButton();     // arms the world pointer
            }
            else
            {
                _mn.npcMN.WarpToTerritory();
            }
        }

        // VANILLA'S PANEL, OPEN AND HIDDEN. MoveSetting draws the point and the two
        // circles and then idles `while (menuNPC != null && territoryPanel.activeSelf)`,
        // so the panel's ACTIVE flag is the preview's lifetime. We set that flag and
        // take the panel's alpha to zero - the same trick the container grid uses -
        // rather than reimplementing SetPoint and SetCircle.
        //
        // D71 forbids SUPPRESSING a vanilla panel and eating its input. This does
        // the opposite: it opens one that would otherwise be shut, and hides only
        // the widgets we have replaced. Nothing of vanilla's is prevented from
        // running - the coroutine is the whole point.
        private static CommonStates _wantTerritoryPreview;
        private static CanvasGroup _territoryVeil;
        private static CommonStates _previewFor;

        private static void SyncTerritoryPreview(ManagersScript mn)
        {
            GameObject panel = mn != null && mn.uiMN != null ? mn.uiMN.territoryPanel : null;
            if (panel == null)
            {
                return;
            }
            // THE WANT IS STICKY, NOT PER-FRAME. It is raised by WriteOrders, which
            // runs on a REBIND - about ten times a second - while this runs every
            // frame. Clearing it here made every intervening frame see "not
            // wanted", shut the panel, and end MoveSetting; the next rebind opened
            // it again. The circles blinked at the rebind rate, and looked right only
            // while a slider was being dragged, because dragging repaints.
            //
            // So the want is dropped when the STATE says it is over - the sheet is
            // no longer Orders, or the target has changed or gone - and not merely
            // because a frame went by without a bind.
            CommonStates want = _wantTerritoryPreview;
            if (want != null && (!_focusShown || _focusAspect != Aspect.Orders
                    || !ReferenceEquals(_target as CommonStates, want)))
            {
                want = null;
                _wantTerritoryPreview = null;
            }
            if (want != null && mn.gameMN != null && ReferenceEquals(mn.gameMN.menuNPC, want))
            {
                if (_territoryVeil == null)
                {
                    _territoryVeil = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
                }
                _territoryVeil.alpha = 0f;
                _territoryVeil.blocksRaycasts = false;
                _territoryVeil.interactable = false;
                if (!panel.activeSelf)
                {
                    panel.SetActive(true);
                }
                // one coroutine per target: MoveSetting exits on its own when the
                // panel closes or menuNPC changes
                if (!ReferenceEquals(_previewFor, want))
                {
                    _previewFor = want;
                    mn.npcMN.StartCoroutine(mn.npcMN.MoveSetting());
                }
                return;
            }
            if (_previewFor != null)
            {
                _previewFor = null;
                if (panel.activeSelf)
                {
                    panel.SetActive(false);   // MoveSetting sees this and clears its marks
                }
            }
        }

        // ---------- THE WAITING POSE ----------

        // THERE IS NO FIXED LIST (D82 in its purest form). OrderAnimPanelVisible
        // shows a pose only when `menuNPC.anim.skeleton.Data.FindAnimation(name)`
        // finds it, so which of the fifteen exist is a property of THAT
        // CHARACTER'S SKELETON, asked per pose per target. A grid drawn from the
        // enum would offer poses the body cannot strike.
        //
        // We ask the skeleton ourselves rather than calling
        // OrderAnimPanelVisible, which would activate vanilla's panel as a side
        // effect. The ART is still vanilla's: each pose's grid child carries the
        // sprite, and the child exists in the hierarchy whether or not the panel
        // is up - so it is read by NAME, like everything else here.
        private const string PoseGridPath =
            "MainCanvas/NPCPanel/position/TerritoryPanel/SetAnim/SetAnimPanel/grid";

        internal sealed class Pose
        {
            public string Name;        // the OrderAnim name, which is the object's name
            public string Label;       // vanilla's own words for it
            public UnityEngine.UI.Button Button;
        }

        private static readonly List<Pose> _poses = new List<Pose>();
        private static CommonStates _posesFor;

        // READ VANILLA'S GRID, not the enum. The first attempt walked
        // NPCManager.OrderAnim's fifteen names and looked each one up; the grid
        // actually holds THREE objects - None, A_walk and A_attack - so twelve of
        // them resolved to nothing. The enum is what the game COULD have; the grid
        // is what it built. Same mistake as keying a glyph on an index.
        //
        // They are WORD buttons, not icons: each child carries a label, and the
        // Image on the button is its plank. So they are drawn as words, and the
        // press goes through the child's own Button rather than through
        // OrderAnimSetButton with an id we worked out ourselves.
        //
        // Vanilla's skeleton test is still applied, because the grid is shared
        // between characters and its children are shown or hidden per target:
        // a pose exists for THIS body only if the skeleton has the animation.
        private static void ReadPoses(CommonStates who)
        {
            if (ReferenceEquals(_posesFor, who))
            {
                return;
            }
            _poses.Clear();
            _posesFor = who;
            if (who == null || _mn == null)
            {
                return;
            }
            GameObject stat = GameObject.Find("StaticGroup");
            Transform grid = stat != null ? stat.transform.Find(PoseGridPath) : null;
            if (grid == null)
            {
                return;
            }
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform t = grid.GetChild(i);
                bool has = t.name == "None";
                if (!has)
                {
                    try
                    {
                        has = who.anim != null && who.anim.skeleton != null
                            && who.anim.skeleton.Data.FindAnimation(t.name) != null;
                    }
                    catch (System.Exception) { has = false; }
                }
                if (!has)
                {
                    continue;
                }
                var text = t.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                _poses.Add(new Pose
                {
                    Name = t.name,
                    Label = text != null && !string.IsNullOrEmpty(text.text) ? text.text : t.name,
                    Button = t.GetComponent<UnityEngine.UI.Button>(),
                });
            }
        }

        private const int DropPose = 2;

        private static void WritePoses(List<SheetLine> lines, CommonStates who, NPCMove move)
        {
            ReadPoses(who);
            if (_poses.Count == 0 || move == null)
            {
                return;
            }
            // A DROPDOWN, NOT A GRID: a grid is a huge amount of buttons taking up
            // half the space of the window for a choice that is infrequently used,
            // so it belongs in something that pops up on demand.
            //
            // No new kit is needed: a dropdown IS that popup, it is already the
            // idiom for Combat and Job two rows above, and its open list lives on
            // the root above every plate it may fall over. A row of planks spends
            // the sheet's height on a choice that is made once and then read never;
            // one line spends a line.
            string current = move.orderAnim.ToString();
            var labels = new string[_poses.Count];
            int mark = 0;
            for (int i = 0; i < _poses.Count; i++)
            {
                labels[i] = _poses[i].Label;
                if (_poses[i].Name == current) { mark = i; }
            }
            lines.Add(new SheetLine
            {
                Label = "Waiting pose", Kind = LineDropdown, Arg = DropPose,
                Options = labels, Index = mark, Enabled = true, Tip = TipWaitingPose,
            });
            // ORIENTATION, AND THE COUPLING VANILLA'S LAYOUT HIDES: fixedRot is
            // applied only when `territoryRad == 0 && OnTerritory()`, so on anyone
            // with a territory the control writes a field that nothing reads. It is
            // drawn dim there rather than hidden, because the setting IS stored and
            // will take effect the moment the territory goes to zero.
            // VANILLA MARKS THE SET ORIENTATION and so must we: OrderAnimPanelVisible
            // colours one of the two off fixedRot, which is how you can tell the
            // press did anything at all. Without the mark the row looks inert even
            // when it worked.
            //
            // WHEN IT BITES is narrower than it looks. Idle() honours fixedRot only
            // under `territoryRad == 0 && tmpEnemy == null && npcType != Follow &&
            // OnTerritory()`, and OnTerritory is within 0.05 UNITS of the exact
            // territory point - standing on the spot, not near it. So a follower,
            // anyone with a radius, anyone in a fight, and anyone merely walking
            // back will not turn. The setting is stored regardless and applies the
            // moment they settle, which is why the row is drawn dim rather than
            // hidden.
            bool bites = move.territoryRad == 0f && move.npcType != NPCMove.NPCType.Follow;
            lines.Add(new SheetLine
            {
                Kind = LineActions, Arg = ActionsRot, Enabled = bites,
                Index = move.fixedRot == -1 ? 0 : move.fixedRot == 1 ? 1 : -1,
                Options = new[] { "Face left", "Face right" }, Tips = new[] { TipFacing, TipFacing },
            });
        }

        private const int ActionsRot = 2;

        // Vanilla's own button, including its clear-on-repress rule: pressing the
        // orientation already set puts it back to none (SetNPCRotButton).
        private static void RotAction(CommonStates who, int which)
        {
            if (_mn == null || _mn.uiMN == null || _mn.gameMN == null
                || !ReferenceEquals(_mn.gameMN.menuNPC, who))
            {
                return;
            }
            _mn.uiMN.SetNPCRotButton(which == 0 ? -1 : 1);
        }

        private static void PosePress(CommonStates who, int index)
        {
            if (_mn == null || _mn.gameMN == null
                || !ReferenceEquals(_mn.gameMN.menuNPC, who)
                || index < 0 || index >= _poses.Count)
            {
                return;
            }
            Pose p = _poses[index];
            if (p.Button != null)
            {
                p.Button.onClick.Invoke();   // vanilla's own handler (D65)
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // Pressed through whichever of vanilla's two buttons is live, so its
        // handler runs and the follower list is kept by the game rather than by us.
        private const int ToggleFollow = 90;

        // vanilla's own label for the talk-edit verb, or null when it does
        // not offer one for this target. The same read the follow toggle uses -
        // ask vanilla, take the answer, copy no condition of its own (D65).
        //
        // TWO THINGS THE READ TURNED UP, both about WHEN this can
        // appear at all:
        //
        // 1. `talkedit` is lit in NPCMenuOpen (UIManager:1627), not in
        //    NPCPanelStateChange - so it is in the npcID group with craft, vacuum
        //    and the shops, which TargetVerbs already records as readable only
        //    when vanilla's own panel is up. A WORLD CLICK sees it; a roster pick
        //    does not. That is the existing gap rather than a new one, and it
        //    fails the safe way: an absent line, never a wrong one.
        // 2. The component is added lazily (UIManager:4704, and SaveManager:2370
        //    restores it on load), so vanilla only lights the button for someone
        //    whose dialogue has ALREADY been authored. It is a re-edit door, and
        //    the first-edit door is somewhere else - not chased here.
        //
        // Verified negatively in game: a follower with no TalkEdit draws no line
        // and throws nothing. The positive case needs an NPC with authored
        // dialogue, which this save has none of.
        private static string TalkEditLabel(CommonStates who)
        {
            if (who == null || _mn == null)
            {
                return null;
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            var live = TargetVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Name == "talkedit")
                {
                    return string.IsNullOrEmpty(live[i].Label) ? "Edit dialogue" : live[i].Label;
                }
            }
            return null;
        }

        // Pressed through vanilla's own button, so its panel opens and its state
        // is the game's rather than ours.
        private static void TalkEditPress(CommonStates who)
        {
            if (who == null || _mn == null)
            {
                return;
            }
            if (_mn.gameMN != null && !ReferenceEquals(_mn.gameMN.menuNPC, who))
            {
                _mn.gameMN.menuNPC = who;   // vanilla's panel reads the focus as it opens
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            var live = TargetVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Name == "talkedit")
                {
                    TargetVerbs.Press(live[i]);
                    break;
                }
            }
        }

        private static void ToggleFollowing(CommonStates who)
        {
            if (who == null || _mn == null)
            {
                return;
            }
            TargetVerbs.ReadIncludingElsewhere(_mn, who);
            var live = TargetVerbs.Current;
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].Name == "follow" || live[i].Name == "goodbye")
                {
                    PressVerb(who, live[i]);
                    break;
                }
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
        }
}
}
