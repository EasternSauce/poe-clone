'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createLibrary, createServer, cleanName } = require('./server');
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
    'Pack A/UI/click.mp3': 'sound-three', 'Pack A/License.txt': 'Keep this license.',
    'Pack A/sword.wav.meta': 'do not copy original GUID', '__MACOSX/._fake.wav': 'ignore',
    'Pack A/._ghost.mp3': 'ignore', 'Pack A/image.png': 'ignore'
  })) {
    const target = path.join(source, file); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.writeFileSync(target, contents);
  }
  const stateFile = path.join(root, 'review/choices.json');
  return { root, source, stateFile, library: createLibrary(root, stateFile) };
}
const choices = library => ({ sounds: library.rows.map(row => ({ id: row.id, name: row.name, selected: true })) });
test('scan skips resource forks and non-audio; defaults and review choices persist', t => {
  const { root, stateFile, library } = fixture(t);
  assert.equal(library.rows.length, 3);
  assert.ok(library.load().every(row => row.selected));
  const body = choices(library); body.sounds[0].name = 'Renamed'; body.sounds[0].selected = false;
  library.save(body);
  const restarted = createLibrary(root, stateFile).load().find(row => row.id === body.sounds[0].id);
  assert.equal(restarted.name, 'Renamed'); assert.equal(restarted.selected, false);
});
test('validation rejects traversal, reserved names, missing rows and duplicate IDs', t => {
  const { library } = fixture(t);
  for (const name of ['../outside', 'bad/name', 'bad\\name', 'CON', 'nul.wav', '', 'trailing.', 'trailing ', 'a'.repeat(101)]) assert.throws(() => cleanName(name), name);
  assert.equal(cleanName('Friendly sound 01'), 'Friendly sound 01');
  assert.throws(() => library.validate({ sounds: [] }));
  const body = choices(library); body.sounds[1] = body.sounds[0]; assert.throws(() => library.validate(body));
});
test('export copies only selections, resolves collisions and survives deleting source', t => {
  const { root, source, library } = fixture(t);
  const body = choices(library);
  for (const row of body.sounds) { row.selected = row.id.includes('battle'); row.name = 'Shared name'; }
  const first = library.exportSounds(body), second = library.exportSounds(body);
  assert.equal(first.count, 2); assert.notEqual(first.directory, second.directory);
  const destination = path.join(root, first.directory);
  const manifest = JSON.parse(fs.readFileSync(path.join(destination, 'manifest.json')));
  assert.equal(new Set(manifest.sounds.map(row => row.file.toLowerCase())).size, 2);
  assert.equal(fs.readFileSync(path.join(destination, 'SourceNotes/Pack A/License.txt'), 'utf8'), 'Keep this license.');
  // The fixture source is explicitly verified before recursive deletion.
  assert.equal(path.resolve(source), path.join(root, 'Assets/assets_for_inspiration'));
  fs.rmSync(source, { recursive: true });
  assert.deepEqual(manifest.sounds.map(row => fs.readFileSync(path.join(destination, row.file), 'utf8')).sort(), ['sound-one', 'sound-two']);
  assert.ok(!manifest.sounds.some(row => row.file.endsWith('.meta')));
});
test('failed export leaves no visible partial collection', t => {
  const { root, source, library } = fixture(t);
  fs.unlinkSync(path.join(source, library.rows[1].id));
  assert.throws(() => library.exportSounds(choices(library)));
  assert.deepEqual(fs.readdirSync(path.join(root, 'Assets/SelectedSounds')), []);
});
test('HTTP page, protected writes, preview ranges, save and export', async t => {
  const options = fixture(t);
  const server = createServer(options);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const base = `http://127.0.0.1:${server.address().port}`;
  const page = await fetch(base); assert.equal(page.status, 200); assert.match(await page.text(), /Sound Library/);
  assert.equal((await fetch(base + '/app.js')).status, 200);
  const data = await (await fetch(base + '/api/library')).json();
  const body = { sounds: data.sounds.map(({ id, name, selected }) => ({ id, name, selected })) };
  const post = (endpoint, token) => fetch(base + endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Library-Token': token }, body: JSON.stringify(body) });
  assert.equal((await post('/api/save', 'wrong')).status, 403);
  assert.equal((await post('/api/save', data.token)).status, 200);
  const result = await (await post('/api/export', data.token)).json(); assert.equal(result.count, 3);
  const url = base + '/audio?id=' + encodeURIComponent(data.sounds[0].id);
  const clip = await fetch(url); const buffer = Buffer.from(await clip.arrayBuffer());
  const range = await fetch(url, { headers: { Range: 'bytes=1-3' } });
  assert.equal(range.status, 206); assert.deepEqual(Buffer.from(await range.arrayBuffer()), buffer.subarray(1, 4));
  const suffix = await fetch(url, { headers: { Range: 'bytes=-3' } }); assert.deepEqual(Buffer.from(await suffix.arrayBuffer()), buffer.subarray(-3));
  assert.equal((await fetch(url, { headers: { Range: 'bytes=999-1000' } })).status, 416);
  assert.equal((await fetch(base + '/audio?id=../../secret')).status, 404);
});
