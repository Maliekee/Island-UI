# Island UI: every feature

Island UI rebuilds Mad Island's interface from the ground up, as one interface rather than a
set of tweaks. Getting the whole right brought every feature below with it, big and small; this
is the list, for searching. The [README](README.md) has the highlights and pictures.

## Your bags
- Your bags are a column of real slot windows on the right: your bag, your loadout, the Shared Bag. **B** opens it, and so do the game's own **I** key and backpack button.
- Each window is signed with what it is: a backpack for your bag, a house for the Shared Bag, a hanger for the loadout.
- Containers keep their natural shape (18 slots show as 9×2), at the game's own cell size.
- Drawn in painted leather. One item cell everywhere: filled cells sit on a suede bed and empty ones stay hollow, so you can tell them apart at a glance, and every count reads on any item.
- Drag and drop, or click to pick up and click to place, both ways. One drag can cross from a game panel into an Island UI window.
- A pressed cell darkens; the item you pick up rides the cursor at a size you can read, with its count.
- Dragging an item over the world doesn't make your character walk after the cursor, and letting go over the world drops it.
- Your character stops walking in place when you stop while dragging.
- **Shift+click** a stack to split it in a small dialog: the wheel steps by 1, Shift+wheel by 10, digits type the count, Enter takes it, Escape or a click outside cancels (Escape no longer pauses the game).
- **Ctrl+click** moves a stack without picking it up: to the open target's bag or chest, else to a nearby chest that already holds it, else to the nearest with room, else to your feet. The other way, like goes to like first.
- Dropping an item on any loadout cell puts it in the right slot (a hat dropped on the boots still goes on the head).
- Hovering something wearable outlines the loadout slot it would fill, on whoever would wear it, in red if they can't.
- Right-click equips, uses or eats from any bag, not only the Shared Bag. Right-click a worn item to take it off.
- A plain use always applies to you, never to whoever you are targeting.
- A potion or other consumable used from your own bag goes back to its cell, and nothing is moved out of the Shared Bag for it.
- With the Shared Bag full, using something still works: room is made in your own bag.
- **T** butchers the animal under the cursor in any cell, including a cage trap.
- A planter cell takes exactly one seed, as the game intends; the rest stays in your hand.
- Expansion pockets still add their slot.
- Your hand is marked on the action bar and in the loadout: gold brackets for the main hand, blue for the off hand.
- Every window closes with the same square enamel close tag.

## Favourites
- **Alt+click** marks a favourite with a gold star; a hint shows while Alt is held.
- A favourite is one item, not a whole kind: mark one axe and its twins can still be stored.
- The mark follows the item through drags, swaps, merges and equips; a split-off part starts unmarked.
- Favourites never leave your bags by accident: no dropping, storing, equipping on someone else, and the game's own shift-transfer is blocked too. A refusal says why and the item goes back.
- Quick stack, All in and Stack leave favourites where they are. You can still rearrange them or bind them to the bar.
- Favourites are kept in your save game.

## Pickups
- Loot fills your own bag as well as the Shared Bag, topping up stacks first, instead of falling to the floor.
- Overflow keeps a weapon's attack, durability and colour.
- Carried people, downed animals and severed parts go into your bag when the Shared Bag is full; the carry limit still counts.
- Each pickup flies into the exact cell it landed in, even while paused. With the bag closed it flies to the bag button.
- Crafted items and harvests arrive the same way.

## Storage around you (the vicinity window)
- **V** lists all storage within reach as real, usable slots: chests, material storage, cages, traps, planters, your people's bags, and the pockets of bodies nearby.
- The list follows you: storage joins and drops out as you walk, nearest first by its edge, as reach measures it. Your people's bags sit among the chests.
- Each holder is shown by its picture: a person by their passport photo, a chest by its own, a body by a skull. Click the picture to target it.
- **Hold the order** keeps the list still while you work, and shows that it is held.
- **Compact** lists only the filled slots, in order, with one "N free" line that takes a drop.
- **All in**, **Stack** and **All out** work on every listed container at once, nearest first, like with like. You see the items fly.
- All out also collects harvests (trap fish, eggs, honey, herbs, mushrooms) and loots the pockets of downed or dead strangers.
- Bulk moves never fill cages, planters, people's bags or bodies.
- Chips per kind of container (materials, chests, breeding, benches, bags, bodies) choose Hidden, Shown or Stack, live.
- Every single chest and every person's bag has the same All in / Stack / All out, so looting is one click.
- Quick stack: tops up matching stacks in storage set to Stack, then fills only containers that already hold the item. It never moves weapons, tools or favourites, and hovering the button previews what it will do. Each item flies to its chest.
- The window keeps one height, the same as the roster's and crafting's, and scrolls.
- Closets, the pig farm, the flesh collector, the harvest box, workbench storage and treasure chests all open in the same windows; the game's own effects still fire (a seed gets planted, a cage gets filled).
- Your workers, haulers and harvesters still fill storage correctly.
- A wrecked chest stays closed until repaired, as in the game.
- The main character you aren't playing is listed with the others.

## Crafting
- **C** opens one crafting window: your own recipes plus those of every workbench and crafter in reach, grouped by bench.
- A bench's or crafter's Craft opens the whole window at that bench's section, and closes it again.
- Materials count from the bench, both your bags and the storage nearby, and the list refreshes as you walk or crafters move.
- Each recipe shows how many you can make; material rows show have/need and grey out what's short.
- The window is drawn as a blueprint sheet: what you can make in colour, what you can't yet as a line drawing.
- Sections carry their bench's icon and fold; a fold-all switch also applies to benches met later, and the fold state is remembered.
- Mouse wheel and PageUp/PageDown scroll the grid.
- Click pins a recipe to its card; click again unpins; Escape clears.
- **Right-click** a recipe crafts one; **Shift+right-click** asks how many.
- The card's quantity: type it, wheel it or −/+; it never crafts by itself.
- The card lists what an item is made from and what it is used in. Click a material to open its own recipe, and **Back** returns you with the filter, scroll and folds as they were.
- A recipe out of reach says "Requires … — not in reach" and comes back when you return.
- The craft button always shows, and when it can't craft it says why ("Need 2 more Iron ingot").
- Collections opens from a link under the crafting window.
- Opening near many benches and chests doesn't stutter.

## Search and filters
- One filter field in each left window (crafting, vicinity, roster), each keeping its own query when you switch tabs.
- Several words narrow together: "clothing yona" finds the Kimono.
- It matches names, descriptions, the characters who can wear an item, and plain tags (weapon, tool, food, storage, pickaxe…).
- Type a bench or crafter's name to see everything made there.
- In the vicinity window, containers with no match disappear and non-matching cells dim, with a "2 of 14" count; All in / Stack / All out then move only what matches.
- A match count and a clear button sit in the field; game keys are blocked while you type, and Escape lets go of the field first.

## Item cards
- Hovering an item shows Island UI's card beside the cell, never over it, and it carries over to the next cell with the same item.
- Every card says how to get the item: where it's found, what drops it, who trades it (625 items).
- Class, use (Food +2, Water +3), attack, durability and who can wear it.
- Hovering a carried person shows their state, level, health, needs and favourite foods.
- Disabled controls say why at the cursor.

## Action bar, hands and tool belt
- An action bar on **1–0** holds what an item does: eat, equip, use. The item itself stays in your bag; drop it on a slot to bind it.
- The count is your total across both bags; when you run out the icon greys but the binding stays.
- Tools and weapons show the durability of the copy that will be used, and the most worn one is used first.
- Press darkens, release fires; a green flash for done, red for refused.
- Drag a binding to move or swap it, or off the bar to remove it; Shift+click removes it too. A setting asks for shift on the drag, against slips.
- The bar always acts on you, even with someone targeted.
- The game's own 1–9 quick-use is turned off while the bar is on, so a key never fires twice.
- Bindings are kept in your save, and each character you play keeps a bar of their own.
- A tool belt with hooks for an axe, a pickaxe and a knife, opened from the dock.
- Chop a tree with a sword in hand and the belt's axe comes out for the job, then the sword goes back, even if you're interrupted.
- The belt shows each tool's durability, and All in leaves hung tools alone. Hooks are kept in your save.

## Targeting
- One target at a time. A click is still the game's; a person you picked stays your target until you press Escape, close it, pick something else or change area.
- **Tab** targets whatever is nearest the cursor without walking there; press again for the next nearest. Tab on nothing deselects.
- Tab reaches people, enemies, bodies, chests and props alike. A chest in reach opens from where you stand, straight on its grid (a setting).
- A marker over the target's head: their portrait, name, level, health and stun, framed in what they are to you, and their state at a glance as icons, each filled to its level: food, water, mood, love for you, libido and pregnancy. A favourite wears the gold star.
- Off screen, the marker waits on the screen's edge, points to them and says how far.
- A target inside a house: the marker stands on the door between you, tagged Inside or Outside. Going into a house keeps the target.
- A ring on the ground under the target: green yours, red hostile, amber neutral or a thing, grey a body. The colour follows what someone has shown you: whoever comes for one of yours is hostile, and a follower in revolt is neutral.
- The game's pulsing selection glow is off.
- The target dock above the action bar: the target's portrait card, the bag and the sheet as small doors beside it, and what you can do as square keys read from the game itself, all in one row.
- The portrait opens the sheet, takes a give, and toggles the favourite's star.
- Out of reach, keys dim and say how much closer to walk; the bag door works from afar and opens when you arrive.
- Talk opens the choices in a speech bubble at the person's head; you can still walk, swing and use your keys while it is up.
- Follow and Leave work at any distance. A targeted bed offers Sleep.
- Set place sits on your villager's dock right after Follow, at any distance; a second press ends it.
- Auto-use: right-click Drink or Eat on a well or a food store to switch it on (gold corners, and sparks running round the key while on); from then on you drink or eat there by yourself whenever you are near and below 70 %. Kept in your save. Every drink or meal from a station flies to you, and to your people when they eat.
- Move, Scale, Edit and Remove rise from one Manage key.
- Entries other mods add to the game's character menu appear on the dock, in their own words.
- A person's bag opens beside yours, and whether it was open is remembered for the next person.
- Follow and Leave keep the window open.
- Props without storage (signs, beds, defences, wrecks) can be targeted too: name, wear and their actions, with Repair while it is a wreck.
- Readings on props: a well's water, a food store's level, breeding progress, wear.
- A far well or food box offers Drink or Eat, greyed until you're in reach.
- A wrecked building stays targeted so you can repair it.
- The target ends by itself when the person is carried, the resource harvested, or you switch character. Deselecting closes the target's sheet and bag.
- **Tab** no longer opens the game menu (Menu on the dock does); it still cancels while building.
- A scene's own controls are never hidden while it runs.

## Combat
- A plaque at the top centre names the fight's first enemy, bosses first, with "+N in combat" for everyone else fighting you. Click it to target that enemy. A downed enemy stays a few seconds, marked Slain or Fainted.
- Light markers over the fight's first five enemies, and over the foes your people fight, with health and faint bars. Off screen they wait on the edge and point the way; an enemy on you glows red.
- Your own people in a fight wear the light marker too, their bar green.
- Health on hover: the person under the pointer shows their health.
- Settings keep markers on your people, on enemies, or on the neutral, outside a fight too.
- What you see of a stranger (level, health) follows your Observation skill.

## Giving
- Drag an item onto a person, or onto the badge over your target's head, which lights up gold while you hold something to give.
- Out of reach, you walk over and hand it over.
- One of the stack goes and flies to them; the rest stays in your hand, so you can hand items round one by one.
- You can also drop it on the target dock's portrait or on their roster row.
- Someone who can't take it says so, and the item stays in your hand.
- Love and hate potions are no longer used up on yourself by a right-click.

## Roster
- The roster replaces the friends list: everyone you employ, wherever they are.
- Sort by vicinity (in reach, at home, away), kind, job (following, each job, idle), mood (worst first) or personality.
- Each row shows level, health, needs, where they are, their job and what is wrong, in a word.
- Followers are counted against your capacity.
- "All names" and "Send home" act on everyone.
- The filter matches names, kinds and "unhappy".
- The roster button carries a red count of unhappy villagers.
- Clicking a row targets that person; a row is a drop target for giving.
- Drawn as a corkboard with each person's pinned photo; the photo's star toggles the favourite.

## Character sheets
- Character sheets replace the status window: a printed form in a kraft folder (yours on a clipboard), with a live portrait of the character as a passport photo, in front of where they are: the sky at the hour of the day, the cave, the lab, the house.
- Yours: Character, Social, Skills. A villager's: Character, Social, Orders.
- The Character page is one height for everyone: vitals in boxes (HP, stun, food, water, libido, moral), attributes with points to spend, combat values, and the personality as a strip of all ten types: theirs stands out, the types they like and dislike carry an arrow, and a click shows everyone of a type. Family is on the Social page. Kind and age are left out where the game does not know them.
- A negative moral is the sheet's alarm; a pregnancy is a pink stamp, and the give-birth act stands out.
- Rename a character on their sheet, in place. Names are printed as written, never in capitals.
- What a stranger's sheet shows follows your Observation skill, and a stranger has no Social page.
- Skills: all 12, each saying what it does at your level and the next, with live numbers.
- Every box, field and button has a hover tip explaining the game rule behind it, including tips the game only has in Japanese. Tips are on light paper, with headings, tables and the marks they describe.
- Tags on tabs and docks show unspent points, only while they can buy something.
- "Always run", "Show name in world" and "Favourite" can be switched on the sheet.
- The sheet stays open across targets until you close it. Drag it, or press its chip, to move it to the free slot on the left.

## Mood and relations
- The Social page charts a villager's moral over three days, with a lane for each thing that moves it and its cost; hover any part for the why.
- It warns of a grudge before it costs anything. A problem (sleep, bad company, a crush who said no, no bed) unfolds to its fix, with a button for each step.
- The mood ledger remembers why moral changed, across sleeps and reloads.
- The love ledger remembers every change of one person's feeling for another: who met whom at each love tick, what it was worth and why. Kept in the save.
- Relations on the same clock: a lane for every relation, love both ways, the last days' ticks, nights marked, the next tick forecast, and family among them.
- Your own page lists who loves you.
- Hover a person on the Social page and an arc in the world shows how the two feel about each other, both ways.
- Names lead to the person, types to everyone of that type.
- A log line warns when someone is close to rebelling, and a morning report says who didn't sleep and why.
- The record: partners, pregnancies, deliveries and the game's other tallies.

## Orders
- Combat role and job, each with what it does in the game's own words, translated. Every switch is its icon.
- Where they stand: following you, or a place of their own with territory and combat-area circles drawn live in the world.
- Set place shows what a spot gives (food, wells, a house, neighbours, with arcs to them); the wheel sizes the circle, Escape cancels and puts everything back, and you can walk while placing. It lasts while they are your target, Orders page open or not, and stays live while they follow.
- Go there, waiting pose (only poses that character can play) and facing.
- Permissions (entering the house, love, sex), Edit dialogue, Return, Call and Banish.
- A "Give birth" action on the pregnancy page.

## HUD
- A painted HUD in walnut and ivory, lettered in the same faces throughout.
- A dock of icon buttons: Crafting, Vicinity, Roster, Map, Quests on the left; Toolbelt, Character, Inventory and Menu on the right. Each looks pressed while its window is open.
- The vitals panel: a live portrait, health and stun bars, food and water. Low health, food or water glows.
- A game log feed in the left column, under the windows.
- Two EXP rails under the action bar, level in blue and skill in green, exact values on hover.
- Level-up: the rail shines, a tag pops the points earned, the level coin bumps.
- During cutscenes the HUD hides as the game's does.

## Map
- Favourites as photo pins, fanned out when close; a click selects the person, the target's pin framed in gold.
- Either mouse button pans the map, and it stays under the cursor.
- The map and the quest diary wear the HUD's look and no longer overlap.

## Console
- A drop-down console on the key left of 1, on any keyboard layout.
- The game's own commands, with Tab completion, hints, history kept between sessions and a scrolling log of the game's messages.
- Mistakes are explained in words; it takes every key while open.

## Keys, settings and cursor
- An **Island UI** entry in the game menu, the mods' icons beside their entries: pages in groups for the cursor and language, the target, combat, sheets, notices, story hints, Island Ex and every key, applied live.
- Click a key row and press the new key; clashes are outlined and named.
- Every one of the game's own keys can be remapped, and typing in text fields is never affected.
- **Space** closes every window and the map; a press that closes something doesn't also attack.
- **F6** quick-saves to the slot you loaded, with a small "Saved" line instead of the game's popup, kept out of the save's screenshot.
- A cursor that stays readable on sand: seven styles, size, fill and outline; **F7** cycles them.
- While holding a modifier, a cursor badge says what the next click will do, and only where it would act.
- Game hotkeys don't fire while you type in the game's own text fields.
- A repeated warning stays up its full time.
- World hover names no longer show through windows.

## Your language
- Island UI and Island Ex follow the game's language, or the one chosen in the settings window.
- Shipped in Simplified Chinese, Japanese, Russian, Brazilian Portuguese, Spanish and German, the item guide and the story hints included. In Chinese and Japanese they use the game's own words for items, places and stats.
- Open windows switch at once; longer languages shrink to fit their boxes.
- The translations are machine-made: corrections are welcome, as an issue or a pull request against `Localization/<language>/` in the mod's folder.

## Story hints
- Each story choice says what it leads to: affection, who joins, what unlocks, what closes later (67 choices).
- Scenes that wait for a key say what each key does, hidden keys included.
- Off, on click, or always: on click (the default) a choice shows one "Hints" bar, and the hints only once you click it.
- Deaths, partings and endings inside a hint stay hidden until you click them.

## Title screen
- Each mod's icon in the corner shows it loaded; hover for its version, click to switch it off or on.

## Under the hood
- Island UI's data lives inside your save file, written safely; the save still loads without the mod.
- After a game update, one broken hook costs one feature, not the mod, and the log names it.
- Recipe and item lookups warm up after a load, so the first open doesn't stutter.
- Loading another save resets everything cleanly.

## Island Ex
- **Effect filter**: switch off any visual effect; the settings page lists the effects seen this session.
- **Hold to struggle**: in a grapple, holding attack keeps hitting.
- **Stay on screen**: no minimising on Alt+Tab, desktop switches or clicks on another monitor; Win+M still works.
- **Dry indoors**: no rain inside houses.
- **Zoom out**: a setting to pull the camera back further than the game allows.
- **Keep broken tools**: a setting to keep a worn-out tool in your bag, broken, for a repair kit, instead of losing it.
- Its own menu entry and title-screen switch.
