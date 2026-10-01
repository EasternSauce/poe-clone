'use strict';
const WebSocket = require('ws');
const ws = new WebSocket('ws://localhost:8099');
ws.on('open', () => ws.send(JSON.stringify({ type: 'hello', role: 'player', name: 'Intruder' })));
ws.on('message', (raw) => {
  console.log('second player got:', raw.toString());
  ws.close();
  process.exit(0);
});
setTimeout(() => process.exit(1), 3000);
