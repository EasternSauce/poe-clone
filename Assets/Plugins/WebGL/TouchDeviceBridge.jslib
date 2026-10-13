// Tells the C# side (TouchMode.cs) whether the browser's primary pointer is a finger. Unity's
// own mobile check reads the user agent, which iPads disguise as a Mac; the pointer media query
// catches those, and stays false on touch laptops whose primary pointer is still the mouse.
mergeInto(LibraryManager.library, {

  ChatInputHide: function () {
    var field = Module.poeChatInput;
    if (field) {
      field.blur();
      field.style.display = "none";
    }
  },

  ChatInputSync: function (receiver, x, y, width, height, fontSize, draft) {
    var field = Module.poeChatInput;
    if (!field) {
      field = document.createElement("input");
      field.type = "text";
      field.maxLength = 200;
      field.placeholder = "Say something...";
      field.setAttribute("aria-label", "Chat message");
      field.setAttribute("enterkeyhint", "send");
      field.style.cssText = "position:absolute;z-index:10000;box-sizing:border-box;border:1px solid #b3a899;border-radius:0;background:#332b24;color:#e0d9cc;padding:4px 8px;outline:none;";
      field.addEventListener("input", function () {
        SendMessage(field.poeReceiver, "ReceiveBrowserDraft", field.value);
      });
      field.addEventListener("focus", function () {
        SendMessage(field.poeReceiver, "ReceiveBrowserFocus", "1");
      });
      field.addEventListener("blur", function () {
        SendMessage(field.poeReceiver, "ReceiveBrowserFocus", "0");
      });
      field.addEventListener("keydown", function (event) {
        event.stopPropagation();
        if (event.key === "Enter" && !event.isComposing) {
          event.preventDefault();
          SendMessage(field.poeReceiver, "SubmitBrowserChat", field.value);
          field.value = "";
        } else if (event.key === "Escape") field.blur();
      });
      ["pointerdown", "pointerup", "touchstart", "touchend", "keyup"].forEach(function (name) {
        field.addEventListener(name, function (event) { event.stopPropagation(); });
      });
      Module.poeChatInput = field;
    }
    field.poeReceiver = UTF8ToString(receiver);
    var host = Module.canvas.parentElement;
    if (field.parentElement !== host) host.appendChild(field);
    var canvas = Module.canvas.getBoundingClientRect();
    var parent = host.getBoundingClientRect();
    field.style.display = "block";
    field.style.left = (canvas.left - parent.left + x * canvas.width) + "px";
    field.style.top = (canvas.top - parent.top + y * canvas.height) + "px";
    field.style.width = width * canvas.width + "px";
    field.style.height = height * canvas.height + "px";
    field.style.fontSize = Math.max(16, fontSize * canvas.height) + "px";
    var value = UTF8ToString(draft);
    if (field.value !== value) field.value = value;
  },

  TouchBridge_IsCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }

});
