# Deployment

1. Review changes, commit intended source changes, and push `master`.
   Do not include generated Unity files. Run tests only when requested.

2. Every deploy must produce a fresh WebGL build from the current source. Never publish a previous build as a substitute. Use one Unity MCP call to verify:
   - WebGL target and `Web - Desktop - Release` profile are active.
   - Unity is neither playing nor compiling.
   Then invoke `WebBuilder.BuildFromMenu()`.
   If preflight fails, activate the profile and wait before building.

3. A bridge timeout does not mean the build failed. Do not repeat the build call.
   Check `Logs/Editor.log` and `Builds/WebGL/version.json` through
   filesystem tools. Require build success and both Mobile and PC
   render pipelines in the latest build log.

4. In `Builds/WebGL`, review changes, stage all build changes,
   commit, and push `master`.

5. Verify the live version and build files:
   https://easternsauce.github.io/poe-clone-web/
   File checks alone do not confirm gameplay rendering.

6. Review and revert only generated Unity changes.
