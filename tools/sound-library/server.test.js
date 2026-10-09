'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createLibrary, createServer, cleanName } = require('./server');
const { importInspiration } = require('./import-inspiration');
function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'poe-sound-library-'));
  t.after(() => {
    assert.equal(path.dirname(path.resolve(root)), path.resolve(os.tmpdir()));
    assert.ok(path.basename(root).startsWith('poe-sound-library-'));
    fs.rmSync(root, { recursive: true, force: true });
  });
  const source = path.join(root, 'Assets/assets_for_inspiration');
  for (const [file, contents] of Object.entries({
    'Pack A/battle/sword.wav': 'sound-one', 'Pack B/battle/sword.wav': 'sound-two',
    'Pack A/UI/click.mp3': 'sound-three', 'Pack C/world/echo.wav': 'sound-one',
    'Pack A/License.txt': 'Keep this license.', 'Pack A/sword.wav.meta': 'original GUID',
    '__MACOSX/._fake.wav': 'ignore', 'Pack A/._ghost.mp3': 'ignore', 'Pack A/image.png': 'ignore'
  })) {
    const target = path.join(source, file); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, contents);
  }
  const result = importInspiration(root), library = createLibrary(root);
  return { root, source, result, library };
}
test('import covers all sources once, deduplicates across categories and preserves documentation', t => {
  const { root, source, result, library } = fixture(t);
  assert.equal(result.sourceFiles, 4); assert.equal(result.uniqueFiles, 3);
  const manifest = JSON.parse(fs.readFileSync(path.join(library.source, 'manifest.json')));
  assert.equal(manifest.sounds.reduce((total, row) => total + row.sources.length, 0), 4);
  assert.equal(new Set(manifest.sounds.map(row => row.sha256)).size, 3);
  assert.equal(fs.readFileSync(path.join(library.source, 'SourceNotes/Pack A/License.txt'), 'utf8'), 'Keep this license.');
  assert.throws(() => importInspiration(root), /already exists/);
  fs.renameSync(source, path.join(root, 'removed-inspiration'));
  assert.equal(library.load().length, 3);
  for (const row of library.load()) assert.ok(fs.readFileSync(path.join(library.source, row.id)).length);
});
test('rename preserves metadata and provenance, including case-only rename, after restart', t => {
  const { root, library } = fixture(t);
  const row = library.load().find(row => row.category === 'Combat & weapons');
  fs.writeFileSync(path.join(library.source, row.id + '.meta'), 'guid: abc123');
  library.rename({ id: row.id, name: 'Renamed' });
  const next = createLibrary(root).load().find(row => row.name === 'Renamed');
  assert.ok(next); assert.equal(fs.existsSync(path.join(library.source, row.id)), false);
  assert.equal(fs.readFileSync(path.join(library.source, next.id + '.meta'), 'utf8'), 'guid: abc123');
  assert.deepEqual(next.sources, row.sources);
  library.rename({ id: next.id, name: 'RENAMED' });
  assert.ok(library.load().some(row => row.name === 'RENAMED'));
});
test('delete immediately removes clip, metadata and manifest entry while preserving originals', t => {
  const { root, source, library } = fixture(t);
  const row = library.load()[0]; fs.writeFileSync(path.join(library.source, row.id + '.meta'), 'guid: abc123');
  library.delete({ id: row.id });
  assert.equal(fs.existsSync(path.join(library.source, row.id)), false);
  assert.equal(fs.existsSync(path.join(library.source, row.id + '.meta')), false);
  assert.equal(createLibrary(root).load().length, 2);
  assert.ok(!JSON.parse(fs.readFileSync(path.join(library.source, 'manifest.json'))).sounds.some(item => item.file === row.id));
  assert.ok(fs.existsSync(path.join(source, row.sources[0].file)));
  assert.throws(() => library.delete({ id: row.id }));
});
test('invalid paths, reserved names, and collisions cannot mutate another asset', t => {
  const { library } = fixture(t);
  for (const name of ['../outside', 'bad/name', 'bad\\name', 'CON', 'nul.wav', '', 'trailing.', 'trailing ', 'a'.repeat(101)]) assert.throws(() => cleanName(name), name);
  for (const id of ['../../other.wav', '/absolute.wav', 'missing.wav']) assert.throws(() => library.delete({ id }));
  const rows = library.load().filter(row => row.category === 'Combat & weapons');
  assert.equal(rows.length, 2);
  assert.throws(() => library.rename({ id: rows[0].id, name: rows[1].name }));
  assert.equal(library.load().length, 3);
});
test('failed manifest write rolls back audio and GUID moves', t => {
  const { library } = fixture(t); const row = library.load()[0];
  fs.writeFileSync(path.join(library.source, row.id + '.meta'), 'guid: abc123');
  fs.mkdirSync(path.join(library.source, 'manifest.json.tmp'));
  assert.throws(() => library.rename({ id: row.id, name: 'Rollback' }));
  assert.throws(() => library.delete({ id: row.id }));
  assert.ok(fs.existsSync(path.join(library.source, row.id)));
  assert.equal(fs.readFileSync(path.join(library.source, row.id + '.meta'), 'utf8'), 'guid: abc123');
});
test('HTTP protects instant actions, previews ranges and rescans external changes', async t => {
  const options = fixture(t); const server = createServer(options);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const base = `http://127.0.0.1:${server.address().port}`;
  assert.equal((await fetch(base)).status, 200); assert.equal((await fetch(base + '/app.js')).status, 200);
  const data = await (await fetch(base + '/api/library')).json(); assert.equal(data.version, 2);
  const row = data.sounds[0]; const url = base + '/audio?id=' + encodeURIComponent(row.id);
  const clip = await fetch(url), buffer = Buffer.from(await clip.arrayBuffer());
  const range = await fetch(url, { headers: { Range: 'bytes=1-3' } });
  assert.equal(range.status, 206); assert.deepEqual(Buffer.from(await range.arrayBuffer()), buffer.subarray(1, 4));
  const suffix = await fetch(url, { headers: { Range: 'bytes=-3' } }); assert.deepEqual(Buffer.from(await suffix.arrayBuffer()), buffer.subarray(-3));
  assert.equal((await fetch(url, { headers: { Range: 'bytes=999-1000' } })).status, 416);
  assert.equal((await fetch(base + '/audio?id=../../secret')).status, 404);
  const post = (endpoint, body, token = data.token) => fetch(base + endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Library-Token': token }, body: JSON.stringify(body) });
  assert.equal((await post('/api/delete', { id: row.id }, 'wrong')).status, 403);
  const renamed = await (await post('/api/rename', { id: row.id, name: 'HTTP renamed' })).json();
  const next = renamed.sounds.find(row => row.name === 'HTTP renamed'); assert.ok(next);
  assert.equal((await fetch(url)).status, 404);
  assert.equal((await post('/api/delete', { id: next.id })).status, 200);
  fs.writeFileSync(path.join(options.library.source, 'Other.wav'), 'external');
  const refreshed = await (await fetch(base + '/api/library')).json(); assert.equal(refreshed.sounds.length, 3);
  assert.equal((await post('/api/export', {})).status, 404);
});
