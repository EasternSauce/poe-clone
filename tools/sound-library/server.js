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
function createLibrary(root = path.resolve(__dirname, '../..'), stateFile = path.join(__dirname, 'choices.json')) {
  const source = path.join(root, 'Assets/assets_for_inspiration');
  const files = walk(source);
  const rows = files.filter(file => MIME[path.extname(file).toLowerCase()]).map(file => {
    const id = slash(path.relative(source, file));
    const ext = path.extname(file).toLowerCase();
    // Drop pack names from semantic classification; preserve inner folders and filenames.
    const semantic = id.split('/').slice(1).join('/');
    return { id, pack: id.split('/')[0], folder: slash(path.dirname(id)), category: categoryFor(semantic), name: path.basename(file, path.extname(file)).slice(0, 100).replace(/[. ]+$/, ''), ext, bytes: fs.statSync(file).size };
  }).sort((a, b) => a.category.localeCompare(b.category) || a.id.localeCompare(b.id));
  const byId = new Map(rows.map(row => [row.id, row]));
  function validate(body) {
    if (!body || !Array.isArray(body.sounds) || body.sounds.length !== rows.length) throw Error('Send one choice for every sound. Reload the page if the library changed.');
    const seen = new Set();
    return { sounds: body.sounds.map(value => {
      if (!byId.has(value.id) || seen.has(value.id) || typeof value.selected !== 'boolean') throw Error('Invalid or duplicate sound selection.');
      seen.add(value.id);
      return { id: value.id, name: cleanName(value.name), selected: value.selected };
    }) };
  }
  function load() {
    const stored = fs.existsSync(stateFile) ? JSON.parse(fs.readFileSync(stateFile, 'utf8')) : { sounds: [] };
    const saved = new Map(stored.sounds.map(row => [row.id, row]));
    return rows.map(row => ({ ...row, name: saved.get(row.id)?.name ?? row.name, selected: saved.get(row.id)?.selected ?? true }));
  }
  function save(body) {
    const value = validate(body);
    fs.mkdirSync(path.dirname(stateFile), { recursive: true });
    fs.writeFileSync(stateFile + '.tmp', JSON.stringify(value, null, 2) + '\n');
    fs.renameSync(stateFile + '.tmp', stateFile);
    return value;
  }
  function exportSounds(body) {
    const selected = validate(body).sounds.filter(row => row.selected);
    if (!selected.length) throw Error('Select at least one sound to export.');
    const parent = path.join(root, 'Assets/SelectedSounds');
    fs.mkdirSync(parent, { recursive: true });
    const staging = fs.mkdtempSync(path.join(parent, '.export-'));
    const destination = path.join(parent, 'Export-' + new Date().toISOString().replace(/[:.]/g, '-') + '-' + crypto.randomBytes(3).toString('hex'));
    try {
      const used = new Set();
      const manifest = selected.map(choice => {
        const row = byId.get(choice.id);
        let relative = row.category + '/' + choice.name + row.ext;
        // Windows paths are case insensitive. Never overwrite a colliding renamed clip.
        if (used.has(relative.toLowerCase())) relative = row.category + '/' + choice.name + '-' + crypto.createHash('sha256').update(row.id).digest('hex').slice(0, 10) + row.ext;
        let suffix = 2;
        const base = relative;
        while (used.has(relative.toLowerCase())) relative = base.slice(0, -row.ext.length) + '-' + suffix++ + row.ext;
        used.add(relative.toLowerCase());
        const target = path.join(staging, relative);
        fs.mkdirSync(path.dirname(target), { recursive: true });
        fs.copyFileSync(path.join(source, row.id), target, fs.constants.COPYFILE_EXCL);
        return { source: row.id, file: relative, name: choice.name, category: row.category, pack: row.pack };
      });
      const packs = new Set(manifest.map(row => row.pack));
      for (const file of files.filter(file => /\.(txt|md|pdf|url)$/i.test(file))) {
        const relative = path.relative(source, file);
        if (!packs.has(relative.split(path.sep)[0])) continue;
        const target = path.join(staging, 'SourceNotes', relative);
        fs.mkdirSync(path.dirname(target), { recursive: true });
        fs.copyFileSync(file, target, fs.constants.COPYFILE_EXCL);
      }
      fs.writeFileSync(path.join(staging, 'manifest.json'), JSON.stringify({ exportedAt: new Date().toISOString(), sounds: manifest }, null, 2) + '\n');
      fs.writeFileSync(path.join(staging, 'README.txt'), 'Independent sound library export. Audio files are real copies, organized by category.\nmanifest.json records original sources for reference only; playback does not require them.\nSourceNotes preserves included pack documentation and licenses.\nOriginal Unity .meta files are intentionally excluded so these copies receive new GUIDs.\nThe game soundboard has not been changed. Keep the inspiration folder until game references are migrated.\n');
      fs.renameSync(staging, destination);
      return { count: manifest.length, directory: slash(path.relative(root, destination)) };
    } catch (error) {
      // Staging is created by mkdtemp directly under our verified export parent.
      if (path.dirname(path.resolve(staging)) === path.resolve(parent)) fs.rmSync(staging, { recursive: true, force: true });
      throw error;
    }
  }
  return { rows, byId, source, validate, load, save, exportSounds };
}
function createServer(options = {}) {
  const library = createLibrary(options.root, options.stateFile);
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
      if (req.method === 'GET' && url.pathname === '/api/library') return json(200, { app: 'sound-library', token, sounds: library.load() });
      if (req.method === 'GET' && url.pathname === '/audio') {
        const row = library.byId.get(url.searchParams.get('id'));
        if (!row) return json(404, { error: 'Unknown sound.' });
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
        res.writeHead(status, { 'Content-Type': MIME[row.ext], 'Content-Length': end - start + 1, 'Accept-Ranges': 'bytes' });
        const stream = fs.createReadStream(file, { start, end });
        stream.on('error', () => res.destroy());
        res.on('close', () => stream.destroy());
        return stream.pipe(res);
      }
      if (req.method === 'POST' && ['/api/save', '/api/export'].includes(url.pathname)) {
        if (req.headers['x-library-token'] !== token) return json(403, { error: 'Reload the page to reconnect.' });
        let body = '';
        for await (const chunk of req) { body += chunk; if (body.length > 4 * 1024 * 1024) throw Error('Request too large.'); }
        const value = JSON.parse(body);
        const saved = library.save(value);
        return json(200, url.pathname === '/api/export' ? library.exportSounds(saved) : { saved: true });
      }
      json(404, { error: 'Not found.' });
    } catch (error) { json(400, { error: error.message }); }
  });
}
if (require.main === module) {
  const port = Number(process.env.SOUND_LIBRARY_PORT || 8101);
  createServer().listen(port, '127.0.0.1', () => console.log(`Sound Library: http://127.0.0.1:${port}`));
}
module.exports = { createServer, createLibrary, cleanName, categoryFor };
