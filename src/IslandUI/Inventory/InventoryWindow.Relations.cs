using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // InventoryWindow — The Relations aspect and the love matrix.
    internal static partial class InventoryWindow
    {
        // THE RELATIONS ASPECT IS A LOOKUP, NOT A LISTING (it is only occasionally
        // consulted). Building the whole
        // graph - fifty-one edges, their reverses, colours, strings - cost
        // twenty frames a second however it was cached. What is always shown
        // costs two lookups: their edge to you, yours to them, and the verbs it
        // unlocks; then vanilla's own two picks, the strongest and the most
        // hated; then a name field - type, and the five best matches appear;
        // pick one and that pair is drawn on the two-band scale with its counts.
        // Names are listed once per carrier (no reverse edges), the pick is
        // remembered per carrier, and nothing here is rebuilt by the frame.
        private const int RelShowLoved = 5;
        private const int RelShowHostile = 3;
        // What BRIEF means on the merged Profile sheet: the player edge plus the
        // two or three strongest named edges, then "… N more"; the hostile pair is kept because who dislikes them is why
        // a follower is unhappy, which is the question Profile gets opened for.
        private const int RelBriefLoved = 3;
        private const int RelBriefHostile = 2;
        private static CommonStates _relNamesWho;
        private static int _relNamesCount = -1;
        private static readonly List<string> RelNames = new List<string>();      // parallel to who.lovers
        private static readonly List<string> RelNamesLower = new List<string>();

        // NPCManager.GetFriend IS NOT A LOOKUP for a character who is no longer a
        // friend: past the roster scan it falls through to something that cost
        // ~20 ms - once per rebind for the "first partner" row alone, ten times
        // a second while walking (50 fps with the tab up, 75
        // without). A miss is as worth remembering as a hit; the roster's
        // length says when to look again.
        private static readonly Dictionary<int, CommonStates> FriendCache = new Dictionary<int, CommonStates>();
        private static int _friendCacheRoster = -1;

        private static CommonStates Friend(NPCManager npcs, int friendID)
        {
            // a negative id is a KIND of native, not a person: GetFriend would
            // build a temporary CommonStates for it (MakeTempCommon) - the cost
            // - so it has no CommonStates here, only a name (FriendNameOf)
            if (npcs == null || friendID < 0)
            {
                return null;
            }
            int roster = npcs.friendList != null ? npcs.friendList.Count : 0;
            if (roster != _friendCacheRoster)
            {
                FriendCache.Clear();
                _friendCacheRoster = roster;
            }
            CommonStates found;
            if (FriendCache.TryGetValue(friendID, out found))
            {
                return found;
            }
            found = npcs.GetFriend(friendID);
            FriendCache[friendID] = found;
            return found;
        }

        private static readonly Dictionary<int, string> KindNames = new Dictionary<int, string>();

        // The name of whoever a friendID means: the friend's own, or for a kind
        // of native the game's default name for that kind
        private static string FriendNameOf(NPCManager npcs, int friendID)
        {
            if (friendID < -1)
            {
                string kind;
                if (!KindNames.TryGetValue(friendID, out kind))
                {
                    kind = string.Empty;
                    if (_mn != null && _mn.randChar != null)
                    {
                        try { _mn.randChar.DefaultNaming(-friendID, ref kind); }
                        catch (System.Exception) { kind = string.Empty; }
                    }
                    KindNames[friendID] = kind ?? string.Empty;
                }
                return kind;
            }
            CommonStates friend = Friend(npcs, friendID);
            return friend != null && !string.IsNullOrEmpty(friend.charaName) ? friend.charaName : string.Empty;
        }

        private static bool LoveBetween(NPCManager npcs, CommonStates from, CommonStates to, out float love)
        {
            love = 0f;
            if (npcs == null || from == null || to == null || from.lovers == null)
            {
                return false;
            }
            int id = npcs.GetLoversID(from, to.friendID);
            if (id < 0 || id >= from.lovers.Count)
            {
                return false;
            }
            love = from.lovers[id].love;
            return true;
        }

        // the names behind a carrier's list, once per carrier and list length
        private static void EnsureRelNames(NPCManager npcs, CommonStates who)
        {
            int count = who.lovers != null ? who.lovers.Count : 0;
            if (ReferenceEquals(_relNamesWho, who) && _relNamesCount == count)
            {
                return;
            }
            _relNamesWho = who;
            _relNamesCount = count;
            RelNames.Clear();
            RelNamesLower.Clear();
            for (int i = 0; i < count; i++)
            {
                CommonStates.Lovers edge = who.lovers[i];
                string name = edge != null ? FriendNameOf(npcs, edge.friendID) : string.Empty;
                if (string.IsNullOrEmpty(name)) { name = "???"; }
                RelNames.Add(name);
                RelNamesLower.Add(name.ToLowerInvariant());
            }
        }

        // The Relations ASPECT, kept whole. The Profile merge took it out of the tab set - the
        // sheet is Profile's two blocks now - and this stays as the aspect's
        // writer, so putting Aspect.Relations back in the order arrays restores
        // the tab exactly as it was.
        private static void WriteRelations(List<SheetLine> lines, CommonStates who, bool own, int group)
        {
            WriteRelationEdges(lines, who, own, RelShowLoved, RelShowHostile);
            WriteRelationHistory(lines, who);
        }

        // THE EDGES, AT WHATEVER LENGTH THE SHEET ASKS FOR. Split out of
        // WriteRelations when Profile and Relations merged, so one writer serves
        // both the full listing and Profile's brief one - the difference is two
        // numbers, not two blocks of code.
        private static void WriteRelationEdges(List<SheetLine> lines, CommonStates who, bool own,
            int maxLoved, int maxHostile)
        {
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            CommonStates player = _mn != null && _mn.gameMN != null && _mn.gameMN.pMove != null
                ? _mn.gameMN.pMove.common : null;
            string me = own ? Shared.Loc.Get("sheet.you", "You") : (string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName);
            // the heading wears the gates over the column; everything on the
            // scale sits under it (the You row sits at the top of the target's
            // relationships list, under the title)
            EnsureRelNames(npcs, who);
            int total = who.lovers != null ? who.lovers.Count : 0;
            SheetLine head = Head(Shared.Loc.Get("relations.head", "Relations"), total > 0 ? total.ToString() : string.Empty, VanillaTabIcon(2));
            head.Gates = true;
            lines.Add(head);
            if (!own && player != null)
            {
                // their edge to you leads - the only edge the game reads. What it
                // opens is what the gate marks over the column say; the "Unlocked"
                // line that spelled it out went
                float theirs, yours;
                bool theirsKnown = LoveBetween(npcs, who, player, out theirs);
                bool yoursKnown = LoveBetween(npcs, player, who, out yours);
                lines.Add(Relation(me, Shared.Loc.Get("sheet.you", "You"), NPCColor(player), theirsKnown ? theirs : 0f, yours, yoursKnown, Emotion(0)));
                lines.Add(new SheetLine { Kind = LineSpace });
            }
            // then the graph: the strongest few and the hostile few, each on the
            // shared scale; the rest behind "… N more", which opens the matrix
            var order = new List<int>();
            for (int i = 0; i < total; i++)
            {
                if (who.lovers[i] == null) { continue; }
                if (player != null && !own && who.lovers[i].friendID == player.friendID) { continue; }
                order.Add(i);
            }
            order.Sort((x, y) => who.lovers[y].love.CompareTo(who.lovers[x].love));
            int shown = 0;
            int lovedShown = 0;
            for (int k = 0; k < order.Count && lovedShown < maxLoved; k++)
            {
                int i = order[k];
                if (who.lovers[i].love < 0f) { break; }
                lines.Add(RelationRowOf(npcs, who, i, me));
                lovedShown++; shown++;
            }
            int hostileShown = 0;
            for (int k = order.Count - 1; k >= 0 && hostileShown < maxHostile; k--)
            {
                int i = order[k];
                if (who.lovers[i].love >= 0f) { break; }
                if (hostileShown == 0) { lines.Add(new SheetLine { Kind = LineSpace }); }
                lines.Add(RelationRowOf(npcs, who, i, me));
                hostileShown++; shown++;
            }
            if (order.Count > shown)
            {
                lines.Add(new SheetLine { Label = string.Empty, Kind = LineMore, Enabled = true,
                    Options = new[] { Shared.Loc.Get("relations.more", "… {0} more", order.Count - shown) } });
            }
        }

        // The counts of a life - fixed facts, not live ones, and so a
        // Profile block under §6.21's tempo axis rather than a Relations one.
        private static void WriteRelationHistory(List<SheetLine> lines, CommonStates who)
        {
            TextManager tm = GameWords.Text(_mn);
            NPCManager npcs = _mn != null ? _mn.npcMN : null;
            lines.Add(Head(Shared.Loc.Get("relations.history", "History"), string.Empty, Emotion(7)));
            HistoryCounts(who, out int sex, out int pregnancies, out int deliveries);
            lines.Add(Row(Shared.Loc.Get("relations.history.first_partner", "First partner"), FirstSexText(tm, npcs, who)));
            lines.Add(Row(Shared.Loc.Get("relations.history.sex", "Sex"), sex.ToString()));
            lines.Add(Row(Shared.Loc.Get("relations.history.pregnancies", "Pregnancies"), pregnancies.ToString()));
            lines.Add(Row(Shared.Loc.Get("relations.history.deliveries", "Deliveries"), deliveries.ToString()));
            lines.Add(Row(Shared.Loc.Get("relations.history.masturbation", "Masturbation"), who.sexInfo != null && who.sexInfo.Length > 2
                ? who.sexInfo[2].ToString() : "0"));
        }

        private static SheetLine RelationRowOf(NPCManager npcs, CommonStates who, int index, string me)
        {
            CommonStates.Lovers edge = who.lovers[index];
            CommonStates other = Friend(npcs, edge.friendID);
            float back = 0f;
            bool backKnown = other != null && LoveBetween(npcs, other, who, out back);
            return Relation(me, RelNames[index], other != null ? NPCColor(other) : null,
                edge.love, back, backKnown);
        }

        // THE NUMBERS' TOOLTIP: the kit's dark paper over whichever bar the
        // pointer rests on, sheet row or matrix row. One tip for both, ticked
        // by whichever window has the pointer; a frame nobody ticks it hides it.
        private static KitTooltip _loveTip;
        private static GameObject _loveTipRoot;
        private static int _loveTipFrame = -1;

        private static void LoveTipTick(string text, Camera cam)
        {
            if (_root == null)
            {
                return;
            }
            if (_loveTip == null || !ReferenceEquals(_loveTipRoot, _root))
            {
                _loveTip = KitTooltip.Create((RectTransform)_root.transform);
                _loveTipRoot = _root;
            }
            _loveTipFrame = Time.frameCount;
            _loveTip.Tick(text, cam);
        }

        // harness: a tip shown at the pointer while a sheet is open, as if
        // hovered - "~" for a line break, "^" for a comma (the harness splits on
        // commas); "-" clears it
        private static string _tipPreview;

        internal static string TipPreview(string text)
        {
            _tipPreview = string.IsNullOrEmpty(text) || text == "-" ? null : text.Replace('~', '\n').Replace('^', ',');
            return _tipPreview == null ? "cleared" : "showing";
        }

        private static void LoveTipIdle()
        {
            if (_loveTip != null && ReferenceEquals(_loveTipRoot, _root)
                && _loveTipFrame < Time.frameCount - 1 && _loveTip.Visible)
            {
                _loveTip.Hide();
            }
        }


        // ---- the relations MATRIX, a window of its own (§6.20) ----
        //
        // "… N more" opens it: every edge the carrier has, both directions,
        // sorted strongest first and flowed into columns of rows - each row the
        // sheet's own widget, the gates written once over every column -
        // vanilla's hover matrix, on the kit, dismissed by its chip, Escape or a
        // click outside. Built once per open; nothing ticks while it is closed.
        private sealed class MatrixRow
        {
            public GameObject Go;
            public TextMeshProUGUI Name;
            public LoveWidget Love;
            public Image Rule;       // the printed ruling under the name (§6.28)
            public string Tip;
        }

        private static GameObject _matrix;
        private static Image _matrixBack;
        private static Image _matrixPaper;
        private static Image _matrixRail;
        private static Image _matrixClose;
        private static TextMeshProUGUI _matrixTitle;
        private static Image _matrixTitleRule;
        private static Image _matrixPill;
        private static TextMeshProUGUI _matrixPillText;
        private static int _matrixSkin = -1;          // 1 printed, 0 wooden, -1 not yet dressed
        private static readonly List<MatrixRow> _matrixRows = new List<MatrixRow>();
        private static readonly List<GateHeader> _matrixGates = new List<GateHeader>();
        private const int MatrixRowsPerCol = 20;      // 20 rows of 16 units sit under the canvas's 450
        private const int MatrixMaxCols = 4;
        private const float MatrixColW = 150f;        // name 44 · a track long enough to read; the numbers are the tip
        private const float MatrixNameW = 44f;
        private const float MatrixPad = 10f;
        private const float MatrixTitleH = 12f;
        private const float MatrixGatesH = 13f;

        // PRINTED WHEN THE SHEET WAS: opened from a dossier the matrix is one more
        // leaf of it - the kraft folder, the paper, the spaced capitals over a rule
        // and the count in a pill, a ruling under every name (the window had kept
        // the old styling)
        internal static bool MatrixOpen(CommonStates who, bool printed = false)
        {
            if (who == null || _root == null || _mn == null)
            {
                return false;
            }
            NPCManager npcs = _mn.npcMN;
            EnsureRelNames(npcs, who);
            var order = new List<int>();
            for (int i = 0; who.lovers != null && i < who.lovers.Count; i++)
            {
                if (who.lovers[i] != null) { order.Add(i); }
            }
            order.Sort((x, y) => who.lovers[y].love.CompareTo(who.lovers[x].love));
            int cols = Mathf.Clamp(Mathf.CeilToInt(order.Count / (float)MatrixRowsPerCol), 1, MatrixMaxCols);
            int perCol = Mathf.Max(1, Mathf.Min(MatrixRowsPerCol, Mathf.CeilToInt(order.Count / (float)cols)));
            EnsureMatrix();
            SkinMatrix(printed);
            float w = 2f * MatrixPad + cols * MatrixColW;
            float h = MatrixPad + MatrixTitleH + MatrixGatesH + perCol * LoveWidget.RowH + MatrixPad;
            var rect = (RectTransform)_matrix.transform;
            rect.sizeDelta = new Vector2(w, h);
            Theme.Place(_matrixTitle, MatrixPad, 4f, w - 2f * MatrixPad, MatrixTitleH);
            if (_matrixPaper != null)
            {
                Theme.Place(_matrixPaper, DossierPaperInset, DossierPaperInset,
                    w - 2f * DossierPaperInset, h - 2f * DossierPaperInset);
            }
            float trackW = MatrixColW - MatrixNameW - 10f;
            float rowsY = MatrixPad + MatrixTitleH + MatrixGatesH;
            string me = string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName;
            for (int c = 0; c < MatrixMaxCols; c++)
            {
                while (_matrixGates.Count <= c) { _matrixGates.Add(GateHeader.Create(_matrix.transform)); }
                bool on = c < cols;
                _matrixGates[c].Show(on);
                if (on)
                {
                    float cx = MatrixPad + c * MatrixColW;
                    _matrixGates[c].Place(cx + MatrixNameW + 8f, trackW - 8f, rowsY - MatrixGatesH, MatrixGatesH);
                }
            }
            int shown = 0;
            for (; shown < order.Count && shown < cols * perCol; shown++)
            {
                int i = order[shown];
                MatrixRow row = EnsureMatrixRow(shown);
                CommonStates.Lovers edge = who.lovers[i];
                CommonStates other = Friend(npcs, edge.friendID);
                float back = 0f;
                bool backKnown = other != null && LoveBetween(npcs, other, who, out back);
                int c = shown / perCol;
                int r = shown % perCol;
                Theme.Place(row.Go.transform, MatrixPad + c * MatrixColW, rowsY + r * LoveWidget.RowH,
                    MatrixColW, LoveWidget.RowH);
                row.Name.text = RelNames[i];
                Color? tint = other != null ? NPCColor(other) : null;
                row.Name.color = tint ?? (printed ? Theme.DossierLabel : Theme.InkDim);
                Theme.Place(row.Name, 0f, 0f, MatrixNameW - 2f, LoveWidget.RowH);
                row.Love.Place(MatrixNameW, 0f, trackW, LoveWidget.RowH);
                if (row.Rule.gameObject.activeSelf != printed) { row.Rule.gameObject.SetActive(printed); }
                if (printed)
                {
                    Theme.Place(row.Rule, 0f, LoveWidget.RowH - Hair, MatrixColW - 8f, Hair);
                }
                row.Love.Show(true);
                row.Love.Paint(edge.love, back, backKnown);
                row.Tip = LoveWidget.TipText(me, RelNames[i], edge.love, back, backKnown);
                if (!row.Go.activeSelf) { row.Go.SetActive(true); }
            }
            for (int k = shown; k < _matrixRows.Count; k++)
            {
                if (_matrixRows[k].Go.activeSelf) { _matrixRows[k].Go.SetActive(false); }
            }
            _matrix.transform.SetAsLastSibling();
            if (!_matrix.activeSelf) { _matrix.SetActive(true); }
            if (printed)
            {
                // the section's heading: the name as it is written (a name is never
                // capitals), the word in the heading's, a rule to the count's pill,
                // the pill clear of the close chip
                _matrixTitle.text = Shared.Loc.Get("relations.matrix.title", "{0} · RELATIONS", string.IsNullOrEmpty(who.charaName) ? "?" : who.charaName);
                _matrixPillText.text = order.Count > shown ? Shared.Loc.Get("relations.matrix.shown_of", "{0} of {1}", shown, order.Count) : order.Count.ToString();
                float cy = 4f + MatrixTitleH * 0.5f;
                float right = w - CloseChipFromRight - CloseChipSize * 0.5f - 3f;
                float pw = _matrixPillText.GetPreferredValues(_matrixPillText.text, 400f, 40f).x + 9f;
                Theme.Place(_matrixPill, right - pw, cy - 4.5f, pw, 9f);
                Theme.Place(_matrixPillText, right - pw, cy - 4.5f, pw, 9f);
                float ruleX = MatrixPad + _matrixTitle.GetPreferredValues(_matrixTitle.text, 400f, 40f).x + 4f;
                Theme.Place(_matrixTitleRule, ruleX, cy - Hair * 0.5f, Mathf.Max(0f, right - pw - 3f - ruleX), Hair);
            }
            else
            {
                string matrixWho = string.IsNullOrEmpty(who.charaName) ? Shared.Loc.Get("relations.head", "Relations") : who.charaName;
                string matrixCount = Shared.Loc.Plural("relations.matrix.count", "{0} relation", "{0} relations", order.Count);
                _matrixTitle.text = order.Count > shown
                    ? Shared.Loc.Get("relations.matrix.plain_shown", "{0} — {1} ({2} shown)", matrixWho, matrixCount, shown)
                    : Shared.Loc.Get("relations.matrix.plain", "{0} — {1}", matrixWho, matrixCount);
            }
            return true;
        }

        // The two looks on one set of objects: the folder and its paper, or the
        // wooden field and rail; the title's type follows
        private static void SkinMatrix(bool printed)
        {
            int skin = printed ? 1 : 0;
            if (_matrixSkin == skin)
            {
                return;
            }
            _matrixSkin = skin;
            if (printed)
            {
                Theme.KitSkin(_matrixBack, "kit_dossier_folder", 16);
                _matrixBack.color = Color.white;
            }
            else if (Theme.Kit("kit_field", 12) != null)
            {
                Theme.KitSkin(_matrixBack, "kit_field", 12);
            }
            _matrixPaper.gameObject.SetActive(printed);
            if (_matrixRail != null) { _matrixRail.gameObject.SetActive(!printed); }
            _matrixTitleRule.gameObject.SetActive(printed);
            _matrixPill.gameObject.SetActive(printed);
            _matrixPillText.gameObject.SetActive(printed);
            _matrixTitle.fontSize = printed ? SectionSize : SheetTextSize;
            _matrixTitle.characterSpacing = printed ? CaptionSpacing + 6f : 0f;
            _matrixTitle.color = printed ? Theme.DossierInk : Theme.Ink;
        }

        // harness: the matrix of whoever is named, printed unless "|0" follows
        internal static string MatrixFor(string arg)
        {
            string[] parts = (arg ?? string.Empty).Split('|');
            foreach (CommonStates c in Object.FindObjectsOfType<CommonStates>())
            {
                if (c != null && string.Equals(c.charaName, parts[0], System.StringComparison.OrdinalIgnoreCase))
                {
                    return MatrixOpen(c, parts.Length < 2 || parts[1] != "0") ? "open" : "failed";
                }
            }
            return "no one named '" + parts[0] + "'";
        }

        internal static void MatrixClose()
        {
            if (_matrix != null && _matrix.activeSelf)
            {
                _matrix.SetActive(false);
            }
        }

        private static bool MatrixShown => _matrix != null && _matrix.activeSelf;

        private static void EnsureMatrix()
        {
            if (_matrix != null)
            {
                return;
            }
            _matrix = Owned.New("RelationsMatrix");
            _matrix.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)_matrix.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            var back = _matrix.AddComponent<Image>();
            back.raycastTarget = true;
            _matrixBack = back;
            Sprite field = Theme.Kit("kit_field", 12);
            if (field != null)
            {
                back.sprite = field;
                back.type = Image.Type.Sliced;
                back.pixelsPerUnitMultiplier = Theme.KitPpu;
            }
            else
            {
                back.sprite = Theme.RoundedFill;
                back.type = Image.Type.Sliced;
                back.color = Theme.ParchmentFill;
            }
            _matrixPaper = Theme.KitImage(_matrix.transform, "Paper", "kit_dossier_paper", 24, Color.white, stretch: true);
            _matrixPaper.gameObject.SetActive(false);
            Image rail = Theme.KitImage(_matrix.transform, "Rail", "kit_rail", 12, Color.white);
            _matrixRail = rail;
            if (rail != null && rail.sprite != null)
            {
                var rr = rail.rectTransform;
                rr.anchorMin = Vector2.zero;
                rr.anchorMax = Vector2.one;
                rr.offsetMin = Vector2.zero;
                rr.offsetMax = Vector2.zero;
            }
            _matrixTitle = Theme.MakeKitText(_matrix.transform, "Title", SheetTextSize, Theme.Ink,
                TextAlignmentOptions.MidlineLeft, bold: true);
            _matrixTitle.enableWordWrapping = false;
            _matrixTitleRule = PlainImage(_matrix.transform, "TitleRule", Theme.DossierInk);
            _matrixPill = Theme.KitImage(_matrix.transform, "Pill", "kit_dossier_pill", 15, Theme.DossierInk, stretch: true);
            _matrixPillText = Theme.MakeKitText(_matrix.transform, "PillText", 6f, Theme.DossierInk,
                TextAlignmentOptions.Center);
            _matrixPillText.enableWordWrapping = false;
            _matrixSkin = -1;
            _matrixClose = CloseChip.Create(_matrix.transform);
            _matrixRows.Clear();
            _matrixGates.Clear();
            _matrix.SetActive(false);
        }

        private static MatrixRow EnsureMatrixRow(int index)
        {
            while (_matrixRows.Count <= index)
            {
                var row = new MatrixRow();
                row.Go = Owned.New("MRow");
                row.Go.transform.SetParent(_matrix.transform, false);
                row.Rule = PlainImage(row.Go.transform, "Rule", Theme.DossierRule);
                row.Name = Theme.MakeKitText(row.Go.transform, "Name", SheetTextSize, Theme.InkDim,
                    TextAlignmentOptions.MidlineLeft);
                row.Name.enableWordWrapping = false;     // a long name ends in an ellipsis before the widget
                row.Love = LoveWidget.Create(row.Go.transform);
                _matrixRows.Add(row);
            }
            return _matrixRows[index];
        }

        // THE WORLD NEVER SEES A PRESS MEANT FOR THE MATRIX. It closes on the press,
        // and the chip hangs over the world: by the time the game asked whether the
        // pointer was over UI (GameManager.OnUICheck) the window was gone, so the
        // press went on to the world and dropped the target, closing the interaction
        // when it should only have closed the popup. So a press while it is up is the
        // matrix's from the frame it goes down until it comes up - DragOwnsThePointer
        // holds the game's flag up for it, whichever of the two Updates runs first.
        // Only the press: the same flag gates the attack keys, which stay live with
        // the window open.
        private static bool _matrixPressHeld;

        internal static bool MatrixOwnsPointer
        {
            get
            {
                if (_matrixPressHeld && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
                {
                    _matrixPressHeld = false;
                }
                return _matrixPressHeld || (MatrixShown && Input.GetMouseButtonDown(0));
            }
        }

        // The window eats the pointer while it is up: a press on the chip or
        // outside it closes; everything else over it is simply consumed.
        private static bool TickMatrix(Camera cam)
        {
            if (!MatrixShown)
            {
                return false;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)_matrix.transform, Input.mousePosition, cam)
                    || (_matrixClose != null && RectTransformUtility.RectangleContainsScreenPoint(
                        _matrixClose.rectTransform, Input.mousePosition, cam)))
                {
                    _matrixPressHeld = true;
                    MatrixClose();
                    return true;
                }
            }
            bool over = RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)_matrix.transform, Input.mousePosition, cam);
            // the chip closes on the press, so hover is the face it has to show
            CloseChip.Paint(_matrixClose, _matrixClose != null && RectTransformUtility.RectangleContainsScreenPoint(
                _matrixClose.rectTransform, Input.mousePosition, cam), false);
            string tip = null;
            for (int i = 0; over && tip == null && i < _matrixRows.Count; i++)
            {
                MatrixRow row = _matrixRows[i];
                if (row.Go.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)row.Go.transform, Input.mousePosition, cam))
                {
                    tip = row.Tip;
                }
            }
            LoveTipTick(tip, cam);
            return over;
        }

        private static int IndexOfLover(CommonStates who, int friendID)
        {
            for (int i = 0; who.lovers != null && i < who.lovers.Count; i++)
            {
                if (who.lovers[i] != null && who.lovers[i].friendID == friendID) { return i; }
            }
            return -1;
        }




        private static string PersonText(TextManager tm, int[] person, int slot)
        {
            if (tm == null || tm.personTexts == null || person == null || slot >= person.Length)
            {
                return string.Empty;
            }
            int id = person[slot];
            return id >= 0 && id < tm.personTexts.Length ? tm.personTexts[id] : string.Empty;
        }

        private static string FriendName(NPCManager npcs, int[] status, int slot)
        {
            if (npcs == null || status == null || slot >= status.Length || status[slot] == -1)
            {
                return string.Empty;
            }
            return FriendNameOf(npcs, status[slot]);
        }

        // ErostaChange's first-sex line: a name once there is one, else the
        // character's orientation in the game's words
        private static string FirstSexText(TextManager tm, NPCManager npcs, CommonStates who)
        {
            if (who.sexInfo == null || who.sexInfo.Length < 2)
            {
                return string.Empty;
            }
            if (who.sexInfo[1] != -1)
            {
                return FriendNameOf(npcs, who.sexInfo[1]);
            }
            if (tm == null || tm.descText == null || _mn.randChar == null)
            {
                return string.Empty;
            }
            int index;
            switch (_mn.randChar.GetSexual(who.npcID))
            {
                case 0: index = 11; break;
                case 1: index = 12; break;
                default: index = 52; break;
            }
            return index < tm.descText.Length ? tm.descText[index] : string.Empty;
        }
    }
}
