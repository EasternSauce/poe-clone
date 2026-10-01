'use strict';
// Manual end-to-end smoke test against a running server (not part of `npm test`).
// Usage: PORT=8099 node server.js &  then  node test/smoke-ws.js
const WebSocket = require('ws');

const URL = 'ws://localhost:8099';

function connect(label) {
  const ws = new WebSocket(URL);
  ws.on('message', (raw) => console.log(`[${label}]`, raw.toString()));
  ws.on('open', () => console.log(`[${label}] open`));
  return ws;
}

async function wait(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

(async () => {
  const player = connect('player');
  await wait(200);
  player.send(JSON.stringify({ type: 'hello', role: 'player', name: 'Alice' }));
  await wait(200);

  const player2 = connect('player2-should-be-denied');
  await wait(200);
  player2.send(JSON.stringify({ type: 'hello', role: 'player', name: 'Bob' }));
  await wait(200);

  const spectator = connect('spectator');
  await wait(200);
  spectator.send(JSON.stringify({ type: 'hello', role: 'spectator', name: 'Watcher' }));
  await wait(200);

  player.send(JSON.stringify({ type: 'state', seq: 1, t: 1.5, area: 0, p: { i: 0, x: 1, z: 2 }, e: [] }));
  await wait(200);

  spectator.send(JSON.stringify({ type: 'chat', text: 'hi from spectator' }));
  await wait(200);

  player.close();
  await wait(200);

  console.log('--- expect spectator to have received a status update: playerActive=false ---');
  await wait(500);

  player2.close();
  spectator.close();
  process.exit(0);
})();
