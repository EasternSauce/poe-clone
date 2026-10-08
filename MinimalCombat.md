# Minimal mobile combat reproduction

Build and publish through the usual **PoeClone > Build Web** workflow. The same
build provides both the full game and the isolated arena; no server route is needed.

Append `?minimal=1` to the game URL (or `&minimal=1` if it already has a query).
This feature needs a new web build; an older published build cannot recognize it.

Examples, relative to the published game's index page:

- `?minimal=1`: sword and one zombie.
- `?minimal=1&weapon=short_bow`: bow and one zombie.
- `?minimal=1&enemy=Raider`: sword and one raider.
- `?minimal=1&touch=1`: force the existing mobile controls in a desktop browser.

The default arena now includes gear, gold and potion drops, loot pickup, the
inventory and character pages, skill and passive menus, and quest kill tracking.
Combat, death, loot and pickup sounds run through the normal audio systems.
Each kill guarantees a gear roll so that the added work runs on every kill;
gold and potion rewards use the normal kill reward logic and potion chances.
All gear generation, icons, labels, pop animations and pickup behavior use the
main game's implementations. Quest progress uses temporary accepted kill quests
from the existing quest book, with the arena treated as a level 1 area.

Use these switches to isolate the hitch:

| URL | Features running |
| --- | --- |
| `?minimal=1` | All the additions |
| `?minimal=1&features=0` | Original bare combat loop, with the floor fix |
| `?minimal=1&features=0&drops=1` | Bare loop plus drops and pickup |
| `?minimal=1&features=0&quests=1` | Bare loop plus quest callbacks and tracker |
| `?minimal=1&features=0&menus=1` | Bare loop plus inventory, character, skill and passive menus |
| `?minimal=1&drops=0` | All additions except drops and pickup |
| `?minimal=1&quests=0` | All additions except quest tracking |
| `?minimal=1&menus=0` | All additions except menus |
| `?minimal=1&audio=0` | All additions with the audio manager removed |
| `?minimal=1&guaranteedGear=0` | All additions with normal random gear drop chances |

Switches can be combined. Audio remains enabled in the original bare loop;
`audio=0` skips the audio manager, including its clip loading and one-shot sources.
It is useful for distinguishing audio processing from the other work on a kill.

The arena is a 48 by 48 empty floor with four low boundary walls. Use the normal
movement and attack controls. Three seconds after each kill, the corpse is removed
and one replacement enemy appears five metres ahead of the player's position,
clamped inside the arena. Respawn creation occurs on a separate frame from corpse
removal and well after the killing frame. Bosses, splitting monsters and summoners
fall back to a zombie to preserve the single-enemy loop. Invalid weapon IDs fall
back to the sword. The player can die and revive using the normal HUD.

This mode retains the real player movement, attack, equipment/stat calculations,
mobile controls, enemy AI/combat, hit effects, experience, death audio and corpse
animation. It bypasses session startup, character selection, profile loading and
saving, chat, spectator broadcasting, world generation, normal spawners,
area transitions and minimap. Existing scene scenery is deactivated
before its Start methods and then destroyed. The browser also skips its automatic
iPhone hint and background update polling in this mode.

The full game's existing `EnemyDropsEnabled` setting remains disabled; minimal
mode overrides it locally through its `drops` switch. Floors and walls explicitly
use the project's Resources/RuntimePrimitive material, whose shader is included
in mobile WebGL builds. This arena isolates the bug; it does not claim to fix the
mobile hitch. A hitch here implicates code retained in this loop. Smooth kills here
suggest that a system omitted from this mode, or the full scene's size, contributes.

No character profile is read or written. Open the ordinary URL to return to the
full game. Gameplay reproduction and mobile measurements are left to the developer;
only Unity asset refresh, compilation and console inspection were performed.
