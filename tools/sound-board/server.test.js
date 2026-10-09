const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createServer, validate } = require('./server');
const rows = require('./catalog.json').effects;
const choicesFile = path.resolve(__dirname, '../../Assets/Resources/SoundBoardChoices.json');
const defaults = () => rows.map(row => ({ id: row.id, path: '', muted: false, volume: 1 }));

test('catalog covers each effect with five real inspiration suggestions and playable defaults', () => {
  assert.equal(new Set(rows.map(row => row.id)).size, rows.length);
  for (const row of rows) {
    assert.equal(row.suggestions.length, 5, row.id);
    assert.equal(new Set(row.suggestions).size, 5, row.id);
    assert.ok(row.current.length, row.id);
    for (const file of [...row.current, ...row.suggestions]) assert.ok(fs.existsSync(path.resolve(__dirname, '../..', file)), file);
    assert.ok(row.suggestions.every(file => file.startsWith('Assets/assets_for_inspiration/')), row.id);
  }
  for (const id of ['skill.Dash', 'skill.Teleport', 'skill.FireBolt', 'skill.ChainLightning', 'enemy.Carrion Saint.Attack']) assert.ok(rows.some(row => row.id === id));
});

test('rejects incomplete, duplicate, foreign and invalid-volume choices', () => {
  const value = defaults();
  assert.throws(() => validate({ effects: value.slice(1) }, rows));
  for (const change of [{ id: value[1].id }, { path: '../../secret' }, { volume: -1 }, { volume: 2 }, { volume: NaN }, { muted: 'false' }]) {
    const invalid = defaults(); Object.assign(invalid[0], change);
    assert.throws(() => validate({ effects: invalid }, rows));
  }
});

test('every enemy sound has a real portrait and the API serves only catalogued images', async t => {
  const enemies = rows.filter(row => row.id.startsWith('enemy.'));
  for (const row of enemies) {
    assert.ok(row.portrait, row.id);
    assert.ok(fs.existsSync(path.resolve(__dirname, row.portrait)), row.portrait);
    const name = row.id.slice(6, row.id.lastIndexOf('.'));
    assert.equal(row.portrait, 'portraits/' + encodeURIComponent(name) + '.png');
  }
  const server = createServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const base = `http://127.0.0.1:${server.address().port}`;
  for (const asset of new Set(enemies.map(row => row.portrait))) {
    const response = await fetch(base + '/portrait?path=' + encodeURIComponent(asset));
    assert.equal(response.status, 200, asset);
    assert.equal(response.headers.get('content-type'), 'image/png');
    assert.deepEqual(Buffer.from(await response.arrayBuffer()), fs.readFileSync(path.resolve(__dirname, asset)));
  }
  assert.equal((await fetch(base + '/portrait?path=../../server/server.js')).status, 404);
  assert.equal((await fetch(base + '/portrait')).status, 404);
});

test('API previews ranged audio and persists chosen clips, mute and volume together', async t => {
  const original = fs.readFileSync(choicesFile);
  const server = createServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(async () => { fs.writeFileSync(choicesFile, original); await new Promise(resolve => server.close(resolve)); });
  const base = `http://127.0.0.1:${server.address().port}`;
  const board = await (await fetch(base + '/api/board')).json();
  const effects = defaults(); effects[0].path = rows[0].suggestions[2]; effects[0].volume = 0.37;
  effects[1].muted = true; effects[1].volume = 0;
  const post = token => fetch(base + '/api/apply', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Board-Token': token }, body: JSON.stringify({ effects }) });
  assert.equal((await post('wrong')).status, 403);
  assert.equal((await post(board.token)).status, 200);
  assert.deepEqual(JSON.parse(fs.readFileSync(choicesFile)).effects, effects);
  assert.deepEqual((await (await fetch(base + '/api/board')).json()).choices, effects);
  const clip = await fetch(base + '/audio?path=' + encodeURIComponent(rows[0].suggestions[0]), { headers: { Range: 'bytes=0-31' } });
  assert.equal(clip.status, 206); assert.equal((await clip.arrayBuffer()).byteLength, 32);
  assert.equal((await fetch(base + '/audio?path=../../server/server.js')).status, 404);
});
