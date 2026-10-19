using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IslandUI.UI;

namespace IslandUI
{
    // THE TOOL SHED'S CONTAINERS AND ROSTER (window-system.md 6.30, D134).
    //
    // Every container window - the vicinity's, the target's, your bag, a person's
    // bag - is a pine SHIPPING CRATE: boards with the gaps between them, an end post
    // down each side with its nails, the name stencilled on, and the slots recessed
    // compartments, kraft-lined when something is in them. The roster is a CORKBOARD in the
    // pegboard's pine frame. Frames are pooled across groups, so every skin here is
    // restated at bind and the kit branch of BindGroupFrame puts back what it took.
    internal static partial class InventoryWindow
    {
        private static readonly Color ShedThumb = Theme.ParseColor("#D9B47A", Color.white);
        private static readonly Color ShedTrack = new Color(0.08f, 0.04f, 0.02f, 0.45f);
        private static readonly Color CrateCount = Theme.ParseColor("#F3E7CC", Color.white);

        // a container, whoever's: yours, a person's bag, a thing's, the vicinity's
        private static bool IsCrate(int group)
        {
            return group == GroupOwn || group == GroupBag
                || (group == GroupReach && !RosterView)
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
        private static void ShedCell(CellView cell, bool filled, bool crate)
        {
            if (!crate)
            {
                if (cell.Box.sprite != Theme.SlotFrame)
                {
                    cell.Box.sprite = Theme.SlotFrame;   // as ApplyItemTile made it
                    cell.Box.type = Image.Type.Simple;
                    cell.Box.preserveAspect = false;
                    cell.Box.pixelsPerUnitMultiplier = 1f;
                }
                if (cell.Count != null && Theme.Font != null && cell.Count.font != Theme.Font
                    && cell.Count.font == ToolShed.StencilFont)
                {
                    cell.Count.font = Theme.Font;
                }
                return;
            }
            Theme.KitSkin(cell.Box, filled ? "kit_shed_hole_full" : "kit_shed_hole", 14);
            cell.Box.preserveAspect = false;
            cell.Box.color = Color.white;
            if (cell.Fill != null)
            {
                cell.Fill.enabled = false;
            }
            TMP_FontAsset stencil = ToolShed.StencilFont;
            if (cell.Count != null && stencil != null && cell.Count.font != stencil)
            {
                cell.Count.font = stencil;
            }
        }


        // A band's name in the vicinity (its caption, or its section header):
        // stencilled on the crate's boards, the container's own icon burnt in.
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
                plate.Badge.color = ToolShed.Brand;
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
                plate.Badge.color = ToolShed.Brand;
            }
        }

        // a pooled plate that was a crate's label or caption, back to the plain face
        private static void Unstencil(Plate plate)
        {
            TMP_FontAsset stencil = ToolShed.StencilFont;
            if (stencil != null && plate.Header.font == stencil && Theme.Font != null)
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
            // the pin holds the snapshot and the strip together, in the kind colour
            ToolShed.PlacePin(row.ShedPin, 6.5f, -2f, 9.5f, line.Tint, true);
            SetShown(row.Stripe, false);
            SetShown(row.Line, false);
            SetShown(row.Hover, false);
            TMP_FontAsset marker = ToolShed.MarkerFont;
            Color name = line.LabelTinted ? line.LabelTint : ToolShed.Marker;
            ToolShed.Hand(row.Label, marker, Color.Lerp(name, ToolShed.Marker, 0.3f));
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

        // A snapshot on every strip: the dossier's portrait of the person, taken by
        // the same camera, one a frame and again after a while, and kept - someone
        // at home, out of the scene, keeps the last one taken of them.
        private const float CorkPhotoRoom = 17f;
        private const float SnapStale = 30f;
        private static readonly Color SnapBack = Theme.ParseColor("#D6DEE2", Color.gray);
        private static readonly Dictionary<CommonStates, Texture2D> Snaps = new Dictionary<CommonStates, Texture2D>();
        private static readonly Dictionary<CommonStates, float> SnapTaken = new Dictionary<CommonStates, float>();
        private static readonly List<CommonStates> SnapWanted = new List<CommonStates>();

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

        private static Texture2D Snapshot(CommonStates who)
        {
            if (who == null)
            {
                return null;
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
                if (!RenderPortrait(GroupNone, who) || _portraitTex[GroupNone] == null)
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
        private static void ShedRosterHead(SheetRow row, string text)
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
            Theme.Place(row.ShedTape, RosterPadX - 5f, 0.5f, w + 11f, RosterHeadH - 1f);
            float tilt = text != null && text.Length % 2 == 0 ? -0.7f : 0.6f;
            row.ShedTape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // The sorts are sticky flags stuck along the board, the one in force solid;
        // the camp-wide verbs are written on tape.
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
                word.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
