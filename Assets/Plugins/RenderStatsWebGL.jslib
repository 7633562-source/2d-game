// Mirrors the RenderStatsRuntime JSON into the page: document.title + a hidden #renderstats
// element, so a headless CDP client can read the numbers from the running WebGL demo.
mergeInto(LibraryManager.library, {
  RS_Report: function (ptr) {
    try {
      var s = UTF8ToString(ptr);
      document.title = "RS " + s;
      var el = document.getElementById("renderstats");
      if (!el) {
        el = document.createElement("pre");
        el.id = "renderstats";
        el.style.display = "none";
        document.body.appendChild(el);
      }
      el.textContent = s;
    } catch (e) {
      try { document.title = "RS_ERROR " + e; } catch (e2) {}
    }
  }
});
