const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { test } = require('node:test');
const html = fs.readFileSync('Assets/WebGLTemplates/FullWindow/index.html', 'utf8');
const source = html.slice(html.indexOf('      function watchForUpdates()'), html.indexOf('      // Cover and freeze immediately'));
const settle = () => new Promise(resolve => setImmediate(resolve));
const release = build => ({ build, downloads: { 'Build/game.wasm': { size: 10, hashes: ['hash'] } } });
function setup() {
  const events = {}, requests = [], stages = [], timers = new Map();
  let id = 0, reloads = 0;
  const context = {
    v: '?v=old', BUILD_ID: 'old', updating: false, AbortController, console: { warn() {} },
    document: { hidden: false, addEventListener: (name, handler) => events[name] = handler },
    window: { addEventListener: (name, handler) => events[name] = handler },
    // Any attempt to pause or cover gameplay fails the test.
    unityInstance: { SendMessage() { assert.fail('Gameplay paused'); } },
    loading: { classList: { remove() { assert.fail('Gameplay covered'); } } },
    setInterval: handler => events.poll = handler,
    setTimeout: (handler, delay) => { timers.set(++id, { handler, delay }); return id; },
    clearTimeout: id => timers.delete(id),
    reloadForUpdate: () => { context.updating = true; reloads++; },
    installChunkDownloads: (manifest, version, progress, background) => {
      assert.equal(background, true);
      return { prefetch: () => new Promise((resolve, reject) => stages.push({ version, resolve, reject })) };
    },
    fetch: (url, options) => new Promise((resolve, reject) => {
      options.signal.addEventListener('abort', () => reject(new Error('aborted')));
      requests.push({ resolve: info => resolve({ ok: true, json: async () => typeof info === 'string' ? { build: info } : info }), signal: options.signal });
    })
  };
  vm.createContext(context); vm.runInContext(source + '\nwatchForUpdates();', context);
  return { context, events, requests, stages, timers, get reloads() { return reloads; },
    runTimers(delay) { for (const [key, timer] of timers) if (timer.delay === delay) { timers.delete(key); timer.handler(); } } };
}
test('wake replaces suspended checks and stages without pausing or covering play', async () => {
  const s = setup();
  s.context.document.hidden = true; s.events.visibilitychange();
  s.context.document.hidden = false; s.events.visibilitychange();
  assert.equal(s.requests[0].signal.aborted, true);
  s.events.focus(); s.events.pageshow(); assert.equal(s.requests.length, 2);
  s.requests[1].resolve(release('new')); await settle();
  assert.equal(s.reloads, 0); assert.equal(s.stages.length, 1);
  s.stages[0].resolve(); await settle(); s.runTimers(0);
  s.requests[2].resolve(release('new')); await settle(); assert.equal(s.reloads, 1);
});
test('unchanged build keeps playing; repeated checks share one background download', async () => {
  const s = setup(); s.requests[0].resolve('old'); await settle();
  assert.equal(s.stages.length, 0);
  s.events.poll(); s.requests[1].resolve(release('new')); await settle();
  s.events.poll(); s.requests[2].resolve(release('new')); await settle();
  assert.equal(s.stages.length, 1); assert.equal(s.reloads, 0);
});
test('offline checks and failed staging can retry without interrupting gameplay', async () => {
  const s = setup(); s.events.focus(); s.runTimers(10000); await settle();
  s.events.online(); s.requests[1].resolve(release('new')); await settle();
  s.stages[0].reject(new Error('offline')); await settle();
  assert.equal(s.reloads, 0); assert.equal(s.timers.size, 0);
  s.events.poll(); s.requests[2].resolve(release('new')); await settle(); assert.equal(s.stages.length, 2);
});
test('a newer deployment during staging must also finish downloading before restart', async () => {
  const s = setup(); s.requests[0].resolve(release('new')); await settle();
  s.stages[0].resolve(); await settle(); s.runTimers(0);
  s.requests[1].resolve(release('newer')); await settle();
  assert.equal(s.reloads, 0); assert.equal(s.stages[1].version, '?v=newer');
  s.stages[1].resolve(); await settle(); s.runTimers(0);
  s.requests[2].resolve(release('newer')); await settle(); assert.equal(s.reloads, 1);
});
test('completed staging while hidden waits for wake; partner updates use staging', async () => {
  const s = setup(); s.requests[0].resolve(release('new')); await settle();
  s.context.document.hidden = true; s.events.visibilitychange();
  s.stages[0].resolve(); await settle(); s.runTimers(0);
  assert.equal(s.requests.length, 1); assert.equal(s.reloads, 0);
  s.context.document.hidden = false; s.context.window.poeReloadForUpdate();
  s.requests[1].resolve(release('new')); await settle(); assert.equal(s.reloads, 1);
});
test('older releases without a background manifest retain compatible updating', async () => {
  const s = setup(); s.requests[0].resolve('new'); await settle(); assert.equal(s.reloads, 1);
});
