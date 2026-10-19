using System.Collections.Generic;
using IslandUI.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    // The Inventory — the ACTING window.
    // See docs/inventory-window-ux.md: "the Catalogue Browser knows, the item
    // card points, the Inventory grabs."
    //
    // DESIGN (revised after the first in-game pass): the CONTAINER is
    // the unit, not the entity. There is no outer window — each container is its
    // own mini-window (plate), and the plates stack UPWARD from the bottom-right
    // corner:
    //
    //        ┌ Nyama · Loadout ┐   target stack, grows further up
    //        ├ Nyama · Bag ────┤   its bag sits closest to yours: short drag
    //        └─────────────────┘
    //        ┌ Bag ────────────┐   your stack, fixed
    //        ├ Loadout ────────┤   green — the game's own equip-strip colour
    //        └ Shared Bag ─────┘   bottom = nearest the hand
    //
    // Bottom-right, growing up, is what keeps both stacks clear of the vanilla
    // NPC dialog options in the middle of the screen while keeping your bags and
    // the target's adjacent, so transfers are a short move (WI-3).
    //
    // Only two entities exist here: the PLAYED character (always pMove.common,
    // never gameMN.menuNPC — vanilla's target-switching display is unwanted) and
    // whatever is currently targeted. Nearby/vicinity entities are deliberately
    // not shown; they have value but they are a separate problem, and they made
    // the window unreadable. [Inventory] ShowNearby brings them back.
    //
    // Still READ-ONLY: renders live slot arrays, never writes. Movement is WI-3.
    internal static partial class InventoryWindow
    {
        // taken from the vanilla slot strip so our grids share the game's cell
        // geometry — same box, same spacing, so columns line up with the bar
        private static float CellPitch = 25f;
        private static float CellBox = 22f;
        private const float PlateHeaderH = 13f;
        private const float StarSize = 7f;   // the favourite mark
        private const float TitleStarSize = 9f;   // the same, on a favourite NPC's name plate
        private const float TitleStarGap = 3f;
        // how far the occupancy shade sits inside the cell, so the frame's own rim
        // still draws around it and a filled slot reads as a slot, not a dark square
        private const float FillInset = 2f;

        // THE WINDOWS' FIXED MEASURES, in canvas units.
        internal const int MaxPlateColumns = 10;      // the widest a container is drawn before it wraps
        internal const float GutterWidth = 12f;       // the icon strip down a container's left edge
        internal const float EmptySlotAlpha = 0.5f;   // an empty slot's frame lets the parchment through
        private const float SlotGap = 1f;             // between slots
        private const float GroupMemberGap = 4f;      // between containers sharing a frame
        private const float PlateGap = 0f;            // between frames in a column
        private const float ColumnGap = 6f;           // between your column and the target's
        private const float MarginX = 10f;            // your column from the screen's right edge
        private const float MarginY = 36f;            // the columns from the screen's bottom edge
        private const float FrameInsetX = 10f;        // the slots inside an ungrouped frame's border
        private const float FrameInsetY = 8f;
        private const float NearbyMaxHeight = 180f;   // the vicinity window's height before it scrolls
        private const float SeparatorHeight = 1f;     // the rule between containers sharing a frame
        // your own containers, bottom-up
        private static readonly string[] OwnStackOrder = { "SharedBag", "Bag", "Loadout" };
        private static readonly Color SeparatorColor = Theme.ParseColor("#3E2C1E4D", Color.black);

        // Theme.SectionRowHeight is sized for a section heading in a document;
        // a panel's own heading band sits directly on the slots and does not need
        // that much air around it.
        private const float TitleH = 13f;
        private const float TypeIconH = 10f;
        // How wide a single container may render before it wraps. A 12-slot bag
        // drawn 12 across forces the whole PANEL 12 wide, and every shorter band
        // in it then trails dead parchment — on a rail of columns, narrower and
        // taller beats wider and ragged.
        private const int OwnMaxCols = MaxPlateColumns;
        private const int AbsoluteMaxCols = 40;
        private const int PlatePool = 8;   // seed only — the pool grows on demand
        // Right to left: you, then the character you are dealing with, then every
        // container in reach. Three KINDS of container were being drawn into two
        // columns, which is why the pool ended up borrowing your column's
        // identity — a chest that was "nearby" and a chest that was "open" were
        // the same chest in two different places (D28/D30).
        private const int OwnColumn = 0;
        private const int CharacterColumn = 1;
        // The pool shares the character's column rather than taking a third of the
        // screen beside it. They are the same kind of thing —
        // containers that are not yours — and the focused one belongs nearest your
        // own bags, so it takes the BOTTOM of the column and the pool stacks above
        // it. Two panels, one column: each keeps its own frame and heading, so the
        // merge costs no legibility and buys back a whole column of width.
        // THE LEFT SLOT (D48): the vicinity window leaves the
        // target's column and takes the crafting window's place - the same rect,
        // one of the two open at a time. Left is what you LOOK AT (recipes or the
        // containers around you), middle is what you are DEALING WITH, right is
        // YOU. Its column is pinned to that rect by Layout and sized to it by
        // BindReach, so the window is static like the crafting window is.
        private const int ReachColumn = 2;
        private const int ColumnCount = 3;

        private static bool _slotKnown;
        private static float _slotLeft, _slotBottom, _slotW, _slotH;

        // The crafting panel's rect, in this window's root space, at the lifted
        // position the crafting window shows it at. Read fresh each layout: the
        // panel is vanilla's and its geometry is its own business.
        // The cell geometry, for the slot's arithmetic (UI.Slot). SIZED ON FIRST
        // ASK: the fields' 25/22 defaults were only ever replaced when this
        // window built, so a crafting window opened before any inventory build
        // derived its width from cells two units too wide and came out ~18 units
        // wide of the mark the first time, and right after a switch to vicinity
        // and back.
        private static bool _cellsSized;
        internal static float CellPitchNow { get { EnsureCellScale(); return CellPitch; } }
        internal static float CellBoxNow { get { EnsureCellScale(); return CellBox; } }

        private static void EnsureCellScale()
        {
            if (!_cellsSized)
            {
                ApplySlotScale();
            }
        }

        // ONE SOURCE (UI.Slot): position and height from vanilla's panel, width
        // from the grid - the crafting shell reads the same numbers.
        private static bool ReadSlot()
        {
            _slotKnown = false;
            if (_root == null || !Slot.Read(_mn, (RectTransform)_root.transform))
            {
                return false;
            }
            _slotLeft = Slot.Left;
            _slotBottom = Slot.Bottom;
            _slotW = Slot.Width;
            _slotH = Slot.Height;
            _slotKnown = true;
            return true;
        }

        private static int SlotCols()
        {
            return Slot.ReachCols;
        }

        // The viewport's height: the slot less the frame's padding and its
        // heading row, so the frame comes out the slot's height whatever the pool
        // holds.
        private static float SlotViewHeight()
        {
            return Mathf.Max(CellPitch, _slotH - 2f * GroupPadY()
                - TitleH - GroupMemberGap - 4f);
        }

        // The name lives on a PLATE over the frame's top rail, like the settings
        // window's, when the kit frame is on. The brush heading stays with the
        // characters, where its colour helps indicate the type of character.
        private static bool PlateNamed()
        {
            return Theme.Kit("kit_field", 12) != null;
        }

        // One group per column: your containers, the focused character's, the pool.
        private const int GroupNone = 0;
        private const int GroupOwn = 1;
        private const int GroupCharacter = 2;
        private const int GroupReach = 3;
        private const int GroupBag = 4;      // a person's bag, a window of its own on the bar
        private const int GroupOwnSheet = 5;
        private const int GroupCount = 6;

        // §6.20 (D49): what a dossier column can SHOW. Inventory is the column as
        // it always was; the rest are the vanilla status sheet, redistributed by
        // kind. The tab set is the carrier's (D51): only the aspects built so far
        // are offered, and each slice adds its own.
        // BY KIND, NOT BY VANILLA'S TAB: the original sheets were grouped in an
        // inconvenient way - libido and stamina are something different from
        // details about the person. Condition is the gauges,
        // Growth what you spend and what it buys, Profile who they are, Relations
        // who they are bound to and the ledger, Orders the standing settings.
        // Overview was Condition and Fight was Growth once; the names now
        // say what the pages became. Overview is the INTERACTION sheet - the
        // condition AND the acts (target-matrix.md G7) - and Fight is where the
        // level, the exp band and the attributes live.
        private enum Aspect { Inventory = 0, Overview, Fight, Skills, Profile, Relations, Orders }
        // yours: two gauges do not earn a tab, so your libido and moral sit on
        // your Profile; theirs: Condition first, since "are they all right" is
        // the first thing asked of a follower
        // Relations left the tab set - it is a block on Profile now, brief,
        // with the full matrix one press further out. The enum member and
        // WriteRelations stay: restoring the tab is putting the name back in these
        // two arrays.
        // Yours is already in tempo order and stays as it is: your bag is the
        // thing you touch, then your points, then the skill book, then your facts.
        // You have no Overview (two live gauges do not earn a tab, §6.21 - they
        // sit on your Profile) and you do not order yourself.
        // CHARACTER · SOCIAL · SKILLS for you, CHARACTER · SOCIAL · ORDERS for them:
        // leveling belongs with the character, configuring with the orders, and
        // history fits better in a social categorization. Fight
        // is gone: its growth block is the Character page's foot, its combat
        // numbers stay with the character since equipment does not change them.
        // Profile IS the Character page and Relations IS the Social page; the enum
        // names are the old ones until the cleanup.
        private static readonly Aspect[] OwnAspects =
            { Aspect.Profile, Aspect.Relations, Aspect.Skills };
        // §6.24 D75: Condition is a tab again, as OVERVIEW. It was retired on the
        // premise that the header made it redundant, and the card (first
        // attempt) disproved that premise - the header cannot hold it. What it
        // carries now is more than the gauges: the condition WITH numbers, then
        // the job, where they are, whether they follow and why they are unhappy.
        //
        // These arrays are the ORDER, not the set. AspectsFor filters them by what
        // a target actually has (D76), so a corpse and a shopkeeper draw fewer
        // without either being named here.
        // A THING HAS NO TABS AT ALL. Tabs are for PEOPLE.
        //
        // A person has aspects that compete for one surface - their bag, their
        // orders, their relations, how they are - and you look at one at a time,
        // which is what a tab strip is for. A thing's aspects are not like that:
        // its contents, its wear and what you can do to it are all about the same
        // object at the same moment, and there is no reason to make you choose
        // between them. So they MERGE onto one page: a prop displays like a
        // container, with a single interface for everything.
        //
        // This is what the pig pen was about to force anyway. It has a bag AND
        // acts, so the presence rule owed it two tabs - and two tabs on a fence is
        // absurd in a way that says the rule was being applied past its subject.
        // The rule is not wrong; its scope was. It derives a PERSON's pages.
        private static readonly Aspect[] PropAspects = new Aspect[0];
        // THE ORDER IS THE TEMPO AXIS (§6.21), left to right - how they are
        // RIGHT NOW, then what they carry, then what you have told them to do,
        // then what they have become, then who they are. The strip reads from the
        // changeable to the permanent, which is the same axis that decided which
        // field goes on which sheet, applied to the sheets themselves.
        //
        // An earlier plan asked for "Inventory · Orders · Relations · Fight · Profile",
        // and that order is stale in both directions: Relations left the set and
        // Overview joined it (D75), so it needs restating rather than restoring.
        // What it was reaching for - the things you DO before the things you READ -
        // the tempo order gives, and gives a reason for the whole run rather than
        // for its two ends.
        //
        // Overview leads because it is what a world click opens (D93) and the one
        // page every person has. The strip's first tab is no longer the default -
        // the door decides that - but it is still where the eye starts.
        // OVERVIEW HAS LEFT THE SET (§6.27 D103/D105): its gauge and its acts
        // live on the unit, and a page that only repeated the unit was what put
        // three health bars on one screen. The enum member and
        // its writer stay, unreferenced, until the whole page is torn out.
        // ...and INVENTORY has left it too: a grid is a transaction
        // you work with your hands, the pages are read or set, and the two do not
        // share a tab set. A person's bag is a window of its own at the bottom, the
        // same window a chest gets (GroupBag), opened by a door on the unit.
        private static readonly Aspect[] TheirAspects =
            { Aspect.Profile, Aspect.Relations, Aspect.Orders };

        // THE SHEET IS OPENED BY A DOOR AND STAYS OPEN (§6.27 D107). A world click
        // gives you the unit alone; the roster, the Status verb, a click on the
        // unit's head and the sheet key open the sheet, and it stays open across
        // targets - the management pass - until you close it; the target ending
        // only hides it, and the next person opens with it (D107 amended).
        // A container is its own door: a chest's grid IS what you clicked for
        // (D104), so it shows whatever this flag says.
        private static bool _sheetOpen;
        // THE BAG WINDOW: a person's bag and loadout as a grid window
        // on the bar, the same window a chest gets, opened by the Bag door on the
        // unit and closed by its chip; remembered across targets like the sheet
        private static bool _bagOpen;

        private static bool SheetOpenFor(object focus)
        {
            // the bag door armed beyond reach opens nothing until you walk up
            if (focus is CommonStates who)
            {
                return _sheetOpen || (_bagOpen && PocketsOpen(who) && InReach(who));
            }
            // A THING IS ITS WINDOW: a chest, a well and a
            // signboard all open the same bottom-anchored frame on the click - the
            // name on the rail, wear and Move · Remove · Repair on the rim, the grid
            // or the reading and the acts in the body - and the window is the
            // indicator. The unit serves the living only.
            // ...until D156 (target indicators for everything that is targetable,
            // with a similar panel that can toggle the inventory): with the unit up, a thing has the indicator and the
            // dock a person has, and only a CONTAINER has a window - its grid,
            // opened by the dock's Inventory door, the one standing choice a
            // person's bag door is. A thing with no grid has no window at all.
            if (Settings.TheUnit.Value)
            {
                return focus is InventorySlot slot && _bagOpen
                    && ReachRule.LiveProp(_mn, slot.GetComponent<ItemInfo>());
            }
            return focus is InventorySlot || focus is ItemInfo;
        }

        internal static void ToggleSheet()
        {
            _sheetOpen = !_sheetOpen;
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        internal static void ToggleBag()
        {
            _bagOpen = !_bagOpen;
            _forceRefresh = true;
            _nextRefresh = 0f;
        }

        // the 9 loadout slots that exist, in equip-ID order — a Theme token now,
        // because the card's miniature of a loadout must show the same nine
        //
        // THE TAB ORDER'S LAST OPEN QUESTION, ANSWERED: THE LOADOUT STAYS IN
        // INVENTORY. §6.21 asked whether it moves to Fight, beside attack and
        // guard, and §6.23's D62 is where the pull comes from - "the one real
        // want-two-at-once is a weapon against attack and guard, which is the
        // loadout, not the bag."
        //
        // It stays because equipping is a TRANSFER, and a transfer needs both ends
        // on screen. The bag and the loadout are one drag apart today; on separate
        // tabs they would be no drag apart at all, and Fight has no bag to drag
        // from. That is a mechanical cost, where the want it would serve is a
        // reading one - you want to SEE attack and guard while you swap a weapon,
        // not to reach the slots from Fight.
        //
        // So the want is recorded rather than met, and the shape an answer would
        // take is on the OTHER sheet: attack and guard read out beside the loadout
        // in Inventory, not the loadout moved to Fight. Not built - it adds content
        // to the one page D62 insists stays cheap to reach.
        private static readonly int[] LoadoutOrder = Theme.LoadoutOrder;
        private static readonly string[] LoadoutNames =
            { "Hat", "Tool", "Cloth", "Under", "Acce", "Shoes", "Off-hand", "Support", "", "Hip" };

        private sealed class CellView
        {
            public GameObject Go;
            // Cached at creation. GetComponent is not free and a cell asks for
            // its own RectTransform on every reshape - the transform of an object
            // this code created and owns, which it could simply have kept.
            public RectTransform Rect;
            public Image Box;
            public Image Fill;     // the OCCUPANCY shade, inside the frame's rim
            public Image Icon;
            public TextMeshProUGUI Count;
            public Outline Highlight;
            public Image Star;     // the favourite mark; its own object, since
                                   // Highlight and Box.color are both already spoken for
            public ItemSlot Slot;
            // index into inventory.itemSlot when this cell is backed by a MIRROR
            // slot, which is a real vanilla UI slot; -1 for plain container slots
            public int VanillaSlotId;
            // WHAT THIS CELL LAST DREW. Halving the number of cells bound barely
            // moved the clock, which is the tell that the cost is not per CELL -
            // it is per cell whose contents did not change. A chest you walked
            // past holds the same items it held 300 ms ago, and re-deriving its
            // icon, re-formatting its count into a fresh string and handing both
            // to a TMP is work with no output.
            public string DrewKey;
            public int DrewStack;
            // 0 = no search running, 1 = this item answers it, 2 = it does not.
            // In the MEMO rather than derived at paint time, because the memo is
            // what decides whether a cell repaints at all — without it a cell whose
            // ITEM did not change would keep its old mark through every query you
            // type, which is the paint cache working as designed and against us.
            public int DrewMark;
            public int DrewLife;
            public Image ToolMark;
            public bool DrewStar, DrewToolMark;
        }

        // One line of a sheet: a label at the left, a value at the right. Kind 0
        // is a value row, 1 a heading, 2 a breath of space.
        private sealed class SheetRow
        {
            public Plate Host;
            public GameObject Go;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            // the row's one control, when it has one: a chip with a glyph (a
            // stat's +, a toggle's mark) or a slider's track, fill and knob
            public Image Chip;
            public Image Glyph;
            public Image Track;
            public Image Fill;
            public Image Knob;
            public Image Icon;     // a skill's glyph, before its name
            // the header's gauge strip - HP in boxes of 100 of max, stamina
            // under it, then the icons that fill from the bottom, parted by hairlines
            public Image Well;         // the oak plate the strip sits on
            public Image BarRim;       // the black rim around both bars - Teamfight Tactics' idiom
            public Image BarTrack;
            public Image BarFill;
            public Image[] Ticks;      // a thin dark line per 100 of max, over the fill
            public Image[] Majors;     // ...and a heavy one per 1000, which outlives them
            public Image Stam;
            public Image StamFill;
            public Image[] IconRims;   // a dark copy behind each icon: its contour
            public Image[] IconBacks;
            public Image[] IconFills;
            // the love heart is BANDED like the Relations bar, with the same three
            // gradients as the relationship graph: deep is IconFills, pink and pale ride over it
            public Image LoveMid;
            public Image LoveTop;
            public Image[] Seps;
            // THE LEVEL-UP ARROW, one more emoji after the run's last group while the
            // person has attribute points to spend (PointsTag). Only a layout that has
            // room for it sets LevelSlot: the roster row and the target marker's footer.
            public Image LevelIcon;
            public Image LevelSep;
            public bool LevelSlot;
            public string LevelTip;
            public PointsTag Points;   // a form section's label, as the tag
            public bool PointsDrawn;   // ...drawn this paint; EndForm hides it otherwise
            // what each icon of the header run says when the pointer rests on it,
            // and what the two bars say: hovering the gauge shows its values
            public string[] IconTips;
            public string BarTip;
            public int DrewTicks = -1;    // how many minor tick images are placed
            public int DrewMajors = -1;   // ...and how many major ones
            public float DrewMax = -1f;   // ...and the max they were placed for
            public bool DrewBaby;
            public bool Bare;          // on a plate: the strip's own oak is not drawn
            public bool IconsOnly;     // the icon run alone, no bars
            public LoveWidget Love;    // a relation row's widget: the two hearts on one scale
            public GateHeader Gates;   // a heading's gate marks over the relation column
            public KitDropdown Drop;   // a choice row's dropdown, rebuilt when its options change
            public string DropKey;
            public Image[] Planks;     // an actions row's buttons
            public Image[] PlankIcons; // ...and their art, when drawn as icons
            public TextMeshProUGUI[] PlankTexts;
            // the roster row's own parts, beside the strip it shares with
            // the header (D55 - one component at three sizes). The gauge, the
            // name and the level come from the pieces above; these are what a
            // ROW says that a header does not have to.
            public Image Stripe;       // the kind colour, standing down the row's left edge
            public Image Hover;        // the ground under the row, lit while the pointer rests on it
            public Image Line;         // the hairline that parts one row from the next
            public Image Follow;       // the pointer: they are following you
            public Image Mark;         // the favourite star
            public Image Ring;         // the empty ring where a shortcut may land (D56)
            public TextMeshProUGUI Label2; // LinePair's second column, built on demand
            public TextMeshProUGUI Value2;
            public TextMeshProUGUI Sub;    // where they are: in reach / home / away
            public TextMeshProUGUI Note;   // why their moral is red
            // the tool shed's corkboard (InventoryWindow.Shed.cs): a roster row is a
            // paper strip pinned to the cork, a heading a strip of tape
            public Image ShedPaper;
            public Image[] ShedPin;
            public Image ShedTape;
            public Image ShedPhoto;        // a snapshot's white border
            public RawImage ShedSnap;      // ...and the snapshot in it
            public string DrewLabel;
            public string DrewValue;
            public string Tip;
            public int DrewKind = -1;
            // a roster row IS a button (D53: a row opens the column), but the CARD
            // is the same writer standing still - it is the head of the column it
            // would open, so it neither lights nor answers
            public bool DrewCard;
            public int Arg;
            public bool On;
            public bool Enabled;
            public float Frac;
            public float TrackX;
            public float TrackW;
            // the bar's DRAWN heights. The layout picks them - a card's bar is taller
            // than a list row's - and the paint must fill to what was laid out, not to
            // the constant, or a scaled track gets a list-sized fill inside it.
            public float BarH = StripBoxH;
            public float StamH = StripStamH;
            // a row on a dossier's printed form draws from its own
            // pools - the form's boxes, rules and captions vary too much by kind to
            // be fields - laid out at FormW × FormH and redrawn in place
            public bool DrewForm;
            public float FormW;
            public float FormH;
            public Transform FormArt;      // the pools' two layers: rules and boxes...
            public Transform FormType;     // ...and the type over them
            public List<Image> FormImages;
            public List<TextMeshProUGUI> FormTexts;
            public int FormImagesUsed;
            public int FormTextsUsed;
            public int[] FormArgs;     // what each box's + or each checkbox acts on, as painted
            // what each part of a form row says under the pointer, recorded as
            // it is painted - rects in the form's own frame (x right and y down from
            // the row's top left) and the tip for each
            public readonly List<Rect> TipZones = new List<Rect>();
            public readonly List<string> TipTexts = new List<string>();
            public Image PortraitMask; // the header's oval, as a stencil...
            public RawImage PortraitRaw; // ...and the character rendered into it (D125)
        }

        // Kinds: 0 a value row · 1 a heading · 2 a breath of space · 3 a stat
        // with a + chip (Arg = status index) · 4 a toggle (Arg = which) · 5 the
        // speed-limit slider
        private const int LineValue = 0;
        private const int LineHead = 1;
        private const int LineSpace = 2;
        private const int LineStat = 3;
        private const int LineToggle = 4;
        private const int LineSlider = 5;
        private const int LineSkill = 6;     // icon · name · Lv · cost · + chip (Arg = skill index)
        private const int LineBar = 7;       // label · a progress band · value (Frac = the fill)
        private const int LineDropdown = 8;  // label · a kit dropdown over Options, at Index (Arg = which)
        private const int LineOption = 9;    // label · a check chip (Arg = sexInfo index; On = allowed)
        private const int LineActions = 10;  // a row of plank buttons, one per Option
        private const int ToggleNameShown = 0;
        private const int ToggleFavourite = 1;

        private struct SheetLine
        {
            public string Label;
            public string Value;
            public int Kind;
            public int Arg;
            public bool On;
            public bool Enabled;
            public float Frac;
            public string[] Options;   // a dropdown's choices, or an actions row's button labels
            public Sprite[] Icons;     // the dropdown's glyph per choice
            public int Index;          // the dropdown's current choice
            public Sprite Icon;        // a glyph before the label, any kind
            public Color Tint;         // a gauge's fill, and its icon when the icon is a mask
            public bool Tinted;
            public bool TintIcon;
            public Color LabelTint;    // the name in the roster's colour for that character
            public bool LabelTinted;
            public Color ValueTint;    // ...and the ANSWER in its own, where the answer is the news
            public bool ValueTinted;
            public float Love;         // a relation's forward edge (A → B)...
            public float Back;         // ...and the edge back (B → A)
            public bool BackKnown;
            public bool Gates;         // a heading that wears the gate marks over the relation column
            public string Tip;         // a relation's numbers, shown over the bar on hover; any other row's tip over the whole row
            public string[] Tips;      // a form row's tip per box, check, field or button (a head: its fields, then level, then exp)
            public float[] Fracs;      // a strip's fills: life · faint · food · water · moral · love · libido · pregnancy (-1 = none)
            public Color[] Tints;      // ...each in its own colour (white = the sprite's own)
            // the love heart in the Relations bar's three bands, bottom up: pale to 50,
            // pink to 75, deep past it - the fill each band reaches, and its colour
            public float[] LoveBands;
            public Color[] LoveBandTints;
            // what the TRACK and the rim wear, when an icon's sprite carries its own
            // colours: a flat mask of the same silhouette. Image.color MULTIPLIES, so a
            // pre-coloured sprite behind the fill stays pre-coloured and never reads as
            // empty: only the inner flame in the icon is dynamic.
            // A null entry means the icon is a mask already and is its own track.
            public Sprite[] Masks;
            public string[] IconTips;  // the strip's per-icon readouts, on hover
            public string BarTip;      // ...and the HP / stun bars'
            // points to spend (PointsTag): a section's label drawn as the tag - 1 the
            // skill book's, 2 attributes' - and on a strip, the level-up arrow
            public int PointsKind;
            public bool LevelUp;
            public long LevelPoints;
            public string LevelTip;
            public float Cur;          // a strip's life...
            public float Max;          // ...and its max, which decides the boxes
            // a roster row's own facts. Tint is the kind colour on this
            // kind of line - the stripe's - since no gauge on a roster row is
            // drawn from it.
            // LinePair's second column. The first pair uses Label/Value like any
            // other row, so a pair line degrades to a plain row by leaving these
            // empty rather than by needing a different kind.
            public string Label2;
            public string Value2;
            public Color Value2Tint;
            public bool Value2Tinted;
            public string Where;       // in reach / home / away
            public string Cause;       // the discontent cause, when moral is red
            public bool CauseWarn;     // ...or the drain coming, while it is not (amber)
            public bool Picked;        // a roster row: this is the person you have targeted
            public bool Following;
            public bool Starred;
            // the CARD (D66): the roster row at frame width. One field is all it
            // differs by - the plate above already carries the name, so the row
            // spends that space on the level and the kind instead.
            public bool Card;
            // the printed form's rows. A BOX ROW is up to four boxes,
            // each a caption (Options), a reading (Values), an optional bar (Fracs,
            // Tints; a negative frac is none), an optional foot with a + chip
            // (Foots, Cans), a glyph (Icons) and level pips (Pips of PipMax); Arg
            // says what a + does and Args which one. A CHECK ROW is up to three
            // checkboxes: Options their words, Cans their state, Args what each
            // toggles, Glyphs how each is drawn. Style picks a kind's variant.
            public string[] Values;
            public string[] Foots;
            public bool[] Cans;
            public int[] Args;
            public int[] Pips;
            public int[] PipMax;
            public int[] Glyphs;
            public int Style;
            public int Danger;     // an actions row's destructive plank, as its index + 1
            public float[] Pools;  // a box bar's pool size (HP, stun), marked per 100 and 1000; 0 = none
            public float[] SeriesX;  // LineMoodNow: when, 0..1 across the last three days...
            public float[] SeriesY;  // ...and the mood then
        }
        private const int LineGauge = 11;    // icon · a filled band in its own colour · value
        private const int LineRelation = 12; // name · two thin love bands on one scale · the two numbers
        private const int LineMore = 14;     // "… N more": the whole row opens the matrix window
        private const int LineStrip = 15;    // the header's gauge strip (§6.21): HP · stamina · icons
        // the roster. A ROW is the header at row size - the same strip
        // under a name line - plus where they are and the marks on them; the BAR
        // is the list's own control row, sort and the two camp-wide verbs.
        // The acts, on Overview (target-matrix.md G7). A row of planks like
        // LineActions, but the labels are READ from vanilla and Arg carries the
        // index of the row's first verb, so a press maps back to the live set.
        private const int LineActs = 18;
        // The PLACEMENT acts - Move, Remove, Repair - as their own row of small
        // planks. A kind of their own rather than a flag on LineActs,
        // because they come from a different panel, press through a different
        // reader, and are drawn smaller: they are about the object as something
        // you BUILT, not something you are dealing with.
        private const int LinePlace = 19;
        // the waiting-pose grid: icon buttons, Arg = where this row starts
        // in the live pose list, Index = which of them is set
        private const int LinePose = 20;
        private const int LineRoster = 16;
        private const int LineRosterBar = 17;
        // TWO facts on one line. Overview's four standing facts are
        // short words on a sheet as wide as a bag grid (D49 keeps the column from
        // moving between tabs), so one per line left the page's right half empty
        // and the block four rows tall for eight words. Two columns fill the
        // measure and halve the height - which is the roster row's own lesson,
        // that density is what makes a reading scannable.
        private const int LinePair = 21;
        // the printed form's own rows (InventoryWindow.Dossier.cs)
        private const int LineFormHead = 22;   // portrait · name line · two fields · level medallion · exp
        private const int LineSection = 23;    // a spaced-capitals heading, its rule, and a pill
        private const int LineBoxes = 24;      // a row of numbers in boxes
        private const int LineChecks = 25;     // checkboxes, side by side
        private const int LineField = 26;      // one or two fill-in fields: a word on a ruled line
        private const int LineMoodNow = 27;    // the mood in a box, and its last three days as a stepped line
        private const int LineMoodCause = 28;  // a tick · a cause · its cost · its fix (Index = MoodSeverity)

        private sealed class Plate
        {
            // A masked spacer: it carries no cells of its own, only a height, and
            // the pool's members live INSIDE it as children so the mask can clip
            // them. See BindReach.
            public bool IsViewport;

            public UIWindow Window;
            public Image Back;
            public Outline Edge;
            public TextMeshProUGUI Header;
            public readonly List<CellView> Cells = new List<CellView>();
            public readonly List<Image> TypeIcons = new List<Image>();
            public int Shown;
            public int Cols;
            public float Height;
            public int Column;  // 0 = your own (right), 1 = the target (left of it)
            public CommonStates Owner;   // whose loadout this is, for equip previews
            // The container this band is drawing, when it is drawing one. Needed
            // because a write to some containers is only HALF the change (D39):
            // the game's own callback has to be handed the container and the slot
            // that moved, and a cell knows neither.
            // the prop this sheet is about, when the target is a thing
            // rather than a person. Owner and Prop are never both set.
            public ItemInfo Prop;
            public InventorySlot Container;
            public bool IsLoadout;
            // the Shared Bag: bound like any container, but placed where vanilla
            // drew its bar instead of stacked into a column
            public bool IsSharedBag;
            // a title/header row: no frame, just a label that names the group
            // stacked beneath it. Kept in the same pool so it participates in the
            // bottom-up layout instead of needing its own positioning pass.
            public bool IsTitle;
            public bool TabTitle;   // ...drawn as a name plate half in over the frame's top rail
            public bool LoadoutEdge; // this member's separator divides loadout from the rest
            public Mask PlateMask;  // the plate's stencil: children cropped to the wood's outline
            // Which GROUP this plate belongs to, and whether it IS the group's
            // frame. Three containers stacked as three frames spent three sets of
            // border padding to say one thing; one frame around the group says it
            // once and a hairline says where the parts divide.
            public int Group;
            public bool IsGroupFrame;
            public Image Separator;   // the hairline under a member, drawn in its gap
            // A group frame's inner FIELD. ui_frame's own middle is the rim
            // colour, not the field, so the cream is painted inside it - which is
            // what vanilla's panel actually is: brown edge, thin orange rim, then
            // the field. One image rather than a second skin.
            public Image Field;
            public Image Rail;     // the kit's hairline walnut rail over a kit_field back
            public Image Close;    // the close chip on the frame's top-right corner (CloseChip)
            public Image ScrollThumb;  // the kit scrollbar's walnut thumb, inside Scroll (its track)
            public Image SectionArrow; // a reach section's disclosure triangle (parked with the sections, Slot.CompactContainers)
            public RectTransform HeaderHit; // ...and its header row's click target
            public Image Scroll;   // the pool's paint-mark scroll hint
            public Image Rule;     // hairline under/around a title
            // The vicinity heading's family toggles (D41). On the plate rather
            // than in a permanent object of their own because they belong to the
            // heading and move with it - and because every decoration a bind path
            // can opt into is cleared in one place, which is what makes the
            // pooled-plate class of bug impossible rather than fixed.
            // The vicinity heading's action cluster. Painted glyphs on the
            // game's own button tile - the one family in its art that already
            // means "pressable" (D44).
            public List<Image> Buttons;
            public Image Badge;    // entity icon beside a title
            public Image Star;     // the favourite mark after a favourited NPC's name
            // D50/D52: the marks you make on them - the star and the eye -
            // as chips on the frame's top rail, left of the close chip
            public List<Image> Tiles;
            public List<Image> TileGlyphs;
            public Image WearTrack;   // a thing's wear, on the rim (D113)
            public Image WearFill;
            public ActRow Acts;       // a thing's act row, the plate at its window's foot (D105)
            public InventorySlot ActBag;   // ...or a person's bag, when the row is that bag's three
            public Image Band;        // a frame collapsed to one oak band, for a thing with nothing to show
            public Image Mark;        // a dossier's stamp (yours) or paperclip (theirs)
            // §6.20: a SHEET - the column's Status / Skills / Relations / Orders
            // aspect, drawn as rows of type instead of cells. Same pooled plate,
            // same bottom-up stack; only what is on it differs.
            public bool IsSheet;
            public RectMask2D SheetMask;
            public Image SheetTrack;
            public Image SheetThumb;
            public float SheetContentH;
            public int SheetScrollGroup;
            public bool IsHeader;   // ...the header over the aspect: the identity line and the gauge strip
            public bool IsCard;     // ...and that header as the CARD (D66), which is re-derived by a different writer
            public bool IsRoster;   // ...or a roster row, whose line is re-derived from Owner
            public List<SheetRow> Rows;
            public int RowsShown;
            public float SeamY;          // where the loadout seam runs, in this plate's content space
            // cell placement only changes when the container's SHAPE changes;
            // contents change constantly, geometry almost never
            public int LaidOutCols;
            public int LaidOutCount;
            public float LaidOutIconH = -1f;
            public float LaidOutHeaderH = -1f;
            public float LaidOutPitch = -1f;
            public float LaidOutGutter = -1f;
            // What the rect was last SIZED to. The inputs below decide how the
            // cells sit; this decides how big the box is, and the two are not the
            // same list - contentH also depends on whether the gutter carries a
            // caption, which none of the others can see.
            public float LaidOutW = -1f;
            public float LaidOutH = -1f;
            public float CellTop;   // where the grid starts inside the plate
            public float CellLeft;  // ...and how far in, once a gutter names it
        }

        private static GameObject _root;
        private static readonly List<Plate> Plates = new List<Plate>();
        private static int _platesUsed;
        private static TextMeshProUGUI _hover;

        private static ManagersScript _mn;
        private static GameObject _vanillaSharedBag;
        private static CanvasGroup _vanillaContainerPanel;
        private static object _target;
        private static float _nextRefresh;
        private static readonly float[] _lastColumnWidth = new float[ColumnCount];
        private static bool _ownOpen;      // your bag+loadout: toggled by the key
        private static bool _focusShown;   // the Focus: driven by what you are dealing with
        private static bool _barShown;     // the Shared Bag: ours now, and normally always up
        // The pool is CLOSED when your inventory opens: a column of a base's worth
        // of chests is not what "show me my bags" asked for. Its heading is the
        // affordance - one row carrying the count, and the arrow that opens it.
        private static bool _reachOpen;   // the vicinity WINDOW: V / the HUD chest
        // D53: the slot holds one of three windows, and two of them are OURS. The
        // flag above says the slot is ours rather than the bench's; this says
        // which of our two is in it. They share the frame, the viewport, the
        // scroll and the filter - only the list differs, which is exactly what a
        // tab is.
        private const int ViewReach = 0;
        private const int ViewRoster = 1;
        private static int _slotView = ViewReach;
        private static bool RosterView => _reachOpen && _slotView == ViewRoster;
        // §6.20: which ASPECT each dossier column shows. Which one is FACE-UP when
        // a column opens on a new carrier is §6.25's question, and the answer is
        // the DOOR, not a constant - see Door below.
        private static Aspect _ownAspect = Aspect.Profile;
        private static bool _ownSheetOpen;
        private static Aspect _focusAspect = Aspect.Inventory;

        // §6.25 D93: THE DOOR DECIDES THE LANDING PAGE; THE TARGET DECIDES THE
        // PAGE SET. A click carries no intention we can read (D59 - the click IS
        // the verb, and vanilla has spent it before any UI exists), but the ROUTE
        // the player took a moment earlier carries one, and it is free: you do not
        // walk across the island and click a follower to check their backpack, and
        // you do not open the roster to read a biography.
        //
        // This was already built, unnamed, for the one window nobody argues about:
        // your own column lands on Inventory from B and on Fight from the Status
        // button. Two doors, two pages, same window.
        //
        // D94 is the constraint that keeps it from being the unpredictability that
        // sank the remembered-page rule: a door chooses which page is
        // face-up and NOTHING else - never the set, the order, the layout, or
        // which acts are live. So D68's one window and D73's distance-only gate
        // both hold untouched.
        internal enum Door
        {
            None,          // nothing armed; the next open is a world click
            World,         // you are standing at them - Overview (G7)
            Roster,        // you were managing followers - Orders
            StatusVerb,    // you asked for the sheet - Overview
        }

        // Armed by a door just before it opens the window, and consumed once.
        // §6.25's table also names a VICINITY door (a container row, landing on
        // Inventory) and a GIFT door (an item dropped on them, the same) - neither
        // has a caller yet, so neither is an enum member: the first caller adds
        // it, and the rule above is what tells it where to land.
        private static Door _doorArmed = Door.None;
        // Set when the target changed, cleared once the landing page has been
        // resolved. It cannot be resolved at the moment the target changes,
        // because the page SET is not known until AspectsFor has run.
        private static bool _doorPending;

        internal static void ArmDoor(Door door)
        {
            _doorArmed = door;
        }

        private static Aspect PreferredAspect(Door door)
        {
            switch (door)
            {
                case Door.Roster:
                    return Aspect.Orders;
                case Door.StatusVerb:
                    // you asked for the sheet: who they are (§6.27 D107)
                    return Aspect.Profile;
                default:
                    // a world click opens no sheet (D104); if one is already open
                    // it refills on the page it was on (D107) - see LandOn
                    return Aspect.Inventory;
            }
        }

        // A door states a PREFERENCE; the set decides whether it can be honoured.
        // One line of arithmetic instead of a special case per target kind: a
        // hostile corpse is Inventory alone after permission (F3), so it lands
        // there through every door with nothing written about corpses anywhere,
        // and an unemployed villager has no Orders, so a roster pick of one lands
        // on Overview.
        private static Aspect LandOn(Door door, Aspect[] set)
        {
            if (set == null || set.Length == 0)
            {
                return Aspect.Inventory;
            }
            if (door == Door.None || door == Door.World)
            {
                // the sticky sheet: a new target under an open sheet keeps the page
                return ClampAspect(_focusAspect, set);
            }
            Aspect want = PreferredAspect(door);
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == want)
                {
                    return want;
                }
            }
            return set[0];
        }

        // The same clamp applied to a page already chosen. This is not part of
        // D93 - it is a latent mismatch D93 would otherwise expose: nothing used
        // to check that _focusAspect was a MEMBER of the target's set, so a target
        // that lost an aspect (a follower dismissed while their Orders page was up)
        // drew that sheet with the first tab lit instead.
        private static Aspect ClampAspect(Aspect aspect, Aspect[] set)
        {
            if (set == null || set.Length == 0)
            {
                return aspect;
            }
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == aspect)
                {
                    return aspect;
                }
            }
            return set[0];
        }
        private static readonly KitFootTabs[] _footTabs = new KitFootTabs[GroupCount];
        private static readonly DossierTabs[] _dossierTabs = new DossierTabs[GroupCount];
        private static readonly CommonStates[] _dossierWho = new CommonStates[GroupCount];   // whose sheet, for its tabs' points
        private static readonly Aspect[][] _footSets = new Aspect[GroupCount][];
        private static readonly Sprite[][] _footIcons = new Sprite[GroupCount][];
        private static readonly bool[] _footShown = new bool[GroupCount];
        // The group's width on its Inventory aspect. A sheet takes THIS width
        // ("the column's width is the Inventory aspect's"), so switching tabs
        // never moves the column packed to its left.
        private static readonly float[] _inventoryW = new float[GroupCount];
        private static readonly List<Plate> SheetPlates = new List<Plate>();
        private static readonly List<SheetLine> SheetLines = new List<SheetLine>();
        private static float _nextSheetPaint;
        // where the current press started: a click only counts if it is released
        // over the same cell, exactly as Unity's own OnPointerClick decides
        private static CellView _pressedCell;
        private static CellView _pressedCellRight;
        private static Vector2 _pressDownPos;
        private static bool _dragging;
        // The cell the held item was taken FROM, so it can be put back where it
        // came from. The action bar needs this: assigning a binding copies the
        // item's KEY and must leave the item alone, but the grab that brought it
        // to the cursor already moved it — so the assign has to undo the grab.
        private static CellView _grabSource;
        // Unity's own EventSystem.pixelDragThreshold default is 10; a shorter one
        // suits a grid of 20px cells, where a drag is a flick rather than a haul
        private const float DragThreshold = 6f;
        private static int _nearbyTotal;
        private static int _nearbyMatched;   // how many survived the query
        // this window's own filter. Its own query, not the crafting
        // window's -- filtering your chests for "iron" and your recipes for "rope"
        // are different questions, and one shared string would make answering
        // either erase the other. The vicinity and the roster each have their own
        // too, so this is whichever of the two is on the slot.
        private static UI.FilterField _filter => RosterView ? UI.SlotFilter.Roster : UI.SlotFilter.Vicinity;
        internal static UI.FilterField Filter => _filter;
        private static int _nearbyShown;
        // Which pool container the visible window starts at. The list SCROLLS by
        // whole containers rather than by pixels: a band is a container, and half
        // a container clipped by a viewport edge is worse than a clean list — you
        // cannot tell whether the slots you can see are all of it.
        // The pool's members live in a pool of their OWN, parented to the viewport
        // and never handed to anything else. The alternative was reparenting shared
        // plates in and out of the mask every bind - and re-ordering siblings is
        // precisely the fault that painted a frame over its own first member
        // earlier today. Two pools cost a list; reparenting costs that bug back.
        private static readonly List<Plate> ReachPlates = new List<Plate>();
        private static int _reachPlatesUsed;
        private static bool _bindingReach;      // routes TakePlate to the pool above
        private static Plate _reachSpacer;      // reserves the space, in the main pool
        private static RectTransform _reachContent;   // permanent, masked, holds them
        private static float _reachScroll;      // units scrolled up from the bottom
        private static float _reachContentH;
        private static float _reachViewH;
        // What the scroll is PINNED to. A pixel offset is meaningless across a
        // rescan: walk two steps and the pool re-sorts by distance, so the same
        // number points at different chests. Remembering a container and how far
        // into it we were survives that; the number is re-derived from it.
        private static readonly List<InventorySlot> ReachOrder = new List<InventorySlot>();
        private static InventorySlot _reachAnchor;
        private static float _reachAnchorOffset;
        // The container you are dealing with, once it has its own panel. Held so
        // the pool can leave it out - it is on screen already, and a thing drawn
        // twice reads as two things.
        private static InventorySlot _focusedContainer;
        private static readonly float[] _colX = new float[ColumnCount];
        private static readonly float[] _colY = new float[ColumnCount];
        private static readonly float[] _colAnchorX = new float[ColumnCount];
        private static readonly float[] _colPivotX = new float[ColumnCount];
        private static readonly float[] _colWidth = new float[ColumnCount];
        private static readonly int[] _colGroup = new int[ColumnCount];
        private static readonly bool[] _colStarted = new bool[ColumnCount];
        private static readonly int[] _groupFrame = new int[GroupCount];
        private static readonly int[] _groupMembers = new int[GroupCount];
        private static readonly float[] _groupLo = new float[GroupCount];
        private static readonly float[] _groupHi = new float[GroupCount];
        private static readonly float[] _groupW = new float[GroupCount];
        // members of one group are always bound back to back, so "is this the
        // first of its group" needs no counter the callers have to keep
        private static int _lastGroupBound;

        // Anything on screen at all. The two pairs open independently, so this is
        // "is the Inventory contributing anything", not "did the player press B".
        internal static bool IsOpen => _root != null && _root.activeSelf;

        // The frame around YOUR containers, while one is on screen. Null when your
        // column is not up — which is also the answer to "should the quick-stack
        // button exist right now".

        // The reach panel's own frame, for the wheel to aim at.
        private static RectTransform _reachPanel;

        // An item is on the cursor as part of a held-button gesture. The world must
        // not treat that held button as a command to walk somewhere.
        //
        // The button is part of the CONDITION, not just the flag: `_dragging` is
        // cleared in Tick's mouse-up branch, and Tick returns early when the window
        // has nothing on screen. A window closing mid-drag would otherwise leave the
        // pointer owned by the inventory forever, and clicking the world would never
        // move the player again.
        internal static bool IsDragging => _dragging && Input.GetMouseButton(0);
    }
}
