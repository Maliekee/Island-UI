using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The heading controls and button row.
    internal static partial class InventoryWindow
    {
        // One line that answers the whole question before anything is opened: how
        // many containers hold it, out of how many are in reach, and how many of
        // the thing there are. The by-item view needed a card and a jump-link to
        // say the same. A cap that only counts is a wall; one that names the way
        // out of itself is a prompt.
        // D44. Verbs, as painted glyphs on the game's own button tile - the
        // family behind ui_carry_button, ui_setting, ui_list, ui_move. That family
        // is real, which is the whole difference from the container icons deleted
        // three days earlier: those were three DIFFERENT languages sharing a
        // gutter (line-art type glyph, photographic item sprite, line art again),
        // and no fourth existed to replace them with. This is one language, and it
        // is already the game's answer to "this is a button".
        //
        // Which is also why the toggles beside them stay words. A row of seven
        // words where three toggle and three act reads as one undifferentiated
        // list; state in type and verbs in paint separates them without a divider,
        // a colour or a box.
        //
        // ONE VERB NOW. The gear that sat beside the cart
        // raised a panel holding the family filter; that filter lives in the
        // mod's settings window (Tab -> Island UI, SettingsWindow.cs) and the
        // gear and its panel are gone. "Nothing hides the gear" was the rule
        // that kept the only way back to the filter on screen; the settings
        // window is that way back now, and nothing hides the game's menu.
        // ...AND NONE NOW: the vicinity window has no stack-all button. G and the button on your own bags remain
        // the way to sweep; the heading row is the filter's alone.
        private static readonly string[] ButtonSprites = { };

        private static Image _hotButton;
        private static Image _pressedButton;
        private static Plate _buttonPlate;

        // Use the final frame geometry after Layout has resolved its height.
        // The shared header stays outside the plate pool, preserving typing focus
        // across refreshes and moving with the frame in the same update.
        private static void PlaceFilter()
        {
            if (_root == null || _mn == null)
            {
                return;
            }
            // THE SLOT'S SHARED FIELD (UI.SlotFilter): in the slot's
            // header row, the same rect the crafting window gives it, attached
            // under this root while the vicinity is up - in root space from the
            // bottom-left like the columns and the tabs
            if (!_reachOpen || !_slotKnown)
            {
                UI.SlotFilter.Hide(_root.transform);
                return;
            }
            if (!UI.SlotFilter.Attach(_root.transform, _mn, _slotLeft + Slot.Inset,
                _slotBottom + (_reachPanel != null ? _reachPanel.rect.height : _slotH) - Slot.PadTop, dark: false, occupant: RosterView ? 2 : 1))
            {
                return;
            }
            // On the roster, the count slot says how many are FOLLOWING out
            // of capacity, which is the number you act on - it decides whether you
            // can take one more. The bare total it used to show is what the
            // sections already add up to in front of you, and it cost the control
            // row a whole line to say "Following n / m" as well. A live filter
            // still wins the slot: while you are searching, what matched is the
            // only thing the number can usefully be.
            _filter.SetCount(_filter.Active
                ? _nearbyMatched + "/" + _nearbyTotal
                : (RosterView
                    ? _rosterFollowers + "/" + _rosterCapacity
                    : _nearbyTotal.ToString()));
        }

        private const float ReachWordW = 36f;   // "In reach" at the heading's size, plus a gap

        private static void BindHeadingControls(Plate plate)
        {
            if (plate == null)
            {
                return;
            }
            BindButtons(plate);
        }

        // THE HEADING KEEPS ITS OWN WIDTH. The chips this row
        // briefly also carried needed ~68 units it did not have, so the title
        // grew - and a title is the widest plate in its group, so the FRAME grew
        // with it, and the scroll mark, which is placed outside the members'
        // rect, ended up inside the widened border. One control taking space it
        // was not given moved three things that had nothing to do with it. Two
        // small glyphs at the right edge cost nothing and take nothing.
        private static void BindButtons(Plate plate)
        {
            if (plate.Buttons == null)
            {
                plate.Buttons = new List<Image>();
                for (int i = 0; i < ButtonSprites.Length; i++)
                {
                    var go = Owned.New("Btn" + i);
                    go.transform.SetParent(plate.Window.Content, false);
                    var img = go.AddComponent<Image>();
                    img.preserveAspect = true;
                    // raycast targets for the same reason the chips are: a drag
                    // released here must read as "over UI" to DropHeldIntoWorld
                    img.raycastTarget = true;
                    plate.Buttons.Add(img);
                }
            }
            Vector2 size = plate.Window.Rect.sizeDelta;
            float bs = Mathf.Min(size.y, 12f);
            const float gap = 3f;
            float total = ButtonSprites.Length > 0
                ? ButtonSprites.Length * bs + (ButtonSprites.Length - 1) * gap
                : 0f;
            float x = size.x - 2f - total;
            for (int i = 0; i < plate.Buttons.Count; i++)
            {
                Image img = plate.Buttons[i];
                Sprite g = Theme.FindSprite(ButtonSprites[i]);
                if (g == null)
                {
                    if (img.gameObject.activeSelf) { img.gameObject.SetActive(false); }
                    continue;
                }
                if (!img.gameObject.activeSelf) { img.gameObject.SetActive(true); }
                img.sprite = g;
                Theme.Place(img, x, (size.y - bs) * 0.5f, bs, bs);
                x += bs + gap;
            }
            _buttonPlate = plate;
            // the heading yields the strip the cluster sits in, and nothing else
            Theme.Place(plate.Header, 1f, 0f,
                Mathf.Max(20f, size.x - total - 6f), size.y);
            PaintButtons();
        }

        // INPUT FEEDBACK BELONGS TO THE FRAME, NOT TO THE REBIND.
        // This used to live inside BindButtons, so a press only
        // showed at the next rebind - and once the rebind interval started
        // scaling with the pool, a big base could sit on half a second of
        // nothing between the finger going down and the tile acknowledging it.
        //
        // The split is the one the file already draws for cells: GEOMETRY changes
        // when the shape changes and STATE changes constantly, so they belong on
        // different clocks. Colour is the cheapest thing here - four assignments
        // - and it is the only part anyone is waiting on.
        private static void PaintButtons()
        {
            Plate plate = _buttonPlate;
            if (plate == null || plate.Buttons == null)
            {
                return;
            }
            bool canStack = QuickStack.CanRun(_mn);
            bool anyDown = Input.GetMouseButton(0);
            for (int i = 0; i < plate.Buttons.Count; i++)
            {
                Image img = plate.Buttons[i];
                if (!img.gameObject.activeSelf)
                {
                    continue;
                }
                // FULL STRENGTH AT REST. It idled dimmed and lit on hover, which
                // makes an untouched button look disabled - and the game's own
                // HUD row does the opposite: those sit at full strength and
                // DARKEN under the finger, so a press reads as the tile going
                // down. Image.color multiplies, which only takes light away, so
                // that is the one direction available anyway.
                bool down = ReferenceEquals(_pressedButton, img)
                    && ReferenceEquals(_hotButton, img) && anyDown;
                float k = down ? Theme.PressTint.r : 1f;
                // ...and genuinely unusable is a third thing, not a resting
                // state: quick-stack cannot run while a vanilla panel owns a
                // container.
                if (i == 0 && !canStack) { k = 0.45f; }
                img.color = new Color(k, k, k, 1f);
            }
        }

        // ---- the header's strip, and the marks on the frame ----

        private static Image HitButton(Camera cam)
        {
            Plate plate = _buttonPlate;
            if (plate == null || plate.Buttons == null
                || !plate.Window.Go.activeInHierarchy)
            {
                return null;
            }
            for (int i = 0; i < plate.Buttons.Count; i++)
            {
                Image img = plate.Buttons[i];
                if (!img.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)img.transform, Input.mousePosition, cam))
                {
                    return img;
                }
            }
            return null;
        }

        private static void PressButton(Image img)
        {
            Plate plate = _buttonPlate;
            if (plate == null || plate.Buttons == null)
            {
                return;
            }
            switch (plate.Buttons.IndexOf(img))
            {
                case 0:
                    // The quick-stack button, home at last. It was once deleted
                    // for having "no home rather than a control in the
                    // wrong home" - and this is the home: the pool it fills is
                    // exactly what this window is showing.
                    QuickStack.TryExecute(_mn);
                    _nextRefresh = 0f;
                    Refresh();
                    break;
            }
        }

        private static string ReachCaption()
        {
            // No arrow in the TEXT. BindStyledTitle already draws the state as a
            // sprite for exactly this reason — its own comment says the font has
            // no triangle glyphs and renders them as tofu boxes.
            //
            // Just the count. The heading used to print a RANGE - "3-7 of 14" -
            // because the panel only ever held a slice and the numbers were the
            // only way to say how big the rest was. Everything is bound now and the
            // window scrolls over all of it, so the range would be reporting the
            // scroll position, which the mark down the edge says better and without
            // arithmetic. The focused container is shown unconditionally, so it is
            // not part of this count.
            // With a query up, the count has to say what it is counting. "In
            // reach 2" while standing in a base of fourteen chests is not a
            // smaller number, it is a wrong one.
            // THE FILTER TAKES THIS ROW when it exists, and the count goes inside
            // it. Overlaying the caption was the first attempt and it read as two
            // things fighting for one row (the vicinity search overlapped the
            // title) -- a well can cover a word, but not the half-pixel
            // of it that sits proud of the plate, and covering is not the same as
            // replacing anyway.
            //
            // THE WORD STAYS (the same kind of search UI for the vicinity as for
            // the crafting): "In reach", then the field beside
            // it carrying the count, the cart at the row's end - the crafting row's
            // grammar. Only the number moves into the field.
            if (PlateNamed())
            {
                return "";   // the plate says it; the field carries the count
            }
            if (_filter.Alive)
            {
                return "In reach";
            }
            return $"In reach  {_nearbyTotal}";
        }
    }
}
