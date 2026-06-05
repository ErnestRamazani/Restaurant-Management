(function (global) {

  "use strict";



  var MODAL_ID = "elite-client-picker-modal";

  var STYLE_ID = "elite-client-picker-styles";

  var activeSession = null;



  function injectStyles() {

    if (document.getElementById(STYLE_ID)) return;

    var style = document.createElement("style");

    style.id = STYLE_ID;

    style.textContent =

      "#" + MODAL_ID + "{position:fixed;inset:0;z-index:10040;display:flex;align-items:center;justify-content:center;padding:16px;background:rgba(0,0,0,.62);font-family:inherit;}" +

      "#" + MODAL_ID + ".hidden{display:none!important;}" +

      "#" + MODAL_ID + " .elite-cp-panel{max-width:480px;width:100%;max-height:min(82vh,640px);display:flex;flex-direction:column;background:#121722;border:1px solid rgba(212,175,55,.28);border-radius:16px;box-shadow:0 20px 56px rgba(0,0,0,.55);color:#e8eaef;overflow:hidden;}" +

      "#" + MODAL_ID + " .elite-cp-head{padding:18px 20px 12px;border-bottom:1px solid rgba(255,255,255,.08);}" +

      "#" + MODAL_ID + " .elite-cp-title{margin:0;font-size:1.1rem;font-weight:700;color:#e8a838;}" +

      "#" + MODAL_ID + " .elite-cp-sub{margin:6px 0 0;font-size:.82rem;color:#94a3b8;line-height:1.4;}" +

      "#" + MODAL_ID + " .elite-cp-search-wrap{padding:12px 16px 8px;}" +

      "#" + MODAL_ID + " .elite-cp-search{width:100%;box-sizing:border-box;padding:10px 12px;border-radius:10px;border:1px solid rgba(255,255,255,.12);background:#0b0e14;color:#e8eaef;font-size:.95rem;}" +

      "#" + MODAL_ID + " .elite-cp-search:focus{outline:none;border-color:rgba(232,168,56,.55);}" +

      "#" + MODAL_ID + " .elite-cp-list{flex:1;min-height:120px;overflow-y:auto;padding:4px 12px 12px;}" +

      "#" + MODAL_ID + " .elite-cp-empty{padding:24px 12px;text-align:center;color:#64748b;font-size:.88rem;}" +

      "#" + MODAL_ID + " .elite-cp-item{display:flex;align-items:flex-start;gap:12px;width:100%;text-align:left;padding:12px 14px;margin:0 0 8px;border:1px solid rgba(255,255,255,.08);border-radius:12px;background:#161c28;color:inherit;cursor:pointer;font:inherit;transition:background .15s,border-color .15s;}" +

      "#" + MODAL_ID + " .elite-cp-item:hover,#" + MODAL_ID + " .elite-cp-item:focus-visible{background:#1c2433;border-color:rgba(212,175,55,.35);outline:none;}" +

      "#" + MODAL_ID + " .elite-cp-item.is-selected{border-color:rgba(232,168,56,.65);background:rgba(232,168,56,.1);}" +

      "#" + MODAL_ID + " .elite-cp-item-main{flex:1;min-width:0;}" +

      "#" + MODAL_ID + " .elite-cp-name{font-weight:600;font-size:.95rem;color:#f1f5f9;}" +

      "#" + MODAL_ID + " .elite-cp-meta{margin-top:4px;font-size:.78rem;color:#94a3b8;}" +

      "#" + MODAL_ID + " .elite-cp-badges{display:flex;flex-wrap:wrap;gap:6px;margin-top:8px;}" +

      "#" + MODAL_ID + " .elite-cp-badge{font-size:.65rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:3px 8px;border-radius:6px;}" +

      "#" + MODAL_ID + " .elite-cp-badge--staff{background:rgba(56,189,248,.15);color:#7dd3fc;border:1px solid rgba(56,189,248,.25);}" +

      "#" + MODAL_ID + " .elite-cp-badge--debt{background:rgba(232,168,56,.12);color:#fbbf24;border:1px solid rgba(232,168,56,.28);}" +

      "#" + MODAL_ID + " .elite-cp-badge--clear{background:rgba(45,212,191,.12);color:#5eead4;border:1px solid rgba(45,212,191,.22);}" +

      "#" + MODAL_ID + " .elite-cp-foot{padding:12px 16px 16px;border-top:1px solid rgba(255,255,255,.08);display:flex;gap:10px;justify-content:flex-end;}" +

      "#" + MODAL_ID + " .elite-cp-btn{padding:9px 16px;border-radius:10px;border:1px solid rgba(255,255,255,.12);cursor:pointer;font-weight:600;font-size:.88rem;}" +

      "#" + MODAL_ID + " .elite-cp-btn--ghost{background:transparent;color:#cbd5e1;}" +

      "#" + MODAL_ID + " .elite-cp-btn--gold{background:rgba(232,168,56,.18);color:#fcd34d;border-color:rgba(232,168,56,.4);}" +

      "#" + MODAL_ID + " .elite-cp-btn:disabled{opacity:.45;cursor:not-allowed;}";

    document.head.appendChild(style);

  }



  function label(options, key, fallback, vars) {

    if (options && typeof options.i18n === "function") {

      return options.i18n(key, fallback, vars);

    }

    if (global.EliteI18n && EliteI18n.t) {

      var full = key.indexOf(".") >= 0 ? key : "common.clientPicker." + key;

      return EliteI18n.t(full, fallback, vars);

    }

    if (fallback == null) return key;

    if (!vars) return String(fallback);

    return String(fallback).replace(/\{\{(\w+)\}\}/g, function (_, name) {

      return vars[name] != null ? String(vars[name]) : "";

    });

  }



  function normClient(raw) {

    if (!raw || typeof raw !== "object") return null;

    return {

      id: raw.id ?? raw.Id ?? 0,

      uniqueId: String(raw.uniqueId ?? raw.UniqueId ?? ""),

      fullName: String(raw.fullName ?? raw.FullName ?? "Client"),

      primaryPhone: String(raw.primaryPhone ?? raw.PrimaryPhone ?? ""),

      isStaffClient: !!(raw.isStaffClient ?? raw.IsStaffClient),

      debtBalanceUsd: Number(raw.debtBalanceUsd ?? raw.DebtBalanceUsd ?? 0)

    };

  }



  function fmtUsd(n) {

    return "$ " + Number(n || 0).toFixed(2);

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

      '<div class="elite-cp-panel">' +

      '<div class="elite-cp-head">' +

      '<h3 class="elite-cp-title" id="elite-cp-title">Select client</h3>' +

      '<p class="elite-cp-sub" id="elite-cp-sub">Choose a client to link to this order.</p>' +

      "</div>" +

      '<div class="elite-cp-search-wrap">' +

      '<input type="search" class="elite-cp-search" id="elite-cp-search" placeholder="Filter by name, phone, or ID…" autocomplete="off" />' +

      "</div>" +

      '<div class="elite-cp-list" id="elite-cp-list" role="listbox" aria-label="Clients"></div>' +

      '<div class="elite-cp-foot">' +

      '<button type="button" class="elite-cp-btn elite-cp-btn--ghost" id="elite-cp-cancel">Cancel</button>' +

      '<button type="button" class="elite-cp-btn elite-cp-btn--gold" id="elite-cp-confirm" disabled>Select</button>' +

      "</div></div>";

    document.body.appendChild(wrap);

    return wrap;

  }



  function renderList(listEl, clients, filter, selectedId, onSelect, options) {

    var q = String(filter || "").trim().toLowerCase();

    var phoneQ = q.replace(/\D/g, "");

    var filtered = clients.filter(function (c) {

      if (!q) return true;

      if (c.fullName.toLowerCase().indexOf(q) >= 0) return true;

      if (c.uniqueId.toLowerCase().indexOf(q) >= 0) return true;

      if (phoneQ && c.primaryPhone.indexOf(phoneQ) >= 0) return true;

      return false;

    });



    if (!filtered.length) {

      listEl.innerHTML = '<div class="elite-cp-empty">' + escapeHtml(label(options, "clientPickerNoMatch", "No clients match your search.")) + "</div>";

      return null;

    }



    listEl.innerHTML = filtered.map(function (c) {

      var debt = c.debtBalanceUsd;

      var debtBadge = debt > 0.009

        ? '<span class="elite-cp-badge elite-cp-badge--debt">' + escapeHtml(label(options, "clientHasDebt", "Debt {{amount}}", { amount: fmtUsd(debt) })) + "</span>"

        : '<span class="elite-cp-badge elite-cp-badge--clear">' + escapeHtml(label(options, "clientNoDebt", "No debt")) + "</span>";

      var staffBadge = c.isStaffClient

        ? '<span class="elite-cp-badge elite-cp-badge--staff">' + escapeHtml(label(options, "clientPickerStaff", "Staff")) + "</span>"

        : "";

      var phone = c.primaryPhone ? c.primaryPhone : label(options, "clientPickerNoPhone", "No phone");

      var sel = c.id === selectedId ? " is-selected" : "";

      return (

        '<button type="button" class="elite-cp-item' + sel + '" data-id="' + c.id + '" role="option">' +

        '<div class="elite-cp-item-main">' +

        '<div class="elite-cp-name">' + escapeHtml(c.fullName) + "</div>" +

        '<div class="elite-cp-meta">' + escapeHtml(phone) +

        (c.uniqueId ? " · " + escapeHtml(c.uniqueId) : "") +

        "</div>" +

        '<div class="elite-cp-badges">' + staffBadge + debtBadge + "</div>" +

        "</div></button>"

      );

    }).join("");



    listEl.querySelectorAll(".elite-cp-item").forEach(function (btn) {

      btn.onclick = function () {

        var id = Number(btn.getAttribute("data-id"));

        onSelect(id);

        listEl.querySelectorAll(".elite-cp-item").forEach(function (b) {

          b.classList.toggle("is-selected", Number(b.getAttribute("data-id")) === id);

        });

      };

    });



    return filtered;

  }



  function escapeHtml(s) {

    return String(s || "")

      .replace(/&/g, "&amp;")

      .replace(/</g, "&lt;")

      .replace(/>/g, "&gt;")

      .replace(/"/g, "&quot;");

  }



  function applyChromeLabels(session) {

    if (!session) return;

    var options = session.options;

    var rows = session.rows;

    session.titleEl.textContent = (options && options.title) || label(options, "clientPickerDefaultTitle", "Select client");

    session.subEl.textContent = (options && options.subtitle) ||

      (rows.length === 1

        ? label(options, "clientPickerConfirmOne", "Confirm this client or cancel.")

        : label(options, "clientPickerConfirmMany", "{{count}} clients found — pick one to link.", { count: rows.length }));

    session.searchEl.placeholder = label(options, "filterClientPlaceholder", "Filter by name, phone, or ID…");

    session.searchEl.setAttribute("aria-label", session.searchEl.placeholder);

    session.btnCancel.textContent = label(options, "common.cancel", "Cancel");

    session.btnConfirm.textContent = label(options, "clientPickerSelect", "Select");

    session.listEl.setAttribute("aria-label", label(options, "clientPickerListAria", "Clients"));

  }



  function refreshActiveSession() {

    if (!activeSession) return;

    applyChromeLabels(activeSession);

    renderList(activeSession.listEl, activeSession.rows, activeSession.searchEl.value, activeSession.selectedId, function (id) {

      activeSession.selectedId = id;

      activeSession.btnConfirm.disabled = !activeSession.selectedId;

    }, activeSession.options);

  }



  if (typeof document !== "undefined") {

    document.addEventListener("elite-language-changed", refreshActiveSession);

  }



  function pick(clients, options) {

    return new Promise(function (resolve) {

      var rows = (Array.isArray(clients) ? clients : [])

        .map(normClient)

        .filter(function (c) { return c && c.id > 0; });



      if (!rows.length) {

        resolve(null);

        return;

      }



      var modal = ensureModal();

      var titleEl = document.getElementById("elite-cp-title");

      var subEl = document.getElementById("elite-cp-sub");

      var searchEl = document.getElementById("elite-cp-search");

      var listEl = document.getElementById("elite-cp-list");

      var btnCancel = document.getElementById("elite-cp-cancel");

      var btnConfirm = document.getElementById("elite-cp-confirm");

      if (!titleEl || !subEl || !searchEl || !listEl || !btnCancel || !btnConfirm) {

        resolve(null);

        return;

      }



      var selectedId = 0;

      searchEl.value = (options && options.initialQuery) || "";

      btnConfirm.disabled = true;



      var session = {

        options: options || {},

        rows: rows,

        titleEl: titleEl,

        subEl: subEl,

        searchEl: searchEl,

        listEl: listEl,

        btnCancel: btnCancel,

        btnConfirm: btnConfirm,

        selectedId: 0,

        resolve: resolve

      };

      activeSession = session;



      function findSelected() {

        return rows.find(function (c) { return c.id === selectedId; }) || null;

      }



      function refresh() {

        renderList(listEl, rows, searchEl.value, selectedId, function (id) {

          selectedId = id;

          session.selectedId = id;

          btnConfirm.disabled = !selectedId;

        }, options);

      }



      function close(result) {

        activeSession = null;

        modal.classList.add("hidden");

        document.removeEventListener("keydown", onKey);

        modal.onclick = null;

        searchEl.oninput = null;

        btnCancel.onclick = null;

        btnConfirm.onclick = null;

        resolve(result);

      }



      function onKey(ev) {

        if (ev.key === "Escape") close(null);

      }



      applyChromeLabels(session);

      refresh();

      searchEl.oninput = refresh;

      btnCancel.onclick = function () { close(null); };

      btnConfirm.onclick = function () { close(findSelected()); };

      modal.onclick = function (ev) {

        if (ev.target === modal) close(null);

      };

      document.addEventListener("keydown", onKey);

      modal.classList.remove("hidden");

      setTimeout(function () { searchEl.focus(); }, 30);

    });

  }



  global.EliteClientPicker = {

    pick: pick,

    normClient: normClient

  };

})(typeof window !== "undefined" ? window : globalThis);

