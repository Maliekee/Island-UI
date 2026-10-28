# Changelog

## 0.9.0

Island UI rebuilt: a new design across nearly every page, in how it works and how it looks, a new combat system, and fundamental changes underneath. This is the design meant for 1.0; it needs playing before it gets that number.

### Your language

- Island UI and Island Ex speak your language. They follow the game's language, or the one chosen on the General page of the settings window. Shipped in Simplified Chinese, Japanese, Russian, Brazilian Portuguese, Spanish and German, the item guide and the story hints included; in Chinese and Japanese they use the game's own words for items, places and stats. The translations are machine-made: corrections are welcome, as an issue or a pull request against `Localization/<language>/` in the mod's folder.
- Open windows switch language at once, and longer languages shrink to fit their boxes.

### A new look

- A painted HUD in walnut and ivory: the vitals panel bottom left, the player dock (Toolbelt, Character, Inventory, Menu), the target dock and the combat plaque.
- Low health, food or water glows on the vitals panel.
- Portraits sit in front of where the person is: the sky at the hour of the day, or the cave, the lab, the house.
- Your bags and the other's bag in painted leather; one item cell everywhere, the held item included, and counts that read on any item.
- Your own character sheet on a clipboard.
- The map and the quest screen wear the HUD's look: a rail, a title tab and a close.
- The mods' icons beside their entries in the game's menu.

### Combat

- The combat plaque at the top centre names the fight's first enemy, bosses first, with "+N in combat" for everyone else fighting you. Click it to target that enemy. A downed enemy stays a few seconds, marked Slain or Fainted.
- Light markers over the fight's first five enemies, and over the foes your people fight, with health and faint bars. Off screen they wait on the rim and point the way; an enemy on you glows red.
- Your own people in a fight wear the light marker too, its bar green.
- Health on hover: the person under the pointer shows their health.
- Settings to keep markers on your people, on enemies, or on the neutral, outside a fight too.
- A marker's colour says what someone has shown you: friendly, neutral or hostile. Whoever comes for one of yours comes for you.
- What you see of a stranger (level, health, their sheet) follows your Observation skill.

### The target

- An off-screen target's marker points to them and says how far.
- A target inside a house: the marker points at the house, and the door between you wears a tag saying Inside or Outside. Going into a house keeps the target.
- The target dock, redrawn: the bag and the sheet as small doors beside the portrait, then the acts as square keys in one row. The portrait opens the sheet, takes a give, and wears the favourite's star (click it to toggle).
- Follow and Leave work at any distance. A targeted bed offers Sleep.
- Set place sits on your villager's dock right after Follow and works from any distance; it no longer needs the Orders page open.
- A crafter's or a bench's Craft opens the whole crafting menu at its section, and closes it again.
- Move, Scale, Edit and Remove sit under one Manage key.
- Entries other mods add to the game's character menu show on the dock, in their own words.
- Talk opens its choices in a speech bubble at the person's head, and you can still walk and use your keys while it is up.
- Deselecting closes the target's sheet and bag; switching character ends the target.

### Character sheets

- The Character page is one height for everyone: a passport photo, and the personality as a strip of all ten types: theirs stands out, the types they like and dislike carry an arrow, and a click shows everyone of a type. Family is on the Social page. Kind and age are left out where the game does not know them.
- Rename a character on their sheet, in place. Names are printed as written.
- A negative moral is the sheet's alarm; pregnancy is a pink stamp, and the give-birth act stands out.
- The Social page reads like a record: who they met today and why, their bonds, every relation with how it moved over the last days, and the mood over time with what moves it. A problem unfolds to its fix, with a button for each step.
- Hover a person on the Social page and an arc in the world shows how the two feel about each other, both ways.
- The sheet can be dragged to the free slot on the left.
- Tooltips on light paper, with headings, tables and the marks they describe.

### Roster, map and orders

- The roster sorts by Job and by personality; a photo's star toggles the favourite.
- Favourites on the map as photo pins, fanned out when close; a click selects the person. Either mouse button pans the map. The map's mood pins are gone.
- Orders: each toggle is its icon. Set place shows what a spot gives (food, water, a house, neighbours, with arcs to them), the wheel sizes the circle, Escape cancels, and you can walk while placing.

### Inventory

- The vicinity window lists nearest first, people's bags with the chests, each holder by its portrait or picture. A Compact view lists only the filled slots, and the order can be held still while you work.
- A chest opens straight on its grid; a setting turns that off.
- Auto-use: right-click Drink or Eat on a well or a food store to switch it on, and from then on you drink or eat there by yourself whenever you are near and in need. Every drink or meal from a station flies to you, or to your people when they eat.
- Each character you play keeps an action bar of their own.
- Action bar slots and belt tools drag freely, and one dragged off the bar is removed. "Shift to move" in the settings brings back the shift guard against slips.
- Escape closes the split dialog instead of pausing the game.
- A game log feed in the left column, under the windows.
- The settings window: seven pages in groups.

### Story hints

- One setting for story hints: off, on click, or always. On click, the default, a choice or a scene's key prompt shows one "Hints" bar until you click it, and its tip says where to change that. Clicking never answers the scene.
- Deaths, endings and partings inside a hint are always masked until clicked; the setting that showed or dropped them is gone.

### Island Ex 0.2.0

- A setting to zoom out further than the game allows.
- A setting to keep a worn-out tool in your bag, broken, for a repair kit, instead of losing it.
- The effect filter no longer blocks pieces combat needs.

## 0.3.1

- Giving: drag an item onto a person, or onto the badge over your target's head, which lights up gold while you hold something to give. Out of reach, you walk over first. One of the stack goes, flies to them, and the rest stays in your hand. Only someone the game lets you give to can take it; anyone else says so, and the item stays in your hand.
- Love and hate potions are no longer used up on yourself by a right-click: they say to drag them onto someone instead.
- A potion or other consumable used from your own bag goes back to its slot, and nothing is moved out of the Shared Bag for it.
- The action bar marks your off hand with blue brackets, your main hand with gold.
- A warning repeated within a few seconds stays up its full time.
- Your character no longer keeps walking in place when you stop while dragging an item.

## 0.3.0

First public release.

- Inventory: your bags as a column of windows on the right, chests opened in the same kind of window, and the vicinity window listing every storage within reach, with All in, Stack and All out across all of them.
- Item handling: Ctrl+click to move, Shift+click to split, Alt+click to favourite; quick stack into nearby storages; pickups that fill your own bag too and fly into their slot.
- The action bar (ten slots that hold what an item does, not the item) and the tool belt (the axe, pickaxe and knife the game reaches for).
- Crafting: your recipes and every workbench's within reach in one grouped, filterable grid, materials taken from nearby storage, counts of how many you can make, and a blueprint recipe card that links to materials and back.
- The target: one at a time, Tab to target without walking, an indicator above the action bar with the target's state and acts, and a ring on the ground.
- Character sheets as tabbed dossiers, the roster in place of the friends list, and moods with their causes: on the sheets, in the roster and as pins on the map.
- Story choices that say what they lead to, item cards that say how to get an item, a readable cursor, quick save, a drop-down console, and every key rebindable in the settings window.
- Island Ex 0.1.0 ships alongside as its own download: the effect filter, hold to struggle, stay on screen and dry indoors.
