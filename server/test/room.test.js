'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { Room, MAX_PLAYERS, MAX_CHAT_HISTORY, MAX_CHAT_MESSAGE_LENGTH, MAX_STATE_BYTES, MAX_SPECTATOR_BACKLOG_BYTES } = require('../room');

function fakeClient() {
  const sent = [];
  return {
    sent,
    send(json) {
      sent.push(JSON.parse(json));
    },
  };
}

test('first player join is granted', () => {
  const room = new Room();
  const p1 = fakeClient();
  const result = room.join(p1, 'player');
  assert.equal(result.granted, true);
  assert.equal(room.playerActive, true);
});

test('up to MAX_PLAYERS players play at once; the next one is told the game is full', () => {
  const room = new Room();
  for (let i = 0; i < MAX_PLAYERS; i++) {
    assert.equal(room.join(fakeClient(), 'player').granted, true);
  }
  assert.equal(room.playerCount, MAX_PLAYERS);
  const result = room.join(fakeClient(), 'player');
  assert.equal(result.granted, false);
  assert.match(result.reason, /full/i);
});

test('MAX_PLAYERS is 10', () => {
  assert.equal(MAX_PLAYERS, 10);
});

test('a repeat hello from the client that already holds the slot does not deny itself', () => {
  const room = new Room();
  const p1 = fakeClient();
  const first = room.join(p1, 'player');
  const second = room.join(p1, 'player');
  assert.equal(second.granted, true);
  assert.equal(second.id, first.id);
});

test('player slot frees up after leave, letting the next player in', () => {
  const room = new Room();
  const p1 = fakeClient();
  const p2 = fakeClient();
  room.join(p1, 'player');
  room.leave(p1);
  const result = room.join(p2, 'player');
  assert.equal(result.granted, true);
});

test('leaving a client that never joined is a no-op', () => {
  const room = new Room();
  const stranger = fakeClient();
  assert.equal(room.leave(stranger), null);
});

test('any number of spectators may join concurrently', () => {
  const room = new Room();
  room.join(fakeClient(), 'spectator');
  room.join(fakeClient(), 'spectator');
  room.join(fakeClient(), 'spectator');
  assert.equal(room.spectatorCount, 3);
});

function snapshot(overrides = {}) {
  return { type: 'state', seq: 1, t: 12.5, area: 0, p: { i: 0, x: 1, y: 0, z: 2, r: 90 }, e: [{ i: 3, x: 5 }], ...overrides };
}

test('spectators receive state snapshots submitted by the player', () => {
  const room = new Room();
  const player = fakeClient();
  const spectator = fakeClient();
  room.join(player, 'player');
  room.join(spectator, 'spectator');

  room.submitState(player, snapshot());

  const state = spectator.sent.find((m) => m.type === 'state');
  assert.ok(state);
  assert.equal(state.t, 12.5);
  assert.equal(state.p.x, 1);
  assert.equal(state.e[0].i, 3);
  assert.ok(!player.sent.some((m) => m.type === 'state'), 'the player does not get its own snapshots back');
});

test('relayed snapshots start with the type field (the Unity client fast-paths on that prefix)', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  const serialized = room.submitState(player, { seq: 1, t: 1, p: {}, type: 'state' });
  assert.ok(serialized.startsWith('{"type":"state"'));
});

test('snapshots are stamped with the server-side player id, overriding anything the client sent', () => {
  const room = new Room();
  const player = fakeClient();
  const spectator = fakeClient();
  const { id } = room.join(player, 'player');
  room.join(spectator, 'spectator');

  room.submitState(player, snapshot({ pid: 999 }));

  assert.equal(spectator.sent.find((m) => m.type === 'state').pid, id);
});

test('a new player session gets a different pid, so spectators know to reset', () => {
  const room = new Room();
  const first = fakeClient();
  room.join(first, 'player');
  const a = JSON.parse(room.submitState(first, snapshot()));
  room.leave(first);

  const second = fakeClient();
  room.join(second, 'player');
  const b = JSON.parse(room.submitState(second, snapshot()));
  assert.notEqual(a.pid, b.pid);
});

test('a spectator cannot submit state', () => {
  const room = new Room();
  const spectator = fakeClient();
  room.join(spectator, 'spectator');
  assert.equal(room.submitState(spectator, snapshot()), null);
});

test('malformed or oversized snapshots are rejected', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  assert.equal(room.submitState(player, null), null);
  assert.equal(room.submitState(player, snapshot({ t: 'soon' })), null);
  assert.equal(room.submitState(player, snapshot({ t: Infinity })), null);
  assert.equal(room.submitState(player, snapshot({ p: undefined })), null);
  assert.equal(room.submitState(player, snapshot({ e: 'lots' })), null);
  assert.equal(room.submitState(player, snapshot(), MAX_STATE_BYTES + 1), null);
  assert.equal(room.lastStateFor(room.players.get(player).id), null);
});

test('spectators with a backed-up socket skip snapshots instead of queueing them', () => {
  const room = new Room();
  const player = fakeClient();
  const slow = fakeClient();
  const fast = fakeClient();
  slow.bufferedAmount = MAX_SPECTATOR_BACKLOG_BYTES + 1;
  fast.bufferedAmount = 0;
  room.join(player, 'player');
  room.join(slow, 'spectator');
  room.join(fast, 'spectator');

  room.submitState(player, snapshot());

  assert.ok(!slow.sent.some((m) => m.type === 'state'));
  assert.ok(fast.sent.some((m) => m.type === 'state'));
});

test('the latest snapshot is retained for spectators who join mid-game', () => {
  const room = new Room();
  const player = fakeClient();
  const { id } = room.join(player, 'player');
  room.submitState(player, snapshot({ seq: 1 }));
  room.submitState(player, snapshot({ seq: 2, t: 13 }));

  // server.js sends status + the watched player's last state explicitly on join; here we
  // just check the room retains the newest one for that purpose.
  assert.equal(JSON.parse(room.lastStateFor(id)).seq, 2);
  const late = fakeClient();
  room.join(late, 'spectator');
  assert.equal(JSON.parse(room.lastStateForSpectator(late)).seq, 2);
});

test('gear messages are relayed, stamped, and kept for spectators who start watching later', () => {
  const room = new Room();
  const player = fakeClient();
  const watcher = fakeClient();
  const { id } = room.join(player, 'player');
  room.join(watcher, 'spectator');
  room.submitState(player, snapshot({ seq: 1 }));

  room.submitGear(player, { type: 'gear', pid: 999, gold: 42 });

  const relayed = watcher.sent.find((m) => m.type === 'gear');
  assert.ok(relayed);
  assert.equal(relayed.gold, 42);
  assert.equal(relayed.pid, id);

  const late = fakeClient();
  room.join(late, 'spectator');
  const catchUp = room.catchUpForSpectator(late).map((m) => JSON.parse(m));
  assert.deepEqual(catchUp.map((m) => m.type), ['state', 'gear']);
});

test('gear messages from spectators are ignored', () => {
  const room = new Room();
  const spectator = fakeClient();
  room.join(spectator, 'spectator');
  assert.equal(room.submitGear(spectator, { type: 'gear' }), null);
});

test('chat messages are broadcast to player and all spectators', () => {
  const room = new Room();
  const player = fakeClient();
  const spectator = fakeClient();
  room.join(player, 'player');
  room.join(spectator, 'spectator');

  room.submitChat(spectator, 'hello from the couch');

  assert.ok(player.sent.some((m) => m.type === 'chat' && m.text === 'hello from the couch'));
  assert.ok(spectator.sent.some((m) => m.type === 'chat' && m.text === 'hello from the couch'));
});

test('chat messages are truncated to the max length', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  const longText = 'x'.repeat(MAX_CHAT_MESSAGE_LENGTH + 50);

  const message = room.submitChat(player, longText);
  assert.equal(message.text.length, MAX_CHAT_MESSAGE_LENGTH);
});

test('blank chat messages are dropped', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  const result = room.submitChat(player, '   ');
  assert.equal(result, null);
});

test('chat history is capped and trimmed from the oldest end', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  for (let i = 0; i < MAX_CHAT_HISTORY + 10; i++) {
    room.submitChat(player, `message ${i}`);
  }
  assert.equal(room.chatHistory.length, MAX_CHAT_HISTORY);
  assert.equal(room.chatHistory[0].text, `message ${10}`);
});

test('leaving player clears the last snapshot so new spectators see "no player" state', () => {
  const room = new Room();
  const player = fakeClient();
  const { id } = room.join(player, 'player');
  room.submitState(player, snapshot());
  room.leave(player);
  assert.equal(room.lastStateFor(id), null);
  assert.equal(room.playerActive, false);
  const late = fakeClient();
  room.join(late, 'spectator');
  assert.equal(room.lastStateForSpectator(late), null);
});

test('spectator status broadcast reflects player presence and spectator count', () => {
  const room = new Room();
  const spectator = fakeClient();
  room.join(spectator, 'spectator');

  const player = fakeClient();
  room.join(player, 'player');

  const status = spectator.sent.find((m) => m.type === 'status' && m.playerActive === true);
  assert.ok(status);
  assert.equal(status.spectatorCount, 1);
});

test('a denied player is promoted automatically when the player leaves', () => {
  const room = new Room({ maxPlayers: 1 });
  const first = fakeClient();
  const second = fakeClient();
  room.join(first, 'player');
  assert.equal(room.join(second, 'player', 'Bob').granted, false);

  room.leave(first);

  assert.ok(room.isPlayer(second));
  const grant = second.sent.find((m) => m.type === 'welcome');
  assert.ok(grant && grant.granted && grant.role === 'player');
  assert.equal(grant.you.name, 'Bob');
});

test('waiting players are promoted oldest first, and ones who left are skipped', () => {
  const room = new Room({ maxPlayers: 1 });
  const player = fakeClient();
  const gone = fakeClient();
  const next = fakeClient();
  const last = fakeClient();
  room.join(player, 'player');
  room.join(gone, 'player');
  room.join(next, 'player');
  room.join(last, 'player');

  assert.equal(room.leave(gone), 'waiting');
  room.leave(player);

  assert.ok(room.isPlayer(next));
  assert.ok(!gone.sent.some((m) => m.type === 'welcome'));
  assert.ok(!last.sent.some((m) => m.type === 'welcome'));
});

test('a repeated hello from a waiting connection does not queue it twice', () => {
  const room = new Room({ maxPlayers: 1 });
  room.join(fakeClient(), 'player');
  const waiter = fakeClient();
  room.join(waiter, 'player');
  room.join(waiter, 'player');
  assert.equal(room.waiting.length, 1);
});

test('spectators see the player slot stay occupied across a handover', () => {
  const room = new Room({ maxPlayers: 1 });
  const first = fakeClient();
  const second = fakeClient();
  const spectator = fakeClient();
  room.join(spectator, 'spectator');
  room.join(first, 'player');
  room.join(second, 'player');
  spectator.sent.length = 0;

  room.leave(first);

  const status = spectator.sent.filter((m) => m.type === 'status').pop();
  assert.equal(status.playerActive, true);
});

test('each player has their own pid and spectators only get the player they watch', () => {
  const room = new Room();
  const alice = fakeClient();
  const bob = fakeClient();
  const watcher = fakeClient();
  const a = room.join(alice, 'player', 'Alice');
  const b = room.join(bob, 'player', 'Bob');
  room.join(watcher, 'spectator');

  room.submitState(alice, snapshot({ seq: 1 }));
  room.submitState(bob, snapshot({ seq: 2 }));

  const states = watcher.sent.filter((m) => m.type === 'state');
  assert.equal(states.length, 1, 'only the watched player is relayed');
  assert.equal(states[0].pid, a.id, 'a new spectator watches the longest-playing player');
  assert.notEqual(a.id, b.id);
});

test('a spectator can switch to another player and immediately gets their latest snapshot', () => {
  const room = new Room();
  const alice = fakeClient();
  const bob = fakeClient();
  const watcher = fakeClient();
  room.join(alice, 'player', 'Alice');
  const b = room.join(bob, 'player', 'Bob');
  room.join(watcher, 'spectator');
  room.submitState(bob, snapshot({ seq: 7 }));
  watcher.sent.length = 0;

  assert.equal(room.watch(watcher, b.id), true);

  const status = watcher.sent.find((m) => m.type === 'status');
  assert.equal(status.watching, b.id);
  const state = watcher.sent.find((m) => m.type === 'state');
  assert.equal(state.pid, b.id);
  assert.equal(state.seq, 7);

  room.submitState(alice, snapshot({ seq: 8 }));
  room.submitState(bob, snapshot({ seq: 9 }));
  assert.deepEqual(watcher.sent.filter((m) => m.type === 'state').map((m) => m.seq), [7, 9]);
});

test('watching an unknown player, or watching as a player, does nothing', () => {
  const room = new Room();
  const player = fakeClient();
  const watcher = fakeClient();
  const { id } = room.join(player, 'player');
  room.join(watcher, 'spectator');
  assert.equal(room.watch(watcher, 12345), false);
  assert.equal(room.watch(player, id), false);
  assert.equal(room.spectators.get(watcher).watching, id);
});

test('when the watched player leaves, their spectators move to another player', () => {
  const room = new Room();
  const alice = fakeClient();
  const bob = fakeClient();
  const watcher = fakeClient();
  room.join(alice, 'player', 'Alice');
  const b = room.join(bob, 'player', 'Bob');
  room.join(watcher, 'spectator');
  room.submitState(bob, snapshot({ seq: 3 }));
  watcher.sent.length = 0;

  room.leave(alice);

  assert.equal(room.spectators.get(watcher).watching, b.id);
  assert.equal(watcher.sent.find((m) => m.type === 'state').pid, b.id);
  const status = watcher.sent.filter((m) => m.type === 'status').pop();
  assert.equal(status.watching, b.id);
  assert.deepEqual(status.players, [{ id: b.id, name: 'Bob' }]);
});

test('a spectator who joined while no one played picks up the first player to arrive', () => {
  const room = new Room();
  const watcher = fakeClient();
  room.join(watcher, 'spectator');
  assert.equal(room.spectators.get(watcher).watching, null);

  const player = fakeClient();
  const { id } = room.join(player, 'player', 'Cat');

  assert.equal(room.spectators.get(watcher).watching, id);
  const status = watcher.sent.filter((m) => m.type === 'status').pop();
  assert.equal(status.watching, id);
  assert.equal(status.playerCount, 1);
  assert.equal(status.maxPlayers, MAX_PLAYERS);
  room.submitState(player, snapshot());
  assert.ok(watcher.sent.some((m) => m.type === 'state' && m.pid === id));
});

test('chat from one player reaches the other players too', () => {
  const room = new Room();
  const alice = fakeClient();
  const bob = fakeClient();
  room.join(alice, 'player', 'Alice');
  room.join(bob, 'player', 'Bob');

  room.submitChat(alice, 'hi bob');

  const got = bob.sent.find((m) => m.type === 'chat');
  assert.equal(got.from, 'Alice');
  assert.equal(got.role, 'player');
});
