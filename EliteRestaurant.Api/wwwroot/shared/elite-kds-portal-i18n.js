/**
 * Shared EN/FR helpers for Kitchen and Bar KDS web portals.
 * Requires elite-i18n.js loaded first.
 */
(function (global) {
  "use strict";

  const STATUS_FALLBACK_EN = {
    pending_approval: "Pending approval",
    pending_cashier: "Pending cashier",
    waiting: "Waiting",
    in_kitchen: "In kitchen",
    ready: "Ready",
    served: "Served",
    completed: "Completed",
    cancelled: "Cancelled",
    pending: "Pending",
    on_account: "On account"
  };

  const STATUS_FALLBACK_FR = {
    pending_approval: "En attente d'approbation",
    pending_cashier: "En attente caisse",
    waiting: "En attente",
    in_kitchen: "En cuisine",
    ready: "Prête",
    served: "Servie",
    completed: "Terminée",
    cancelled: "Annulée",
    pending: "En attente",
    on_account: "En compte"
  };

  const STATUS_ORDERS_KEY = {
    pending_approval: "orders.pendingApproval",
    pending_cashier: "orders.pendingCashier",
    waiting: "orders.waiting",
    in_kitchen: "orders.inKitchen",
    ready: "orders.ready",
    served: "orders.served",
    completed: "orders.completed",
    cancelled: "orders.cancelled",
    pending: "orders.pending"
  };

  function metaSlug(value) {
    return String(value || "")
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "");
  }

  function normalizeStatusKey(status) {
    const raw = String(status ?? "").trim();
    if (!raw) return "";
    const spaced = raw.replace(/([a-z])([A-Z])/g, "$1 $2");
    const n = spaced.replace(/\s+/g, " ").trim().toLowerCase();
    const aliases = {
      waiting: "waiting",
      "pending approval": "pending_approval",
      "pending cashier": "pending_cashier",
      "in kitchen": "in_kitchen",
      ready: "ready",
      served: "served",
      completed: "completed",
      cancelled: "cancelled",
      canceled: "cancelled",
      pending: "pending",
      "on account": "on_account",
      debt: "on_account"
    };
    return aliases[n] || metaSlug(n);
  }

  function create(portalPrefix) {
    const prefix = "portals." + portalPrefix + ".";
    let hubPillState = "off";

    function currentLangCode() {
      return (global.EliteI18n && EliteI18n.lang) || "fr";
    }

    function t(key, fallback, vars) {
      const full =
        key.indexOf("portals.") === 0 ||
        key.indexOf("auth.") === 0 ||
        key.indexOf("common.") === 0 ||
        key.indexOf("orders.") === 0
          ? key
          : prefix + key;
      return global.EliteI18n && EliteI18n.t
        ? EliteI18n.t(full, fallback, vars)
        : fallback != null
          ? String(fallback)
          : full;
    }

    function translateOrderStatus(status) {
      const raw = String(status ?? "").trim();
      if (!raw) return raw;
      const key = normalizeStatusKey(raw);
      if (!key) return raw;
      const lang = currentLangCode();
      const fb = (lang === "fr" ? STATUS_FALLBACK_FR : STATUS_FALLBACK_EN)[key] || raw;
      let translated = t("status." + key, fb);
      if (translated && translated.indexOf(prefix) !== 0) return translated;
      const ordersKey = STATUS_ORDERS_KEY[key];
      if (ordersKey) {
        translated = t(ordersKey, fb);
        if (translated && translated.indexOf("orders.") !== 0) return translated;
      }
      return fb;
    }

    function translateOriginHeadline(headline) {
      const h = String(headline || "").trim().toUpperCase();
      if (h === "DELIVERY") return t("originHeadline.delivery", "DELIVERY");
      if (h === "TO GO") return t("originHeadline.toGo", "TO GO");
      if (h === "PLATED") return t("originHeadline.plated", "PLATED");
      return headline;
    }

    function translateInvExpStatus(status) {
      const map = {
        "No Expiry": t("invExp.noExpiry", "No Expiry"),
        Expired: t("invExp.expired", "Expired"),
        Critical: t("invExp.critical", "Critical"),
        Bad: t("invExp.bad", "Bad"),
        Good: t("invExp.good", "Good")
      };
      return map[status] || status;
    }

    function translateInvQtyStatus(status) {
      const map = {
        Out: t("invQty.out", "Out"),
        Critical: t("invQty.critical", "Critical"),
        Low: t("invQty.low", "Low"),
        Healthy: t("invQty.healthy", "Healthy")
      };
      return map[status] || status;
    }

    function applyStatic(root) {
      if (global.EliteI18n) EliteI18n.applyToDocument(root || document);
      document.title = t("pageTitle", portalPrefix === "bar" ? "Elite Bar" : "Elite Kitchen");
    }

    function setHubPill(el, state) {
      if (!el) return;
      hubPillState = state;
      el.classList.remove("ok", "warn", "off");
      if (state === "live") {
        el.textContent = t("hubLive", "Live: connected");
        el.classList.add("ok");
      } else if (state === "degraded") {
        el.textContent = t("hubReconnecting", "Live: reconnecting");
        el.classList.add("warn");
      } else {
        el.textContent = t("hubPolling", "Live: polling");
        el.classList.add("off");
      }
    }

    function getHubPillState() {
      return hubPillState;
    }

    async function init(switcherSelectors) {
      if (!global.EliteI18n) return;
      await EliteI18n.init();
      applyStatic();
      (switcherSelectors || []).forEach(function (sel) {
        if (sel) EliteI18n.mountSwitcher(sel);
      });
    }

    return {
      t: t,
      translateOrderStatus: translateOrderStatus,
      translateOriginHeadline: translateOriginHeadline,
      translateInvExpStatus: translateInvExpStatus,
      translateInvQtyStatus: translateInvQtyStatus,
      applyStatic: applyStatic,
      setHubPill: setHubPill,
      getHubPillState: getHubPillState,
      init: init
    };
  }

  global.EliteKdsPortalI18n = { create: create };
})(typeof window !== "undefined" ? window : globalThis);
