using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Sheet interaction: ticks, sliders, presses, stat allocation.
    internal static partial class InventoryWindow
    {
        // HOVER TIPS ON THE SHEETS (hovering them explains their purpose): the zones a form row recorded as it was painted, in its
        // own frame, or a plain row's Tip over the whole row. The relation, strip
        // and roster rows answer for themselves below.
        private static string RowTip(SheetRow row, Camera cam)
        {
            bool zones = row.TipZones.Count > 0;
            bool whole = !zones && !string.IsNullOrEmpty(row.Tip) && row.DrewKind != LineRelation
                && row.DrewKind != LineStrip && row.DrewKind != LineRoster;
            if (!zones && !whole)
            {
                return null;
            }
            var rect = (RectTransform)row.Go.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, cam, out Vector2 lp))
            {
                return null;
            }
            var at = new Vector2(lp.x - rect.rect.xMin, rect.rect.yMax - lp.y);
            if (whole)
            {
                return at.x >= 0f && at.x <= rect.rect.width && at.y >= 0f && at.y <= rect.rect.height ? row.Tip : null;
            }
            for (int z = 0; z < row.TipZones.Count; z++)
            {
                if (row.TipZones[z].Contains(at))
                {
                    return row.TipTexts[z];
                }
            }
            return null;
        }

        // For the harness: every tip the open sheets carry, with the screen
        // rect it answers in (top-left origin, as a screenshot reads)
        internal static string SheetTipDump()
        {
            var sb = new System.Text.StringBuilder();
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                Plate plate = SheetPlates[p];
                if (!plate.IsSheet || plate.Rows == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < plate.RowsShown && i < plate.Rows.Count; i++)
                {
                    SheetRow row = plate.Rows[i];
                    if (!row.Go.activeInHierarchy)
                    {
                        continue;
                    }
                    var rect = (RectTransform)row.Go.transform;
                    Canvas canvas = rect.GetComponentInParent<Canvas>();
                    Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    int zones = row.TipZones.Count;
                    for (int z = 0; z < Mathf.Max(zones, 1); z++)
                    {
                        Rect r = zones > 0 ? row.TipZones[z] : new Rect(0f, 0f, rect.rect.width, rect.rect.height);
                        string tip = zones > 0 ? row.TipTexts[z] : row.Tip;
                        if (string.IsNullOrEmpty(tip))
                        {
                            continue;
                        }
                        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam,
                            rect.TransformPoint(new Vector3(rect.rect.xMin + r.xMin, rect.rect.yMax - r.yMin, 0f)));
                        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam,
                            rect.TransformPoint(new Vector3(rect.rect.xMin + r.xMax, rect.rect.yMax - r.yMax, 0f)));
                        int cut = tip.IndexOf('\n');
                        sb.Append((int)a.x).Append(',').Append(Screen.height - (int)a.y).Append(" - ")
                            .Append((int)b.x).Append(',').Append(Screen.height - (int)b.y).Append(": ")
                            .Append(cut > 0 ? tip.Substring(0, cut) : tip).Append('\n');
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no tips on the open sheets";
        }

        // Returns true while the pointer is over a sheet control, or a slider
        // is being dragged, so the cells and buttons beneath stay untouched.
        private static bool TickSheets(Camera cam)
        {
            SheetRow hot = null;
            Plate hotPlate = null;
            int hotSub = -1;
            bool overDrop = false;
            string tip = null;      // a relation bar under the pointer shows its numbers
            for (int p = 0; p < SheetPlates.Count && hot == null; p++)
            {
                Plate plate = SheetPlates[p];
                if (!plate.IsSheet || plate.Rows == null || !plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                // ...and a roster row scrolled up behind the control bar is
                // MASKED, not gone: its rect is still where it was, and this scan
                // is rect arithmetic. The cell scan has taken a clip rect since it
                // was written (ScanPlates); this one never needed one until a list
                // long enough to scroll arrived under it.
                if (ClippedAway(plate, cam))
                {
                    continue;
                }
                for (int i = 0; i < plate.RowsShown && i < plate.Rows.Count; i++)
                {
                    SheetRow row = plate.Rows[i];
                    if (!row.Go.activeInHierarchy)
                    {
                        continue;
                    }
                    // a form row's parts, or any other row's tip over the whole row
                    if (tip == null)
                    {
                        tip = RowTip(row, cam);
                    }
                    // the dropdown polls itself; its open list may cover other rows,
                    // so it goes first and claims the pointer while it is over it
                    if (row.DrewKind == LineDropdown && row.Drop != null)
                    {
                        if (row.Drop.Tick(cam))
                        {
                            overDrop = true;
                            hot = null;
                            break;
                        }
                        continue;
                    }
                    // the header run: each icon answers for itself, and the two
                    // bars for the pair
                    if (row.DrewKind == LineStrip)
                    {
                        if (tip == null && row.IconTips != null && row.IconBacks != null)
                        {
                            for (int k = 0; k < row.IconBacks.Length && k < row.IconTips.Length; k++)
                            {
                                if (row.IconBacks[k].gameObject.activeInHierarchy
                                    && !string.IsNullOrEmpty(row.IconTips[k])
                                    && RectTransformUtility.RectangleContainsScreenPoint(
                                        row.IconBacks[k].rectTransform, Input.mousePosition, cam))
                                {
                                    tip = row.IconTips[k];
                                    break;
                                }
                            }
                        }
                        if (tip == null && row.BarRim != null && row.BarRim.gameObject.activeInHierarchy
                            && RectTransformUtility.RectangleContainsScreenPoint(
                                row.BarRim.rectTransform, Input.mousePosition, cam))
                        {
                            tip = row.BarTip;
                        }
                        continue;
                    }
                    if (row.DrewKind == LineRelation)
                    {
                        if (tip == null && row.Love != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                (RectTransform)row.Go.transform, Input.mousePosition, cam, out Vector2 rl)
                            && rl.x >= row.Love.TrackX - 4f && rl.x <= row.Love.TrackX + row.Love.TrackW + 4f
                            && rl.y <= 0f && rl.y >= -LoveWidget.RowH)
                        {
                            tip = row.Tip;
                        }
                        continue;
                    }
                    // the whole roster row is the target - it is one thing
                    // that does one thing (D53), so there is nothing smaller to aim at
                    if (row.DrewKind == LineRoster)
                    {
                        // ...but the card is the head of the column you are in, so
                        // pressing it would "open" what is already open. It reads as
                        // a row and answers as a heading.
                        if (row.DrewCard)
                        {
                            continue;
                        }
                        if (row.Hover != null && RectTransformUtility.RectangleContainsScreenPoint(
                                row.Hover.rectTransform, Input.mousePosition, cam))
                        {
                            hot = row; hotPlate = plate;
                            break;
                        }
                        continue;
                    }
                    if (row.DrewKind == LineActions || row.DrewKind == LineActs
                        || row.DrewKind == LinePlace || row.DrewKind == LinePose
                        || row.DrewKind == LineMore || row.DrewKind == LineRosterBar
                        || row.DrewKind == LineBoxes || row.DrewKind == LineChecks
                        || row.DrewKind == LineMoodCause)
                    {
                        for (int k = 0; row.Planks != null && k < row.Planks.Length; k++)
                        {
                            if (row.Planks[k].gameObject.activeInHierarchy && RectTransformUtility
                                    .RectangleContainsScreenPoint(row.Planks[k].rectTransform, Input.mousePosition, cam))
                            {
                                hot = row; hotPlate = plate; hotSub = k;
                                break;
                            }
                        }
                        if (hot != null)
                        {
                            break;
                        }
                        continue;
                    }
                    RectTransform target = row.DrewKind == LineSlider ? row.Track.rectTransform
                        : row.DrewKind == LineStat || row.DrewKind == LineToggle || row.DrewKind == LineSkill
                            || row.DrewKind == LineOption
                            ? row.Chip.rectTransform
                        : null;
                    if (target == null || !target.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    // the knob is small; the whole row's height counts for a slider
                    if (row.DrewKind == LineSlider)
                    {
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                (RectTransform)row.Go.transform, Input.mousePosition, cam, out Vector2 local)
                            && local.x >= row.TrackX - SheetKnob && local.x <= row.TrackX + row.TrackW + SheetKnob
                            && local.y <= 0f && local.y >= -SheetRowH)
                        {
                            hot = row; hotPlate = plate; break;
                        }
                        continue;
                    }
                    if (RectTransformUtility.RectangleContainsScreenPoint(target, Input.mousePosition, cam))
                    {
                        hot = row; hotPlate = plate; break;
                    }
                }
            }
            LoveTipTick(tip, cam);
            if (!ReferenceEquals(hot, _sheetHot) || hotSub != _sheetHotSub
                || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                _nextSheetPaint = 0f;   // hover and press read on the next tick
            }
            _sheetHot = hot;
            _sheetHotPlate = hotPlate;
            _sheetHotSub = hotSub;
            if (Input.GetMouseButtonDown(0) && hot != null)
            {
                _sheetPressed = hot;
                _sheetPressedSub = hotSub;
            }
            bool dragging = _sheetPressed != null && _sheetPressed.DrewKind == LineSlider
                && Input.GetMouseButton(0);
            if (dragging)
            {
                DragSlider(_sheetPressed);
            }
            if (Input.GetMouseButtonUp(0))
            {
                SheetRow pressed = _sheetPressed;
                int pressedSub = _sheetPressedSub;
                _sheetPressed = null;
                _sheetPressedSub = -1;
                // The Owner gate: most pressable rows act ON a carrier. Two do
                // not - the roster's control row, which has none, and a PROP
                // sheet, whose subject is a thing. The prop case was the
                // second null-Owner assumption this work found; the first was in
                // RepaintSheets, and both date from when every sheet was about a
                // person.
                if (pressed != null && ReferenceEquals(pressed, hot) && pressedSub == hotSub
                    && hotPlate != null && pressed.DrewKind != LineSlider
                    && (hotPlate.Owner != null || hotPlate.Prop != null
                        || pressed.DrewKind == LineRosterBar))
                {
                    PressSheetRow(hotPlate, pressed, pressedSub);
                }
            }
            else if (_sheetPressed != null && !Input.GetMouseButton(0))
            {
                _sheetPressed = null;
                _sheetPressedSub = -1;
            }
            return hot != null || dragging || overDrop;
        }

        private static void DragSlider(SheetRow row)
        {
            Plate plate = null;
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                if (SheetPlates[p].Rows != null && SheetPlates[p].Rows.Contains(row))
                {
                    plate = SheetPlates[p];
                    break;
                }
            }
            if (plate == null || plate.Owner == null)
            {
                return;
            }
            Canvas canvas = _root.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)row.Go.transform, Input.mousePosition, cam, out Vector2 local))
            {
                return;
            }
            float frac = Mathf.Clamp01((local.x - row.TrackX) / Mathf.Max(1f, row.TrackW));
            SetSheetSlider(plate, row, frac);
        }

        // For the harness (tests/scenarios): act on a sheet row by its index, as a
        // click on its chip would, or set a slider to `frac`. The controls are
        // polled, so /ui/click cannot reach them.
        internal static bool SheetAct(int group, int index, float frac)
        {
            for (int p = 0; p < SheetPlates.Count; p++)
            {
                Plate plate = SheetPlates[p];
                if (plate.Group != group || plate.Rows == null || index < 0 || index >= plate.RowsShown)
                {
                    continue;
                }
                SheetRow row = plate.Rows[index];
                if (row.DrewKind == LineSlider)
                {
                    SetSheetSlider(plate, row, Mathf.Clamp01(frac));
                    return true;
                }
                if (row.DrewKind == LineDropdown)
                {
                    ChooseOrder(plate, row.Arg, Mathf.RoundToInt(frac));   // frac carries the choice
                    return true;
                }
                if (row.DrewKind == LineStat || row.DrewKind == LineToggle || row.DrewKind == LineSkill
                    || row.DrewKind == LineOption || row.DrewKind == LineActions
                    || row.DrewKind == LineActs || row.DrewKind == LinePlace
                    || row.DrewKind == LinePose || row.DrewKind == LineMore
                    || row.DrewKind == LineBoxes || row.DrewKind == LineChecks)
                {
                    PressSheetRow(plate, row, Mathf.RoundToInt(frac));   // ...or the plank
                    return true;
                }
            }
            return false;
        }

        internal static int SheetToggleRow(int group, int action)
        {
            foreach (var plate in SheetPlates)
            {
                if (plate.Group != group || plate.Rows == null || !plate.Window.Go.activeInHierarchy) continue;
                for (int i = 0; i < plate.RowsShown; i++)
                    if (plate.Rows[i].DrewKind == LineToggle && plate.Rows[i].Arg == action) return i;
            }
            return -1;
        }

        private static void PressSheetRow(Plate plate, SheetRow row, int sub = -1)
        {
            CommonStates who = plate.Owner;
            switch (row.DrewKind)
            {
                case LineOption:
                    // UIManager.NPCOptionChange, on this carrier: 0 is ALLOWED
                    if (who.sexInfo != null && row.Arg >= 0 && row.Arg < who.sexInfo.Length)
                    {
                        who.sexInfo[row.Arg] = who.sexInfo[row.Arg] == 0 ? 1 : 0;
                    }
                    break;
                case LineActions:
                    if (row.Arg == ActionsStanding)
                    {
                        StandingAction(who, sub);
                    }
                    else if (row.Arg == ActionsRot)
                    {
                        RotAction(who, sub);
                    }
                    else if (row.Arg == ActionsTalkEdit)
                    {
                        TalkEditPress(who);
                    }
                    else if (row.Arg == ActionsMood)
                    {
                        MoodAction(sub);
                    }
                    else
                    {
                        OrderAction(who, sub);
                    }
                    break;
                case LinePlace:
                    PlacePress(row.Arg + sub);
                    break;
                case LinePose:
                    PosePress(who, row.Arg + sub);
                    break;
                case LineActs:
                    // a thing's acts and a person's are read from different panels
                    if (plate.Prop != null)
                    {
                        PropActPress(plate.Prop, row.Arg + sub);
                    }
                    else
                    {
                        ActPress(who, row.Arg + sub);
                    }
                    break;
                case LineMore:
                    MatrixOpen(plate.Owner, plate.Group != GroupNone && IsDossier(plate.Group));
                    return;
                case LineRoster:
                    PressRosterRow(who);
                    return;   // the opener rebinds on its own terms
                case LineRosterBar:
                    PressRosterBar(sub);
                    break;
                case LineStat:
                    AllocateStat(who, row.Arg);
                    break;
                case LineSkill:
                    RaiseSkill(row.Arg);
                    break;
                case LineToggle:
                    PressToggle(who, row.Arg);
                    break;
                case LineBoxes:
                case LineChecks:
                    // the printed form's boxes and checkboxes: which one is the sub
                    PressForm(who, row, sub);
                    break;
                case LineMoodCause:
                    MoodAction(row.Arg);   // the cause's one fix
                    break;
            }
            _forceRefresh = true;
            _nextRefresh = 0f;
            Refresh();
        }

        private static void PressToggle(CommonStates who, int which)
        {
            if (which == 91)
            {
                _mn.gameMN.ToggleRunChange();
            }
            else if (which == ToggleFollow)
            {
                ToggleFollowing(who);
            }
            else
            {
                ToggleMark(who, which);
            }
        }

        // SkillManager.StatusUp, on the sheet's own carrier: the vanilla method
        // resolves menuNPC ?? player, which is the one thing a column may never
        // do (D8). Same costs, same gains, same Shift and Ctrl multipliers.
        private static void AllocateStat(CommonStates who, int id)
        {
            if (who == null || who.status == null || id < 0 || id >= who.status.Length
                || _mn == null || _mn.npcMN == null)
            {
                return;
            }
            int times = 1;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                times = Input.GetKey(KeyCode.LeftControl) ? 100 : 10;
            }
            if (id == 2 && who.speed <= 0f)
            {
                if (_mn.eventMN != null)
                {
                    _mn.StartCoroutine(_mn.eventMN.GoCaution(51));
                }
                return;
            }
            StatsData stats = _mn.npcMN.GetStatsData(who.npcID);
            if (stats == null)
            {
                Plugin.Log.LogWarning("Sheet: no StatsData for npcID " + who.npcID);
                return;
            }
            for (int i = 0; i < times; i++)
            {
                int cost = StatCost(who, id);
                if (who.statusPoint < cost)
                {
                    break;
                }
                who.statusPoint -= cost;
                who.status[id]++;
                switch (id)
                {
                    case 0:
                        who.maxLife += stats.upLife;
                        if (_mn.gameMN != null && who.gameObject == _mn.gameMN.player)
                        {
                            _mn.gameMN.LifeImageChange();
                        }
                        break;
                    case 1:
                        who.attack += stats.upAttack;
                        break;
                    case 2:
                        who.speed += 0.005f;
                        break;
                }
            }
            if (_mn.uiMN != null)
            {
                _mn.uiMN.StatusChange(who);   // vanilla's sheet, should it be open
            }
        }

        private static readonly string[] StatNames = { "Health", "Strength", "Speed" };

        // UIManager.StatusChange's guard: the shield's own value when one is
        // worn, else 25 with a weapon in hand and 10 without.
        private static float GuardOf(CommonStates who)
        {
            if (who.equip == null || who.equip.Length <= 6 || _mn == null || _mn.itemMN == null)
            {
                return 0f;
            }
            ItemSlot off = who.equip[6];
            ItemData data = off != null && !string.IsNullOrEmpty(off.itemKey)
                ? _mn.itemMN.FindItem(off.itemKey) : null;
            if (data != null && data.itemType == ItemData.ItemType.Shield)
            {
                return off.attack;
            }
            ItemSlot hand = who.equip.Length > 1 ? who.equip[1] : null;
            return hand == null || string.IsNullOrEmpty(hand.itemKey) ? 10f : 25f;
        }
    }
}
