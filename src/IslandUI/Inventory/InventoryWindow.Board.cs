using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE TOOL BELT. A slender leather belt
    // beside the menu plank, out while its chip - the first one, a crossed axe and
    // pick - is pressed in. Three loops: axe, pickaxe, knife. Each has a quiet
    // plain-tool silhouette. Real item icons hang upright between
    // the narrow strap and its riveted leather loops. The model is
    // Model/ToolBelt.
    //
    // The gestures are the action bar's (docs/action-bar.md §7), because a hook is a
    // binding too - it holds a mark, never the item:
    //   release over a hook, tool in hand   hung there, and the hand goes home
    //   shift + hold left + move 6 px       the hung tool lifts onto the cursor
    //   drop it on a hook that takes it     it moves there (the two trade if they can)
    //   drop it anywhere else               taken down
    //   shift + click                       taken down
    internal static partial class InventoryWindow
    {
        private sealed class HookView
        {
            public Image Shape, Tool, Box, WearTrack, WearFill;
            public EquipBrackets Equipped;
            public TextMeshProUGUI WearText;
            public RectTransform Cell;
            public string ShownKey;
            public ItemData ShownData;
            public int ShownLife = -1;
        }

        private static RectTransform _board;
        private static readonly HookView[] BoardHooks = new HookView[ToolBelt.Hooks];
        private static readonly string[] BoardDrawings = { "wp_axe_01", "wp_pickax_01", "wp_knife_01" };
        private static readonly string[] BoardNames = { "axe", "pickaxe", "knife" };

        private static int _boardPressed = -1;
        private static Vector2 _boardPressPos;
        private static int _boardLifted = -1;
        private static ItemSlot _boardLiftedSlot;
        private static Image _boardGhost;

        // 3.2 px per unit at 1440p: 276 x 116 art, 84 px pitch, 70 px base icons
        // scaled to 84 px axe/pick and 56 px knife around the fixed neck grips.
        // The art generator bakes two layers with an 18 px shadow margin.
        // A 14 px strap at y=5..23 and loops down to y=41 leave the tools exposed.
        // Tools rotate about their centres, raised into the neck grips; hit targets
        // stay unrotated. Loop stems sit behind the heads, grip bands in front.
        private const float BoardPad = 3.75f, BoardPitch = 26.25f, BoardCell = 21.875f, BoardCellTop = 9.6875f;
        private const float BeltHeight = 36.25f, BoardHeight = 34f, BoardGap = 4f, BoardBottom = 0.5f, BeltMargin = 5.625f;
        private const float WearWidth = 17.5f;
        private static float BoardWidth => ToolBelt.Hooks * BoardPitch + 2f * BoardPad;

        internal static bool BoardOpen => Settings.ToolBeltOpen.Value;

        // a hung tool is on the cursor: the world must not read the held button as
        // "walk there" - taking one down deliberately ends out over the world
        internal static bool BoardDragging => _boardLifted >= 0 && Input.GetMouseButton(0);

        private static void MakeBoard(RectTransform dock)
        {
            var go = Owned.New("IslandUI_ToolBelt");
            go.transform.SetParent(_hud, false);
            // transparent but a raycast target: the board is UI, so a click on it
            // never walks you there and a drag's release over it is not a drop
            var root = go.AddComponent<Image>();
            root.color = Color.clear;
            _board = root.rectTransform;
            float height = BoardHeight;
            _board.anchorMin = _board.anchorMax = _board.pivot = new Vector2(1f, 0f);
            _board.sizeDelta = new Vector2(BoardWidth, height);
            _board.anchoredPosition = new Vector2(dock.anchoredPosition.x - dock.sizeDelta.x - BoardGap,
                BoardBottom);
            var belt = Theme.KitImage(_board, "LeatherBelt", "kit_tool_belt_slim", 0, Color.white);
            belt.preserveAspect = false;
            Theme.Place(belt, -BeltMargin, -BeltMargin, BoardWidth + 2f * BeltMargin, BeltHeight + 2f * BeltMargin);
            for (int i = 0; i < ToolBelt.Hooks; i++)
            {
                BoardHooks[i] = MakeHook(i, height);
            }
            var loops = Theme.KitImage(_board, "LeatherLoops", "kit_tool_belt_loops", 0, Color.white);
            loops.preserveAspect = false;
            Theme.Place(loops, -BeltMargin, -BeltMargin, BoardWidth + 2f * BeltMargin, BeltHeight + 2f * BeltMargin);
            // Readouts overlay the lower handles, so the compact belt fits below
            // the inventory's 36-unit baseline. Keep them above every tool sprite.
            foreach (HookView hook in BoardHooks)
            {
                hook.Box.transform.SetAsLastSibling();
                hook.WearTrack.transform.SetAsLastSibling();
                hook.WearFill.transform.SetAsLastSibling();
                hook.WearText.transform.SetAsLastSibling();
                hook.Equipped.ToFront();
            }
            _board.gameObject.SetActive(BoardOpen);
        }

        private static HookView MakeHook(int i, float height)
        {
            var v = new HookView();
            float x = BoardPad + i * BoardPitch + (BoardPitch - BoardCell) / 2f;
            // A fixed plain-tool silhouette hangs from each empty loop.
            var shapeGo = Owned.New("Drawing");
            shapeGo.transform.SetParent(_board, false);
            v.Shape = shapeGo.AddComponent<Image>();
            v.Shape.raycastTarget = false;
            v.Shape.preserveAspect = true;
            v.Shape.color = new Color32(220, 203, 149, 200);
            v.Shape.enabled = false;
            PlaceHangingTool(v.Shape, x, i);
            var outline = shapeGo.AddComponent<Outline>();
            outline.effectColor = new Color32(32, 24, 15, 240);
            outline.effectDistance = new Vector2(0.6f, -0.6f);
            // Each full column remains easy to hit, independently of icon rotation.
            var cellGo = Owned.New("LoopTarget");
            var cellRect = (RectTransform)cellGo.transform;
            cellGo.transform.SetParent(_board, false);
            v.Cell = Theme.Place(cellRect, BoardPad + i * BoardPitch, 2.1875f,
                BoardPitch - 1.25f, height - 4.6875f);
            // The live tool hangs in front of the strap, behind its leather loop.
            var toolGo = Owned.New("Tool");
            toolGo.transform.SetParent(_board, false);
            v.Tool = toolGo.AddComponent<Image>();
            v.Tool.raycastTarget = false;
            v.Tool.preserveAspect = true;
            v.Tool.enabled = false;
            Outline rim = toolGo.AddComponent<Outline>();
            rim.effectColor = new Color32(24, 18, 12, 230);
            rim.effectDistance = new Vector2(0.45f, -0.45f);
            PlaceHangingTool(v.Tool, x, i);
            // Crafting's marker box outlines the full column for a compatible tool.
            v.Box = Theme.KitImage(_board, "Box", "kit_shed_box", 8, ToolShed.Highlight);
            Theme.Place(v.Box, BoardPad + i * BoardPitch, 2.1875f, BoardPitch - 1.25f, height - 4.6875f);
            v.Box.enabled = false;
            // Durability overlays the lower handle, with dark track and outlined
            // type for contrast. It no longer needs its own row below the icon.
            float wearX = x + (BoardCell - WearWidth) / 2f;
            v.WearTrack = Theme.KitImage(_board, "DurabilityTrack", null, 0, new Color32(30, 23, 16, 230));
            Theme.Place(v.WearTrack, wearX - 0.5f, 21f, WearWidth + 1f, 2.5f);
            v.WearFill = Theme.KitImage(_board, "DurabilityFill", null, 0, Color.white);
            Theme.Place(v.WearFill, wearX, 21.5f, WearWidth, 1.5f);
            v.WearText = Theme.MakeText(_board, "Durability", 6.25f, Theme.TextCream, TextAlignmentOptions.Center);
            v.WearText.enableWordWrapping = false;
            Theme.Place(v.WearText, x, 23f, BoardCell, Theme.CaptionH);
            v.WearTrack.enabled = v.WearFill.enabled = v.WearText.enabled = false;
            // In your hand: the action bar's gold corners (EquipBrackets), on the
            // column the crafting box outlines
            v.Equipped = new EquipBrackets(_board, BoardPad + i * BoardPitch, 2.1875f,
                BoardPitch - 1.25f, height - 4.6875f, 4.5f, 1f);
            return v;
        }

        private static Image MakeToolBadge(Transform parent, string name, string glyph, Color ink)
        {
            var badge = Theme.KitImage(parent, name, null, 0, ink);
            badge.sprite = HudGlyphs.Get(glyph);
            badge.preserveAspect = true;
            var rim = badge.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color32(24, 18, 12, 255);
            rim.effectDistance = new Vector2(0.5f, -0.5f);
            return badge;
        }

        private static void PlaceHangingTool(Image image, float x, int hook)
        {
            RectTransform r = Theme.Place(image, x, BoardCellTop, BoardCell, BoardCell);
            r.pivot = new Vector2(0.5f, 0.5f);
            float scale = hook == ToolBelt.Knife ? 0.8f : 1.2f;
            // Scale around the fixed neck grip (y=37 px), not the sprite centre:
            // larger axe/pick heads and a smaller knife still sit in their loops.
            float centerY = 37f + ((hook == ToolBelt.Knife ? 58f : 52f) - 37f) * scale;
            r.anchoredPosition = new Vector2(x + BoardCell / 2f, -centerY / Theme.KitPpu);
            r.localScale = Vector3.one * scale;
            // Game sprites are diagonal: axe/pick handles down, knife handle up.
            r.localRotation = Quaternion.Euler(0f, 0f, hook == ToolBelt.Knife ? 135f : 35f);
        }

        private static void ToggleBoard()
        {
            Settings.ToolBeltOpen.Value = !Settings.ToolBeltOpen.Value;
        }

        // Each HUD tick: open or shut, the hooks as the model has them, the gestures.
        // Returns the hover tip for the hook under the pointer.
        private static string TickBoard()
        {
            if (_board == null)
            {
                return null;
            }
            bool open = BoardOpen;
            if (_board.gameObject.activeSelf != open)
            {
                _board.gameObject.SetActive(open);
            }
            if (!open)
            {
                _boardPressed = -1;
                _boardLifted = -1;
                UpdateBoardGhost();
                return null;
            }
            ItemManager items = _mn.itemMN;
            ItemSlot hand = HeldTool();
            CommonStates owner = _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            int hover = HookUnderPointer();
            for (int i = 0; i < ToolBelt.Hooks; i++)
            {
                HookView v = BoardHooks[i];
                if (v.Shape.sprite == null && items != null)
                {
                    ItemData plain = items.FindItem(BoardDrawings[i]);
                    v.Shape.sprite = plain != null ? Theme.MaskOf(plain.itemSprite) : null;
                    v.Shape.enabled = v.Shape.sprite != null;
                }
                ItemSlot hung = i == _boardLifted ? null : ToolBelt.Hung(i);
                string key = hung != null ? hung.itemKey : null;
                if (key != v.ShownKey)
                {
                    v.ShownKey = key;
                    v.ShownData = key != null && items != null ? items.FindItem(key) : null;
                    v.Tool.sprite = v.ShownData != null ? v.ShownData.itemSprite : null;
                    v.Tool.enabled = v.Tool.sprite != null;
                }
                RefreshToolWear(v, hung);
                v.Equipped.Shown = hung != null && ToolBelt.IsEquipped(i, owner);
                v.Shape.color = new Color32(220, 203, 149, (byte)(v.Tool.enabled ? 32 : 200));
                v.Box.enabled = hand != null && ToolBelt.Fits(i, hand.toolType);
            }
            ReadBoardInput(hover, hand);
            UpdateBoardGhost();
            return BoardTip(hover, hand);
        }

        private static void RefreshToolWear(HookView view, ItemSlot slot)
        {
            bool shown = slot != null && view.ShownData != null;
            float maximum = shown ? view.ShownData.itemLife : 0f;
            float remaining = shown ? Mathf.Max(0f, slot.itemLife) : 0f;
            bool bar = shown && maximum > 0f;
            view.WearTrack.enabled = bar;
            view.WearFill.enabled = bar && remaining > 0f;
            view.WearText.enabled = shown;
            if (!shown) { view.ShownLife = -1; return; }
            // Refresh by instance value, not item key: wear, repair and exchanging
            // two copies of the same tool must all update this display immediately.
            int value = Mathf.CeilToInt(remaining);
            if (value != view.ShownLife)
            {
                view.ShownLife = value;
                view.WearText.text = value.ToString();
            }
            float fraction = maximum > 0f ? Mathf.Clamp01(remaining / maximum) : 1f;
            Color ink = Theme.WearInk(fraction);
            view.WearFill.color = ink;
            view.WearText.color = ink;
            view.WearFill.rectTransform.sizeDelta = new Vector2(WearWidth * fraction, 1.5f);
        }

        // what is in your hand, if it came from your own bags; a tool from a chest
        // is not yours to hang (the mark lives only in your inventory)
        private static ItemSlot HeldTool()
        {
            InventoryManager inv = _mn.inventory;
            ItemSlot hand = inv != null ? inv.hoverSlot : null;
            if (hand == null || !hand.gameObject.activeSelf || string.IsNullOrEmpty(hand.itemKey))
            {
                return null;
            }
            return _grabSource == null || _grabSource.Slot == null || FavouriteMoves.Yours(inv, _grabSource.Slot)
                ? hand : null;
        }

        private static bool HandHolds()
        {
            ItemSlot hand = _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            return hand != null && hand.gameObject.activeSelf && !string.IsNullOrEmpty(hand.itemKey);
        }

        private static int HookUnderPointer()
        {
            for (int i = 0; i < ToolBelt.Hooks; i++)
            {
                RectTransform cell = BoardHooks[i].Cell;
                if (RectTransformUtility.RectangleContainsScreenPoint(cell, Input.mousePosition, null))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ReadBoardInput(int hover, ItemSlot hand)
        {
            bool holding = HandHolds();
            if (hover >= 0 && _boardLifted < 0)
            {
                ClickMap.Offer(ClickMap.ForBarSlot(ToolBelt.Hung(hover) != null, !holding));
            }
            // a tool brought from your bag: a drag that ends here, or a click with
            // it in hand - the press never touched the board, so it is caught here
            if (Input.GetMouseButtonUp(0) && _boardPressed < 0 && hover >= 0 && holding)
            {
                HangHeld(hover, hand);
                return;
            }
            if (Input.GetMouseButtonDown(0) && hover >= 0 && !holding && _boardPressed < 0)
            {
                _boardPressed = hover;
                _boardPressPos = Input.mousePosition;
            }
            // the lift, at the threshold as the bar's is, and only with shift
            if (_boardLifted < 0 && _boardPressed >= 0 && Input.GetMouseButton(0) && ClickMap.Shift
                && ToolBelt.Hung(_boardPressed) != null
                && ((Vector2)Input.mousePosition - _boardPressPos).sqrMagnitude > DragThreshold * DragThreshold)
            {
                _boardLifted = _boardPressed;
                _boardLiftedSlot = ToolBelt.Hung(_boardLifted);
            }
            if (_boardPressed >= 0 && Input.GetMouseButtonUp(0))
            {
                if (_boardLifted >= 0)
                {
                    EndBoardLift(hover);
                }
                else if (hover == _boardPressed
                    && ClickMap.ForBarSlot(ToolBelt.Hung(hover) != null, true) == ClickMap.Verb.RemoveAction)
                {
                    Plugin.Log.LogInfo("Tool belt: " + BoardNames[hover] + " hook emptied by shift-click");
                    ToolBelt.TakeDown(hover);
                }
                _boardPressed = -1;
                _boardLifted = -1;
                _boardLiftedSlot = null;
            }
        }

        // The hand is marked, not the cell it came from: sending it home is a real
        // move (SlotToSlot), and ToolBeltMoves carries the mark with it. A refusal
        // goes home the same way, as the action bar's does.
        private static void HangHeld(int hook, ItemSlot hand)
        {
            if (hand != null && ToolBelt.Fits(hook, hand.toolType))
            {
                ToolBelt.Hang(hook, hand);
                Plugin.Log.LogInfo("Tool belt: " + hand.itemKey + " hung on the " + BoardNames[hook] + " hook");
            }
            ReturnHeldToSource();
        }

        private static void EndBoardLift(int hover)
        {
            int from = _boardLifted;
            ItemSlot slot = _boardLiftedSlot;
            if (hover == from || slot == null)
            {
                return;   // back where it hung
            }
            if (hover < 0)
            {
                Plugin.Log.LogInfo("Tool belt: " + slot.itemKey + " taken down");
                ToolBelt.TakeDown(from);
                return;
            }
            if (!ToolBelt.Fits(hover, slot.toolType))
            {
                return;   // a hook that does not take it: nothing changes
            }
            ItemSlot other = ToolBelt.Hung(hover);
            ToolBelt.Hang(hover, slot);
            if (other != null && ToolBelt.Fits(from, other.toolType))
            {
                ToolBelt.Hang(from, other);
            }
            else
            {
                ToolBelt.TakeDown(from);
            }
        }

        // The lifted tool rides the cursor as an icon of ours, as the action bar's
        // binding does: on the root canvas, raised every frame.
        private static void UpdateBoardGhost()
        {
            ItemData data = _boardLifted >= 0 && _boardLiftedSlot != null && _mn != null && _mn.itemMN != null
                ? _mn.itemMN.FindItem(_boardLiftedSlot.itemKey) : null;
            if (data == null || data.itemSprite == null || _hud == null)
            {
                if (_boardGhost != null && _boardGhost.enabled)
                {
                    _boardGhost.enabled = false;
                }
                return;
            }
            Canvas rootCanvas = _hud.GetComponentInParent<Canvas>().rootCanvas;
            if (_boardGhost == null)
            {
                var go = Owned.New("IslandUI_ToolBeltGhost");
                go.transform.SetParent(rootCanvas.transform, false);
                _boardGhost = go.AddComponent<Image>();
                _boardGhost.raycastTarget = false;
                _boardGhost.preserveAspect = true;
                var gr = (RectTransform)go.transform;
                gr.sizeDelta = new Vector2(BoardCell, BoardCell);
                gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            }
            _boardGhost.sprite = data.itemSprite;
            _boardGhost.enabled = true;
            _boardGhost.transform.SetAsLastSibling();
            Camera cam = rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rootCanvas.transform,
                    Input.mousePosition, cam, out Vector2 local))
            {
                ((RectTransform)_boardGhost.transform).anchoredPosition = local;
            }
        }

        private static string BoardTip(int hover, ItemSlot hand)
        {
            if (hover < 0 || _boardLifted >= 0)
            {
                return null;
            }
            string hook = BoardNames[hover];
            if (HandHolds())
            {
                bool fits = hand != null && ToolBelt.Fits(hover, hand.toolType);
                CursorBadge.Show(fits ? "assign  →  " + hook + " loop"
                    : hand == null ? "choose a tool from your own bags"
                    : "the " + hook + " loop takes " + (hover == ToolBelt.Axe ? "an axe" : "a " + hook));
                return null;
            }
            ItemSlot hung = ToolBelt.Hung(hover);
            ItemData data = hung != null && _mn.itemMN != null ? _mn.itemMN.FindItem(hung.itemKey) : null;
            if (data == null)
            {
                return "Drop " + (hover == ToolBelt.Axe ? "an axe" : "a " + hook) + " from your bag here";
            }
            string wear = "  ·  Durability " + Mathf.Max(0f, hung.itemLife).ToString("0.#");
            if (data.itemLife > 0f) { wear += " / " + data.itemLife.ToString("0.#"); }
            CommonStates owner = _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            string equipped = ToolBelt.IsEquipped(hover, owner) ? "  ·  Equipped" : "";
            return data.GetItemName() + equipped + wear + "  ·  used when a job needs "
                + (hover == ToolBelt.Axe ? "an axe" : "a " + hook);
        }
    }
}
