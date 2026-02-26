(function () {
  function normalizeTheme(value) {
    return value === "dark" ? "dark" : "light";
  }

  function applyTheme(theme) {
    var normalized = normalizeTheme(theme);

    document.documentElement.setAttribute("data-theme", normalized);
    if (document.body) {
      document.body.setAttribute("data-theme", normalized);
    }

    return normalized;
  }

  function getTheme() {
    try {
      var stored = localStorage.getItem("pbp-theme");
      if (stored === "dark" || stored === "light") {
        return stored;
      }
    } catch {
      // Ignore storage access errors in restricted environments.
    }

    return "light";
  }

  function setTheme(theme) {
    var normalized = applyTheme(theme);

    try {
      localStorage.setItem("pbp-theme", normalized);
    } catch {
      // Ignore storage access errors in restricted environments.
    }

    return normalized;
  }

  function syncThemeFromStorage() {
    applyTheme(getTheme());
  }

  function syncSoon() {
    requestAnimationFrame(syncThemeFromStorage);
  }

  window.pbpTheme = {
    getTheme: getTheme,
    setTheme: setTheme,
    applyTheme: applyTheme,
    syncTheme: syncThemeFromStorage
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", syncThemeFromStorage, { once: true });
  } else {
    syncThemeFromStorage();
  }

  // Blazor enhanced navigation can replace page content without a full reload.
  if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
    window.Blazor.addEventListener("enhancedload", syncSoon);
  }

  document.addEventListener("enhancedload", syncSoon);
  window.addEventListener("popstate", syncSoon);
  window.addEventListener("pageshow", syncSoon);
})();
