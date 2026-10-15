# Deployment

Start with this file and the current Git status. Do not run a generic project
discovery scan (for example, searching for `AGENTS.md`, `package.json`, README,
or hosting config files) when the deployment target and workflow are already
documented here. Inspect additional files only to resolve a specific deployment
question.

The game source checkout is `EasternSauce/poe-clone`. The live GitHub Pages site
(`https://easternsauce.github.io/poe-clone-web/`) is served from the separate
`EasternSauce/poe-clone-web` repository. Pushing the source checkout alone does
not update the site. Deploy both repositories: commit and push the source/build
changes here, then copy the complete fresh `Builds/WebGL` output into a clean
checkout of `poe-clone-web`, commit it, and push that repository's `master`
branch. The only Pages checkout directory on this machine is
`C:\Users\admin\poe-clone-web`. Reuse it for every deployment; keeping it outside
the source checkout prevents its Git metadata from interfering with this repo.
Validate this exact directory first. Do not create alternate, numbered, dated,
temporary, `-deploy`, `-pages`, `-publish`, `-shallow`, `-metadata`, or `-recovery`
checkouts. If it is missing, clone into this exact path. If it exists but is
invalid, dirty, or has local-only commits, resolve the issue in this directory
while preserving local work; report a blocker if needed instead of cloning
elsewhere. A failed clone or fetch is not a reason to create another directory.

Check every native command's exit code before proceeding. PowerShell does not
stop on a failed `git`, `curl.exe`, or `robocopy` command by default: check
`$LASTEXITCODE` immediately and throw on failure (`robocopy` codes 0-7 are
successful; 8 or higher is failure). A commit or push failure must stop dependent
steps. If there is nothing to commit, confirm the intended files are already
committed before continuing. Do not report completion until both pushes and
live verification succeed.

When running in WSL, check Windows Git and its existing credential manager if
Linux Git cannot authenticate to GitHub. Windows credentials persist across
sessions even when WSL Git has no credential helper configured. Check
`git.exe` on PATH, then the standard installation at
`/mnt/c/Program Files/Git/cmd/git.exe`. From the checkout, use Windows Git
directly, for example:
```bash
'/mnt/c/Program Files/Git/cmd/git.exe' push
```
Use the same executable for authenticated fetches and clones as needed. This
uses the credentials already stored in Windows; do not copy or print tokens.
Try this fallback before reporting missing credentials as a deployment blocker.

WSL Git can also reuse Windows Credential Manager through the GitHub-scoped
helper `/home/ubuntu/.local/bin/git-credential-manager-windows` configured in
`~/.gitconfig`. The Windows helper is
`/mnt/c/Program Files/Git/mingw64/bin/git-credential-manager.exe`. Credentials
remain in Windows Credential Manager; no token copy is required. If Windows
commands fail with `UtilAcceptVsock` / `accept4`, probe existing
`/run/WSL/*_interop` sockets with a short timeout and `git.exe --version`, then
use the responsive socket as `WSL_INTEROP`. The helper handles this selection.
Preserve running WSL sessions; never restart WSL to repair a stale socket.
The credential-location memory is `~/.codex/memories/github-credentials.md`.

1. Review intended source changes. Commit them together with the fresh build in
   step 4; do not make a separate source-only push before building. If source
   changes are already committed, leave them as they are and push once after
   the build. Exclude incidental generated Unity files. Run tests only when requested.
   Record the initial working-tree status and existing build id so later
   cleanup affects only this deployment's generated changes and completion
   cannot be confused with a previous build. Preserve unrelated untracked files.

2. Every deploy must produce a fresh WebGL build from the current source. Never publish a previous build as a substitute. Use one Unity MCP `execute_code` call to check the active target, `BuildProfile.GetActiveBuildProfile()`, `EditorApplication.isPlaying`, and `EditorApplication.isCompiling`. If all checks pass, invoke `WebBuilder.BuildFromMenu()` in that same call. If preflight fails, activate the profile or wait for the editor, then retry once ready. This avoids an extra editor round trip without risking a build on the wrong profile.

   Use a normal incremental build in the same project and output directory.
   Preserve `Library/Bee` and the other Unity build caches; do not use Clean
   Build, `BuildOptions.CleanBuildCache`, or delete `Library` for routine deploys.
   A fresh build from current source can reuse unchanged cached work.
   The Web profile uses Code Optimization = Shorter Build Time and Player
   Settings > Publishing Settings > Compression Format = Gzip, with
   Decompression Fallback enabled for GitHub Pages. These reduce optimization
   and compression time at the cost of runtime performance and download size.
   IL2CPP Code Generation is already Faster (smaller) builds. For releases
   where runtime performance or size matters more than build time, explicitly
   choose Runtime Speed and/or Brotli; changing settings requires cache warmup.

3. A Unity MCP bridge timeout does not mean the build failed. Never invoke the
   build again while the original build may still be running. While waiting,
   identify the active editor log once, then check its tail and
   `Builds/WebGL/version.json` at roughly 30-second intervals if the MCP call
   times out. While the call is pending, prefer a completion-aware tool wait
   with a timeout of up to 30 seconds: it returns immediately when the build
   finishes. Do not add a fixed sleep before collecting an available result.
   On Windows the
   normal editor log is `$env:LOCALAPPDATA\Unity\Editor\Editor.log`;
   `Logs/Editor.log` may belong to an old batch run. Compare modification times
   and use the log receiving current build progress. Avoid repeated full-log
   scans and one-second polling of a pending MCP call. Keep user progress
   updates within 60 seconds during long builds. Continue only when this build has a
   `Build Web: succeeded ... (build <id>)` entry and `version.json` contains
   that same new build id. Unchanged render pipeline and renderer assets may
   be reused from cache; do not force reimports just to obtain log entries. A
   timeout or partially written/zero-length build file is not completion.
   Check the Unity console for errors after the build completes.

4. In `Builds/WebGL`, review the changed files and confirm the WebGL data and
   wasm outputs are present and non-empty. Stage the build changes in this
   source repository along with intended source changes, then commit once.
   `Builds/` is ignored but the
   release files are already tracked: use `git add -u -- Builds/WebGL` for
   updates, and explicitly force-add only intended new release files if needed.
   Do not force-add the whole ignored directory. For the Pages repository, use
   `C:\Users\admin\poe-clone-web`: verify that `origin` is
   `https://github.com/EasternSauce/poe-clone-web.git`, the branch is `master`,
   and the working tree is clean. Fetch `origin master` with depth 1, confirm
   the checkout has no local-only commits, then fast-forward it to
   `origin/master`. Only if that directory does not exist, create it with
   `git clone --depth 1 --branch master
   https://github.com/EasternSauce/poe-clone-web.git C:\Users\admin\poe-clone-web`.
   The deploy needs only the current Pages revision, so fetching the full
   repository history adds time without helping the build. Copy the contents of
   `Builds/WebGL` into the checkout (including the `Build` and `StreamingAssets`
   directories), review the diff, and commit there. Once both commits are ready,
   push both repositories' `master` branches concurrently, checking each result.
   Record the Pages push completion time independently of the source push;
   Pages can deploy while the source push finishes. Confirm both pushes succeed
   before reporting completion.
   Start Pages checkout validation and synchronization while Unity builds;
   copying and publishing must wait for the verified fresh build. Do not reset,
   clean, or overwrite a dirty checkout or one with local-only commits.

5. Verify the live version and build files:
   https://easternsauce.github.io/poe-clone-web/
   Record the time immediately after the Pages-repository push succeeds (not
   when the push starts), for example:
   ```powershell
   $pagesPushedAt = [DateTimeOffset]::UtcNow
   ```

   By default, make the first live request 30 seconds after that time. This is
   a compromise between avoiding immediate requests and noticing a fast deploy;
   it is not a known minimum deployment time. If an already available deployment
   status reports success for the exact pushed commit, verify immediately.
   Do not install or configure another tool just to save this short delay.
   Use that interval for final local cleanup and status checks;
   wait only the remaining time, in chunks of at most 30 seconds so progress
   updates remain possible. Do not make an immediate speculative version check.
   Calculate the remaining delay instead of sleeping another full interval after
   cleanup:
   ```powershell
   $remainingSeconds = [Math]::Max(0, 30 - ([DateTimeOffset]::UtcNow - $pagesPushedAt).TotalSeconds)
   ```

   GitHub Pages and its CDN may still serve old files after this delay.
   Fetch `version.json` with a changing query parameter
   (for example, with `curl.exe -fsSL
   "https://easternsauce.github.io/poe-clone-web/version.json?check=<timestamp>"`)
   to bypass caches. If it differs, wait 30 seconds before the next version
   check. Treat temporary HTTP failures as pending verification, not success.
   After five minutes from the push, use 60-second intervals. After ten minutes,
   inspect the Pages deployment status for the exact pushed commit and report
   any failure or ongoing delay; do not poll indefinitely without diagnosis.
   Once the version matches the local build id,
   fetch `index.html` once and check it with a substring match for that id.
   The index fetch and the two asset HEAD requests are independent; run them
   concurrently in one tool call and inspect every result.
   PowerShell `curl.exe` output is an array of lines, so use `-match` rather
   than `.Contains()` on the result. Use the same build-id query parameter on
   the page and asset URLs. Check the data and wasm URLs with
   `curl.exe -fsSI`; require HTTP success and `Content-Length` matching the local
   file sizes to verify those large files without downloading them again.
   Poll only `version.json` while Pages updates. Do not push again just because
   Pages is still deploying. Gameplay visual verification is not required.

   Do not add editor refresh/recompilation, screenshots, gameplay checks, or a
   second build solely for deployment after a successful build and console
   check. Feature/fix checks required by `AGENTS.md` still apply when changing
   the game. Documentation-only changes need a source commit/push, not a WebGL
   rebuild or Pages deploy unless explicitly requested.

6. Review the root repository's final status. Revert only files known to have
   been generated by the Unity build (for example,
   `Data/Plugins/lib_burst_generated.wasm`) and only if they were clean before
   the deploy. Leave unrelated user changes alone.
