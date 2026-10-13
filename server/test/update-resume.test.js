'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const bridgeSource = fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/WebSocketBridge.jslib'), 'utf8');

function harness() {
  const library = {}, storage = new Map(), messages = [], acknowledgements = [];
  let finishSync;
  const context = {
    LibraryManager: { library }, mergeInto: Object.assign, UTF8ToString: value => value,
    sessionStorage: {
      getItem: key => storage.get(key) || null,
      setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key),
    },
    Date, JSON, console,
    FS: { syncfs: (populate, callback) => { assert.equal(populate, false); finishSync = callback; } },
    SendMessage: (...args) => messages.push(args),
    window: { poeUpdateSaved: error => acknowledgements.push(error) },
  };
  vm.runInNewContext(bridgeSource, context);
  return { library, storage, messages, acknowledgements, context, finish: error => finishSync(error) };
}

test('update handoff waits for durable save and is consumed exactly once in the same tab', () => {
  const h = harness();
  const json = JSON.stringify({ version: 1, profile: 'test', area: 2, position: { x: 1, y: 2, z: 3 }, party: 'party', host: false });
  h.library.UpdateBridge_SaveResume(json);
  assert.equal(h.acknowledgements.length, 0);
  assert.equal(h.storage.size, 0);
  h.finish(null);
  assert.deepEqual(h.acknowledgements, ['']);
  h.library.UpdateBridge_ReadResume();
  assert.deepEqual(h.messages.at(-1), ['GameSessionController', 'ResumeAfterUpdate', json]);
  h.library.UpdateBridge_ReadResume();
  assert.equal(h.messages.at(-1)[2], '');
  assert.equal(harness().storage.size, 0);
});

test('failed durable save reports an error and does not create a resume record', () => {
  const h = harness();
  h.library.UpdateBridge_SaveResume('{}');
  h.finish(new Error('storage unavailable'));
  assert.match(h.acknowledgements[0], /storage unavailable/);
  assert.equal(h.storage.size, 0);
});

test('stale update records fall back to the normal menu', () => {
  const h = harness();
  h.storage.set('PoeClone.UpdateResume', JSON.stringify({ savedAt: Date.now()-31*60*1000, json: '{}' }));
  h.library.UpdateBridge_ReadResume();
  assert.equal(h.messages.at(-1)[2], '');
  assert.equal(h.storage.size, 0);
});

test('partner update bridge invokes the page update flow', () => {
  const h = harness();
  let requested = 0;
  h.context.window.poeReloadForUpdate = () => requested++;
  h.library.UpdateBridge_RequestUpdate();
  assert.equal(requested, 1);
});

test('WebGL template JavaScript parses after build substitutions', () => {
  const html = fs.readFileSync(path.join(__dirname, '../../Assets/WebGLTemplates/FullWindow/index.html'), 'utf8');
  const script = html.match(/<script>([\s\S]*?)<\/script>/)[1]
    .replace(/\{\{\{[^]*?\}\}\}/g, 'test').replace(/^\s*#(?:if|else|endif).*$/gm, '');
  assert.doesNotThrow(() => new vm.Script(script));
});
