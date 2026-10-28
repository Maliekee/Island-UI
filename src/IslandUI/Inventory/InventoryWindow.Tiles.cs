using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — Tiles on the frame rim (favourite, name).
    internal static partial class InventoryWindow
    {
        // The toggle's rule, restated on the carrier (D8): vanilla's
        // NameVisibleButton and FavoriteButton resolve menuNPC, which a column
        // may never do
        private static void ToggleMark(CommonStates who, int which)
        {
            if (who == null || _mn == null || _mn.uiMN == null)
            {
                return;
            }
            if (which == ToggleNameShown)
            {
                _mn.uiMN.NameVisibleState(who, who.nameVisible == null);
            }
            else
            {
                who.employ = who.employ != CommonStates.Employ.Favorite
                    ? CommonStates.Employ.Favorite : CommonStates.Employ.Friend;
                _mn.uiMN.FriendFavCheck(who);
            }
        }

        // ---- the tiles: star and eye on the character frame's top rail ----

        private const float TileGlyph = 8f;
        private const float TileGap = 3f;
        private static readonly List<Plate> _tilePlates = new List<Plate>();
        private static readonly List<CommonStates> _tileWhos = new List<CommonStates>();
        // the same rail, for a thing: exactly one of the two is set per entry
        private static readonly List<ItemInfo> _tileProps = new List<ItemInfo>();
        private static Image _hotTile;
        private static Image _pressedTile;

        // THE TOP RAIL CARRIES YOUR MARKS: the star and the eye, beside the close.
        //
        // It carried the VERBS too for one day (D77) and they are gone, along
        // with the glyphs in the lower right corner. The acts moved into the Overview sheet instead
        // (target-matrix.md G7), which is where they always belonged: both
        // attempts to place them - chips on this rim, then a band at the foot -
        // put them on the FRAME, and acts are CONTENTS.
        //
        // The rail is not finished changing. Placement acts - Move, Remove,
        // Repair - are due to arrive here as a dynamic glyph set (G6) once props
        // enter the window, and the space for them comes from the gauge run
        // leaving the name plate. That is a later slice; today it is two marks.
        private const int MarkTiles = 2;

        private static void EnsureTiles(Plate plate)
        {
            EnsureTiles(plate, MarkTiles);
        }

        private static void EnsureTiles(Plate plate, int need)
        {
            if (plate.Tiles == null)
            {
                plate.Tiles = new List<Image>();
                plate.TileGlyphs = new List<Image>();
            }
            for (int i = plate.Tiles.Count; i < need; i++)
            {
                Image chip = Theme.KitImage(plate.Window.Go.transform, "Tile" + i, "kit_chip_walnut", 0, Color.white);
                chip.raycastTarget = true;
                var rect = chip.rectTransform;
                // Measured, and the measurement is why the acts are not
                // here: the rail is 245 units, the name plate with its icon run
                // takes ~150 and the close, eye and star another 51, leaving 44 for
                // six verbs that wanted 102. The foot was tried next and dropped.
                // The sheet has the room both rails lacked.
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(CloseChipSize, CloseChipSize);
                // left of the close chip, the star first, on the frame's top rail
                rect.anchoredPosition = new Vector2(
                    -CloseChipFromRight - (i + 1) * (CloseChipSize + TileGap), 0f);
                var go = Owned.New("Glyph");
                go.transform.SetParent(chip.transform, false);
                var glyph = go.AddComponent<Image>();
                glyph.raycastTarget = false;
                glyph.preserveAspect = true;
                Theme.Place(glyph, (CloseChipSize - TileGlyph) * 0.5f, (CloseChipSize - TileGlyph) * 0.5f, TileGlyph, TileGlyph);
                plate.Tiles.Add(chip);
                plate.TileGlyphs.Add(glyph);
            }
        }

        // The frame's top rail, right to left: the close cross (its own object),
        // then the eye, then the star.
        private static void ShowTiles(Plate head, CommonStates who, bool star)
        {
            if (head == null || who == null)
            {
                return;
            }
            const int n = MarkTiles;
            EnsureTiles(head, n);
            for (int i = 0; i < head.Tiles.Count; i++)
            {
                bool on = i < n && (i != 0 || star);   // yours wears no star, as vanilla
                if (head.Tiles[i].gameObject.activeSelf != on) { head.Tiles[i].gameObject.SetActive(on); }
            }
            // the pool is shared with the prop rail, which anchors LEFT - so a
            // person's marks put the right anchor back rather than inheriting it
            for (int i = 0; i < head.Tiles.Count; i++)
            {
                var rt = head.Tiles[i].rectTransform;
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = rt.anchorMin;
                Theme.Place(head.TileGlyphs[i], (CloseChipSize - TileGlyph) * 0.5f,
                    (CloseChipSize - TileGlyph) * 0.5f, TileGlyph, TileGlyph);
            }
            // the eye next to the close, the star left of it
            head.Tiles[1].rectTransform.anchoredPosition = new Vector2(
                -CloseChipFromRight - (CloseChipSize + TileGap), 0f);
            head.Tiles[0].rectTransform.anchoredPosition = new Vector2(
                -CloseChipFromRight - 2 * (CloseChipSize + TileGap), 0f);
            HideWear(head);
            _tilePlates.Add(head);
            _tileWhos.Add(who);
            _tileProps.Add(null);
            PaintTiles();
        }

        // MOVE, REMOVE AND REPAIR AS GLYPHS ON THE TOP RAIL (the third
        // placing: glyphs in the top right corner, using the same icons as
        // before).
        //
        // They wear VANILLA'S OWN art, not a kit mark - the blue four-way arrow
        // and the red barred circle a player already reads them by. That is what
        // makes a glyph row work here where it failed for the general acts: these
        // three are always the same three, uniform across every prop, and already
        // have art the player knows. Nothing about them is unpredictable.
        //
        // A prop wears no star and no eye: those are marks you make on a PERSON,
        // and there is no favourite chest.
        // A THING'S RIM (D113: move and remove are consistent across the
        // buildings): the name
        // plate, the close chip, Move · Remove · Repair as vanilla's own glyphs
        // right of centre, and the wear band beside them. What every building
        // shares lives here, always in the same corner; what THIS thing lets you
        // do lives in the act row at the window's foot.
        private static void ShowPropTiles(Plate head, ItemInfo prop)
        {
            if (head == null || prop == null)
            {
                return;
            }
            PropVerbs.ReadPlacement(_mn);
            int n = PropVerbs.Placement.Count;
            EnsureTiles(head, n);
            for (int i = 0; i < head.Tiles.Count; i++)
            {
                bool on = i < n;
                if (head.Tiles[i].gameObject.activeSelf != on) { head.Tiles[i].gameObject.SetActive(on); }
            }
            for (int i = 0; i < n; i++)
            {
                var rt = head.Tiles[i].rectTransform;
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = rt.anchorMin;
                rt.anchoredPosition = new Vector2(
                    -CloseChipFromRight - (i + 1) * (CloseChipSize + TileGap), 0f);
                Theme.Place(head.TileGlyphs[i], 0f, 0f, CloseChipSize, CloseChipSize);
            }
            HideWear(head);   // wear is a body line now, not a rim band
            _tilePlates.Add(head);
            _tileWhos.Add(null);
            _tileProps.Add(prop);
            PaintTiles();
        }

        private static float HeaderControlReserve(int group)
        {
            int count = 0;
            for (int i = 0; i < _platesUsed; i++)
            {
                Plate p = Plates[i]; if (p.Group != group || p.Tiles == null) continue;
                int visible = 0; foreach (var tile in p.Tiles) if (tile.gameObject.activeSelf) visible++;
                count = Mathf.Max(count, visible);
            }
            return PlateChipReserve + count * (CloseChipSize + TileGap) + TitlePlateX;
        }

        private const float WearW = 36f;
        private const float WearH = 3f;

        private static void EnsureWear(Plate plate)
        {
            if (plate.WearTrack != null)
            {
                return;
            }
            plate.WearTrack = Theme.KitImage(plate.Window.Go.transform, "Wear", "kit_hairline", 4, Theme.KitInk);
            plate.WearTrack.raycastTarget = false;
            var rect = plate.WearTrack.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(WearW, WearH);
            plate.WearFill = Theme.KitImage(plate.WearTrack.transform, "Fill", "kit_hairline", 4, Theme.KitCream);
            plate.WearFill.raycastTarget = false;
            Theme.Place(plate.WearFill, 0f, 0f, 0f, WearH);
        }

        private static void HideWear(Plate plate)
        {
            if (plate != null && plate.WearTrack != null && plate.WearTrack.gameObject.activeSelf)
            {
                plate.WearTrack.gameObject.SetActive(false);
            }
        }

        // D73: REACH decides which verbs are live, not how you picked the target.
        // A verb you cannot reach is drawn dim and does not answer, so the window
        // says the same thing about the same person however you came to it. The
        // distance is the rule system's (D145: a person is live at 3 m), the one
        // the roster and the vicinity window ask too.
        private static bool InReach(CommonStates who)
        {
            if (who == null || _mn == null || _mn.gameMN == null || _mn.gameMN.pMove == null)
            {
                return true;
            }
            return ReachRule.Live(_mn, who);
        }

        // Which verbs work at any distance: reading a sheet and sending someone
        // home need no hands, and neither does calling someone to follow or
        // letting them go (Forgejo #87) - vanilla's FollowMe and GoodBye ask
        // only who is in focus, never how far, and its follower list lets one
        // go from anywhere. Everything else wants you standing there.
        private static bool ReachFree(string name)
        {
            return name == "status" || name == "position" || name == "follow" || name == "goodbye";
        }

        private static void PaintTiles()
        {
            for (int t = 0; t < _tilePlates.Count; t++)
            {
                if (_tileProps[t] != null)
                {
                    PaintPropTilesOf(_tilePlates[t]);
                    PaintWear(_tilePlates[t], _tileProps[t]);
                }
                else
                {
                    PaintTilesOf(_tilePlates[t], _tileWhos[t]);
                }
            }
        }

        // Vanilla's own sprite per act, resolved when the placement set was read.
        private static void PaintPropTilesOf(Plate plate)
        {
            if (plate == null || plate.Tiles == null)
            {
                return;
            }
            bool anyDown = Input.GetMouseButton(0);
            var live = PropVerbs.Placement;
            for (int i = 0; i < plate.Tiles.Count; i++)
            {
                Image chip = plate.Tiles[i];
                if (!chip.gameObject.activeSelf || i >= live.Count)
                {
                    continue;
                }
                Image glyph = plate.TileGlyphs[i];
                bool down = ReferenceEquals(_pressedTile, chip) && ReferenceEquals(_hotTile, chip) && anyDown;
                bool over = ReferenceEquals(_hotTile, chip);
                // The chip stays as the HIT TARGET and draws nothing: an Image with
                // no alpha still answers the pointer, so the press keeps working
                // without a plank being visible.
                chip.color = new Color(1f, 1f, 1f, 0f);
                Sprite art = live[i].Icon;
                if (glyph.sprite != art) { glyph.sprite = art; }
                glyph.enabled = art != null;
                // ...so the press and the hover have to be said by the GLYPH now
                float k = down ? Theme.PressTint.r : over ? 1f : 0.88f;
                glyph.color = new Color(k, k, k, 1f);
            }
        }

        // WEAR, AND ONLY WEAR. ItemInfo.life is breeding progress on a farm (G9),
        // and a level's fill is a different field again; those are the body's,
        // where WritePropSheet names them. The rim says how damaged the thing you
        // built is, on every thing that can be damaged, and nothing else.
        private static bool PropWear(ItemInfo prop, out float frac)
        {
            frac = 0f;
            if (prop == null || prop.lifeMax <= 0f)
            {
                return false;
            }
            var slot = prop.GetComponent<InventorySlot>();
            if (slot != null && slot.type == InventorySlot.Type.FactoryBreed)
            {
                return false;
            }
            frac = Mathf.Clamp01(prop.life / prop.lifeMax);
            return true;
        }

        private static void PaintWear(Plate plate, ItemInfo prop)
        {
            if (plate == null || plate.WearTrack == null)
            {
                return;
            }
            float frac;
            bool has = PropWear(prop, out frac);
            if (plate.WearTrack.gameObject.activeSelf != has)
            {
                plate.WearTrack.gameObject.SetActive(has);
            }
            if (has)
            {
                plate.WearFill.rectTransform.sizeDelta = new Vector2(frac * WearW, WearH);
            }
        }

        private static void PaintTilesOf(Plate plate, CommonStates who)
        {
            if (plate == null || plate.Tiles == null || who == null)
            {
                return;
            }
            bool anyDown = Input.GetMouseButton(0);
            for (int i = 0; i < plate.Tiles.Count; i++)
            {
                Image chip = plate.Tiles[i];
                if (!chip.gameObject.activeSelf)
                {
                    continue;
                }
                Image glyph = plate.TileGlyphs[i];
                bool down = ReferenceEquals(_pressedTile, chip) && ReferenceEquals(_hotTile, chip) && anyDown;
                float k = down ? Theme.PressTint.r : 1f;
                bool on = i == 0 ? who.employ == CommonStates.Employ.Favorite : who.nameVisible != null;
                Sprite mark = ToggleSprite(i == 0 ? ToggleFavourite : ToggleNameShown, on);
                chip.color = new Color(k, k, k, 1f);
                if (glyph.sprite != mark) { glyph.sprite = mark; }
                glyph.enabled = mark != null;
                // vanilla's rule for the star: lit when set, a dark ghost when not;
                // the eye swaps sprites instead
                glyph.color = i == 0 && !on ? new Color(0.25f, 0.25f, 0.25f, 1f) : Color.white;
            }
        }

        private static bool TickTiles(Camera cam)
        {
            Image hot = null;
            Plate plate = null;
            CommonStates who = null;
            ItemInfo prop = null;
            for (int t = 0; t < _tilePlates.Count && hot == null; t++)
            {
                Plate p = _tilePlates[t];
                if (p == null || p.Tiles == null || !p.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < p.Tiles.Count; i++)
                {
                    Image chip = p.Tiles[i];
                    if (chip.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                            chip.rectTransform, Input.mousePosition, cam))
                    {
                        hot = chip; plate = p; who = _tileWhos[t]; prop = _tileProps[t];
                        break;
                    }
                }
            }
            _hotTile = hot;
            if (plate == null)
            {
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) { _pressedTile = null; }
                return false;
            }
            if (Input.GetMouseButtonDown(0) && !_dragging)
            {
                _pressedTile = hot;
            }
            if (Input.GetMouseButtonUp(0))
            {
                bool fire = _pressedTile != null && ReferenceEquals(_pressedTile, hot) && !_dragging;
                int which = hot != null ? plate.Tiles.IndexOf(hot) : -1;
                _pressedTile = null;
                if (fire && which >= 0)
                {
                    if (prop != null)
                    {
                        // pressed through vanilla's own button (D65). Re-read first:
                        // Move and Remove swap to Repair the moment the thing breaks.
                        PropVerbs.ReadPlacement(_mn);
                        if (which < PropVerbs.Placement.Count)
                        {
                            PropVerbs.Press(PropVerbs.Placement[which]);
                        }
                    }
                    else
                    {
                        ToggleMark(who, which == 0 ? ToggleFavourite : ToggleNameShown);
                    }
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                    Refresh();
                    return true;
                }
            }
            else if (_pressedTile != null && !Input.GetMouseButton(0))
            {
                _pressedTile = null;
            }
            return hot != null;
        }
    }
}
