(function (global) {
  "use strict";

  var STYLE_ID = "elite-api-error-banner-styles";
  var BANNER_ID = "elite-api-error-banner";

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
      "#" + BANNER_ID + "{position:fixed;top:0;left:0;right:0;z-index:10040;display:flex;align-items:center;justify-content:space-between;gap:12px;padding:10px 14px;background:#7f1d1d;color:#fecaca;border-bottom:1px solid rgba(248,113,113,.4);font-family:inherit;font-size:.9rem;box-shadow:0 4px 16px rgba(0,0,0,.25);}" +
      "#" + BANNER_ID + ".hidden{display:none!important;}" +
      "#" + BANNER_ID + " .elite-api-error-msg{flex:1;line-height:1.4;}" +
      "#" + BANNER_ID + " .elite-api-error-dismiss{background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.2);color:inherit;border-radius:8px;padding:6px 12px;cursor:pointer;font-weight:600;}";
    document.head.appendChild(style);
  }

  function ensureBanner() {
    injectStyles();
    var el = document.getElementById(BANNER_ID);
    if (el) return el;
    el = document.createElement("div");
    el.id = BANNER_ID;
    el.className = "hidden";
    el.setAttribute("role", "alert");
    el.innerHTML =
      '<span class="elite-api-error-msg" id="elite-api-error-msg"></span>' +
      '<button type="button" class="elite-api-error-dismiss" id="elite-api-error-dismiss">' +
      t("portals.common.apiErrorDismiss", "Dismiss") +
      "</button>";
    document.body.appendChild(el);
    el.querySelector("#elite-api-error-dismiss").onclick = function () {
      el.classList.add("hidden");
    };
    return el;
  }

  function showApiError(message) {
    var banner = ensureBanner();
    var msgEl = document.getElementById("elite-api-error-msg");
    if (msgEl) {
      msgEl.textContent = message || t("portals.common.apiErrorDefault",
        "Something went wrong — refresh the page or contact support.");
    }
    banner.classList.remove("hidden");
  }

  function hideApiError() {
    var banner = document.getElementById(BANNER_ID);
    if (banner) banner.classList.add("hidden");
  }

  function messageFromResponse(result) {
    if (!result) return null;
    var body = result.body;
    if (body && (body.message || body.Message)) return String(body.message || body.Message);
    if (result.status >= 500) {
      return t("portals.common.apiErrorDefault",
        "Something went wrong — refresh the page or contact support.");
    }
    if (result.status === 401 || result.status === 403) {
      return t("portals.common.apiErrorAuth", "You are not allowed to perform this action.");
    }
    if (!result.ok) {
      return t("portals.common.apiErrorDefault",
        "Something went wrong — refresh the page or contact support.");
    }
    return null;
  }

  function wrapApiResult(result, options) {
    if (options && options.silent) return result;
    var msg = messageFromResponse(result);
    if (msg) showApiError(msg);
    return result;
  }

  global.EliteApiError = {
    show: showApiError,
    hide: hideApiError,
    wrap: wrapApiResult,
    messageFromResponse: messageFromResponse
  };
})(typeof window !== "undefined" ? window : globalThis);
