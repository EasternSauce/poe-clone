const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { test } = require('node:test');

const html = fs.readFileSync('Assets/WebGLTemplates/FullWindow/index.html', 'utf8');
const source = html.slice(html.indexOf('      function watchForUpdates()'), html.indexOf('      // Cover and freeze immediately'));
function setup() {
  const events = {}, requests = [], messages = [], timers = new Map();
  let nextTimer = 0, reloads = 0, covered = false;
  const context = {
    v: '?v=old', BUILD_ID: 'old', updating: false, AbortController,
    document: { hidden: false, addEventListener: (name, handler) => events[name] = handler,
      getElementById: () => ({ textContent: '' }) },
    window: { addEventListener: (name, handler) => events[name] = handler },
    unityInstance: { SendMessage: (...args) => messages.push(args.at(-1)) },
    detail: { textContent: '' },
    loading: { classList: { remove: () => covered = true, add: () => covered = false } },
    setInterval: handler => events.poll = handler,
    setTimeout: handler => { timers.set(++nextTimer, handler); return nextTimer; },
    clearTimeout: id => timers.delete(id),
    reloadForUpdate: () => { context.updating = true; reloads++; },
    fetch: (url, options) => new Promise((resolve, reject) => {
      options.signal.addEventListener('abort', () => reject(new Error('aborted')));
      requests.push({ resolve: build => resolve({ ok: true, json: async () => ({ build }) }), signal: options.signal });
    })
  };
  vm.createContext(context);
  vm.runInContext(source + '\nwatchForUpdates();', context);
  return { context, events, requests, messages, timers,
    get reloads() { return reloads; }, get covered() { return covered; } };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('wake replaces a suspended check and keeps play paused through forced update', async () => {
  const s = setup();
  s.context.document.hidden = true; s.events.visibilitychange();
  assert.equal(s.messages.at(-1), '1');
  s.context.document.hidden = false; s.events.visibilitychange();
  assert.equal(s.requests[0].signal.aborted, true);
  assert.equal(s.requests.length, 2);
  assert.equal(s.covered, true);
  s.events.focus(); s.events.pageshow();
  assert.equal(s.requests.length, 2, 'Duplicate wake events share the active check');
  s.requests[1].resolve('new'); await settle();
  assert.equal(s.reloads, 1);
  assert.equal(s.messages.includes('0'), false, 'Never unfreeze the old build during the save/reload');
  assert.equal(s.covered, true);
});

test('unchanged build resumes play and continues periodic checks', async () => {
  const s = setup();
  s.requests[0].resolve('old'); await settle();
  s.events.focus();
  s.requests[1].resolve('old'); await settle();
  assert.equal(s.messages.at(-1), '0');
  assert.equal(s.covered, false);
  s.events.poll(); assert.equal(s.requests.length, 3);
  s.requests[2].resolve('new'); await settle(); assert.equal(s.reloads, 1);
});

test('offline wake times out, releases pause and can retry on reconnect', async () => {
  const s = setup();
  s.events.focus();
  for (const timeout of s.timers.values()) timeout();
  await settle();
  assert.equal(s.messages.at(-1), '0');
  assert.equal(s.covered, false);
  s.events.online();
  assert.equal(s.requests.length, 2);
  s.requests[1].resolve('new'); await settle(); assert.equal(s.reloads, 1);
});

test('a check finishing while hidden cannot release the wake pause', async () => {
  const s = setup();
  s.context.document.hidden = true; s.events.visibilitychange();
  s.requests[0].resolve('old'); await settle();
  assert.equal(s.messages.includes('0'), false);
  s.context.document.hidden = false; s.events.visibilitychange();
  s.requests[1].resolve('old'); await settle();
  assert.equal(s.messages.at(-1), '0');
});
