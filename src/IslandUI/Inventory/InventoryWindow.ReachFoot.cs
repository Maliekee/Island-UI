using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE VICINITY WINDOW'S FOOT: the three 'all in', 'all out' and 'stack'
    // buttons at the bottom, working as they do for a chest but on every chest in
    // the vicinity - and a closer way to control how vicinity chests behave than
    // the Island UI menu's configuration window.
    //
    // One band, the chest window's act row. On the right the chest's own three,
    // done across every chest and material storage the window lists; on the left
    // the families as chips that step Hidden -> Shown -> Stack, the settings
    // window's glyphs brought to the window they govern (the same settings, so
    // either place sets both).
    //
    // A permanent object, parented into the pooled plate that reserves its height
    // on each bind, and hidden at the start of every rebind so the plate can go on
    // to another job without carrying it.
    internal static partial class InventoryWindow
    {
        private const float FootChipSize = 15f;
        private const float FootChipGap = 2.5f;
        private static GameObject _reachFoot;
        private static ActRow _reachFootRow;
        private static KitTooltip _reachFootTip;
        private static bool _reachFootBound;
        private static int _footChipHot = -1;
        private static int _footChipPressed = -1;

        private sealed class FootChip
        {
            public ContainerPolicy.Family Family;
            public string Art;
            public Image Back;
            public Image Icon;
            public Image Mark;
        }

        private static readonly List<FootChip> FootChips = new List<FootChip>();

        // the verbs' ids: GameObject names and the log; the words shown are ReachVerbLabel
        private static readonly string[] ReachVerbWords = { "All in", "Stack", "All out" };

        private static string ReachVerbLabel(int which)
        {
            switch (which)
            {
                case 0: return Shared.Loc.Get("reach.all_in", "All in");
                case 1: return Shared.Loc.Get("reach.stack", "Stack");
                default: return Shared.Loc.Get("reach.all_out", "All out");
            }
        }

        private static float ReachFootH()
        {
            if (IsVicinity(GroupReach))
            {
                return VicFootH;
            }
            return _reachFootRow != null ? _reachFootRow.Height : ActRow.H;
        }

        // THE COMPACT OPTION (facelift look 02): a checkbox and its word in the foot's
        // gap between the chips and the verbs - the view, never the storage
        private static Image _compactBox;
        private static TMPro.TextMeshProUGUI _compactWord;
        private static Image _footRule;
        private static bool _compactHot;
        private static bool _compactPressed;

        private static void EnsureReachFoot()
        {
            if (_reachFoot != null)
            {
                return;
            }
            FootChips.Clear();
            _reachFoot = Owned.New("ReachFoot");
            _reachFoot.transform.SetParent(_root.transform, false);
            _reachFootRow = ActRow.Create(_reachFoot.transform, "Verbs");
            _reachFootRow.Compact = true;
            _reachFootRow.RightCount = 3;
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                _reachFootRow.Set(ActRow.Sockets - 3 + i, new ActRow.Slot
                {
                    Name = "reach" + ReachVerbWords[i],
                    Label = ReachVerbLabel(i),
                    Press = () => ReachAct(which),
                });
            }
            AddFootChip(ContainerPolicy.Family.Materials, "tent_spider");
            AddFootChip(ContainerPolicy.Family.Chests, "chest_01");
            AddFootChip(ContainerPolicy.Family.Breeding, "planter_01");
            AddFootChip(ContainerPolicy.Family.Benches, "bench_wood");
            AddFootChip(ContainerPolicy.Family.Bags, null);
            AddFootChip(ContainerPolicy.Family.Bodies, "kit_emblem_skull");
            _footRule = Theme.KitImage(_reachFoot.transform, "Rule", "kit_cross", 0, Facelift.VicFootRule);
            _footRule.sprite = null;
            _footRule.type = Image.Type.Simple;
            _compactBox = Facelift.Make(_reachFoot.transform, "Compact", "vicinity-checkbox-off-idle");
            _compactBox.raycastTarget = true;
            _compactWord = Facelift.Text(_reachFoot.transform, "CompactWord", false, ActRow.VicWordPx, Facelift.VicCaption,
                TMPro.TextAlignmentOptions.MidlineLeft);
            _reachFoot.SetActive(false);
        }

        // a chip's name and what it covers, read at use
        private static string FootChipLabel(ContainerPolicy.Family family)
        {
            switch (family)
            {
                case ContainerPolicy.Family.Materials: return Shared.Loc.Get("reach.chip.materials", "Material storages");
                case ContainerPolicy.Family.Chests: return Shared.Loc.Get("reach.chip.chests", "Chests and boxes");
                case ContainerPolicy.Family.Breeding: return Shared.Loc.Get("reach.chip.breeding", "Cages, traps, planters");
                case ContainerPolicy.Family.Benches: return Shared.Loc.Get("reach.chip.benches", "Workbench strips");
                case ContainerPolicy.Family.Bags: return Shared.Loc.Get("reach.chip.bags", "Your people's bags");
                default: return Shared.Loc.Get("reach.chip.bodies", "Bodies");
            }
        }

        private static string FootChipAbout(ContainerPolicy.Family family)
        {
            switch (family)
            {
                case ContainerPolicy.Family.Materials: return Shared.Loc.Get("reach.chip.materials.help", "The storages a workbench counts as its own stock.");
                case ContainerPolicy.Family.Chests: return Shared.Loc.Get("reach.chip.chests.help", "Wooden and iron chests, boxes, the mine's output.");
                case ContainerPolicy.Family.Breeding: return Shared.Loc.Get("reach.chip.breeding.help", "Cages, traps, planters, hives, poultry boxes, mushroom logs, herb boxes.");
                case ContainerPolicy.Family.Benches: return Shared.Loc.Get("reach.chip.benches.help", "Each workbench's own ten-slot strip.");
                case ContainerPolicy.Family.Bags: return Shared.Loc.Get("reach.chip.bags.help", "The bags of the people you employ, within reach.");
                default: return Shared.Loc.Get("reach.chip.bodies.help", "The pockets of the downed and the dead, within reach.");
            }
        }

        private static void AddFootChip(ContainerPolicy.Family family, string art)
        {
            var chip = new FootChip { Family = family, Art = art };
            chip.Back = Theme.KitImage(_reachFoot.transform, "Chip_" + family, "kit_chip_walnut", 0, Color.white);
            chip.Back.raycastTarget = true;
            var iconGo = Owned.New("Icon");
            iconGo.transform.SetParent(chip.Back.transform, false);
            chip.Icon = iconGo.AddComponent<Image>();
            chip.Icon.preserveAspect = true;
            chip.Icon.raycastTarget = false;
            var markGo = Owned.New("Mark");
            markGo.transform.SetParent(chip.Back.transform, false);
            chip.Mark = markGo.AddComponent<Image>();
            chip.Mark.preserveAspect = true;
            chip.Mark.raycastTarget = false;
            chip.Mark.enabled = false;
            FootChips.Add(chip);
        }

        // At the start of every rebind: the plate it sat in may be handed to
        // something else, and only BindReachFoot puts it back.
        private static void HideReachFoot()
        {
            _reachFootBound = false;
            if (_reachFoot != null && _reachFoot.activeSelf)
            {
                _reachFoot.SetActive(false);
            }
            if (_reachFootTip != null && _reachFootTip.Visible)
            {
                _reachFootTip.Hide();
            }
        }

        // Bound BEFORE the spacer, so the column's bottom-up stack puts it under
        // the list, at the window's foot.
        private static void BindReachFoot(float width)
        {
            EnsureReachFoot();
            Plate plate = TakePlate();
            plate.Column = ReachColumn;
            plate.Group = GroupReach;
            plate.Container = null;
            plate.Owner = null;
            plate.Prop = null;
            plate.IsGroupFrame = false;
            plate.IsTitle = false;
            _lastGroupBound = GroupReach;
            _prevMemberLoadout = false;
            _prevMemberShared = false;
            plate.Shown = 0;
            plate.Cols = 0;
            plate.LaidOutCols = -1;
            HideCells(plate);
            for (int i = 0; i < plate.TypeIcons.Count; i++)
            {
                if (plate.TypeIcons[i].gameObject.activeSelf)
                {
                    plate.TypeIcons[i].gameObject.SetActive(false);
                }
            }
            if (plate.Header.gameObject.activeSelf)
            {
                plate.Header.gameObject.SetActive(false);
            }
            for (int i = 0; plate.Rows != null && i < plate.Rows.Count; i++)
            {
                if (plate.Rows[i].Go.activeSelf) { plate.Rows[i].Go.SetActive(false); }
            }
            plate.RowsShown = 0;
            plate.Back.enabled = false;
            plate.Edge.enabled = false;

            // the chips' run, kept clear of the verbs
            bool vic = IsVicinity(GroupReach);
            float chipsW = FootChips.Count * (FootChipSize + FootChipGap) + ActRow.GroupGap;
            _reachFootRow.LeftReserve = chipsW;
            _reachFootRow.Vicinity = vic;
            var rect = (RectTransform)_reachFoot.transform;
            if (rect.parent != plate.Window.Content)
            {
                rect.SetParent(plate.Window.Content, false);
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _reachFootRow.Show(true);
            // on the leather: the rule over the foot, and its controls 23 px under it
            // on the 39 px buttons' line (.vicinity-footer)
            float rowY = vic ? 23f * VicPx : 0f;
            _reachFootRow.Layout(vic ? VicFootX : 0f, rowY, width);
            float h = vic ? VicFootH : _reachFootRow.Height;
            SetShown(_footRule, vic);
            SetShown(_compactBox, vic);
            SetShown(_compactWord, vic);
            if (vic)
            {
                Theme.Place(_footRule, -10f * VicPx, 0f, 760f * VicPx, VicPx);
                // .compact-option: the 17 px box, 8 px, the word - centred in the
                // gap between the chips and the verbs rather than at the design's
                // x = 380, which the verbs reach since their words grew to the
                // roster's size
                string word = Shared.Loc.Get("reach.compact", "Compact");
                if (_compactWord.text != word)
                {
                    _compactWord.text = word;
                }
                float wordW = _compactWord.GetPreferredValues(word).x;
                float optionW = 25f * VicPx + wordW;
                float chipsEnd = VicFootX + FootChips.Count * (VicChipW + VicChipGap) - VicChipGap;
                float cx = Mathf.Max(chipsEnd + 8f * VicPx,
                    chipsEnd + (_reachFootRow.VicLeft - chipsEnd - optionW) * 0.5f);
                Theme.Place(_compactBox, cx, rowY + (VicButtonH - 17f * VicPx) * 0.5f, 17f * VicPx, 17f * VicPx);
                Theme.Place(_compactWord, cx + 25f * VicPx, rowY, wordW + 2f * VicPx, VicButtonH);
            }
            for (int i = 0; i < FootChips.Count; i++)
            {
                FootChip chip = FootChips[i];
                if (vic)
                {
                    // .kind-filters: 40 px buttons, 7 apart, their symbol 29 px
                    Theme.Place(chip.Back, VicFootX + i * (VicChipW + VicChipGap), rowY, VicChipW, VicButtonH);
                    Theme.Place(chip.Icon, (VicChipW - VicChipIcon) * 0.5f, (VicButtonH - VicChipIcon) * 0.5f,
                        VicChipIcon, VicChipIcon);
                    float vm = 14f * VicPx;
                    Theme.Place(chip.Mark, VicChipW - vm - 2f * VicPx, VicButtonH - vm - 2f * VicPx, vm, vm);
                }
                else
                {
                    Theme.Place(chip.Back, ActRow.PadX + i * (FootChipSize + FootChipGap),
                        (ActRow.H - FootChipSize) * 0.5f, FootChipSize, FootChipSize);
                    float ih = FootChipSize - 3f;
                    Theme.Place(chip.Icon, 1.5f, 1.5f, ih, ih);
                    const float mk = 6.5f;
                    Theme.Place(chip.Mark, FootChipSize - mk + 1.5f, FootChipSize - mk + 1.5f, mk, mk);
                }
                if (chip.Icon.sprite == null)
                {
                    chip.Icon.sprite = FootChipArt(chip);
                    chip.Icon.enabled = chip.Icon.sprite != null;
                }
                if (chip.Mark.sprite == null)
                {
                    chip.Mark.sprite = Theme.FindSprite("ui_carry_button");
                }
            }
            plate.Height = h;
            plate.Window.Rect.sizeDelta = new Vector2(width, h);
            Theme.Place(plate.Window.Content, 0f, 0f, width, h);
            plate.LaidOutW = width;
            plate.LaidOutH = h;
            _lastColumnWidth[ReachColumn] = Mathf.Max(_lastColumnWidth[ReachColumn], width);
            if (!plate.Window.Go.activeSelf)
            {
                plate.Window.Go.SetActive(true);
            }
            if (!_reachFoot.activeSelf)
            {
                _reachFoot.SetActive(true);
            }
            _reachFootBound = true;
            PaintReachFoot();
        }

        private static Sprite FootChipArt(FootChip chip)
        {
            // the people's bags wear the dock's backpack, the button of your own bag
            if (chip.Art == null)
            {
                return AnimeHud.Icon("bag") ?? OwnBagIcon;
            }
            if (chip.Art.StartsWith("kit_"))
            {
                return Theme.Kit(chip.Art, 0);
            }
            ItemData d = _mn != null && _mn.itemMN != null ? _mn.itemMN.FindItem(chip.Art) : null;
            return d != null && d.itemSprite != null ? d.itemSprite : Theme.FindSprite(chip.Art);
        }

        private static void PaintReachFoot()
        {
            if (!_reachFootBound)
            {
                return;
            }
            bool held = Input.GetMouseButton(0);
            bool vic = IsVicinity(GroupReach);
            for (int i = 0; i < FootChips.Count; i++)
            {
                FootChip chip = FootChips[i];
                ContainerPolicy.FamilyMode mode = ContainerPolicy.Mode(chip.Family);
                bool hot = i == _footChipHot;
                bool down = hot && _footChipPressed == i && held;
                float a;
                if (vic)
                {
                    // A TOGGLE READS ON OR OFF AT A GLANCE (user): only the
                    // symbol's fade told them apart. A shown family is the lit
                    // button in its gold rim (the focus piece); a hidden one sits
                    // sunk in the leather (the pressed piece), its symbol at .45,
                    // and lifts to the hover piece under the pointer.
                    bool on = mode != ContainerPolicy.FamilyMode.Hidden;
                    Facelift.State state = on ? Facelift.State.Focus
                        : hot && !down ? Facelift.State.Hover : Facelift.State.Pressed;
                    if (down && on)
                    {
                        state = Facelift.State.Pressed;
                    }
                    Facelift.Skin(chip.Back, Facelift.Id("vicinity-button", state));
                    chip.Back.color = Color.white;
                    a = mode == ContainerPolicy.FamilyMode.Hidden ? (hot ? 0.6f : 0.45f) : 1f;
                    chip.Icon.color = new Color(1f, 1f, 1f, a);
                    bool stacking = mode == ContainerPolicy.FamilyMode.Stack && ContainerPolicy.SweepBound
                        && chip.Mark.sprite != null;
                    if (chip.Mark.enabled != stacking)
                    {
                        chip.Mark.enabled = stacking;
                    }
                    continue;
                }
                Theme.KitSkin(chip.Back, "kit_shed_block", 14);
                chip.Back.preserveAspect = false;
                float k = down ? 0.8f : 1f;
                chip.Back.color = new Color(k, k, k, 1f);
                // ghosted reads as absent, which is what Hidden is (the settings
                // window's own rule); under the pointer it lifts a little
                a = mode == ContainerPolicy.FamilyMode.Hidden ? (hot ? 0.5f : 0.3f) : 1f;
                chip.Icon.color = new Color(1f, 1f, 1f, a);
                // the sweep's mark, only while there is a sweep to fill it
                bool stacks = mode == ContainerPolicy.FamilyMode.Stack && ContainerPolicy.SweepBound
                    && chip.Mark.sprite != null;
                if (chip.Mark.enabled != stacks)
                {
                    chip.Mark.enabled = stacks;
                }
            }
            if (vic && _compactBox != null)
            {
                bool on = Settings.VicinityCompact.Value;
                Facelift.Skin(_compactBox, on
                    ? (_compactHot ? "vicinity-checkbox-on-focus" : "vicinity-checkbox-on-idle")
                    : (_compactHot ? "vicinity-checkbox-off-focus" : "vicinity-checkbox-off-idle"));
                _compactWord.color = _compactHot ? Facelift.VicInk : Facelift.VicCaption;
            }
            _reachFootRow.Shed = !vic;
            _reachFootRow.Paint();
        }

        // Every frame the vicinity is up. True while the pointer is on the foot, so
        // the press goes nowhere else; mid-drag the foot stands aside, since a
        // release there means "keep it in hand", not a press.
        private static bool TickReachFoot(Camera cam)
        {
            if (!_reachFootBound || _reachFoot == null || !_reachFoot.activeInHierarchy || _dragging)
            {
                if (_reachFootTip != null && _reachFootTip.Visible)
                {
                    _reachFootTip.Hide();
                }
                PaintVicIdentityHover();
                TickVicOrder();
                return false;
            }
            // a holder's picture first: it sits in the list, clear of the foot
            bool onIdentity = TickVicIdentity(cam, out string identityTip);
            PaintVicIdentityHover();
            TickVicOrder();
            int hot = -1;
            for (int i = 0; i < FootChips.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        FootChips[i].Back.rectTransform, Input.mousePosition, cam))
                {
                    hot = i;
                    break;
                }
            }
            _footChipHot = hot;
            // the Compact box and its word are one target
            bool compactHot = _compactBox != null && _compactBox.gameObject.activeInHierarchy
                && (RectTransformUtility.RectangleContainsScreenPoint(_compactBox.rectTransform, Input.mousePosition, cam)
                    || RectTransformUtility.RectangleContainsScreenPoint(_compactWord.rectTransform, Input.mousePosition, cam));
            _compactHot = compactHot;
            if (Input.GetMouseButtonDown(0))
            {
                _compactPressed = compactHot;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_compactPressed && compactHot)
                {
                    ToggleCompact();
                }
                _compactPressed = false;
            }
            bool onRow = _reachFootRow.ReadInput();
            if (Input.GetMouseButtonDown(0))
            {
                _footChipPressed = hot;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_footChipPressed >= 0 && _footChipPressed == hot)
                {
                    ContainerPolicy.Cycle(FootChips[hot].Family);
                    OnConfigChanged();
                }
                _footChipPressed = -1;
            }
            PaintReachFoot();
            if (_reachFootTip == null)
            {
                _reachFootTip = KitTooltip.Create((RectTransform)_root.transform);
            }
            string tip = identityTip != null ? identityTip
                : hot >= 0 ? FootChipHelp(FootChips[hot])
                : _reachFootRow.Hot >= 0 ? ReachVerbHelp(_reachFootRow.Hot - (ActRow.Sockets - 3)) : null;
            _reachFootTip.Tick(tip, cam);
            return hot >= 0 || onRow || compactHot || onIdentity;
        }

        // The view flips; the containers do not. The scroll stays pinned to the
        // holder it was on (_reachAnchor), which the next layout restores.
        private static void ToggleCompact()
        {
            Settings.VicinityCompact.Value = !Settings.VicinityCompact.Value;
            _forceRefresh = true;
            _nextRefresh = 0f;
            Refresh();
        }

        // For the harness: the Compact box, as a click would press it
        internal static bool ReachCompact(bool on)
        {
            if (!_reachFootBound)
            {
                return false;
            }
            if (on != Settings.VicinityCompact.Value)
            {
                ToggleCompact();
            }
            return true;
        }

        private static string FootChipHelp(FootChip chip)
        {
            bool bulk = chip.Family == ContainerPolicy.Family.Materials || chip.Family == ContainerPolicy.Family.Chests;
            string reach = bulk ? Shared.Loc.Get("reach.chip.bulk", "{0} · {1} · {2} reach it.", ReachVerbLabel(0), ReachVerbLabel(1), ReachVerbLabel(2))
                : chip.Family == ContainerPolicy.Family.Breeding ? Shared.Loc.Get("reach.chip.harvest", "{0} takes the harvest; the rest by hand.", ReachVerbLabel(2))
                : Shared.Loc.Get("reach.chip.by_hand", "Listed; move things by hand.");
            string label = FootChipLabel(chip.Family);
            string about = FootChipAbout(chip.Family);
            bool sweep = ContainerPolicy.SweepBound && ContainerPolicy.CanStack(chip.Family);
            ContainerPolicy.FamilyMode mode = ContainerPolicy.Mode(chip.Family);
            if (mode == ContainerPolicy.FamilyMode.Hidden)
            {
                return "<b>" + label + "  ·  " + Shared.Loc.Get("reach.chip.hidden", "hidden") + "</b>\n" + about
                    + "\n" + Shared.Loc.Get("reach.chip.hidden.help", "Not listed here. Click to show.");
            }
            if (mode == ContainerPolicy.FamilyMode.Stack && sweep)
            {
                return "<b>" + label + "  ·  " + Shared.Loc.Get("reach.chip.shown_stack", "shown, quick-stack fills it") + "</b>\n" + about + "\n" + reach
                    + " " + Shared.Loc.Get("reach.chip.shown_stack.help", "The quick-stack key may deposit here too. Click to hide.");
            }
            return "<b>" + label + "  ·  " + Shared.Loc.Get("reach.chip.shown", "shown") + "</b>\n" + about + "\n" + reach
                + " " + (sweep ? Shared.Loc.Get("reach.chip.let_stack", "Click to let the quick-stack key fill it too.") : Shared.Loc.Get("reach.chip.click_hide", "Click to hide."));
        }

        private static string ReachVerbHelp(int which)
        {
            string only = SlotFilter.Vicinity.Active ? "\n" + Shared.Loc.Get("reach.only_filtered", "Only what matches the filter.") : "";
            switch (which)
            {
                case 0:
                    return "<b>" + ReachVerbLabel(0) + "</b>\n" + Shared.Loc.Get("reach.all_in.help", "Everything in your bags goes to the chests listed here: to one already holding it first, then the nearest with room. Favourites stay.") + only;
                case 1:
                    return "<b>" + ReachVerbLabel(1) + "</b>\n" + Shared.Loc.Get("reach.stack.help", "What a listed chest already holds goes to it. Favourites stay.") + only;
                case 2:
                    return "<b>" + ReachVerbLabel(2) + "</b>\n" + Shared.Loc.Get("reach.all_out.help", "Everything in the listed chests comes into your bags, nearest chest first, until they are full. Then the harvest: fish from the traps, eggs, honey, herbs and mushrooms.") + only;
            }
            return null;
        }

        // THE CHEST'S THREE, ACROSS THE VICINITY. Targets are what the window lists
        // of plain storage - chests and material storages in a family that is not
        // hidden, the one you have open included - nearest first. Cages, planters,
        // bench strips and people's bags are never a bulk move's destination
        // (ContainerPolicy.BulkTarget); All out also takes the breeding props'
        // harvest, as the game's Harvest job does (ContainerPolicy.HarvestSource). With the window filtered, only matching
        // items move: the filter is what the window is showing you.
        private static void ReachAct(int which)
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            if (inv == null || _mn.gameMN == null || _mn.gameMN.pMove == null
                || (inv.hoverSlot != null && inv.hoverSlot.gameObject.activeSelf))
            {
                return;
            }
            var targets = new List<InventorySlot>();
            ScanReach(targets);
            // All out also takes the harvest of the breeding props listed
            // (HarvestSource) - not one vanilla has open with its mirror live, whose
            // panel would write the old contents back on closing
            var harvest = new List<InventorySlot>();
            if (which == 2)
            {
                harvest.AddRange(targets);
                harvest.RemoveAll(c => !ContainerPolicy.HarvestSource(c)
                    || (VanillaContainerUI.MirrorsLive(inv) && ReferenceEquals(inv.tmpSubInventory, c)));
            }
            // ...and loots the bodies listed (All out did nothing beside a dead
            // native) - taking is what a body is for; nothing is ever
            // put INTO one by All in or Stack. Not one the game's own search has
            // open, whose mirror would write the pockets back on closing.
            var bodies = new List<InventorySlot>();
            if (which == 2)
            {
                bodies.AddRange(targets);
                bodies.RemoveAll(c => c == null || c.slots == null
                    || ContainerPolicy.FamilyOf(c) != ContainerPolicy.Family.Bodies
                    || (VanillaContainerUI.MirrorsLive(inv) && ReferenceEquals(inv.tmpSubInventory, c)));
            }
            targets.RemoveAll(c => c == null || c.slots == null || !ContainerPolicy.BulkTarget(c));
            if (which == 2)
            {
                targets.AddRange(bodies);
            }
            if (targets.Count == 0 && harvest.Count == 0)
            {
                return;
            }
            FilterField filter = SlotFilter.Vicinity;
            bool filtered = filter.Active;
            CommonStates you = _mn.gameMN.pMove.common;
            ItemSlot[] yours = you != null && you.bag != null ? you.bag.slots : null;
            int moved = 0;
            var log = new List<QuickStack.Deposit>();
            if (which == 2)
            {
                for (int c = 0; c < targets.Count; c++)
                {
                    InventorySlot chest = targets[c];
                    for (int i = 0; i < chest.slots.Length; i++)
                    {
                        ItemSlot slot = chest.slots[i];
                        string key = slot != null ? slot.itemKey : null;
                        if (string.IsNullOrEmpty(key) || (filtered && !filter.Matches(_mn, key)))
                        {
                            continue;
                        }
                        int n = MoveStackOut(inv, slot, yours, out ItemData data);
                        if (n > 0 && QuickStackFlight.SourceOf(_mn, key, out Vector2 landing))
                        {
                            NoteReachFlight(log, data, chest, n, landing, reverse: true);
                        }
                        moved += n;
                    }
                }
                for (int c = 0; c < harvest.Count; c++)
                {
                    InventorySlot prop = harvest[c];
                    for (int i = 0; i < prop.slots.Length; i++)
                    {
                        ItemSlot slot = prop.slots[i];
                        string key = slot != null ? slot.itemKey : null;
                        if (!ContainerPolicy.IsHarvest(prop, key) || (filtered && !filter.Matches(_mn, key)))
                        {
                            continue;
                        }
                        int n = MoveStackOut(inv, slot, yours, out ItemData data);
                        if (n <= 0)
                        {
                            continue;
                        }
                        ContainerPolicy.AfterHarvest(_mn, prop, i);
                        if (QuickStackFlight.SourceOf(_mn, key, out Vector2 landing))
                        {
                            NoteReachFlight(log, data, prop, n, landing, reverse: true);
                        }
                        moved += n;
                    }
                }
            }
            else
            {
                var sources = new List<ItemSlot>();
                for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
                {
                    sources.Add(inv.itemSlot[i]);
                }
                if (yours != null)
                {
                    sources.AddRange(yours);
                }
                for (int s = 0; s < sources.Count; s++)
                {
                    ItemSlot src = sources[s];
                    string key = src != null ? src.itemKey : null;
                    if (string.IsNullOrEmpty(key) || Favourites.IsFavourite(src) || ToolBelt.IsHung(src)
                        || (filtered && !filter.Matches(_mn, key)))
                    {
                        continue;
                    }
                    bool seen = QuickStackFlight.SourceOf(_mn, key, out Vector2 from);
                    // like with like first, in whichever listed chest holds it...
                    for (int c = 0; c < targets.Count && !string.IsNullOrEmpty(src.itemKey); c++)
                    {
                        int n = MoveStack(inv, src, targets[c].slots, targets[c].slots.Length, true, out ItemData data);
                        if (n > 0 && seen)
                        {
                            NoteReachFlight(log, data, targets[c], n, from, reverse: false);
                        }
                        moved += n;
                    }
                    // ...and All in puts what is left in the nearest with room
                    for (int c = 0; which == 0 && c < targets.Count && !string.IsNullOrEmpty(src.itemKey); c++)
                    {
                        int n = MoveStack(inv, src, targets[c].slots, targets[c].slots.Length, false, out ItemData data);
                        if (n > 0 && seen)
                        {
                            NoteReachFlight(log, data, targets[c], n, from, reverse: false);
                        }
                        moved += n;
                    }
                }
            }
            Plugin.Log.LogInfo($"Vicinity {ReachVerbWords[which]}: {moved} item(s) across {targets.Count + harvest.Count} storage(s)"
                + (filtered ? " (filtered '" + filter.Query + "')" : ""));
            if (moved > 0)
            {
                for (int i = 0; i < inv.baseCount && i < inv.itemSlot.Length; i++)
                {
                    inv.StackTextChange(i);
                }
                if (_mn.npcMN != null)
                {
                    _mn.npcMN.CarryWeightCheck();
                }
                if (_mn.sound != null)
                {
                    _mn.sound.GoSound(195, _mn.inventory.transform.position);
                }
                _forceRefresh = true;
                _nextRefresh = 0f;
                // a looted body leaves the list (AddBodies lists only full pockets)
                _reachScanDirty = true;
                QuickStackFlight.Show(_mn, log);
            }
        }

        // One notice per item per chest, as the sweep's: a vicinity move lands in
        // several places, and NoteFlight's one-chest merge would credit the first.
        private static void NoteReachFlight(List<QuickStack.Deposit> log, ItemData item, InventorySlot storage,
            int count, Vector2 screen, bool reverse)
        {
            if (item == null || count <= 0)
            {
                return;
            }
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i].Item == item && log[i].Storage == storage && log[i].Reverse == reverse)
                {
                    log[i].Count += count;
                    return;
                }
            }
            log.Add(new QuickStack.Deposit
            {
                Item = item, Storage = storage, Count = count, FromScreen = screen, Reverse = reverse,
            });
        }

        // For the harness: the foot's verbs by number, as a click would press them
        internal static bool ReachFootAct(int which)
        {
            if (!_reachFootBound || which < 0 || which > 2)
            {
                return false;
            }
            ReachAct(which);
            return true;
        }
    }
}
