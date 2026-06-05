(function (global) {
  "use strict";

  var MODAL_ID = "elite-order-cancel-modal";
  var STYLE_ID = "elite-order-cancel-styles";

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
      "#" + MODAL_ID + "{position:fixed;inset:0;z-index:10050;display:flex;align-items:center;justify-content:center;padding:16px;background:rgba(0,0,0,.55);}" +
      "#" + MODAL_ID + ".hidden{display:none!important;}" +
      "#" + MODAL_ID + " .elite-oc-panel{max-width:380px;width:100%;background:#161c28;border:1px solid rgba(255,255,255,.12);border-radius:14px;padding:20px;box-shadow:0 16px 48px rgba(0,0,0,.45);color:#e8eaef;font-family:inherit;}" +
      "#" + MODAL_ID + " .elite-oc-title{margin:0 0 8px;font-size:1.05rem;font-weight:700;}" +
      "#" + MODAL_ID + " .elite-oc-msg{margin:0 0 14px;font-size:.9rem;color:#94a3b8;line-height:1.45;}" +
      "#" + MODAL_ID + " .elite-oc-input{width:100%;box-sizing:border-box;padding:10px 12px;border-radius:10px;border:1px solid rgba(255,255,255,.15);background:#0b0e14;color:#e8eaef;font-size:1rem;letter-spacing:.08em;}" +
      "#" + MODAL_ID + " .elite-oc-actions{display:flex;gap:10px;margin-top:16px;justify-content:flex-end;}" +
      "#" + MODAL_ID + " .elite-oc-btn{padding:9px 16px;border-radius:10px;border:1px solid rgba(255,255,255,.12);cursor:pointer;font-weight:600;font-size:.9rem;}" +
      "#" + MODAL_ID + " .elite-oc-btn--ghost{background:transparent;color:#cbd5e1;}" +
      "#" + MODAL_ID + " .elite-oc-btn--danger{background:rgba(248,113,113,.18);color:#fca5a5;border-color:rgba(248,113,113,.35);}" +
      ".btn-cancel-order{background:rgba(248,113,113,.15)!important;color:#fca5a5!important;border:1px solid rgba(248,113,113,.35)!important;}";
    document.head.appendChild(style);
  }

  function ensureModal() {
    injectStyles();
    var existing = document.getElementById(MODAL_ID);
    if (existing) return existing;

    var wrap = document.createElement("div");
    wrap.id = MODAL_ID;
    wrap.className = "hidden";
    wrap.setAttribute("role", "dialog");
    wrap.setAttribute("aria-modal", "true");
    wrap.innerHTML =
      '<div class="elite-oc-panel">' +
      '<h3 class="elite-oc-title" id="elite-oc-title"></h3>' +
      '<p class="elite-oc-msg" id="elite-oc-msg"></p>' +
      '<input type="password" class="elite-oc-input" id="elite-oc-input" autocomplete="off" inputmode="numeric" />' +
      '<div class="elite-oc-actions">' +
      '<button type="button" class="elite-oc-btn elite-oc-btn--ghost" id="elite-oc-cancel"></button>' +
      '<button type="button" class="elite-oc-btn elite-oc-btn--danger" id="elite-oc-confirm"></button>' +
      "</div></div>";
    document.body.appendChild(wrap);
    return wrap;
  }

  function canCancelStatus(status) {
    var s = String(status || "").trim().toLowerCase();
    return s !== "completed" && s !== "cancelled";
  }

  function promptPasscode(options) {
    return new Promise(function (resolve) {
      var modal = ensureModal();
      var titleEl = document.getElementById("elite-oc-title");
      var msgEl = document.getElementById("elite-oc-msg");
      var input = document.getElementById("elite-oc-input");
      var btnCancel = document.getElementById("elite-oc-cancel");
      var btnConfirm = document.getElementById("elite-oc-confirm");
      if (!titleEl || !msgEl || !input || !btnCancel || !btnConfirm) {
        resolve(null);
        return;
      }

      titleEl.textContent = options && options.title
        ? options.title
        : t("portals.common.orderCancelTitle", "Cancel order");
      msgEl.textContent = options && options.message
        ? options.message
        : t("portals.common.orderCancelMessage", "Enter the admin cancel passcode to confirm cancellation.");
      btnCancel.textContent = t("portals.common.orderCancelBack", "Back");
      btnConfirm.textContent = t("portals.common.orderCancelConfirm", "Cancel order");
      input.setAttribute("aria-label", t("portals.common.orderCancelPasscodeAria", "Admin cancel passcode"));
      input.value = "";

      function close(result) {
        modal.classList.add("hidden");
        document.removeEventListener("keydown", onKey);
        modal.onclick = null;
        btnCancel.onclick = null;
        btnConfirm.onclick = null;
        resolve(result);
      }

      function onKey(ev) {
        if (ev.key === "Escape") close(null);
        if (ev.key === "Enter") {
          ev.preventDefault();
          close(String(input.value || "").trim());
        }
      }

      btnCancel.onclick = function () { close(null); };
      btnConfirm.onclick = function () { close(String(input.value || "").trim()); };
      modal.onclick = function (ev) {
        if (ev.target === modal) close(null);
      };
      document.addEventListener("keydown", onKey);
      modal.classList.remove("hidden");
      setTimeout(function () { input.focus(); }, 0);
    });
  }

  async function cancelStaffOrder(orderId, postCancel, options) {
    var opts = options || {};

    var passcode = await promptPasscode({
      title: opts.modalTitle || t("portals.common.orderCancelTitle", "Cancel order"),
      message: opts.modalMessage || t("portals.common.orderCancelMessage", "Enter the admin cancel passcode to confirm cancellation.")
    });
    if (passcode === null || passcode === "") {
      return { ok: false, userCancelled: true };
    }

    var result = await postCancel(orderId, passcode);
    if (!result || !result.ok) {
      var msg = (result && result.body && (result.body.message || result.body.Message))
        || (result && result.error)
        || "Cancel failed.";
      global.alert(msg);
      return { ok: false, error: msg };
    }

    if (typeof opts.onSuccess === "function") {
      await opts.onSuccess(orderId);
    }
    return { ok: true };
  }

  global.EliteOrderCancel = {
    canCancelStatus: canCancelStatus,
    cancelStaffOrder: cancelStaffOrder,
    promptPasscode: promptPasscode
  };
})(typeof window !== "undefined" ? window : globalThis);
