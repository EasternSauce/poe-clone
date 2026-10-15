// Failure-path checks use native Response/ReadableStream and real abort signals.
const vm = require('node:vm');
const fs = require('node:fs');
const {webcrypto, createHash} = require('node:crypto');
const assert = require('node:assert/strict');
const code = fs.readFileSync('Assets/WebGLTemplates/FullWindow/chunk-downloads.js', 'utf8');
const data = Buffer.alloc(700000, 7);
const hashes = [data.subarray(0,524288), data.subarray(524288)].map(bytes => createHash('sha256').update(bytes).digest('hex'));
const manifest = {'Build/WebGL.wasm.unityweb': {size:data.length,hashes}};
function environment(fetch, timers = setTimeout) {
 const ctx={window:{fetch},document:{baseURI:'https://easternsauce.github.io/poe-clone-web/'},crypto:webcrypto,URL,Response,ReadableStream,Uint8Array,Map,Array,AbortController,setTimeout:timers,clearTimeout,console};
 vm.createContext(ctx);vm.runInContext(code,ctx);return ctx;
}
function response(url) {
 const index=hashes.findIndex(hash=>url.includes(hash));
 return new Response(index<0?data:data.subarray(index*524288,Math.min(data.length,(index+1)*524288)));
}
async function load(ctx) {
 const restore=ctx.installChunkDownloads(manifest,'?v=test');
 try {assert.deepEqual(Buffer.from(await (await ctx.window.fetch('Build/WebGL.wasm.unityweb?v=test')).arrayBuffer()),data);}
 finally {restore();}
}
(async()=>{
 // A blocked Pages route must not prevent a valid alternative from supplying bytes.
 let alternative=0, pagesAborted=0;
 await load(environment(async(url,options)=>{
  if(url.startsWith('https://easternsauce.github.io'))return new Promise((resolve,reject)=>{
   options.signal.addEventListener('abort',()=>{pagesAborted++;reject(new Error('aborted'))});
  });
  alternative++;return response(url);
 }));
 assert.ok(alternative>0 && pagesAborted>0);
 console.log('PASS: blocked Pages route bypassed; losing requests cancelled');
 // Missing pieces on every origin must fall back to the exact full release file.
 let full=0;
 await load(environment(async url=>{
  if(url.includes('/chunks/'))return new Response('not published',{status:404});
  full++;return response(url);
 }));
 assert.equal(full,1);
 console.log('PASS: all chunk origins fail; verified full file loads');
 // A stale full file must be rejected and the next origin tried.
 let originals=0;
 await load(environment(async url=>{
  if(url.includes('/chunks/'))return new Response('',{status:404});
  originals++;return originals===1?new Response(Buffer.alloc(data.length)):response(url);
 }));
 assert.equal(originals,2);
 console.log('PASS: stale full file rejected; correct fallback accepted');
 // Emulate an inactivity deadline of 25 ms. Total transfer lasts over 60 ms but
 // delivers new bytes every 5 ms, so resetting the timer is essential.
 const tiny=Buffer.alloc(12,9),hash=createHash('sha256').update(tiny).digest('hex');
 let cancelled=0;
 const ctx=environment(async(url,options)=>new Response(new ReadableStream({
  start(stream){
   let index=0;
   const push=()=>{
    if(options.signal.aborted){cancelled++;stream.error(new Error('aborted'));return;}
    stream.enqueue(tiny.subarray(index,index+1));index++;
    if(index===tiny.length)stream.close();else setTimeout(push,5);
   };setTimeout(push,5);
  }
 })),(fn,ms)=>setTimeout(fn,ms===45000?25:ms));
 ctx.document.baseURI='https://example.org/';
 const restore=ctx.installChunkDownloads({'Build/WebGL.wasm.unityweb':{size:tiny.length,hashes:[hash]}},'?v=slow');
 const started=performance.now();
 assert.deepEqual(Buffer.from(await(await ctx.window.fetch('Build/WebGL.wasm.unityweb?v=slow')).arrayBuffer()),tiny);
 assert.ok(performance.now()-started>50);assert.equal(cancelled,0);restore();
 console.log('PASS: continuous slow transfer outlives the timeout without cancellation');
})().catch(error=>{console.error(error);process.exitCode=1});
