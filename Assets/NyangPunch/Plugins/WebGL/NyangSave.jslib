// Browser localStorage access for the save file.
// itch.io serves every upload from a new URL (html/<upload>-<build>/), and Unity keys PlayerPrefs by that URL,
// so PlayerPrefs alone forgets progress after each update. localStorage is per origin, so it survives updates.
mergeInto(LibraryManager.library, {
  NP_StorageGet: function (key) {
    var v = null;
    try { v = window.localStorage.getItem(UTF8ToString(key)); } catch (e) { v = null; }
    if (v === null || v === undefined) return 0;
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);   // IL2CPP frees a returned string itself
    stringToUTF8(v, buf, size);
    return buf;
  },
  NP_StorageSet: function (key, value) {
    try { window.localStorage.setItem(UTF8ToString(key), UTF8ToString(value)); return 1; } catch (e) { return 0; }
  },
  NP_StorageRemove: function (key) {
    try { window.localStorage.removeItem(UTF8ToString(key)); } catch (e) { }
  },
  // Calls <gameObject>.<method>() when the page is hidden or unloaded (tab or app switch, closing the tab):
  // Unity's OnApplicationPause / OnApplicationQuit do not reliably fire for that in a browser.
  NP_HookPageHide: function (gameObject, method) {
    var go = UTF8ToString(gameObject), m = UTF8ToString(method);
    var fire = function () {
      // quoted name: Unity exports it as Module["SendMessage"], and a minifier must not rename it
      try { if (typeof Module !== 'undefined' && Module['SendMessage']) Module['SendMessage'](go, m); } catch (e) { }
    };
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') fire(); });
    window.addEventListener('pagehide', fire);
  },
  NP_Reload: function () {
    try { window.location.reload(); } catch (e) { }
  }
});
