// Minimal WebSocket bridge for WebGL builds. Raw sockets aren't available inside the
// browser sandbox, so this wraps the browser's native WebSocket and reports events back
// into Unity via SendMessage, which the C# side (WebSocketClient.cs) listens for on a
// GameObject that must be named "NetworkClient" (WebSocketClient renames itself to that
// in Awake so this always resolves).
mergeInto(LibraryManager.library, {

  UpdateBridge_ReadResume: function () {
    var json = '';
    try {
      var stored = sessionStorage.getItem('PoeClone.UpdateResume');
      sessionStorage.removeItem('PoeClone.UpdateResume');
      var record = stored ? JSON.parse(stored) : null;
      // Never resume a stale update after the player has left the tab for a long time.
      if (record && Date.now() - record.savedAt >= 0 && Date.now() - record.savedAt < 1800000)
        json = record.json || '';
    } catch (e) { console.warn('Could not read update resume', e); }
    SendMessage('GameSessionController', 'ResumeAfterUpdate', json);
  },

  UpdateBridge_RequestUpdate: function () {
    if (window.poeReloadForUpdate) window.poeReloadForUpdate();
  },

  UpdateBridge_Completed: function () {
    var toast = document.createElement('div');
    toast.textContent = 'Game updated';
    toast.setAttribute('role', 'status');
    toast.style.cssText = 'position:absolute;left:50%;top:calc(env(safe-area-inset-top, 0px) + 20px);transform:translateX(-50%);z-index:20;padding:12px 24px;border-radius:8px;background:#211d16;color:#e8d5a5;font:600 20px system-ui;pointer-events:none;transition:opacity .5s';
    (document.getElementById('game-root') || document.body).appendChild(toast);
    setTimeout(function () { toast.style.opacity = '0'; }, 3500);
    setTimeout(function () { toast.remove(); }, 4000);
  },

  UpdateBridge_SaveResume: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    var complete = function (error) {
      try {
        if (error) throw error;
        if (json) sessionStorage.setItem('PoeClone.UpdateResume', JSON.stringify({ savedAt: Date.now(), json: json }));
        else sessionStorage.removeItem('PoeClone.UpdateResume');
      } catch (e) { error = e; }
      if (window.poeUpdateSaved) window.poeUpdateSaved(error ? String(error) : '');
    };
    // PlayerPrefs.Save writes the virtual filesystem. Wait for IndexedDB before reloading.
    FS.syncfs(false, complete);
  },

  WSConnect: function (urlPtr, goNamePtr) {
    var url = UTF8ToString(urlPtr);
    var goName = UTF8ToString(goNamePtr);

    if (!window.__unityWebSockets) window.__unityWebSockets = {};
    // Closing the tab or browser: let the game say goodbye (e.g. leave a co-op party) while
    // the socket can still send, so the server doesn't take it for a dropped connection.
    if (!window.__unityPageCloseHooked) {
      window.__unityPageCloseHooked = true;
      window.addEventListener('pagehide', function () {
        try { SendMessage(goName, 'OnWSPageClosing', ''); } catch (e) {}
      });
    }

    var existing = window.__unityWebSockets[goName];
    if (existing) {
      try { existing.onclose = null; existing.close(); } catch (e) {}
    }

    var socket;
    try {
      socket = new WebSocket(url);
    } catch (e) {
      SendMessage(goName, 'OnWSError', 'Failed to create WebSocket: ' + e.message);
      SendMessage(goName, 'OnWSClose', '');
      return;
    }

    socket.onopen = function () {
      SendMessage(goName, 'OnWSOpen', '');
    };
    socket.onmessage = function (evt) {
      if (typeof evt.data === 'string') {
        SendMessage(goName, 'OnWSMessage', evt.data);
      }
    };
    socket.onclose = function (evt) {
      var detail = evt.reason || (evt.code === 1006
        ? 'Connection lost unexpectedly'
        : 'Connection closed');
      SendMessage(goName, 'OnWSClose', detail + ' (code ' + evt.code + ').');
    };
    socket.onerror = function () {
      SendMessage(goName, 'OnWSError', 'WebSocket error');
    };

    window.__unityWebSockets[goName] = socket;
  },

  WSSend: function (goNamePtr, dataPtr) {
    var goName = UTF8ToString(goNamePtr);
    var data = UTF8ToString(dataPtr);
    var socket = window.__unityWebSockets && window.__unityWebSockets[goName];
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(data);
    }
  },

  WSBufferedAmount: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    var socket = window.__unityWebSockets && window.__unityWebSockets[goName];
    return socket ? socket.bufferedAmount : 0;
  },

  WSClose: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    var socket = window.__unityWebSockets && window.__unityWebSockets[goName];
    if (socket) {
      try { socket.onclose = null; socket.close(); } catch (e) {}
      delete window.__unityWebSockets[goName];
    }
  },

  WSRequestLocationSearch: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    SendMessage(goName, 'OnLocationSearch', window.location.search || '');
  }

});
