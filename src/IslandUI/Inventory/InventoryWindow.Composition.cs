using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private const float SheetScrollGutter = 8f;
        private static readonly float[] SheetOffsets = new float[GroupCount];
        private static readonly object[] SheetOwners = new object[GroupCount];
        private static readonly Aspect[] SheetPages = new Aspect[GroupCount];
        private static Plate _sheetScrollDrag;
        private static float _sheetDragGrab;

        private static string SheetTabLabel(Aspect aspect)
        {
            switch (aspect)
            {
                case Aspect.Profile: return "Character";
                case Aspect.Relations: return "Social";
                default: return aspect.ToString();
            }
        }

        private static void FitSheetViewport(Plate plate, int group, Aspect aspect)
        {
            if (plate.Owner == null || plate.IsHeader) { return; }
            plate.SheetContentH = plate.Height;
            plate.SheetScrollGroup = group;
            if (!ReferenceEquals(SheetOwners[group], plate.Owner) || SheetPages[group] != aspect)
            {
                SheetOffsets[group] = 0f;
                SheetOwners[group] = plate.Owner;
                SheetPages[group] = aspect;
                _sheetScrollDrag = null;
            }
            if (plate.SheetMask == null) { plate.SheetMask = plate.Window.Go.AddComponent<RectMask2D>(); }
            plate.SheetMask.enabled = true;
            if (plate.SheetTrack == null)
            {
                plate.SheetTrack = Theme.KitImage(plate.Window.Go.transform, "SheetScroll", "kit_plank_walnut", 18, Color.white);
                plate.SheetTrack.raycastTarget = true;
                plate.SheetThumb = Theme.KitImage(plate.SheetTrack.transform, "Thumb", "kit_plank_pine", 18, Color.white);
                plate.SheetThumb.raycastTarget = true;
            }
        }

        // Run after bags have their final bounds. Content gets only the remaining
        // space; the bag and the sheet's top rail never move when a page changes.
        private static void LayoutSheetGroups()
        {
            PlaceSheetGroup(GroupOwnSheet, GroupOwn, OwnColumn, OwnSheetTop());
            if (Focus() is CommonStates)
            {
                float top = UnitHang();
                if (float.IsNaN(top)) { top = OwnSheetTop(); }
                PlaceSheetGroup(GroupCharacter, GroupBag, CharacterColumn, top);
            }
        }

        private static float OwnSheetTop()
        {
            return Mathf.Min(360f, _root != null ? ((RectTransform)_root.transform).rect.height - 30f : 360f);
        }

        private static void PlaceSheetGroup(int group, int bagGroup, int column, float top)
        {
            if (_groupMembers[group] == 0) { return; }
            float pad = GroupPadY();
            float floor = Mathf.Max(MarginY, _groupHi[bagGroup])
                + PlateGap + CloseChipSize * 0.5f;
            // THE TARGET'S SHEET CLEARS THE UNIT, bag or no bag. With the bag
            // window shut its floor was the screen's margin, so a page taller
            // than the room above the unit hung down over the act row. It
            // scrolls instead.
            if (group == GroupCharacter)
            {
                floor = Mathf.Max(floor, UnitStanding() + PlateGap + CloseChipSize * 0.5f);
            }
            float other = 0f;
            Plate sheet = null;
            for (int i = 0; i < _platesUsed; i++)
            {
                Plate p = Plates[i];
                if (p.Group != group || p.IsGroupFrame) { continue; }
                if (p.SheetContentH > 0f) { sheet = p; }
                else { other += p.Height + GroupMemberGap; }
            }
            if (sheet == null) { return; }
            top = Mathf.Max(top, floor + 2f * pad + other + SheetRowH * 2f);
            float available = Mathf.Max(SheetRowH, top - floor - 2f * pad - other);
            sheet.Height = Mathf.Min(sheet.SheetContentH, available);
            sheet.Window.Rect.sizeDelta = new Vector2(sheet.Window.Rect.sizeDelta.x, sheet.Height);
            sheet.LaidOutH = sheet.Height;
            float y = top - pad;
            for (int i = _platesUsed - 1; i >= 0; i--)
            {
                Plate p = Plates[i];
                if (p.Group != group || p.IsGroupFrame) { continue; }
                y -= p.Height;
                Vector2 at = p.Window.Rect.anchoredPosition;
                p.Window.Rect.anchoredPosition = new Vector2(at.x, y);
                y -= GroupMemberGap;
            }
            _groupHi[group] = top;
            _groupLo[group] = y + GroupMemberGap - pad;
            _colY[column] = Mathf.Max(_colY[column], top);
            ApplySheetScroll(sheet);
        }

        private static void ApplySheetScroll(Plate plate)
        {
            int group = plate.SheetScrollGroup;
            float range = Mathf.Max(0f, plate.SheetContentH - plate.Height);
            SheetOffsets[group] = Mathf.Clamp(SheetOffsets[group], 0f, range);
            // Content is top-left anchored, so positive Y reveals lower rows.
            plate.Window.Content.anchoredPosition = new Vector2(0f, SheetOffsets[group]);
            bool scrolls = range > 0.1f;
            plate.SheetTrack.gameObject.SetActive(scrolls);
            if (!scrolls) { return; }
            if (IsDossier(group))
            {
                // printed, not wooden: a faint ink groove and an ink thumb on the paper
                plate.SheetTrack.sprite = plate.SheetThumb.sprite = Theme.RoundedFill;
                plate.SheetTrack.type = plate.SheetThumb.type = UnityEngine.UI.Image.Type.Sliced;
                plate.SheetTrack.color = new Color(Theme.DossierInk.r, Theme.DossierInk.g, Theme.DossierInk.b, 0.12f);
                plate.SheetThumb.color = new Color(Theme.DossierInk.r, Theme.DossierInk.g, Theme.DossierInk.b, 0.55f);
            }
            else
            {
                Theme.KitSkin(plate.SheetTrack, "kit_plank_walnut", 18);
                Theme.KitSkin(plate.SheetThumb, "kit_plank_pine", 18);
                plate.SheetTrack.color = plate.SheetThumb.color = Color.white;
            }
            float h = plate.Height - 4f;
            float thumb = Mathf.Max(12f, h * plate.Height / plate.SheetContentH);
            Theme.Place(plate.SheetTrack, plate.Window.Rect.sizeDelta.x - SheetScrollGutter + 2f, 2f, 4f, h);
            Theme.Place(plate.SheetThumb, 0f, (h - thumb) * SheetOffsets[group] / range, 4f, thumb);
            plate.SheetTrack.transform.SetAsLastSibling();
        }

        private static bool TickSheetScroll(Camera cam)
        {
            if (_sheetScrollDrag != null && (!Input.GetMouseButton(0)
                || !_sheetScrollDrag.Window.Go.activeInHierarchy || _sheetScrollDrag.SheetContentH <= 0f))
            {
                _sheetScrollDrag = null;
            }
            for (int i = 0; i < SheetPlates.Count; i++)
            {
                Plate p = SheetPlates[i];
                if (p.SheetContentH <= 0f || !p.Window.Go.activeInHierarchy) { continue; }
                bool inside = RectTransformUtility.RectangleContainsScreenPoint(p.Window.Rect, Input.mousePosition, cam);
                float wheel = Input.mouseScrollDelta.y;
                if (inside && Mathf.Abs(wheel) > 0.01f)
                {
                    ScrollSheet(p.SheetScrollGroup, wheel);
                    return true;
                }
                if (p.SheetTrack == null || !p.SheetTrack.gameObject.activeInHierarchy) { continue; }
                bool track = RectTransformUtility.RectangleContainsScreenPoint(p.SheetTrack.rectTransform, Input.mousePosition, cam);
                Vector2 point;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(p.SheetTrack.rectTransform, Input.mousePosition, cam, out point);
                float fromTop = p.SheetTrack.rectTransform.rect.yMax - point.y;
                if (track && Input.GetMouseButtonDown(0))
                {
                    _sheetScrollDrag = p;
                    bool thumb = RectTransformUtility.RectangleContainsScreenPoint(p.SheetThumb.rectTransform, Input.mousePosition, cam);
                    _sheetDragGrab = thumb ? fromTop + p.SheetThumb.rectTransform.anchoredPosition.y
                        : p.SheetThumb.rectTransform.sizeDelta.y * 0.5f;
                }
                if (ReferenceEquals(_sheetScrollDrag, p))
                {
                    float travel = p.SheetTrack.rectTransform.rect.height - p.SheetThumb.rectTransform.rect.height;
                    SheetOffsets[p.SheetScrollGroup] = Mathf.Clamp01((fromTop - _sheetDragGrab) / Mathf.Max(1f, travel))
                        * (p.SheetContentH - p.Height);
                    ApplySheetScroll(p);
                    return true;
                }
                if (track) { return true; }
            }
            return false;
        }

        // Same entry point for the wheel and the in-game harness.
        internal static bool ScrollSheet(int group, float notches)
        {
            for (int i = 0; i < SheetPlates.Count; i++)
            {
                Plate p = SheetPlates[i];
                if (p.SheetContentH <= 0f || p.SheetScrollGroup != group || !p.Window.Go.activeInHierarchy) { continue; }
                SheetOffsets[group] -= notches * SheetRowH * 3f;
                ApplySheetScroll(p);
                return true;
            }
            return false;
        }

        internal static string CompositionDump()
        {
            var s = new System.Text.StringBuilder();
            s.Append("ownBag=").Append(_ownOpen).Append(" ownDetails=").Append(_ownSheetOpen)
                .Append(" targetBag=").Append(_bagOpen).Append(" targetDetails=").Append(_sheetOpen);
            for (int i = 0; i < SheetPlates.Count; i++)
            {
                Plate p = SheetPlates[i];
                if (p.SheetContentH <= 0f) { continue; }
                int g = p.SheetScrollGroup;
                s.Append(" | g").Append(g).Append(' ').Append(SheetPages[g])
                    .Append(" content=").Append(p.SheetContentH.ToString("0.##"))
                    .Append(" view=").Append(p.Height.ToString("0.##"))
                    .Append(" scroll=").Append(SheetOffsets[g].ToString("0.##"));
            }
            return s.ToString();
        }

        // Harness door into the real interaction, like RosterAct. Requires a
        // nearby, placed container; does not grant remote access or run its acts.
        internal static bool ReviewProp(ItemInfo prop)
        {
            if (_mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null || prop == null
                || GameManager.nowEvent) { return false; }
            PlayerMove pm = _mn.gameMN.pMove;
            if (Vector3.Distance(pm.transform.position, prop.transform.position) > 3f) { return false; }
            ItemData data = _mn.itemMN.FindItem(prop.itemKey);
            if (data == null || (data.subType != ItemData.SubType.Chest && data.subType != ItemData.SubType.Stock
                && data.subType != ItemData.SubType.None)) { return false; }
            CloseFocus(_mn);
            pm.target = prop.gameObject;
            pm.StartCoroutine(pm.UseProp(data));
            return true;
        }

        internal static void ToggleOwnDetails()
        {
            _ownSheetOpen = !_ownSheetOpen;
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

    }
}
