# Local sound board

Double-click `Start-SoundBoard.cmd` in the project root, or run
`powershell -ExecutionPolicy Bypass -File .\Start-SoundBoard.ps1`.
The launcher starts a hidden local Node server and opens http://127.0.0.1:8100.
Alternatively run `node tools/sound-board/server.js` and open that address yourself.
Requires Node 18 or later. No npm packages or game session are needed.

Each effect offers its current recording (including random variations), five suggestions
from `Assets/assets_for_inspiration`, a volume slider, previews, and **No sound**.
Filtering retains edits. **Discard edits** restores the last saved choices.
Preview volume is a recording gain; in game, existing master volumes, spatial attenuation,
creature pitch variation and deliberately quieter pickup/reward mixes still apply.

**Apply changes** writes `Assets/Resources/SoundBoardChoices.json`. Focus Unity to import
the changes into `SoundBoardSettings.asset`; choices are also imported before builds.
The settings apply to all characters and are included in builds. Existing builds must be rebuilt.
The board never enters Play mode. The current sound means the authored default; the selected
radio and volume show your saved override.

After adding effects or inspiration clips, use Unity's **PoeClone > Audio > Refresh Standalone
Sound Board Catalog**. This retains saved choices. Commit the JSON choices and generated
settings asset when you want to share your mix. The local service binds only to 127.0.0.1.
Set `SOUND_BOARD_PORT` to change its port. To stop it, end the Node process whose command
line contains `tools/sound-board/server.js`.

Run API checks with `node --test tools/sound-board/server.test.js`.

Enemy sounds show a small portrait rendered from their game model. After changing enemy visuals,
use **PoeClone > Audio > Refresh Sound Board Enemy Portraits** to regenerate the images.
