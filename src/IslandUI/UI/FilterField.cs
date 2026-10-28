using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // Second shape. A FILTER field, per window — and the word matters. This
    // is not a search: nothing is found and nowhere is jumped to. The window keeps
    // showing what it was showing, with less of it, so filter is the terminology.
    //
    // WHAT MOVED AND WHAT DID NOT. The first attempt put one field on a floating
    // surface, on D28a's finding that a field is four rules — a keyboard grab, an
    // Escape that must mean "leave the field" before "close the window", a
    // focus-versus-collapse policy, and a summon chord — and that all four belong
    // to the FIELD rather than to the window being searched. That finding stands.
    // What was wrong was concluding the WIDGET had to float too.
    //
    // So the four rules stay paid once, in FilterInput below: one set of input
    // patches, one Escape rule, one "is anything focused" answer, however many
    // fields exist. Only the widget multiplies, and a widget is cheap.
    //
    // EACH FIELD OWNS ITS OWN QUERY. Filtering your chests for "iron" and your
    // recipes for "rope" are different questions, and a shared string would make
    // answering one erase the other. It also removes the last of D28a's four rules
    // that a shared field would have re-introduced: with a field per window there
    // is no "which window is active", because you type into the one you clicked.
    internal sealed class FilterField
    {
        private readonly List<string> _terms = new List<string>();
        private readonly Dictionary<string, bool> _matchMemo = new Dictionary<string, bool>();
        private TMP_InputField _field;
        private Image _well;
        private Image _rim;
        private TextMeshProUGUI _count;
        private Image _clear;
        private bool _bench;
        private int _carved = -1;   // -1 paper, 0 warm light, 1 dark, 2 roster stone, 10+ the tool shed
        private float _border = 12f; // the well sprite's border in px, for Layout's multiplier
        private TMP_FontAsset _plainFont, _plainHintFont, _plainCountFont;
        private Vector3 _laidOut = new Vector3(-1f, -1f, -1f); // well width, height, count width last laid out
        private string _query = "";
        private const float ClearW = 5f;

        public bool IsFocused => _field != null && _field.isFocused;
        public string Query => _query;
        public bool Active => _terms.Count > 0;
        public TMP_InputField Field => _field;
        public GameObject Go => _well != null ? _well.gameObject : null;

        // Bumped whenever the answer to Matches could have changed. Consumers that
        // memoise what a cell last drew compare this rather than re-running the
        // matcher — without it a cell whose ITEM did not change would keep its
        // stale mark through every letter you type.
        public int Version { get; private set; }

        // ---------- matching ----------

        // Multi-term AND over the localized name, the internal key, the localized
        // tips and the derived ItemTags. Memoised per item key, because the
        // vicinity window asks this once per drawn cell and the standing lesson
        // is that anything per-cell there is counted in thousands.
        public bool Matches(ManagersScript mn, string itemKey)
        {
            if (_terms.Count == 0)
            {
                return true; // no query: everything matches, so nothing is marked
            }
            if (string.IsNullOrEmpty(itemKey) || mn == null || mn.itemMN == null)
            {
                return false;
            }
            if (_matchMemo.TryGetValue(itemKey, out bool cached))
            {
                return cached;
            }
            ItemData item = mn.itemMN.FindItem(itemKey);
            bool hit = item != null && MatchesItem(mn, item);
            _matchMemo[itemKey] = hit;
            return hit;
        }

        public bool MatchesItem(ManagersScript mn, ItemData item)
        {
            if (_terms.Count == 0)
            {
                return true;
            }
            if (item == null)
            {
                return false;
            }
            string name = item.GetItemName().ToLowerInvariant();
            string key = item.name.ToLowerInvariant();
            string tips = FilterInput.SideText(mn, item);
            for (int t = 0; t < _terms.Count; t++)
            {
                if (!name.Contains(_terms[t]) && !key.Contains(_terms[t])
                    && !tips.Contains(_terms[t]) && !ItemTags.Matches(item, _terms[t]))
                {
                    return false;
                }
            }
            return true;
        }

        // Every term in one name, already lowercased - the same AND as
        // MatchesItem. The crafting search asks it of each bench's and
        // crafter's name.
        public bool MatchesText(string lowered)
        {
            if (_terms.Count == 0 || string.IsNullOrEmpty(lowered))
            {
                return false;
            }
            for (int t = 0; t < _terms.Count; t++)
            {
                if (!lowered.Contains(_terms[t]))
                {
                    return false;
                }
            }
            return true;
        }

        private void RebuildTerms()
        {
            _terms.Clear();
            string q = _query.Trim().ToLowerInvariant();
            if (q.Length > 0)
            {
                string[] parts = q.Split(FilterInput.SplitChars,
                    System.StringSplitOptions.RemoveEmptyEntries);
                _terms.AddRange(parts);
            }
            _matchMemo.Clear();
            Version++;
        }

        // Dropped when the language flips: every cached string was localized.
        internal void DropCaches()
        {
            _matchMemo.Clear();
            Version++;
        }

        // ---------- the widget ----------

        // Built into whatever rect the window gives it. The window owns placement
        // entirely — this class has no opinion about where a filter lives, which
        // is the point of it not being a floating surface any more.
        // `bench`: the field sits on the dark working surface and takes ITS
        // material - the paper's shape, in the surface's well colour, with the
        // surface's text, as the old docked search did, which looked good there.
        // On the dark field the cream paper was a bright bar across the top of a
        // dark window; a well sunk one shade lighter than the field is what read
        // as "type here". The kit's "paper is cream on both grounds"
        // was a call, not a finding, and this is the finding. The light surfaces
        // keep the cream paper.
        public bool Build(Transform parent, ManagersScript mn, string placeholder, float fontSize,
            bool bench = false, string wellName = "FilterWell")
        {
            if (_field != null)
            {
                return true;
            }
            _bench = bench;
            if (parent == null || mn == null || mn.uiMN == null
                || mn.uiMN.mainInputField == null)
            {
                return false;
            }
            // A REBUILT WIDGET IS PAPER AGAIN. The instance outlives a scene load
            // (the note above the key patches), so the look it last wore is still
            // recorded while the new widget below is built plain - and Wear, Carve,
            // Shed and Sheet all skip a look already recorded: the vicinity's well
            // came back with ink text on the leather and the paper's rim on.
            _carved = -1;
            _wornFamily = null;
            _border = 12f;
            _laidOut = new Vector3(-1f, -1f, -1f);
            // THE KIT'S TEXT FIELD (docs/ui-kit.md §3): kit_paper cream with
            // kit_paper_rim ink over it, ink text, placeholder #967D5F. Two layers
            // rather than one so the body and its rim tint separately -- the same
            // prefab the kit's light and dark tooltips are built from.
            //
            // It is CREAM in both windows, and that is the kit's call rather than
            // an oversight of D46. A sheet of paper laid on a bench and a sheet
            // laid on a ledger are the same sheet; making the paper follow the
            // surface would give the two windows two different controls that do
            // the same job, which is the opposite of what a kit is for.
            var wellGo = Owned.New(wellName);
            wellGo.transform.SetParent(parent, false);
            _well = wellGo.AddComponent<Image>();
            _well.sprite = Theme.Kit("kit_paper", 12);
            _well.type = Image.Type.Sliced;
            _well.color = Theme.KitCream;
            _well.raycastTarget = true;

            var rimGo = Owned.New("Rim");
            rimGo.transform.SetParent(wellGo.transform, false);
            _rim = rimGo.AddComponent<Image>();
            _rim.sprite = Theme.Kit("kit_paper_rim", 12);
            _rim.type = Image.Type.Sliced;
            _rim.color = Theme.KitInk;
            _rim.raycastTarget = false;
            Stretch((RectTransform)rimGo.transform);

            GameObject go = Owned.Instantiate(mn.uiMN.mainInputField.gameObject, wellGo.transform);
            go.name = "Field";
            _field = go.GetComponent<TMP_InputField>();
            // The dialog prefab sizes itself after layout. In a fixed header that
            // overwrites our rect on the next canvas pass (25 high in a 13 high well).
            foreach (Behaviour behaviour in go.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour is ILayoutController || behaviour is Animator)
                    behaviour.enabled = false;
            }
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.contentType = TMP_InputField.ContentType.Standard;
            _field.richText = false;
            _field.transition = Selectable.Transition.None;
            // the clone carries the original's serialized listeners
            _field.onValueChanged = new TMP_InputField.OnChangeEvent();
            _field.onEndEdit = new TMP_InputField.SubmitEvent();
            _field.onSubmit = new TMP_InputField.SubmitEvent();
            _field.onSelect = new TMP_InputField.SelectionEvent();
            _field.onDeselect = new TMP_InputField.SelectionEvent();
            _field.text = "";
            // Forced, not assumed. Both come from whatever state the dialog we
            // cloned was left in, and either one silently makes Select() a no-op
            // and the field impossible to focus -- a failure with no error and no
            // visible cause, which is the kind this feature has already produced
            // twice.
            _field.interactable = true;
            _field.readOnly = false;
            // the source is a dialog-style input: drop its attached "OK" button
            foreach (Button button in go.GetComponentsInChildren<Button>(true))
            {
                Object.Destroy(button.gameObject);
            }
            var background = go.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0f, 0f, 0f, 0f); // the paper is the surface
                // Nothing inside answers raycasts. We route the click ourselves
                // (see HitTest), and a raycast target in here would only take hits
                // away from the paper that is absorbing them.
                background.raycastTarget = false;
            }
            if (_field.textComponent != null)
            {
                _field.textComponent.enableAutoSizing = false;
                _field.textComponent.fontSize = fontSize;
                _field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                _field.textComponent.margin = Vector4.zero;
                _field.textComponent.fontStyle = FontStyles.Normal;
                _field.textComponent.enableWordWrapping = false;
                _field.textComponent.color = Theme.KitInk;
                Theme.FlattenType(_field.textComponent);
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ph.text = placeholder;
                ph.enableAutoSizing = false;
                ph.fontSize = fontSize;
                ph.alignment = TextAlignmentOptions.MidlineLeft;
                ph.margin = Vector4.zero;
                ph.fontStyle = FontStyles.Normal;
                ph.enableWordWrapping = false;
                ph.color = Theme.KitPlaceholder;
                Theme.FlattenType(ph);
            }
            _count = Theme.MakeText(wellGo.transform, "Count", Theme.FontCaption,
                Theme.KitPlaceholder, TextAlignmentOptions.MidlineRight);
            Theme.FlattenType(_count);
            // the x that clears the query, at the field's right end, only while
            // there is a query to clear (the old docked search had one, and it is
            // the one thing the field owes the mouse)
            _clear = Theme.KitImage(wellGo.transform, "Clear", "kit_cross", 0, Theme.KitPlaceholder);
            _clear.raycastTarget = false;
            _clear.enabled = false;

            // AND SWITCH IT ON. Instantiate copies the source's active state, and
            // the source is a dialog input that is switched OFF whenever no dialog
            // is up -- which is every moment we clone it. An inactive TMP field
            // runs no Update, so: no caret, no placeholder, no typing, and Select()
            // silently does nothing on an inactive object.
            //
            // That is the whole of "cant click it, cant type anything, no
            // placeholder" -- one symptom set from one cause, which is why the two
            // input fixes before this changed nothing. The old BrowserSearch had
            // this line and I did not carry it over.
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }
            if (_field.placeholder != null && !_field.placeholder.gameObject.activeSelf)
            {
                _field.placeholder.gameObject.SetActive(true);
            }
            _count.enableWordWrapping = false;

            _field.onValueChanged.AddListener(OnChanged);
            // a widget rebuilt after a scene load shows the query it still holds
            if (_query.Length > 0)
            {
                _field.text = _query;
            }
            FilterInput.Register(this);
            if (_bench)
            {
                Paint(Theme.CraftSurface);
            }
            return true;
        }

        // The bench material, applied live when the crafting surface changes
        // (BrowserShell.ApplyCraftSurface). Well and text from the surface; the
        // rim is the surface's text at a third, so the paper's edge is still
        // there without being a drawn border.
        // CARVED INTO THE GROUND, a normal simple field rather than an organic
        // one: no paper, no rim - a plain rounded well that is the
        // ground sunk a shade, the text in the ground's own ink (ink on the
        // parchment, cream on the bench), the count and the x in the same.
        // `dark` is the bench. Idempotent; the slot calls it every attach.
        public void Carve(bool dark, bool cool = false)
        {
            int want = dark ? 1 : cool ? 2 : 0;
            if (_well == null || _carved == want)
            {
                return;
            }
            if (_carved >= 10)
            {
                Unshed();
            }
            _carved = want;
            _border = 12f;
            _laidOut = Vector3.zero;
            _well.sprite = Theme.RoundedFill;
            _well.type = Image.Type.Sliced;
            _well.color = dark ? Theme.CraftSurface.Well : new Color(0f, 0f, 0f, 34f / 255f);
            _rim.enabled = false;
            Color text = dark ? Theme.KitCream : cool ? Theme.RosterInk : Theme.KitInk;
            Color soft = new Color(text.r, text.g, text.b, 0.8f);
            if (_field.textComponent != null)
            {
                _field.textComponent.color = text;
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ph.color = dark ? new Color(text.r, text.g, text.b, 0.72f)
                    : cool ? Theme.RosterSoftInk : Theme.KitPlaceholder;
            }
            _count.color = soft;
            _clear.color = soft;
        }

        // THE TOOL SHED'S FIELD (window-system.md 6.30): what a shed writes a query
        // on - masking tape on the pegboard, a kraft shipping label on the crate, an
        // index card pinned to the cork - in marker, or typed on the label.
        public void Shed(int occupant)
        {
            int want = 10 + occupant;
            if (_well == null || _carved == want)
            {
                return;
            }
            if (_plainFont == null && _field.textComponent != null)
            {
                _plainFont = _field.textComponent.font;
                _plainHintFont = _field.placeholder is TMP_Text hint ? hint.font : _plainFont;
                _plainCountFont = _count.font;
            }
            _carved = want;
            string sprite = occupant == 0 ? "kit_shed_tape" : occupant == 1 ? "kit_shed_label" : "kit_shed_paper";
            _border = occupant == 0 ? 18f : occupant == 1 ? 14f : 8f;
            _laidOut = Vector3.zero;
            _well.sprite = Theme.Kit(sprite, (int)_border);
            _well.type = Image.Type.Sliced;
            _well.color = Color.white;
            _rim.enabled = false;
            TMP_FontAsset face = occupant == 1 ? ToolShed.TypedFont : ToolShed.MarkerFont;
            if (_field.textComponent != null)
            {
                ToolShed.Hand(_field.textComponent, face, ToolShed.Marker);
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ToolShed.Hand(ph, face, ToolShed.Pencil);
            }
            ToolShed.Hand(_count, face, ToolShed.MarkerRed);
            _clear.color = ToolShed.Pencil;
        }

        // THE BLUEPRINT'S FIELD (BrowserShell.Sheet.cs): a ruled box on the sheet,
        // lettered in its face, the count in amber.
        public void Sheet()
        {
            const int want = 20;
            if (_well == null || _carved == want)
            {
                return;
            }
            if (_plainFont == null && _field.textComponent != null)
            {
                _plainFont = _field.textComponent.font;
                _plainHintFont = _field.placeholder is TMP_Text hint ? hint.font : _plainFont;
                _plainCountFont = _count.font;
            }
            _carved = want;
            _border = 6f;
            _laidOut = Vector3.zero;
            _well.sprite = Theme.Kit("kit_bp_box", 6);
            _well.type = Image.Type.Sliced;
            _well.pixelsPerUnitMultiplier = Theme.KitPpu;
            _well.color = Blueprint.Ink;
            _rim.enabled = false;
            TMP_FontAsset face = Blueprint.Face;
            if (_field.textComponent != null)
            {
                ToolShed.Hand(_field.textComponent, face, Blueprint.Ink);
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ToolShed.Hand(ph, face, Blueprint.Dim);
            }
            ToolShed.Hand(_count, face, Blueprint.Lit);
            _clear.color = Blueprint.Dim;
        }

        // THE WINDOW FACELIFT'S FIELD (Facelift.cs): a blank painted well - the
        // vicinity's leather inset, crafting's blueprint box - wearing its focus face
        // while typed in, the query, hint and count in the window's own inks and the
        // HUD's serif. Sizes in reference pixels.
        private string _wornFamily;

        public void Wear(string family, Color query, Color hint, Color count, float queryPx, float countPx)
        {
            int want = family == "craft-filter" ? 31 : 30;
            if (_well == null || _carved == want)
            {
                return;
            }
            if (_plainFont == null && _field.textComponent != null)
            {
                _plainFont = _field.textComponent.font;
                _plainHintFont = _field.placeholder is TMP_Text was ? was.font : _plainFont;
                _plainCountFont = _count.font;
            }
            _carved = want;
            _wornFamily = family;
            _border = 2f;
            _laidOut = Vector3.zero;
            Facelift.Skin(_well, family + "-idle");
            _well.color = Color.white;
            _rim.enabled = false;
            TMP_FontAsset face = InventoryWindow.HudBodyFont;
            if (_field.textComponent != null)
            {
                ToolShed.Hand(_field.textComponent, face, query);
                _field.textComponent.fontSize = queryPx * Facelift.Px;
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ToolShed.Hand(ph, face, hint);
                ph.fontSize = queryPx * Facelift.Px;
            }
            ToolShed.Hand(_count, face, count);
            _count.fontSize = countPx * Facelift.Px;
            _clear.color = hint;
        }

        // the worn well's focus face, every frame (Tick)
        internal void PaintWorn()
        {
            if (_wornFamily == null || _carved < 30 || _well == null)
            {
                return;
            }
            Facelift.Skin(_well, _wornFamily + (IsFocused ? "-focus" : "-idle"));
        }

        private void Unshed()
        {
            if (_plainFont != null && _field.textComponent != null)
            {
                _field.textComponent.font = _plainFont;
                if (_field.placeholder is TMP_Text ph)
                {
                    ph.font = _plainHintFont ?? _plainFont;
                }
            }
            if (_plainCountFont != null)
            {
                _count.font = _plainCountFont;
            }
        }

        public void Paint(Theme.Surface s)
        {
            if (_well == null || !_bench || _carved >= 0)
            {
                return;
            }
            _well.color = s.Well;
            _rim.color = new Color(s.Text.r, s.Text.g, s.Text.b, 0.35f);
            if (_field.textComponent != null)
            {
                _field.textComponent.color = s.Text;
            }
            if (_field.placeholder is TMP_Text ph)
            {
                ph.color = new Color(s.TextSoft.r, s.TextSoft.g, s.TextSoft.b, 0.8f);
            }
            _count.color = s.TextSoft;
            _clear.color = s.TextSoft;
        }

        private void OnChanged(string value)
        {
            _query = value ?? "";
            RebuildTerms();
            if (_clear != null && _clear.enabled != Active)
            {
                _clear.enabled = Active;
            }
        }

        // Stretch the field inside the well and park the count at its right end.
        // Called after every geometry change rather than once, because both
        // windows resize under it.
        // the hint in the empty well, set at each show so a language switch reaches it
        public void SetPlaceholder(string text)
        {
            if (_field != null && _field.placeholder is TMP_Text ph && ph.text != text)
            {
                ph.text = text;
            }
        }

        public void Layout(float countWidth)
        {
            if (_field == null)
            {
                return;
            }
            const float pad = 4f;
            // THE BORDER HAS TO FIT INSIDE THE CONTROL. kit_paper is 256x128 with a
            // 12px border, and a sliced Image maps those pixels 1:1 to UI units by
            // default -- so on a 13-unit-tall filter row the top and bottom borders
            // alone want 24 units and Unity renders the corners overlapping into
            // mush. pixelsPerUnitMultiplier is the dial for exactly this (Theme's
            // FramePixelsPerUnit carries the same note about the panel frame).
            //
            // Derived from the ACTUAL height rather than hardcoded, so the same
            // widget is correct in a 13-unit crafting row and an 18-unit heading:
            // aim for the border to be about a quarter of the height.
            float h = Mathf.Max(8f, ((RectTransform)_well.transform).rect.height);
            // A THIRD of the height, not a quarter. The rim's ink sits in the outer
            // 3px of a 12px border, so squeezing that border harder does not thin
            // the line evenly -- it squashes the top and bottom edges while the
            // corners, which are not scaled on both axes, keep their weight. The
            // result reads as a box with strong corners and a faint lid.
            // the blueprint's box is a rule, not a paper edge: the kit's own weight
            float ppu = _carved >= 20 ? Theme.KitPpu : Mathf.Max(1f, _border / (h / 3f));
            _well.pixelsPerUnitMultiplier = ppu;
            _rim.pixelsPerUnitMultiplier = ppu;
            // THE TEXT RECT IS LAID OUT ONCE PER GEOMETRY, NOT PER CALL. TMP scrolls
            // a single-line field by moving the text component inside its viewport
            // as the caret runs past the right edge, and that offset IS the scroll
            // state. Layout runs again whenever the crafting panel reopens - which
            // the refill after a typing pause does - and re-stretching the text
            // rect zeroed the offset: while you typed the field showed the caret
            // end, and a quarter second after you stopped it snapped back to show
            // the start with the end cut off. The
            // field's own rect and the count are geometry and may be restated;
            // the text and placeholder are only touched when the well changed.
            var fr = (RectTransform)_field.transform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(pad, 0f);
            fr.offsetMax = new Vector2(-(pad + countWidth + ClearW + 2f), 0f);
            var key = new Vector3(((RectTransform)_well.transform).rect.width, h, countWidth);
            if (key != _laidOut)
            {
                _laidOut = key;
                if (_field.textViewport != null)
                {
                    _field.textViewport.anchorMin = Vector2.zero;
                    _field.textViewport.anchorMax = Vector2.one;
                    _field.textViewport.offsetMin = Vector2.zero;
                    _field.textViewport.offsetMax = Vector2.zero;
                }
                if (_field.textComponent != null)
                {
                    Stretch(_field.textComponent.rectTransform);
                }
                if (_field.placeholder is TMP_Text ph)
                {
                    Stretch(ph.rectTransform);
                }
            }
            var cr = (RectTransform)_count.transform;
            cr.anchorMin = new Vector2(1f, 0.5f);
            cr.anchorMax = new Vector2(1f, 0.5f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-(pad + ClearW + 2f), 0f);
            cr.sizeDelta = new Vector2(Mathf.Max(10f, countWidth), 12f);
            var xr = _clear.rectTransform;
            xr.anchorMin = new Vector2(1f, 0.5f);
            xr.anchorMax = new Vector2(1f, 0.5f);
            xr.pivot = new Vector2(1f, 0.5f);
            xr.anchoredPosition = new Vector2(-pad, 0f);
            xr.sizeDelta = new Vector2(ClearW, ClearW);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // NO Repaint. It existed to make the well follow its window's surface,
        // and the kit answers that differently: the paper is cream on both, so
        // there is nothing left for a window to say about it. Deleted rather than
        // left as a no-op -- a method that does nothing is a claim that it might.

        // Whatever the window wants to say about the result: a match count, a
        // "2 of 14", or nothing at all.
        public void SetCount(string text)
        {
            if (_count != null && _count.text != text)
            {
                _count.text = text ?? "";
            }
        }

        public void Clear()
        {
            if (_field != null)
            {
                _field.text = "";
            }
        }

        // Is the pointer over this field. Public because FOCUS IS THE WINDOW'S TO
        // GIVE here: every control in this mod is driven by polled Input plus a
        // rect test rather than by the EventSystem (see BrowserShell's cells and
        // InventoryWindow's slots), and the one thing that was left to Unity's own
        // routing -- clicking an input field -- is the one thing that did not work.
        //
        // Why it cannot work as a child: the click lands on the field's graphic and
        // Unity BUBBLES UP looking for a handler. Up from the field is the paper,
        // then the window. The field's own Selectable is never asked. The old
        // docked field had the same shape and the same answer -- BrowserShell
        // hit-tested the bar and called FocusField by hand.
        public bool HitTest(Vector2 screenPos, Camera cam)
        {
            return Go != null && Go.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)Go.transform, screenPos, cam);
        }

        // Click inside focuses, click outside blurs. Returns true if the click was
        // ours, so the window knows not to also treat it as a click on itself.
        // ONCE PER FRAME, however many windows ask. The slot's one field is
        // polled by both occupants' Ticks, and the second asker undid the first:
        // the crafting window's click on the x cleared and blurred, then the
        // inventory's asked again, found the x gone and the well under the
        // pointer, and focused it back, where clicking the clear button is meant
        // to remove focus from the field. The first answer stands.
        private int _clickFrame = -1;
        private bool _clickResult;

        public bool HandleClick(Vector2 screenPos, Camera cam)
        {
            if (_field == null)
            {
                return false;
            }
            if (_clickFrame == Time.frameCount)
            {
                return _clickResult;
            }
            _clickFrame = Time.frameCount;
            _clickResult = HandleClickOnce(screenPos, cam);
            return _clickResult;
        }

        private bool HandleClickOnce(Vector2 screenPos, Camera cam)
        {
            // the x first: it is inside the well, so the well's test would take it
            if (_clear != null && _clear.enabled && Go.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(_clear.rectTransform, screenPos, cam))
            {
                // and lets go of the keyboard: a cleared field is a finished
                // search, not the start of another
                Clear();
                Blur();
                return true;
            }
            if (HitTest(screenPos, cam))
            {
                Focus();
                return true;
            }
            if (IsFocused)
            {
                Blur();
            }
            return false;
        }

        public void Focus()
        {
            if (_field != null)
            {
                _field.ActivateInputField();
                _field.Select();
            }
        }

        public void Blur()
        {
            if (_field == null)
            {
                return;
            }
            if (_field.isFocused)
            {
                _field.DeactivateInputField();
            }
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.currentSelectedGameObject == _field.gameObject)
            {
                es.SetSelectedGameObject(null);
            }
        }

        // The window this field lived in was destroyed with its scene; the
        // statics that described it are fake-null and must be dropped so Build
        // makes a new one instead of returning a corpse.
        public bool Alive => _field != null;
    }

    // The four rules D28a priced, paid ONCE however many fields exist. Nothing in
    // here knows about a window, and nothing in a window needs to know about it.
    [HarmonyPatch]
    internal static class FilterInput
    {
        internal static readonly char[] SplitChars = { ' ', '\t' };
        private static readonly List<FilterField> Fields = new List<FilterField>();
        private static readonly Dictionary<string, string> SideTextCache =
            new Dictionary<string, string>();
        private static int _sideTextLang = -1;
        private static bool _suppressing;
        private static int _suppressGrace;
        private static bool _bypass;

        internal static void Register(FilterField field)
        {
            if (!Fields.Contains(field))
            {
                Fields.Add(field);
            }
        }

        // Is ANY filter field taking keys right now. This is the whole of what the
        // rest of the game needs to know, and it is why a field per window costs
        // no more than one field did.
        internal static bool AnyFocused
        {
            get
            {
                // the sheet's rename field is typing too (NameEdit)
                if (NameEdit.IsFocused)
                {
                    return true;
                }
                for (int i = 0; i < Fields.Count; i++)
                {
                    if (Fields[i].IsFocused)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // Lowercased searchable side-text per item: localized tips plus the
        // equip-target character names the tooltip generates, so filtering by a
        // character finds their gear. Only NAMED lists are indexed — the tooltip
        // says "everyone" past 100 targets, and indexing that would make a
        // character query match all generic gear. Shared across fields because it
        // does not depend on any query.
        internal static string SideText(ManagersScript mn, ItemData item)
        {
            int lang = SaveManager.SaveSettingStatic.lang;
            if (lang != _sideTextLang)
            {
                SideTextCache.Clear();
                _sideTextLang = lang;
                for (int i = 0; i < Fields.Count; i++)
                {
                    Fields[i].DropCaches();
                }
            }
            if (SideTextCache.TryGetValue(item.name, out string cached))
            {
                return cached;
            }
            string text = item.GetItemTips() ?? "";
            if (mn.randChar != null && mn.randChar.ItemTypeToEquipID(item.itemType) != -1)
            {
                List<int> npcIDs = mn.randChar.EquipableNPCCache(item.name);
                if (npcIDs != null && npcIDs.Count > 0 && npcIDs.Count <= 100)
                {
                    var sb = new System.Text.StringBuilder(text);
                    for (int i = 0; i < npcIDs.Count; i++)
                    {
                        string npcName = "";
                        mn.randChar.DefaultNaming(npcIDs[i], ref npcName);
                        if (npcName.Length > 0)
                        {
                            sb.Append(' ').Append(npcName);
                        }
                    }
                    text = sb.ToString();
                }
            }
            text = text.ToLowerInvariant();
            SideTextCache[item.name] = text;
            return text;
        }

        // No prune, deliberately. A scene load leaves every field's widget as
        // Unity's fake-null, which Alive already reports and Build already
        // repairs in place -- the FilterField instances are static and outlive
        // the scene, so the registry never holds a corpse to sweep.
        // OUR OWN KEY READS HAVE TO OPT OUT OF OUR OWN SUPPRESSION. The patches
        // below answer "not pressed" to every Input.GetKey* while a field is
        // focused, and they cannot tell our call from the game's — so Escape could
        // never leave a focused field, which is the only state Escape is pressed
        // in.
        private static bool KeyDown(KeyCode key)
        {
            _bypass = true;
            try { return Input.GetKeyDown(key); }
            finally { _bypass = false; }
        }

        // ...and the console's, which owns the keyboard the same way
        internal static bool RawKeyDown(KeyCode key)
        {
            return KeyDown(key);
        }

        internal static bool RawKey(KeyCode key)
        {
            _bypass = true;
            try { return Input.GetKey(key); }
            finally { _bypass = false; }
        }

        // WHO OWNS THE KEYBOARD: a focused field, the open console, or the game's
        // rename box. Every key read below answers "not pressed" while one does. The
        // Talk bubble and the Manage key's stem own one key, Escape, and only that one
        // answers so while one is up (TalkBubble.Swallows, ManageFan.Swallows): the walk,
        // the swing and the hotkeys stay live.
        internal static bool Captured
        {
            get { return AnyFocused || DevConsole.Swallowing || VanillaTyping; }
        }

        // THE GAME'S RENAME BOX: typing in it also activated hotkeys. A newer build names a flag through
        // uiMN.nameEditInput (UIManager.NameEdit), and guards only GameManager's
        // main hotkey block on it - the keys read before that block (X, Escape) and
        // every hotkey of ours still fired. Its loop reads no keys and TMP types and
        // submits through the event system, so capturing the keyboard costs it
        // nothing. The sign's box (mainInputField) is NOT captured: SignBoard's own
        // loop reads Return through Input.GetKeyDown.
        //
        // ...AND ITS SIZE AND SCALE FIELDS (build 25095856): the block and scale
        // editors' readings became TMP_InputFields you type numbers into
        // (BuildManager.sizeValueTexts / scaleValueTexts), and the game's own hotkey
        // guard was not told - the digits are the action bar's, so "1.5"
        // fired slot 1. They sit on screen for the whole edit, so only a FOCUSED
        // one owns the keyboard; they read their text through TMP's events too.
        private static TMPro.TMP_InputField _nameEdit;
        private static TMPro.TMP_InputField[] _buildSizes, _buildScales;

        private static bool VanillaTyping
        {
            get
            {
                return (_nameEdit != null && _nameEdit.gameObject.activeInHierarchy)
                    || AnyFocusedIn(_buildSizes) || AnyFocusedIn(_buildScales);
            }
        }

        private static bool AnyFocusedIn(TMPro.TMP_InputField[] fields)
        {
            for (int i = 0; fields != null && i < fields.Length; i++)
            {
                if (fields[i] != null && fields[i].isFocused)
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Tick(ManagersScript mn)
        {
            if (mn == null || mn.uiMN == null)
            {
                return;
            }
            _nameEdit = mn.uiMN.nameEditInput;
            _buildSizes = mn.buildMN != null ? mn.buildMN.sizeValueTexts : null;
            _buildScales = mn.buildMN != null ? mn.buildMN.scaleValueTexts : null;
            bool focused = AnyFocused;
            for (int i = 0; i < Fields.Count; i++)
            {
                Fields[i].PaintWorn();
            }
            // ESCAPE LEAVES THE FIELD, and never reaches the window under it. One
            // rule, one place, however many fields — D28a's second cost, paid once.
            if (focused && KeyDown(KeyCode.Escape))
            {
                for (int i = 0; i < Fields.Count; i++)
                {
                    if (Fields[i].IsFocused)
                    {
                        Fields[i].Blur();
                    }
                }
                focused = false;
            }
            // the console keeps its own grace (DevConsole.Swallowing)
            bool suppress = focused || DevConsole.Swallowing;
            if (focused)
            {
                _suppressGrace = 3;
            }
            else if (_suppressGrace > 0)
            {
                _suppressGrace--;
                suppress = true;
            }
            // GameManager.Update's hotkey guard (and our own) check
            // uiMN.nameInput.enabled — flag it while typing, and for a few grace
            // frames after blur: TMP releases focus during the input-event phase,
            // so on the Enter that blurs a field, DebugTool's Return handler would
            // otherwise fire the same frame and open the command box.
            if (suppress != _suppressing && mn.uiMN.nameInput != null)
            {
                mn.uiMN.nameInput.enabled = suppress;
                _suppressing = suppress;
            }
        }

        // ---------- keeping typing out of the game ----------

        // movement/interactions ask PlayerMove.NowInput; count our fields as typing
        [HarmonyPatch(typeof(PlayerMove), nameof(PlayerMove.NowInput))]
        [HarmonyPostfix]
        private static void NowInput_Postfix(ref bool __result)
        {
            __result |= Captured;
        }

        // While a field is focused, ALL Input.GetKey* queries answer "not pressed".
        // The game reads keys in many unguarded places (X toggles run before
        // GameManager's guard, H closes menus from raw coroutine waits), so
        // per-site guards cannot win — suppress at the chokepoint instead. TMP's
        // own typing and submit handling uses the UI event system rather than
        // Input.GetKey, so the fields themselves are unaffected; mouse buttons are
        // a separate API and stay live.
        [HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
        [HarmonyPrefix]
        private static bool GetKey_Suppress(KeyCode key, ref bool __result)
        {
            if (_bypass || !(Captured || TalkBubble.Swallows(key) || ManageFan.Swallows(key)))
            {
                return true;
            }
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPrefix]
        private static bool GetKeyDown_Suppress(KeyCode key, ref bool __result)
        {
            if (_bypass || !(Captured || TalkBubble.Swallows(key) || ManageFan.Swallows(key)))
            {
                return true;
            }
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
        [HarmonyPrefix]
        private static bool GetKeyUp_Suppress(KeyCode key, ref bool __result)
        {
            if (_bypass || !(Captured || TalkBubble.Swallows(key) || ManageFan.Swallows(key)))
            {
                return true;
            }
            __result = false;
            return false;
        }

        // VANILLA'S RETURN BOX IS SILENCED. DebugTool.Update does
        // nothing but open its command line on Return, and Return confirms our
        // own dialogs now; the drop-down console runs the same DebugCodeEnter.
        // With the setting off, Enter still belongs to a focused field and keeps
        // the box shut that frame.
        [HarmonyPatch(typeof(DebugTool), "Update")]
        [HarmonyPrefix]
        private static bool DebugToolUpdate_Prefix()
        {
            if (Settings.SilenceVanillaConsole.Value)
            {
                return false;
            }
            if (!KeyDown(KeyCode.Return) || !AnyFocused)
            {
                return true;
            }
            ManagersScript mn = GameObject.Find("Managers")?.GetComponent<ManagersScript>();
            if (mn != null && mn.gameMN != null && mn.gameMN.debugCanvas != null
                && mn.gameMN.debugCanvas.activeSelf)
            {
                return true; // the console is open and needs its own Enter
            }
            return false;
        }
    }
}
