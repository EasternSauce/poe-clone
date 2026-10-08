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
saving, chat, spectator broadcasting, quests, world generation, normal spawners,
area transitions, minimap and menu systems. Existing scene scenery is deactivated
before its Start methods and then destroyed. The browser also skips its automatic
iPhone hint and background update polling in this mode.

Enemy drops remain subject to the project's existing `EnemyDropsEnabled` setting,
currently disabled. This arena isolates the bug; it does not claim to fix the
mobile hitch. A hitch here implicates code retained in this loop. Smooth kills here
suggest that a system omitted from this mode, or the full scene's size, contributes.

No character profile is read or written. Open the ordinary URL to return to the
full game. Gameplay reproduction and mobile measurements are left to the developer;
only Unity asset refresh, compilation and console inspection were performed.
