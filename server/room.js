'use strict';

// Pure session/room logic, kept free of actual WebSocket/network objects so it
// can be unit tested with plain fake "client" objects ({ id, send(obj) }).
//
// Rules:
// - At most one "player" at a time. A second player connection is rejected
//   (granted: false) and queued; when the player disconnects or goes stale, the
//   longest-waiting queued connection is promoted and sent a fresh grant, so the
//   "someone is already playing" screen turns into the game on its own.
// - Any number of spectators may connect at any time.
// - The player pushes ~10Hz "state" snapshots (positions/animation events of the
//   player and nearby enemies, HUD, gear - see Assets/Scripts/Network/Replication),
//   which are stamped with the player's session id and relayed to every spectator,
//   plus kept as `lastState` so a spectator who joins mid-game immediately has a
//   scene to show instead of waiting for the next tick.
// - Snapshots are disposable (the next one supersedes it), so a spectator whose
//   socket is backed up simply skips some rather than queueing them forever.
// - Chat messages from anyone are broadcast to everyone (player + spectators).
// - A short rolling chat history is kept so newly joined clients aren't
//   dropped into a conversation with no context.

const MAX_CHAT_HISTORY = 50;
const MAX_CHAT_MESSAGE_LENGTH = 300;
const MAX_NAME_LENGTH = 24;
const MAX_STATE_BYTES = 32 * 1024;
// A spectator with more than this already queued on its socket is on a connection too
// slow for the stream; drop snapshots for it until it catches up.
const MAX_SPECTATOR_BACKLOG_BYTES = 256 * 1024;

class Room {
  constructor({ now = () => Date.now() } = {}) {
    this.now = now;
    this.player = null; // { client, id, joinedAt }
    this.spectators = new Map(); // client -> { id, joinedAt }
    this.waiting = []; // denied player connections, oldest first: { client, id, name }
    this.lastState = null; // serialized JSON of the most recent snapshot
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
        if (!this.waiting.some((w) => w.client === client)) this.waiting.push({ client, id, name });
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
      this.lastState = null;
      this._promoteNextWaiting();
      this._broadcastStatus();
      return 'player';
    }
    const waitingIndex = this.waiting.findIndex((w) => w.client === client);
    if (waitingIndex >= 0) {
      this.waiting.splice(waitingIndex, 1);
      return 'waiting';
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

  // Accepts a gameplay snapshot from the player and relays it to spectators.
  // `payload` is the parsed message and `rawLength` its size on the wire.
  // Returns the serialized message that was broadcast, or null if rejected.
  submitState(client, payload, rawLength = 0) {
    if (!this.isPlayer(client)) return null;
    if (!payload || typeof payload !== 'object') return null;
    if (rawLength > MAX_STATE_BYTES) return null;
    if (typeof payload.t !== 'number' || !Number.isFinite(payload.t)) return null;
    if (!payload.p || typeof payload.p !== 'object') return null;
    if (payload.e !== undefined && !Array.isArray(payload.e)) return null;

    // "type" must stay first: the Unity client fast-paths messages starting with
    // {"type":"state". pid is server-owned (a change tells spectators "new player, reset"),
    // so whatever the client put there is discarded.
    const { type, pid, ...rest } = payload;
    const serialized = JSON.stringify({ type: 'state', ...rest, pid: this.player.id });

    this.lastState = serialized;
    for (const spectator of this.spectators.keys()) {
      if (typeof spectator.bufferedAmount === 'number' && spectator.bufferedAmount > MAX_SPECTATOR_BACKLOG_BYTES) continue;
      safeSendRaw(spectator, serialized);
    }
    return serialized;
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

  _promoteNextWaiting() {
    const next = this.waiting.shift();
    if (!next) return null;
    this.player = { client: next.client, id: next.id, name: next.name, joinedAt: this.now() };
    safeSend(next.client, {
      type: 'welcome',
      role: 'player',
      granted: true,
      you: { id: next.id, name: next.name },
    });
    return next;
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

function safeSendRaw(client, serialized) {
  try {
    client.send(serialized);
  } catch {
    // see safeSend
  }
}

function sanitizeName(name) {
  if (typeof name !== 'string') return null;
  const trimmed = name.trim().slice(0, MAX_NAME_LENGTH);
  return trimmed.length > 0 ? trimmed : null;
}

module.exports = { Room, MAX_CHAT_HISTORY, MAX_CHAT_MESSAGE_LENGTH, MAX_NAME_LENGTH, MAX_STATE_BYTES, MAX_SPECTATOR_BACKLOG_BYTES };
