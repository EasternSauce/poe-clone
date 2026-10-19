Permanent categorized sound library. Each unique file is stored in exactly one category.
Byte-identical duplicates share one copy. Same-name sounds in each folder keep one format, preferring WAV.
manifest.json records provenance only; no source file is needed for playback.
SourceNotes preserves included pack documentation. Browser Rename/Delete changes these files immediately.
Gameplay and the library share permanent recordings; manifest paths are relative to this directory, including ../Resources and ../SFX entries.
Assigned recordings stay in these named category folders; identical filenames can be renamed instead of creating GUID folders.
Gameplay sound effects and ambience preload and decode synchronously before the first scene. Music uses its separate loading system.
