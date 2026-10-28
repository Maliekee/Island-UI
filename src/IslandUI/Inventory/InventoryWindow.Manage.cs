using System.Collections.Generic;
using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE MANAGE KEY (Forgejo #52, direction C with #53's A+; docs/dock-actions.md).
    // What you do to a thing as something you BUILT - Move, Scale, Edit, Remove - is
    // never why you picked it, so it left the row: one round key on the card's corner,
    // where a person wears the favourite star, the same on every building and still
    // under any anchoring. Pressed, it opens the list: Move, then Scale and Edit where
    // the thing has them, and Remove last, in red, under a rule - Remove takes a second
    // step at last (target-matrix G6's misclick). A wreck has Repair alone, which leads
    // the row instead (ReadThingActs), so a wreck usually has no key.
    //
    // THE LIST'S LOOK (Forgejo #142): not the Talk bubble's paper, #52's interim, but a
    // stem of round keys rising out of this one (ManageFan), and the key wears Move's
    // glyph, so it and its first option are one. ShowManageList is the one place that
    // picks the look. Each row presses through vanilla as the row's keys did
    // (PressThingAct), on the reach rule they had: Move and Remove on build range, Scale
    // and Edit on the thing's.
    internal static partial class InventoryWindow
    {
        private static readonly List<KeptAct> ManageActs = new List<KeptAct>();

        // board px on the card: the star's centre, and a key a little larger than its 54
        // (the stem's keys hang from it at its scale)
        internal const float ManageKeyS = 64f;
        private const float ManageGlyphS = 38f;
        private static Image _manageHit, _manageSocket, _manageGlyph;
        private static readonly object _manageOwner = new object();

        private static bool ManageKeyUp => _unitThing != null && ManageActs.Count > 0;

        // the dock is drawn while it has anything: an act or a door on the row, or the
        // Manage key - a torch of yours is its card and its name, and no plank
        private static bool DockUp => _unitRow != null && (_unitRow.Any || ManageKeyUp);

        // Vanilla's prop panel's Manage acts, by the state PropAction switches on: a
        // BuildScaler's Scale (and a hatch's Edit of its size), a PositionEdit's Edit
        private static bool ManageState(int state)
        {
            return state == 27 || state == 32;
        }

        // SCALE AND EDIT, OPENED BY VANILLA OR NOT, as Move and Remove are: a thing of
        // yours with no container that is not a bed has them where it carries a
        // BuildScaler or a PositionEdit (UIManager.PropPanelTypeChange: text 35 state
        // 27, text 41 state 32) - so a Tabbed decoration's list has them before you
        // walk up, and a press opens vanilla and presses through
        private static void PredictManage(ItemInfo prop)
        {
            ItemData data = _mn.itemMN != null ? _mn.itemMN.FindItem(prop.itemKey) : null;
            if (data == null || data.itemType != ItemData.ItemType.Prop || data.subType == ItemData.SubType.Bed
                || prop.GetComponent<InventorySlot>() != null)
            {
                return;
            }
            if (prop.GetComponent<BuildScaler>() != null) { Predict(35, 27); }
            if (prop.GetComponent<PositionEdit>() != null) { Predict(41, 32); }
        }

        // Move first, Remove last, the rest in vanilla's order between
        private static int ManageOrder(KeptAct act)
        {
            return act.Name == "MoveButton" ? 0 : act.Name == "RemoveButton" ? 2 : 1;
        }

        private static void SortManage()
        {
            var sorted = new List<KeptAct>(ManageActs);
            ManageActs.Clear();
            for (int rank = 0; rank <= 2; rank++)
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (ManageOrder(sorted[i]) == rank) { ManageActs.Add(sorted[i]); }
                }
            }
        }

        // ---- the key on the card ----------------------------------------------------

        private static void BuildManageKey()
        {
            Transform t = _animeIdentity.transform;
            _manageHit = AnimeHud.Image(t, "ManageHit", "mask");
            _manageHit.color = Color.clear;
            _manageHit.raycastTarget = true;
            _manageSocket = Theme.KitImage(_manageHit.transform, "Socket", "kit_emblem_socket_idle", 0, Color.white);
            // Move's glyph, the list's first key (Forgejo #142: "so that it's both an
            // option and the icon of the initial button"); #52's wrench and hammer before
            _manageGlyph = Theme.KitImage(_manageHit.transform, "Glyph", "kit_emblem_move", 0, Color.white);
            _manageSocket.raycastTarget = _manageGlyph.raycastTarget = false;
            float s = ManageKeyS, g = ManageGlyphS;
            AnimePieces.Place(_manageHit, CardStarX - s * .5f, CardStarY - s * .5f, s, s);
            AnimePieces.Place(_manageSocket, 0f, 0f, s, s);
            AnimePieces.Place(_manageGlyph, (s - g) * .5f, (s - g) * .5f, g, g);
            _manageHit.gameObject.SetActive(false);
        }

        private static bool OnManageKey => _manageHit != null && _manageHit.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_manageHit.rectTransform, Input.mousePosition, null);

        // every frame, from the dock's input: up while the thing has a list, lit while
        // it is open - the green of a door whose window is up - warm under the pointer
        private static void PaintManageKey(bool hot, bool down)
        {
            if (_manageHit == null)
            {
                return;
            }
            bool up = ManageKeyUp;
            if (_manageHit.gameObject.activeSelf != up) { _manageHit.gameObject.SetActive(up); }
            if (!up)
            {
                return;
            }
            bool open = ManageOpen;
            Theme.KitSkin(_manageSocket, open ? "kit_emblem_socket_on" : hot ? "kit_emblem_socket_hot" : "kit_emblem_socket_idle", 0);
            float k = down ? .8f : 1f;
            _manageSocket.color = _manageGlyph.color = new Color(k, k, k, 1f);
        }

        // ---- the list ---------------------------------------------------------------

        // either look's, while it is this key's
        private static bool ManageOpen => ManageFan.OpenFor(_manageOwner) || TalkBubble.ListOpenFor(_manageOwner);

        internal static void ToggleManage()
        {
            ItemInfo prop = _unitThing;
            if (prop == null || ManageActs.Count == 0)
            {
                return;
            }
            ShowManageList(prop);
        }

        // THE ONE PLACE THAT CHOOSES THE LIST'S LOOK: the stem of keys (Forgejo #142).
        // The Talk bubble's paper, #52's interim, takes the same spec (TalkBubble.ToggleList).
        private static void ShowManageList(ItemInfo prop)
        {
            ManageFan.Toggle(_mn, new TalkBubble.ListSpec
            {
                Owner = _manageOwner,
                Title = Shared.Loc.Get("unit.manage", "Manage"),
                Anchor = () => _manageHit != null ? _manageHit.rectTransform : null,
                Read = rows => ReadManageList(prop, rows),
                Ended = () => ManageEnded(prop),
            });
        }

        private static void ReadManageList(ItemInfo prop, List<TalkBubble.ListItem> rows)
        {
            bool building = ReachRule.LivePlacement(_mn, prop);
            bool live = ReachRule.LiveProp(_mn, prop);
            for (int i = 0; i < ManageActs.Count; i++)
            {
                KeptAct act = ManageActs[i];
                rows.Add(new TalkBubble.ListItem
                {
                    Name = act.Name,
                    Label = act.Label,
                    Icon = act.Placement ? PlacementDockIcon(act.Name) : ManageIcon(act.State),
                    Danger = act.Name == "RemoveButton",
                    Enabled = act.Placement ? building : live,
                    Press = () => PressThingAct(prop, act),
                    Why = () => CloserBy(new ActRow.Slot { Name = act.Name }),
                });
            }
        }

        // Scale (a BuildScaler's, and a hatch's Edit of its size) and Edit (a
        // PositionEdit's), by the state vanilla's button switches on, never its words
        private static Sprite ManageIcon(int state)
        {
            return Theme.Kit(state == 27 ? "kit_emblem_scale" : "kit_emblem_edit", 0);
        }

        // the list ends with its thing: another target, none, or its key gone
        private static string ManageEnded(ItemInfo prop)
        {
            if (prop == null || !prop.gameObject.activeInHierarchy)
            {
                return "thing gone";
            }
            if (!ReferenceEquals(_unitThing, prop) || !ReferenceEquals(FocusProp(Focus()), prop))
            {
                return "target changed";
            }
            return ManageKeyUp && _unit != null && _unit.activeInHierarchy ? null : "nothing to manage";
        }

        // For the harness: the key's list as the dock has it, and the key's centre in
        // screen pixels from the top left, for a real click (post-input.ps1)
        internal static string ManageDump()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ManageKeyUp ? "key" : "no key");
            if (_manageHit != null && _manageHit.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                _manageHit.rectTransform.GetWorldCorners(corners);
                Vector2 c = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
                sb.Append(" at ").Append(c.x.ToString("0")).Append(',').Append((Screen.height - c.y).ToString("0"));
            }
            for (int i = 0; i < ManageActs.Count; i++)
            {
                sb.Append(" | ").Append(ManageActs[i].Name).Append(" \"").Append(ManageActs[i].Label).Append('"');
            }
            sb.Append(ManageOpen ? " | open" : " | shut");
            return sb.ToString();
        }

        // ...and the stem's keys as drawn, each with its centre for a click
        internal static string ManageStemDump()
        {
            return ManageFan.Dump();
        }

        // ...and the stem on the target's key with a Scale and a dim Edit after its
        // Move, which save 17 has no thing for: the glyphs, the dim key and its answer.
        // They only log when pressed.
        internal static string ManageStemProbe()
        {
            ItemInfo prop = _unitThing;
            if (prop == null || !ManageKeyUp)
            {
                return "no key";
            }
            ManageFan.Toggle(_mn, new TalkBubble.ListSpec
            {
                Owner = _manageOwner,
                Title = "Manage (probe)",
                Anchor = () => _manageHit != null ? _manageHit.rectTransform : null,
                Read = rows =>
                {
                    ReadManageList(prop, rows);
                    int at = rows.Count > 0 && rows[0].Name == "MoveButton" ? 1 : 0;
                    rows.Insert(at, new TalkBubble.ListItem
                    {
                        Name = "act32", Label = "Edit", Icon = ManageIcon(32), Enabled = false,
                        Press = () => Plugin.Log.LogInfo("stem probe: Edit"), Why = () => "out of reach · 2.0 m closer",
                    });
                    rows.Insert(at, new TalkBubble.ListItem
                    {
                        Name = "act27", Label = "Scale", Icon = ManageIcon(27),
                        Press = () => Plugin.Log.LogInfo("stem probe: Scale"),
                    });
                },
                Ended = () => ManageEnded(prop),
            });
            return ManageFan.Dump();
        }
    }
}
