'use strict';

// A scripted co-op partner for testing one side of co-op in the Unity Editor without a second
// game build. It speaks the same protocol as the game (see Assets/Scripts/Network/CoopSession.cs):
//
//   node test/coop-fake-partner.js guest [seconds] [url] [still]
//     Joins the first hosted game. Walks a circle round the host (or, with "still", stands 3m
//     from it so monsters can reach it), swings, hits the host's monsters that are near it
//     (hit claims) and claims every drop it is told about.
//
//   node test/coop-fake-partner.js host [seconds] [url]
//     Hosts a game. Stands near the guest with one zombie between them, which swings at the
//     guest, takes the guest's hit claims and dies (sending the kill). Drops gold and a bow
//     and grants claims.
//
// Prints what it received at the end. Defaults: 30 seconds, ws://localhost:8099.

const WebSocket = require('ws');
const readline = require('node:readline');

const mode = process.argv[2] === 'host' ? 'host' : 'guest';
const seconds = Number(process.argv[3] || 30);
const url = process.argv[4] || 'ws://localhost:8099';
const still = process.argv[5] === 'still';
const ws = new WebSocket(url);
const started = Date.now();
const counts = {};
const log = [];
let partner = null; // latest partner snapshot
let running = false;
let seq = 0;
let attacks = 0;
let me = { x: 0, y: 1.1, z: 0, r: 0, area: 0 };
const zombie = { i: 1, hp: 60, mhp: 60, d: 0, atk: 0, x: 0, y: 1.1, z: 0 };
const claimed = new Set();
let manualPose = null;
let fixtures = [];
let passive = false;

// Optional JSON commands on stdin let a DevTest run drive area splits, minions and attacks:
// {"pose":{"area":0,"x":3,"y":1.1,"z":0}} / {"pose":null} resumes following.
// {"entities":[...]} adds snapshot entities; {"event":{...}} sends one cev.
// {"passive":true} stops hit/loot claims. {"inspect":true} reports the latest state.
readline.createInterface({ input: process.stdin }).on('line', (line) => {
  try {
    const command = JSON.parse(line);
    if ('pose' in command) manualPose = command.pose;
    if (Array.isArray(command.entities)) fixtures = command.entities;
    if ('passive' in command) passive = !!command.passive;
    if (command.event && running) send({ type: 'cev', ...command.event });
    if (command.inspect) console.log(JSON.stringify({ counts, partner }));
  } catch (e) { console.error(`invalid command: ${e.message}`); }
});

const now = () => (Date.now() - started) / 1000;
const send = (o) => ws.send(JSON.stringify(o.type === 'cev' ? { type: 'cev', ar: me.area, ...o } : o));
const note = (s) => {
  log.push(`${now().toFixed(1)}s ${s}`);
};

ws.on('open', () => send({ type: 'hello', role: 'player', name: mode === 'host' ? 'FakeHost' : 'FakeGuest' }));

ws.on('message', (raw) => {
  const text = raw.toString();
  if (text.startsWith('{"type":"co",')) {
    partner = JSON.parse(text);
    counts.snapshots = (counts.snapshots || 0) + 1;
    counts.minionsSeen = Math.max(counts.minionsSeen || 0, (partner.e || []).filter(e => e.i >= 1000000).length);
    if (counts.snapshots === 1) note(`first partner snapshot: area ${partner.area}, at ${partner.p.x},${partner.p.z}, ${(partner.e || []).length} enemies`);
    return;
  }
  const msg = JSON.parse(text);
  if (msg.type === 'cev') {
    counts[msg.k] = (counts[msg.k] || 0) + 1;
    onEvent(msg);
    return;
  }
  if (msg.type === 'welcome') {
    if (!msg.granted) return note(`denied: ${msg.reason}`);
    send(mode === 'host' ? { type: 'coopHost', name: 'FakeHost' } : { type: 'coopList' });
  } else if (msg.type === 'lobby' && mode === 'guest' && !running) {
    if (msg.hosts.length > 0) send({ type: 'coopJoin', id: msg.hosts[0].id, name: 'FakeGuest' });
  } else if (msg.type === 'coop') {
    note(`coop ${msg.state} ${msg.coopRole || ''} ${msg.partnerName || msg.reason || ''}`);
    if (msg.state === 'started') start();
    if (msg.state === 'ended') finish();
  }
});

function onEvent(e) {
  if (e.k === 'atk') note(`enemy ${e.id} attack kind ${e.n} ${e.f & 16 ? 'AT ME' : 'at partner'} dmg ${e.a.toFixed(1)}`);
  if (['skl', 'bmv', 'dmg', 'fx', 'wld'].includes(e.k)) note(`${e.k} enemy ${e.id || 0} area ${e.ar || 0}`);
  if (e.k === 'kill') note(`kill shared: kind ${e.ek} xp ${e.xp}`);
  if (e.k === 'drop') {
    note(`drop ${e.id}: ${e.it.n}`);
    if (mode === 'guest' && !passive) setTimeout(() => send({ type: 'cev', k: 'claim', id: e.id }), 1500);
  }
  if (e.k === 'got' || e.k === 'deny' || e.k === 'gone') note(`${e.k} ${e.id}`);
  if (e.k === 'hit' && mode === 'host' && e.id === zombie.i && !zombie.d) {
    zombie.hp -= e.a;
    note(`hit on zombie for ${e.a.toFixed(1)} -> ${Math.max(0, zombie.hp).toFixed(1)}`);
    if (zombie.hp <= 0) {
      zombie.d = 1;
      send({ type: 'cev', k: 'kill', ek: 0, r: 1, xp: 20, x: zombie.x, y: zombie.y, z: zombie.z });
      dropLoot();
    }
  }
  if (e.k === 'claim' && mode === 'host') {
    const ok = !claimed.has(e.id);
    claimed.add(e.id);
    send({ type: 'cev', k: ok ? 'got' : 'deny', id: e.id });
    if (ok) send({ type: 'cev', k: 'gone', id: e.id });
    note(`claim ${e.id} -> ${ok ? 'granted' : 'denied'}`);
  }
  if (e.k === 'put' && mode === 'host') note(`partner put down ${e.it.n}`);
}

let dropped = false;
function dropLoot() {
  if (dropped) return;
  dropped = true;
  const at = (dx) => ({ x: zombie.x + dx, y: 0.05, z: zombie.z });
  send({ type: 'cev', k: 'drop', id: 9001, a: 25, ...at(0.6), f: 32, tx: zombie.x, ty: zombie.y, tz: zombie.z,
    it: { i: 'gold_coins', n: '25 Gold', ct: 1, t: 11, w: 1, h: 1, wp: 1, tn: 'FFD64CFF', m: [], mt: [] } });
  send({ type: 'cev', k: 'drop', id: 9002, a: 0, ...at(-0.6), f: 32, tx: zombie.x, ty: zombie.y, tz: zombie.z,
    it: { i: 'short_bow', n: 'Fake Bow', ct: 1, ilvl: 5, t: 7, w: 2, h: 3, wp: 5, tn: '9E7042FF', m: [], mt: [] } });
}

function start() {
  running = true;
  setInterval(tick, 50);
  setInterval(act, 500);
  if (mode === 'host') setTimeout(dropLoot, 20000); // loot even if the zombie survives
  setTimeout(finish, seconds * 1000);
}

// 20 snapshots a second: a slow circle round the partner (in the partner's area).
function tick() {
  // A host has no world of its own here: it waits to see where the guest is, then stands there.
  if (mode === 'host' && !partner) return;
  if (partner) {
    const a = now() * 0.6;
    const cx = partner.p.x;
    const cz = partner.p.z;
    if (mode === 'host') {
      // Stand still 4m from where the guest first was, the zombie halfway between.
      if (!me.placed) {
        Object.assign(me, { x: cx + 4, y: partner.p.y, z: cz, area: partner.area, placed: true });
        Object.assign(zombie, { x: cx + 2, y: partner.p.y, z: cz + 1.5 });
      }
    } else if (still) {
      if (me.area !== partner.area) me.placed = false;
      if (!me.placed) Object.assign(me, { x: cx + 3, y: partner.p.y, z: cz, r: 90, area: partner.area, placed: true });
    } else {
      Object.assign(me, { x: cx + Math.cos(a) * 4, y: partner.p.y, z: cz + Math.sin(a) * 4, r: (-a * 180) / Math.PI, area: partner.area });
    }
  }
  if (manualPose) Object.assign(me, manualPose);
  const s = {
    type: 'co', seq: ++seq, t: now(), area: me.area,
    p: { i: 0, x: me.x, y: me.y, z: me.z, r: me.r, hp: 50, mhp: 50, atk: attacks, ap: 0 },
    hud: { lv: 3, hp: 50, mhp: 50, mp: 20, mmp: 20 },
    eq: [],
  };
  s.e = mode === 'host' ? [{ i: zombie.i, x: zombie.x, y: zombie.y, z: zombie.z, r: 270, hp: Math.max(0, zombie.hp), mhp: zombie.mhp, d: zombie.d, atk: zombie.atk, k: 0, ch: 1 }, ...fixtures] : fixtures;
  ws.send(JSON.stringify(s));
}

// Twice a second: swing, and hit (guest) / attack the guest (host).
function act() {
  if (!partner || passive) return;
  attacks++;
  if (mode === 'guest') {
    for (const e of partner.e || []) {
      const d = Math.hypot(e.x - me.x, e.z - me.z);
      if (!e.d && d < 12) {
        send({ type: 'cev', k: 'hit', id: e.i, a: 4, dt: 0, f: 4 });
        counts.hitsSent = (counts.hitsSent || 0) + 1;
        break;
      }
    }
  } else if (!zombie.d && attacks % 3 === 0) {
    zombie.atk++;
    send({ type: 'cev', k: 'atk', ar: me.area, id: zombie.i, ek: 0, n: 0, f: 16, a: 3, r: 2.3, x: zombie.x, y: zombie.y, z: zombie.z, tx: partner.p.x, ty: partner.p.y, tz: partner.p.z });
  }
}

function finish() {
  console.log(log.join('\n'));
  console.log('received:', JSON.stringify(counts));
  process.exit(0);
}

ws.on('error', (e) => {
  console.error('socket error', e.message);
  process.exit(1);
});
