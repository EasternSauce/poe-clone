// Tells the C# side (TouchMode.cs) whether the browser's primary pointer is a finger. Unity's
// own mobile check reads the user agent, which iPads disguise as a Mac; the pointer media query
// catches those, and stays false on touch laptops whose primary pointer is still the mouse.
mergeInto(LibraryManager.library, {

  TouchBridge_IsCoarsePointer: function () {
    try {
      return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }

});
