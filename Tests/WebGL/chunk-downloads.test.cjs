const vm = require('node:vm');
const fs = require('node:fs');
const { webcrypto, createHash } = require('node:crypto');
const assert = require('node:assert/strict');
(async () => {
 const files = [Buffer.alloc(524288, 1), Buffer.alloc(524288, 2), Buffer.alloc(913, 3)];
 const hashes = files.map(b => createHash('sha256').update(b).digest('hex'));
 const saved = new Map(); let calls = 0, concurrent = 0, peak = 0, corrupt = true;
 const cache = { match: async u => saved.get(u)?.clone(), put: async (u,r) => saved.set(u,r.clone()), delete: async u => saved.delete(u) };
 const nativeFetch = async (url,options) => {
  calls++; concurrent++; peak=Math.max(peak,concurrent);
  await new Promise(r=>setTimeout(r, url.includes(hashes[0]) ? 30 : 1)); concurrent--;
  const index=hashes.findIndex(h=>url.includes(h));
  if(index<0) return new Response('ordinary');
  if(index===1 && corrupt) {corrupt=false;return new Response(Buffer.alloc(files[1].length));}
  return new Response(files[index]);
 };
 const context={window:{fetch:nativeFetch},document:{baseURI:'https://example.org/game/'}, caches:{open:async()=>cache},crypto:webcrypto,URL,Response,ReadableStream,Uint8Array,Map,Array,AbortController,setTimeout,clearTimeout,console};
 vm.createContext(context);vm.runInContext(fs.readFileSync('Assets/WebGLTemplates/FullWindow/chunk-downloads.js','utf8'),context);
 const manifest={'Build/WebGL.data.unityweb':{size:1049489,hashes}};
 const restore=context.installChunkDownloads(manifest,'?v=one');
 const result=await context.window.fetch('Build/WebGL.data.unityweb?v=one',{method:'GET'});
 assert.equal(result.headers.get('Content-Length'),'1049489');
 assert.deepEqual(Buffer.from(await result.arrayBuffer()),Buffer.concat(files));
 assert.equal(calls,4);assert.ok(peak>1 && peak<=6);
 assert.equal(await (await context.window.fetch('version.json')).text(),'ordinary');
 restore();assert.equal(context.window.fetch,nativeFetch);
 const before=calls;const restore2=context.installChunkDownloads(manifest,'?v=two');
 assert.deepEqual(Buffer.from(await (await context.window.fetch('Build/WebGL.data.unityweb?v=two')).arrayBuffer()),Buffer.concat(files));
 assert.equal(calls,before);restore2();
 // Background staging leaves gameplay fetch intact and awaits durable cache writes.
 saved.clear();
 const staged=context.installChunkDownloads(manifest,'?v=staged',null,true);
 assert.equal(context.window.fetch,nativeFetch);
 await staged.prefetch();assert.equal(saved.size,hashes.length);
 const afterStage=calls;
 const restoreStaged=context.installChunkDownloads(manifest,'?v=staged');
 assert.deepEqual(Buffer.from(await (await context.window.fetch('Build/WebGL.data.unityweb?v=staged')).arrayBuffer()),Buffer.concat(files));
 assert.equal(calls,afterStage,'Restart reuses the staged cache');restoreStaged();
 // Storage unavailable must still download successfully.
 context.caches.open=async()=>{throw Error('storage blocked')};
 await assert.rejects(context.installChunkDownloads(manifest,'?v=no-storage',null,true).prefetch(),/storage unavailable/);
 const restore3=context.installChunkDownloads(manifest,'?v=three');
 assert.deepEqual(Buffer.from(await (await context.window.fetch('Build/WebGL.data.unityweb?v=three')).arrayBuffer()),Buffer.concat(files));
 restore3();
 console.log('PASS: parallel ordered reconstruction, corrupt-piece retry, cache reuse across deploy IDs, ordinary fetch passthrough, fetch restoration, unavailable storage.');
})().catch(e=>{console.error(e);process.exitCode=1});
