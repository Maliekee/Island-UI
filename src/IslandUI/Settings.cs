using BepInEx.Configuration;
using UnityEngine;

namespace IslandUI
{
    // Every ConfigEntry in one place, grouped by the architecture the sections
    // describe (docs/inventory-window-ux.md §1):
    //   [Browser]     the Catalogue Browser — the reading window (left)
    //   [Card]        the item card adapter, wherever an item appears
    //   [Inventory]   the Inventory — the acting window (right) + quick stack
    //   [Crafting]    crafting GAMEPLAY: sourcing, counting, consumption
    //   [Performance] cost controls for the vanilla panels we still drive
    //
    // Plugin.Awake calls Bind once; everything else reads Settings.X.Value.
    // Only what a player would tune is a setting: the look itself is fixed in
    // code, next to what it shapes.
    internal static class Settings
    {
        public enum RecipeGrouping
        {
            SourceBench,
            ItemType,
            None,
        }

        // ---------- [Window] ----------
        internal static ConfigEntry<KeyCode> CloseAllKey;
        internal static ConfigEntry<KeyCode> WorldFocusKey;
        internal static ConfigEntry<KeyCode> ConsoleKey;
        internal static ConfigEntry<bool> SilenceVanillaConsole;
        internal static ConfigEntry<bool> MapMood;

        // ---------- [Browser] ----------
        internal static ConfigEntry<bool> CustomGrid;
        internal static ConfigEntry<RecipeGrouping> GroupRecipesBy;
        internal static ConfigEntry<float> ScrollSpeed;

        // ---------- [Card] ----------
        internal static ConfigEntry<bool> CustomItemTooltip;
        internal static ConfigEntry<bool> ItemGuide;
        internal static ConfigEntry<bool> StoryHints;
        internal static ConfigEntry<bool> StoryPromptHints;
        internal static ConfigEntry<SpoilerMode> StorySpoilers;

        // ---------- [Cursor] ----------
        internal static ConfigEntry<UI.GameCursor.Style> CursorStyle;
        internal static ConfigEntry<int> CursorSize;
        internal static ConfigEntry<string> CursorFill;
        internal static ConfigEntry<string> CursorOutline;
        internal static ConfigEntry<float> CursorOutlineWidth;
        internal static ConfigEntry<KeyCode> CursorCycleKey;

        // ---------- [Inventory] ----------
        internal static ConfigEntry<KeyCode> InventoryKey;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachMaterials;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachChests;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachBreeding;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachBenches;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachBags;
        internal static ConfigEntry<ContainerPolicy.FamilyMode> ReachBodies;
        internal static ConfigEntry<bool> ShowNearby;
        internal static ConfigEntry<KeyCode> ReachKey;
        internal static ConfigEntry<bool> StatusOnColumn;
        internal static ConfigEntry<bool> RosterOnSlot;
        internal static ConfigEntry<bool> ToolBeltOpen;
        internal static ConfigEntry<bool> SelectionGlow;
        // props with no container become targets of ours
        internal static ConfigEntry<bool> PropWindow;
        internal static ConfigEntry<bool> PickupFlight;
        internal static ConfigEntry<float> PickupFlightSeconds;
        internal static ConfigEntry<bool> CascadeToCharacterBag;
        internal static ConfigEntry<KeyCode> QuickStackKey;
        internal static ConfigEntry<bool> QuickStackFlight;
        internal static ConfigEntry<bool> ActionBar;
        internal static ConfigEntry<int> ActionBarSlots;
        internal static ConfigEntry<bool> TheUnit;
        internal static ConfigEntry<KeyCode> SheetKey;
        internal static ConfigEntry<bool> TargetRing;
        internal static ConfigEntry<float> TargetRingScale;
        internal static ConfigEntry<float> TargetRingAlpha;
        internal static ConfigEntry<KeyCode> TargetKey;

        // ---------- [Crafting] ----------
        internal static ConfigEntry<bool> UseCharacterBag;
        internal static ConfigEntry<bool> TooltipAvailability;

        // ---------- [Performance] ----------
        internal static ConfigEntry<bool> LazyFill;
        internal static ConfigEntry<float> LazyFillBudgetMs;
        internal static ConfigEntry<bool> IsolatePanelCanvases;
        internal static ConfigEntry<bool> CoalesceCraftSweeps;
        internal static ConfigEntry<bool> DeferAggregationOnOpen;
        internal static ConfigEntry<float> MoveRefreshCooldown;

        internal static void Bind(ConfigFile cfg)
        {
            // ---------- [Browser] ----------
            CustomGrid = cfg.Bind("Browser", "CustomGrid", true,
                "Show recipes in Island UI's crafting grid. Bench menus keep the game's grid. Off = the game's grid everywhere.");
            GroupRecipesBy = cfg.Bind("Browser", "GroupRecipesBy", RecipeGrouping.SourceBench,
                "How recipes are grouped under headings: SourceBench (your own recipes first, then each bench in reach), ItemType (the game's item categories) or None (the game's order, no headings).");
            ScrollSpeed = cfg.Bind("Browser", "ScrollSpeed", 2f,
                "Mouse-wheel speed in the crafting window: rows moved per wheel notch. Higher scrolls faster.");

            // ---------- [Card] ----------
            CustomItemTooltip = cfg.Bind("Card", "CustomItemTooltip", true,
                "Hovering an item in your bags, chests or loadout shows Island UI's item card instead of the game's tooltip. Crafting, shop and held-item slots keep the game's tooltip.");
            ItemGuide = cfg.Bind("Card", "ItemGuide", true,
                "An item's card says how to get it: where it is found, what drops it, who trades it. Based on what players have collected, not on your own world.");

            // ---------- [Story] ----------
            StoryHints = cfg.Bind("Story", "ChoiceHints", true,
                "When the story asks you to choose, each option says what it leads to: affection, who joins, what unlocks. Dramatic outcomes follow the Spoilers setting.");
            StoryPromptHints = cfg.Bind("Story", "ScenePromptHints", true,
                "Scenes that wait for a key instead of showing buttons say what each key leads to, hidden ones included (as spoilers).");
            StorySpoilers = cfg.Bind("Story", "Spoilers", SpoilerMode.ClickToReveal,
                "How a hint's dramatic part (deaths, endings, someone leaving for good) is shown: ClickToReveal hides it until clicked, Always shows it, Hide leaves it out.");

            // ---------- [Cursor] ----------
            CursorStyle = cfg.Bind("Cursor", "Style", UI.GameCursor.Style.Arrow,
                "The mouse pointer: Arrow, ArrowBold, Carved, Spear, Claw, Crosshair, Ring, or Off for the system pointer. Applies live; CycleKey steps through them in game.");
            CursorSize = cfg.Bind("Cursor", "Size", 48,
                "Pointer size in pixels, 12-64. Above 32 it is drawn in software, which can trail the mouse by a frame.");
            CursorFill = cfg.Bind("Cursor", "Fill", "#FFF1D2",
                "Pointer colour, as a hex code.");
            CursorOutline = cfg.Bind("Cursor", "Outline", "#241708",
                "Pointer outline colour, as a hex code. A dark outline keeps the pointer visible on both bright and dark ground.");
            CursorOutlineWidth = cfg.Bind("Cursor", "OutlineWidth", 2f,
                "Outline thickness in pointer pixels, 0-6. 0 = no outline.");
            CursorCycleKey = cfg.Bind("Cursor", "CycleKey", KeyCode.F7,
                "Steps through the pointer styles in game. None = no key.");

            // ---------- [Inventory] ----------
            InventoryKey = cfg.Bind("Inventory", "WindowKey", KeyCode.B,
                "Opens and closes the inventory window. None = no key.");
            ReachMaterials = cfg.Bind("Inventory", "ReachMaterials", ContainerPolicy.FamilyMode.Stack,
                "Material storages (the ones a workbench draws from) in the vicinity window. Hidden = not listed; Show = listed, move items by hand; Stack = also filled by quick stack. Applies live; the chips at the foot of the vicinity window also set it.");
            ReachChests = cfg.Bind("Inventory", "ReachChests", ContainerPolicy.FamilyMode.Show,
                "Chests, boxes and the mine's output in the vicinity window. Hidden = not listed; Show = listed, move items by hand; Stack = also filled by quick stack. Applies live; the vicinity window's chips also set it.");
            ReachBreeding = cfg.Bind("Inventory", "ReachBreeding", ContainerPolicy.FamilyMode.Show,
                "Cages, traps, planters, hives, poultry boxes, mushroom logs and herb boxes in the vicinity window: Hidden or Show. Stack is not offered here and counts as Show. Applies live; the vicinity window's chips also set it.");
            ReachBenches = cfg.Bind("Inventory", "ReachBenches", ContainerPolicy.FamilyMode.Hidden,
                "Workbench storage in the vicinity window: Hidden, Show or Stack. Hidden by default, since a large base can have many benches. Needs CustomGrid. Applies live; the vicinity window's chips also set it.");
            ReachBags = cfg.Bind("Inventory", "ReachBags", ContainerPolicy.FamilyMode.Show,
                "Your people's bags within reach, in the vicinity window: Hidden or Show. Quick stack never fills them. Applies live; the vicinity window's chips also set it.");
            ReachBodies = cfg.Bind("Inventory", "ReachBodies", ContainerPolicy.FamilyMode.Show,
                "Pockets of downed or dead strangers within 3 m that still hold something, in the vicinity window: Hidden or Show. Quick stack never fills them; All out loots them. Applies live; the vicinity window's chips also set it.");
            PickupFlight = cfg.Bind("Inventory", "PickupFlight", true,
                "A picked-up item's notice flies from you to the slot it landed in, instead of waiting at the screen edge. Off = the game's notice. Applies to the next pickup.");
            PickupFlightSeconds = cfg.Bind("Inventory", "PickupFlightSeconds", 0.55f,
                "How long the pickup notice takes to fly, in seconds. Below about 0.35 the count is hard to read; above 0.8 it feels slow.");
            ShowNearby = cfg.Bind("Inventory", "ShowNearby", true,
                "Enables the vicinity window, which lists your storage within reach. Off also removes its button. Applies live.");
            ReachKey = cfg.Bind("Inventory", "ReachKey", KeyCode.V,
                "Opens and closes the vicinity window. None = no key; the button beside the backpack still works.");
            StatusOnColumn = cfg.Bind("Inventory", "StatusOnColumn", true,
                "The status button, the Status command and a roster row open Island UI's character sheet instead of the game's status window. Off = the game's window. Applies live.");
            RosterOnSlot = cfg.Bind("Inventory", "RosterOnSlot", true,
                "The friends-list button opens Island UI's roster of everyone you employ instead of the game's paged list. Off = the game's list. Applies live.");
            ToolBeltOpen = cfg.Bind("Inventory", "ToolBeltOpen", false,
                "Shows the tool belt beside the menu bar, where your axe, pickaxe and knife hang. The belt's button also switches it. What hangs on it is kept in the save.");
            SelectionGlow = cfg.Bind("Inventory", "SelectionGlow", false,
                "The game's pulsing white glow on the character you select. Off by default; on, it follows the game's own interact-glow setting. Applies to the next selection.");
            PropWindow = cfg.Bind("Inventory", "PropWindow", true,
                "Clicking a prop with no storage, such as a signboard, bed, defence or wreck, shows its name, wear and actions in Island UI's target window. Off = only the game's panel. Applies live.");
            CascadeToCharacterBag = cfg.Bind("Inventory", "CascadeToCharacterBag", true,
                "Picked-up items can also go into your character's own bag: matching stacks fill first, then empty slots, the Shared Bag before your bag. Off = only the Shared Bag, as in the unmodded game.");
            QuickStackKey = cfg.Bind("Inventory", "QuickStackKey", KeyCode.None,
                "Puts stackable items from your bags into nearby storage set to Stack. None = no key; the vicinity window's Stack button does the same.");
            QuickStackFlight = cfg.Bind("Inventory", "QuickStackFlight", true,
                "When quick stack moves items, a notice flies from your bags to each chest it filled. Off = no notice.");
            ActionBar = cfg.Bind("Inventory", "ActionBar", true,
                "A bar of item actions on keys 1-9 and 0: drag a stack onto a slot and it eats, equips or uses that item, while the stack stays in your bag. Items with no action are refused. The bar is kept in the save. Off = no action bar.");
            ActionBarSlots = cfg.Bind("Inventory", "ActionBarSlots", 10,
                "How many action bar slots, 1-10, on keys 1-9 then 0.");
            TheUnit = cfg.Bind("Inventory", "TheUnit", true,
                "When you pick someone or something, a target panel above the action bar shows its name, mood, health, stamina and needs, with buttons for what you can do with it. Off = these are shown in the target's own window instead. Applies live.");
            TargetRing = cfg.Bind("Inventory", "TargetRing", true,
                "A ring on the ground under your target, in its marker's colour: green yours, red hostile, amber a stranger or a thing, grey a body. Applies live.");
            TargetRingScale = cfg.Bind("Inventory", "TargetRingScale", 1f,
                "The ground ring's size, 0.5-2, as a multiple of the target's footprint. Applies live.");
            TargetRingAlpha = cfg.Bind("Inventory", "TargetRingAlpha", 0.85f,
                "How solid the ground ring is, 0-1. Applies live.");
            SheetKey = cfg.Bind("Inventory", "SheetKey", KeyCode.None,
                "Opens and closes your target's sheet, the same as clicking the target panel's head. None = no key.");
            TargetKey = cfg.Bind("Inventory", "TargetKey", KeyCode.Tab,
                "Targets what is nearest the cursor, one of your people or a thing, without walking there; press again for the next nearest. On Tab it replaces the game's menu key. None = no key.");

            // ---------- [Window] ----------
            CloseAllKey = cfg.Bind("Window", "CloseAllKey", KeyCode.None,
                "Closes every open window, Island UI's first and then the game's, and unpauses. None = no key.");
            WorldFocusKey = cfg.Bind("Window", "WorldFocusKey", KeyCode.Space,
                "Closes Island UI's windows and the map and quest overlays, never a scene's own menu. A press that closes something does not also attack. None = no key.");

            // ---------- [Console] ----------
            ConsoleKey = cfg.Bind("Console", "ToggleKey", KeyCode.None,
                "Opens the drop-down command console, which runs the game's own commands. None = the key left of 1, whatever your keyboard layout prints on it. While it is open it takes every key press; Escape closes it.");
            SilenceVanillaConsole = cfg.Bind("Console", "SilenceVanillaConsole", true,
                "Stops the game's own command box (opened with Return) from appearing; the drop-down console runs the same commands. Off = the game's box works again.");

            // ---------- [Map] ----------
            MapMood = cfg.Bind("Map", "MapMood", true,
                "Shows your villagers' moods on the map, plus yarn between neighbours who cannot stand each other; hover a pin for details. The map's Mood button also switches it. Applies live.");

            // ---------- [Crafting] ----------
            UseCharacterBag = cfg.Bind("Crafting", "UseCharacterBag", true,
                "Your character's own bag also counts as a source of crafting materials, alongside the Shared Bag.");
            TooltipAvailability = cfg.Bind("Crafting", "TooltipAvailability", true,
                "Recipe tooltips show how many of each material you have against how many are needed (e.g. 8/5), and grey out what you lack.");

            // ---------- [Performance] ----------
            LazyFill = cfg.Bind("Performance", "LazyFill", true,
                "Fills the game's recipe grid a few rows at a time instead of all at once, which removes the stutter near many benches and chests. Off = all at once.");
            LazyFillBudgetMs = cfg.Bind("Performance", "LazyFillBudgetMs", 3f,
                "Milliseconds per frame spent filling recipe rows. Lower is smoother, but rows appear later.");
            IsolatePanelCanvases = cfg.Bind("Performance", "IsolatePanelCanvases", true,
                "Draws the crafting panel, chest window and clothes closet apart from the rest of the UI, which removes a hitch when they change. Takes effect when a save loads.");
            CoalesceCraftSweeps = cfg.Bind("Performance", "CoalesceCraftSweeps", true,
                "Checks which recipes can be made at most once per frame, and not at all while the UI is closed, which removes stutter when opening large containers.");
            DeferAggregationOnOpen = cfg.Bind("Performance", "DeferAggregationOnOpen", true,
                "The crafting window opens at once with its last contents and refreshes the recipe list a moment later. Off = the full list is rebuilt before it opens.");
            MoveRefreshCooldown = cfg.Bind("Performance", "MoveRefreshCooldown", 0.75f,
                "Minimum seconds between recipe-list refreshes while you walk with the crafting window open. Raise it if a busy base still stutters.");

            GroupRecipesBy.SettingChanged += (_, __) => CraftLazyFill.InvalidateUnchangedCache();
        }
    }
}
