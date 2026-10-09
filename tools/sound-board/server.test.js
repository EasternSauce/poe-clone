const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { createServer, catalog } = require('./server');
const root = path.resolve(__dirname, '../..');
const snapshot = require('./catalog.json');
const digest = file => crypto.createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex');

test('catalog contains only assigned clips, exposes runtime groups, and retains every usage', () => {
  const board = catalog();
  assert.equal(board.readOnly, true);
  const keys = board.effects.map(row => row.id);
  assert.equal(new Set(keys).size, keys.length);
  const uses = new Set(board.effects.flatMap(row => row.usages.map(use => use.id)));
  for (const entry of snapshot.effects) for (const use of entry.usages) assert.ok(uses.has(use.id), use.id);
  for (const entry of snapshot.effects.filter(entry => entry.soundGroup)) {
    const group = board.effects.find(row => row.id === entry.soundGroup);
    assert.equal(group.purpose, entry.purpose);
    assert.deepEqual(group.clips.map(clip => clip.path).sort(), [...entry.current].sort());
  }
  for (const row of board.effects) for (const clip of row.clips) {
    assert.ok(fs.existsSync(path.join(root, clip.path)), clip.path);
    assert.ok(!clip.path.includes('assets_for_inspiration'), clip.path);
  }
  for (const [id, shared] of [['skill.FireBolt', 'enemy.Fire Caster.Attack'], ['skill.IceShard', 'enemy.Frost Caster.Attack'], ['player.bow', 'enemy.Archer.Attack'], ['player.bow', 'enemy.Skeleton Archer.Attack'], ['skill.RaiseSkeletons', 'skill.SkeletonMages']]) {
    const row = board.effects.find(row => row.usages.some(use => use.id === id));
    assert.ok(row.usages.some(use => use.id === shared), shared);
  }
  assert.ok(board.effects.some(row => row.usages.some(use => use.id.startsWith('ambient.'))));
  assert.ok(board.effects.every(row => row.usages.length > 0));
  const expected = new Set(snapshot.effects.filter(row => row.usages.length).flatMap(row => row.current));
  const clips = new Set(board.effects.flatMap(row => row.clips.map(clip => clip.path)));
  assert.deepEqual([...clips].sort(), [...expected].sort());
});

test('old catalog entries with no gameplay usages are omitted', () => {
  const { groupEntries } = require('./groups');
  const assigned = snapshot.effects.find(entry => entry.usages.length);
  const unused = { id: 'clip:unused', label: 'Unused recording', group: 'Unassigned recordings', current: ['Assets/Audio/unused.wav'], usages: [] };
  const rows = groupEntries([assigned, unused]);
  assert.equal(rows.length, 1);
  assert.ok(rows.every(row => !row.current.includes('Assets/Audio/unused.wav')));
});

test('per-recording enemy hover includes users from overlapping pools with real portraits', () => {
  const board = catalog();
  for (const row of board.effects) for (const clip of row.clips) {
    const expected = [...new Set(board.effects.flatMap(other => other.usages.filter(use => use.enemy && use.recordings.includes(clip.path)).map(use => use.enemy)))].sort();
    assert.deepEqual(clip.enemies.map(enemy => enemy.name).sort(), expected, clip.path);
    for (const enemy of clip.enemies) assert.ok(fs.existsSync(path.join(__dirname, enemy.portrait)), enemy.name);
  }
  const fire = board.effects.find(row => row.usages.some(use => use.id === 'skill.FireBolt'));
  assert.ok(fire.enemies.some(enemy => enemy.name === 'Fire Caster'));
  const bow = board.effects.find(row => row.usages.some(use => use.id === 'player.bow'));
  for (const name of ['Archer', 'Skeleton Archer']) assert.ok(bow.enemies.some(enemy => enemy.name === name));
});

test('purpose-specific runtime pools stay separate and real alternate takes are assigned together', () => {
  const board = catalog();
  const assignment = id => board.effects.find(row => row.usages.some(use => use.id === id));
  assert.notEqual(assignment('combat.ground.Cold').id, assignment('world.shatter').id);
  assert.notEqual(assignment('combat.ground.Lightning').id, assignment('skill.ChainLightning').id);
  assert.notEqual(assignment('player.hurt').id, assignment('enemy.Archer.Death').id);
  assert.deepEqual(assignment('player.hurt').enemies, []);
  assert.equal(assignment('skill.ChainLightning').id, assignment('enemy.Storm Caster.Attack').id);
  for (const [id, count] of [['enemy.Zombie.Attack', 4], ['enemy.Skeleton.Attack', 4], ['enemy.Raider.Attack', 3], ['enemy.Forest Shaman.Attack', 4]])
    assert.equal(assignment(id).clips.length, count, id);
  const aggro = assignment('enemy.Raider.Aggro');
  assert.equal(aggro.usages.find(use => use.id === 'enemy.Raider.Aggro').muted, true);
  assert.equal(aggro.usages.find(use => use.id === 'enemy.Zombie.Aggro').muted, false);
  // A numbered generic skill pack is not automatically a pool of interchangeable sounds.
  assert.notEqual(assignment('skill.GraveRot').id, assignment('skill.VenomArrow').id);

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
