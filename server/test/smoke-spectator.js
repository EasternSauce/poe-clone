'use strict';
// Manual check: connect as a spectator to a live server and measure the gameplay stream
// (rate, jitter, payload size, entity counts) coming from whoever is playing.
// Usage: node test/smoke-spectator.js [ws://host:port] [snapshotCount]
const WebSocket = require('ws');

const url = process.argv[2] || 'ws://localhost:8099';
const wanted = Number(process.argv[3] || 30);
const ws = new WebSocket(url);

ws.on('open', () => {
  ws.send(JSON.stringify({ type: 'hello', role: 'spectator', name: 'SmokeTestWatcher' }));
});

const arrivals = [];
let done = false;
ws.on('message', (raw) => {
  const text = raw.toString();
  const msg = JSON.parse(text);
  if (msg.type !== 'state') {
    console.log('other message:', text);
    return;
  }

  arrivals.push({ at: Date.now(), t: msg.t, bytes: Buffer.byteLength(text), enemies: msg.e.length, pid: msg.pid });
  if (arrivals.length === 1) console.log('first snapshot:', text.slice(0, 400));
  if (done || arrivals.length < wanted) return;
  done = true;

  const gaps = arrivals.slice(1).map((a, k) => a.t - arrivals[k].t);
  const meanGap = gaps.reduce((s, g) => s + g, 0) / gaps.length;
  const jitter = arrivals.slice(1).map((a, k) => Math.abs((a.at - arrivals[k].at) / 1000 - gaps[k]));
  const meanBytes = arrivals.reduce((s, a) => s + a.bytes, 0) / arrivals.length;
  console.log(JSON.stringify({
    snapshots: arrivals.length,
    rateHz: +(1 / meanGap).toFixed(2),
    meanJitterMs: +(1000 * jitter.reduce((s, j) => s + j, 0) / jitter.length).toFixed(1),
    meanBytes: Math.round(meanBytes),
    kbPerSecond: +((meanBytes / meanGap) / 1024).toFixed(1),
    maxEnemies: Math.max(...arrivals.map((a) => a.enemies)),
    pid: arrivals[0].pid,
  }));
  ws.send(JSON.stringify({ type: 'chat', text: 'hello from spectator smoke test' }));
  setTimeout(() => { ws.close(); process.exit(0); }, 300);
});

setTimeout(() => {
  console.log(`timed out: got ${arrivals.length}/${wanted} snapshots`);
  process.exit(1);
}, 15000);
