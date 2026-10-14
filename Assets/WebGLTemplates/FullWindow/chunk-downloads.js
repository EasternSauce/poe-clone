// Only intercept this build's verified files. Saves, music and update polling use native fetch.
function installChunkDownloads(manifest, version, progress, background) {
  var originalFetch = window.fetch.bind(window);
  var previousFetch = window.fetch;
  var base = new URL('.', document.baseURI).href;
  var sources = [base];
  if (base === 'https://easternsauce.github.io/poe-clone-web/') {
    sources.push('https://raw.githubusercontent.com/EasternSauce/poe-clone-web/master/',
      'https://cdn.jsdelivr.net/gh/EasternSauce/poe-clone-web@master/');
  }
  var entries = new Map(Object.keys(manifest).map(function (path) {
    return [new URL(path + version, base).href, { path: path, info: manifest[path] }];
  }));
  var cachePromise = typeof caches !== 'undefined'
    ? caches.open('poe-download-chunks-v1').catch(function () { return null; }) : Promise.resolve(null);
  var active = 0, waiting = [], route = null, bulkRoute = null;
  var hedges = 0, nextAlternative = 0;
  var unavailable = new Set();
  function slot(priority, work) {
    return new Promise(function (resolve, reject) {
      waiting.push({ priority: priority, work: work, resolve: resolve, reject: reject });
      waiting.sort(function (a, b) { return a.priority - b.priority; });
      pump();
    });
  }
  function pump() {
    while (active < 4 && waiting.length) {
      var job = waiting.shift();
      active++;
      (function (job) {
        Promise.resolve().then(job.work).then(job.resolve, job.reject).finally(function () { active--; pump(); });
      })(job);
    }
  }
  async function check(bytes, hash, size) {
    if (bytes.length !== size) throw new Error('Incomplete download piece');
    var digest = await crypto.subtle.digest('SHA-256', bytes);
    var actual = Array.from(new Uint8Array(digest), function (n) { return n.toString(16).padStart(2, '0'); }).join('');
    if (actual !== hash) throw new Error('Download checksum mismatch');
    return bytes;
  }
  // A transfer may take minutes on a slow connection. Abort only if it stops making
  // progress, rather than throwing away healthy transfers after twenty seconds.
  async function read(url, signal, update) {
    var controller = new AbortController(), timer, reader;
    var abort = function () { controller.abort(); };
    function touch() { clearTimeout(timer); timer = setTimeout(abort, 45000); }
    if (signal) { signal.addEventListener('abort', abort); if (signal.aborted) abort(); }
    try {
      touch();
      var response = await originalFetch(url, { signal: controller.signal });
      if (!response.ok) {
        var error = new Error('HTTP ' + response.status + ' for ' + url);
        error.status = response.status;
        throw error;
      }
      reader = response.body.getReader();
      var parts = [], length = 0;
      while (true) {
        var part = await reader.read();
        if (part.done) break;
        touch(); parts.push(part.value); length += part.value.length;
        if (update) update(length);
      }
      var bytes = new Uint8Array(length), offset = 0;
      parts.forEach(function (part) { bytes.set(part, offset); offset += part.length; });
      return bytes;
    } finally {
      clearTimeout(timer); controller.abort();
      if (reader) reader.releaseLock();
      if (signal) signal.removeEventListener('abort', abort);
    }
  }
  async function chooseRoute(hash, size) {
    var controllers = sources.map(function () { return new AbortController(); });
    try {
      var winner = await Promise.any(sources.map(async function (source, index) {
        var bytes = await read(source + 'Build/chunks/' + hash + '.bin', controllers[index].signal);
        await check(bytes, hash, size);
        return { source: source, hash: hash, bytes: bytes };
      }));
      console.log('[Loading] Download route: ' + winner.source);
      return winner;
    } finally { controllers.forEach(function (controller) { controller.abort(); }); }
  }
  // A nearby small-script cache hit does not measure large-file throughput. Also
  // allow at most two extra requests to rescue slow pieces without flooding mobile
  // connections. Only a fully verified winner can replace the current route.
  function fetchPiece(winner, path, hash, size, signal, update) {
    if (sources.length < 2 || size < 65536)
      return read(winner.source + path, signal, update).then(function (bytes) { return check(bytes, hash, size); });
    return new Promise(function (resolve, reject) {
      var controllers = [], pending = 0, finished = false, alternativeStarted = false, highWater = 0, lastError;
      var timer;
      function finish(error, bytes, source) {
        if (finished) return;
        finished = true; clearTimeout(timer);
        signal.removeEventListener('abort', abort);
        controllers.forEach(function (controller) { controller.abort(); });
        if (error) reject(error);
        else {
          if (source !== winner.source) console.log('[Loading] Faster piece route: ' + source);
          winner.source = source;
          resolve(bytes);
        }
      }
      function abort() { finish(new Error('Download cancelled')); }
      function start(source, hedge) {
        var controller = new AbortController(); controllers.push(controller); pending++;
        if (hedge) hedges++;
        read(source + path, controller.signal, function (count) {
          highWater = Math.max(highWater, count); update(highWater);
        }).then(function (bytes) { return check(bytes, hash, size); })
          .then(function (bytes) { finish(null, bytes, source); })
          .catch(function (error) {
            lastError = error; pending--;
            if (error.status === 404) unavailable.add(source);
            if (!finished && pending === 0 && !alternate()) finish(lastError);
          }).finally(function () { if (hedge) hedges--; });
      }
      function alternate() {
        if (finished || alternativeStarted || hedges >= 2 || signal.aborted) return false;
        var alternatives = sources.filter(function (source) { return source !== winner.source && !unavailable.has(source); });
        if (!alternatives.length) return false;
        alternativeStarted = true;
        start(alternatives[nextAlternative++ % alternatives.length], true);
        return true;
      }
      signal.addEventListener('abort', abort);
      if (signal.aborted) { abort(); return; }
      timer = setTimeout(alternate, 3000);
      start(winner.source, false);
    });
  }
  async function piece(hash, size, priority, signal, update) {
    var path = 'Build/chunks/' + hash + '.bin', key = new URL(path, base).href;
    var cache = await cachePromise;
    if (cache) {
      try {
        var saved = await cache.match(key);
        if (saved) { var bytes = await check(new Uint8Array(await saved.arrayBuffer()), hash, size); update(size); return bytes; }
      } catch (error) { await cache.delete(key).catch(function () {}); }
    }
    return slot(priority, async function () {
      if (signal.aborted) throw new Error('Download cancelled');
      var winner;
      if (priority >= 0 && size >= 262144) {
        if (!bulkRoute) bulkRoute = chooseRoute(hash, size);
        winner = await bulkRoute;
      } else {
        if (!route) route = chooseRoute(hash, size);
        winner = await route;
      }
      var order = [winner.source].concat(sources.filter(function (source) { return source !== winner.source; }));
      var lastError;
      for (var source of order) {
        for (var attempt = 0; attempt < 2; attempt++) {
          try {
            var bytes = winner.hash === hash && winner.source === source ? winner.bytes :
              source === order[0] ? await fetchPiece(winner, path, hash, size, signal, update) : await read(source + path, signal, update);
            await check(bytes, hash, size);
            update(size);
            if (cache) await cache.put(key, new Response(bytes)).catch(function (error) { if (background) throw error; });
            return bytes;
          } catch (error) {
            lastError = error; if (signal.aborted) throw error; update(0);
            if (error.status === 404) { unavailable.add(source); break; }
          }
        }
      }
      throw lastError;
    });
  }
  async function whole(entry, signal, update) {
    var cache = await cachePromise;
    var key = new URL(entry.path + version, base).href;
    if (cache) {
      var saved = await cache.match(key);
      if (saved) {
        var cached = new Uint8Array(await saved.arrayBuffer());
        try {
          if (entry.info.encoding === 'gzip-pieces') {
            await check(cached, entry.info.fallbackHash, entry.info.fallbackSize);
            return cached;
          }
        } catch (error) { await cache.delete(key); }
      }
    }
    var lastError;
    var selection = bulkRoute || route;
    var winner = selection ? await selection.catch(function () { return null; }) : null;
    var order = winner ? [winner.source].concat(sources.filter(function (source) { return source !== winner.source; })) : sources;
    for (var source of order) {
      try {
        var bytes = await read(source + entry.path + version, signal, update);
        if (entry.info.encoding === 'gzip-pieces') {
          await check(bytes, entry.info.fallbackHash, entry.info.fallbackSize);
          if (cache) await cache.put(key, new Response(bytes));
          return bytes;
        }
        if (bytes.length !== entry.info.size) throw new Error('Full download size mismatch');
        for (var index = 0; index < entry.info.hashes.length; index++) {
          var part = bytes.subarray(index * 524288, Math.min(bytes.length, (index + 1) * 524288));
          await check(part, entry.info.hashes[index], part.length);
        }
        var cache = await cachePromise;
        if (cache) await Promise.all(entry.info.hashes.map(function (hash, index) {
          var part = bytes.slice(index * 524288, Math.min(bytes.length, (index + 1) * 524288));
          return cache.put(new URL('Build/chunks/' + hash + '.bin', base).href, new Response(part))
            .catch(function (error) { if (background) throw error; });
        }));
        return bytes;
      } catch (error) { lastError = error; if (signal && signal.aborted) throw error; }
    }
    throw lastError;
  }
  async function download(url, entry, signal) {
    var controller = new AbortController(), loaded = entry.info.hashes.map(function () { return 0; });
    var abort = function () { controller.abort(); };
    if (signal) { signal.addEventListener('abort', abort); if (signal.aborted) abort(); }
    var transferSize = entry.info.transferSize || entry.info.size;
    function report() { if (progress) progress(url, loaded.reduce(function (a, b) { return a + b; }, 0), transferSize); }
    try {
      if (entry.info.encoding === 'gzip-pieces' && typeof DecompressionStream === 'undefined')
        throw new Error('Independent gzip decoding unavailable; using the regular archive');
      var parts = await Promise.all(entry.info.hashes.map(function (hash, index) {
        var size = entry.info.parts ? entry.info.parts[index].size : Math.min(524288, entry.info.size - index * 524288);
        return piece(hash, size, transferSize < 524288 ? -1 : index,
          controller.signal, function (count) { loaded[index] = count; report(); });
      }));
      var bytes = new Uint8Array(entry.info.size), offset = 0;
      for (var index = 0; index < parts.length; index++) {
        var part = parts[index];
        if (entry.info.encoding === 'gzip-pieces') {
          var descriptor = entry.info.parts[index];
          part = new Uint8Array(await new Response(new Response(part).body.pipeThrough(new DecompressionStream('gzip'))).arrayBuffer());
          if (part.length !== descriptor.length || descriptor.offset !== offset)
            throw new Error('Invalid decoded asset piece');
        }
        bytes.set(part, offset); offset += part.length;
      }
      if (offset !== bytes.length) throw new Error('Incomplete reconstructed file');
      return new Response(bytes, { headers: { 'Content-Length': String(bytes.length) } });
    } catch (error) {
      controller.abort();
      if (signal && signal.aborted) throw error;
      console.warn('[Loading] Piece download failed; trying verified full file:', entry.path, error);
      // Do not expose a broken synthetic stream to Unity: return a complete, verified
      // response, falling back to the regular release file if the piece route fails.
      var bytes = await whole(entry, signal, function (count) { if (progress) progress(url, count, entry.info.fallbackSize || entry.info.size); });
      return new Response(bytes, { headers: { 'Content-Length': String(bytes.length) } });
    } finally { if (signal) signal.removeEventListener('abort', abort); }
  }
  var wrappedFetch = function (input, options) {
    var url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url, base).href;
    var entry = entries.get(url);
    var method = options && options.method || input && input.method || 'GET';
    if (!entry || method !== 'GET') return originalFetch(input, options);
    return download(url, entry, options && options.signal || input && input.signal);
  };
  if (!background) window.fetch = wrappedFetch;
  var restore = function () { if (window.fetch === wrappedFetch) window.fetch = previousFetch; };
  // Warm verified pieces without replacing fetch or retaining a second game's files in RAM.
  restore.prefetch = async function () {
    if (!await cachePromise) throw new Error('Background update storage unavailable');
    var controller = new AbortController();
    try {
      for (var entry of entries.values()) {
        if (entry.info.encoding === 'gzip-pieces' && typeof DecompressionStream === 'undefined') {
          await whole(entry, controller.signal, function () {});
          continue;
        }
        try {
          await Promise.all(entry.info.hashes.map(function (hash, index) {
            var size = entry.info.parts ? entry.info.parts[index].size : Math.min(524288, entry.info.size - index * 524288);
            return piece(hash, size, index, controller.signal, function () {}).then(function () {});
          }));
        } catch (error) {
          await whole(entry, controller.signal, function () {});
        }
      }
    } finally { controller.abort(); }
  };
  return restore;
}
