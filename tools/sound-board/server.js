'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const choicesFile = path.join(root, 'Assets/Resources/SoundBoardChoices.json');
const token = crypto.randomBytes(24).toString('hex');
const mime = { '.wav': 'audio/wav', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg' };
function catalog() { return JSON.parse(fs.readFileSync(path.join(__dirname, 'catalog.json'), 'utf8')); }
function choices() { return fs.existsSync(choicesFile) ? JSON.parse(fs.readFileSync(choicesFile, 'utf8')) : { effects: [] }; }
function validate(body, rows) {
  if (!body || !Array.isArray(body.effects) || body.effects.length !== rows.length) throw Error('Send one choice for every effect.');
  const seen = new Set();
  return { effects: body.effects.map(value => {
    const row = rows.find(row => row.id === value.id);
    if (!row || seen.has(value.id)) throw Error('Unknown or duplicate effect.');
    seen.add(value.id);
    if (typeof value.volume !== 'number' || !Number.isFinite(value.volume) || value.volume < 0 || value.volume > 1) throw Error('Volume must be between 0 and 1.');
    if (typeof value.muted !== 'boolean') throw Error('Invalid mute choice.');
    if (value.path !== '' && !row.suggestions.includes(value.path)) throw Error('Select a suggestion or the current sound.');
    return { id: value.id, path: value.path, muted: value.muted, volume: value.volume };
  }) };
}
function createServer() {
  return http.createServer(async (req, res) => {
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    const json = (status, data) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data)); };
    try {
      const url = new URL(req.url, 'http://localhost');
      if (req.method === 'GET' && url.pathname === '/') {
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        return res.end(fs.readFileSync(path.join(__dirname, 'index.html')));
      }
      if (req.method === 'GET' && url.pathname === '/api/board') return json(200, { ...catalog(), choices: choices().effects, token });
      if (req.method === 'POST' && url.pathname === '/api/apply') {
        if (req.headers['x-board-token'] !== token) return json(403, { error: 'Reload the board before applying.' });
        let body = '';
        for await (const chunk of req) { body += chunk; if (body.length > 1000000) throw Error('Request too large.'); }
        const validated = validate(JSON.parse(body), catalog().effects);
        const temp = choicesFile + '.tmp';
        fs.writeFileSync(temp, JSON.stringify(validated, null, 2) + '\n');
        fs.renameSync(temp, choicesFile);
        return json(200, { message: 'Saved to the Unity project. Unity imports the choices when focused, and before every build.' });
      }
      if (req.method === 'GET' && url.pathname === '/audio') {
        const asset = url.searchParams.get('path');
        const rows = catalog().effects;
        if (!rows.some(row => [...row.current, ...row.suggestions].includes(asset))) return json(404, { error: 'Unknown clip.' });
        const file = path.resolve(root, asset);
        if (!file.startsWith(root + path.sep) || !mime[path.extname(file).toLowerCase()]) return json(400, { error: 'Invalid clip.' });
        const stat = fs.statSync(file);
        let start = 0, end = stat.size - 1;
        const range = req.headers.range;
        if (range) {
          const match = /^bytes=(\d+)-(\d*)$/.exec(range);
          if (!match) { res.writeHead(416); return res.end(); }
          start = Number(match[1]); end = match[2] ? Math.min(Number(match[2]), end) : end;
          if (start > end || start >= stat.size) { res.writeHead(416, { 'Content-Range': `bytes */${stat.size}` }); return res.end(); }
          res.setHeader('Content-Range', `bytes ${start}-${end}/${stat.size}`);
        }
        res.writeHead(range ? 206 : 200, { 'Content-Type': mime[path.extname(file).toLowerCase()], 'Content-Length': end - start + 1, 'Accept-Ranges': 'bytes' });
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
  server.listen(port, '127.0.0.1', () => console.log(`Sound board: http://127.0.0.1:${port}`));
}
module.exports = { createServer, validate };
