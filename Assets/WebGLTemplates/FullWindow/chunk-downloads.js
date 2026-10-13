// Intercept only this build's two large Unity requests. Unity still owns decompression,
// progress reporting and startup; other fetches (saves, music, updates) are untouched.
function installChunkDownloads(manifest, version) {
  var originalFetch = window.fetch;
  var entries = new Map(Object.keys(manifest).map(function (path) {
    return [new URL(path + version, document.baseURI).href, manifest[path]];
  }));
  var cachePromise = typeof caches !== "undefined"
    ? caches.open("poe-download-chunks-v1").catch(function () { return null; })
    : Promise.resolve(null);
  var active = 0, waiting = [];
  async function slot(work) {
    if (active >= 6) await new Promise(function (resolve) { waiting.push(resolve); });
    else active++;
    try { return await work(); }
    finally { if (waiting.length) waiting.shift()(); else active--; }
  }
  async function verified(response, hash, size) {
    if (!response || !response.ok) throw new Error("Missing download piece");
    var bytes = new Uint8Array(await response.arrayBuffer());
    if (bytes.length !== size) throw new Error("Incomplete download piece");
    var digest = await crypto.subtle.digest("SHA-256", bytes);
    var actual = Array.from(new Uint8Array(digest), function (n) { return n.toString(16).padStart(2, "0"); }).join("");
    if (actual !== hash) throw new Error("Download piece checksum mismatch");
    return bytes;
  }
  async function piece(hash, size, signal) {
    var url = new URL("Build/chunks/" + hash + ".bin", document.baseURI).href;
    var cache = await cachePromise;
    if (cache) {
      try {
        var saved = await cache.match(url);
        if (saved) return await verified(saved, hash, size);
      } catch (error) { await cache.delete(url).catch(function () {}); }
    }
    return slot(async function () {
      for (var attempt = 0; attempt < 3; attempt++) {
        var controller = new AbortController();
        var abort = function () { controller.abort(); };
        if (signal) { signal.addEventListener("abort", abort); if (signal.aborted) abort(); }
        var timer = setTimeout(abort, 20000);
        try {
          var response = await originalFetch(url, { signal: controller.signal, cache: attempt ? "reload" : "default" });
          var bytes = await verified(response, hash, size);
          if (cache) await cache.put(url, new Response(bytes)).catch(function () {});
          return bytes;
        } catch (error) {
          if (attempt === 2 || signal && signal.aborted) throw error;
        } finally {
          clearTimeout(timer);
          if (signal) signal.removeEventListener("abort", abort);
        }
      }
    });
  }
  var wrappedFetch = function (input, options) {
    var url = new URL(typeof input === "string" ? input : input.url, document.baseURI).href;
    var entry = entries.get(url);
    if (!entry || options && options.method && options.method !== "GET") return originalFetch(input, options);
    var controller = new AbortController();
    var signal = options && options.signal;
    var abort = function () { controller.abort(); };
    if (signal) { signal.addEventListener("abort", abort); if (signal.aborted) abort(); }
    // Start in manifest order, with six requests total across both files. Emit in the
    // original byte order so Unity's normal streaming progress and gzip decoding work.
    var parts = entry.hashes.map(function (hash, index) {
      var pending = piece(hash, Math.min(524288, entry.size - index * 524288), controller.signal);
      pending.catch(function () {}); // Later pieces can fail before the stream reaches them.
      return pending;
    });
    var index = 0;
    return Promise.resolve(new Response(new ReadableStream({
      async pull(stream) {
        try {
          stream.enqueue(await parts[index++]);
          if (index === parts.length) {
            stream.close();
            if (signal) signal.removeEventListener("abort", abort);
          }
        } catch (error) {
          controller.abort();
          if (signal) signal.removeEventListener("abort", abort);
          stream.error(error);
        }
      },
      cancel() { controller.abort(); if (signal) signal.removeEventListener("abort", abort); }
    }), { headers: { "Content-Length": String(entry.size), "Content-Type": "application/octet-stream" } }));
  };
  window.fetch = wrappedFetch;
  return function () { if (window.fetch === wrappedFetch) window.fetch = originalFetch; };
}
