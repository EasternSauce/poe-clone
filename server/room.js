'use strict';

// Pure session/room logic, kept free of actual WebSocket/network objects so it
// can be unit tested with plain fake "client" objects ({ id, send(obj) }).
//
// Rules:
// - Up to MAX_PLAYERS players at a time, each playing their own, separate game
//   (nothing is shared between them but chat). Further player connections are
//   rejected (granted: false) and queued; when a player disconnects or goes stale,
//   the longest-waiting queued connection is promoted and sent a fresh grant, so
//   the "the game is full" screen turns into the game on its own.
// - Any number of spectators may connect at any time. Each spectator watches one
//   player at a time (the longest-playing one by default) and can switch with a
//   "watch" message; when the watched player leaves, they move on to the next one.
// - Each player pushes ~10Hz "state" snapshots (positions/animation events of the
//   player and nearby enemies, HUD, gear - see Assets/Scripts/Network/Replication),
//   which are stamped with that player's session id and relayed to the spectators
//   watching them, plus kept as that player's `lastState` so a spectator who starts
//   watching mid-game immediately has a scene to show instead of waiting a tick.
// - Players also send a "gear" message whenever their inventory, passives or skills change
//   (the full contents, so a spectator can open the same menus and read the same item
//   stats). It is relayed the same way and kept as `lastGear`, sent after `lastState`.
// - Snapshots are disposable (the next one supersedes it), so a spectator whose
//   socket is backed up simply skips some rather than queueing them forever.
// - Chat messages from anyone are broadcast to everyone (players + spectators).
// - A short rolling chat history is kept so newly joined clients aren't
//   dropped into a conversation with no context.
// - Co-op: a player can host ("coopHost"), which lists them in the lobby that browsing
//   players ("coopList") see live. "coopJoin" pairs a browser with a host; from then on
//   their "co"/"cev" messages (the host's world, the guest's character, hits and kills)
//   are relayed raw to the partner. A host plays from the start and stays hosting: when its
//   guest leaves or drops, the game is listed again, so the guest (or anyone) can rejoin.
//   The host leaving ends the party for the guest.

const MAX_PLAYERS = 10;
const MAX_CHAT_HISTORY = 50;
const MAX_CHAT_MESSAGE_LENGTH = 300;
const MAX_NAME_LENGTH = 24;
const MAX_STATE_BYTES = 32 * 1024;
// A full bag, gear and an open 12x12 stash, every item with its stats.
const MAX_GEAR_BYTES = 60 * 1024;
// A spectator with more than this already queued on its socket is on a connection too
// slow for the stream; drop snapshots for it until it catches up. Kept to about a second of
// snapshots: everything queued is that much behind the player, and a bigger queue showed up
// as several seconds of lag (attacks and gear changes arriving long after they happened).
const MAX_SPECTATOR_BACKLOG_BYTES = 48 * 1024;
// A co-op world snapshot: the host, every monster near either player, recent skill casts.
const MAX_COOP_BYTES = 48 * 1024;

class Room {
  constructor({ now = () => Date.now(), maxPlayers = MAX_PLAYERS } = {}) {
    this.now = now;
    this.maxPlayers = maxPlayers;
    this.players = new Map(); // client -> { id, name, joinedAt, lastState, lastGear }, oldest first
    this.spectators = new Map(); // client -> { id, name, joinedAt, watching: player id | null }
    this.waiting = []; // denied player connections, oldest first: { client, id, name }
    this.chatHistory = [];
    this.nextId = 1;
    // Co-op, by player client: hosts (client -> name, listed in the lobby while they have no
    // guest), browsing clients, and the pairs playing together (client -> partner, both ways).
    this.hosting = new Map();
    this.browsing = new Set();
    this.partners = new Map();
  }

  _allocId() {
    return this.nextId++;
  }

  get playerActive() {
    return this.players.size > 0;
  }

  get playerCount() {
    return this.players.size;
  }

  get spectatorCount() {
    return this.spectators.size;
  }

  // Returns { granted, id, name, reason }
  join(client, role, requestedName) {
    const id = this._allocId();
    const name = sanitizeName(requestedName) || (role === 'player' ? 'Player' : `Spectator ${id}`);

    if (role === 'player') {
      // A repeat hello from a connection that already holds a slot (e.g. a client retry)
      // must not deny itself the slot it's already holding.
      const existing = this.players.get(client);
      if (existing) {
        return { granted: true, id: existing.id, name: existing.name };
      }
      if (this.players.size >= this.maxPlayers) {
        if (!this.waiting.some((w) => w.client === client)) this.waiting.push({ client, id, name });
        return {
          granted: false,
          id,
          reason: `The game is full (${this.maxPlayers} players). You'll join as soon as someone leaves.`,
        };
      }
      this.players.set(client, { id, name, joinedAt: this.now(), lastState: null, lastGear: null });
      this._onPlayersChanged();
      return { granted: true, id, name };
    }

    // role === 'spectator': always allowed, watching the longest-playing player.
    this.spectators.set(client, { id, name, joinedAt: this.now(), watching: this._firstPlayerId() });
    return { granted: true, id, name };
  }

  leave(client) {
    this.coopLeave(client, 'Your partner left the game.');
    this.browsing.delete(client);
    const player = this.players.get(client);
    if (player) {
      this.players.delete(client);
      this._promoteNextWaiting();
      this._moveWatchersOff(player.id);
      this._onPlayersChanged();
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
    return this.players.has(client);
  }

  // A spectator asks to watch another player. Returns true if it switched.
  watch(client, playerId) {
    const spectator = this.spectators.get(client);
    if (!spectator || !this._playerById(playerId)) return false;
    spectator.watching = playerId;
    safeSend(client, this.statusMessage(client));
    for (const message of this.catchUpFor(playerId)) safeSendRaw(client, message);
    return true;
  }

  // The most recent snapshot of a player (serialized), or null.
  lastStateFor(playerId) {
    const player = this._playerById(playerId);
    return player ? player.lastState : null;
  }

  // The most recent gear message of a player (serialized), or null.
  lastGearFor(playerId) {
    const player = this._playerById(playerId);
    return player ? player.lastGear : null;
  }

  // Everything a spectator who starts watching a player needs, in sending order.
  catchUpFor(playerId) {
    return [this.lastStateFor(playerId), this.lastGearFor(playerId)].filter((m) => m);
  }

  catchUpForSpectator(client) {
    const spectator = this.spectators.get(client);
    return spectator && spectator.watching !== null ? this.catchUpFor(spectator.watching) : [];
  }

  // What a (newly joined) spectator should be shown first, or null.
  lastStateForSpectator(client) {
    const spectator = this.spectators.get(client);
    return spectator && spectator.watching !== null ? this.lastStateFor(spectator.watching) : null;
  }

  // Accepts a gameplay snapshot from a player and relays it to the spectators watching them.
  // `payload` is the parsed message and `rawLength` its size on the wire.
  // Returns the serialized message that was broadcast, or null if rejected.
  submitState(client, payload, rawLength = 0) {
    const player = this.players.get(client);
    if (!player) return null;
    if (!payload || typeof payload !== 'object') return null;
    if (rawLength > MAX_STATE_BYTES) return null;
    if (typeof payload.t !== 'number' || !Number.isFinite(payload.t)) return null;
    if (!payload.p || typeof payload.p !== 'object') return null;
    if (payload.e !== undefined && !Array.isArray(payload.e)) return null;

    // "type" must stay first: the Unity client fast-paths messages starting with
    // {"type":"state". pid is server-owned (a change tells spectators "new player, reset"),
    // so whatever the client put there is discarded.
    const { type, pid, ...rest } = payload;
    const serialized = JSON.stringify({ type: 'state', ...rest, pid: player.id });

    player.lastState = serialized;
    for (const [spectator, info] of this.spectators) {
      if (info.watching !== player.id) continue;
      if (typeof spectator.bufferedAmount === 'number' && spectator.bufferedAmount > MAX_SPECTATOR_BACKLOG_BYTES) continue;
      safeSendRaw(spectator, serialized);
    }
    return serialized;
  }

  // Accepts a player's gear message (menus' contents) and relays it like a snapshot, except
  // that it is never skipped for a backed-up spectator: it only comes when something changed,
  // so a dropped one would leave the spectator's copy wrong until the next change.
  submitGear(client, payload, rawLength = 0) {
    const player = this.players.get(client);
    if (!player) return null;
    if (!payload || typeof payload !== 'object') return null;
    if (rawLength > MAX_GEAR_BYTES) return null;

    const { type, pid, ...rest } = payload;
    const serialized = JSON.stringify({ type: 'gear', ...rest, pid: player.id });

    player.lastGear = serialized;
    for (const [spectator, info] of this.spectators) {
      if (info.watching === player.id) safeSendRaw(spectator, serialized);
    }
    return serialized;
  }

  submitChat(client, text) {
    const trimmed = String(text || '').slice(0, MAX_CHAT_MESSAGE_LENGTH).trim();
    if (!trimmed) return null;

    const player = this.players.get(client);
    const from = player ? (player.name || 'Player') : (this.spectators.get(client)?.name || 'Spectator');

    const message = {
      type: 'chat',
      from,
      role: player ? 'player' : 'spectator',
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

  // ------------------------------------------------------------------ co-op

  // Lists a player in the lobby under the given name, while they have no guest, until they leave.
  coopHost(client, requestedName) {
    const player = this.players.get(client);
    if (!player || this.partners.has(client)) return false;
    this.browsing.delete(client);
    this.hosting.set(client, sanitizeName(requestedName) || player.name);
    safeSend(client, { type: 'coop', state: 'hosting' });
    this._broadcastLobby();
    return true;
  }

  // A player starts browsing: sent the lobby now and whenever it changes.
  coopList(client) {
    if (!this.players.has(client) || this.partners.has(client)) return false;
    this._stopHosting(client);
    this.browsing.add(client);
    safeSend(client, this.lobbyMessage());
    return true;
  }

  // Pairs a browsing player with a waiting host. Returns true if the party started.
  coopJoin(client, hostId, requestedName) {
    const guest = this.players.get(client);
    if (!guest || this.partners.has(client)) return false;
    let host = null;
    for (const [c] of this.hosting) {
      if (c !== client && !this.partners.has(c) && this.players.get(c)?.id === hostId) host = c;
    }
    if (!host) {
      safeSend(client, { type: 'coop', state: 'failed', reason: 'That game is no longer open.' });
      safeSend(client, this.lobbyMessage());
      return false;
    }
    const hostName = this.hosting.get(host);
    const guestName = sanitizeName(requestedName) || guest.name;
    this.browsing.delete(client);
    this.browsing.delete(host);
    this.partners.set(host, client);
    this.partners.set(client, host);
    safeSend(host, { type: 'coop', state: 'started', coopRole: 'host', partnerName: guestName });
    safeSend(client, { type: 'coop', state: 'started', coopRole: 'guest', partnerName: hostName });
    this._broadcastLobby();
    return true;
  }

  // Stops hosting/browsing and ends any party (telling the partner why). A host whose guest
  // left keeps hosting: its game is listed again.
  coopLeave(client, reason = 'Your partner left the game.') {
    this.browsing.delete(client);
    const wasHosting = this.hosting.delete(client);
    const partner = this.partners.get(client);
    if (partner) {
      this.partners.delete(client);
      this.partners.delete(partner);
      safeSend(partner, { type: 'coop', state: 'ended', reason });
    }
    if (wasHosting || partner) this._broadcastLobby();
  }

  // Relays a co-op message, as received, to the sender's partner. Returns whether it was sent.
  coopRelay(client, raw, rawLength = 0) {
    const partner = this.partners.get(client);
    if (!partner || rawLength > MAX_COOP_BYTES) return false;
    safeSendRaw(partner, raw);
    return true;
  }

  partnerOf(client) {
    return this.partners.get(client) || null;
  }

  lobbyMessage() {
    const hosts = [];
    for (const [client, name] of this.hosting) {
      const player = this.players.get(client);
      if (player && !this.partners.has(client)) hosts.push({ id: player.id, name });
    }
    return { type: 'lobby', hosts };
  }

  _stopHosting(client) {
    if (!this.hosting.delete(client)) return false;
    this._broadcastLobby();
    return true;
  }

  _broadcastLobby() {
    const message = this.lobbyMessage();
    for (const client of this.browsing) safeSend(client, message);
  }

  // The status a client sees; for a spectator it includes who they are watching.
  statusMessage(client) {
    const spectator = client ? this.spectators.get(client) : undefined;
    return {
      type: 'status',
      playerActive: this.playerActive,
      playerCount: this.players.size,
      maxPlayers: this.maxPlayers,
      spectatorCount: this.spectatorCount,
      players: [...this.players.values()].map((p) => ({ id: p.id, name: p.name })),
      watching: spectator && spectator.watching !== null ? spectator.watching : 0,
    };
  }

  _playerById(playerId) {
    for (const player of this.players.values()) {
      if (player.id === playerId) return player;
    }
    return null;
  }

  _firstPlayerId() {
    const first = this.players.values().next();
    return first.done ? null : first.value.id;
  }

  // Spectators of a player who left move to the next player in join order (wrapping), or
  // to nobody; each is sent that player's latest snapshot so the switch is immediate.
  _moveWatchersOff(leftId) {
    const ids = [...this.players.values()].map((p) => p.id);
    for (const [client, info] of this.spectators) {
      if (info.watching !== leftId) continue;
      const next = ids.find((id) => id > leftId) ?? ids[0] ?? null;
      info.watching = next;
      if (next !== null) for (const message of this.catchUpFor(next)) safeSendRaw(client, message);
    }
  }

  // Spectators watching nobody (no one was playing) pick up the first player who appears.
  _assignIdleWatchers() {
    const first = this._firstPlayerId();
    if (first === null) return;
    for (const info of this.spectators.values()) {
      if (info.watching === null || !this._playerById(info.watching)) info.watching = first;
    }
  }

  _onPlayersChanged() {
    this._assignIdleWatchers();
    this._broadcastStatus();
  }

  _promoteNextWaiting() {
    const next = this.waiting.shift();
    if (!next) return null;
    this.players.set(next.client, { id: next.id, name: next.name, joinedAt: this.now(), lastState: null, lastGear: null });
    safeSend(next.client, {
      type: 'welcome',
      role: 'player',
      granted: true,
      you: { id: next.id, name: next.name },
    });
    return next;
  }

  _broadcastStatus() {
    for (const client of this.spectators.keys()) {
      safeSend(client, this.statusMessage(client));
    }
  }

  _broadcastToAll(message) {
    for (const client of this.players.keys()) safeSend(client, message);
    for (const client of this.spectators.keys()) safeSend(client, message);
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

module.exports = {
  Room,
  MAX_PLAYERS,
  MAX_CHAT_HISTORY,
  MAX_CHAT_MESSAGE_LENGTH,
  MAX_NAME_LENGTH,
  MAX_STATE_BYTES,
  MAX_SPECTATOR_BACKLOG_BYTES,
  MAX_COOP_BYTES,
};
