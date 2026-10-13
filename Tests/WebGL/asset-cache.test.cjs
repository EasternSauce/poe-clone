const vm = require('node:vm'), fs = require('node:fs'), assert = require('node:assert/strict');
const {webcrypto, createHash} = require('node:crypto');
const {gzipSync, gunzipSync} = require('node:zlib');
const digest = bytes => createHash('sha256').update(bytes).digest('hex');
function pack(files) {
 const compressed=files.map(bytes=>gzipSync(bytes));let offset=0;
 const parts=files.map((bytes,index)=>{const part={offset,length:bytes.length,size:compressed[index].length};offset+=bytes.length;return part});
 const raw=Buffer.concat(files),fallback=gzipSync(raw);
 return {raw,fallback,compressed,info:{encoding:'gzip-pieces',size:raw.length,transferSize:compressed.reduce((n,b)=>n+b.length,0),fallbackSize:fallback.length,fallbackHash:digest(fallback),hashes:compressed.map(digest),parts}};
}
(async()=>{
 const shared=Buffer.from('Unchanged textures, models, sound and metadata'.repeat(2000));
 const before=pack([Buffer.from('header A'),Buffer.from('old patch notes'),shared]);
 const after=pack([Buffer.from('a longer header B'),Buffer.from('updated patch notes with more text'),shared]);
 const storage=new Map(),network=new Map();
 [before,after].forEach(build=>build.compressed.forEach(bytes=>network.set(digest(bytes),bytes)));
 let calls=0,fullCalls=0,current=before;
 const cache={match:async key=>storage.get(key)?.clone(),put:async(key,response)=>storage.set(key,response.clone()),delete:async key=>storage.delete(key)};
 const native=async url=>{
  calls++;
  if(url.includes('/chunks/'))return new Response(network.get(url.split('/').pop().slice(0,-4)));
  fullCalls++;return new Response(current.fallback);
 };
 const ctx={window:{fetch:native},document:{baseURI:'https://example.org/'},caches:{open:async()=>cache},crypto:webcrypto,URL,Response,ReadableStream,DecompressionStream,Uint8Array,Map,Array,AbortController,setTimeout,clearTimeout,console};
 vm.createContext(ctx);vm.runInContext(fs.readFileSync('Assets/WebGLTemplates/FullWindow/chunk-downloads.js','utf8'),ctx);
 async function load(build,version){
  current=build;const restore=ctx.installChunkDownloads({'Build/WebGL.data.unityweb':build.info},'?v='+version);
  try{return Buffer.from(await(await ctx.window.fetch('Build/WebGL.data.unityweb?v='+version)).arrayBuffer());}finally{restore();}
 }
 assert.deepEqual(await load(before,'one'),before.raw);assert.equal(calls,3);
 let previous=calls;
 assert.deepEqual(await load(after,'two'),after.raw);
 assert.equal(calls-previous,2,'Only the changed header and notes should download; shifted unchanged file must be reused');
 assert.equal(fullCalls,0);
 console.log('PASS: independent gzip reconstruction; cached file reused despite changed preceding lengths and offsets');
 previous=calls;assert.deepEqual(await load(after,'three'),after.raw);assert.equal(calls,previous);
 console.log('PASS: identical asset archive across deploy IDs makes no network requests');
 delete ctx.DecompressionStream;
 assert.deepEqual(gunzipSync(await load(after,'legacy')),after.raw);assert.equal(fullCalls,1);
 console.log('PASS: browsers without native gzip decoding load the verified original archive');
 ctx.DecompressionStream=DecompressionStream;
 const invalid={...after,info:{...after.info,parts:after.info.parts.map((p,i)=>({...p,length:p.length+(i===0?1:0)}))}};
 assert.deepEqual(gunzipSync(await load(invalid,'invalid')),after.raw);assert.equal(fullCalls,2);
 console.log('PASS: malformed decoded piece falls back safely to the exact release file');
})().catch(error=>{console.error(error);process.exitCode=1});
