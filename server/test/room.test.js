'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { Room, MAX_CHAT_HISTORY, MAX_CHAT_MESSAGE_LENGTH } = require('../room');

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

test('spectators receive frames submitted by the player', () => {
  const room = new Room();
  const player = fakeClient();
  const spectator = fakeClient();
  room.join(player, 'player');
  room.join(spectator, 'spectator');

  room.submitFrame(player, { image: 'base64data', hud: { hp: 50 } });

  const frame = spectator.sent.find((m) => m.type === 'frame');
  assert.ok(frame);
  assert.equal(frame.image, 'base64data');
  assert.equal(frame.hud.hp, 50);
});

test('a spectator cannot submit frames', () => {
  const room = new Room();
  const spectator = fakeClient();
  room.join(spectator, 'spectator');
  const result = room.submitFrame(spectator, { image: 'x' });
  assert.equal(result, null);
});

test('new spectators immediately get the last known frame and status', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  room.submitFrame(player, { image: 'first-frame', hud: {} });

  const lateSpectator = fakeClient();
  room.join(lateSpectator, 'spectator');
  // server.js sends status + lastFrame explicitly on join; here we just check
  // the room retains lastFrame for that purpose.
  assert.equal(room.lastFrame.image, 'first-frame');
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

test('leaving player clears the last frame so new spectators see "no player" state', () => {
  const room = new Room();
  const player = fakeClient();
  room.join(player, 'player');
  room.submitFrame(player, { image: 'frame-1' });
  room.leave(player);
  assert.equal(room.lastFrame, null);
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
