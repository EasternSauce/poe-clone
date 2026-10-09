# Read-only sound board

Double-click `Start-SoundBoard.cmd`, or run `node tools/sound-board/server.js`
and open http://127.0.0.1:8100. Requires Node 18+, no npm packages or game session.
Set `SOUND_BOARD_PORT` for a different port.

The board lists the game's current effective recordings, their uses and source file
references, plus all permanent unassigned recordings. Search by effect, enemy, use
or file and filter by category or usage. Identical recording pools share a row;
Fire Bolt and Fire Caster, for example, are one effect with both uses listed.
Every recording has its own Play/Stop button, including each variation. Only one
preview plays at a time. Previews use the original recording, without in-game
pitch, distance or master volume processing. Muted game effects can still be previewed.
Hover or keyboard-focus an effect to see all enemy portraits using its recordings.
Hover a particular variation to see all enemies using that exact recording, even
when it also belongs to other variation pools. Escape stops playback and hides portraits.

The HTTP server only reads files; all write methods return 405. There are no
selection, volume, mute, apply or import controls. Existing game audio settings
are preserved. The old choices JSON is no longer automatically imported on asset
refresh or builds. The browser never enters Play mode.

After changing game audio assignments or adding recordings, use Unity's
**PoeClone > Audio > Refresh Standalone Sound Board Catalog** and reload the page.
This exports only `tools/sound-board/catalog.json`; it never changes Unity assets,
preferences or runtime settings. The snapshot reads `SoundBoardSettings.asset`
(including existing selections) and `AmbientSoundLibrary.asset`. Source references
show the playback systems and their callers; they are not a runtime execution trace.
Unassigned means absent from these current recording pools, including retired
fallback recordings. Inspiration assets are excluded entirely.

After enemy visual changes, use **PoeClone > Audio > Refresh Sound Board Enemy
Portraits**. This writes only the board's portrait images. Stop an older sound-board
Node process before launching the rebuilt version (the launcher rejects older servers).

Run checks: `node --test tools/sound-board/server.test.js`.
