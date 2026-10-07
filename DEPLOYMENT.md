# Deployment

Start with this file and the current Git status. Do not run a generic project
discovery scan (for example, searching for `AGENTS.md`, `package.json`, README,
or hosting config files) when the deployment target and workflow are already
documented here. Inspect additional files only to resolve a specific deployment
question.

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
   Once the fresh build is deployed and its live version and build files
   are confirmed, deployment is complete. Gameplay visual verification is
   not required.

6. Review and revert only generated Unity changes.
