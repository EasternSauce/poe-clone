'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const mime = { '.wav': 'audio/wav', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.aif': 'audio/aiff', '.aiff': 'audio/aiff', '.flac': 'audio/flac' };

function catalog() {
  const snapshot = JSON.parse(fs.readFileSync(path.join(__dirname, 'catalog.json'), 'utf8'));
  const pools = new Map();
  for (const entry of snapshot.effects) {
    const key = entry.current.length ? [...entry.current].sort().join('\n') : entry.id;
    let row = pools.get(key);
    if (!row) {
      row = { id: entry.id, label: entry.label, group: entry.group, current: entry.current, usages: [], labels: [] };
      pools.set(key, row);
    }
    row.labels.push(entry.label);
    row.usages.push(...entry.usages);
  }
  const effects = [...pools.values()];
  // A recording may be in several different variation pools. Its hover includes every user.
  const enemiesByClip = new Map();
  for (const row of effects) for (const clip of row.current) {
    if (!enemiesByClip.has(clip)) enemiesByClip.set(clip, new Map());
    for (const use of row.usages) if (use.enemy) enemiesByClip.get(clip).set(use.enemy, { name: use.enemy, portrait: use.portrait });
  }
  for (const row of effects) {
    row.usages = [...new Map(row.usages.map(use => [use.id, use])).values()];
    row.clips = row.current.map(asset => ({ path: asset, enemies: [...enemiesByClip.get(asset).values()].sort((a, b) => a.name.localeCompare(b.name)) }));
    row.enemies = [...new Map(row.clips.flatMap(clip => clip.enemies).map(enemy => [enemy.name, enemy])).values()].sort((a, b) => a.name.localeCompare(b.name));
    if (row.labels.length > 1) {
      row.label = row.current.length ? path.basename(row.current[0], path.extname(row.current[0])).replaceAll('_', ' ') : row.label;
      if (row.current.length > 1) row.label += ` (${row.current.length} variations)`;
      if (new Set(row.usages.map(use => use.enemy ? 'Enemies' : use.id.split('.')[0])).size > 1) row.group = 'Shared effects';
    }
    delete row.current;
  }
  effects.sort((a, b) => Number(!a.usages.length) - Number(!b.usages.length) || a.group.localeCompare(b.group) || a.label.localeCompare(b.label));
  return { readOnly: true, generatedAt: snapshot.generatedAt, effects };
}
function createServer() {
  return http.createServer((req, res) => {
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    const json = (status, data) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
    try {
      // No mutation routes: reject writes before even reading the request body.
      if (req.method !== 'GET' && req.method !== 'HEAD') {
        res.setHeader('Allow', 'GET, HEAD');
        return json(405, { error: 'The sound board is read only.' });
      }
      const url = new URL(req.url, 'http://localhost');
      if (url.pathname === '/' || url.pathname === '/app.js') {
        const file = url.pathname === '/' ? 'index.html' : 'app.js';
        res.writeHead(200, { 'Content-Type': file.endsWith('.js') ? 'text/javascript; charset=utf-8' : 'text/html; charset=utf-8' });
        return res.end(req.method === 'HEAD' ? undefined : fs.readFileSync(path.join(__dirname, file)));
      }
      if (url.pathname === '/api/board') return json(200, catalog());
      const board = catalog();
      const asset = url.searchParams.get('path');
      if (url.pathname === '/portrait') {
        if (!board.effects.some(row => row.enemies.some(enemy => enemy.portrait === asset))) return json(404, { error: 'Unknown portrait.' });
        const file = path.resolve(__dirname, asset);
        if (!file.startsWith(path.join(__dirname, 'portraits') + path.sep) || path.extname(file) !== '.png') return json(400, { error: 'Invalid portrait.' });
        res.writeHead(200, { 'Content-Type': 'image/png' });
        return res.end(req.method === 'HEAD' ? undefined : fs.readFileSync(file));
      }
      if (url.pathname === '/audio') {
        if (!board.effects.some(row => row.clips.some(clip => clip.path === asset))) return json(404, { error: 'Unknown clip.' });
        const file = path.resolve(root, asset);
        if (!file.startsWith(root + path.sep) || !mime[path.extname(file).toLowerCase()]) return json(400, { error: 'Invalid clip.' });
        const stat = fs.statSync(file);
        let start = 0, end = stat.size - 1;
        if (req.headers.range) {
          const match = /^bytes=(\d+)-(\d*)$/.exec(req.headers.range);
          if (!match) { res.writeHead(416, { 'Content-Range': `bytes */${stat.size}` }); return res.end(); }
          start = Number(match[1]); end = match[2] ? Math.min(Number(match[2]), end) : end;
          if (start > end || start >= stat.size) { res.writeHead(416, { 'Content-Range': `bytes */${stat.size}` }); return res.end(); }
          res.setHeader('Content-Range', `bytes ${start}-${end}/${stat.size}`);
        }
        res.writeHead(req.headers.range ? 206 : 200, { 'Content-Type': mime[path.extname(file).toLowerCase()], 'Content-Length': end - start + 1, 'Accept-Ranges': 'bytes' });
        if (req.method === 'HEAD') return res.end();
        const stream = fs.createReadStream(file, { start, end });
        stream.on('error', () => res.destroy());
        return stream.pipe(res);
      }
      json(404, { error: 'Not found.' });
    } catch (error) { json(400, { error: error.message }); }
  });
}
if (require.main === module) {
  const port = Number(process.env.SOUND_BOARD_PORT || 8100);
  const server = createServer();
  server.on('error', error => { console.error(`Sound board could not start: ${error.message}`); process.exitCode = 1; });
  server.listen(port, '127.0.0.1', () => console.log(`Read-only sound board: http://127.0.0.1:${port}`));
}
module.exports = { createServer, catalog };
