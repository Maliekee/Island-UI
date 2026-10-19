using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Reach: the fold/unfold sections and the position-cached scan.
    internal static partial class InventoryWindow
    {
        // ---------- reach sections: fold and unfold ----------
        private const float SectionArrowW = 8f;
        private static readonly HashSet<int> CollapsedReach = new HashSet<int>();

        private static void EnsureSectionParts(Plate plate)
        {
            if (plate.SectionArrow != null)
            {
                return;
            }
            var go = Owned.New("SectionArrow");
            go.transform.SetParent(plate.Window.Content, false);
            plate.SectionArrow = go.AddComponent<Image>();
            plate.SectionArrow.sprite = Theme.Triangle;
            plate.SectionArrow.raycastTarget = false;
            plate.SectionArrow.color = Theme.Ink;
            var hit = Owned.New("HeaderHit");
            hit.transform.SetParent(plate.Window.Content, false);
            plate.HeaderHit = (RectTransform)hit.transform;
        }

        // A click on a section's header folds or unfolds it. Hit-tested before the
        // cells; the header row never overlaps a cell, so hovering it takes
        // nothing from them, and only a release on the row acts.
        private static bool ClickSectionHeaders(Camera cam)
        {
            if (!Input.GetMouseButtonUp(0) || _dragging)
            {
                return false;
            }
            for (int i = 0; i < _reachPlatesUsed && i < ReachPlates.Count; i++)
            {
                Plate p = ReachPlates[i];
                if (p.HeaderHit == null || !p.HeaderHit.gameObject.activeInHierarchy
                    || p.Container == null)
                {
                    continue;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(p.HeaderHit, Input.mousePosition, cam))
                {
                    int id = p.Container.GetInstanceID();
                    if (!CollapsedReach.Remove(id))
                    {
                        CollapsedReach.Add(id);
                    }
                    Invalidate();
                    return true;
                }
            }
            return false;
        }

        private const float CloseChipSize = CloseChip.Size;
        private const float CloseChipFromRight = CloseChip.FromRight;
        private static Plate _pressedClose;

        private const float TitlePlateX = 9f;
        // THE NAME ON A PLATE (a tab sign on the target and player windows, so
        // the name sits on the title tab in the same paint as before; a plain tab
        // looked wrong): the bag's and the target's heading leave the frame and hang HALF IN over
        // its top rail as the hand-cut oak plate, the way the settings window's
        // title does, with the character badge, the name and its brush splash on
        // the wood. The frame's top is the plate's middle; the plate is drawn
        // over it, as a plate is.
        private const float PlateH = 16f;
        private const float PlateShoulder = 10f;   // from the plate's end to past its nail
        // THE GUTTER GLYPH, CENTRED IN THE LEFT BAND (the inventory windows had a
        // little too much margin on the left):
        // the band is the frame's inset plus the gutter, and the glyph sits in
        // the middle of it - partly left of the plate's own edge - instead of
        // hanging at the gutter's start with all its air on one side. The gutter
        // is narrower for it (GutterWidth 16 -> 12): the cells moved in by four,
        // the glyph by seven, and the air it has is now the same on both sides.
        private const float GutterGlyph = 14f;

        private static float GutterGlyphX(float gutterW)
        {
            return -GroupPadX() + (GroupPadX() + gutterW - GutterGlyph) * 0.5f;
        }
        // A FRAME WITH A NAME PLATE IS NEVER NARROWER THAN FOUR CELLS (a two-slot
        // cage's frame left the plate room for "Iron Fe..."); the plate
        // may be that wide too, whatever its column measured last time.
        // THE LOADOUT'S SEPARATOR IS HEAVIER (a more prominent line between the
        // loadout and the rest of the containers): where a group crosses the loadout boundary, the stroke gains
        // 2 units of thickness and the gap gains this much air.
        private const float LoadoutAir = 3f;
        private static bool _prevMemberLoadout;
        // the dotted seam
        private const float ScorePeriod = 4.4f;   // one punched hole and its gap, in units (T1)
        private const float ScoreTilePx = 28f;    // kit_score is one square tile of that period
        private const float ScoreH = 4.4f;        // the tile's height in units: the hole (2) sits centred
        private const float ScoreInset = 1.5f;    // the dotted seam's distance from the frame's inner edge
        private const int PlateMinCells = 4;
        private static float PlateFrameMinW()
        {
            return PlateMinCells * CellPitch - (CellPitch - CellBox);
        }
        // ...AND THE PLATE IS AS WIDE AS ITS NAME (at
        // four cells "Iron Fence (Large Animals)" still cropped to "Iron Fence
        // ..."): the plate takes what the name needs, up to the bags' ten cells,
        // and the frame grows to hold it with the close chip's corner kept clear.
        private const float PlateChipReserve = 16f;
        private static float PlateMaxW()
        {
            return MaxPlateColumns * CellPitch - (CellPitch - CellBox);
        }
        private const float PlateSink = PlateH * 0.5f;   // how far the plate hung into the frame (before the head)
        // THE HEAD: the name plate grown into the target
        // frame - as wide as the frame, ABOVE its rail with daylight between, the
        // name row with the chips at its end, the gauge row under it for a person
        // in focus. Share the column, not the frame: the parchment is only ever the
        // aspect the tabs swap.
        private const float HeadGap = 3f;
        private const float HeadGaugeH = SheetStripH;

        private static bool TabTitle(int group)
        {
            return group == GroupOwn || group == GroupCharacter || group == GroupBag;
        }
        private static KitPlateTabs _slotTabs;

        // The slot's tab row while one of OUR windows holds the slot (D53):
        // "Crafting" walnut, "In reach" and "Roster" oak. Picking Crafting opens
        // the crafting window, which closes this one; the other two swap the list
        // in place, since they are the same frame with a different question asked
        // of it. A permanent object under the root, placed over the slot's top
        // rail at each layout, never a pooled plate's child.
        private static void PlaceSlotTabs(bool show)
        {
            if (!show)
            {
                if (_slotTabs != null && _slotTabs.Rect.gameObject.activeSelf)
                {
                    _slotTabs.Rect.gameObject.SetActive(false);
                }
                return;
            }
            if (_slotTabs == null)
            {
                _slotTabs = KitPlateTabs.Create(_root.transform, Slot.TabNames, 1, index =>
                {
                    if (index == 0 && _mn != null && _mn.uiMN != null)
                    {
                        _mn.uiMN.LeftPanelState(1); // the crafting window; TryHandleOpen closes this one
                        return;
                    }
                    ShowSlotView(index == 2 ? ViewRoster : ViewReach);
                }, KitPlateTabs.Style.Tabs, Slot.TabWoods);
                // placed in root space from the bottom-left, like the columns
                _slotTabs.Rect.anchorMin = Vector2.zero;
                _slotTabs.Rect.anchorMax = Vector2.zero;
            }
            int want = _slotView == ViewRoster ? 2 : 1;
            if (_slotTabs.Active != want)
            {
                _slotTabs.SetActive(want);   // Paint costs a skin per tab; only on a change
            }
            _slotTabs.Rect.anchoredPosition = new Vector2(_slotLeft + TitlePlateX,
                _slotBottom + _slotH + _slotTabs.YOffset);
            if (!_slotTabs.Rect.gameObject.activeSelf)
            {
                _slotTabs.Rect.gameObject.SetActive(true);
            }
            _slotTabs.Rect.SetAsFirstSibling();   // behind the frame plate, as T11 draws them
        }

        // Nearest first, so "the first one" means "the closest one" — which is the
        // only part of WHERE a name can carry. Two chests of the same kind share a
        // name and nothing else tells them apart.
        // WHAT IS NEAR YOU CHANGES WHEN YOU MOVE, not ten times a second
        // (reported as a stall in a base with many chests).
        //
        // The rebind has to run at 10Hz - contents change from crafting, from
        // NPCs, from vanilla moves - but the SCAN behind it does not, and the scan
        // is where the cost is. Per refresh it was an OverlapSphere, a
        // GetComponent per hit, a second GetComponent for the BreedInfo the engine
        // test reads, and `buildMN.OwnCheck` - which is `List.Contains` over
        // everything the player has ever built, walked once per hit. In a mature
        // base that last one alone is tens of thousands of reference comparisons
        // ten times a second, for an answer that cannot change until you walk.
        //
        // So it is cached against POSITION, with a one-second ceiling so a chest
        // built or broken beside you still appears without a step. The ceiling is
        // what keeps this a cache rather than a second source of truth.
        private static readonly List<InventorySlot> ReachScan = new List<InventorySlot>();
        // Measured band heights, by container. Only ever read to decide whether a
        // band is worth painting; a miss means "paint it", so a stale entry costs
        // one frame of a band being lean when it should not be, and never a wrong
        // layout - the heights the STACK uses are the ones just measured.
        private static readonly Dictionary<int, float> ReachHeights =
            new Dictionary<int, float>();
        private static Vector3 _reachScanAt = new Vector3(1e9f, 1e9f, 1e9f);
        private static float _reachScanAge = -1e9f;
        private static bool _reachScanDirty = true;

        internal static void InvalidateReachScan()
        {
            _reachScanDirty = true;
        }

        // Does this container hold anything answering the current query? Stops at
        // the first hit, and every test after the first item of a kind is a
        // dictionary lookup on the matcher's memo — so the whole pool filter costs
        // one pass over slots that are already in memory, not a search per chest.
        private static bool HoldsMatch(InventorySlot container)
        {
            if (container == null || container.slots == null)
            {
                return false;
            }
            for (int i = 0; i < container.slots.Length; i++)
            {
                ItemSlot slot = container.slots[i];
                if (slot != null && !string.IsNullOrEmpty(slot.itemKey)
                    && _filter.Matches(_mn, slot.itemKey))
                {
                    return true;
                }
            }
            return false;
        }

        // For the harness: what the reach rule (D145) says about everything of
        // yours nearby - one line each: name, distance / reach, live.
        internal static string ReachReport()
        {
            if (_mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return "no player";
            }
            Vector3 here = _mn.gameMN.pMove.transform.position;
            var sb = new System.Text.StringBuilder();
            sb.Append("construction range ").Append(ReachRule.Construction(_mn).ToString("0.0")).Append(" m\n");
            var seen = new HashSet<InventorySlot>();
            foreach (Collider hit in Physics.OverlapSphere(here, ReachRule.Widest(_mn) + 4f, LayerMask.GetMask("BG")))
            {
                InventorySlot c = hit.GetComponent<InventorySlot>();
                float reach = ReachRule.Of(_mn, c);
                if (c == null || !seen.Add(c) || reach <= 0f)
                {
                    continue;
                }
                sb.Append(c.name).Append(" [").Append(c.type).Append("] ")
                    .Append(Vector3.Distance(here, c.transform.position).ToString("0.0")).Append(" / ")
                    .Append(reach.ToString("0.0")).Append(ReachRule.Live(_mn, c) ? "  live" : "").Append('\n');
            }
            List<CommonStates> friends = _mn.npcMN != null ? _mn.npcMN.friendList : null;
            for (int i = 0; friends != null && i < friends.Count; i++)
            {
                CommonStates who = friends[i];
                if (who == null || who == _mn.gameMN.pMove.common || !who.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float d = Vector3.Distance(here, who.transform.position);
                if (d <= 12f)
                {
                    sb.Append(who.charaName).Append(" [person] ").Append(d.ToString("0.0")).Append(" / ")
                        .Append(ReachRule.Near.ToString("0.0")).Append(ReachRule.Live(_mn, who) ? "  live" : "").Append('\n');
                }
            }
            return sb.ToString();
        }

        private static void ScanReach(List<InventorySlot> into)
        {
            Vector3 here = _mn.gameMN.pMove.transform.position;
            if (_reachScanDirty
                || (here - _reachScanAt).sqrMagnitude > 0.25f
                || Time.unscaledTime - _reachScanAge > 1f)
            {
                ReachScan.Clear();
                Rescan(here, ReachScan);
                _reachScanAt = here;
                _reachScanAge = Time.unscaledTime;
                _reachScanDirty = false;
            }
            for (int i = 0; i < ReachScan.Count; i++)
            {
                // a cached container can be broken between scans; Unity's null
                // is the destroyed check as well as the missing one
                if (ReachScan[i] != null)
                {
                    into.Add(ReachScan[i]);
                }
            }
        }

        private static void Rescan(Vector3 center, List<InventorySlot> into)
        {
            // the widest reach anything of yours can have; each container is then
            // held to its own kind's (D145)
            Collider[] hits = Physics.OverlapSphere(center,
                ReachRule.Widest(_mn), LayerMask.GetMask("BG"));
            var seen = new HashSet<InventorySlot>();
            var found = new List<InventorySlot>();
            for (int i = 0; i < hits.Length; i++)
            {
                InventorySlot chest = hits[i].GetComponent<InventorySlot>();
                if (chest == null || !seen.Add(chest)
                    || chest.slots == null || chest.slots.Length == 0)
                {
                    continue;
                }
                // D38: one predicate, asked here and in quick-stack and
                // nowhere open-coded. This used to be
                // `type == Material || type == None`, which admitted every breed
                // prop in the game - traps, planters, beehives, pet cages are
                // ALL Type.None - and a write to one of those lands in the slots
                // with nothing acting on it. See ContainerPolicy.
                if (!ContainerPolicy.PoolEligible(chest)
                    || !_mn.buildMN.OwnCheck(chest.GetComponent<ItemInfo>()))
                {
                    continue;
                }
                // ...and only then what is worth seeing (D41). AFTER the safety
                // gate and never folded into it: a filter that could widen the
                // set would be a preference switching off a callback.
                if (!ContainerPolicy.FamilyShown(ContainerPolicy.FamilyOf(chest)))
                {
                    continue;
                }
                // D145: a special crafting storage at 5 m, everything else of yours
                // at 3 m - construction range when that is larger
                if (!ReachRule.Live(_mn, chest))
                {
                    continue;
                }
                found.Add(chest);
            }
            found.Sort((a, b) =>
                (a.transform.position - center).sqrMagnitude
                    .CompareTo((b.transform.position - center).sqrMagnitude));
            into.AddRange(found);
            if (ContainerPolicy.FamilyShown(ContainerPolicy.Family.Bags))
            {
                AddBags(center, into);
            }
            if (ContainerPolicy.FamilyShown(ContainerPolicy.Family.Bodies))
            {
                AddBodies(center, into);
            }
        }

        // THE POCKETS OF BODIES: enemy corpses looted from the vicinity window -
        // downed and dead bodies with pockets, not the corpses that transform into
        // loot. No list of the
        // game's holds a stranger, so the world is asked, on the characters' layers
        // as Tab asks it. Admitted: a body (ReachRule.Body) that is not yours, not
        // on someone's back, whose pockets the game's own click would open
        // (PocketsOpen - never one that turns into loot), with something in them.
        // Bodies lie still, so these follow the bags nearest first.
        private static void AddBodies(Vector3 center, List<InventorySlot> into)
        {
            Collider[] hits = Physics.OverlapSphere(center, ReachRule.Near,
                LayerMask.GetMask("NPC", "BGNPC"));
            var seen = new HashSet<CommonStates>();
            var bodies = new List<CommonStates>();
            for (int i = 0; i < hits.Length; i++)
            {
                CommonStates who = hits[i].GetComponentInParent<CommonStates>();
                if (who == null || !seen.Add(who) || !who.gameObject.activeInHierarchy
                    || who.dead == 5 || who.bag == null || who.bag.slots == null
                    || !ContainerPolicy.IsBodyBag(who.bag) || !PocketsOpen(who)
                    || !HoldsAnything(who.bag) || !ReachRule.Live(who, center, ReachRule.Near))
                {
                    continue;
                }
                bodies.Add(who);
            }
            bodies.Sort((a, b) =>
                (a.transform.position - center).sqrMagnitude
                    .CompareTo((b.transform.position - center).sqrMagnitude));
            for (int i = 0; i < bodies.Count; i++)
            {
                into.Add(bodies[i].bag);
            }
        }

        private static bool HoldsAnything(InventorySlot bag)
        {
            for (int i = 0; i < bag.slots.Length; i++)
            {
                if (bag.slots[i] != null && !string.IsNullOrEmpty(bag.slots[i].itemKey) && bag.slots[i].stack > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // THE PEOPLE'S BAGS (window-system.md D35's second scan). The
        // scan above cannot find them: it masks the BG layer, and a character's
        // bag is a colliderless child of the character. So they come from the
        // roster's own source - your people within the radius - and follow the
        // containers, by NAME: people walk, and a list ordered by their distance
        // would reshuffle under the pointer while you stood still.
        private static void AddBags(Vector3 center, List<InventorySlot> into)
        {
            NPCManager npcs = _mn.npcMN;
            if (npcs == null || npcs.friendList == null)
            {
                return;
            }
            CommonStates player = _mn.gameMN.pMove != null ? _mn.gameMN.pMove.common : null;
            var people = new List<CommonStates>();
            List<CommonStates> yours = People(npcs);   // the partner too (Partner)
            for (int i = 0; i < yours.Count; i++)
            {
                CommonStates who = yours[i];
                if (who == null || who == player || !who.gameObject.activeInHierarchy
                    || (who.employ != CommonStates.Employ.Friend && who.employ != CommonStates.Employ.Favorite)
                    || who.dead == 5 || who.bag == null || who.bag.slots == null || who.bag.slots.Length == 0
                    || !ReachRule.Live(who, center, ReachRule.Near))
                {
                    continue;
                }
                people.Add(who);
            }
            people.Sort((a, b) => string.CompareOrdinal(a.charaName, b.charaName));
            for (int i = 0; i < people.Count; i++)
            {
                into.Add(people[i].bag);
            }
        }

        // A person's bag is captioned by the person. A body says whether it is
        // down or dead, and a stranger with no name is still a body
        private static string BagName(CommonStates owner)
        {
            if (!ReachRule.Body(owner) || !ContainerPolicy.IsBodyBag(owner.bag))
            {
                return owner.charaName;
            }
            string name = string.IsNullOrEmpty(owner.charaName) ? "Body" : owner.charaName;
            return name + (owner.life > 0.0 ? " · down" : " · dead");
        }

        private static CommonStates BagOwner(InventorySlot container)
        {
            return ContainerPolicy.IsCharacterBag(container)
                ? container.transform.parent.GetComponent<CommonStates>() : null;
        }

        // Your own bands wear an icon in their gutter, as the ones in reach do.
        private static Sprite ContainerIcon(Sprite sprite)
        {
            return sprite;
        }

        // YOUR BAG'S SIGN IS A BACKPACK OF OUR OWN DRAWING (the game's
        // ui_backpack through the mask builder came out a white block): white
        // with the ink contour like the hanger and the house. The line-art
        // carried glyph remains the fallback.
        private static Sprite OwnBagIcon =>
            Theme.Kit("kit_bag_own", 0) ?? Theme.CarriedBadge;

        // THE SHARED BAG'S SIGN IS A HOUSE (the storehouse: a place, not a
        // person; one, fixed, open to all). The chest badge remains the fallback.
        private static Sprite SharedBagIcon =>
            Theme.Kit("kit_bag_shared", 0) ?? Theme.ContainerBadge;

        // THE LOADOUT'S SIGN IS THE HANGER (a shirt hanging on its hanger, moss
        // green, in the gutter): the colour and the glyph say "what they wear" together, and
        // the green wash behind the band retired with it. Vanilla's white cloth
        // glyph remains the fallback if the sprite is missing.
        private static Sprite LoadoutIcon =>
            Theme.Kit("kit_loadout_hanger", 0)
                ?? (Theme.SlotTypeIcons != null && Theme.SlotTypeIcons.Length > 2
                    ? Theme.SlotTypeIcons[2] : null);

        private static ItemData ChestItem(InventorySlot chest)
        {
            ItemInfo info = chest.GetComponent<ItemInfo>();
            return info != null ? _mn.itemMN.FindItem(info.itemKey) : null;
        }

        private static Sprite ChestIcon(InventorySlot chest)
        {
            // a person's bag wears the bag window's backpack
            if (BagOwner(chest) != null)
            {
                return OwnBagIcon;
            }
            ItemData item = ChestItem(chest);
            return item != null && item.itemSprite != null
                ? item.itemSprite : Theme.ContainerBadge;
        }

        // With the icon carrying the kind, the name is repetition and the distance
        // is the only part of WHERE a header can say — so with icons on, the label
        // shrinks to the distance alone.
        // The heading of the focused container's own panel. Unlike ChestLabel it
        // always names the thing: a panel headed "3m" says where but never what,
        // and this one is not competing for height with four others.
        // The focused container costs no extra height for its heading - it has a
        // title band either way - but the number there was never worth reading: you
        // reach a container by walking to it, so the answer is always about zero.
        // "The one you are dealing with" is what the panel already says.
        private static string ChestTitle(InventorySlot chest)
        {
            CommonStates owner = BagOwner(chest);
            if (owner != null)
            {
                return BagName(owner);
            }
            ItemData item = ChestItem(chest);
            return item != null ? item.GetItemName() : "Container";
        }

        // A container's own icon says what it is, so only a person's bag carries
        // words: whose it is.
        //
        // No distance either. A caption is not free: wantsText adds Theme.CaptionH to every
        // band in the pool, so five chests spent five captions of height to print
        // five numbers that differ by a metre or two. The ORDER already carries it -
        // the pool is sorted nearest first - and "in reach" is the only distance
        // claim the window actually makes.
        //
        // It may come back once there is a design for it. The thing to avoid
        // repeating is where it lived: under the icon, in the one place that
        // charges every row for it.
        private static string ChestLabel(InventorySlot chest)
        {
            // a bag's backpack says what, never whose: its label is the person
            CommonStates owner = BagOwner(chest);
            return owner != null ? BagName(owner) : "";
        }
    }
}
