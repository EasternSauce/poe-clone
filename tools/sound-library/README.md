# Sound Library

Double-click `Start-SoundLibrary.cmd` in the project root. Requires Node.js; no packages to install. The local page runs at http://127.0.0.1:8101. Set `SOUND_LIBRARY_PORT` to use another port.

- Scans every WAV, MP3, OGG, AIFF and FLAC in `Assets/assets_for_inspiration` on server startup. Skips hidden files, macOS resource forks, and symbolic links. Restart the tool after adding source files.
- Every sound starts selected. Search by name, source path, or pack; filter by inferred sound category, pack, and selection. Categories are inferred from inner folders and filenames, so ambiguous sounds can appear under Other.
- Play previews with seek and volume controls. Only one sound plays at a time.
- Edit filenames without their extension. Names and selections save to local `tools/sound-library/choices.json`, across refreshes and restarts. New sounds default to selected. Select/Deselect filtered applies to **all matching sounds across pages**.
- Export selected sounds creates a new `Assets/SelectedSounds/Export-<timestamp>-<id>` folder each time. It copies the audio into category folders, preserves included pack documentation in SourceNotes, and writes a source-to-export manifest. Duplicate names receive suffixes. Original clips and existing exports stay untouched. Unity generates fresh GUIDs for the exported copies.

Exports are self-contained and do not need the inspiration directory. The existing game and soundboard references are unchanged; migrate those before deleting the inspiration folder. Exporting does not commit the resulting collection automatically.

Run backend checks with `node --test tools/sound-library/server.test.js`. Stop the tool's Node process to shut down the local page.
