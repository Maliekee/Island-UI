using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IslandUI.UI;

namespace IslandUI
{
    // The action bar: a row of slots that hold ITEM KEYS, not items.
    //
    // Dragging a stack here does not move it. The slot comes to represent that
    // item's ACTION, so the bar eats the meat and equips the sword while the item
    // stays where it lives. See docs/action-bar.md for the recon this rests on; the
    // three findings that shaped it:
    //
    // 1. Vanilla's bottom bar was called "the shared hotbar" all over our own code
    //    (Crafting/CraftingPatches.cs and three others) while being STORAGE -
    //    itemSlot[0..baseCount]. That word was reached for independently, before any
    //    of this was designed, which is how the conflation announced itself. So this
    //    feature splits a conflation rather than adding a widget, and the split is
    //    why the name here is ACTION BAR: those comments now say "Shared Bag", and
    //    "hotbar" is not a word this tree uses for either half any more.
    // 2. "What does this item do" is already ItemManager.UseItem's question, and it
    //    answers by itemType. We never invent a verb per type; we ask.
    // 3. Item and ResourceW have NO action, so binding one would make a slot that
    //    does nothing on press - indistinguishable from a broken one. Refused.
    internal static class ActionBar
    {
        // The SAME metrics the container plates use, harvested from vanilla's own
        // slot (Theme.SlotSize / SlotPitch). The bar had its own hardcoded 34/4 and
        // read visibly larger than the bags beside it — a cell is a cell, and two
        // sizes for one thing is exactly the kind of drift Theme exists to prevent.
        private static float CellBox { get { return Theme.SlotSize; } }
        private static float CellPitch { get { return Theme.SlotPitch; } }
        // Was the plate's padding; with no plate there is nothing to pad away from,
        // so the bar is exactly its row of cells.
        private const float Inset = 0f;

        // The keys a slot can be bound to, in bar order. 1..9 then 0, which is the
        // convention every game with a bar uses and therefore the one already in
        // the player's fingers.
        private static readonly KeyCode[] Keys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
        };

        private sealed class Cell
        {
            public GameObject Go;
            public Image Box;
            public Image Icon;
            public TextMeshProUGUI Count;
            public TextMeshProUGUI Key;
            public EquipBrackets Worn;
            public Image Back;
            public bool Outlined;
        }

        // Bound item keys, by bar index. Kept inside the save (SaveBlock).
        private static readonly List<string> Bound = new List<string>();

        internal static string[] SnapshotBindings()
        {
            return Bound.ToArray();
        }

        // after a load: the save's bindings, padded to the bar's width
        internal static void RestoreBindings(IList<string> keys)
        {
            Bound.Clear();
            Bound.AddRange(keys);
            while (Bound.Count < SlotCount)
            {
                Bound.Add(null);
            }
        }
        private static readonly List<Cell> Cells = new List<Cell>();

        private static GameObject _root;
        private static ManagersScript _mn;

        // ---- pointer gesture state --------------------------------------------
        //
        // The SAME grammar InventoryWindow runs (press -> threshold -> release),
        // and deliberately so: the bar sits beside the bags and a second set of
        // rules for the same-looking cells would be the drift Theme exists to
        // prevent. What differs is only what the gestures MEAN here.
        private static int _pressedIndex = -1;
        private static int _pressedButton = -1;
        private static Vector2 _pressPos;
        private static bool _dragging;
        private static string _dragKey;
        private static int _dragFrom = -1;
        private const float DragThreshold = 6f;

        // The drag ghost: our own icon on the cursor, NOT vanilla's hoverSlot.
        // hoverSlot carries a real item, and a binding is not one - routing a
        // binding through it would move the actual stack, which is the single
        // thing this whole feature exists not to do.
        private static Image _ghost;

        private static int _flashIndex = -1;
        private static float _flashUntil;
        private static Color _flashColor;
        private const float FlashSeconds = 0.12f;
        // Green-ish for a press that did something, red for one that was refused.
        private static readonly Color FireFlash = new Color(1f, 1f, 1f, 1f);
        private static readonly Color RefuseFlash = new Color(1f, 0.45f, 0.4f, 1f);

        /// <summary>
        /// A binding is on the cursor. Mirrors InventoryWindow.IsDragging, button
        /// and all, and for the same reason: the world must not read the held
        /// button as "walk toward the cursor" for the length of the gesture.
        /// </summary>
        internal static bool IsDragging
        {
            get { return _dragging && Input.GetMouseButton(0); }
        }

        /// <summary>
        /// True while the bar is firing an item. Read by the target guard below -
        /// see Fire().
        /// </summary>
        internal static bool Firing;

        internal static int SlotCount
        {
            get { return Mathf.Clamp(Settings.ActionBarSlots.Value, 1, Keys.Length); }
        }

        internal static void Tick(ManagersScript mn)
        {
            if (!Settings.ActionBar.Value)
            {
                if (_root != null) _root.SetActive(false);
                return;
            }
            _mn = mn;
            if (!Build(mn))
            {
                return;
            }
            _root.SetActive(true);
            // Re-placed every frame, not once at build: vanilla's strip is found by
            // scanning the scene and may not resolve on the first frame, so a
            // build-time position would freeze whatever the fallback guessed.
            Position();
            Refresh();
            ReadInput();
        }

        // ---- building ----------------------------------------------------------

        private static bool Build(ManagersScript mn)
        {
            if (_root != null)
            {
                return true;
            }
            if (mn == null || mn.uiMN == null || !Theme.Ensure(mn, mn.inventory))
            {
                return false;
            }
            Transform parent = mn.uiMN.itemDescPanel != null
                ? mn.uiMN.itemDescPanel.transform.parent : null;
            if (parent == null)
            {
                return false;
            }

            _root = Owned.New("IslandUI_ActionBar");
            _root.transform.SetParent(parent, false);
            var rect = (RectTransform)_root.transform;

            int n = SlotCount;
            float w = n * CellPitch - (CellPitch - CellBox) + 2f * Inset;
            float h = CellBox + 2f * Inset;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(w, h);

            // NO PLATE BEHIND THE BAR. A container plate frames its slots because it
            // is a thing you look *into*; the action bar is a row you look *along*, and a
            // frame around it only adds an edge to parse. The cells carry their own
            // raycast targets, so nothing needed the backing image to catch clicks.

            while (Bound.Count < n) Bound.Add(null);
            for (int i = 0; i < n; i++)
            {
                Cells.Add(CreateCell(i));
            }
            Plugin.Log.LogInfo("Action bar built (" + n + " slots)");
            return true;
        }

        private static Cell CreateCell(int index)
        {
            var cell = new Cell();
            cell.Go = Owned.New("Bar" + index);
            cell.Go.transform.SetParent(_root.transform, false);
            cell.Box = cell.Go.AddComponent<Image>();
            cell.Box.sprite = Theme.SlotFrame;
            cell.Box.raycastTarget = true;

            // A SOLID GROUND UNDER THE ICON: the items seemed dimmed. They were not - the frame's own fill is see-through, and a
            // one-pixel spear over bright grass reads as faded. Inset from the frame
            // so its rounded corners stay the frame's.
            cell.Back = Theme.KitImage(cell.Go.transform, "Back", null, 0,
                new Color(0.07f, 0.05f, 0.035f, 0.88f));
            Theme.Place(cell.Back, 1.5f, 1.5f, CellBox - 3f, CellBox - 3f);

            var iconGo = Owned.New("Icon");
            iconGo.transform.SetParent(cell.Go.transform, false);
            cell.Icon = iconGo.AddComponent<Image>();
            cell.Icon.raycastTarget = false;
            cell.Icon.preserveAspect = true;
            Theme.PlaceSlotIcon(cell.Icon, CellBox);

            cell.Count = Theme.MakeText(cell.Go.transform, "Count", Theme.FontCaption,
                Theme.TextCream, TextAlignmentOptions.BottomRight);
            cell.Count.enableWordWrapping = false;
            Theme.Place(cell.Count, 0f, CellBox - 10f, CellBox - 1f, 10f);

            // The keybind label is the bar's whole point - a slot you have to hunt
            // the key for is just a small container.
            cell.Key = Theme.MakeText(cell.Go.transform, "Key", Theme.FontCaption,
                Theme.TextDim, TextAlignmentOptions.TopLeft);
            cell.Key.enableWordWrapping = false;
            cell.Key.text = KeyLabel(index);
            Theme.Place(cell.Key, 2f, 1f, CellBox - 2f, 10f);

            // in your hand: the gold corners the tool belt uses too, standing just
            // outside the frame so they never cover the icon or its numbers
            float o = CellBox * 0.06f;
            cell.Worn = new EquipBrackets(cell.Go.transform, -o, -o, CellBox + 2f * o, CellBox + 2f * o,
                CellBox * 0.22f, CellBox * 0.05f);

            Theme.Place(cell.Box, Inset + index * CellPitch, Inset, CellBox, CellBox);
            return cell;
        }

        // Where the bar IS, for anything that wants to line up with it. The bar is
        // the one surface that never moves and is always on screen, which makes it
        // the only honest thing in this UI to anchor a window to.
        internal static RectTransform Rect
        {
            get { return _root != null ? _root.transform as RectTransform : null; }
        }

        // Where the bar's bottom edge stands: the configured offset, plus room for the
        // second EXP rail beneath it - the character level's rail sits under the bar
        // and the skill level's under that (InventoryWindow.PlacePlayerExperience),
        // so the bar moves up slightly to make room.
        internal const float RailRoom = 3.5f;
        private const float OffsetY = 4f;   // above the bottom edge, bottom-centre

        internal static float Lift
        {
            get { return OffsetY + RailRoom; }
        }

        private static void Position()
        {
            var rect = _root.transform as RectTransform;
            if (rect == null)
            {
                return;
            }
            // Bottom-centre, on its own terms. Following the height vanilla's strip
            // used was the wrong instinct — that position was chosen for a row of 18
            // storage slots, and this is a shorter bar of a different kind. Sitting
            // at the bottom edge also leaves the space above it clear for the bags
            // rather than landing in the middle of it.
            rect.anchoredPosition = new Vector2(0f, Lift);
        }

        private static string KeyLabel(int index)
        {
            return index == 9 ? "0" : (index + 1).ToString();
        }

        // ---- per-frame ---------------------------------------------------------

        private static void Refresh()
        {
            for (int i = 0; i < Cells.Count && i < Bound.Count; i++)
            {
                Cell cell = Cells[i];
                string key = Bound[i];
                if (string.IsNullOrEmpty(key))
                {
                    // EMPTY READS AS EMPTY, as the backpack marks an occupied cell
                    // by its background. The bags' rule and the bags' dial (EmptySlotAlpha): an
                    // empty cell lets the world through, a bound one has its ground.
                    cell.Icon.enabled = false;
                    cell.Count.text = string.Empty;
                    cell.Box.color = new Color(1f, 1f, 1f, InventoryWindow.EmptySlotAlpha);
                    cell.Back.enabled = false;
                    cell.Worn.Shown = false;
                    continue;
                }

                cell.Back.enabled = true;
                ItemData data = _mn.itemMN.FindItem(key);
                cell.Icon.enabled = data != null && data.itemSprite != null;
                int held = CountOf(key);
                ItemSlot worn = Worn(key);
                // NONE LEFT IS A GHOST: a bound item no longer in the inventory
                // shows as its art in grey at 40 percent. The binding stays - you still mean to
                // eat meat - and a worn item is held, so a weapon in hand never ghosts.
                bool none = held == 0 && worn == null;
                if (cell.Icon.enabled)
                {
                    Sprite ghost = none ? Theme.GreyOf(data.itemSprite) : null;
                    cell.Icon.sprite = ghost ?? data.itemSprite;
                    cell.Icon.color = none ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
                }

                // A weapon shows the DURABILITY OF THE ONE IT WILL USE, not how
                // many you own - vanilla's own rule for a weapon slot
                // (StackTextChange prints itemLife in green instead of a stack).
                // It is also what makes the selector visible: you can see it
                // picked the axe at 10 rather than the one at 20. Aggregating
                // things that differ is the category error; showing the chosen
                // one is the fix, and it needs no identity to do it.
                //
                // WORN IS HELD (two weapons on the bar to switch between, and the
                // one just equipped greyed out as if not carried, its durability
                // hidden). Equipping moves it
                // out of both bags into the loadout, where neither count looked.
                // In hand it is the one that matters: its durability is shown, and
                // the cell takes the gold corners (EquipBrackets).
                Hit shown = worn != null ? new Hit { Slot = worn, SlotID = -1 } : Resolve(key);
                bool durable = shown.Slot != null && _mn.inventory != null
                    && _mn.inventory.IsWeaponOrTool(shown.Slot.toolType);
                cell.Count.text = durable
                    ? Mathf.RoundToInt(shown.Slot.itemLife).ToString()
                    : (held > 1 ? held.ToString() : string.Empty);
                // the tool belt's wear colours, outlined: the dark equip green sank
                // into the grass behind the bar
                float fraction = durable && data != null && data.itemLife > 0f
                    ? Mathf.Clamp01(shown.Slot.itemLife / data.itemLife) : 1f;
                cell.Count.color = durable ? Theme.WearInk(fraction) : Theme.TextCream;
                if (!cell.Outlined)
                {
                    cell.Outlined = Theme.OutlineNumber(cell.Count);
                }
                // A binding is a statement of intent, so running out GHOSTS the icon
                // (above) rather than clearing it - you still mean to eat meat, there
                // is just no meat. Clearing would make the bar forget things for you.
                // The frame stays as a bound cell's: the ghost is the one signal.
                cell.Box.color = Color.white;
                cell.Worn.Shown = worn != null;
                // the off hand's corners are blue, so the two hands read apart
                cell.Worn.Colour = InOffHand(worn) ? EquipBrackets.OffHand : EquipBrackets.Gold;
            }

            // Applied AFTER the loop, not inside it: Refresh repaints every cell
            // from the rule above on every frame, so a tint set anywhere earlier
            // would be wiped out before it was ever seen. InventoryWindow restates
            // its press tint after Refresh for exactly this reason; doing it here
            // instead keeps the whole colour decision for a cell in one place.
            ApplyFeedback();
        }

        // The press had no answer at all before this: you pushed a cell and nothing
        // moved until the item was already used, which reads as lag rather than as
        // care. Now the cell darkens under the finger and returns on release, and a
        // press that DID something (or was refused) says so on its way out.
        private static void ApplyFeedback()
        {
            if (_pressedIndex >= 0 && !_dragging && _pressedIndex < Cells.Count
                && Input.GetMouseButton(_pressedButton))
            {
                Image box = Cells[_pressedIndex].Box;
                Color under = box.color;
                // MULTIPLY, so a dim (out-of-stock) cell darkens further instead of
                // brightening back up to the lit tint
                box.color = new Color(under.r * Theme.PressTint.r,
                    under.g * Theme.PressTint.g, under.b * Theme.PressTint.b, under.a);
            }

            if (_flashIndex >= 0 && _flashIndex < Cells.Count)
            {
                if (Time.unscaledTime < _flashUntil)
                {
                    Cells[_flashIndex].Box.color = _flashColor;
                }
                else
                {
                    _flashIndex = -1;
                }
            }
        }

        // The binding rides the cursor as an icon of our own. It is parented to the
        // ROOT canvas and re-raised every frame rather than parented to the bar,
        // because the inventory window lifts vanilla's hoverSlot branch above ours
        // (InventoryWindow.cs:637) and a ghost under that would vanish behind the
        // very panel you are dragging away from.
        private static void UpdateGhost()
        {
            if (!_dragging || string.IsNullOrEmpty(_dragKey))
            {
                if (_ghost != null && _ghost.enabled)
                {
                    _ghost.enabled = false;
                }
                return;
            }

            Canvas canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                return;
            }
            Canvas rootCanvas = canvas.rootCanvas;

            if (_ghost == null)
            {
                var go = Owned.New("IslandUI_ActionBarGhost");
                go.transform.SetParent(rootCanvas.transform, false);
                _ghost = go.AddComponent<Image>();
                _ghost.raycastTarget = false;
                _ghost.preserveAspect = true;
                var gr = (RectTransform)go.transform;
                gr.sizeDelta = new Vector2(CellBox, CellBox);
                gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            }

            ItemData data = _mn != null && _mn.itemMN != null ? _mn.itemMN.FindItem(_dragKey) : null;
            if (data == null || data.itemSprite == null)
            {
                _ghost.enabled = false;
                return;
            }
            _ghost.sprite = data.itemSprite;
            _ghost.enabled = true;
            _ghost.transform.SetAsLastSibling();

            Camera cam = rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)rootCanvas.transform, Input.mousePosition, cam, out local))
            {
                ((RectTransform)_ghost.transform).anchoredPosition = local;
            }
        }

        // THE GESTURE (reworked to match the reference).
        //
        // Both buttons ACTIVATE, and both act on RELEASE. That is not a stylistic
        // choice copied over: acting on press is what made removal need its own
        // button, and a button spent on removal is a button that cannot activate.
        // Releasing frees the left button's HOLD to mean something else - a drag -
        // and drag-off-the-bar is then the natural place for removal to live.
        //
        //   press either button          -> the cell darkens under the finger
        //   release over the same cell   -> activate (or assign, if holding an item)
        //   SHIFT + hold left + move 6px -> the binding lifts onto the cursor
        //   drop on another bar cell     -> the two bindings swap
        //   drop anywhere else           -> the binding is gone
        //   SHIFT + click, no movement   -> the binding is gone (ClickMap)
        //
        // Keys stay on key-DOWN. There is no drag to disambiguate from a keypress,
        // so key-up would buy nothing and cost a frame of latency on every press.
        private static void ReadInput()
        {
            // digits typed into the split dialog or the recipe's counter are a
            // count, not a slot's key
            for (int i = 0; i < Cells.Count && i < Keys.Length && !SplitDialog.IsOpen && !ItemCard.Counting; i++)
            {
                if (Input.GetKeyDown(Keys[i]))
                {
                    Fire(i);
                    Flash(i, FireFlash);
                }
            }

            int hover = CellUnderPointer();
            if (hover >= 0 && !_dragging && hover < Bound.Count)
            {
                ClickMap.Offer(ClickMap.ForBarSlot(!string.IsNullOrEmpty(Bound[hover]), !HandFull()));
            }

            bool downL = Input.GetMouseButtonDown(0);
            bool downR = Input.GetMouseButtonDown(1);
            if ((downL || downR) && hover >= 0 && _pressedIndex < 0)
            {
                _pressedIndex = hover;
                _pressedButton = downL ? 0 : 1;
                _pressPos = Input.mousePosition;
                _dragging = false;
            }

            // The lift. It happens HERE rather than on release so the binding is
            // already on the cursor for the rest of the gesture - the same reason
            // InventoryWindow grabs at the threshold and not at the end.
            if (!_dragging && _pressedIndex >= 0 && _pressedButton == 0
                && Input.GetMouseButton(0) && CanStartDrag()
                && ((Vector2)Input.mousePosition - _pressPos).sqrMagnitude
                    > DragThreshold * DragThreshold)
            {
                _dragging = true;
                _dragFrom = _pressedIndex;
                _dragKey = Bound[_dragFrom];
                Bound[_dragFrom] = null;   // it is on the cursor now, not in the cell
            }

            // A drag that STARTED in a bag and ends over the bar is an assignment,
            // and it has to be caught here because the press never touched us -
            // _pressedIndex is still -1, so the release branch below would ignore
            // it and the item would sit on the cursor waiting for a second click.
            // That extra click was the whole complaint: the drop should place it.
            if (Input.GetMouseButtonUp(0) && _pressedIndex < 0 && hover >= 0 && HandFull())
            {
                Activate(hover);
            }

            if (_pressedIndex >= 0 && Input.GetMouseButtonUp(_pressedButton))
            {
                if (_dragging)
                {
                    EndDrag(hover);
                }
                else if (hover == _pressedIndex)
                {
                    // Shift+click (down and up, no drag) removes the binding; the
                    // same shift held while moving is the lift above (ClickMap)
                    if (_pressedButton == 0 && ClickMap.ForBarSlot(
                            !string.IsNullOrEmpty(Bound[_pressedIndex]), !HandFull()) == ClickMap.Verb.RemoveAction)
                    {
                        Plugin.Log.LogInfo("Action bar: " + Bound[_pressedIndex] + " removed by shift-click");
                        Bound[_pressedIndex] = null;
                    }
                    else
                    {
                        Activate(_pressedIndex);
                    }
                }
                _pressedIndex = -1;
                _pressedButton = -1;
                _dragging = false;
            }
            UpdateGhost();
        }

        // A drag lifts a BINDING, so it needs one to lift and a free hand to lift
        // it with. Holding an item means the gesture in progress is an assignment,
        // and one press may not mean two things.
        //
        // SHIFT gates it, as the reference does. Without a modifier the bar's two
        // most common gestures - press to use, and the small mouse drift that comes
        // free with pressing anything - are separated by six pixels, and losing a
        // binding is not a six-pixel-sized mistake. Requiring shift makes rearrange
        // and remove DELIBERATE while leaving a plain press to mean use, always.
        private static bool CanStartDrag()
        {
            return ShiftHeld()
                && _pressedIndex >= 0 && _pressedIndex < Bound.Count
                && !string.IsNullOrEmpty(Bound[_pressedIndex])
                && !HandFull();
        }

        // Only the LIFT is gated. Once a binding is on the cursor the gesture is
        // already committed, so letting go of shift mid-drag does not drop it - the
        // modifier says "I meant to start this", not "keep meaning it".
        private static bool ShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool HandFull()
        {
            ItemSlot held = _mn != null && _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            return held != null && held.gameObject.activeSelf
                && !string.IsNullOrEmpty(held.itemKey);
        }

        private static void Activate(int index)
        {
            // Holding something means the click is an ASSIGNMENT, not a press. The
            // held item is NOT consumed and NOT placed - binding copies the key and
            // leaves the stack alone, because a bar slot is a reference and taking
            // the item would make it a container.
            //
            // But the grab that brought it to the cursor already moved it out of
            // its slot, so leaving it there would make every assignment cost a
            // second gesture to put the item back. The assign undoes the grab.
            ItemSlot held = _mn.inventory != null ? _mn.inventory.hoverSlot : null;
            if (held != null && held.gameObject.activeSelf && !string.IsNullOrEmpty(held.itemKey))
            {
                if (Assign(index, held.itemKey))
                {
                    InventoryWindow.ReturnHeldToSource();
                    Flash(index, FireFlash);
                }
                else
                {
                    // Refused, and the binding goes home the same way - by us, now,
                    // with the refresh that goes with it. Left to the game it went
                    // back on its own and the window never heard (a caged unit
                    // dragged to the bar popped back into the steel cage without
                    // triggering the steel cage's update).
                    InventoryWindow.ReturnHeldToSource();
                    Flash(index, RefuseFlash);
                }
                return;
            }
            Fire(index);
            Flash(index, FireFlash);
        }

        // Drop on a cell and the two bindings SWAP; drop on the cell it came from
        // and nothing happened; drop anywhere else and the binding is gone. The
        // last one is the reference's removal gesture, and it is why activation had
        // to move off the buttons' press in the first place.
        private static void EndDrag(int hover)
        {
            if (hover < 0 || hover >= Bound.Count)
            {
                Plugin.Log.LogInfo("Action bar: " + _dragKey + " dragged off the bar");
            }
            else if (hover == _dragFrom)
            {
                Bound[_dragFrom] = _dragKey;
            }
            else
            {
                Bound[_dragFrom] = Bound[hover];
                Bound[hover] = _dragKey;
            }
            _dragKey = null;
            _dragFrom = -1;
        }

        private static void Flash(int index, Color color)
        {
            _flashIndex = index;
            _flashColor = color;
            _flashUntil = Time.unscaledTime + FlashSeconds;
        }

        private static int CellUnderPointer()
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                var rect = Cells[i].Go.transform as RectTransform;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(
                        rect, Input.mousePosition, null))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Bind the held item to a bar slot. Called from the click handler.</summary>
        internal static bool Assign(int index, string itemKey)
        {
            if (index < 0 || index >= Bound.Count || string.IsNullOrEmpty(itemKey))
            {
                return false;
            }
            ItemData data = _mn != null && _mn.itemMN != null ? _mn.itemMN.FindItem(itemKey) : null;
            if (data == null || !ItemUse.HasAction(data))
            {
                // Refused rather than accepted-and-inert: UseItem's switch has no
                // case for Item/ResourceW, so a bound slot would look identical to
                // a broken one on every press.
                Plugin.Log.LogInfo("Action bar: " + itemKey + " has no action to bind");
                return false;
            }
            Bound[index] = itemKey;
            return true;
        }

        // ---- firing ------------------------------------------------------------

        private static void Fire(int index)
        {
            if (index < 0 || index >= Bound.Count || _mn == null || _mn.itemMN == null)
            {
                return;
            }
            string key = Bound[index];
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            Hit hit = Resolve(key);
            if (hit.Slot == null)
            {
                return;   // the dim state already said this would happen
            }
            // The bar always acts as YOU (D27), so staging through the Shared Bag
            // is always allowed here: both containers are yours. That is the whole
            // reason the bar may not reach an NPC's bag — staging out of one would
            // move their item into yours and use it as you.
            if (ItemUse.RefusedOnYou(_mn, hit.Slot))
            {
                Flash(index, RefuseFlash);
                return;
            }
            if (!ItemUse.Use(_mn, hit.Slot, hit.SlotID, Guarded, true))
            {
                Plugin.Log.LogInfo("Action bar: " + key + " could not be used from here");
                Flash(index, RefuseFlash);
            }
        }

        // THE TARGET GUARD, and it is not optional. Both leaves of UseItem resolve
        // `menuNPC ?? player` (UseEquipItem directly, UseConsumables in its first
        // synchronous segment), so with an NPC focused a bar press would equip or
        // FEED THEM. window-system.md D8: your own surfaces never retarget, and a
        // bar you pressed is unambiguously yours.
        //
        // Nulling the field is safe here for a reason worth writing down: every
        // read that matters happens before the first yield, so StartCoroutine has
        // already captured the target by the time the call returns. If a future
        // build moves that read after a yield this silently starts feeding NPCs
        // again - which is exactly an earlier failure mode, so it is called out in
        // docs/action-bar.md as a thing to re-check after an update.
        private static void Guarded(System.Action use)
        {
            Firing = true;
            try
            {
                ItemUse.OnYou(_mn, use);   // D141: now every plain use's rule
            }
            finally
            {
                Firing = false;
            }
        }

        // Where a bound key currently is. SlotID >= 0 means the Shared Bag, which
        // is an index into inventory.itemSlot[] and therefore usable by slotID;
        // SlotID == -1 means the character bag, which is not in that array at all.
        private struct Hit
        {
            public ItemSlot Slot;
            public int SlotID;
        }

        // the player's loadout cell holding this item, if it is worn
        private static ItemSlot Worn(string key)
        {
            CommonStates player = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.common : null;
            if (player == null || player.equip == null)
            {
                return null;
            }
            for (int i = 0; i < player.equip.Length; i++)
            {
                if (player.equip[i] != null && player.equip[i].itemKey == key)
                {
                    return player.equip[i];
                }
            }
            return null;
        }

        // equip[6] is the off hand - a shield or a second tool (CommonStates reads
        // its shieldType there); equip[1] is the main hand
        private const int OffHandSlot = 6;

        private static bool InOffHand(ItemSlot worn)
        {
            CommonStates player = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.common : null;
            return worn != null && player != null && player.equip != null && player.equip.Length > OffHandSlot
                && ReferenceEquals(player.equip[OffHandSlot], worn);
        }

        private static ItemSlot[] CharacterBag()
        {
            CommonStates player = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.common : null;
            return player != null && player.bag != null ? player.bag.slots : null;
        }

        // Resolve by ITEM KEY, never by slot identity - the decision the item marks
        // made, for the same reason: the referenced stack gets eaten and refilled, so a per-stack
        // reference would go dead on the first bite.
        //
        // The SHARED BAG IS SEARCHED FIRST, and that ordering is load-bearing
        // rather than arbitrary: a shared-bag hit fires through UseItem(slotID)
        // untouched, while a character-bag hit may have to be staged (ItemUse).
        // Same item, cheaper path, so prefer it whenever the item is in both.
        private static Hit Resolve(string key)
        {
            var miss = new Hit { Slot = null, SlotID = -1 };
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || inv.itemSlot == null)
            {
                return miss;
            }

            // A binding names a KIND, so when you own two of that kind something
            // has to choose between them - and it cannot be a reference, because
            // there is nothing to refer to. SlotToSlot is copy-then-clear, so an
            // item that moves is destroyed in one cell and rebuilt in another; a
            // pointer would die on the first tidy-up. The choice is therefore a
            // RULE, evaluated fresh on every press.
            //
            // The rule: for anything carrying durability, take the WORST one. You
            // use up the damaged axe and keep the good one in reserve, which is
            // also the only reading under which two axes are meaningfully
            // different at all. Everything else is fungible and takes the first
            // hit, Shared Bag first, because a shared-bag hit skips the staging
            // dance in Fire().
            //
            // Durability items scan BOTH bags before choosing, and can afford to:
            // they are all equip-family, so they fire through UseEquipItem, which
            // takes the slot object and never needs staging.
            Hit first = miss;
            Hit worst = miss;
            float worstLife = float.MaxValue;

            int max = Mathf.Min(inv.baseCount, inv.itemSlot.Length);
            for (int i = 0; i < max; i++)
            {
                ItemSlot slot = inv.itemSlot[i];
                if (slot == null || slot.itemKey != key)
                {
                    continue;
                }
                if (inv.IsWeaponOrTool(slot.toolType))
                {
                    if (slot.itemLife < worstLife)
                    {
                        worstLife = slot.itemLife;
                        worst = new Hit { Slot = slot, SlotID = i };
                    }
                }
                else if (first.Slot == null)
                {
                    first = new Hit { Slot = slot, SlotID = i };
                }
            }
            ItemSlot[] bag = CharacterBag();
            if (bag != null)
            {
                for (int i = 0; i < bag.Length; i++)
                {
                    ItemSlot slot = bag[i];
                    if (slot == null || slot.itemKey != key)
                    {
                        continue;
                    }
                    if (inv.IsWeaponOrTool(slot.toolType))
                    {
                        if (slot.itemLife < worstLife)
                        {
                            worstLife = slot.itemLife;
                            worst = new Hit { Slot = slot, SlotID = -1 };
                        }
                    }
                    else if (first.Slot == null)
                    {
                        first = new Hit { Slot = slot, SlotID = -1 };
                    }
                }
            }
            return worst.Slot != null ? worst : first;
        }

        // The count is what you HAVE, wherever it is - both bags, aggregated. A bar
        // that says 0 while the item is in your pack is worse than no count at all.
        private static int CountOf(string key)
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            int total = 0;
            if (inv != null && inv.itemSlot != null)
            {
                int max = Mathf.Min(inv.baseCount, inv.itemSlot.Length);
                for (int i = 0; i < max; i++)
                {
                    ItemSlot slot = inv.itemSlot[i];
                    if (slot != null && slot.itemKey == key)
                    {
                        total += Mathf.Max(1, slot.stack);
                    }
                }
            }
            ItemSlot[] bag = CharacterBag();
            if (bag != null)
            {
                for (int i = 0; i < bag.Length; i++)
                {
                    if (bag[i] != null && bag[i].itemKey == key)
                    {
                        total += Mathf.Max(1, bag[i].stack);
                    }
                }
            }
            return total;
        }

        internal static void ResetForSceneLoad()
        {
            _root = null;
            Cells.Clear();
            Bound.Clear();
            Firing = false;
            // The gesture does not survive the scene either: the cells it referred
            // to are gone, and a _dragging left true would hold the pointer hostage
            // (DragOwnsThePointer) with nothing on screen to release it over.
            _ghost = null;
            _pressedIndex = -1;
            _pressedButton = -1;
            _dragging = false;
            _dragKey = null;
            _dragFrom = -1;
            _flashIndex = -1;
        }

        // THE NUMBER KEYS ARE THE BAR'S (a hotkey ALSO fired the item in the
        // bag). Vanilla maps 1-9 to the Shared Bag's
        // first nine cells - GameManager.Update into ItemManager.QuickItem, its only
        // caller - so with the bar on, every number key fired twice. While the bar
        // is on, vanilla's quick-use is skipped and the key is the bar's alone.
        [HarmonyLib.HarmonyPatch(typeof(ItemManager), nameof(ItemManager.QuickItem))]
        private static class NumberKeysAreTheBars
        {
            private static bool Prefix(ref System.Collections.IEnumerator __result)
            {
                // ...and never while digits are being typed into the split dialog
                if (!Settings.ActionBar.Value && !SplitDialog.IsOpen && !ItemCard.Counting)
                {
                    return true;
                }
                __result = Nothing();
                return false;
            }

            private static System.Collections.IEnumerator Nothing()
            {
                yield break;
            }
        }
    }
}
