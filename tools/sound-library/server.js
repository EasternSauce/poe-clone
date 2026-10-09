'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const MIME = { '.wav': 'audio/wav', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.aif': 'audio/aiff', '.aiff': 'audio/aiff', '.flac': 'audio/flac' };
const slash = value => value.split(path.sep).join('/');
function walk(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    if (entry.name.startsWith('.') || entry.name === '__MACOSX' || entry.isSymbolicLink()) return [];
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(file) : entry.isFile() ? [file] : [];
  });
}
function categoryFor(file) {
  const text = file.toLowerCase().replace(/[_/\\-]+/g, ' ');
  for (const [category, expression] of [
    ['Creatures & voices', /monster|creature|enemy|\bnpc\b|beast|ogre|slime|goblin|orc|zombie|skeleton|growl|roar|grunt|voice|beetle|giant|shade|gutteral/],
    ['Interface', /interface|\bui\b|button|menu|click|hover|confirm|cancel|notification|achievement|level up|quest/],
    ['Combat & weapons', /dagger|blade|warrior/],
    ['Magic & spells', /magic|spell|skill|buff|blessing|curse|heal|potion|fireball|teleport|portal|lightning|ice|mana/],
    ['Combat & weapons', /battle|combat|sword|axe|bow|arrow|weapon|punch|hit|impact|attack|swing|slash|shield|block|damage|explosion/],
    ['Inventory & loot', /inventory|coin|gold|loot|pickup|pick up|equip|item|bag|chest|gem|book|scroll|belt|cloth/],
    ['Movement', /footstep|step|walk|run|jump|land|dash/],
    ['World & ambience', /world|ambien|wind|rain|water|river|fire|cave|forest|door|bridge|creak|stone|wood|metal|lever/],
    ['Music', /music|theme|soundtrack/]
  ]) if (expression.test(text)) return category;
  return 'Other';
}
function cleanName(value) {
  if (typeof value !== 'string' || !value.trim() || value.length > 100 || /[<>:"/\\|?*\x00-\x1f]/.test(value) || /[. ]$/.test(value) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(value) || value === '.' || value === '..') {
    throw Error('Names must be 1–100 characters without Windows filename characters, reserved names, or a trailing dot/space.');
  }
  return value;
}
function createLibrary(root = path.resolve(__dirname, '../..')) {
  const source = path.join(root, 'Assets/Audio');
  const imported = path.join(source, 'SoundLibrary');
  const manifestFile = path.join(imported, 'manifest.json');
  function manifest() { return fs.existsSync(manifestFile) ? JSON.parse(fs.readFileSync(manifestFile, 'utf8')) : { sounds: [] }; }
  function load() {
    if (fs.existsSync(source) && fs.lstatSync(source).isSymbolicLink()) throw Error('The library directory must not be a symbolic link.');
    const origins = new Map(manifest().sounds.map(row => ['SoundLibrary/' + row.file, row.sources]));
    return walk(source).filter(file => MIME[path.extname(file).toLowerCase()]).map(file => {
      const id = slash(path.relative(source, file));
      const sources = origins.get(id) || [];
      const packs = [...new Set(sources.map(row => row.pack))];
      const category = id.startsWith('SoundLibrary/') ? id.split('/')[1] : categoryFor(id);
      return { id, packs: packs.length ? packs : ['Local files'], sources, category, name: path.basename(file, path.extname(file)), ext: path.extname(file), bytes: fs.statSync(file).size };
    }).sort((a, b) => a.category.localeCompare(b.category) || a.id.localeCompare(b.id));
  }
  function find(id) {
    const row = load().find(row => row.id === id);
    if (!row) throw Error('This file no longer exists. Refresh the library.');
    const relative = path.relative(fs.realpathSync(source), fs.realpathSync(path.join(source, row.id)));
    if (relative.startsWith('..') || path.isAbsolute(relative)) throw Error('File is outside the library.');
    return row;
  }
  function mutate(body, deleting = false) {
    const row = find(body?.id);
    const oldFile = path.join(source, row.id);
    const newId = deleting ? null : slash(path.join(path.dirname(row.id), cleanName(body.name) + row.ext));
    if (newId === row.id) return { sounds: load(), message: 'Filename unchanged.' };
    const newFile = newId && path.join(source, newId);
    if (newId && load().some(other => other.id.toLowerCase() === newId.toLowerCase() && other.id !== row.id)) throw Error('A sound with that name already exists in this category.');
    if (newFile && newId.toLowerCase() !== row.id.toLowerCase() && (fs.existsSync(newFile) || fs.existsSync(newFile + '.meta'))) throw Error('The destination filename or its Unity metadata already exists.');
    const hasMeta = fs.existsSync(oldFile + '.meta');
    if (hasMeta && fs.lstatSync(oldFile + '.meta').isSymbolicLink()) throw Error('Unity metadata must not be a symbolic link.');
    const data = manifest();
    const importedId = row.id.startsWith('SoundLibrary/') ? row.id.slice('SoundLibrary/'.length) : null;
    const updateManifest = importedId !== null && fs.existsSync(manifestFile);
    if (updateManifest) data.sounds = deleting ? data.sounds.filter(item => item.file !== importedId) : data.sounds.map(item => item.file === importedId ? { ...item, file: newId.slice('SoundLibrary/'.length) } : item);
    const staged = path.join(source, '.operation-' + crypto.randomUUID());
    const moves = [];
    const move = (from, to) => { fs.renameSync(from, to); moves.push([from, to]); };
    try {
      // Keep the audio and its GUID together, including case-only renames on Windows.
      move(oldFile, staged);
      if (hasMeta) move(oldFile + '.meta', staged + '.meta');
      if (!deleting) {
        if (hasMeta) move(staged + '.meta', newFile + '.meta');
        move(staged, newFile);
      }
      if (updateManifest) {
        fs.writeFileSync(manifestFile + '.tmp', JSON.stringify(data, null, 2) + '\n');
        fs.renameSync(manifestFile + '.tmp', manifestFile);
      }
    } catch (error) {
      for (const [from, to] of moves.reverse()) fs.renameSync(to, from);
      throw error;
    }
    if (deleting) { fs.unlinkSync(staged); if (hasMeta) fs.unlinkSync(staged + '.meta'); }
    return { sounds: load(), message: deleting ? 'Deleted ' + row.name + row.ext : 'Renamed to ' + body.name + row.ext };
  }
  return { source, load, find, rename: body => mutate(body), delete: body => mutate(body, true) };
}
function createServer(options = {}) {
  const library = createLibrary(options.root);
  const token = crypto.randomBytes(24).toString('hex');
  return http.createServer(async (req, res) => {
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; media-src 'self'; frame-ancestors 'none'");
    const json = (status, data) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
    try {
      const url = new URL(req.url, 'http://localhost');
      if (req.method === 'GET' && ['/', '/app.js'].includes(url.pathname)) {
        res.writeHead(200, { 'Content-Type': url.pathname === '/' ? 'text/html; charset=utf-8' : 'text/javascript; charset=utf-8' });
        return res.end(fs.readFileSync(path.join(__dirname, url.pathname === '/' ? 'index.html' : 'app.js')));
      }
      if (req.method === 'GET' && url.pathname === '/api/library') return json(200, { app: 'sound-library', version: 3, token, directory: 'Assets/Audio', sounds: library.load() });
      if (req.method === 'GET' && url.pathname === '/audio') {
        let row;
        try { row = library.find(url.searchParams.get('id')); } catch { return json(404, { error: 'Unknown sound.' }); }
        const file = path.join(library.source, row.id);
        const size = fs.statSync(file).size;
        let start = 0, end = size - 1, status = 200;
        if (req.headers.range) {
          const match = /^bytes=(\d*)-(\d*)$/.exec(req.headers.range);
          if (!match || (!match[1] && !match[2])) { res.writeHead(416, { 'Content-Range': `bytes */${size}` }); return res.end(); }
          start = match[1] ? Number(match[1]) : Math.max(0, size - Number(match[2]));
          end = match[1] && match[2] ? Math.min(Number(match[2]), size - 1) : size - 1;
          if (start > end || start >= size) { res.writeHead(416, { 'Content-Range': `bytes */${size}` }); return res.end(); }
          status = 206;
          res.setHeader('Content-Range', `bytes ${start}-${end}/${size}`);
        }
        res.writeHead(status, { 'Content-Type': MIME[row.ext.toLowerCase()], 'Content-Length': end - start + 1, 'Accept-Ranges': 'bytes' });
        const stream = fs.createReadStream(file, { start, end });
        stream.on('error', () => res.destroy());
        res.on('close', () => stream.destroy());
        return stream.pipe(res);
      }
      if (req.method === 'POST' && ['/api/rename', '/api/delete'].includes(url.pathname)) {
        if (req.headers['x-library-token'] !== token) return json(403, { error: 'Reload the page to reconnect.' });
        let body = '';
        for await (const chunk of req) { body += chunk; if (body.length > 4 * 1024 * 1024) throw Error('Request too large.'); }
        const value = JSON.parse(body);
        return json(200, url.pathname === '/api/rename' ? library.rename(value) : library.delete(value));
      }
      json(404, { error: 'Not found.' });
    } catch (error) { json(400, { error: error.message }); }
  });
}
if (require.main === module) {
  const port = Number(process.env.SOUND_LIBRARY_PORT || 8101);
  createServer().listen(port, '127.0.0.1', () => console.log(`Sound Library: http://127.0.0.1:${port}`));
}
module.exports = { createServer, createLibrary, cleanName, categoryFor, walk, MIME, slash };
