using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // THE VICINITY, FACELIFT LOOK 02 (art/ref/window-facelift-v1/vicinity,
    // runtime-ready/HANDOFF.md).
    //
    // The inventory's amber leather fitted to the vicinity: one painted frame, the
    // title and the blank well on its head, and a row per holder - its identity in a
    // column at the left (the target dock's portrait card: a person on their sky
    // under a bar with their name, a thing's own picture on its enamel), and the real cells beside it, sockets
    // dark when empty and grey-brown when taken. No holder headers, no worn slots,
    // never the shared backpack. The foot keeps the six family chips and All in /
    // Stack / All out on the leather's buttons, with the Compact option between.
    //
    // Measured in the design's pixels from the frame's top-left (layout.json) and
    // drawn at Theme.KitPpu, so one reference pixel at 1440p is one sprite pixel.
    // The cells stay the game's 64 px on its pitch, as the inventory's do: with a
    // 64 px identity column the grid still starts at the design's x = 96.
    internal static partial class InventoryWindow
    {
        private const float VicPx = Facelift.Px;
        private const float VicW = 804f * VicPx;
        private const float VicGutter = 64f * VicPx;            // identity column; the grid at x = 96
        private const float VicIdentityX = -8f * VicPx;         // the column's left, from the gutter's (x = 24)
        private const float VicIdentityW = 64f * VicPx;
        private const float VicPictureW = 60f * VicPx;
        private const float VicRowGap = 28f * VicPx;            // 14 under a row, the rule, 13 over the next
        private const float VicRowRuleAt = 14f * VicPx;
        private const float VicBarW = 12f * VicPx;
        private const float VicBarX = 770f * VicPx;             // the track's left, from the frame's
        private const float VicFootH = 67f * VicPx;             // the rule over it to the frame's padding
        private const float VicFootX = -6f * VicPx;             // its buttons from x = 26
        private const float VicFootW = 752f * VicPx;
        private const float VicButtonH = 39f * VicPx;
        private const float VicChipW = 40f * VicPx;
        private const float VicChipGap = 7f * VicPx;
        private const float VicChipIcon = 29f * VicPx;

        // The vicinity's own list, wearing the facelift: the reach group while the
        // slot shows In reach, not the roster that shares its pool.
        private static bool IsVicinity(int group)
        {
            return group == GroupReach && !RosterView;
        }

        // ---------- the frame ----------

        private static void SkinVicinityFrame(Plate plate)
        {
            Facelift.Skin(plate.Back, "vicinity-frame");
            plate.Back.color = Color.white;
            plate.Edge.enabled = false;
            if (plate.Field != null && plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(false);
            }
            if (plate.Mark != null && plate.Mark.gameObject.activeSelf)
            {
                plate.Mark.gameObject.SetActive(false);
            }
            if (plate.Rail != null)
            {
                plate.Rail.enabled = false;
            }
            // the close key inside the frame: 33 px, its centre 32.5 px in from the
            // right edge and 29.5 px down (.close at 755, 13)
            EnsureClose(plate, -29.5f * VicPx, 33f * VicPx, 32.5f * VicPx);
            if (!plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(true);
            }
            plate.Close.transform.SetAsLastSibling();
        }

        private static void PaintVicinityClose(Image close, bool hot, bool down)
        {
            Facelift.Skin(close, Facelift.Id("vicinity-close", Facelift.Pick(false, down, false, false, hot)));
            close.preserveAspect = true;
            if (close.color != Color.white)
            {
                close.color = Color.white;
            }
        }

        // ---------- a holder's identity ----------

        // Whose bag this is, or what thing: a person shows their portrait, a thing
        // its own item picture. A body is a person too.
        private static CommonStates VicHolder(InventorySlot container)
        {
            return BagOwner(container);
        }

        // How tall the identity column stands: the card alone, a person's and a
        // thing's alike. No caption under it - a name there only added height to
        // every row - and a person's name sits on a bar over the card's own top
        // instead (VicNameBar). (The dock's name tab along the row's top was
        // tried: it boxed the row in and read poorly over the cells.)
        private static float VicIdentityH(bool person)
        {
            return VicCardH;
        }

        // A BODY IS A BODY, not a face: a corpse the Bodies chip lists shows the
        // chip's skull on the world marker's grey enamel.
        private static bool VicBody(InventorySlot holder)
        {
            return holder != null && ContainerPolicy.FamilyOf(holder) == ContainerPolicy.Family.Bodies;
        }

        // THE TARGET'S CARD (PaintAnimeIdentity), at the column's size: the board's
        // portrait card, as the dock and the world marker show whatever you point
        // at - a person, or a body, live on the sky of where they stand; a thing's
        // own picture on the enamel in its brass, or brown when it is not yours.
        // Only what it is in the world: no level coin, no star.
        private const float VicCardH = VicPictureW * 198f / 180f;
        private const float VicCardPx = VicPictureW / 180f;   // units per board pixel

        // A PERSON'S NAME, ON A BAR OVER THE CARD'S TOP (user): the name must
        // never move the row or widen the column, and it need not be whole. So a
        // dark bar lies across the card's top edge, inside the card, and the name
        // on it steps down from its size to a floor as it grows long; past the
        // floor it is cut, with dots.
        private const float VicNameBarH = 17f * VicPx;
        private const float VicNameMax = 13f;                 // design pixels
        private const float VicNameMin = 11f;
        private const float VicNameInset = 3f * VicPx;
        private static readonly Color VicNameBarInk = new Color(0.09f, 0.055f, 0.04f, 0.82f);

        private static void EnsureVicIdentity(Plate plate)
        {
            if (plate.VicMount != null)
            {
                return;
            }
            Transform at = plate.Window.Content;
            // the opening: the card's 147 x 165 window at (23, 18), clipping what is behind it
            plate.VicSky = AnimeHud.Image(at, "VicSky", "mask");
            plate.VicSky.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            plate.VicSkyFade = PortraitSky(plate.VicSky);
            var face = Owned.New("Face");
            face.transform.SetParent(plate.VicSky.transform, false);
            plate.VicFace = face.AddComponent<RawImage>();
            plate.VicFace.raycastTarget = false;
            Stretch(plate.VicFace.rectTransform);
            plate.VicFace.uvRect = PortraitFraming;
            plate.VicItem = AnimeHud.Art(plate.VicSky.transform, "Item", "vicinity");
            plate.VicGlyph = Theme.KitImage(plate.VicSky.transform, "Glyph", "kit_emblem_skull", 0, Color.white);
            plate.VicMount = AnimePieces.Image(at, "VicCard", "target_card");
            plate.VicHigher = AnimePieces.Image(at, "Higher", "mark_chevron");
            SetShown(plate.VicHigher, false);
            var bar = Owned.New("VicNameBar");
            bar.transform.SetParent(at, false);
            plate.VicNameBar = bar.AddComponent<Image>();
            plate.VicNameBar.raycastTarget = false;
            plate.VicNameBar.color = VicNameBarInk;
            plate.VicName = Facelift.Text(bar.transform, "Name", true, VicNameMax, Facelift.VicCaption,
                TextAlignmentOptions.Center);
            plate.VicName.enableAutoSizing = true;
            plate.VicName.fontSizeMax = VicNameMax * VicPx;
            plate.VicName.fontSizeMin = VicNameMin * VicPx;
            plate.VicName.overflowMode = TextOverflowModes.Ellipsis;
            plate.VicName.rectTransform.anchorMin = Vector2.zero;
            plate.VicName.rectTransform.anchorMax = Vector2.one;
            plate.VicName.rectTransform.offsetMin = new Vector2(VicNameInset, 0f);
            plate.VicName.rectTransform.offsetMax = new Vector2(-VicNameInset, 0f);
            SetShown(plate.VicNameBar, false);
            plate.VicPlace = new Place();
            SetShown(plate.VicSky, false);
            SetShown(plate.VicMount, false);
        }

        // The identity column, placed: the card, and behind its opening the
        // person on their sky, the thing on its enamel or the body's skull.
        private static void PlaceVicIdentity(Plate plate, CommonStates person, Sprite picture, bool lean)
        {
            EnsureVicIdentity(plate);
            EnsureTitleParts(plate);
            bool shown = !lean && (person != null || picture != null);
            bool body = VicBody(plate.Container);
            if (shown)
            {
                float x = VicIdentityX + 2f * VicPx, top = plate.CellTop, k = VicCardPx;
                Theme.Place(plate.VicMount, x, top, VicPictureW, VicCardH);
                Theme.Place(plate.VicSky, x + 23f * k, top + 18f * k, 147f * k, 165f * k);
                plate.VicSky.transform.SetAsLastSibling();
                plate.VicMount.transform.SetAsLastSibling();
                plate.VicGlyph.enabled = body;
                if (body)
                {
                    // the world marker's body: the skull on its grey enamel
                    plate.VicFace.enabled = false;
                    plate.VicItem.enabled = false;
                    Sprite enamel = Backdrop("card_enamel");
                    if (plate.VicSky.sprite != enamel) { plate.VicSky.sprite = enamel; }
                    plate.VicSky.color = EmblemBody;
                    SetShown(plate.VicSkyFade, false);
                    float icon = Mathf.Min(147f, 165f) * k * .72f;
                    Theme.Place(plate.VicGlyph, (147f * k - icon) * .5f, (165f * k - icon) * .5f, icon, icon);
                }
                else if (person != null)
                {
                    plate.VicFace.texture = Snapshot(person);
                    plate.VicFace.enabled = plate.VicFace.texture != null;
                    plate.VicItem.enabled = false;
                    PaintSky(plate.VicSky, plate.VicSkyFade, person, plate.VicPlace);
                }
                else
                {
                    plate.VicFace.enabled = false;
                    plate.VicItem.sprite = picture;
                    plate.VicItem.enabled = true;
                    AnimePieces.Place(plate.VicItem, (147f - ThingIconS) * .5f, (165f - ThingIconS) * .5f + ThingIconDrop,
                        ThingIconS, ThingIconS, k * AnimePieces.Px);
                    ItemInfo info = plate.Container != null ? plate.Container.GetComponent<ItemInfo>() : null;
                    bool mine = info != null && _mn.buildMN != null && _mn.buildMN.OwnCheck(info);
                    Sprite enamel = Backdrop("card_enamel");
                    if (plate.VicSky.sprite != enamel) { plate.VicSky.sprite = enamel; }
                    plate.VicSky.color = ThingEnamel(mine);
                    SetShown(plate.VicSkyFade, false);
                }
            }
            SetShown(plate.VicSky, shown);
            SetShown(plate.VicMount, shown);
            bool named = shown && !body && person != null;
            if (named)
            {
                Theme.Place(plate.VicNameBar, VicIdentityX + 2f * VicPx, plate.CellTop, VicPictureW, VicNameBarH);
                plate.VicNameBar.transform.SetAsLastSibling();
                string name = person.charaName ?? string.Empty;
                if (plate.VicName.text != name)
                {
                    plate.VicName.text = name;
                }
            }
            SetShown(plate.VicNameBar, named);
            // the held order's arrow on the card's top right corner: this one
            // belongs higher, and goes there once the list is let go
            bool higher = shown && ReachHigher.Contains(plate.Container);
            if (higher)
            {
                PlaceVicHigher(plate);
                plate.VicHigher.transform.SetAsLastSibling();
            }
            SetShown(plate.VicHigher, higher);
            SetShown(plate.Badge, false);
            SetShown(plate.Header, false);
        }

        // a pooled plate that drew a vicinity holder, back to what other skins expect
        private static void ShedVicinity(Plate plate)
        {
            SetShown(plate.VicMount, false);
            SetShown(plate.VicSky, false);
            SetShown(plate.VicHigher, false);
            SetShown(plate.VicNameBar, false);
            SetShown(plate.FreeBack, false);
        }

        // ---------- the held order, shown ----------

        // THE ORDER HOLDS while the pointer is on the list or an item is in hand
        // (OrderReach), so a row never moves under the pointer - and the hold was
        // invisible: you walked up to a bench and it stayed below a chest further
        // off. Two things say it now. A row that belongs higher wears an up arrow
        // on its card (ReachHigher). And when the hold ends the list re-sorts at
        // once, every row gliding from where it stood to its new place, so the
        // reorder is seen happen rather than a jump.
        private static readonly HashSet<InventorySlot> ReachHigher = new HashSet<InventorySlot>();
        private const float HigherBy = 0.25f;    // metres nearer than a row above it
        private const float SlideTime = 0.15f;
        private static readonly Dictionary<InventorySlot, float> SlideTopWas = new Dictionary<InventorySlot, float>();
        private static readonly Dictionary<InventorySlot, float> SlideFrom = new Dictionary<InventorySlot, float>();
        private static readonly Dictionary<InventorySlot, float> SlideAt = new Dictionary<InventorySlot, float>();
        private static int _vicTickFrame = -10;
        private static bool _reachHeldWas;

        // THE ARROW: the dock's painted chevron (mark_chevron, the board's amber),
        // turned to point up, over the card's top right corner - the size of a
        // cell's third, so it is seen from across the list - and bobbing, since
        // a thing that moves is found before a thing that is merely there.
        private const float HigherW = 36f * VicPx;           // across, once turned
        private const float HigherBob = 3f * VicPx;
        private const float HigherHz = 1.6f;

        private static void PlaceVicHigher(Plate plate)
        {
            float s = HigherW / 69f;                         // the piece is 44 x 69, pointing left
            float cx = VicIdentityX + 2f * VicPx + VicPictureW - 8f * VicPx;
            float cy = plate.CellTop + 2f * VicPx
                - HigherBob * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * HigherHz));
            Theme.Place(plate.VicHigher, cx - 22f * s, cy - 34.5f * s, 44f * s, 69f * s);
            AnimePieces.Centre(plate.VicHigher);
            plate.VicHigher.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
        }

        // how far below its place a row is drawn now: from where it was, easing out
        private static float SlideOffset(InventorySlot c)
        {
            if (c == null || !SlideAt.TryGetValue(c, out float at))
            {
                return 0f;
            }
            float t = (Time.unscaledTime - at) / SlideTime;
            if (t >= 1f)
            {
                SlideAt.Remove(c);
                SlideFrom.Remove(c);
                return 0f;
            }
            float rest = 1f - t;
            return SlideFrom[c] * rest * rest * rest;
        }

        // LayoutReachContent, for each row: `top` is how far down the list it
        // starts. A row whose place changed slides from where it was drawn; one
        // new to the list just appears. A list that was not up a frame ago
        // starts fresh, so opening the window never slides anything.
        private static float SlideTo(InventorySlot c, float top, bool live)
        {
            if (c == null || !live)
            {
                return 0f;
            }
            float off = SlideOffset(c);
            if (SlideTopWas.TryGetValue(c, out float was) && Mathf.Abs(was - top) > 0.01f)
            {
                SlideFrom[c] = was + off - top;
                SlideAt[c] = Time.unscaledTime;
                off = SlideFrom[c];
            }
            SlideTopWas[c] = top;
            return off;
        }

        private static bool SlidesLive()
        {
            bool live = !RosterView && Time.frameCount - _vicTickFrame <= 2;
            if (!live)
            {
                SlideTopWas.Clear();
                SlideFrom.Clear();
                SlideAt.Clear();
            }
            return live;
        }

        // Every frame the vicinity is up: the slides drawn, and a hold let go
        // re-sorts now rather than at the next scan.
        private static void TickVicOrder()
        {
            _vicTickFrame = Time.frameCount;
            bool held = ReachOrderHeld();
            if (_reachHeldWas && !held)
            {
                InvalidateReachScan();
                _forceRefresh = true;
                _nextRefresh = 0f;
            }
            _reachHeldWas = held;
            for (int i = 0; i < _reachPlatesUsed && i < ReachPlates.Count; i++)
            {
                Plate p = ReachPlates[i];
                if (p.VicHigher != null && p.VicHigher.gameObject.activeSelf)
                {
                    PlaceVicHigher(p);
                }
            }
            if (RosterView || SlideAt.Count == 0)
            {
                return;
            }
            for (int i = 0; i < _reachPlatesUsed && i < ReachPlates.Count && i < ReachOrder.Count; i++)
            {
                RectTransform r = ReachPlates[i].Window.Rect;
                float y = ReachPlates[i].ReachY - SlideOffset(ReachOrder[i]);
                if (r.anchoredPosition.y != y)
                {
                    r.anchoredPosition = new Vector2(r.anchoredPosition.x, y);
                }
            }
        }

        // ---------- the cells ----------

        // A cell in the vicinity is a cell of your bag (ItemCellFace): the same
        // socket and bed, rim and count. The whole cell at .30 when a search passes
        // it by; the rims are painted each frame (PaintVicinityCells).
        private static void VicinityCell(CellView cell, bool on, bool filled, bool dimmed)
        {
            cell.Vicinity = on;
            if (!on)
            {
                SetShown(cell.CountBack, false);
                return;
            }
            cell.Filled = filled;
            ItemCellFace(cell, filled, false, -1);
            if (dimmed)
            {
                cell.Box.color = new Color(1f, 1f, 1f, Facelift.SearchDim);
            }
            SetShown(cell.CountBack, false);
        }

        // a pooled cell that wore the leather, its count put back as SizeCellParts
        // and the plain path expect it
        private static void ShedVicinityCell(CellView cell)
        {
            if (!cell.Vicinity)
            {
                return;
            }
            cell.Vicinity = false;
            SetShown(cell.CountBack, false);
            if (cell.Rim != null && cell.Rim.gameObject.activeSelf)
            {
                cell.Rim.gameObject.SetActive(false);
            }
            BarCount(cell, false);
            cell.Box.type = Image.Type.Simple;
            SizeCellParts(cell);
        }

        // HOVER, A PRESS AND A SELECTION CHANGE THE RIM, never the socket or bed, as
        // in your bag (PaintCellRim); the free-slots summary wears the state of the
        // cell it stands on.
        private static void PaintVicinityCells()
        {
            if (!_reachOpen || RosterView)
            {
                return;
            }
            for (int p = 0; p < _reachPlatesUsed && p < ReachPlates.Count; p++)
            {
                Plate plate = ReachPlates[p];
                if (!plate.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                for (int i = 0; i < plate.Shown && i < plate.Cells.Count; i++)
                {
                    CellView cell = plate.Cells[i];
                    if (!cell.Vicinity || !cell.Go.activeSelf)
                    {
                        continue;
                    }
                    PaintCellRim(cell);
                    // the summary wears the state of the cell it stands on: a held
                    // item over it lights it as a place to drop
                    if (i == plate.SummaryAt && plate.FreeBack != null && plate.FreeBack.gameObject.activeSelf)
                    {
                        bool pressed = ReferenceEquals(cell, _pressedCell) && !_dragging && Input.GetMouseButton(0);
                        Facelift.State state = Facelift.Pick(false, pressed, false, cell.Highlight.enabled,
                            ReferenceEquals(cell, _leatherHover));
                        Facelift.Skin(plate.FreeBack, Facelift.Id("vicinity-cell-empty", state));
                    }
                }
            }
        }

        // ---------- a holder's picture: a press selects it, a hover names it ----------

        private static Plate _vicIdentityPressed;

        // The holder's picture under the pointer, in the masked list only.
        private static Plate VicIdentityUnder(Camera cam)
        {
            if (!_reachOpen || RosterView || !PointerInReachView(cam))
            {
                return null;
            }
            for (int i = 0; i < _reachPlatesUsed && i < ReachPlates.Count; i++)
            {
                Plate p = ReachPlates[i];
                if (!p.WasVicinity || p.Container == null || !p.Window.Go.activeInHierarchy)
                {
                    continue;
                }
                Image picture = p.VicMount != null && p.VicMount.gameObject.activeSelf ? p.VicMount
                    : p.Badge != null && p.Badge.gameObject.activeSelf ? p.Badge : null;
                if (picture != null && RectTransformUtility.RectangleContainsScreenPoint(
                        picture.rectTransform, Input.mousePosition, cam))
                {
                    return p;
                }
            }
            return null;
        }

        // A PRESS ON A HOLDER SELECTS IT, as a click on it in the world or a pick
        // from Tab does: a person of yours as a roster pick, anyone else held, a
        // thing opened where it stands when it opens from reach. Its name is the
        // hover's. True while the pointer is on one, so the press goes nowhere else.
        private static bool TickVicIdentity(Camera cam, out string tip)
        {
            tip = null;
            Plate over = _dragging ? null : VicIdentityUnder(cam);
            if (Input.GetMouseButtonDown(0))
            {
                _vicIdentityPressed = over;
            }
            if (Input.GetMouseButtonUp(0))
            {
                Plate pressed = _vicIdentityPressed;
                _vicIdentityPressed = null;
                if (pressed != null && ReferenceEquals(pressed, over) && over.Container != null)
                {
                    CommonStates person = BagOwner(over.Container);
                    if (person != null)
                    {
                        TargetPerson(person, "vicinity");
                    }
                    else
                    {
                        TargetFromTab(over.Container.gameObject);
                    }
                    _forceRefresh = true;
                    _nextRefresh = 0f;
                }
            }
            if (over == null)
            {
                return false;
            }
            tip = "<b>" + ChestTitle(over.Container) + "</b>\n"
                + Shared.Loc.Get("reach.identity.select", "Click to select.");
            // the picture lifts under the pointer, a little
            Image picture = over.VicMount != null && over.VicMount.gameObject.activeSelf ? over.VicMount : over.Badge;
            _vicIdentityHot = picture;
            return true;
        }

        private static Image _vicIdentityHot;

        // the lift, painted each frame: the picture under the pointer brighter
        private static void PaintVicIdentityHover()
        {
            for (int i = 0; i < _reachPlatesUsed && i < ReachPlates.Count; i++)
            {
                Plate p = ReachPlates[i];
                if (!p.WasVicinity)
                {
                    continue;
                }
                foreach (Image picture in new[] { p.VicMount, p.Badge })
                {
                    if (picture == null || !picture.gameObject.activeSelf)
                    {
                        continue;
                    }
                    Color want = ReferenceEquals(picture, _vicIdentityHot) ? new Color(1.15f, 1.12f, 1.05f, 1f) : Color.white;
                    if (picture.color != want)
                    {
                        picture.color = want;
                    }
                }
            }
            _vicIdentityHot = null;
        }

        // ---------- the compact view's summary ----------

        // What is free in a container, said once after its taken slots: the empty
        // socket's face with the count over "free", drawn over the cell that stands
        // on the container's first empty real slot - so a drop on it lands there,
        // as on any empty cell, and nothing else about it is a slot of its own.
        private static void PlaceFreeSummary(Plate plate, int free, int at, int cols, float gutterW)
        {
            if (free <= 0)
            {
                SetShown(plate.FreeBack, false);
                return;
            }
            if (plate.FreeBack == null)
            {
                plate.FreeBack = Facelift.Make(plate.Window.Content, "FreeSummary", "vicinity-cell-empty-idle");
                plate.FreeNum = Facelift.Text(plate.FreeBack.transform, "Count", true, 23f, Facelift.VicFree,
                    TextAlignmentOptions.Center);
                plate.FreeWord = Facelift.Text(plate.FreeBack.transform, "Free", false, 12f, Facelift.VicFree,
                    TextAlignmentOptions.Center);
            }
            Theme.Place(plate.FreeBack, gutterW + (at % cols) * CellPitch, (at / cols) * CellPitch, CellBox, CellBox);
            plate.FreeBack.raycastTarget = false;
            plate.FreeBack.transform.SetAsLastSibling();   // over the cell it stands on
            string n = free.ToString();
            if (plate.FreeNum.text != n)
            {
                plate.FreeNum.text = n;
            }
            string word = Shared.Loc.Get("reach.free", "free");
            if (plate.FreeWord.text != word)
            {
                plate.FreeWord.text = word;
            }
            // the figure's 27 px line over the word's 15, centred in the socket
            float top = (CellBox - 42f * VicPx) * 0.5f;
            Theme.Place(plate.FreeNum, 0f, top, CellBox, 27f * VicPx);
            Theme.Place(plate.FreeWord, 0f, top + 27f * VicPx, CellBox, 15f * VicPx);
            SetShown(plate.FreeBack, true);
        }

        // ---------- the scrollbar ----------

        private static void SkinVicinityScroll(Plate window, bool over, bool held)
        {
            Facelift.Skin(window.Scroll, "vicinity-scroll-track");
            window.Scroll.color = Color.white;
            Facelift.Skin(window.ScrollThumb, held ? "vicinity-scroll-thumb-pressed"
                : over ? "vicinity-scroll-thumb-hover" : "vicinity-scroll-thumb-idle");
            window.ScrollThumb.color = Color.white;
        }
    }
}
