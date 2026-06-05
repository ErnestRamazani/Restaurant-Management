(function (global) {
  "use strict";

  function t(key, fallback) {
    if (global.EliteI18n && typeof global.EliteI18n.t === "function") {
      return global.EliteI18n.t(key, fallback);
    }
    return fallback;
  }

  function setButtonBusy(btn, busy, busyLabelKey, busyFallback) {
    if (!btn) return;
    if (busy) {
      if (!btn.dataset.eliteBusyLabel) btn.dataset.eliteBusyLabel = btn.textContent || "";
      btn.disabled = true;
      btn.setAttribute("aria-busy", "true");
      btn.textContent = t(busyLabelKey || "common.loading", busyFallback || "…");
    } else {
      btn.disabled = false;
      btn.removeAttribute("aria-busy");
      if (btn.dataset.eliteBusyLabel) {
        btn.textContent = btn.dataset.eliteBusyLabel;
        delete btn.dataset.eliteBusyLabel;
      }
    }
  }

  global.EliteButtonBusy = { set: setButtonBusy };
})(typeof window !== "undefined" ? window : globalThis);
