(function (global) {
  "use strict";

  var STYLE_ID = "elite-signalr-banner-styles";
  var BANNER_ID = "elite-signalr-banner";

  function t(key, fallback) {
    if (global.EliteI18n && typeof global.EliteI18n.t === "function") {
      return global.EliteI18n.t(key, fallback);
    }
    return fallback;
  }

  function injectStyles() {
    if (document.getElementById(STYLE_ID)) return;
    var style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent =
      "#" + BANNER_ID + "{position:fixed;top:0;left:0;right:0;z-index:10030;padding:8px 14px;text-align:center;background:#854d0e;color:#fef08a;border-bottom:1px solid rgba(250,204,21,.35);font-family:inherit;font-size:.88rem;}" +
      "#" + BANNER_ID + ".hidden{display:none!important;}";
    document.head.appendChild(style);
  }

  function ensureBanner() {
    injectStyles();
    var el = document.getElementById(BANNER_ID);
    if (el) return el;
    el = document.createElement("div");
    el.id = BANNER_ID;
    el.className = "hidden";
    el.setAttribute("role", "status");
    document.body.appendChild(el);
    return el;
  }

  function showReconnecting() {
    var el = ensureBanner();
    el.textContent = t("portals.common.signalrReconnecting",
      "Connection lost — reconnecting…");
    el.classList.remove("hidden");
  }

  function hide() {
    var el = document.getElementById(BANNER_ID);
    if (el) el.classList.add("hidden");
  }

  function wireConnection(connection, onReconnected, debounceMs) {
    if (!connection) return;
    var minDownMs = typeof debounceMs === "number" ? debounceMs : 3000;
    var disconnectedAt = null;
    connection.onclose(function () {
      disconnectedAt = Date.now();
      showReconnecting();
    });
    if (typeof connection.onreconnecting === "function") {
      connection.onreconnecting(function () {
        if (!disconnectedAt) disconnectedAt = Date.now();
        showReconnecting();
      });
    }
    connection.onreconnected(function () {
      hide();
      var wasLongOutage = disconnectedAt && (Date.now() - disconnectedAt >= minDownMs);
      disconnectedAt = null;
      if (wasLongOutage && typeof onReconnected === "function") onReconnected();
    });
  }

  global.EliteSignalRBanner = {
    showReconnecting: showReconnecting,
    hide: hide,
    wire: wireConnection
  };
})(typeof window !== "undefined" ? window : globalThis);
