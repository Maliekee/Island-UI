using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        // THE SHEET FLOATS LEFT WHEN THE LEFT SLOT IS FREE: with crafting, the
        // vicinity and the roster all shut, a person's sheet may stand where the
        // left windows do instead of above the unit, and it is taller there - it
        // keeps its top rail and its floor drops to the slot's. One setting says
        // whether it does (Settings.SheetFloatLeft); a dock chip beside the
        // sheet's close is the same setting, switched in play. Opening a left
        // window sends the sheet back to the middle; closing the last brings it
        // back. The bag, the unit and the act row stay in the middle: they are
        // the target's, not the sheet's.
        //
        // WHAT SLIDES IT: the chip, and a left window opening or shutting under
        // an open sheet - the sheet makes way, or takes the room back. What does
        // not: the sheet opening, which puts it where it belongs at once (one
        // that opened in the middle and then drifted left read as a glitch),
        // and the settings window's switch. The layout runs on refresh, not
        // every frame, so TickFloat watches what the sheet's place depends on
        // and lays out the moment it changes, and while a slide runs it eases
        // _float and lays out each frame. Every place that reads the sheet's x
        // adds FloatShiftX(group), so the frame, its members, its tabs and the
        // dock chip move as one.
        private const float FloatSeconds = 0.25f;
        private static float _float;          // 0 the middle .. 1 the left slot
        private static float _floatShiftX;    // this layout's offset of the sheet
        private static float _floatFloor = float.NaN;   // this layout's floor, eased
        private static bool _floatLaidOut;    // the sheet was placed by the last layout
        private static bool _floatSliding;    // the tab was clicked: ease, do not jump
        private static bool _floatFreeSeen;   // the slot as the last layout saw it
        private static Image _floatTab;   // the dock chip
        private static bool _floatTabPressed;

        private static float FloatShiftX(int group)
        {
            return group == GroupCharacter ? _floatShiftX : 0f;
        }

        // the slide eases; a drag follows the pointer as it is
        private static float FloatEase()
        {
            return _floatDragging ? _float : _float * _float * (3f - 2f * _float);
        }

        private static bool LeftSlotFree()
        {
            return !_reachOpen && _mn != null && _mn.uiMN != null && !VanillaHoldsLeftSlot(_mn.uiMN);
        }

        // a person's sheet, open, with a bar to stand on and a slot to go to
        private static bool FloatPossible()
        {
            return _sheetOpen && Focus() is CommonStates && _root != null
                && Slot.Read(_mn, (RectTransform)_root.transform);
        }

        // where the sheet belongs, whether or not it is up - so it opens there
        private static float FloatTarget()
        {
            return Settings.SheetFloatLeft.Value && LeftSlotFree() ? 1f : 0f;
        }

        // Called by Layout once the columns have their x: where the sheet stands
        // this frame, between the bar's edge and the slot's.
        private static void LayoutFloat(bool pinned, float barX)
        {
            _floatShiftX = 0f;
            _floatFloor = float.NaN;
            // a left window came or went under a sheet that was up: slide. The
            // vicinity and the roster refresh as they toggle, so it is noticed
            // here and not only in TickFloat.
            bool free = LeftSlotFree();
            if (_floatLaidOut && free != _floatFreeSeen)
            {
                _floatSliding = true;
            }
            _floatFreeSeen = free;
            _floatLaidOut = false;
            if (!_floatSliding && !_floatDragging)
            {
                _float = FloatTarget();
            }
            _floatSpan = pinned && FloatPossible() ? Slot.Left - barX : 0f;
            if (!pinned || _float <= 0f || !FloatPossible())
            {
                return;
            }
            float e = FloatEase();
            _floatShiftX = (Slot.Left - barX) * e;
            _floatFloor = Slot.Bottom;
        }

        // the floor PlaceSheetGroup computed for the middle, eased toward the slot's
        private static float FloatFloor(int group, float middle)
        {
            if (group == GroupCharacter)
            {
                _floatMidFloor = middle;
            }
            if (group != GroupCharacter || float.IsNaN(_floatFloor))
            {
                return middle;
            }
            return Mathf.Lerp(middle, _floatFloor, FloatEase());
        }

        // Every frame: the slide, if the tab started one, or else a layout the
        // moment the slot or the setting changes - not at the next refresh.
        private static void TickFloat()
        {
            if (_floatDragging)
            {
                return;   // the drag lays out as the pointer moves (TickSheetDrag)
            }
            float to = FloatTarget();
            if (!_floatLaidOut)
            {
                _floatSliding = false;
                _float = to;
                return;
            }
            if (_floatSliding)
            {
                _float = Mathf.MoveTowards(_float, to, Time.unscaledDeltaTime / FloatSeconds);
                _floatSliding = !Mathf.Approximately(_float, to);
                Layout();
                return;
            }
            if (!Mathf.Approximately(_float, to) || LeftSlotFree() != _floatFreeSeen)
            {
                Layout();
            }
        }

        // the room the tab row keeps for the chip, on every person's sheet that
        // could float, so the tabs do not change width as a left window comes
        // and goes - only the chip does
        private static float DockReserve(int group)
        {
            return group == GroupCharacter && Focus() is CommonStates && _colAnchorX[CharacterColumn] < 0.5f
                ? CloseChipSize + TileGap : 0f;
        }

        // After the frames: the dock chip, a window control beside the close and
        // the same enamel tag. It stays while a left window holds the slot, so
        // it still says what the setting is, and a click there only switches the
        // setting: the sheet goes left when the slot frees. Its glyph is the
        // dock-side mark for what a click does: a window with its left side
        // filled while the setting is off, its middle filled while it is on. A
        // child of the frame, so it rides the slide with it.
        private static void PlaceFloatTab()
        {
            int index = _groupFrame[GroupCharacter];
            _floatLaidOut = index >= 0 && _groupMembers[GroupCharacter] > 0 && Focus() is CommonStates;
            bool show = _floatLaidOut && DockReserve(GroupCharacter) > 0f && FloatPossible()
                && Plates[index].Close != null;
            if (!show)
            {
                if (_floatTab != null && _floatTab.gameObject.activeSelf)
                {
                    _floatTab.gameObject.SetActive(false);
                }
                return;
            }
            Plate frame = Plates[index];
            if (_floatTab == null)
            {
                _floatTab = Theme.KitImage(frame.Window.Go.transform, "IslandUI_Dock", "kit_dock_left_idle", 0, Color.white);
                _floatTab.raycastTarget = true;
                _floatTab.preserveAspect = true;
            }
            // frames are pooled: the chip follows whichever plate is the sheet's now
            if (_floatTab.transform.parent != frame.Window.Go.transform)
            {
                _floatTab.transform.SetParent(frame.Window.Go.transform, false);
            }
            if (!_floatTab.gameObject.activeSelf)
            {
                _floatTab.gameObject.SetActive(true);
            }
            RectTransform close = frame.Close.rectTransform;
            RectTransform r = _floatTab.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(CloseChipSize, CloseChipSize);
            // left of the close, as the star and the eye stand on a name plate
            r.anchoredPosition = new Vector2(close.anchoredPosition.x - CloseChipSize - TileGap,
                close.anchoredPosition.y);
            r.SetAsLastSibling();
            PaintDock(false, false);
            PlaceLift();
        }

        private static void PaintDock(bool hot, bool down)
        {
            string side = Settings.SheetFloatLeft.Value ? "middle" : "left";
            Sprite face = Theme.Kit("kit_dock_" + side + (down ? "_down" : hot ? "_hot" : "_idle"), 0);
            if (_floatTab.sprite != face)
            {
                _floatTab.sprite = face;
                _floatTab.type = Image.Type.Simple;
            }
        }

        // The chip's click, the close chip's way: hover lights it, a press sinks
        // it, and press and release over it switch the setting - and slide.
        private static bool TickFloatTab(Camera cam)
        {
            if (_floatTab == null || !_floatTab.gameObject.activeInHierarchy)
            {
                _floatTabPressed = false;
                return false;
            }
            bool over = RectTransformUtility.RectangleContainsScreenPoint(
                _floatTab.rectTransform, Input.mousePosition, cam);
            if (over && Input.GetMouseButtonDown(0) && !_dragging)
            {
                _floatTabPressed = true;
            }
            else if (_floatTabPressed && Input.GetMouseButtonUp(0))
            {
                _floatTabPressed = false;
                if (over)
                {
                    Settings.SheetFloatLeft.Value = !Settings.SheetFloatLeft.Value;
                    _floatSliding = true;
                }
                PaintDock(over, false);
                return true;
            }
            else if (_floatTabPressed && !Input.GetMouseButton(0))
            {
                _floatTabPressed = false;
            }
            PaintDock(over, over && _floatTabPressed);
            return over || _floatTabPressed;
        }

        // DRAGGED BY THE WHOLE SHEET, the window's own way to its place: the tab
        // row, the folder and the paper - not the close or the dock chip, and
        // not a slider, which drags on its own. (The folder's kraft edge alone
        // was tried first: 2 units wide, nobody found it.) A press there is the
        // tabs' or the row's until it moves DragThreshold sideways, and then it
        // is a drag - they let go of their press, since they act on release, so
        // nothing is picked. Held, the folder lifts (a shadow under it). Nothing
        // marks it on hover: the whole sheet is the handle, so there is no part
        // of it to point out (a lift and a "drag -> left" badge over the tab row
        // and the edge were tried while only those dragged). The sheet follows
        // the pointer between its two homes (the slide's own track, unhurried by
        // the ease), both homes are outlined in dashes, the one a let-go would
        // land in bright, and the release is the setting: past half way it
        // floats, short of it it does not, and the sheet eases the rest of the
        // way home from where it was let go. A left window in the slot does not
        // stop it: let go on the left, the sheet takes the slot - the window
        // there closes and the setting ends on, whether it was on (then the
        // close is all that changes) or off.
        private static bool _dragPending, _floatDragging;
        private static Vector2 _dragFromScreen, _dragFromLocal;
        private static float _dragFromFloat;
        private static float _floatSpan;          // the slide's length in root units
        private static readonly RectTransform[] _dropZones = new RectTransform[2];   // 0 left, 1 middle
        private static readonly Image[] _dropFills = new Image[2];
        private static readonly Image[] _dropDashes = new Image[8];
        private static float _floatMidFloor;      // the middle's floor, for its outline
        private static Image _lift;

        // The pointer is the sheet's while it is dragged, and for the frame it is
        // let go in: out over the world the held button would walk you there
        // (DragOwnsThePointer).
        private static int _dragEndedFrame = -1;
        internal static bool SheetDragging => _floatDragging || _dragEndedFrame == Time.frameCount;

        private static bool FloatDragAllowed()
        {
            return _floatLaidOut && DockReserve(GroupCharacter) > 0f
                && FloatPossible() && Mathf.Abs(_floatSpan) > 1f;
        }

        // the t whose smoothstep is e, so a let-go eases on from where it stands
        private static float InverseEase(float e)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 18; i++)
            {
                float t = (lo + hi) * 0.5f;
                if (t * t * (3f - 2f * t) < e) { lo = t; } else { hi = t; }
            }
            return (lo + hi) * 0.5f;
        }

        private static bool TickSheetDrag(Camera cam)
        {
            DossierTabs row = _dossierTabs[GroupCharacter];
            var rootRect = (RectTransform)_root.transform;
            if (_floatDragging)
            {
                if (Input.GetMouseButton(0) && FloatDragAllowed())
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Input.mousePosition, cam, out Vector2 at);
                    _float = Mathf.Clamp01(_dragFromFloat + (at.x - _dragFromLocal.x) / _floatSpan);
                    Layout();
                    PlaceDropZone();
                    return true;
                }
                // let go: where it lands is the setting, and it eases home from here
                bool allowed = FloatDragAllowed();
                bool left = _float >= 0.5f;
                _floatDragging = false;
                _dragEndedFrame = Time.frameCount;
                _float = InverseEase(_float);
                if (allowed)
                {
                    Settings.SheetFloatLeft.Value = left;
                }
                _floatSliding = true;
                if (allowed && left && !LeftSlotFree())
                {
                    CloseLeftWindow();
                }
                HideDropZone();
                Layout();
                PlaceLift();
                return true;
            }
            if (_dragPending)
            {
                if (!Input.GetMouseButton(0))
                {
                    _dragPending = false;
                    return false;
                }
                if (Mathf.Abs(Input.mousePosition.x - _dragFromScreen.x) < DragThreshold || !FloatDragAllowed())
                {
                    return false;
                }
                _dragPending = false;
                _dragFromFloat = FloatEase();   // where a slide in progress stands
                _float = _dragFromFloat;
                _floatSliding = false;
                _floatDragging = true;
                if (row != null) { row.CancelPress(); }
                _sheetPressed = null;   // a row acts on release: it does not now
                _sheetPressedSub = -1;
                PlaceDropZone();
                return true;
            }
            if (Input.GetMouseButtonDown(0) && !_dragging && FloatDragAllowed() && OverSheetHandle(cam)
                && !(_sheetHot != null && _sheetHot.DrewKind == LineSlider))
            {
                _dragPending = true;
                _dragFromScreen = Input.mousePosition;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Input.mousePosition, cam, out _dragFromLocal);
            }
            return false;   // the press is the tabs' too, until it moves
        }

        // the tab row and the folder, paper and all, but not its chips
        private static bool OverSheetHandle(Camera cam)
        {
            int index = _groupFrame[GroupCharacter];
            if (index < 0)
            {
                return false;
            }
            Plate frame = Plates[index];
            Vector2 m = Input.mousePosition;
            DossierTabs row = _dossierTabs[GroupCharacter];
            bool tabs = row != null && row.Rect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(row.Rect, m, cam);
            bool folder = RectTransformUtility.RectangleContainsScreenPoint(frame.Window.Rect, m, cam);
            if (!tabs && !folder)
            {
                return false;
            }
            if (frame.Close != null && frame.Close.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(frame.Close.rectTransform, m, cam))
            {
                return false;
            }
            return !(_floatTab != null && _floatTab.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_floatTab.rectTransform, m, cam));
        }

        // THE FOLDER LIFTS: a soft shadow under it, down and to the right, while
        // it is held. Behind every frame, so only its edge shows; laid out with
        // the folder, so it rides the drag.
        private static void PlaceLift()
        {
            int index = _groupFrame[GroupCharacter];
            bool on = _floatDragging && index >= 0 && _floatLaidOut;
            if (!on)
            {
                if (_lift != null && _lift.gameObject.activeSelf)
                {
                    _lift.gameObject.SetActive(false);
                }
                return;
            }
            if (_lift == null)
            {
                _lift = Theme.KitImage(_root.transform, "IslandUI_Lift", "kit_plank_walnut", 0, Color.white);
                _lift.sprite = Theme.RoundedFill;
                _lift.type = Image.Type.Sliced;
                _lift.raycastTarget = false;
                _lift.color = new Color(0f, 0f, 0f, 0.38f);
            }
            if (!_lift.gameObject.activeSelf)
            {
                _lift.gameObject.SetActive(true);
            }
            RectTransform f = Plates[index].Window.Rect;
            DossierTabs row = _dossierTabs[GroupCharacter];
            float tabs = row != null && row.Rect.gameObject.activeInHierarchy ? row.Rect.sizeDelta.y : 0f;
            RectTransform r = _lift.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(_colX[CharacterColumn] + _floatShiftX + 2.5f, _groupLo[GroupCharacter] - 3.5f);
            r.sizeDelta = new Vector2(f.sizeDelta.x + 1f, f.sizeDelta.y + tabs * 0.6f);
            r.SetAsFirstSibling();
        }

        // BOTH HOMES, outlined in cream dashes over a faint wash, while the sheet
        // is dragged: the left slot and the middle above the unit, each the room
        // the sheet could stand in there, from that home's floor to the sheet's
        // top rail. The one a let-go would land in is the bright one.
        private static void PlaceDropZone()
        {
            int index = _groupFrame[GroupCharacter];
            float w = index >= 0 ? Plates[index].Window.Rect.sizeDelta.x : Slot.Width;
            float top = _groupHi[GroupCharacter];
            bool left = _float >= 0.5f;
            PlaceZone(0, Slot.Left, Slot.Bottom, w, top, left);
            PlaceZone(1, _colX[CharacterColumn], _floatMidFloor, w, top, !left);
        }

        private static void PlaceZone(int z, float x, float floor, float w, float top, bool lands)
        {
            if (_dropZones[z] == null)
            {
                var go = Owned.New("IslandUI_DropZone" + z);
                go.transform.SetParent(_root.transform, false);
                RectTransform zone = (RectTransform)go.transform;
                _dropZones[z] = zone;
                Image wash = Theme.KitImage(zone, "Wash", "kit_plank_walnut", 0, Color.white);
                wash.sprite = Theme.RoundedFill;
                wash.type = Image.Type.Sliced;
                wash.raycastTarget = false;
                Stretch(wash.rectTransform);
                _dropFills[z] = wash;
                for (int i = 0; i < 4; i++)
                {
                    bool vertical = i >= 2;
                    var dash = Owned.New(vertical ? "DashV" : "DashH").AddComponent<Image>();
                    dash.transform.SetParent(zone, false);
                    dash.sprite = DashSprite(vertical);
                    dash.type = Image.Type.Tiled;
                    dash.raycastTarget = false;
                    RectTransform r = dash.rectTransform;
                    // 0 top, 1 bottom, 2 left, 3 right, each 2 units thick on the rim
                    r.anchorMin = new Vector2(i == 3 ? 1f : 0f, i == 0 ? 1f : 0f);
                    r.anchorMax = new Vector2(i == 2 ? 0f : 1f, i == 1 ? 0f : 1f);
                    r.pivot = new Vector2(i == 3 ? 1f : 0f, i == 0 ? 1f : 0f);
                    r.sizeDelta = new Vector2(vertical ? 2f : 0f, vertical ? 0f : 2f);
                    r.anchoredPosition = Vector2.zero;
                    _dropDashes[z * 4 + i] = dash;
                }
            }
            RectTransform at = _dropZones[z];
            if (!at.gameObject.activeSelf)
            {
                at.gameObject.SetActive(true);
            }
            at.anchorMin = at.anchorMax = at.pivot = Vector2.zero;
            at.anchoredPosition = new Vector2(x, floor);
            at.sizeDelta = new Vector2(w, Mathf.Max(20f, top - floor));
            Color cream = Theme.KitCream;
            _dropFills[z].color = new Color(cream.r, cream.g, cream.b, lands ? 0.16f : 0.06f);
            for (int i = 0; i < 4; i++)
            {
                _dropDashes[z * 4 + i].color = new Color(cream.r, cream.g, cream.b, lands ? 0.95f : 0.45f);
            }
            // behind the frames, so the sheet passes over it - but over a left
            // window still in the slot, which it would close
            if (z == 0 && !LeftSlotFree()) { at.SetAsLastSibling(); }
            else { at.SetAsFirstSibling(); }
        }

        // The slot's occupant, shut its own way: the vicinity and the roster are
        // one window of ours, the rest are vanilla's panels (crafting, collections,
        // the dissect panel, the shop), closed through vanilla's own close.
        private static void CloseLeftWindow()
        {
            if (_reachOpen)
            {
                ToggleReach();
            }
            else if (_mn != null && _mn.uiMN != null && VanillaHoldsLeftSlot(_mn.uiMN))
            {
                _mn.uiMN.LeftPanelState(0);
            }
        }

        private static void HideDropZone()
        {
            for (int z = 0; z < _dropZones.Length; z++)
            {
                if (_dropZones[z] != null && _dropZones[z].gameObject.activeSelf)
                {
                    _dropZones[z].gameObject.SetActive(false);
                }
            }
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = Vector2.zero;
        }

        // one dash and its gap, 6 on and 4 off, tiled along an edge
        private static Sprite _dashH, _dashV;
        private static Sprite DashSprite(bool vertical)
        {
            Sprite cached = vertical ? _dashV : _dashH;
            if (cached != null)
            {
                return cached;
            }
            int w = vertical ? 2 : 10, h = vertical ? 10 : 2;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int along = vertical ? y : x;
                    tex.SetPixel(x, y, along < 6 ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            made.hideFlags = HideFlags.HideAndDontSave;
            if (vertical) { _dashV = made; } else { _dashH = made; }
            return made;
        }
    }
}
