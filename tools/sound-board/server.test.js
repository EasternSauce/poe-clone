const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { createServer, catalog } = require('./server');
const root = path.resolve(__dirname, '../..');
const snapshot = require('./catalog.json');
const digest = file => crypto.createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex');

test('catalog covers permanent clips, merges shared pools, and retains every usage', () => {
  const board = catalog();
  assert.equal(board.readOnly, true);
  const keys = board.effects.map(row => row.clips.map(c => c.path).sort().join('\n'));
  assert.equal(new Set(keys).size, keys.length);
  const uses = new Set(board.effects.flatMap(row => row.usages.map(use => use.id)));
  for (const entry of snapshot.effects) for (const use of entry.usages) assert.ok(uses.has(use.id), use.id);
  for (const row of board.effects) for (const clip of row.clips) {
    assert.ok(fs.existsSync(path.join(root, clip.path)), clip.path);
    assert.ok(!clip.path.includes('assets_for_inspiration'), clip.path);
  }
  for (const [id, shared] of [['skill.FireBolt', 'enemy.Fire Caster.Attack'], ['skill.IceShard', 'enemy.Frost Caster.Attack'], ['player.bow', 'enemy.Archer.Attack'], ['player.bow', 'enemy.Skeleton Archer.Attack'], ['skill.RaiseSkeletons', 'skill.SkeletonMages']]) {
    const row = board.effects.find(row => row.usages.some(use => use.id === id));
    assert.ok(row.usages.some(use => use.id === shared), shared);
  }
  assert.ok(board.effects.some(row => row.usages.some(use => use.id.startsWith('ambient.'))));
  assert.ok(board.effects.some(row => !row.usages.length));
  function walk(dir) {
    return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
      if (entry.name === 'assets_for_inspiration' || entry.name === '__MACOSX' || entry.name.startsWith('.')) return [];
      const file = path.join(dir, entry.name);
      return entry.isDirectory() ? walk(file) : /\.(wav|mp3|ogg|aiff?|flac)$/i.test(file) ? [path.relative(root, file).replaceAll('\\', '/')] : [];
    });
  }
  const clips = new Set(board.effects.flatMap(row => row.clips.map(clip => clip.path)));
  for (const file of walk(path.join(root, 'Assets'))) assert.ok(clips.has(file), file);
});

test('per-recording enemy hover includes users from overlapping pools with real portraits', () => {
  const board = catalog();
  for (const row of board.effects) for (const clip of row.clips) {
    const expected = [...new Set(board.effects.filter(other => other.clips.some(c => c.path === clip.path)).flatMap(other => other.usages.filter(use => use.enemy).map(use => use.enemy)))].sort();
    assert.deepEqual(clip.enemies.map(enemy => enemy.name).sort(), expected, clip.path);
    for (const enemy of clip.enemies) assert.ok(fs.existsSync(path.join(__dirname, enemy.portrait)), enemy.name);
  }
  const fire = board.effects.find(row => row.usages.some(use => use.id === 'skill.FireBolt'));
  assert.ok(fire.enemies.some(enemy => enemy.name === 'Fire Caster'));
  const bow = board.effects.find(row => row.usages.some(use => use.id === 'player.bow'));
  for (const name of ['Archer', 'Skeleton Archer']) assert.ok(bow.enemies.some(enemy => enemy.name === name));
});

test('HTTP plays every recording, serves portraits, rejects all writes and preserves game files', async t => {
  const files = ['Assets/Resources/SoundBoardSettings.asset', 'Assets/Resources/SoundBoardChoices.json', 'Assets/Resources/AmbientSoundLibrary.asset'];
  const before = files.map(digest);
  const server = createServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const base = `http://127.0.0.1:${server.address().port}`;
  const board = await (await fetch(base + '/api/board')).json();
  assert.equal(board.readOnly, true);
  assert.equal(board.token, undefined);
  assert.equal(board.choices, undefined);
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) for (const endpoint of ['/api/apply', '/api/board', '/audio']) {
    const response = await fetch(base + endpoint, { method, body: JSON.stringify({ effects: [] }) });
    assert.equal(response.status, 405);
  }
  for (const asset of new Set(board.effects.flatMap(row => row.clips.map(clip => clip.path)))) {
    const response = await fetch(base + '/audio?path=' + encodeURIComponent(asset), { headers: { Range: 'bytes=0-31' } });
    assert.equal(response.status, 206, asset);
    assert.equal((await response.arrayBuffer()).byteLength, 32, asset);
  }
  for (const asset of new Set(board.effects.flatMap(row => row.enemies.map(enemy => enemy.portrait)))) {
    const response = await fetch(base + '/portrait?path=' + encodeURIComponent(asset));
    assert.equal(response.status, 200, asset);
    assert.deepEqual(Buffer.from(await response.arrayBuffer()), fs.readFileSync(path.join(__dirname, asset)));
  }
  const clip = board.effects[0].clips[0].path;
  assert.equal((await fetch(base + '/audio?path=' + encodeURIComponent(clip), { headers: { Range: 'bytes=999999999999-' } })).status, 416);
  for (const endpoint of ['/audio?path=../../server/server.js', '/portrait?path=../../server/server.js', '/api/apply']) assert.equal((await fetch(base + endpoint)).status, 404);
  assert.deepEqual(files.map(digest), before);
});
