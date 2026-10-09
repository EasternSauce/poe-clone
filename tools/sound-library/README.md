# Sound Library

Double-click `Start-SoundLibrary.cmd` in the project root. Requires Node.js; no packages to install. Opens http://127.0.0.1:8101. Set `SOUND_LIBRARY_PORT` to use another port. Restart any older Sound Library server after updating.

The browser reads only permanent files in `Assets/Audio/SoundLibrary`. It does not need the inspiration folder or saved selection choices. Search by filename, current path, original path, or pack; filter by category or pack. Category folders are authoritative, so each file appears exactly once. Refresh library picks up external changes.

Play previews with seek and volume controls. Edit the filename without its extension and click **Rename** (or press Enter) to move the real file immediately. Its Unity `.meta` file moves with it to preserve the GUID. Existing filenames cannot be overwritten. **Delete** permanently removes the real audio file and its `.meta` immediately. The manifest updates with both actions. There is no selection, export, or Apply step. These changes remain on disk after restarting the browser; they are not committed automatically.

The one-time importer (`node tools/sound-library/import-inspiration.js`) originally copied all 1,231 source files into 1,229 unique files, merging two byte-identical duplicates. The library has since been curated to keep one format for sounds with the same filename in the same folder, preferring WAV when available. Each file has one category, chosen from inner folders and filenames. SourceNotes preserves included documentation and licenses; manifest.json records original sources for reference only. The importer refuses to overwrite an existing library, so it cannot accidentally restore deleted clips.

The game soundboard is unchanged and still needs migration before removing the inspiration directory.

Run backend checks with `node --test tools/sound-library/server.test.js`. Stop the tool's Node process to shut down the page.
