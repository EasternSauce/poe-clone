// Minimal WebSocket bridge for WebGL builds. Raw sockets aren't available inside the
// browser sandbox, so this wraps the browser's native WebSocket and reports events back
// into Unity via SendMessage, which the C# side (WebSocketClient.cs) listens for on a
// GameObject that must be named "NetworkClient" (WebSocketClient renames itself to that
// in Awake so this always resolves).
mergeInto(LibraryManager.library, {

  WSConnect: function (urlPtr, goNamePtr) {
    var url = UTF8ToString(urlPtr);
    var goName = UTF8ToString(goNamePtr);

    if (!window.__unityWebSockets) window.__unityWebSockets = {};

    var existing = window.__unityWebSockets[goName];
    if (existing) {
      try { existing.onclose = null; existing.close(); } catch (e) {}
    }

    var socket;
    try {
      socket = new WebSocket(url);
    } catch (e) {
      SendMessage(goName, 'OnWSError', 'Failed to create WebSocket: ' + e.message);
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
      SendMessage(goName, 'OnWSClose', String(evt.code || 0));
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
