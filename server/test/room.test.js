'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { Room, MAX_CHAT_HISTORY, MAX_CHAT_MESSAGE_LENGTH, MAX_STATE_BYTES, MAX_SPECTATOR_BACKLOG_BYTES } = require('../room');

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

test('second player join is rejected while the first is active', () => {
  const room = new Room();
  const p1 = fakeClient();
  const p2 = fakeClient();
  room.join(p1, 'player');
  const result = room.join(p2, 'player');
  assert.equal(result.granted, false);
  assert.match(result.reason, /already/i);
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
  assert.equal(room.lastState, null);
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
  room.join(player, 'player');
  room.submitState(player, snapshot({ seq: 1 }));
  room.submitState(player, snapshot({ seq: 2, t: 13 }));

  // server.js sends status + lastState explicitly on join; here we just check
  // the room retains the newest one for that purpose.
  assert.equal(JSON.parse(room.lastState).seq, 2);
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
  room.join(player, 'player');
  room.submitState(player, snapshot());
  room.leave(player);
  assert.equal(room.lastState, null);
  assert.equal(room.playerActive, false);
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
  const room = new Room();
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
  const room = new Room();
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
  const room = new Room();
  room.join(fakeClient(), 'player');
  const waiter = fakeClient();
  room.join(waiter, 'player');
  room.join(waiter, 'player');
  assert.equal(room.waiting.length, 1);
});

test('spectators see the player slot stay occupied across a handover', () => {
  const room = new Room();
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
