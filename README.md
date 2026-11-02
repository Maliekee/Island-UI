<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**Your island, in plain sight.**

**English** · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [Русский](README.ru.md) · [Português (Brasil)](README.pt-BR.md) · [Español](README.es.md) · [Deutsch](README.de.md)

![Island UI in play: the HUD, a character sheet, the roster, crafting and the settings](O1.webp)

**A carefully crafted, coherent interface for Mad Island.** It rests on a few principles, and every one of its [200 features →](FEATURES.md) grows from them:

- **[Built from the ground up.](SHOWCASE.md#built-from-the-ground-up)** Not a thousand band-aids. Island UI steps back and reworks the interface completely, from concept to code.
- **[Second nature.](SHOWCASE.md#second-nature)** Everything is presented the way people naturally take things in, so nothing needs explaining. Items fly to you as you pick them up, and stats show what they mean instead of being raw numbers.
- **[Play, not paperwork.](SHOWCASE.md#play-not-paperwork)** The chores get out of your way. Your axe comes out when you swing at a tree, you drink at the well without a click, and one press empties every chest around you into your bags.
- **[One job.](SHOWCASE.md#one-job)** Island UI is a big project with a narrow aim: the interface. It deliberately stays in its lane, so it doesn't get in the way of the game's content or your other mods. Its conveniences stay in the spirit of the game, and the rule-changing tweaks live in Island Ex instead.

This is a labour of love, and I want it to be really good. I'd love to hear what you think, so drop me a line on [Discord](https://discord.com/users/1523362502224777439) or [open an issue here](../../issues).

**[See it in play, with clips →](SHOWCASE.md)**

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

To uninstall by hand, delete `BepInEx/plugins/IslandUI`, `BepInEx/plugins/islandui-art`, and, if you updated from inside the game or turned on Check at startup, `BepInEx/patchers/IslandUpdate` and `BepInEx/island-update`
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

Island UI is young. 0.9 reworks most of the interface, and it has been played, not tested at
length: expect rough edges and the odd bug. Some of the game's own screens are not rebuilt yet
and still look and work as they always did. If something breaks, an issue with your
`BepInEx/LogOutput.log` attached helps the most.

Tested on the Steam `beta_open` branch, build 25095856, with BepInEx 5.4.23.5. After a
game update, look in `BepInEx/LogOutput.log` for `Patch census [IslandUI]: ... all bound.`
If it says anything else, the update moved something and the mod needs a new release.

## Source

Each commit here is one release; the version tags match the release zips.

<details><summary><b>How to build</b></summary>

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

## Thanks for the engagement and support

h1sagi, NoobInDaHause, Ramiyak, Ex0du5_2169, Dimastrelok, Yuuto, Cosmo, Abyss, NotTeaBut, Kringe, Kestraz, VoMor
