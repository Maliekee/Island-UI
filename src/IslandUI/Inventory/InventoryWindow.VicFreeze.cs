using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE VICINITY FREEZES WHOLE while the pointer is on its list (Forgejo #237,
    // window-system.md §6.30). The held order (OrderReach) kept the rows that stayed
    // in range in their places, but a row whose thing walked out of range, or was
    // taken, still left the list - and every row under it moved up under the
    // pointer. Now nothing in the list moves while the pointer is on it:
    //
    //   a row out of range stays where it was, FROZEN: drawn as it was, under a
    //     cold wash with "out of reach" on a dark bar, and no part of it answers a
    //     press - its cells are not cells to the hit test (ScanPlates), its
    //     picture selects nothing (VicIdentityUnder).
    //   a row whose thing is GONE - destroyed, despawned, a pile picked up, a body
    //     emptied - holds too, under a darker wash marked "gone": its plate is not
    //     bound again, so it keeps the picture and the cells it last drew.
    //   a NEW row joins at the BOTTOM, nearest first among the new, so nothing
    //     above it moves.
    //
    // When the pointer leaves, the list settles in one step: the frozen rows drop
    // out, the rest re-sort nearest first, each gliding from where it stood
    // (TickVicOrder's release, SlideTo). The window never grows for it - its
    // height is the slot's - so what is appended below the view is reached with the
    // wheel, as any long list is.
    //
    // ONLY THE DRAWING HOLDS. ReachScan, which All in, Stack, Ctrl's routes and the
    // refresh signature read, is the live scan and nothing else: a frozen row is
    // never a destination and never a source.
    internal static partial class InventoryWindow
    {
        private const int FrozenFar = 1;
        private const int FrozenGone = 2;

        // a frozen row's thing may be a destroyed Unity object, which compares equal
        // to null and to every other destroyed one: rows are told apart by reference
        private sealed class ByReference : IEqualityComparer<InventorySlot>
        {
            public bool Equals(InventorySlot a, InventorySlot b) { return ReferenceEquals(a, b); }
            public int GetHashCode(InventorySlot c) { return RuntimeHelpers.GetHashCode(c); }
        }

        private static readonly ByReference RowIdentity = new ByReference();
        // the list as it was last drawn, frozen rows and all, before the search narrows it
        private static readonly List<InventorySlot> VicHeldRows = new List<InventorySlot>();
        private static readonly Dictionary<InventorySlot, int> VicFrozen =
            new Dictionary<InventorySlot, int>(RowIdentity);

        // FROST over a row out of reach: a pale cold film the row is seen through,
        // milky over the leather and the sockets alike, so the whole row reads as
        // held at a glance - a dark wash over dark sockets did not show. A row that
        // is gone is darkened instead: nothing is there to see through to.
        private static readonly Color FreezeWashFar = new Color(0.74f, 0.82f, 0.88f, 0.42f);
        private static readonly Color FreezeWashGone = new Color(0.05f, 0.035f, 0.03f, 0.72f);
        private const float FreezeWordPx = 15f;
        private const float FreezeTagH = 23f * VicPx;
        private const float FreezeTagPad = 9f * VicPx;

        // the next bind starts from the list's natural order (a new view, a reopen)
        private static void ForgetVicHold()
        {
            VicHeldRows.Clear();
            VicFrozen.Clear();
        }

        // The piles the ground scan must keep as they are: those drawn while the list
        // holds, so one that leaves range keeps its holder and its items (GroundPiles).
        private static IEnumerable<InventorySlot> VicHeldPiles()
        {
            if (!VicHoldNow())
            {
                yield break;
            }
            for (int i = 0; i < VicHeldRows.Count; i++)
            {
                if (GroundPiles.Is(VicHeldRows[i]))
                {
                    yield return VicHeldRows[i];
                }
            }
        }

        // held, and the list was up a moment ago: a window opened under a still
        // pointer starts from its natural order, not the last place it was held
        private static bool VicHoldNow()
        {
            return ReachOrderHeld() && VicHeldRows.Count > 0 && Time.frameCount - _vicTickFrame <= 2;
        }

        // BindReach, on the live scan: while the list holds, every row drawn last
        // time keeps its place - live, or frozen with the reason - and what is new
        // follows them. Never held, the list is the scan.
        private static void HoldVicinityRows(List<InventorySlot> found)
        {
            VicFrozen.Clear();
            if (VicHoldNow())
            {
                var live = new HashSet<InventorySlot>(found, RowIdentity);
                var placed = new HashSet<InventorySlot>(RowIdentity);
                var rows = new List<InventorySlot>(VicHeldRows.Count + found.Count);
                for (int i = 0; i < VicHeldRows.Count; i++)
                {
                    InventorySlot c = VicHeldRows[i];
                    if (!placed.Add(c))
                    {
                        continue;
                    }
                    if (live.Contains(c))
                    {
                        rows.Add(c);
                        continue;
                    }
                    int why = FreezeWhy(c);
                    if (why == 0)
                    {
                        continue;   // its family was switched off: that is asked for
                    }
                    VicFrozen[c] = why;
                    rows.Add(c);
                }
                for (int i = 0; i < found.Count; i++)
                {
                    if (placed.Add(found[i]))
                    {
                        rows.Add(found[i]);   // new: at the bottom, in the scan's order
                    }
                }
                found.Clear();
                found.AddRange(rows);
            }
            VicHeldRows.Clear();
            VicHeldRows.AddRange(found);
        }

        // Why a row the scan no longer lists is frozen: its thing out of range, or
        // gone. 0: it is not held at all (its family's chip was switched off).
        private static int FreezeWhy(InventorySlot c)
        {
            if (c == null)
            {
                return FrozenGone;   // destroyed: a chest broken, a body despawned
            }
            if (!ContainerPolicy.FamilyShown(ContainerPolicy.FamilyOf(c)))
            {
                return 0;
            }
            if (GroundPiles.Is(c))
            {
                return GroundPiles.HoldsLive(c) ? FrozenFar : FrozenGone;
            }
            if (!c.gameObject.activeInHierarchy || c.slots == null || c.slots.Length == 0)
            {
                return FrozenGone;
            }
            CommonStates owner = BagOwner(c);
            if (owner != null && ReachRule.Body(owner) && !HoldsAnything(c))
            {
                return FrozenGone;   // a body emptied: the list only shows one holding something
            }
            return FrozenFar;
        }

        private static int FrozenOf(InventorySlot c)
        {
            return !ReferenceEquals(c, null) && VicFrozen.TryGetValue(c, out int why) ? why : 0;
        }

        // A row that is gone draws as it last drew: the plate that drew it is the
        // next one in the pool, because nothing above it moved, so it is taken
        // without being bound again. Should the pool have shifted under it (a
        // search typed meanwhile), an empty row of the same cells stands in.
        private static void BindGoneRow(InventorySlot row, bool lean, int cols)
        {
            Plate was = _reachPlatesUsed < ReachPlates.Count ? ReachPlates[_reachPlatesUsed] : null;
            if (was != null && ReferenceEquals(was.VicRow, row) && was.Window.Go.activeSelf)
            {
                Plate plate = TakePlate();
                plate.Column = ReachColumn;
                plate.Group = GroupReach;
                plate.WasVicinity = true;
                plate.Container = null;
                _lastGroupBound = GroupReach;
                _lastColumnWidth[ReachColumn] = Mathf.Max(_lastColumnWidth[ReachColumn],
                    plate.Window.Rect.sizeDelta.x);
            }
            else
            {
                // a field read, safe on a destroyed object
                int n = row.slots != null && row.slots.Length > 0 ? row.slots.Length : cols;
                Bind(string.Empty, new ItemSlot[n], n, null, null,
                    false, ReachColumn, cols, null, -1, false,
                    forceLabel: true, group: GroupReach, lean: lean);
            }
            Plate drawn = ReachPlates[_reachPlatesUsed - 1];
            drawn.VicRow = row;
            PaintFreeze(drawn, FrozenGone);
        }

        // The overlay: a wash over the whole row, the picture included, and the
        // reason on a dark bar over the first row of cells.
        private static void PaintFreeze(Plate plate, int why)
        {
            plate.Frozen = why;
            if (why == 0)
            {
                HideFreeze(plate);
                return;
            }
            Transform at = plate.Window.Content;
            if (plate.FreezeWash == null)
            {
                var wash = Owned.New("FreezeWash");
                wash.transform.SetParent(at, false);
                plate.FreezeWash = wash.AddComponent<Image>();
                plate.FreezeWash.raycastTarget = false;
                var tag = Owned.New("FreezeTag");
                tag.transform.SetParent(at, false);
                plate.FreezeTag = tag.AddComponent<Image>();
                plate.FreezeTag.raycastTarget = false;
                plate.FreezeTag.color = VicNameBarInk;
                plate.FreezeWord = Facelift.Text(tag.transform, "Word", true, FreezeWordPx, Facelift.VicCaption,
                    TextAlignmentOptions.Center);
                plate.FreezeWord.rectTransform.anchorMin = Vector2.zero;
                plate.FreezeWord.rectTransform.anchorMax = Vector2.one;
                plate.FreezeWord.rectTransform.offsetMin = Vector2.zero;
                plate.FreezeWord.rectTransform.offsetMax = Vector2.zero;
            }
            Vector2 size = plate.Window.Rect.sizeDelta;
            float left = VicIdentityX;
            Theme.Place(plate.FreezeWash, left, 0f, size.x - left, size.y);
            plate.FreezeWash.color = why == FrozenGone ? FreezeWashGone : FreezeWashFar;
            string word = why == FrozenGone
                ? Shared.Loc.Get("reach.frozen.gone", "gone")
                : Shared.Loc.Get("reach.frozen.far", "out of reach");
            if (plate.FreezeWord.text != word)
            {
                plate.FreezeWord.text = word;
            }
            float w = plate.FreezeWord.GetPreferredValues(word).x + 2f * FreezeTagPad;
            float cellsW = Mathf.Max(w, size.x - plate.CellLeft);
            float rowH = Mathf.Min(CellBox, size.y - plate.CellTop);
            Theme.Place(plate.FreezeTag, plate.CellLeft + (cellsW - w) * 0.5f,
                plate.CellTop + Mathf.Max(0f, (rowH - FreezeTagH) * 0.5f), w, FreezeTagH);
            plate.FreezeWash.transform.SetAsLastSibling();
            plate.FreezeTag.transform.SetAsLastSibling();
            SetShown(plate.FreezeWash, true);
            SetShown(plate.FreezeTag, true);
        }

        private static void HideFreeze(Plate plate)
        {
            SetShown(plate.FreezeWash, false);
            SetShown(plate.FreezeTag, false);
        }

        // For the harness: the first stack of `key` in your bag put down at your
        // feet through the game's own drop (InventoryManager.ItemDrop).
        internal static string HarnessDropHere(string key)
        {
            InventoryManager inv = _mn != null ? _mn.inventory : null;
            for (int i = 0; inv != null && i < inv.baseCount && i < inv.itemSlot.Length; i++)
            {
                if (inv.itemSlot[i] != null && inv.itemSlot[i].itemKey == key)
                {
                    inv.ItemDrop(inv.itemSlot[i]);
                    InvalidateReachScan();
                    return "dropped " + key;
                }
            }
            return "none";
        }

        // For the harness: the bodies whose pockets the vicinity would list, within
        // 120 m: name, where, how far.
        internal static string HarnessBodies()
        {
            if (_mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return "no player";
            }
            Vector3 here = _mn.gameMN.pMove.transform.position;
            var sb = new System.Text.StringBuilder();
            foreach (CommonStates who in Object.FindObjectsOfType<CommonStates>())
            {
                if (who == null || !who.gameObject.activeInHierarchy || who.dead == 5 || who.bag == null
                    || who.bag.slots == null || !ReachRule.Body(who) || !ContainerPolicy.IsBodyBag(who.bag)
                    || !PocketsOpen(who) || !HoldsAnything(who.bag))
                {
                    continue;
                }
                Vector3 at = who.transform.position;
                float d = Vector3.Distance(here, at);
                if (d <= 120f)
                {
                    sb.Append(string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName)
                        .Append(" at ").Append(at.x.ToString("0.0")).Append(',').Append(at.z.ToString("0.0"))
                        .Append(" d=").Append(d.ToString("0")).Append(" | ");
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no bodies";
        }

        // For the harness: the list as drawn, one row a line, frozen rows marked.
        internal static string VicFreezeReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ReachOrderHeld() ? "held" : "free").Append(", ").Append(_reachPlatesUsed).Append(" plates\n");
            for (int i = 0; i < ReachOrder.Count; i++)
            {
                InventorySlot c = ReachOrder[i];
                int why = i < _reachPlatesUsed && i < ReachPlates.Count ? ReachPlates[i].Frozen : -1;
                string name = ReferenceEquals(c, null) ? "?" : c == null ? "(destroyed)" : ChestTitle(c);
                sb.Append(i).Append(' ').Append(name)
                    .Append(why == FrozenFar ? "  FROZEN far" : why == FrozenGone ? "  FROZEN gone" : "")
                    .Append('\n');
            }
            return sb.ToString();
        }
    }
}
