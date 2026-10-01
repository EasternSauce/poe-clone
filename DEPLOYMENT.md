# Playing this in a browser with a friend

This turns the game into a link you can send a friend: whoever opens the plain link gets to
play (as long as no one else currently is), and a second link lets anyone watch live while
someone else plays. There's a shared chat box both of you can use.

It's two separate pieces you deploy separately, both on free tiers:

1. **The game itself** (a Unity WebGL export) - static files, hosted on **GitHub Pages**.
2. **The session server** (small Node.js app - the thing that enforces "only one player at a
   time" and relays the spectator feed + chat) - hosted on **Render.com**.

They're separate because GitHub Pages can only serve static files; it can't run the
always-on Node process the session lock needs. Render's free tier runs that for you.

---

## Part 1 - Deploy the session server (Render.com)

### 1.1 Create a Render account
1. Go to render.com and sign up (GitHub login is the easiest option since your project is
   probably already in a GitHub repo - see Part 2 if it isn't yet).

### 1.2 Push this repo to GitHub (if you haven't already)
Render deploys from a GitHub repo.
```
git remote add origin https://github.com/<your-username>/<your-repo>.git
git push -u origin master
```

### 1.3 Create the web service
1. In the Render dashboard: **New +** -> **Blueprint**.
2. Connect your GitHub account if prompted, and pick this repository.
3. Render will detect `server/render.yaml` and propose a service named
   `poeclone-session-server`. Click **Apply**.
   - If you'd rather set it up by hand instead of the Blueprint: **New +** -> **Web Service**,
     pick the repo, set **Root Directory** to `server`, **Build Command** to `npm install`,
     **Start Command** to `npm start`, and **Instance Type** to **Free**.
4. Wait for the first deploy to finish (a couple of minutes). Render will give the service a
   URL like `https://poeclone-session-server.onrender.com`.
5. Test it: open `https://poeclone-session-server.onrender.com/health` in a browser - it
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

### 2.1 Export the WebGL build from Unity
1. Open the project in Unity, **File -> Build Profiles** (or **Build Settings**).
2. Select **WebGL**, click **Switch Platform** if it isn't already active.
3. Click **Build**, and choose an output folder, e.g. `Builds/WebGL` (this folder is
   git-ignored on purpose - see step 2.3 for why that's fine).
4. Wait for the build - the first one can take several minutes.

The project's Player Settings are already configured for a host like GitHub Pages that can't
set custom compression headers (`Compression Format = Gzip` with `Decompression Fallback`
enabled) - you don't need to change anything there.

### 2.2 Point the build at your session server
1. In the build output folder, open `StreamingAssets/network-config.json` in a text editor.
2. Replace the placeholder with your Render URL from Part 1, **using `wss://` (not
   `https://`)**:
   ```json
   { "serverUrl": "wss://poeclone-session-server.onrender.com" }
   ```
3. Save. You can edit this file any time after deploying too, without rebuilding in Unity -
   see step 2.5.

### 2.3 Create a GitHub Pages repo for the build output
Keeping the ~50-100MB build output out of your main source repo is the norm - use a separate
repo (or a separate branch) just for the hosted files.
1. On GitHub, create a new repository, e.g. `poeclone-web`.
2. On your machine, in the WebGL build output folder (the one containing `index.html`):
   ```
   git init
   git add .
   git commit -m "WebGL build"
   git branch -M main
   git remote add origin https://github.com/<your-username>/poeclone-web.git
   git push -u origin main
   ```

### 2.4 Turn on Pages
1. In that repo on GitHub: **Settings -> Pages**.
2. Under **Build and deployment**, set **Source** to **Deploy from a branch**, branch
   `main`, folder `/ (root)`.
3. Save. GitHub gives you a URL like `https://<your-username>.github.io/poeclone-web/`.
   It can take a minute to go live.

### 2.5 Share the links
- **To play**: `https://<your-username>.github.io/poeclone-web/`
- **To spectate**: `https://<your-username>.github.io/poeclone-web/?role=spectator`

Whoever opens the plain link first gets the play slot. Anyone else who opens the plain link
while that's happening sees "Someone is already playing" and it automatically starts the
moment the first person's session ends. The spectate link always works, showing a "no one's
playing" placeholder when the game is idle and a live (every ~0.5s) view once someone starts.

### 2.6 Changing the server URL later without rebuilding
Since the server address lives in `StreamingAssets/network-config.json` inside the *hosted*
files, you can fix a wrong URL (or point at a new Render service) by editing that one file
directly in the `poeclone-web` GitHub repo (GitHub's web UI lets you edit text files
in-browser) and pushing/committing - no Unity rebuild needed.

---

## Updating the game later
1. Make your changes in the main project, rebuild WebGL (step 2.1).
2. Copy `StreamingAssets/network-config.json` from your *previous* build output into the new
   one (or re-edit it) so you don't lose your server URL.
3. Replace the contents of the `poeclone-web` repo with the new build output and push.
4. If you changed `server/`, just push to the main repo's branch Render is watching - it
   auto-redeploys.

## Known limitations (proof-of-concept scope)
- **One player at a time.** A second simultaneous player is explicitly out of scope for this
  version (see the project's own notes on why: real-time state sync between two players is a
  materially larger project than a session lock + spectator feed).
- **Spectator video is a slideshow, not a video stream.** It's a downscaled JPEG every ~0.5s,
  not real-time video - intentional for the proof-of-concept, but it means fast action reads
  as choppy for the watcher (the player's own screen is perfectly smooth).
- **No accounts, no persistence.** Closing the player's tab ends the session; nothing is
  saved server-side between sessions except the current in-memory chat scrollback.
- **Crash recovery takes up to ~30 seconds.** If the player's tab crashes or their network
  drops without a clean disconnect, the server notices via a heartbeat check and frees the
  slot within about 15-30 seconds, not instantly.
