'use strict';
// One-time import. The browser never reads the inspiration directory.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { walk, MIME, slash, categoryFor, cleanName } = require('./server');
function importInspiration(root = path.resolve(__dirname, '../..')) {
  const source = path.join(root, 'Assets/assets_for_inspiration');
  const destination = path.join(root, 'Assets/Audio/SoundLibrary');
  if (fs.existsSync(destination)) throw Error('SoundLibrary already exists. Import refuses to overwrite edits or restore deleted sounds.');
  const files = walk(source).sort();
  const audio = files.filter(file => MIME[path.extname(file).toLowerCase()]);
  if (!audio.length) throw Error('No inspiration audio found.');
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  const staging = fs.mkdtempSync(path.join(path.dirname(destination), '.sound-import-'));
  const byHash = new Map(), used = new Set(), sounds = [];
  try {
    for (const file of audio) {
      const relative = slash(path.relative(source, file));
      const origin = { file: relative, pack: relative.split('/')[0] };
      const sha256 = crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
      if (byHash.has(sha256)) { byHash.get(sha256).sources.push(origin); continue; }
      const category = categoryFor(relative.split('/').slice(1).join('/'));
      const ext = path.extname(file).toLowerCase();
      const name = cleanName(path.basename(file, path.extname(file)).slice(0, 100).replace(/[. ]+$/, ''));
      let target = category + '/' + name + ext;
      if (used.has(target.toLowerCase())) target = category + '/' + name + '-' + sha256.slice(0, 12) + ext;
      let suffix = 2; const base = target;
      while (used.has(target.toLowerCase())) target = base.slice(0, -ext.length) + '-' + suffix++ + ext;
      used.add(target.toLowerCase());
      const output = path.join(staging, target);
      fs.mkdirSync(path.dirname(output), { recursive: true });
      fs.copyFileSync(file, output, fs.constants.COPYFILE_EXCL);
      const row = { file: target, sha256, sources: [origin] };
      sounds.push(row); byHash.set(sha256, row);
    }
    for (const file of files.filter(file => /\.(txt|md|pdf|url)$/i.test(file))) {
      const output = path.join(staging, 'SourceNotes', path.relative(source, file));
      fs.mkdirSync(path.dirname(output), { recursive: true });
      fs.copyFileSync(file, output, fs.constants.COPYFILE_EXCL);
    }
    fs.writeFileSync(path.join(staging, 'manifest.json'), JSON.stringify({ version: 1, importedAt: new Date().toISOString(), sourceFiles: audio.length, sounds }, null, 2) + '\n');
    fs.writeFileSync(path.join(staging, 'README.txt'), 'Permanent categorized sound library. Each unique file is stored in exactly one category.\nByte-identical duplicates share one copy; distinct encodings are preserved.\nmanifest.json records provenance only; no source file is needed for playback.\nSourceNotes preserves included pack documentation. Browser Rename/Delete changes these files immediately.\nThe existing soundboard still needs migration before the inspiration directory is removed.\n');
    fs.renameSync(staging, destination);
    return { sourceFiles: audio.length, uniqueFiles: sounds.length, duplicates: audio.length - sounds.length, directory: slash(path.relative(root, destination)) };
  } catch (error) {
    if (path.dirname(path.resolve(staging)) === path.resolve(path.dirname(destination))) fs.rmSync(staging, { recursive: true, force: true });
    throw error;
  }
}
if (require.main === module) console.log(JSON.stringify(importInspiration(), null, 2));
module.exports = { importInspiration };
