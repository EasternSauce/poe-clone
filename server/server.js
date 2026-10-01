'use strict';

const http = require('http');
const { WebSocketServer } = require('ws');
const { Room } = require('./room');

const PORT = process.env.PORT || 8080;
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || '*';
const HEARTBEAT_MS = 15000;
// If the player's tab is frozen/crashed without a clean socket close, the ws
// heartbeat (below) still reclaims the slot within ~2 missed pings.
const FRAME_RATE_LIMIT_MS = 300; // hard floor so a misbehaving client can't flood spectators

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

const wss = new WebSocketServer({ server: httpServer });

let debugConnCounter = 0;

wss.on('connection', (ws) => {
  ws.isAlive = true;
  ws.lastFrameAt = 0;
  ws.role = null;
  ws._debugLabel = `conn#${++debugConnCounter}`;
  if (process.env.DEBUG_ROOM) console.log('connected', ws._debugLabel);

  ws.on('pong', () => {
    ws.isAlive = true;
  });

  ws.on('message', (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return; // ignore malformed frames
    }

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
          safeSend(ws, room.statusMessage());
          if (room.lastFrame) safeSend(ws, room.lastFrame);
          for (const chatMsg of room.chatHistory) safeSend(ws, chatMsg);
        }
        break;
      }

      case 'frame': {
        const now = Date.now();
        if (now - ws.lastFrameAt < FRAME_RATE_LIMIT_MS) return;
        ws.lastFrameAt = now;
        room.submitFrame(ws, msg);
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
  try {
    client.send(JSON.stringify(message));
  } catch {
    // client already gone; 'close' handler will clean up the room.
  }
}

httpServer.listen(PORT, () => {
  console.log(`Session server listening on :${PORT} (allowed origin: ${ALLOWED_ORIGIN})`);
});

module.exports = { httpServer, wss, room };
