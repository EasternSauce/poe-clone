# Deploying

Hosting is set up and live; this is only how to ship an update.

| What | Where |
|---|---|
| Play | https://easternsauce.github.io/poe-clone-web/ |
| Spectate | https://easternsauce.github.io/poe-clone-web/?role=spectator |
| Server status | https://poe-clone-session-server.onrender.com/status |
| Game source | https://github.com/EasternSauce/poe-clone (`master`) |
| Web build (GitHub Pages) | https://github.com/EasternSauce/poe-clone-web (`master`), cloned at `Builds/WebGL` |

## Ship an update
1. Everything to ship is on `master` in `poe-clone`; push it. Render redeploys the session
   server (`server/`) from `master` by itself.
2. Build: Editor menu **PoeClone > Build Web** (through the bridge: `execute_menu_item`). It
   writes into `Builds/WebGL` and blocks the Editor for ~5 min; the bridge call times out, so
   check afterwards that `Builds/WebGL/Build/*` have fresh timestamps.
3. Publish from `Builds/WebGL`: `git add -A` (`-A` also removes files the build no longer has),
   commit, `git push`. GitHub Pages updates within a minute or two.
4. Revert the Unity noise in `poe-clone` (see `SESSION_SUMMARY.md`).

## Facts worth knowing
- Every build is stamped with an id (the patch notes version + UTC time): `WebBuilder` writes it
  into the published `index.html` and into `version.json`. Open pages check `version.json` once a
  minute; when it changes they save the character, show a notice and reload, so everyone moves to
  the new version within about a minute of the push. The id is also added to the build files'
  URLs (`?v=`), so a reload never mixes in cached files from the old build.
- The Web profile builds without link-time optimisation and with IL2CPP "Release" (not "Master"),
  which cuts the build time; Unity reuses compiled code for unchanged scripts (Library/Bee). Brotli
  compression is still on (smaller downloads); switching to Gzip would save a little more build time.
- The server URL is baked in from `Assets/StreamingAssets/network-config.json`
  (`wss://poe-clone-session-server.onrender.com`); it can also be edited in the hosted
  `StreamingAssets/network-config.json` without rebuilding.
- Render's free server sleeps after ~15 min idle; the first visit then takes up to a minute.
- Browsers pause hidden tabs, so a player's tab must stay visible for spectators to see it.
- URL parameters: `?role=spectator`, `?server=ws://localhost:8099` (local server),
  `?touch=1` (force phone controls).
- The page around the game is `Assets/WebGLTemplates/FullWindow/` (index.html, manifest and
  icons for iPhone "Add to Home Screen").
