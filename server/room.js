'use strict';

// Pure session/room logic, kept free of actual WebSocket/network objects so it
// can be unit tested with plain fake "client" objects ({ id, send(obj) }).
//
// Rules:
// - At most one "player" at a time. A second player connection is rejected
//   (granted: false) until the first one disconnects or goes stale.
// - Any number of spectators may connect at any time.
// - The player pushes periodic "frame" (screenshot + HUD) messages, which are
//   relayed to every connected spectator, plus kept as `lastFrame` so brand
//   new spectators immediately see the current picture instead of a blank
//   screen until the next tick.
// - Chat messages from anyone are broadcast to everyone (player + spectators).
// - A short rolling chat history is kept so newly joined clients aren't
//   dropped into a conversation with no context.

const MAX_CHAT_HISTORY = 50;
const MAX_CHAT_MESSAGE_LENGTH = 300;
const MAX_NAME_LENGTH = 24;

class Room {
  constructor({ now = () => Date.now() } = {}) {
    this.now = now;
    this.player = null; // { client, id, joinedAt }
    this.spectators = new Map(); // client -> { id, joinedAt }
    this.lastFrame = null; // { image, hud, ts }
    this.chatHistory = [];
    this.nextId = 1;
  }

  _allocId() {
    return this.nextId++;
  }

  get playerActive() {
    return this.player !== null;
  }

  get spectatorCount() {
    return this.spectators.size;
  }

  // Returns { granted, id, reason }
  join(client, role, requestedName) {
    const id = this._allocId();
    const name = sanitizeName(requestedName) || (role === 'player' ? 'Player' : `Spectator ${id}`);

    if (role === 'player') {
      // A repeat hello from the connection that already holds the slot (e.g. a client retry)
      // must not deny itself the slot it's already holding.
      if (this.player && this.player.client === client) {
        return { granted: true, id: this.player.id, name: this.player.name };
      }
      if (this.player) {
        return { granted: false, id, reason: 'A player is already connected. Try again later.' };
      }
      this.player = { client, id, name, joinedAt: this.now() };
      this._broadcastStatus();
      return { granted: true, id, name };
    }

    // role === 'spectator': always allowed
    this.spectators.set(client, { id, name, joinedAt: this.now() });
    return { granted: true, id, name };
  }

  leave(client) {
    if (this.player && this.player.client === client) {
      this.player = null;
      this.lastFrame = null;
      this._broadcastStatus();
      return 'player';
    }
    if (this.spectators.has(client)) {
      this.spectators.delete(client);
      return 'spectator';
    }
    return null;
  }

  isPlayer(client) {
    return this.player !== null && this.player.client === client;
  }

  // Accepts a raw frame payload from the player and relays it to spectators.
  // Returns the normalized frame that was broadcast, or null if rejected.
  submitFrame(client, payload) {
    if (!this.isPlayer(client)) return null;
    if (!payload || typeof payload.image !== 'string') return null;

    const frame = {
      type: 'frame',
      image: payload.image,
      hud: payload.hud && typeof payload.hud === 'object' ? payload.hud : {},
      ts: this.now(),
    };

    this.lastFrame = frame;
    this._broadcastToSpectators(frame);
    return frame;
  }

  submitChat(client, text, roleHint) {
    const trimmed = String(text || '').slice(0, MAX_CHAT_MESSAGE_LENGTH).trim();
    if (!trimmed) return null;

    const from = this.isPlayer(client)
      ? (this.player.name || 'Player')
      : (this.spectators.get(client)?.name || 'Spectator');

    const message = {
      type: 'chat',
      from,
      role: this.isPlayer(client) ? 'player' : 'spectator',
      text: trimmed,
      ts: this.now(),
    };

    this.chatHistory.push(message);
    if (this.chatHistory.length > MAX_CHAT_HISTORY) {
      this.chatHistory.shift();
    }

    this._broadcastToAll(message);
    return message;
  }

  statusMessage() {
    return {
      type: 'status',
      playerActive: this.playerActive,
      spectatorCount: this.spectatorCount,
    };
  }

  _broadcastStatus() {
    this._broadcastToSpectators(this.statusMessage());
  }

  _broadcastToSpectators(message) {
    for (const client of this.spectators.keys()) {
      safeSend(client, message);
    }
  }

  _broadcastToAll(message) {
    if (this.player) safeSend(this.player.client, message);
    this._broadcastToSpectators(message);
  }
}

function safeSend(client, message) {
  try {
    if (process.env.DEBUG_ROOM) console.log('safeSend ->', client._debugLabel || client, message.type, message.text || '');
    client.send(JSON.stringify(message));
  } catch {
    // Dead sockets are cleaned up by the ws 'close'/heartbeat handling in server.js;
    // a failed send here just means this particular message is dropped.
  }
}

function sanitizeName(name) {
  if (typeof name !== 'string') return null;
  const trimmed = name.trim().slice(0, MAX_NAME_LENGTH);
  return trimmed.length > 0 ? trimmed : null;
}

module.exports = { Room, MAX_CHAT_HISTORY, MAX_CHAT_MESSAGE_LENGTH, MAX_NAME_LENGTH };
