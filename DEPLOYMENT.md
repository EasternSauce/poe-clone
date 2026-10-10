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
branch. Keep the second checkout outside the source checkout so its Git metadata
does not interfere with this repository. Use a stable directory outside the
source checkout so it can be reused between deploys.

1. Review changes, commit intended source changes, and push `master`.
   Do not include generated Unity files. Run tests only when requested.

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
   check the end of `Logs/Editor.log` and `Builds/WebGL/version.json`; avoid
   repeated full-log scans. Continue only when the latest build has a
   `Build Web: succeeded ... (build <id>)` entry and `version.json` contains
   that same new build id. Unchanged render pipeline and renderer assets may
   be reused from cache; do not force reimports just to obtain log entries. A
   timeout or partially written/zero-length build file is not completion.

4. In `Builds/WebGL`, review the changed files and confirm the WebGL data and
   wasm outputs are present and non-empty. Stage the build changes in this
   source repository, commit, and push `master`. For the Pages repository, reuse
   its stable checkout when available: verify that `origin` is
   `https://github.com/EasternSauce/poe-clone-web.git`, the branch is `master`,
   and the working tree is clean. Fetch `origin master` with depth 1, confirm
   the checkout has no local-only commits, then fast-forward it to
   `origin/master`. If no valid checkout exists, create one with
   `git clone --depth 1 --branch master
   https://github.com/EasternSauce/poe-clone-web.git <deploy-directory>`.
   The deploy needs only the current Pages revision, so fetching the full
   repository history adds time without helping the build. Copy the contents of
   `Builds/WebGL` into the checkout (including the `Build` and `StreamingAssets`
   directories), review the diff, and commit and push `master` there. Confirm
   both pushes succeeded before checking Pages.

5. Verify the live version and build files:
   https://easternsauce.github.io/poe-clone-web/
   GitHub Pages and its CDN may still serve cached old files just after the
   Pages-repository push. Fetch `version.json` with a changing query parameter
   (for example, with `curl.exe -fsSL
   "https://easternsauce.github.io/poe-clone-web/version.json?check=<timestamp>"`)
   to bypass caches and keep checking until it matches the local build id, then
   fetch `index.html` once and check it with a substring match for that id.
   PowerShell `curl.exe` output is an array of lines, so use `-match` rather
   than `.Contains()` on the result. Check the data and wasm URLs with
   `curl.exe -I`; HTTP success and nonzero `Content-Length` verify those large
   files without downloading them again. Poll `version.json` at a modest
   interval while Pages updates instead of repeatedly fetching the other
   assets. Do not push again just because Pages is still deploying. Gameplay
   visual verification is not required.

6. Review the root repository's final status. Revert only files known to have
   been generated by the Unity build (for example,
   `Data/Plugins/lib_burst_generated.wasm`) and only if they were clean before
   the deploy. Leave unrelated user changes alone.
