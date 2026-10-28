using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI.UI
{
    // "Island UI" design tokens: keep the game's material language (parchment
    // frame, slot brown, its own TMP font, its own stat colors), impose OUR
    // discipline: opaque reading surfaces, one type ladder, one spacing rhythm,
    // uniform states. Every custom component reads from here — a future reskin is
    // a token swap, not a rewrite.
    internal static class Theme
    {
        // palette — SURFACE-AWARE: the game's frame sprite is light parchment, so
        // text on it is dark INK (the game's own "Basic"/"Craft" labels); cream is
        // reserved for dark surfaces (buttons, slot cells)
        public static readonly Color Ink = new Color(0.38f, 0.29f, 0.20f, 1f);    // for parchment surfaces
        public static readonly Color InkDim = new Color(0.55f, 0.45f, 0.34f, 1f);
        public static readonly Color TextCream = new Color(0.95f, 0.92f, 0.85f, 1f);
        public static readonly Color TextDim = new Color(0.72f, 0.67f, 0.58f, 1f);
        public static readonly Color AccentGold = new Color(0.95f, 0.88f, 0.70f, 1f);
        public static readonly Color Positive = new Color(0.20f, 1.00f, 0.60f, 1f);  // the game's #33FF99 (dark surfaces)
        public static readonly Color Negative = new Color(1.00f, 0.25f, 0.30f, 1f);  // vivid red (dark surfaces)
        // the game's own two slot-corner readouts (InventoryManager.StackTextChange):
        // green = remaining durability, magenta = a captured creature's happiness
        public static readonly Color Durability = new Color(0f, 1f, 0f, 1f);       // #00ff00
        public static readonly Color Happiness = new Color(1f, 0f, 1f, 1f);        // #ff00ff
        public static readonly Color PlateDark = new Color(0.16f, 0.12f, 0.08f, 1f); // same family as the search field
        // The light surface's flat fill, for the places a sliced frame cannot
        // go (a header cover strip would draw a border across the panel). SAMPLED
        // from a screenshot of the game's own panel — (246, 216, 188) — rather than
        // guessed: the first value was picked by eye and read as a visible tan band
        // across the top of a pale pink panel.
        //
        // ...and then the sample was written into the COMMENT and not into the
        // value. The number that shipped, (237,183,139), is vanilla's RIM: a
        // histogram of the game's own bag panel finds it on 9.5k pixels of trim
        // around a field of (246,216,188) on 41k. Every panel we drew was
        // therefore filled with the trim colour, which is the whole of why the UI
        // read orange and heavy next to vanilla's.
        public static readonly Color ParchmentFill = new Color(246f / 255f, 216f / 255f, 188f / 255f, 1f);
        // The rim itself, kept as a rim. Vanilla's panel reads brown edge, thin
        // orange rim, cream field, and the middle of that sandwich is this.
        public static readonly Color ParchmentRim = new Color(237f / 255f, 183f / 255f, 139f / 255f, 1f);
        // vanilla's empty slot interior, measured the same way: a neutral warm
        // grey-brown, and notably NOT a tint of the parchment. A slot is a hole in
        // the panel's material, not a darker shade of it.
        public static readonly Color SlotWell = new Color(105f / 255f, 91f / 255f, 79f / 255f, 1f);
        // the loadout keeps the game's own equip-strip green so "what I am
        // wearing" stays a colour, not a label
        public static readonly Color PlateEquip = new Color(0.11f, 0.17f, 0.10f, 1f);
        public static readonly Color EdgeEquip = new Color(0.36f, 0.55f, 0.28f, 1f);
        public static readonly Color EdgeDefault = new Color(0.45f, 0.34f, 0.22f, 1f);
        public static readonly Color EdgeTarget = new Color(0.62f, 0.50f, 0.30f, 1f);
        public static readonly Color FieldDark = new Color(0.10f, 0.075f, 0.05f, 1f); // input wells sunk INTO a plate
        // The same idea on parchment. A well must read as SUNK, which means
        // darker than the surface — but FieldDark is near-black, and a black slot
        // in a pale panel reads as a hole punched through it rather than a dip in
        // it. Two steps down from ParchmentFill, not fifteen.
        // A well sunk into parchment, not a slab laid on it: one step down from
        // ParchmentFill, far enough to read as recessed and no further. The old
        // value was most of the way to Ink, which made every field the heaviest
        // thing on its panel.
        public static readonly Color FieldOnLight = new Color(0.86f, 0.66f, 0.50f, 1f);
        public static readonly Color DisabledTint = new Color(0.784f, 0.784f, 0.784f, 0.502f);
        // An item that does not answer the current search. DIMMED, never
        // hidden — a grid that reflowed as you typed would move the thing you were
        // reaching for, and the whole value of marking over filtering is that the
        // layout holds still. Deliberately NOT DisabledTint: "you cannot craft
        // this" and "this is not what you asked for" are different statements and
        // must not wear the same face.
        public static readonly Color SearchMiss = new Color(0.45f, 0.45f, 0.45f, 0.4f);
        // a cell under the finger: multiplied over the vanilla slot frame, so it
        // reads as the same slot pressed in rather than a different colour
        public static readonly Color PressTint = new Color(0.62f, 0.62f, 0.62f, 1f);
        // (Marking an occupied slot is OPEN again. Four shapes tried and all reverted: vanilla's
        // empty-slot sprite is a blank so it marked nothing; a lighter well behind
        // the art was brown on the loadout's green and invisible on the bag; and a
        // light Outline on the icon made every item read BLURRY, because Unity's
        // Outline draws four offset copies of the sprite rather than tracing it.
        // Whatever comes next must not multiply the art or repaint the plate.)
        public static readonly Color ButtonFace = new Color(0.31f, 0.23f, 0.15f, 1f);   // dark tab-bar brown
        public static readonly Color ButtonHover = new Color(0.42f, 0.31f, 0.20f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.31f, 0.23f, 0.15f, 0.45f);

        // ---------- surfaces (D46: parchment holds, wood works) ----------

        // The palette above has always been SURFACE-AWARE — Ink/FieldOnLight for
        // light surfaces, TextCream/FieldDark for dark ones — but the pairing lived
        // in each consumer's head, which is exactly how a taste rule with nowhere
        // to live becomes a law nobody can find. A Surface is that pairing given a
        // home: pick one, and the field, the type and the wells all follow from it.
        //
        // TWO instances, and there is no third waiting. The whole point of D46 is
        // that a window's material says what the window is FOR, so a new material
        // would have to mean a new kind of surface, not a new mood.
        internal sealed class Surface
        {
            public Color Field;      // the material inside the frame
            public Color Rim;        // the band the frame paints just inside its border
            public Color Text;       // primary type
            public Color TextSoft;   // secondary type, disclosure glyphs
            public Color Well;       // an input or a pill set INTO this surface
            public Color Accent;     // headings, selection
        }

        // A LEDGER. Bags, containers, characters — surfaces that record what
        // exists and where it is.
        public static readonly Surface Parchment = new Surface
        {
            Field = ParchmentFill,
            Rim = ParchmentRim,
            Text = Ink,
            TextSoft = InkDim,
            Well = FieldOnLight,
            Accent = Ink,
        };

        // A BENCH. The surface things are made on, and it is DARK — the first
        // pass made it a mid-wood #603E28 and it looked poor, which the
        // candidate sheet then explained: at that
        // value the field, vanilla's slot sprite and the frame's poles are all the
        // same brown at the same weight, so the grid turns to soup. Going dark
        // separates them, and the item art — the thing you are actually reading —
        // stops competing with its own background.
        //
        // The field sits within a few points of PlateDark, and that is FINE here
        // where parchment found it fatal, because the difference between this window and
        // the item card is no longer carried by colour: this one wears the frame
        // and the card does not. That is the whole argument for keeping the
        // harvested frame while the field goes dark — CONSTRUCTION separates them,
        // so the field is free to be as dark as it needs to be.
        public static readonly Surface Wood = new Surface
        {
            Field = new Color(51f / 255f, 46f / 255f, 41f / 255f, 1f),   // #332E29
            Rim = new Color(32f / 255f, 22f / 255f, 16f / 255f, 1f),
            Text = TextCream,
            TextSoft = TextDim,
            // LIGHTER than the field, and this is the one that had to be learned
            // twice. FieldOnLight's rule was "a well is one step darker, not
            // fifteen" — but on a near-black field there is no darker to step to,
            // and a well that tries reads as nothing at all. The rule underneath
            // both is that a well steps AWAY from whatever extreme its surface is
            // at. The old dark crafting window did exactly this (plate 39,26,21;
            // its filter field 58,44,28) which is how we know it works on screen
            // and not just on paper.
            Well = new Color(69f / 255f, 60f / 255f, 51f / 255f, 1f),    // #453C33
            Accent = AccentGold,
        };

        // The crafting window's surface: a making surface is wood.
        public static Surface CraftSurface => Wood;

        // type ladder (game font, no auto-sizing) — dense card scale
        public const float FontHeader = 10f;
        public const float FontBody = 8f;
        public const float FontCaption = 7f;

        // rhythm
        public const float Pad = 6f;
        public const float Gap = 4f;
        public const float RowHeight = 18f;

        // A CAPTION'S ROW, and it is not decoration: the game's font lays a
        // FontCaption line out taller than 7 units, and MakeText defaults to
        // Ellipsis overflow — which does not clip a line too tall for its rect, it
        // DROPS it. Every section heading on the item card sat in a 9-unit box and
        // therefore rendered nothing at all: no error, no clipping, and the space
        // still reserved, so the card looked like it had unexplained gaps.
        // Anything ≥10 rendered fine, which is why the stat rows (10) never showed
        // the bug. Captions get this, and MakeHeading gets Overflow as well, so a
        // heading can never fail silently again.
        public const float CaptionH = 11f;

        // A SECTION HEADING, defined once. The browser labels a workbench group
        // with it and the Inventory labels a container group with it, so the two
        // read at the same weight instead of drifting apart.
        public const float SectionRowHeight = 16f;
        public const float SectionIconSize = 12f;

        // discovered from the live scene (fake-null on sprites detects reloads)
        public static Sprite Frame;
        public static Image.Type FrameType = Image.Type.Sliced;
        public static float FramePixelsPerUnit = 1f; // border thickness scale — MUST match the source panel
        public static Sprite SlotFrame;
        // (Settled: `ItemManager.emptySprite`, which vanilla puts on a cleared
        // slot, is `2dcube_alpha` at 4x4 — a transparent blank used to ERASE the
        // previous icon, not a mark. So there is nothing to harvest for the empty
        // side, which is why occupancy is marked on the art instead. See ArtEdge.)
        public static GameObject BuildButtonTemplate; // the game's bottom-left hammer button
        public static TMP_FontAsset Font;

        // ---- harvested container skins ----
        // Vanilla itself uses TWO plate materials, and the material is the
        // container-type signal: cream parchment for bags (bottom bar, character
        // bag) and translucent green + brass for the loadout. We reuse both
        // rather than invent a third language.
        public sealed class Skin
        {
            public Sprite Sprite;
            public Image.Type Type = Image.Type.Sliced;
            public float PixelsPerUnit = 1f;
            public Color Color = Color.white;
            public bool Valid => Sprite != null;
        }

        // A sprite BY NAME, from whatever the game has loaded - and failing that,
        // from a PNG you drop in BepInEx/plugins/islandui-art. That second half is
        // the answer to "can we add our own assets": yes, and it costs a file read,
        // because a Texture2D built at runtime makes a Sprite like any other. No
        // asset bundle, no rebuild, no importer.
        //
        // Cached both ways, misses included: Resources.FindObjectsOfTypeAll walks
        // every loaded object, which is fine once and not fine every refresh.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> NamedSprites
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        // The SHAPE of a sprite, in white, ready to be painted any colour.
        //
        // This is the "colouring mask" the tint actually wants, and Unity does not
        // do it by itself: Image.color MULTIPLIES the sprite's own pixels, so a
        // dark sprite stays dark whatever colour you hand it. Most of this game's
        // painted bars are dark or coloured - ui_faintimage_back is #0C0C0C,
        // ui_lifeimage is #930000 - so tinting them directly produces mud, and only
        // ui_faintimage happens to be white enough to work untouched.
        //
        // Keeping the alpha and discarding the RGB turns every one of them into a
        // usable stencil, which is what makes the whole sprite library available as
        // brush shapes rather than the one that happens to be white.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> MaskSprites
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite FindMask(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            name = name.Trim();
            if (MaskSprites.TryGetValue(name, out Sprite hit))
            {
                return hit;
            }
            Sprite source = FindSprite(name);
            Sprite mask = source != null ? BuildMask(source) : null;
            MaskSprites[name] = mask;
            return mask;
        }

        // A mask of a sprite held by reference rather than by name - an item's own
        // icon, which has no name FindSprite could look up. The tool shed paints a
        // recipe's outline on the pegboard from it (window-system.md 6.30).
        private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> MaskOfSprite
            = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

        public static Sprite MaskOf(Sprite source)
        {
            if (source == null)
            {
                return null;
            }
            if (MaskOfSprite.TryGetValue(source, out Sprite hit) && hit != null)
            {
                return hit;
            }
            Sprite mask = BuildMask(source, quiet: true);
            MaskOfSprite[source] = mask;
            return mask;
        }

        // THE SAME ART WITHOUT ITS COLOUR (an item on the action bar that is no
        // longer in your bags should say so - "ghosted"). Luminance kept,
        // alpha kept, so a caller's Image.color alpha makes it a ghost.
        private static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> GreyOfSprite
            = new System.Collections.Generic.Dictionary<Sprite, Sprite>();

        public static Sprite GreyOf(Sprite source)
        {
            if (source == null)
            {
                return null;
            }
            if (GreyOfSprite.TryGetValue(source, out Sprite hit) && hit != null)
            {
                return hit;
            }
            Sprite grey = BuildMask(source, quiet: true, grey: true);
            GreyOfSprite[source] = grey;
            return grey;
        }

        private static Sprite BuildMask(Sprite source, bool quiet = false, bool grey = false)
        {
            try
            {
                // via a RenderTexture, because an atlas page is not CPU-readable -
                // the same route SpriteDump takes for the same reason
                // The sprite's own rect, not its page: the items' atlas is 8192 px
                // square, and a target that size per mask was a quarter of a gigabyte
                // allocated to read 256 px of it (found with the blueprint's drawings,
                // which read the same way - Blueprint.ReadSprite, which also draws a
                // tight-packed sprite from its mesh, having no rectangle to read).
                Rect r = Blueprint.SourceRect(source);
                int w = Mathf.Max(1, (int)r.width);
                int h = Mathf.Max(1, (int)r.height);
                Color32[] px = Blueprint.ReadSprite(source, w, h);
                var flat = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int i = 0; i < px.Length; i++)
                {
                    if (grey)
                    {
                        byte l = (byte)Mathf.Clamp(0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b, 0f, 255f);
                        px[i] = new Color32(l, l, l, px[i].a);
                        continue;
                    }
                    // shape only: white at the pixel's own opacity, so Image.color
                    // multiplies against 1 and the tint survives intact
                    px[i] = new Color32(255, 255, 255, px[i].a);
                }
                flat.SetPixels32(px);
                flat.Apply();
                flat.wrapMode = TextureWrapMode.Clamp;
                // Bilinear, not Point. A mask is STRETCHED into a band - often
                // several times its own height - and Point turns a ragged edge into
                // blocky teeth, which is exactly how ui_faintimage failed: a 13px
                // hairline blown up 6x read as a comb rather than a brush.
                flat.filterMode = FilterMode.Bilinear;
                if (!quiet)
                {
                    Plugin.Log.LogInfo($"Theme: mask built from '{source.name}' ({w}x{h})");
                }
                // FullRect: the default tight mesh is made by analysing the texture
                return Sprite.Create(flat, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Theme: could not mask '{source.name}': {e.Message}"
                    + " - using it unmasked, which will be wrong unless it is white");
                return source;
            }
        }

        // ---------- the UI kit (docs/ui-kit.md) ----------

        // Kit palette, transcribed from docs/ui-kit.md §4. Named rather than
        // inlined because the kit is a contract between two sessions: if these
        // drift from the table, the drift is a finding.
        public static readonly Color KitCream = ParseColor("#EAD4B0", TextCream);
        public static readonly Color KitInk = ParseColor("#281A10", Ink);
        public static readonly Color KitPlaceholder = ParseColor("#967D5F", InkDim);
        // THE DOSSIER'S PRINTING: a character sheet is
        // a printed form on fresh paper, so its type and rules are one brown ink at
        // three strengths, with the page's one red kept for what cannot be undone.
        public static readonly Color DossierInk = ParseColor("#342418", Ink);
        public static readonly Color DossierLabel = ParseColor("#705E4A", InkDim);    // a field's name
        public static readonly Color DossierSoft = ParseColor("#968268", InkDim);     // a caption, a blank line
        public static readonly Color DossierFaded = ParseColor("#563E26", InkDim);    // a tab further back
        public static readonly Color DossierRule = ParseColor("#DECEB4", InkDim);     // the ruling between rows
        public static readonly Color DossierRed = ParseColor("#A82622", Ink);
        public static readonly Color DossierTrack = ParseColor("#3A2C20", Ink);       // a bar's empty part
        // experience is the HUD's cyan: GameManager.expImage's own tint, read live
        // where it can be (InventoryWindow.ExpTint) and this where it cannot
        public static readonly Color DossierExp = ParseColor("#00E1FF", KitCream);
        // the skill level's experience is a light green, so the save's one skill
        // level never reads as a character's own level
        public static readonly Color DossierSkillExp = ParseColor("#83F26D", KitCream);
        // Roster paper is a restrained stone tint of the same inventory material.
        // Apply only to its outer frame; pooled frames reset to white for vicinity.
        public static readonly Color RosterFrameTint = new Color(0.86f, 0.91f, 0.99f, 1f);
        public static readonly Color RosterControl = ParseColor("#596775", Ink);
        public static readonly Color RosterControlHot = ParseColor("#6A7882", Ink);
        public static readonly Color RosterControlDown = ParseColor("#46535A", Ink);
        public static readonly Color RosterSelected = ParseColor("#A9AFAC", ParchmentFill);
        public static readonly Color RosterInk = ParseColor("#35434A", Ink);
        public static readonly Color RosterSoftInk = ParseColor("#626B6C", InkDim);
        public static readonly Color RosterControlText = ParseColor("#EEEAE0", KitCream);


        // Sprite pixels per canvas unit for the kit. The mockups were drawn at
        // 1440p and the canvas is 450 units tall (CanvasScaler 800x450, which is
        // a scale of 3.2 on a 1440p screen), so a kit sprite drawn at this
        // multiplier is exactly the size it was designed at, and the 9-slice
        // borders in docs/ui-kit.md's table come out the thickness they were
        // drawn. Sizes in that table divide by this to become canvas units:
        // the 300x64 title plate is 94x20, a 64-tall plank is 20.
        public const float KitPpu = 3.2f;

        // A kit sprite on an Image, at its designed size: sliced when the table
        // gives it a border, simple with its aspect kept when it is a mark or a
        // fixed plate.
        public static void KitSkin(Image image, string name, int border)
        {
            image.sprite = Kit(name, border);
            image.type = border > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = border == 0;
            image.pixelsPerUnitMultiplier = KitPpu;
        }

        // A cut piece (Kit with a border per side) on an Image, sliced at its
        // designed size.
        public static void KitSkin(Image image, string name, Vector4 border)
        {
            image.sprite = Kit(name, border);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = KitPpu;
        }

        // `stretch`: a borderless mask that fills its rect rather than keeping its
        // aspect - the brush-stroke rule, drawn to whatever width a row has.
        // THE KIT'S TYPEFACE. The
        // mockups are set in Georgia - bold on wood, regular for body - and Unity
        // can build a TMP font from an installed face at runtime, so the kit's
        // text wears it without an asset being shipped. Georgia is a Windows
        // system font and the game is Windows-only; if the face cannot be built
        // the kit falls back to the harvested game font, as before.
        private static TMP_FontAsset _kitFont;
        private static TMP_FontAsset _kitFontBold;
        private static bool _kitFontTried;

        public static TMP_FontAsset KitFont(bool bold)
        {
            if (!_kitFontTried)
            {
                _kitFontTried = true;
                _kitFont = OsFont("Georgia");
                _kitFontBold = OsFont("Georgia Bold") ?? _kitFont;
            }
            TMP_FontAsset face = bold ? _kitFontBold : _kitFont;
            return face != null ? face : Font;
        }

        internal static TMP_FontAsset OsFont(string face)
        {
            try
            {
                UnityEngine.Font os = UnityEngine.Font.CreateDynamicFontFromOSFont(face, 48);
                if (os == null)
                {
                    return null;
                }
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(os, 48, 6,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (asset != null)
                {
                    asset.name = "IslandUI " + face;
                    Plugin.Log.LogInfo($"Theme: kit typeface '{face}' built from the OS font");
                }
                return asset;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Theme: kit typeface '{face}' unavailable ({e.Message}); using the game font");
                return null;
            }
        }

        // Kit text: MakeText in the kit's face, flat, bold on request (a faux bold
        // when only the regular face could be built).
        public static TextMeshProUGUI MakeKitText(Transform parent, string name, float size,
            Color color, TextAlignmentOptions align, bool bold = false)
        {
            TextMeshProUGUI text = MakeText(parent, name, size, color, align);
            TMP_FontAsset face = KitFont(bold);
            if (face != null)
            {
                text.font = face;
            }
            if (bold && ReferenceEquals(face, _kitFont))
            {
                text.fontStyle |= FontStyles.Bold;
            }
            FlattenType(text);
            return text;
        }

        public static Image KitImage(Transform parent, string name, string sprite, int border,
            Color color, bool stretch = false)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            KitSkin(image, sprite, border);
            if (stretch)
            {
                image.preserveAspect = false;
            }
            image.color = color;
            return image;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> KitSprites =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        // A KIT SPRITE, WITH ITS 9-SLICE BORDER. This is the first item on
        // docs/ui-kit.md's "what the code side has to add" list, and the reason it
        // is there: LoadSpriteFile creates sprites with a zero border, so a plank
        // or a sheet of paper stretched to a control's width smears its corners
        // across the middle. The border comes from the kit's own table, passed by
        // the caller, because only the kit knows it.
        public static Sprite Kit(string name, int border)
        {
            float b = Mathf.Max(0, border);
            return Kit(name, new Vector4(b, b, b, b));
        }

        // ...or a border per side - left, bottom, right, top, Sprite.border's order -
        // for a piece cut whole out of a painting rather than drawn to the kit's
        // table, whose corners are not square: the sheet's clipboard pieces
        // (make_sheet_facelift.py). A sprite is cut once, so one name, one border.
        public static Sprite Kit(string name, Vector4 border)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            if (KitSprites.TryGetValue(name, out Sprite hit) && hit != null)
            {
                return hit;
            }
            Sprite made = null;
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.Combine(BepInEx.Paths.PluginPath, "islandui-art"), name + ".png");
                if (System.IO.File.Exists(path))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(System.IO.File.ReadAllBytes(path)))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        // Bilinear, for the same reason FindMask uses it: these are
                        // stretched well past their own size, and Point turns the
                        // kit's deliberate edge wobble into stair-steps.
                        tex.filterMode = FilterMode.Bilinear;
                        made = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Theme: could not load kit sprite '{name}': {e.Message}");
            }
            KitSprites[name] = made;
            Plugin.Log.LogInfo(made != null
                ? $"Theme: kit sprite '{name}' loaded ({made.rect.width}x{made.rect.height}, border {border})"
                : $"Theme: kit sprite '{name}' NOT FOUND in BepInEx/plugins/islandui-art");
            return made;
        }

        // A KIT TILE: a seamless texture drawn Image.Type.Tiled (the pegboard, the
        // cork, the crate's boards), so it repeats at its own size instead of
        // stretching. Repeat wrap lets Unity draw the whole field as one quad.
        public static Sprite KitTile(string name)
        {
            Sprite sprite = Kit(name, 0);
            if (sprite != null && sprite.texture != null && sprite.texture.wrapMode != TextureWrapMode.Repeat)
            {
                sprite.texture.wrapMode = TextureWrapMode.Repeat;
            }
            return sprite;
        }

        public static Sprite FindSprite(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            name = name.Trim();
            if (NamedSprites.TryGetValue(name, out Sprite hit))
            {
                return hit;
            }
            Sprite found = null;
            Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    found = all[i];
                    break;
                }
            }
            if (found == null)
            {
                found = LoadSpriteFile(name);
            }
            NamedSprites[name] = found;
            Plugin.Log.LogInfo(found != null
                ? $"Theme: sprite '{name}' resolved ({found.rect.width}x{found.rect.height})"
                : $"Theme: sprite '{name}' NOT FOUND - not a loaded game sprite, and no "
                  + $"{name}.png in BepInEx/plugins/islandui-art");
            return found;
        }

        // Our own art, if there is any. Point filtering is deliberate: the game is
        // hand-painted at a low resolution and bilinear on a brush edge turns the
        // bristles into fog.
        private static Sprite LoadSpriteFile(string name)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.Combine(BepInEx.Paths.PluginPath, "islandui-art"), name + ".png");
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
                {
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Theme: could not load art '{name}': {e.Message}");
                return null;
            }
        }

        // A one-pixel-wide vertical ramp: opaque at the TOP, clear at the bottom.
        // Used to dissolve the cut edge of a scrolling window into the surface
        // behind it - a clipped row otherwise ends in a straight horizontal line,
        // which is the one shape this UI never draws (D36). Flip it with a 180
        // degree rotation for the other edge.
        // Hex to Color, one implementation. Every colour dial in the mod is an
        // html string, and each one that grew its own parser grew its own idea of
        // what an empty value means.
        public static Color ParseColor(string html, Color fallback)
        {
            return !string.IsNullOrEmpty(html)
                && ColorUtility.TryParseHtmlString(html.Trim(), out Color c) ? c : fallback;
        }

        public static readonly Skin BagSkin = new Skin();
        public static readonly Skin EquipSkin = new Skin();
        // per-equip-index type icon (hat / tool / cloth / ... ), indexed the way
        // RandomCharacter.ItemTypeToEquipID indexes CommonStates.equip
        public static readonly Sprite[] SlotTypeIcons = new Sprite[10];
        public const int LoadoutSlotCount = 9;
        // The loadout is an ItemSlot[10] whose index 8 the game never reads or
        // writes (recon §7.1) — these are the 9 that exist, in the order
        // RandomCharacter.ItemTypeToEquipID assigns them. Shared, because the
        // Inventory's loadout plate and the card's miniature of it must agree on
        // which slots a loadout HAS.
        public static readonly int[] LoadoutOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 9 };

        // Does this character have a loadout to show? Lives beside the order
        // it depends on, because the Inventory's plate and the browser's holder
        // list must agree - two gates that drifted apart is exactly how this bug
        // would come back.
        //
        // The old test was `equip != null && equip.Length > 9`, and it MEANT "this
        // thing can wear something" only by accident: vanilla allocated the array
        // inside SetBag, behind `bagSlot != 0`. Build 24759651 moved the allocation
        // into the early init that runs for every object not tagged "Rag", so the
        // test began saying yes to everything and loadout plates appeared on
        // entities that never had one. Nothing crashed and the boot census read
        // clean - it was found by reading the diff.
        //
        // Re-gating on `bagSlot != 0` alone would restore the old answer while
        // re-tying two things vanilla deliberately untied (it added TransferEquip
        // in the same update), so something may now legitimately carry gear with
        // no bag. So gate on what window-system.md D15 actually says: a column
        // shows a loadout WHEN IT HAS ONE. Wearing something counts; so does being
        // the kind of character that carries a bag, and that second half is what
        // keeps a naked NPC dressable - a purely content-based test would refuse
        // to draw the plate you need in order to put the first item in it.
        public static bool HasLoadout(CommonStates common)
        {
            if (common == null || common.equip == null
                || common.equip.Length <= LoadoutOrder[LoadoutOrder.Length - 1])
            {
                return false;
            }
            if (common.bagSlot != 0)
            {
                return true;
            }
            for (int i = 0; i < LoadoutOrder.Length; i++)
            {
                ItemSlot slot = common.equip[LoadoutOrder[i]];
                if (slot != null && !string.IsNullOrEmpty(slot.itemKey))
                {
                    return true;
                }
            }
            return false;
        }
        // Chests can badge themselves with their own item sprite; characters have
        // no portrait anywhere in the game, so they borrow the person silhouette
        // the bottom-right button row already uses for the status screen.
        public static Sprite CharacterBadge;
        public static Sprite ContainerBadge;
        // "What you are CARRYING", drawn in the game's own line art rather than
        // with ui_backpack. The game has two visual languages and the split is a
        // rule, not a coincidence: a PAINTED, shaded glyph is a BUTTON you press
        // (ui_backpack, ui_collection, ui_craft, ui_map, ui_setting - all of them
        // HUD buttons), and FLAT WHITE LINE ART is a LABEL that names something
        // (every ui_equip_*, every ui_tab_*, ui_chest_01). A gutter badge is a
        // label, so ui_backpack was wrong there - not for being coloured, but for
        // being a button glyph doing a label's job, which is why it fought the
        // line-art badges either side of it.
        public static Sprite CarriedBadge =>
            SlotTypeIcons != null && SlotTypeIcons.Length > 1 ? SlotTypeIcons[1] : null;
        // the game's own backpack glyph, taken from the bottom-right icon whose
        // callback is InventoryManager.BackPackButton — so "your bag" is drawn with
        // the button that has always meant your bag
        public static Sprite BagBadge;
        // ...and the button ITSELF, kept because the pickup flight aims at it
        // when the Shared Bag bar is not on screen. It is the only "your stuff is
        // over there" target that is always visible.
        public static GameObject BagButton;
        // how much of a slot the item art fills in the vanilla slot prefab.
        // Our cells were drawing at 73% and read visibly smaller than the game's.
        public static float SlotIconFill = 0.9f;
        // the vanilla slot's own box size and centre-to-centre spacing. Our cells
        // were 22/25 against the game's 20/x, so a bag drawn beside or above the
        // shared bar could never line up with it column for column. FIXED, not
        // measured: they were read off the game's slots at harvest, and anything
        // sized before that - the crafting window opened right after a hot reload
        // or a start - took the old 22 and stood wider until the next layout.
        // The game's slot is 20 on a 23 pitch in its canvas units at every
        // resolution.
        public const float SlotSize = 20f;
        public const float SlotPitch = 23f;

        public static bool Ensure(ManagersScript mn, InventoryManager inv)
        {
            if (Frame != null && Font != null)
            {
                // A save load reloads the scene: the ASSETS harvested here (sprites,
                // fonts) survive it, the SCENE OBJECTS do not. Fall through to
                // rediscover those. A null BagButton is NOT "already failed" —
                // the pack is often still coming up the first time allSet flips,
                // and treating null as success left the bag unharvested for the
                // whole first session (the chest HUD button then parented to the
                // left cluster instead of the pack).
                if (Live(BuildButtonTemplate) && Live(BagButton))
                {
                    return true;
                }
                if (!Live(BagButton))
                {
                    // BagBadge is the sprite off this button and it GATES the bag
                    // harvest below — being an asset it outlives the button, so
                    // leaving it set would skip the very rediscovery we came for
                    BagButton = null;
                    BagBadge = null;
                }
                if (!Live(BuildButtonTemplate))
                {
                    BuildButtonTemplate = null;
                }
                // only the HUD buttons are missing — hunt those, leave Frame/skins
                Canvas existing = mn != null && mn.uiMN != null && mn.uiMN.craftPanel != null
                    ? mn.uiMN.craftPanel.GetComponentInParent<Canvas>()
                    : null;
                if (existing != null)
                {
                    HarvestHudButtons(existing.rootCanvas);
                }
                return true;
            }
            if (mn == null || mn.uiMN == null || mn.uiMN.craftPanel == null || inv == null)
            {
                return false;
            }
            // EVERY prerequisite first, and nothing harvested until they all hold.
            // This used to check them as it went, which meant that at the MAIN MENU
            // — where the craft panel exists but its canvas does not — the run set
            // Frame but never Font, failed the early-out above, and harvested again
            // on the next call. Forever. Each harvest logs what it found, so idling
            // on the title screen printed a page of stale discoveries per frame.
            // All-or-nothing is what makes the early-out reachable.
            Image panelImage = mn.uiMN.craftPanel.GetComponent<Image>();
            Canvas canvas = mn.uiMN.craftPanel.GetComponentInParent<Canvas>();
            TextMeshProUGUI reference = canvas != null
                ? canvas.rootCanvas.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (panelImage == null || panelImage.sprite == null
                || canvas == null || reference == null || reference.font == null)
            {
                return false;   // scene not up yet; say so quietly and try again later
            }
            Frame = panelImage.sprite;
            FrameType = panelImage.type;
            FramePixelsPerUnit = panelImage.pixelsPerUnitMultiplier;
            Font = reference.font;
            WireGlobalFallback(Font);
            if (inv.itemSlot.Length > 100 && inv.itemSlot[100] != null)
            {
                Image slotImage = inv.itemSlot[100].GetComponent<Image>();
                SlotFrame = slotImage != null ? slotImage.sprite : null;
            }

            // find the game's bottom-left hammer (it OPENS the crafting menu):
            // the UI element whose persistent callback is LeftPanelState(1).
            // Buttons and EventTriggers both scanned; the int argument is only
            // reachable via reflection into the persistent call storage.
            HarvestHudButtons(canvas.rootCanvas);
            HarvestSlotIconFill(inv);
            HarvestBadges(canvas);
            HarvestContainerSkins(mn, inv, canvas);
            return true;
        }

        // "Destroyed along with its scene", which is NOT the same as "never found".
        // Unity overloads == so a destroyed object reports itself as null, and that
        // one convenience hides the difference that matters here: a real null means
        // the scan already failed and must not be repeated on every call, while a
        // destroyed reference means the scan succeeded in a session that has since
        // ended and MUST be repeated. Casting to object asks the C# reference
        // instead, which only the first case makes null.
        //
        // This has now caught two harvests, so it is a named test rather than
        // an inline idiom: any scene object cached here needs it.
        private static bool Destroyed(GameObject go)
        {
            return (object)go != null && go == null;
        }

        // Scene object we can actually use: exists, not destroyed, and on-screen.
        // An inactive first-harvest hit (a disabled duplicate in the canvas) is
        // as useless as a destroyed one and must be searched for again.
        internal static bool Live(GameObject go)
        {
            return go != null && go.activeInHierarchy;
        }

        // Bottom-left hammer (LeftPanelState(1)) and bottom-right bag
        // (BackPackButton). Quiet on miss: the pack is often not in the canvas
        // the first time allSet flips, and we retry from Ensure.
        private static void HarvestHudButtons(Canvas root)
        {
            if (root == null)
            {
                return;
            }
            if (BuildButtonTemplate == null)
            {
                BuildButtonTemplate = FindByPersistentCall(root, "LeftPanelState", 1);
                if (BuildButtonTemplate != null)
                {
                    Plugin.Log.LogInfo($"Theme: build button template found ('{BuildButtonTemplate.name}')");
                }
            }
            if (BagBadge == null)
            {
                GameObject bagIcon = FindByPersistentCall(root, "BackPackButton");
                Image image = bagIcon != null
                    ? (bagIcon.GetComponent<Image>() ?? bagIcon.GetComponentInChildren<Image>(true))
                    : null;
                if (image != null)
                {
                    BagBadge = image.sprite;
                    BagButton = bagIcon;
                    Plugin.Log.LogInfo($"Theme: bag badge '{BagBadge.name}' from '{bagIcon.name}'");
                }
            }
        }

        // Measure the vanilla slot prefab: how wide is its "itemImage" child
        // relative to the slot itself? Matching that ratio is what makes our item
        // art the same size as the game's rather than a guess that reads small.
        private static void HarvestSlotIconFill(InventoryManager inv)
        {
            if (inv.itemSlot == null)
            {
                return;
            }
            int[] probes = { 0, 100, 50 };
            for (int i = 0; i < probes.Length; i++)
            {
                int id = probes[i];
                if (id >= inv.itemSlot.Length || inv.itemSlot[id] == null)
                {
                    continue;
                }
                var slotRect = inv.itemSlot[id].transform as RectTransform;
                var iconRect = inv.itemSlot[id].transform.Find("itemImage") as RectTransform;
                if (slotRect == null || iconRect == null)
                {
                    continue;
                }
                float slotW = slotRect.rect.width;
                float iconW = iconRect.rect.width;
                if (slotW > 1f && iconW > 1f)
                {
                    SlotIconFill = Mathf.Clamp(iconW / slotW, 0.5f, 1f);
                    if (!Mathf.Approximately(slotW, SlotSize))
                    {
                        Plugin.Log.LogWarning($"Theme: the game's slot is {slotW:0.#}, not the fixed {SlotSize:0.#}");
                    }
                    Plugin.Log.LogInfo($"Theme: slot icon fill {SlotIconFill:0.000} "
                        + $"(itemImage {iconW:0.#} / slot {slotW:0.#}, from slot {id})");
                    return;
                }
            }
            Plugin.Log.LogInfo($"Theme: slot icon fill not measurable — keeping {SlotIconFill:0.000}");
        }

        // The bottom bar's buttons are the only place the game draws a generic
        // "a character" glyph. Match on sprite name, and log every button sprite
        // on a miss so the right name is one restart away rather than a guess.
        private static void HarvestBadges(Canvas canvas)
        {
            if (CharacterBadge != null || canvas == null)
            {
                return;
            }
            string[] wanted = { "status", "chara", "human", "person", "npc", "friend" };
            var seen = new System.Collections.Generic.List<string>();
            foreach (Image image in canvas.rootCanvas.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                {
                    continue;
                }
                string n = image.sprite.name.ToLowerInvariant();
                if (!n.StartsWith("ui_"))
                {
                    continue;
                }
                if (!seen.Contains(image.sprite.name))
                {
                    seen.Add(image.sprite.name);
                }
                for (int w = 0; w < wanted.Length && CharacterBadge == null; w++)
                {
                    if (n.Contains(wanted[w]))
                    {
                        CharacterBadge = image.sprite;
                        Plugin.Log.LogInfo($"Theme: character badge '{image.sprite.name}' from '{image.name}'");
                    }
                }
                // "chest" FIRST and on its own pass, because the fallbacks match
                // painted button glyphs too and whichever came first down the
                // hierarchy won. ui_chest_01 is the line-art one, and the family
                // matters more than the word.
                if ((ContainerBadge == null || !ContainerBadge.name.ToLowerInvariant().Contains("chest"))
                    && (n.Contains("chest") || (ContainerBadge == null
                        && (n.Contains("box") || n.Contains("bag") || n.Contains("item")))))
                {
                    ContainerBadge = image.sprite;
                    // logged like the other two: a badge picked by name match is a
                    // guess, and an unlogged guess cannot be argued with
                    Plugin.Log.LogInfo($"Theme: container badge '{image.sprite.name}' from '{image.name}'");
                }
            }
            if (CharacterBadge == null)
            {
                Plugin.Log.LogInfo("Theme: no character badge matched. ui_ sprites seen: "
                    + string.Join(", ", seen.ToArray()));
            }
        }

        // Pull the container plate materials straight out of the live scene, the
        // same way Frame/SlotFrame are pulled off the craft panel. Nothing is
        // authored: these are the game's own sprites, so our plates read as
        // native rather than as a mod's idea of native.
        private static void HarvestContainerSkins(ManagersScript mn, InventoryManager inv, Canvas canvas)
        {
            if (BagSkin.Valid && EquipSkin.Valid)
            {
                return;
            }
            // the bottom bar's parent carries the cream parchment strip; the
            // character bag panel is the fallback (same material)
            Transform root = canvas != null ? canvas.rootCanvas.transform : null;
            Capture(FindImage(root, "InventoryPanel") ?? FindImage(root, "InventorySubPanel"), BagSkin, "bag");
            Capture(inv.equipPanel != null ? inv.equipPanel.GetComponent<Image>() : null, EquipSkin, "equip");

            HarvestSlotTypeIcons(inv);
        }

        private static Image FindImage(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    Image image = t.GetComponent<Image>();
                    if (image != null && image.sprite != null)
                    {
                        return image;
                    }
                }
            }
            return null;
        }

        private static void Capture(Image source, Skin skin, string label)
        {
            if (source == null || source.sprite == null)
            {
                Plugin.Log.LogWarning($"Theme: {label} skin not found — falling back to the flat plate");
                return;
            }
            skin.Sprite = source.sprite;
            skin.Type = source.type;
            skin.PixelsPerUnit = source.pixelsPerUnitMultiplier;
            skin.Color = source.color;
            // The RGB matters as much as the alpha and was never printed. Both
            // skins turn out to be the SAME sprite (ui_frame): vanilla's loadout is
            // not a green picture, it is this frame wearing a colour - so this line
            // is where the loadout's green actually lives.
            Plugin.Log.LogInfo($"Theme: {label} skin '{source.sprite.name}' from '{source.name}' "
                + $"type={source.type} ppu={source.pixelsPerUnitMultiplier} "
                + $"color=#{ColorUtility.ToHtmlStringRGBA(source.color)}");
        }

        // The vanilla equip strip draws a small type icon ABOVE each slot. Those
        // are scene objects, so they cannot be named from the decompiled source —
        // match them positionally: for each equip slot, the nearest Image that is
        // horizontally aligned with it and sits above it. Every match is logged so
        // a wrong pairing is visible in the log rather than only on screen.
        // The nine glyphs vanilla draws over the loadout slots (hat, gloves, ring…),
        // matched by SCREEN PROXIMITY — the nearest Image sitting above each equip
        // slot — because nothing names them and nothing indexes them.
        //
        // Proximity looks fragile enough to blame, and it was blamed once: when a
        // harvest box's icon turned up over loadout slot 0, this was the suspect
        // and a guard against harvesting with a container open was written. The
        // log then said `equip[0] type icon 'ui_equip_hat' from 'head' dx=0.0`,
        // 9/9, every slot exact — so the match is not the problem and the guard was
        // reverted rather than left in as insurance against a theory. Something is
        // drawn OVER slot 0; nothing is wrong with what is drawn IN it.
        private static void HarvestSlotTypeIcons(InventoryManager inv)
        {
            if (inv.equipPanel == null || inv.itemSlot == null || inv.itemSlot.Length < 50)
            {
                return;
            }
            var candidates = new System.Collections.Generic.List<Image>();
            foreach (Image image in inv.equipPanel.GetComponentsInChildren<Image>(true))
            {
                // skip the panel itself, the slots, and each slot's own children
                if (image.sprite == null || image.GetComponent<ItemSlot>() != null
                    || image.GetComponentInParent<ItemSlot>() != null
                    || image.gameObject == inv.equipPanel)
                {
                    continue;
                }
                candidates.Add(image);
            }
            int matched = 0;
            for (int i = 40; i < 50; i++)
            {
                ItemSlot slot = inv.itemSlot[i];
                // index 8 is the gap the game never uses (recon §7.1) — matching
                // it would just duplicate its neighbour's icon
                if (slot == null || i - 40 == 8)
                {
                    continue;
                }
                var slotRect = (RectTransform)slot.transform;
                Vector3 slotPos = slotRect.position;
                Image best = null;
                float bestDx = float.MaxValue;
                for (int c = 0; c < candidates.Count; c++)
                {
                    Vector3 p = candidates[c].transform.position;
                    if (p.y <= slotPos.y)
                    {
                        continue; // the icons sit above their slot
                    }
                    float dx = Mathf.Abs(p.x - slotPos.x);
                    if (dx < bestDx)
                    {
                        bestDx = dx;
                        best = candidates[c];
                    }
                }
                if (best != null && bestDx < slotRect.rect.width)
                {
                    SlotTypeIcons[i - 40] = best.sprite;
                    matched++;
                    Plugin.Log.LogInfo($"Theme: equip[{i - 40}] type icon '{best.sprite.name}' "
                        + $"from '{best.name}' dx={bestDx:0.0}");
                }
            }
            Plugin.Log.LogInfo($"Theme: {matched}/{LoadoutSlotCount} loadout type icons harvested "
                + $"({candidates.Count} candidates)");
        }

        // Same lookup for the no-argument callbacks wired straight to a scene icon
        // (BackPackButton, EquipVisible): there is no int to match on, so the
        // method name alone identifies them.
        public static GameObject FindByPersistentCall(Canvas root, string method)
        {
            return FindByPersistentCall(root, method, null);
        }

        public static GameObject FindByPersistentCall(Canvas root, string method, int intArg)
        {
            return FindByPersistentCall(root, method, (int?)intArg);
        }

        // The same search under ONE subtree, for a call that several panels
        // wire up: the status sheet's order buttons, the NPC menu's and the
        // follower panel's all call OrderChange(n), and the canvas-wide search
        // prefers whichever happens to be live.
        public static GameObject FindByPersistentCall(Transform scope, string method, int intArg)
        {
            if (scope == null)
            {
                return null;
            }
            foreach (Button button in scope.GetComponentsInChildren<Button>(true))
            {
                if (MatchesCall(button.onClick, method, intArg))
                {
                    return button.gameObject;
                }
            }
            foreach (var trigger in scope.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
            {
                foreach (var entry in trigger.triggers)
                {
                    if (MatchesCall(entry.callback, method, intArg))
                    {
                        return trigger.gameObject;
                    }
                }
            }
            return null;
        }

        private static GameObject FindByPersistentCall(Canvas root, string method, int? intArg)
        {
            // several elements share the callback (text tabs like クラフト and
            // the icon hammer) — prefer the IMAGE-ONLY one, log all candidates
            var candidates = new System.Collections.Generic.List<GameObject>();
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (MatchesCall(button.onClick, method, intArg))
                {
                    candidates.Add(button.gameObject);
                }
            }
            foreach (var trigger in root.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
            {
                foreach (var entry in trigger.triggers)
                {
                    if (MatchesCall(entry.callback, method, intArg))
                    {
                        candidates.Add(trigger.gameObject);
                        break;
                    }
                }
            }
            GameObject best = null;
            int bestScore = -1;
            foreach (GameObject candidate in candidates)
            {
                bool hasText = candidate.GetComponentInChildren<TMP_Text>(true) != null
                    || candidate.GetComponentInChildren<UnityEngine.UI.Text>(true) != null;
                bool live = candidate.activeInHierarchy;
                // live image-only > live with text > inactive image-only > anything
                int score = (live ? 2 : 0) + (hasText ? 0 : 1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private static bool MatchesCall(UnityEngine.Events.UnityEventBase evt, string method, int? intArg)
        {
            for (int e = 0; e < evt.GetPersistentEventCount(); e++)
            {
                if (evt.GetPersistentMethodName(e) != method)
                {
                    continue;
                }
                if (intArg == null)
                {
                    return true;   // no argument to disambiguate on
                }
                try
                {
                    var calls = HarmonyLib.Traverse.Create(evt)
                        .Field("m_PersistentCalls").Field("m_Calls")
                        .GetValue() as System.Collections.IList;
                    if (calls == null || e >= calls.Count)
                    {
                        continue;
                    }
                    int arg = HarmonyLib.Traverse.Create(calls[e])
                        .Field("m_Arguments").Field("m_IntArgument").GetValue<int>();
                    if (arg == intArg.Value)
                    {
                        return true;
                    }
                }
                catch (System.Exception)
                {
                    // storage layout differs on some Unity versions — just skip
                }
            }
            return false;
        }

        public static TextMeshProUGUI MakeText(Transform parent, string name, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var go = Owned.New(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            if (Font != null)
            {
                text.font = Font;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        // The harvested font's material carries a hard BLACK OUTLINE. On the dark
        // plates it is invisible — black on near-black — and it is what made the
        // game's own text readable over its art. On parchment it haloes every
        // glyph and turns Ink into something muddy and heavy, which is what "text
        // colour is off" looked like: the colour was right, there was a second
        // colour drawn around it.
        //
        // The material is INSTANCED by touching fontMaterial, so zeroing it here
        // cannot reach vanilla's own text through the shared material.
        //
        // Was ForLightSurface until the wood surface arrived and the
        // obvious next move — leave the outline ON for dark, since that is what it
        // was for in vanilla — turned out to be wrong twice over. It buys nothing
        // (cream on #603E28 is already a wide margin) and it costs reversibility:
        // stripping is destructive, so a live parchment→wood flip could never put
        // back what a wood→parchment flip had removed. Our type is flat on BOTH
        // materials, which is a rule, so the name stopped naming a surface.
        // A TOOL'S WEAR IN ONE COLOUR, the tool belt's since it was built: green,
        // amber from half, red from a quarter. The action bar prints the same
        // durability and now says it the same way.
        public static Color WearInk(float fraction)
        {
            return fraction <= 0.25f ? new Color32(230, 105, 75, 255)
                : fraction <= 0.5f ? new Color32(232, 182, 84, 255)
                : new Color32(153, 190, 116, 255);
        }

        // A CELL'S COUNT, the inventory's and the action bar's: the wear inks,
        // the cream and the creature's magenta lifted until they clear the leather's
        // filled bed, on a darker edge (OutlineCount). The tool belt's number wears
        // them too; its wear bar keeps WearInk.
        public static readonly Color CountCream = new Color32(255, 251, 240, 255);
        public static readonly Color CountHappiness = new Color32(255, 128, 238, 255);

        public static Color CountInk(float fraction)
        {
            return fraction <= 0.25f ? new Color32(255, 122, 92, 255)
                : fraction <= 0.5f ? new Color32(255, 204, 84, 255)
                : new Color32(168, 236, 112, 255);
        }

        public static bool OutlineCount(TMP_Text text)
        {
            return OutlineNumber(text, 0.38f, 0.28f, new Color32(20, 12, 7, 255));
        }

        // Dark ink round a small number that sits over the world rather than on a
        // plate (PointsTag's recipe). The material is instanced by fontMaterial, so
        // no other text is reached. False while the face is not built yet: ask again.
        public static bool OutlineNumber(TMP_Text text)
        {
            return OutlineNumber(text, 0.22f, 0.34f, new Color32(32, 21, 14, 255));
        }

        private static bool OutlineNumber(TMP_Text text, float dilate, float width, Color32 edge)
        {
            if (text == null || text.font == null || text.fontSharedMaterial == null)
            {
                return false;
            }
            Material m = text.fontMaterial;
            if (m == null)
            {
                return false;
            }
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, edge);
            text.UpdateMeshPadding();
            return true;
        }

        // EVERY LANGUAGE WE SHIP HAS ITS GLYPHS (Forgejo #19). Our faces are static
        // Latin-1 atlases that fall back to the game's M+ 1p (dynamic: Latin Extended,
        // Cyrillic, kana, kanji), which falls back to the game's Noto Sans CJK SC (dynamic,
        // 20,928 characters) - FontCoverage.Report. What did not reach that chain was a
        // text set in TMP's default LiberationSans, which has no fallbacks (a text built
        // before the game's face was harvested). TMP's global fallback list is read only
        // for a glyph a face and its own fallbacks lack, so this changes nothing that
        // renders today; it turns a box into a letter.
        private static void WireGlobalFallback(TMP_FontAsset font)
        {
            System.Collections.Generic.List<TMP_FontAsset> global = TMP_Settings.instance != null ? TMP_Settings.fallbackFontAssets : null;
            if (font != null && global != null && !global.Contains(font))
            {
                global.Add(font);
            }
        }

        // A LABEL MEASURED TO ITS ENGLISH (Forgejo #19): a longer language shrinks to the
        // box, down to 60%, instead of running into its neighbour; English stays as drawn.
        // `size` is the drawn size, since auto-size leaves its last one behind.
        public static void FitLanguage(TMP_Text text, float size)
        {
            bool fit = Shared.Loc.Language != "en";
            if (text.enableAutoSizing == fit && (fit ? text.fontSizeMax == size : text.fontSize == size))
            {
                return;
            }
            text.enableAutoSizing = fit;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.6f;
            text.fontSize = size;
        }

        public static void FlattenType(TMP_Text text)
        {
            // A text made before any font is harvested has no shared material, and
            // reading fontMaterial then constructs a Material from null - the
            // ArgumentNullException the harness reported on every boot, thrown out
            // of Prewarm. Worse than the log line: it aborted whatever was being
            // built mid-way (the plate tabs) and left a sprite-less
            // Image behind as a white quad. Nothing to flatten yet is not an error.
            if (text == null || text.font == null || text.fontSharedMaterial == null)
            {
                return;
            }
            Material m = text.fontMaterial;
            if (m == null)
            {
                return;
            }
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0f));
            // and the drop shadow, if this material carries one instead
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0f));
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        }

        // A section heading inside a card. Overflow rather than Ellipsis: a
        // heading that does not fit must LOOK wrong, not disappear (see CaptionH).
        public static TextMeshProUGUI MakeHeading(Transform parent, string name, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            TextMeshProUGUI text = MakeText(parent, name, FontCaption, color, align);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        // Procedurally generated rounded-rect sprites — the game ships no clean
        // rounded bar sprite, and its decorative frames fall apart when sliced to
        // bar proportions. WHITE pixels so Image.color tints them; the edge ring
        // is a separate sprite so a fill tint (e.g. focus brighten) leaves the
        // border alone. 9-sliced; PPU 100 = canvas reference, so corner pixels
        // map 1:1 to UI units.
        private static Sprite _roundedFill;
        private static Sprite _roundedEdge;
        public static Sprite RoundedFill
        {
            get
            {
                if (_roundedFill == null) _roundedFill = BuildRounded(edgeOnly: false);
                return _roundedFill;
            }
        }
        public static Sprite RoundedEdge
        {
            get
            {
                if (_roundedEdge == null) _roundedEdge = BuildRounded(edgeOnly: true);
                return _roundedEdge;
            }
        }

        private static Sprite BuildRounded(bool edgeOnly)
        {
            const int size = 16;
            const float radius = 5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // signed distance to the rounded-rect rim (<0 inside)
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist); // ~1px antialiased rim
                    if (edgeOnly)
                    {
                        alpha *= Mathf.Clamp01(dist + 1.8f); // keep only a thin ring
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            var slice = new Vector4(radius + 1f, radius + 1f, radius + 1f, radius + 1f);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, slice);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // The favourite mark: a five-pointed star, generated for the same
        // reason the disclosure triangle is — the game's font renders these as tofu
        // boxes. White and tintable; drawn small in a cell's free corner.
        private static Sprite _star;
        public static Sprite Star
        {
            get
            {
                if (_star == null) _star = BuildStar();
                return _star;
            }
        }

        private static Sprite BuildStar()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            var center = new Vector2(size / 2f, size / 2f - 0.5f);
            const float outer = 7.5f;
            const float inner = 3.1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - center;
                    // angle measured from straight up, so a point lands at the top
                    float angle = Mathf.Atan2(d.x, d.y);
                    // fold into one tenth-turn wedge: the star is that wedge x10
                    float wedge = Mathf.PI / 5f;
                    float t = Mathf.Abs(Mathf.Repeat(angle + wedge, 2f * wedge) - wedge) / wedge;
                    float radius = Mathf.Lerp(outer, inner, t);
                    float alpha = Mathf.Clamp01(radius - d.magnitude + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // small down-pointing disclosure triangle (white, tintable); rotate the
        // Image's rect +90° to point right for the collapsed state
        private static Sprite _triangle;
        public static Sprite Triangle
        {
            get
            {
                if (_triangle == null) _triangle = BuildTriangle();
                return _triangle;
            }
        }

        private static Sprite BuildTriangle()
        {
            const int size = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                // texture y=0 is the BOTTOM: apex bottom-center, base at top
                float t = (y + 0.5f) / size;
                float halfWidth = t * (size / 2f - 1f);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(halfWidth - dx + 0.5f)));
                }
            }
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // an arrow, a head on a stem (white, tintable): up, or down. PERSONALITY's
        // tags on the type they like (up) and dislike (down)
        private static Sprite _arrowUp, _arrowDown;
        public static Sprite ArrowUp
        {
            get
            {
                if (_arrowUp == null) _arrowUp = BuildArrow(true);
                return _arrowUp;
            }
        }

        public static Sprite ArrowDown
        {
            get
            {
                if (_arrowDown == null) _arrowDown = BuildArrow(false);
                return _arrowDown;
            }
        }

        private static Sprite BuildArrow(bool up)
        {
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                // texture y=0 is the BOTTOM; f runs 0 at the tip to 1 at the tail
                float py = y + 0.5f;
                float f = up ? (size - py) / size : py / size;
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size / 2f);
                    float a;
                    if (f < 0.5f)
                    {
                        // the head: a triangle from the tip, full width at its base
                        float halfWidth = f / 0.5f * (size * 0.5f - 1f);
                        a = Mathf.Clamp01(halfWidth - dx + 0.5f) * Mathf.Clamp01(f * size - 1f);
                    }
                    else
                    {
                        // the stem: a third of the width, down to the tail
                        a = Mathf.Clamp01(size / 6f - dx + 0.5f) * Mathf.Clamp01((1f - f) * size - 1f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // Place an item icon inside a slot box of the given size, at the same
        // art-to-slot ratio the game's own slot prefab uses. Every card drew its
        // icon at a hardcoded ~70% before this, which read visibly smaller than
        // the same item in a vanilla panel.
        public static RectTransform PlaceSlotIcon(Component icon, float boxSize)
        {
            float size = boxSize * SlotIconFill;
            float inset = (boxSize - size) * 0.5f;
            return Place(icon, inset, inset, size, size);
        }

        // top-left anchored placement helper — all our layout math is top-left based
        public static RectTransform Place(Component c, float x, float y, float w, float h)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
    }
}
