# Playing this in a browser with a friend

This turns the game into a link you can send a friend: whoever opens the plain link gets to
play (as long as no one else currently is), and a second link lets anyone watch live while
someone else plays. There's a shared chat box both of you can use.

It's two separate pieces you deploy separately, both on free tiers:

1. **The game itself** (a Unity WebGL export) - static files, hosted on **GitHub Pages**.
2. **The session server** (small Node.js app - the thing that enforces "only one player at a
   time" and relays the spectator stream + chat) - hosted on **Render.com**.

They're separate because GitHub Pages can only serve static files; it can't run the
always-on Node process the session lock needs. Render's free tier runs that for you.

---

## Part 0 - One-time setup (GitHub account + Git)

Both hosts are free and need no credit card. Render logs in with your GitHub account, so
start there.

### 0.1 Create a GitHub account
1. Go to https://github.com/signup, enter your email, a password and a username (the
   username ends up in your game's URL: `https://<username>.github.io/...`).
2. Confirm the email GitHub sends you.

### 0.2 Install Git
1. Download Git for Windows from https://git-scm.com/download/win and install it with the
   default options.
2. Open a new PowerShell window and tell Git who you are (once per machine):
   ```
   git config --global user.name "Your Name"
   git config --global user.email "you@example.com"
   ```

### 0.3 Put this project on GitHub
Render deploys the server straight from a GitHub repository.
1. On GitHub: top-right **+** -> **New repository**. Name it e.g. `poe-clone`, choose
   **Private** if you like (Render can read private repos once connected), and **don't** tick
   "Add a README" - the repo must start empty.
2. In PowerShell, from this project's folder:
   ```
   git remote add origin https://github.com/<your-username>/poe-clone.git
   git push -u origin master
   ```
   The first push opens a browser window asking you to sign in to GitHub - approve it.
   (`Library/`, `Builds/` etc. are git-ignored, so only the source is uploaded.)

---

## Part 1 - Deploy the session server (Render.com)

### 1.1 Create a Render account
1. Go to https://dashboard.render.com/register and choose **GitHub** - this both creates the
   account and links it to your repositories.
2. When GitHub asks which repositories Render may access, either allow all or select
   `poe-clone`.

### 1.2 Create the web service
1. In the Render dashboard: **+ New** -> **Web Service**.
2. Pick the `poe-clone` repository from the list (if it's missing: **Configure account** ->
   grant access to it).
3. Fill in the form:
   - **Name**: `poe-clone-session-server` (this becomes part of the URL)
   - **Language/Runtime**: `Node`
   - **Branch**: `master`
   - **Root Directory**: `server`   <- important, the server lives in a subfolder
   - **Build Command**: `npm install`
   - **Start Command**: `npm start`
   - **Instance Type**: **Free**
   - Under **Advanced**: **Health Check Path** = `/health`
4. Click **Deploy Web Service**.

   (Alternative: **+ New** -> **Blueprint**, pick the repo and set the blueprint file path to
   `server/render.yaml` - it contains exactly the settings above.)
5. Wait for the first deploy to finish (a couple of minutes). Render will give the service a
   URL like `https://poe-clone-session-server.onrender.com`.
5. Test it: open `https://poe-clone-session-server.onrender.com/health` in a browser - it
   should show `{"ok":true}`.

### 1.4 Know the free-tier catch
Render's free web services **spin down after ~15 minutes with no traffic** and take
20-50 seconds to wake back up on the next request. In practice: if nobody's played in a
while, the first person to open the link will see "Connecting..." for up to a minute before
the game appears. This is normal - there's nothing broken, it's just waking up. Later
connections (like your friend joining right after) are instant.

### 1.5 (Recommended) lock CORS down to your actual site
The service ships with `ALLOWED_ORIGIN=*`, which works fine but is loose. Once Part 2 gives
you your GitHub Pages URL, go to the service's **Environment** tab on Render and set:
```
ALLOWED_ORIGIN=https://<your-username>.github.io
```
then let it redeploy. (This only restricts the plain HTTP `/health` and `/status` endpoints;
it's a minor hardening step, not what actually enforces the one-player-at-a-time rule - the
server's own session lock does that regardless.)

---

## Part 2 - Build and deploy the game (GitHub Pages)

### 2.1 Export the Web (WebGL) build from Unity
In Unity 6 the WebGL platform is called **Web**, and builds are made from a *build profile*.
1. **File -> Build Profiles**. On a fresh project the left side says "No build profiles in
   your project".
2. Click **Add Build Profile** (top-left, or the button in the middle). The **Platform
   Browser** opens.
3. Scroll to the **Web** section, select **Web** (globe icon), and click **Add Build Profile**
   at the bottom right. If Web shows an **Install** button instead, the Web Build Support
   module is missing: install it from Unity Hub -> Installs -> (your Unity version) -> gear
   icon -> **Add modules** -> **Web Build Support**, then reopen the project.
4. Back in Build Profiles, select the new **Web** profile on the left and click
   **Switch Profile** (makes it the active platform; the first switch reimports assets and can
   take a few minutes).
5. Check that **Scene List** (top-left) has `Assets/Scenes/Game.unity` ticked.
6. Click **Build**, choose an output folder, e.g. `Builds/WebGL` (this folder is git-ignored
   on purpose - see step 2.3 for why that's fine), and wait - the first build takes several
   minutes (about 5 on this machine).

The project's Player Settings are already configured for a host like GitHub Pages that can't
set custom compression headers (`Compression Format = Gzip` with `Decompression Fallback`
enabled) - you don't need to change anything there.

### 2.2 Point the build at your session server
1. In the build output folder, open `StreamingAssets/network-config.json` in a text editor.
2. Replace the placeholder with your Render URL from Part 1, **using `wss://` (not
   `https://`)**:
   ```json
   { "serverUrl": "wss://poe-clone-session-server.onrender.com" }
   ```
3. Save. You can edit this file any time after deploying too, without rebuilding in Unity -
   see step 2.7.

### 2.3 Create a GitHub Pages repo for the build output
Keeping the ~50-100MB build output out of your main source repo is the norm - use a separate
repo (or a separate branch) just for the hosted files.
1. On GitHub, create a new repository, e.g. `poe-clone-web`. Make it **Public** - on a free
   GitHub account, Pages only publishes public repositories. (It only contains the compiled
   build, not your source.) Don't tick "Add a README".
2. On your machine, in the WebGL build output folder (the one containing `index.html`):
   ```
   git init -b master
   git add .
   git commit -m "WebGL build"
   git remote add origin https://github.com/<your-username>/poe-clone-web.git
   git push -u origin master
   ```

### 2.4 Turn on Pages
1. In that repo on GitHub: **Settings -> Pages**.
2. Under **Build and deployment**, set **Source** to **Deploy from a branch**, branch
   `master`, folder `/ (root)`.
3. Save. GitHub gives you a URL like `https://<your-username>.github.io/poe-clone-web/`.
   The first deploy takes 1-2 minutes - until it finishes the URL shows **"404 - There isn't a
   GitHub Pages site here"**. Progress is visible in the repo's **Actions** tab ("pages build
   and deployment"); once that's green, reload.

### 2.5 Share the links
- **To play**: `https://<your-username>.github.io/poe-clone-web/`
- **To spectate**: `https://<your-username>.github.io/poe-clone-web/?role=spectator`

Whoever opens the plain link first gets the play slot. Anyone else who opens the plain link
while that's happening sees "Someone is already playing" and it automatically starts the
moment the first person's session ends. The spectate link always works, showing a "no one's
playing" placeholder when the game is idle and the live game once someone starts (see
"How the live spectator view works" below).

### 2.6 Check that everything works (5 minutes)
1. Open `https://poe-clone-session-server.onrender.com/status` - it should show
   `{"type":"status","playerActive":false,...}`. (First request after a quiet period can take
   up to a minute: the free server is waking up.)
2. Open the **play** link. After "Connecting...", the game starts and you can move.
3. In a second browser window, open the **spectate** link. Within a second or two you should see
   the same scene with a red **LIVE** badge at the top, and your character moving smoothly
   as you play in the first window. Type in the chat box in either window - it appears in both.
4. In a third window, open the **play** link again - it must say "Someone is already
   playing". Close the first (playing) window: the third one takes over within a few seconds,
   and the spectator switches to watching it.

If step 1 shows a plain "Not Found" instantly, there's no Render service at that address:
the service wasn't created, or Render gave it a different URL (it appends a random suffix when
the name is taken - copy the real one from the top of the service's page in the Render
dashboard, and update `network-config.json` to match).

If step 2 hangs on "Connecting..." for more than a minute: check `network-config.json` uses
`wss://` and the exact Render URL (step 2.2), and that the Render service shows **Live**.
Press F12 in the browser -> **Console** for the actual error message.

### 2.7 Changing the server URL later without rebuilding
Since the server address lives in `StreamingAssets/network-config.json` inside the *hosted*
files, you can fix a wrong URL (or point at a new Render service) by editing that one file
directly in the `poe-clone-web` GitHub repo (GitHub's web UI lets you edit text files
in-browser) and pushing/committing - no Unity rebuild needed.

---

## Updating the game later
1. Make your changes in the main project and rebuild (step 2.1), into the same `Builds/WebGL`
   folder - if that folder is your clone of `poe-clone-web`, the build simply overwrites the
   files in place.
2. The server URL comes along automatically: it's baked in from
   `Assets/StreamingAssets/network-config.json`, which already points at
   `wss://poe-clone-session-server.onrender.com`.
3. Publish it from the build folder - `-A` matters, it also removes files the new build no
   longer has:
   ```
   git add -A
   git commit -m "Update build"
   git push
   ```
4. If you changed `server/`, push the main `poe-clone` repo's `master` - Render redeploys it
   automatically.

The web page around the game comes from `Assets/WebGLTemplates/FullWindow/index.html` (selected
in Player Settings -> Web -> Resolution and Presentation): it fills the browser window, has no
Unity branding, and has its own fullscreen button (top-right; F11 works too).

## How the live spectator view works
The spectator isn't watching a video. Their browser runs the same game and rebuilds the
player's world from a stream of small state snapshots:

- 10 times a second, the player's game sends a ~1KB snapshot: the player's position, facing,
  HUD values and equipped gear, the current area, whether a loading screen is up, and every
  enemy within 40 units of the player (position, facing, health, dead/alive, chasing or idle).
  Swings and hits are sent as running counters, so a swing that starts and ends between two
  snapshots is never missed. This is ~8KB/s, far less than the old JPEG slideshow.
- The spectator plays this back ~0.15-0.2s behind real time. That small buffer is what makes
  it smooth: between two snapshots, characters are **interpolated** (moved continuously
  from one known position to the next) instead of jumping 10 times a second. The buffer grows
  automatically on a jittery connection. If snapshots stop arriving, motion is extrapolated
  for up to 0.25s and then holds, with an on-screen notice after 1.5s.
- Swings, hits, deaths, area changes and loading fades replay at the exact moment the
  playback reaches them, with the game's own animations and sounds. Walking and running
  animate by themselves from the interpolated movement, so the view looks like the real game,
  at full frame rate.

Code: `Assets/Scripts/Network/PlayerStateBroadcaster.cs` (sender),
`Assets/Scripts/Network/Replication/` (wire format + playback buffer, unit tested) and
`Assets/Scripts/Network/SpectatorReplica.cs` (drives the spectator's scene).

## Testing locally (no hosting needed)
1. `cd server && npm install && npm test`, then start a local server: `PORT=8099 node server.js`
   (PowerShell: `$env:PORT=8099; node server.js`).
2. In the Unity Editor there's no browser URL, so the role/server come from a PlayerPrefs key
   instead. To play against the local server, set it once (for example from a small editor
   script or the Unity console tool of your choice):
   `PlayerPrefs.SetString("PoeClone.DevQuery", "?server=ws://localhost:8099")` - or
   `"?role=spectator&server=ws://localhost:8099"` to watch. Delete the key to go back to
   normal.
3. `node server/test/smoke-spectator.js ws://localhost:8099` connects as a spectator and prints
   the stream's rate, jitter and size while someone is playing.
4. In a WebGL build, the same overrides are just URL parameters:
   `index.html?server=ws://localhost:8099` and `index.html?role=spectator&server=ws://localhost:8099`.

## Known limitations (proof-of-concept scope)
- **One player at a time.** A second simultaneous player is explicitly out of scope for this
  version (see the project's own notes on why: real-time state sync between two players is a
  materially larger project than a session lock + spectator feed).
- **The spectator sees the area around the player, not the player's literal screen.** Things
  the replay doesn't carry (the player's open inventory/character windows, the enemy-target
  highlight under the mouse, birds and other ambient effects, which run independently in each
  browser) can differ. Everything gameplay-related (characters, fights, HUD, gear, areas,
  deaths) matches.
- **The spectator is ~0.2s behind the player.** That buffer is what makes the movement smooth.
- **If the player's tab goes to the background**, browsers pause it, so the stream pauses too.
  The spectator sees a "Waiting for the player's game..." notice until the player comes back.
- **No accounts, no persistence.** Closing the player's tab ends the session; nothing is
  saved server-side between sessions except the current in-memory chat scrollback.
- **Crash recovery takes up to ~30 seconds.** If the player's tab crashes or their network
  drops without a clean disconnect, the server notices via a heartbeat check and frees the
  slot within about 15-30 seconds, not instantly.
