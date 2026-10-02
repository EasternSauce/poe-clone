'use strict';

const http = require('http');
const { WebSocketServer } = require('ws');
const { Room } = require('./room');

const PORT = process.env.PORT || 8080;
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || '*';
const HEARTBEAT_MS = 15000;
// If a player's tab is frozen/crashed without a clean socket close, the ws
// heartbeat (below) still reclaims the slot within ~2 missed pings.
// Each player sends ~10 snapshots/s; allow some burstiness from frame timing but cap
// at ~20/s so a misbehaving client can't flood spectators.
const STATE_RATE_LIMIT_MS = 45;
// Snapshots are a few KB and chat lines are tiny; nothing legitimate comes close to this.
const MAX_PAYLOAD_BYTES = 64 * 1024;

const room = new Room();

function corsHeaders() {
  return {
    'Access-Control-Allow-Origin': ALLOWED_ORIGIN,
    'Access-Control-Allow-Methods': 'GET, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
  };
}

const httpServer = http.createServer((req, res) => {
  const headers = corsHeaders();

  if (req.method === 'OPTIONS') {
    res.writeHead(204, headers);
    res.end();
    return;
  }

  if (req.url === '/health') {
    res.writeHead(200, { ...headers, 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ ok: true }));
    return;
  }

  if (req.url === '/status') {
    res.writeHead(200, { ...headers, 'Content-Type': 'application/json' });
    res.end(JSON.stringify(room.statusMessage()));
    return;
  }

  res.writeHead(404, headers);
  res.end('Not found');
});

const wss = new WebSocketServer({ server: httpServer, maxPayload: MAX_PAYLOAD_BYTES });

let debugConnCounter = 0;

wss.on('connection', (ws) => {
  ws.isAlive = true;
  ws.lastStateAt = 0;
  ws.role = null;
  ws._debugLabel = `conn#${++debugConnCounter}`;
  if (process.env.DEBUG_ROOM) console.log('connected', ws._debugLabel);

  ws.on('pong', () => {
    ws.isAlive = true;
  });

  ws.on('message', (raw) => {
    const text = raw.toString();
    let msg;
    try {
      msg = JSON.parse(text);
    } catch {
      return; // ignore malformed messages
    }
    if (!msg || typeof msg !== 'object') return;

    switch (msg.type) {
      case 'hello': {
        const role = msg.role === 'player' ? 'player' : 'spectator';
        const result = room.join(ws, role, msg.name);
        ws.role = role;

        safeSend(ws, {
          type: 'welcome',
          role,
          granted: result.granted,
          reason: result.reason,
          you: result.granted ? { id: result.id, name: result.name } : undefined,
        });

        if (role === 'spectator') {
          safeSend(ws, room.statusMessage(ws));
          const state = room.lastStateForSpectator(ws);
          if (state) safeSendRaw(ws, state);
          for (const chatMsg of room.chatHistory) safeSend(ws, chatMsg);
        }
        break;
      }

      case 'state': {
        const now = Date.now();
        if (now - ws.lastStateAt < STATE_RATE_LIMIT_MS) return;
        ws.lastStateAt = now;
        room.submitState(ws, msg, Buffer.byteLength(text));
        break;
      }

      case 'watch': {
        // A spectator switching to another player's game.
        room.watch(ws, Number(msg.id));
        break;
      }

      case 'chat': {
        room.submitChat(ws, msg.text);
        break;
      }

      default:
        break;
    }
  });

  ws.on('close', () => {
    room.leave(ws);
  });

  ws.on('error', () => {
    room.leave(ws);
  });
});

// Reclaim slots held by clients that vanished without a clean close
// (crashed tab, network drop, laptop sleep).
const heartbeat = setInterval(() => {
  for (const ws of wss.clients) {
    if (ws.isAlive === false) {
      ws.terminate();
      continue;
    }
    ws.isAlive = false;
    ws.ping();
  }
}, HEARTBEAT_MS);

wss.on('close', () => clearInterval(heartbeat));

function safeSend(client, message) {
  safeSendRaw(client, JSON.stringify(message));
}

function safeSendRaw(client, serialized) {
  try {
    client.send(serialized);
  } catch {
    // client already gone; 'close' handler will clean up the room.
  }
}

httpServer.listen(PORT, () => {
  console.log(`Session server listening on :${PORT} (allowed origin: ${ALLOWED_ORIGIN})`);
});

module.exports = { httpServer, wss, room };
