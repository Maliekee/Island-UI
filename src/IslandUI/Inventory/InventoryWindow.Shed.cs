using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IslandUI.UI;

namespace IslandUI
{
    // THE TOOL SHED'S CONTAINERS AND ROSTER (window-system.md 6.30, D134), what is
    // left of them. Your inventory, a person's bag and a container's window wear
    // inventory look 07's leather (Leather.cs), the vicinity look 02's (Vicinity.cs).
    // The pine SHIPPING CRATE - boards with the gaps between them, an end post down
    // each side with its nails, the name stencilled on, the slots recessed
    // compartments, kraft-lined when taken - is only a container's window with
    // Settings.TheUnit off. The roster is a CORKBOARD in the
    // pegboard's pine frame. Frames are pooled across groups, so every skin here is
    // restated at bind and the kit branch of BindGroupFrame puts back what it took.
    internal static partial class InventoryWindow
    {
        private static readonly Color ShedThumb = Theme.ParseColor("#D9B47A", Color.white);
        private static readonly Color ShedTrack = new Color(0.08f, 0.04f, 0.02f, 0.45f);
        private static readonly Color CrateCount = Theme.ParseColor("#F3E7CC", Color.white);

        // a container, whoever's: yours, a person's bag, a thing's, the vicinity's -
        // less the grids that wear look 07's leather now (Leather.cs)
        private static bool IsCrate(int group)
        {
            if (IsLeather(group))
            {
                return false;
            }
            // (the vicinity wears facelift look 02's leather now: Vicinity.cs)
            return group == GroupOwn || group == GroupBag
                || (group == GroupCharacter && !(_target is CommonStates));
        }

        private static bool IsCork(int group)
        {
            return group == GroupReach && RosterView;
        }

        private static void SkinShedFrame(Plate plate, int group)
        {
            bool cork = IsCork(group);
            ToolShed.Tile(plate.Back, cork ? "kit_shed_cork" : "kit_shed_crate");
            plate.Edge.enabled = false;
            if (plate.Field != null && plate.Field.gameObject.activeSelf)
            {
                plate.Field.gameObject.SetActive(false);
            }
            if (plate.Mark != null && plate.Mark.gameObject.activeSelf)
            {
                plate.Mark.gameObject.SetActive(false);
            }
            EnsureRail(plate);
            // the crate's rim is the thin one (its posts had overlapped
            // the first and last columns): an 18 px slice, was 34
            ToolShed.Slice(plate.Rail, cork ? "kit_shed_pine" : "kit_shed_crate_rim", cork ? 30 : 18, fillCenter: false);
            plate.Rail.enabled = true;
            EnsureClose(plate);
            if (!plate.Close.gameObject.activeSelf)
            {
                plate.Close.gameObject.SetActive(true);
            }
            plate.Close.transform.SetAsLastSibling();
        }

        // A compartment for a slot: a dark well with its pine rim when empty, lined
        // with kraft paper when something is in it - so a faint icon still reads as
        // a taken slot. The count is stencilled in cream, over the thing it counts.
        // A pooled cell leaving the crate gets its skin and BarCount's face back
        // at its next bind, on the leather or the vicinity's.
        private static void ShedCell(CellView cell, bool filled)
        {
            Theme.KitSkin(cell.Box, filled ? "kit_shed_hole_full" : "kit_shed_hole", 14);
            cell.Box.preserveAspect = false;
            cell.Box.color = Color.white;
            TMP_FontAsset stencil = ToolShed.StencilFont;
            if (cell.Count != null && stencil != null && cell.Count.font != stencil)
            {
                cell.Count.font = stencil;
            }
        }


        // A band's name in the vicinity (its caption, or its section header):
        // stencilled on the crate's boards, the container's own icon burnt in -
        // but not a backpack, which is white with its ink wherever it is: burnt
        // brown, a main character's face under it was a smudge (IsBackpack).
        private static void ShedCaption(Plate plate, bool crate)
        {
            if (!crate)
            {
                Unstencil(plate);
                return;
            }
            TMP_FontAsset stencil = ToolShed.StencilFont;
            if (stencil != null && plate.Header.font != stencil)
            {
                plate.Header.font = stencil;
            }
            plate.Header.color = ToolShed.Stencil;
            plate.Header.characterSpacing = 3f;
            Theme.FlattenType(plate.Header);
            if (plate.Badge != null)
            {
                plate.Badge.color = IsBackpack(plate.Badge.sprite) ? Color.white : ToolShed.Brand;
            }
        }

        // The target's name on a kraft shipping label tacked over the crate's top.
        private static void ShedNamePlate(Plate plate)
        {
            Theme.KitSkin(plate.Back, "kit_shed_label", 14);
            plate.Back.pixelsPerUnitMultiplier = 52f / PlateH;
            plate.Back.color = Color.white;
            TMP_FontAsset stencil = ToolShed.StencilFont;
            if (stencil != null && plate.Header.font != stencil)
            {
                plate.Header.font = stencil;
            }
            plate.Header.color = ToolShed.Stencil;
            plate.Header.characterSpacing = 4f;
            Theme.FlattenType(plate.Header);
            if (plate.Badge != null)
            {
                plate.Badge.color = IsBackpack(plate.Badge.sprite) ? Color.white : ToolShed.Brand;
            }
        }

        // a pooled plate that was a crate's label or caption - or a leather
        // section's head (Leather.cs) - back to the plain face
        private static void Unstencil(Plate plate)
        {
            TMP_FontAsset stencil = ToolShed.StencilFont;
            TMP_FontAsset was = plate.Header.font;
            if (Theme.Font != null && was != null && was != Theme.Font
                && (was == stencil || was == HudStrongFont))
            {
                plate.Header.font = Theme.Font;
                plate.Header.characterSpacing = 0f;
            }
            if (plate.Badge != null && plate.Badge.color == ToolShed.Brand)
            {
                plate.Badge.color = Color.white;
            }
        }

        // ---------- the corkboard ----------

        private static bool CorkRows => RosterView;
        // the sorts are three sticky flags, one colour each; the one in force is solid
        private static readonly Color[] FlagInk =
        {
            Theme.ParseColor("#FFE36E", Color.yellow), Theme.ParseColor("#FFB3C4", Color.magenta),
            Theme.ParseColor("#A8E3AC", Color.green),
        };
        private static readonly Color PaperHot = new Color(1f, 0.96f, 0.82f, 1f);
        private static readonly Color PaperDown = new Color(0.9f, 0.86f, 0.76f, 1f);
        private static readonly Color PaperPicked = new Color(1f, 0.93f, 0.68f, 1f);
        // the cause word's marker: red for why they are unhappy, amber for what is coming
        private static readonly Color MarkerAmber = Theme.ParseColor("#B86E0E", new Color(0.72f, 0.43f, 0.05f, 1f));

        // A person on the roster: a paper strip pinned to the cork, the pin in their
        // kind colour where the stripe stood, the name in marker, where they are in
        // pencil, the cause in red marker underlined, a gold star for a favourite.
        private static void ShedRosterRow(SheetRow row, SheetLine line)
        {
            if (line.Card || !CorkRows)
            {
                UnshedRosterRow(row);
                return;
            }
            if (row.ShedPaper == null)
            {
                row.ShedPaper = Theme.KitImage(row.Go.transform, "Paper", "kit_shed_paper", 8, Color.white);
                Shadow drop = row.ShedPaper.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.4f);
                drop.effectDistance = new Vector2(0.6f, -1f);
                row.ShedPin = ToolShed.MakePin(row.Go.transform, "Pin");
            }
            if (row.ShedTape != null)
            {
                SetShown(row.ShedTape, false);
            }
            Rect r = ((RectTransform)row.Go.transform).rect;
            SetShown(row.ShedPaper, true);
            row.ShedPaper.transform.SetAsFirstSibling();
            Theme.Place(row.ShedPaper, 2.5f, 0.6f, r.width - 5f, r.height - 1.2f);
            // pinned by hand: each strip a hair off level, the same hair every time
            int hash = string.IsNullOrEmpty(line.Label) ? 0 : line.Label[0] + line.Label.Length * 7;
            row.ShedPaper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ((hash % 7) - 3) * 0.14f);
            bool hot = ReferenceEquals(_sheetHot, row);
            bool down = hot && ReferenceEquals(_sheetPressed, row) && Input.GetMouseButton(0);
            // the one you have targeted: their strip gone over with a highlighter,
            // held while they are the target - a light one (the first was slightly
            // too prominent; the hover's own tint was too quiet to tell apart)
            Color paper = line.Picked ? PaperPicked : Color.white;
            row.ShedPaper.color = down ? PaperDown : hot ? paper * PaperHot : paper;
            PlaceSnapshot(row, r.height, hash);
            // the favourite's star on the photo, as on the Character page's - not
            // at the strip's end - and the switch, as there: unset, its outline
            PlacePhotoStar(row, line.Starred, CanFavourite(row.Host != null ? row.Host.Owner : null));
            SetShown(row.Mark, false);
            // the pin holds the snapshot and the strip together, in the kind colour
            ToolShed.PlacePin(row.ShedPin, 6.5f, -2f, 9.5f, line.Tint, true);
            SetShown(row.Stripe, false);
            SetShown(row.Line, false);
            SetShown(row.Hover, false);
            TMP_FontAsset marker = ToolShed.MarkerFont;
            // the name in plain marker: the pin says the kind
            ToolShed.Hand(row.Label, marker, ToolShed.Marker);
            row.Label.fontStyle = FontStyles.Bold;
            ToolShed.Hand(row.Value, marker, ToolShed.Marker);
            ToolShed.Hand(row.Sub, marker, ToolShed.Pencil);
            ToolShed.Hand(row.Note, marker, line.CauseWarn ? MarkerAmber : ToolShed.MarkerRed);
            row.Note.fontStyle = FontStyles.Bold | FontStyles.Underline;
            if (row.Follow != null)
            {
                row.Follow.color = ToolShed.Marker;
            }
            if (row.Mark != null)
            {
                Sprite star = Theme.Kit("kit_shed_star", 0);
                if (star != null && row.Mark.sprite != star)
                {
                    row.Mark.sprite = star;
                }
            }
        }

        // ---------- the snapshots ----------

        // the Character page's star (DrawPhotoStar) on the snapshot, a child of the
        // photo so it tilts with it; the snapshot's frame stands in for the head's
        private static void PlacePhotoStar(SheetRow row, bool starred, bool can)
        {
            if (row.ShedPhoto == null)
            {
                return;
            }
            // three siblings on the photo, each placed - the world marker's star: a
            // gold glow, a dark edge, the gold. Stretched children of one another
            // came out at the cork's scale, a smear across three rows
            Transform photo = row.ShedPhoto.transform;
            if (photo.Find("StarGlow") == null || photo.Find("StarRim") == null)
            {
                // a star of an earlier make goes first
                for (int i = photo.childCount - 1; i >= 0; i--)
                {
                    Transform c = photo.GetChild(i);
                    if (c.name.StartsWith("Star"))
                    {
                        c.SetParent(null, false);
                        Object.Destroy(c.gameObject);
                    }
                }
                if (!starred && !can)
                {
                    return;
                }
                Theme.KitImage(photo, "StarGlow", "kit_dossier_star", 0, Alpha(StarShine, 0.35f));
                Theme.KitImage(photo, "Star", "kit_dossier_star", 0, EmblemInk);
                Theme.KitImage(photo, "StarFill", "kit_dossier_star", 0, StarShine);
                Theme.KitImage(photo, "StarRim", "kit_dossier_star_rim", 0, Alpha(EmblemInk, 0.6f));
            }
            Image glow = photo.Find("StarGlow").GetComponent<Image>();
            Image edge = photo.Find("Star").GetComponent<Image>();
            Image fill = photo.Find("StarFill").GetComponent<Image>();
            Image rim = photo.Find("StarRim").GetComponent<Image>();
            SetShown(glow, starred);
            SetShown(edge, starred);
            SetShown(fill, starred);
            SetShown(rim, !starred && can);
            const float pw = 14f;
            float k = pw / HeadPhotoW, s = HeadStarS * k * SmallStarGrow;
            float e = s * SmallStarEdge, g = s * SmallStarGlow;
            // a step further left than the head's place: the pin sits at the photo's top
            float x = HeadStarX * k - 1.5f, y = HeadStarY * k;
            if (starred)
            {
                Theme.Place(glow, x - g * 0.5f, y - g * 0.5f, s + g, s + g);
                Theme.Place(edge, x - e * 0.5f, y - e * 0.5f, s + e, s + e);
                Theme.Place(fill, x, y, s, s);
            }
            else if (can)
            {
                Theme.Place(rim, x, y, s, s);
            }
        }

        // who the Character page offers the star to: one of yours, not the two
        // the game keeps off the friends list's heart (npcID 0 and 1)
        private static bool CanFavourite(CommonStates who)
        {
            return who != null && who.npcID > 1
                && (who.employ == CommonStates.Employ.Friend || who.employ == CommonStates.Employ.Favorite);
        }

        // a press on the roster's photo star: is the pointer on it
        private static bool OnPhotoStar(SheetRow row, Camera cam)
        {
            if (row.ShedPhoto == null || !row.ShedPhoto.gameObject.activeInHierarchy)
            {
                return false;
            }
            foreach (string name in new[] { "StarGlow", "StarRim" })
            {
                Transform t = row.ShedPhoto.transform.Find(name);
                if (t != null && t.gameObject.activeSelf
                    && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)t, Input.mousePosition, cam))
                {
                    return true;
                }
            }
            return false;
        }

        // A snapshot on every strip: the dossier's portrait of the person, taken by
        // the same camera, one a frame and again after a while, and kept - someone
        // at home, out of the scene, keeps the last one taken of them.
        private const float CorkPhotoRoom = 17f;
        private const float SnapStale = 30f;
        private static readonly Color SnapBack = Theme.ParseColor("#D6DEE2", Color.gray);
        private static readonly Dictionary<CommonStates, Texture2D> Snaps = new Dictionary<CommonStates, Texture2D>();
        private static readonly Dictionary<CommonStates, float> SnapTaken = new Dictionary<CommonStates, float>();
        private static readonly List<CommonStates> SnapWanted = new List<CommonStates>();
        // asked for WHOLE: a rig with no head to frame is photographed entire
        // (RenderPortrait), for the combat plaque - the swamp boss is a plant
        // (Forgejo #184); elsewhere a headless creature keeps its glyph
        private static readonly HashSet<CommonStates> SnapWhole = new HashSet<CommonStates>();

        private static void PlaceSnapshot(SheetRow row, float h, int hash)
        {
            if (row.ShedPhoto == null)
            {
                row.ShedPhoto = Theme.KitImage(row.Go.transform, "Photo", "kit_shed_paper", 8, Color.white);
                Shadow drop = row.ShedPhoto.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.45f);
                drop.effectDistance = new Vector2(0.5f, -0.8f);
                var back = Theme.KitImage(row.ShedPhoto.transform, "Back", "kit_dossier_chip", 0, SnapBack);
                back.sprite = null;
                var br = back.rectTransform;
                br.anchorMin = Vector2.zero;
                br.anchorMax = Vector2.one;
                br.offsetMin = new Vector2(1.2f, 3.2f);
                br.offsetMax = new Vector2(-1.2f, -1.2f);
                var snapGo = Owned.New("Snap");
                snapGo.transform.SetParent(back.transform, false);
                row.ShedSnap = snapGo.AddComponent<RawImage>();
                row.ShedSnap.raycastTarget = false;
                var sr = row.ShedSnap.rectTransform;
                sr.anchorMin = Vector2.zero;
                sr.anchorMax = Vector2.one;
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;
            }
            SetShown(row.ShedPhoto, true);
            row.ShedPhoto.transform.SetSiblingIndex(1);   // over the strip, under the words
            Theme.Place(row.ShedPhoto, 3.5f, 1.5f, 14f, h - 3f);
            row.ShedPhoto.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ((hash % 5) - 2) * 1.6f);
            Texture2D snap = Snapshot(row.Host != null ? row.Host.Owner : null);
            row.ShedSnap.texture = snap;
            row.ShedSnap.enabled = snap != null;
            if (snap != null)
            {
                // the camera frames a head in 160x192; the photo is squarer, so it
                // shows the middle of the frame
                float aspect = (14f - 2.4f) / (h - 3f - 4.4f);
                float v = Mathf.Clamp01(aspect * 192f / 160f);
                row.ShedSnap.uvRect = new Rect(0f, (1f - v) * 0.35f, 1f, v);
            }
        }

        private static Texture2D Snapshot(CommonStates who, bool whole = false)
        {
            if (who == null)
            {
                return null;
            }
            if (whole)
            {
                SnapWhole.Add(who);
            }
            Snaps.TryGetValue(who, out Texture2D snap);
            bool stale = !SnapTaken.TryGetValue(who, out float at) || Time.realtimeSinceStartup - at > SnapStale;
            if (stale && !SnapWanted.Contains(who))
            {
                SnapWanted.Add(who);
            }
            return snap;
        }

        // one snapshot a frame while any are wanted: a render is a camera pass and a
        // read-back, and the roster asks for all of its people at once
        private static void TickSnapshots()
        {
            while (SnapWanted.Count > 0)
            {
                CommonStates who = SnapWanted[0];
                SnapWanted.RemoveAt(0);
                if (who == null)
                {
                    continue;
                }
                SnapTaken[who] = Time.realtimeSinceStartup;
                if (!RenderPortrait(GroupNone, who, true, SnapWhole.Contains(who)) || _portraitTex[GroupNone] == null)
                {
                    return;   // not in the scene: the last snapshot stands
                }
                if (!Snaps.TryGetValue(who, out Texture2D snap) || snap == null)
                {
                    snap = new Texture2D(PortraitPxW, PortraitPxH, TextureFormat.RGBA32, false);
                    Snaps[who] = snap;
                }
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = _portraitTex[GroupNone];
                snap.ReadPixels(new Rect(0, 0, PortraitPxW, PortraitPxH), 0, 0);
                snap.Apply(false);
                RenderTexture.active = was;
                _nextSheetPaint = 0f;
                return;
            }
        }

        // a pooled row that was on the cork, back to the plain roster
        private static void UnshedRosterRow(SheetRow row)
        {
            if (row.ShedTape != null)
            {
                SetShown(row.ShedTape, false);
            }
            if (row.ShedPaper == null || !row.ShedPaper.gameObject.activeSelf)
            {
                return;
            }
            SetShown(row.ShedPaper, false);
            ToolShed.PlacePin(row.ShedPin, 0f, 0f, 0f, Color.white, false);
            if (row.ShedPhoto != null)
            {
                SetShown(row.ShedPhoto, false);
            }
            Plain(row.Label);
            Plain(row.Value);
            Plain(row.Sub);
            Plain(row.Note);
            if (row.Note != null)
            {
                row.Note.fontStyle = FontStyles.Normal;
            }
            if (row.Mark != null)
            {
                row.Mark.sprite = Theme.Kit("kit_star", 0);
            }
        }

        private static void Plain(TMP_Text text)
        {
            TMP_FontAsset marker = ToolShed.MarkerFont;
            if (text != null && marker != null && text.font == marker)
            {
                text.font = Theme.KitFont(false);
                Theme.FlattenType(text);
            }
        }

        // A heading on the cork: its words on a strip of tape torn to their length.
        // `lead`: room before the words for a token (the Personality sort's),
        // inside the tape.
        private static void ShedRosterHead(SheetRow row, string text, float lead = 0f)
        {
            if (row.ShedPaper != null && row.ShedPaper.gameObject.activeSelf)
            {
                SetShown(row.ShedPaper, false);
                ToolShed.PlacePin(row.ShedPin, 0f, 0f, 0f, Color.white, false);
            }
            if (row.ShedPhoto != null)
            {
                SetShown(row.ShedPhoto, false);
            }
            if (!CorkRows)
            {
                if (row.ShedTape != null)
                {
                    SetShown(row.ShedTape, false);
                }
                Plain(row.Label);
                return;
            }
            if (row.ShedTape == null)
            {
                row.ShedTape = Theme.KitImage(row.Go.transform, "Tape", "kit_shed_tape", 18, Color.white);
            }
            SetShown(row.ShedTape, true);
            row.ShedTape.transform.SetAsFirstSibling();
            ToolShed.Hand(row.Label, ToolShed.MarkerFont, ToolShed.Marker);
            row.Label.fontStyle = FontStyles.Bold;
            row.Label.text = (text ?? string.Empty).ToUpperInvariant();
            float w = row.Label.GetPreferredValues(row.Label.text).x;
            // the tape holds the token as well as the words
            Theme.Place(row.ShedTape, RosterPadX - 5f, 0.5f, lead + w + 11f, RosterHeadH - 1f);
            float tilt = text != null && text.Length % 2 == 0 ? -0.7f : 0.6f;
            row.ShedTape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // The sorts are sticky flags stuck along the board, the one in force solid;
        // the camp-wide actions are words on the HUD's walnut chips.
        private static void ShedRosterBar(SheetRow row, SheetLine line, int n)
        {
            bool cork = CorkRows;
            bool anyDown = Input.GetMouseButton(0);
            for (int i = 0; i < n; i++)
            {
                Image face = row.Planks[i];
                TextMeshProUGUI word = row.PlankTexts[i];
                if (!cork)
                {
                    Plain(word);
                    face.rectTransform.localRotation = Quaternion.identity;
                    continue;
                }
                bool sort = i < RosterBarSorts;
                bool on = sort && i == line.Arg;
                bool hot = ReferenceEquals(_sheetHot, row) && _sheetHotSub == i;
                bool down = hot && ReferenceEquals(_sheetPressed, row) && _sheetPressedSub == i && anyDown;
                if (sort)
                {
                    Theme.KitSkin(face, "kit_shed_flag", 14);
                    Color c = FlagInk[i % FlagInk.Length];
                    face.color = new Color(c.r, c.g, c.b, on ? 1f : hot ? 0.8f : 0.5f);
                }
                else
                {
                    // the camp verbs are buttons, the HUD's own chips: walnut, pine
                    // under the pointer, the glyph cream on walnut and ink on pine
                    Theme.KitSkin(face, hot ? "kit_plank_pine" : "kit_chip_walnut", hot ? 18 : 0);
                    face.color = down ? PaperDown : Color.white;
                    row.PlankIcons[i].color = hot ? Theme.KitInk : Theme.KitCream;
                }
                face.preserveAspect = false;
                face.enabled = true;
                float tilt = 0f;
                face.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                ToolShed.Hand(word, ToolShed.MarkerFont, ToolShed.Marker);
                if (!sort)
                {
                    // an action's word is on its walnut chip: cream, ink on pine
                    word.color = hot ? Theme.KitInk : Theme.KitCream;
                }
                word.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
