<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**Play the island, not the interface.**

![Island UI in play: the HUD, a character sheet, the roster, crafting and the settings](O1.webp)

**Not a thousand band-aids.** Island UI steps back and rewrites Mad Island's interface from
the ground up: the screen, the targeting, and everything that follows from them. It is built
on a few ideas - a place on the screen for you, for what is around you and for what you are
dealing with, and one target that ties them together - and [every feature (currently counting 202) →](FEATURES.md) grew from those.

Made for the island, it steps back and lets you **play**: what you need to know is where you
look, what you want to do is one move away, and the game's otherwise hidden rules come
to light.

## What it does

- **[Yours is always yours.](SHOWCASE.md#yours-is-always-yours)** You on the right, what you are dealing with in the middle, what is around you on the left. Using an item is always on you; to give it, you drop it on them.
- **[One target, held.](SHOWCASE.md#a-target)** `Tab` picks the person or thing you are dealing with without walking, and keeps them until you let go, with a dock of everything you can do.
- **[A village, not a list of numbers.](SHOWCASE.md#your-village)** The roster shows who is unhappy, the sheet says why, and a button takes you to the fix.
- **[No more item juggling.](SHOWCASE.md#no-more-item-juggling)** Every storage within reach in one window, and crafting from your bags and everything around you.
- **[The whole fight.](SHOWCASE.md#the-whole-fight)** Who is in it, who is on you, and who is coming from off screen.
- **[Every number says why.](SHOWCASE.md#on-top)** How to get every item, where a story choice leads, the rule behind every number - in seven languages.

**[See it in play, with clips →](SHOWCASE.md)** · **[Every feature, big and small →](FEATURES.md)**

## Install

Download `Island-UI-vX.Y.Z-installer.zip` from [Releases](../../releases), extract it anywhere
and double-click `install.bat`. It finds the game through Steam, adds BepInEx only if the game
does not have it yet, and copies the mod in. `uninstall.bat` removes it again.

<details><summary><b>Other downloads, and uninstalling by hand</b></summary>

| Download | For whom |
|---|---|
| `Island-UI-vX.Y.Z-installer.zip` | **Easiest.** The installer above. It does not touch other mods or settings, and downloads nothing. |
| `Island-UI-vX.Y.Z-with-Island-Ex-installer.zip` | The same installer, with Island Ex as well. |
| `Island-UI-vX.Y.Z-with-BepInEx.zip` | No installer wanted: extract it into the game folder (next to `Mad Island.exe`). Brings BepInEx 5.4.23.5 along. |
| `Island-UI-vX.Y.Z.zip` | You already have BepInEx 5: extract it into the game folder. |
| `Island-Ex-vX.Y.Z.zip` | Island Ex alone: extract it into the game folder. Needs BepInEx 5. |

The game folder is usually `C:\Program Files (x86)\Steam\steamapps\common\Mad Island` (in
Steam: right-click the game, *Manage*, *Browse local files*). Windows may warn about running
a downloaded script; the installer is plain text and lives in [`installer/`](installer).

To uninstall by hand, delete `BepInEx/plugins/IslandUI` and `BepInEx/plugins/islandui-art`
(and `BepInEx/plugins/IslandEx` for Island Ex).

</details>

## Settings

The game's menu has an **Island UI** entry below *Option*. Changes apply at once. The mod
follows the game's language, or the one you pick: English, Simplified Chinese, Japanese,
Russian, Brazilian Portuguese, Spanish or German.

<details><summary><b>The pages, the translations and the config file</b></summary>

The pages come in groups: General (your pointer, and the language), Target, Combat, Notices,
Windows, Story and Hotkeys. **Island Ex** gets its own entry while that mod is installed.

<img src="SET.jpg" width="480" alt="The settings window on its Combat page">

The translations are machine-made, so corrections are welcome: open an issue, or edit
`BepInEx/plugins/IslandUI/Localization/<language>/strings.txt` (and `item-guide.tsv`,
`story-choices.tsv`) and send it in. Each file's header says how.

Switching off the larger pieces (the vicinity window, the action bar, the target panel, the
roster, the character sheets) is in `BepInEx/config/madisland.islandui.cfg`.

The mods' icons in the bottom-left of the title screen show that they loaded; hover one for
its version, click it to switch the mod off or on.

</details>

## Island Ex

<img src="island-ex.png" width="64" align="right" alt="Island Ex">

Island UI plays by the game's rules: it changes how you see the game and how you get there,
never what you can do. Tweaks that change the rules live in a separate small mod, Island Ex,
in the same release. Island UI does not need it.

<details><summary><b>What Island Ex does</b></summary>

- **Effect filter**: switch off any visual effect you would rather not see.
- **Hold to struggle**: in a grapple, holding the attack button keeps hitting.
- **Stay on screen**: the game no longer minimises itself on Alt+Tab or a click on another monitor.
- **Dry indoors**: rain no longer falls inside a house.
- **Zoom out**: pull the camera back further than the game allows.
- **Keep broken tools**: a worn-out tool stays in your bag, broken, for a repair kit.

</details>

## Limitations

Island UI is young. 0.9.0 reworks most of the interface, and it has been played, not tested at
length: expect rough edges and the odd bug. Some of the game's own screens are not rebuilt yet
and still look and work as they always did. If something breaks, an issue with your
`BepInEx/LogOutput.log` attached helps the most.

## Compatibility

Tested on the Steam `beta_open` branch, build 25095856, with BepInEx 5.4.23.5. After a
game update, look in `BepInEx/LogOutput.log` for `Patch census [IslandUI]: ... all bound.`
If it says anything else, the update moved something and the mod needs a new release.

## Source

Each commit here is one release; the version tags match the release zips.

<details><summary><b>The layout, and how to build</b></summary>

```
src/IslandUI/    the mod: Inventory/ (the windows, action bar, tool belt, target), Crafting/ and
                 Browser/ (the crafting window), Card/ (item and recipe cards), Model/ (favourites,
                 moods, recipes), QoL/ (keys, quick save), UI/ (controls, cursor, console, map),
                 Data/ (the item guide and story hints, as tables)
src/IslandEx/    Island Ex: one file per feature
src/Shared/      linked into both DLLs: PatchCensus.cs applies the Harmony patches one class at
                 a time and logs any that no longer bind; TitleBadge.cs is the title-screen icon
art/             the interface's art, shipped as islandui-art
installer/       the install.bat / install.ps1 shipped in the installer downloads
```

To build, you need the .NET SDK and the game installed (the projects reference its DLLs):

```
dotnet build src/IslandUI/IslandUI.csproj -c Release
dotnet build src/IslandEx/IslandEx.csproj -c Release
```

A build copies the DLL into the game's `BepInEx/plugins`; add `-p:GameDir="D:\...\Mad Island"`
for a game elsewhere, or `-p:NoDeploy=true` to build without copying. Island UI also needs
`art/*.png` in `BepInEx/plugins/islandui-art`.

</details>

## Contact

Bugs, ideas, or want to reuse something? [Open an issue](../../issues), write to
[maliekee@pm.me](mailto:maliekee@pm.me), or find me on [Discord](https://discord.com/users/1523362502224777439).
