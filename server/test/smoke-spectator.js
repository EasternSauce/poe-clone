'use strict';
// Manual check: connect as a spectator to a live server and report what we get.
const WebSocket = require('ws');
const ws = new WebSocket('ws://localhost:8099');

ws.on('open', () => {
  ws.send(JSON.stringify({ type: 'hello', role: 'spectator', name: 'SmokeTestWatcher' }));
});

let frameCount = 0;
ws.on('message', (raw) => {
  const msg = JSON.parse(raw.toString());
  if (msg.type === 'frame') {
    frameCount++;
    console.log(`frame #${frameCount}: image bytes(base64)=${msg.image.length}, hud=${JSON.stringify(msg.hud)}`);
    if (frameCount >= 2) {
      ws.send(JSON.stringify({ type: 'chat', text: 'hello from spectator smoke test' }));
      setTimeout(() => { ws.close(); process.exit(0); }, 500);
    }
  } else {
    console.log('other message:', JSON.stringify(msg));
  }
});

setTimeout(() => {
  console.log('timed out waiting for frames');
  process.exit(1);
}, 5000);
