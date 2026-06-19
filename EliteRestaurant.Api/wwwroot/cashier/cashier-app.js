(function () {
  const PORTAL_ID = "Cashier";
  let hubPillState = "off";
  let ordersUpdatedTime = "";

  function t(key, fallback, vars) {
    const full = key.indexOf("portals.") === 0 || key.indexOf("auth.") === 0 || key.indexOf("common.") === 0 || key.indexOf("orders.") === 0
      ? key
      : "portals.cashier." + key;
    return (window.EliteI18n && EliteI18n.t) ? EliteI18n.t(full, fallback, vars) : (fallback != null ? String(fallback) : full);
  }

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
      "waiting": "waiting",
      "pending approval": "pending_approval",
      "pending cashier": "pending_cashier",
      "in kitchen": "in_kitchen",
      "ready": "ready",
      "served": "served",
      "completed": "completed",
      "cancelled": "cancelled",
      "canceled": "cancelled",
      "refunded": "refunded",
      "pending": "pending",
      "on account": "on_account",
      "debt": "on_account"
    };
    return aliases[n] || metaSlug(n);
  }

  const STATUS_FALLBACK_EN = {
    pending_approval: "Pending approval",
    pending_cashier: "Pending cashier",
    waiting: "Waiting",
    in_kitchen: "In kitchen",
    ready: "Ready",
    served: "Served",
    completed: "Completed",
    cancelled: "Cancelled",
    refunded: "Refunded",
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
    refunded: "Remboursée",
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
    refunded: "orders.refunded",
    pending: "orders.pending"
  };

  const META_FALLBACK_FR = {
    payment: { deferred: "Différé", immediate: "Immédiat", pay_now: "Payer maintenant", on_account: "En compte" },
    origin: { online: "En ligne", dine_in: "Sur place", walk_in: "Sans réservation" },
    source: { delivery: "Livraison", pickup: "À emporter", dine_in: "Sur place", table: "Table" },
    table: { online: "En ligne" }
  };

  const META_FALLBACK_EN = {
    payment: { deferred: "Deferred", immediate: "Immediate", pay_now: "Pay now", on_account: "On account" },
    origin: { online: "Online", dine_in: "Dine-in", walk_in: "Walk-in" },
    source: { delivery: "Delivery", pickup: "Pickup", dine_in: "Dine-in", table: "Table" },
    table: { online: "Online" }
  };

  function currentLangCode() {
    return (window.EliteI18n && EliteI18n.lang) || "fr";
  }

  function translateOrderStatus(status) {
    const raw = String(status ?? "").trim();
    if (!raw) return raw;
    const key = normalizeStatusKey(raw);
    if (!key) return raw;
    const lang = currentLangCode();
    const fb = (lang === "fr" ? STATUS_FALLBACK_FR : STATUS_FALLBACK_EN)[key] || raw;
    let translated = t("status." + key, fb);
    if (translated && translated.indexOf("portals.cashier.") !== 0) return translated;
    const ordersKey = STATUS_ORDERS_KEY[key];
    if (ordersKey) {
      translated = t(ordersKey, fb);
      if (translated && translated.indexOf("orders.") !== 0) return translated;
    }
    return fb;
  }

  function normalizeMetaKey(kind, value) {
    const raw = String(value ?? "").trim();
    if (!raw) return "";
    const spaced = raw.replace(/([a-z])([A-Z])/g, "$1 $2");
    const n = spaced.replace(/\s+/g, " ").trim().toLowerCase();
    const aliases = {
      payment: {
        deferred: "deferred",
        immediate: "immediate",
        "pay now": "pay_now",
        "on account": "on_account"
      },
      origin: {
        online: "online",
        "dine in": "dine_in",
        "dine-in": "dine_in",
        "walk in": "walk_in",
        "walk-in": "walk_in"
      },
      source: {
        delivery: "delivery",
        pickup: "pickup",
        "dine in": "dine_in",
        "dine-in": "dine_in",
        table: "table"
      },
      table: {
        online: "online"
      }
    };
    return (aliases[kind] && aliases[kind][n]) || metaSlug(n);
  }

  function translateMetaValue(kind, value) {
    const raw = String(value ?? "").trim();
    if (!raw) return raw;
    if (raw.includes("·")) {
      return raw.split("·").map(part => translateMetaValue(kind, part.trim())).join(" · ");
    }
    const slug = normalizeMetaKey(kind, raw);
    if (!slug) return raw;
    const lang = currentLangCode();
    const fbMap = lang === "fr" ? META_FALLBACK_FR : META_FALLBACK_EN;
    const fb = (fbMap[kind] && fbMap[kind][slug]) || raw;
    const translated = t("meta." + kind + "." + slug, fb);
    if (translated && translated.indexOf("portals.cashier.") !== 0) return translated;
    return fb;
  }

  function applyCashierStaticI18n(root) {
    if (window.EliteI18n) EliteI18n.applyToDocument(root || document);
    document.title = t("pageTitle", "Elite Cashier");
  }

  function refreshCashierDynamicLabels() {
    setHubPill(hubPillState);
    const ou = $("ordersUpdated");
    if (ou && ordersUpdatedTime) {
      ou.textContent = t("updatedPrefix", "Updated") + " " + ordersUpdatedTime;
    }
    if (token) {
      if (currentView === "orders") {
        renderPendingOrders();
        renderActiveOrders();
        renderPastOrders();
        updateOrdersNavBadge();
      }
      if (currentView === "menu") renderMenuFromCatalog();
      const payModal = $("paymentModal");
      if (payModal && !payModal.classList.contains("hidden")) {
        updatePaymentFlowUI();
        const debtLbl = $("payOnAccountLabel");
        if (debtLbl) debtLbl.title = paymentCanAddToDebt ? "" : t("pay.debtCapTitle", "Debt cap reached — collect payment first.");
      }
      if (detailOrderId && detailOrderStatus) {
        const pill = $("detailStatusPill");
        if (pill) pill.textContent = translateOrderStatus(detailOrderStatus) || detailOrderStatus || "—";
        if (!$("orderDetailModal").classList.contains("hidden")) {
          openOrderDetail(detailOrderId).catch(() => {});
        }
      }
    }
  }

  document.addEventListener("elite-language-changed", function () {
    applyCashierStaticI18n();
    refreshCashierDynamicLabels();
  });

  let token = "";
  let me = null;
  let activeOrderRows = [];
  let pastOrderRows = [];
  let pendingRows = [];
  let paymentTargetOrderId = 0;
  let detailOrderId = 0;
  let detailOrderStatus = "";
  let paymentDueUsd = 0;
  let paymentOrderCode = "";
  let paymentStep = "payment";
  let numpadTarget = "PaidUsd";
  let paidUsdInput = "";
  let paidFcInput = "";
  let changeUsdInput = "";
  let changeFcInput = "";
  let paymentHasLinkedClient = false;
  let paymentCanAddToDebt = false;
  let config = { restaurantName: "Elite Restaurant", restaurantLogoUrl: "", employeePhotoUrl: "", currencyDisplayMode: "Dual", usdToFcRate: 2250, taxPercent: 7, servicePercent: 10 };
  let currentView = "orders";
  let menuCatalogRows = [];
  let invByIdForMenu = {};
  let orderHubConnection = null;
  let pollTimer = null;
  function $(id) { return document.getElementById(id); }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));
  }

  function orderDetailStatusPillClass(stRaw) {
    const s = String(stRaw || "").toLowerCase();
    if (s.includes("debt")) return "od-status-pill--debt";
    if (s.includes("pending")) return "od-status-pill--pending";
    if (s.includes("waiting")) return "od-status-pill--wait";
    if (s.includes("kitchen")) return "od-status-pill--kitchen";
    if (s.includes("ready")) return "od-status-pill--ready";
    if (s.includes("served")) return "od-status-pill--served";
    return "";
  }

  function orderDetailNoteInnerHtml(bodyRaw) {
    const note = String(bodyRaw ?? "").trim();
    if (!note || note === "-") return "<span class=\"od-note-empty\">" + escapeHtml(t("detail.noneNoted", "None noted")) + "</span>";
    return escapeHtml(note);
  }

  function orderConfirmationCode(o) {
    return String(o?.confirmationCode ?? o?.ConfirmationCode ?? "").trim();
  }

  function orderCardCodeHtml(o) {
    const code = orderConfirmationCode(o);
    if (!code) return "";
    return "<div class='order-card__code'>" + escapeHtml(t("orderCodeLabel", "Code")) + " <strong>" + escapeHtml(code) + "</strong></div>";
  }

  function setDetailConfirmationCode(code) {
    const wrap = $("detailConfirmationWrap");
    const val = $("detailConfirmationCode");
    if (!wrap || !val) return;
    if (code) {
      val.textContent = code;
      wrap.classList.remove("hidden");
    } else {
      val.textContent = "—";
      wrap.classList.add("hidden");
    }
  }

  function absMediaUrl(u) {
    const t = (u || "").trim();
    if (!t) return "";
    if (t.startsWith("http://") || t.startsWith("https://")) return t;
    if (t.startsWith("/")) return location.origin + t;
    return t;
  }

  function invNormItem(raw) {
    if (!raw || typeof raw !== "object") return null;
    return {
      id: raw.id ?? raw.Id,
      uniqueId: String(raw.uniqueId ?? raw.UniqueId ?? ""),
      name: String(raw.name ?? raw.Name ?? ""),
      unit: String(raw.unit ?? raw.Unit ?? "")
    };
  }

  async function printOrderTicket(orderId, orderStatus) {
    if (!orderId || !token) return;
    const st = String(orderStatus || "").trim();
    const usePayment = st.toLowerCase() === "completed";
    const variant = usePayment ? "payment" : "client";
    const url =
      "/api/cashier/orders/" +
      encodeURIComponent(orderId) +
      "/ticket.html?variant=" +
      encodeURIComponent(variant);
    try {
      const res = await fetch(url, { headers: { Authorization: "Bearer " + token } });
      if (!res.ok) {
        console.warn("Ticket print: server returned", res.status);
        return;
      }
      const html = await res.text();
      if (!html || html.length < 20) {
        console.warn("Ticket print: empty receipt");
        return;
      }

      const iframe = document.createElement("iframe");
      iframe.setAttribute("title", t("receiptPrintTitle", "Receipt print"));
      iframe.style.cssText =
        "position:fixed;left:0;top:0;width:0;height:0;border:0;visibility:hidden";
      document.body.appendChild(iframe);

      const win = iframe.contentWindow;
      if (!win) {
        iframe.remove();
        return;
      }

      let cleaned = false;
      const cleanup = () => {
        if (cleaned) return;
        cleaned = true;
        iframe.remove();
      };
      win.addEventListener("afterprint", cleanup, { once: true });

      const doc = win.document;
      doc.open();
      doc.write(html);
      doc.close();
      doc.title = " ";

      setTimeout(() => {
        try {
          win.focus();
          win.print();
        } catch (err) {
          console.warn("Ticket print failed", err);
          cleanup();
        }
      }, 400);

      setTimeout(cleanup, 60000);
    } catch (e) {
      console.warn("Ticket print failed", e);
    }
  }

  async function api(url, method, body, auth, wrapOptions) {
    const headers = {};
    if (body) headers["Content-Type"] = "application/json";
    if (auth !== false && token) headers["Authorization"] = "Bearer " + token;
    const res = await fetch(url, { method, headers, body: body ? JSON.stringify(body) : undefined });
    const txt = await res.text();
    let json;
    try { json = JSON.parse(txt); } catch { json = { raw: txt }; }
    var result = { ok: res.ok, status: res.status, body: json };
    if (window.EliteApiError) window.EliteApiError.wrap(result, wrapOptions);
    return result;
  }

  function revokeImgBlob(el) {
    if (!el) return;
    const s = el.getAttribute("src") || "";
    if (s.startsWith("blob:")) {
      URL.revokeObjectURL(s);
      el.removeAttribute("src");
    }
  }

  async function setAuthImage(el, path) {
    revokeImgBlob(el);
    if (!el || !path || !token) {
      el?.classList.remove("show");
      return;
    }
    try {
      const res = await fetch(path, { headers: { Authorization: "Bearer " + token } });
      if (!res.ok) throw new Error("bad");
      const blob = await res.blob();
      el.src = URL.createObjectURL(blob);
      el.classList.add("show");
      el.onerror = () => el.classList.remove("show");
    } catch {
      el.classList.remove("show");
    }
  }

  const fmtUsd = n => "$ " + Number(n || 0).toFixed(2);
  const fmtFc = n => "FC " + Number(n || 0).toFixed(0);
  const toFc = usd => Number(usd || 0) * Number(config.usdToFcRate || 2250);
  const fcToUsd = fc => Math.round(Number(fc || 0) / Number(config.usdToFcRate || 2250) * 100) / 100;

  function menuSearchQuery() {
    return ($("menuSearch").value || "").trim().toLowerCase();
  }

  function renderMenuFromCatalog() {
    const q = menuSearchQuery();
    let rows = menuCatalogRows;
    if (q) {
      rows = menuCatalogRows.filter(row => {
        const p = row.p;
        const hay = (
          p.name + "\n" + row.category + "\n" + row.subCategory + "\n" +
          (p.description || "") + "\n" + (p.composition || "") + "\n" + row.ingText
        ).toLowerCase();
        return hay.includes(q);
      });
    }
    if (!rows.length) {
      $("menuBody").innerHTML =
        q ? '<p class="muted">' + escapeHtml(t("noDishesSearch", "No dishes match your search.")) + '</p>' : '<p class="muted">' + escapeHtml(t("noProducts", "No products.")) + '</p>';
      return;
    }
    const byCat = {};
    for (const row of rows) {
      const c = row.category;
      const sc = row.subCategory;
      if (!byCat[c]) byCat[c] = {};
      if (!byCat[c][sc]) byCat[c][sc] = [];
      byCat[c][sc].push(row);
    }
    const catNames = Object.keys(byCat).sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
    let html = "";
    for (const c of catNames) {
      html += '<div class="menu-section"><h3>' + escapeHtml(c) + '</h3>';
      const subs = byCat[c];
      const subNames = Object.keys(subs).sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
      for (const sc of subNames) {
        if (subNames.length > 1 || sc !== "General")
          html += '<h4 class="menu-sub">' + escapeHtml(sc) + '</h4>';
        html += '<div class="menu-grid">';
        const list = subs[sc].slice().sort((a, b) => a.p.name.localeCompare(b.p.name, undefined, { sensitivity: "base" }));
        for (const row of list) {
          const p = row.p;
          const photoFull = row.photoUrl ? absMediaUrl(row.photoUrl) : "";
          let thumb = '<div class="menu-thumb"><div class="menu-thumb-ph">🍽</div></div>';
          if (photoFull) {
            thumb =
              '<div class="menu-thumb">' +
              '<img src="' + escapeHtml(photoFull) + '" alt="" loading="lazy" ' +
              'onerror="this.classList.add(\'hidden\');var n=this.nextElementSibling;if(n)n.classList.remove(\'hidden\');">' +
              '<div class="menu-thumb-ph hidden" aria-hidden="true">🍽</div></div>';
          }
          html += '<div class="menu-card">' + thumb + '<div class="menu-card-text">';
          html += '<div class="title">' + escapeHtml(p.name) + '</div>';
          html += '<div class="price">$ ' + p.price.toFixed(2) + '</div>';
          if (p.description)
            html += '<div class="muted menu-card-desc">' + escapeHtml(p.description) + '</div>';
          html += '<div class="ing"><strong>' + escapeHtml(t("ingredients", "Ingredients")) + '</strong> · ' + escapeHtml(row.ingText || "—") + '</div>';
          if (p.composition)
            html += '<div class="comp"><strong>' + escapeHtml(t("composition", "Composition")) + '</strong> · ' + escapeHtml(p.composition) + '</div>';
          html += '</div></div>';
        }
        html += '</div>';
      }
      html += '</div>';
    }
    $("menuBody").innerHTML = html;
  }

  async function loadMenu() {
    const [rp, rpi, ri, rpub] = await Promise.all([
      api("/api/admin/data/products", "GET", null, true),
      api("/api/admin/data/productingredients", "GET", null, true),
      api("/api/admin/data/inventory", "GET", null, true),
      api("/api/public/menu/products", "GET", null, false)
    ]);
    if (!rp.ok) throw new Error(rp.body?.message || t("failedLoadProducts", "Failed to load products"));
    if (!rpi.ok) throw new Error(rpi.body?.message || t("failedLoadIngredients", "Failed to load product ingredients"));
    const photoById = {};
    if (rpub.ok && Array.isArray(rpub.body)) {
      for (const x of rpub.body) {
        const id = x.id ?? x.Id;
        const u = x.photoUrl ?? x.PhotoUrl;
        if (id != null && u != null && String(u).trim())
          photoById[id] = String(u).trim();
      }
    }
    const invItems = (ri.ok ? (ri.body.items || ri.body.Items) : []) || [];
    invByIdForMenu = {};
    for (const raw of invItems) {
      const n = invNormItem(raw);
      if (n) invByIdForMenu[n.id] = n;
    }
    const prodList = (rp.body.items || rp.body.Items || []).map(p => ({
      id: p.id ?? p.Id,
      name: p.name ?? p.Name ?? "",
      category: (p.category ?? p.Category ?? "").trim() || "Menu",
      subCategory: (p.subCategory ?? p.SubCategory ?? "").trim() || "General",
      price: Number(p.price ?? p.Price ?? 0),
      description: (p.description ?? p.Description ?? "").trim(),
      composition: (p.composition ?? p.Composition ?? "").trim()
    }));
    const byProd = {};
    for (const row of (rpi.body.items || rpi.body.Items || [])) {
      const pid = row.productId ?? row.ProductId;
      const iid = row.inventoryItemId ?? row.InventoryItemId;
      const qn = Number(row.quantity ?? row.Quantity ?? 0);
      if (!byProd[pid]) byProd[pid] = [];
      const inv = invByIdForMenu[iid];
      const nm = inv ? inv.name : ("#" + iid);
      const un = inv ? inv.unit : "";
      byProd[pid].push({ name: nm, qty: qn, unit: un });
    }
    menuCatalogRows = [];
    for (const p of prodList) {
      const ing = byProd[p.id] || [];
      const ingText = ing.length
        ? ing.map(x => (x.name || "").trim()).filter(Boolean).join(", ")
        : "";
      menuCatalogRows.push({
        p,
        category: p.category,
        subCategory: p.subCategory,
        ingText,
        photoUrl: photoById[p.id] || ""
      });
    }
    renderMenuFromCatalog();
  }

  async function loadPortalData() {
    const cfg = await api("/api/server/config");
    if (!cfg.ok) return false;
    const b = cfg.body;
    config = {
      restaurantName: b.restaurantName ?? b.RestaurantName ?? "Elite Restaurant",
      restaurantLogoUrl: b.restaurantLogoUrl ?? b.RestaurantLogoUrl ?? "",
      employeePhotoUrl: b.employeePhotoUrl ?? b.EmployeePhotoUrl ?? "",
      currencyDisplayMode: b.currencyDisplayMode ?? b.CurrencyDisplayMode ?? "Dual",
      usdToFcRate: Number(b.usdToFcRate ?? b.UsdToFcRate ?? 2250),
      taxPercent: Number(b.taxPercent ?? b.TaxPercent ?? 7),
      servicePercent: Number(b.servicePercent ?? b.ServicePercent ?? 10)
    };
    $("brandText").textContent = (config.restaurantName || "Elite Restaurant").toUpperCase();
    const logoEl = $("brandLogo");
    if (config.restaurantLogoUrl) await setAuthImage(logoEl, config.restaurantLogoUrl);
    else logoEl.classList.remove("show");
    const staffEl = $("staffPhoto");
    if (config.employeePhotoUrl) await setAuthImage(staffEl, config.employeePhotoUrl);
    else staffEl.classList.remove("show");
    return true;
  }

  function filterRows(rows, needle) {
    if (!needle || !needle.trim()) return rows;
    const n = needle.trim().toLowerCase();
    return rows.filter(o => JSON.stringify(o).toLowerCase().includes(n));
  }

  function localDayKeyFromTs(ts) {
    if (ts == null || ts === "") return "";
    const d = new Date(ts);
    if (Number.isNaN(d.getTime())) return "";
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return y + "-" + m + "-" + day;
  }

  function pastDayLabelForKey(key) {
    if (!key) return "";
    const parts = key.split("-").map(Number);
    if (parts.length !== 3 || parts.some(n => Number.isNaN(n))) return key;
    const dt = new Date(parts[0], parts[1] - 1, parts[2]);
    return dt.toLocaleDateString(undefined, { weekday: "short", year: "numeric", month: "short", day: "numeric" });
  }

  function collectSortedPastDayKeys(rows, getTs) {
    const set = new Set();
    for (const r of rows) {
      const k = localDayKeyFromTs(getTs(r));
      if (k) set.add(k);
    }
    return [...set].sort().reverse();
  }

  function renderCashierAlerts(lines) {
    const el = $("cashierAlerts");
    if (!el) return;
    if (!lines || !lines.length) { el.textContent = ""; return; }
    el.textContent = lines.join("\n");
  }

  async function loadCashierAlerts() {
    const r = await api("/api/cashier/alerts");
    if (r.ok && Array.isArray(r.body)) renderCashierAlerts(r.body);
  }

  async function loadOrdersTab() {
    const [a, p, pend, al] = await Promise.all([
      api("/api/cashier/orders/active"),
      api("/api/cashier/orders/past"),
      api("/api/cashier/orders/pending"),
      api("/api/cashier/alerts")
    ]);
    activeOrderRows = a.ok && Array.isArray(a.body) ? a.body : [];
    pastOrderRows = p.ok && Array.isArray(p.body) ? p.body : [];
    pendingRows = pend.ok && Array.isArray(pend.body) ? pend.body : [];
    if (al.ok && Array.isArray(al.body)) renderCashierAlerts(al.body);
    renderPendingOrders();
    renderActiveOrders();
    renderPastOrders();
    updateOrdersNavBadge();
    ordersUpdatedTime = new Date().toLocaleTimeString();
    $("ordersUpdated").textContent = t("updatedPrefix", "Updated") + " " + ordersUpdatedTime;
  }

  function updateOrdersNavBadge() {
    const badge = $("navOrdersBadge");
    if (!badge) return;
    const n = pendingRows.length;
    if (n > 0) {
      badge.textContent = String(n);
      badge.classList.remove("hidden");
    } else {
      badge.classList.add("hidden");
      badge.textContent = "";
    }
  }

  function renderPendingOrders() {
    const el = $("pendingOrders");
    if (!el) return;
    if (!pendingRows.length) { el.innerHTML = "<div class='muted' style='padding:16px;'>" + escapeHtml(t("noTicketsAwaiting", "No tickets awaiting validation.")) + "</div>"; return; }
    el.innerHTML = pendingRows.map(o => {
      const id = o.id ?? o.Id;
      const code = o.orderCode ?? o.OrderCode ?? "";
      const tbl = o.tableLabel ?? o.TableLabel ?? "";
      const srv = o.serverName ?? o.ServerName ?? "";
      const lines = o.linesSummary ?? o.LinesSummary ?? "";
      const gt = o.grandTotalText ?? o.GrandTotalText ?? "";
      const cat = o.createdAtText ?? o.CreatedAtText ?? "";
      return (
        "<div class='order-card'>" +
        "<button type='button' class='order-card__hit' data-open-p='" + id + "'>" +
        "<div><strong>" + escapeHtml(code) + "</strong> · " + escapeHtml(tbl) + "</div>" +
        orderCardCodeHtml(o) +
        "<div class='muted'>" + escapeHtml(t("serverLabel", "Server")) + ": " + escapeHtml(srv) + " · " + escapeHtml(cat) + "</div>" +
        "<div class='muted'>" + escapeHtml(lines) + "</div><div>" + escapeHtml(gt) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(o.status ?? o.Status ?? "") + "'>" + escapeHtml(t("printTicket", "Print ticket")) + "</button>" +
        "<button type='button' class='btn btn-primary btn-sm' data-release='" + id + "'>" + escapeHtml(t("releaseToKitchen", "Release to kitchen")) + "</button>" +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-p='" + id + "'>" + escapeHtml(t("cancel", "Cancel")) + "</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open-p]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-p"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
    el.querySelectorAll("[data-release]").forEach(b => b.onclick = async () => {
      const id = Number(b.getAttribute("data-release"));
      if (!confirm(t("confirmRelease", "Release to kitchen? Inventory will be deducted."))) return;
      if (window.EliteButtonBusy) EliteButtonBusy.set(b, true, "portals.common.saving", "Saving…");
      let r;
      try {
        r = await api("/api/cashier/orders/pending/" + id + "/release", "POST");
      } finally {
        if (window.EliteButtonBusy) EliteButtonBusy.set(b, false);
      }
      if (!r.ok) { alert(r.body?.message || t("releaseFailed", "Release failed")); return; }
      await loadOrdersTab();
    });
    el.querySelectorAll("[data-cancel-p]").forEach(b => b.onclick = async () => {
      const id = Number(b.getAttribute("data-cancel-p"));
      await EliteOrderCancel.cancelStaffOrder(id, async (orderId, passcode) =>
        api("/api/cashier/orders/pending/" + orderId + "/cancel", "POST", { passcode }),
        { confirmMessage: t("confirmCancelTicket", "Cancel this ticket? Stock has not been deducted."), onSuccess: () => loadOrdersTab() });
    });
  }

  function renderActiveOrders() {
    const el = $("activeOrders");
    const needle = ($("activeSearch") && $("activeSearch").value) || "";
    const rows = filterRows(activeOrderRows, needle);
    if (!rows.length) { el.innerHTML = "<div class='muted' style='padding:16px;'>" + escapeHtml(t("noActiveOrders", "No active orders.")) + "</div>"; return; }
    el.innerHTML = rows.map(o => {
      const id = o.id ?? o.Id;
      const oid = o.orderId ?? o.OrderId ?? "";
      const st = o.status ?? o.Status ?? "";
      const complete = o.showCompleteInOrders ?? o.ShowCompleteInOrders;
      return (
        "<div class='order-card'>" +
        "<button type='button' class='order-card__hit' data-open='" + id + "'>" +
        "<div><strong>" + escapeHtml(oid) + "</strong> <span class='muted'>" + escapeHtml(translateOrderStatus(st)) + "</span></div>" +
        orderCardCodeHtml(o) +
        "<div class='muted'>" + escapeHtml(o.tableNumber ?? o.TableNumber ?? "") + "</div>" +
        "<div class='muted'>" + escapeHtml(t("serverLabel", "Server")) + ": " + escapeHtml(o.serverName ?? o.ServerName ?? "") + "</div>" +
        "<div class='muted'>" + escapeHtml(o.items ?? o.Items ?? "") + "</div>" +
        "<div style=\"font-size:1.05rem;font-weight:700;margin-top:6px;\">" + fmtUsd(o.total ?? o.Total) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(st) + "'>" + escapeHtml(t("printTicket", "Print ticket")) + "</button>" +
        (complete ? "<button type='button' class='btn btn-primary btn-sm' data-complete='" + id + "'>" + escapeHtml(t("completePayment", "Complete payment")) + "</button>" : "") +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-o='" + id + "'>" + escapeHtml(t("cancel", "Cancel")) + "</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
    el.querySelectorAll("[data-complete]").forEach(b => b.onclick = () => openPaymentModal(Number(b.getAttribute("data-complete"))));
    el.querySelectorAll("[data-cancel-o]").forEach(b => b.onclick = async () => {
      const id = Number(b.getAttribute("data-cancel-o"));
      await EliteOrderCancel.cancelStaffOrder(id, async (orderId, passcode) =>
        api("/api/cashier/orders/" + orderId + "/cancel", "POST", { passcode }),
        { confirmMessage: t("confirmCancelOrder", "Cancel this order?"), onSuccess: () => loadOrdersTab() });
    });
  }

  function renderPastOrders() {
    const el = $("pastOrders");
    const sel = $("pastDaySelect");
    if (!el || !sel) return;
    const getTs = o => o.createdAt ?? o.CreatedAt;
    const dayKeys = collectSortedPastDayKeys(pastOrderRows, getTs);
    if (!pastOrderRows.length) {
      sel.innerHTML = "";
      el.innerHTML = "<div class='muted' style='padding:16px;'>" + escapeHtml(t("noPastOrders", "No past orders.")) + "</div>";
      return;
    }
    let rows;
    if (!dayKeys.length) {
      sel.innerHTML = "<option value=\"\">" + escapeHtml(t("pastAllOrders", "All past orders")) + "</option>";
      sel.disabled = true;
      sel.value = "";
      const needle = ($("pastSearch") && $("pastSearch").value) || "";
      rows = filterRows(pastOrderRows, needle);
    } else {
      sel.disabled = false;
      const counts = {};
      for (const o of pastOrderRows) {
        const k = localDayKeyFromTs(getTs(o));
        if (k) counts[k] = (counts[k] || 0) + 1;
      }
      const prevDay = sel.value;
      sel.innerHTML = dayKeys.map(k =>
        "<option value=\"" + escapeHtml(k) + "\">" + escapeHtml(pastDayLabelForKey(k)) + " (" + (counts[k] || 0) + ")</option>"
      ).join("");
      sel.value = dayKeys.includes(prevDay) ? prevDay : dayKeys[0];
      const day = sel.value;
      const needle = ($("pastSearch") && $("pastSearch").value) || "";
      rows = filterRows(
        pastOrderRows.filter(o => localDayKeyFromTs(getTs(o)) === day),
        needle
      );
    }
    if (!rows.length) {
      const searched = Boolean(($("pastSearch") && $("pastSearch").value || "").trim());
      const msg = searched
        ? t("noPastOrdersSearch", "No past orders match your search.")
        : (!dayKeys.length ? t("noPastOrdersShow", "No past orders to show.") : t("noPastOrdersDay", "No past orders for this day."));
      el.innerHTML = "<div class='muted' style='padding:16px;'>" + msg + "</div>";
      return;
    }
    el.innerHTML = rows.map(o => {
      const id = o.id ?? o.Id;
      const oid = o.orderId ?? o.OrderId ?? "";
      const st = o.status ?? o.Status ?? "";
      const time = o.time ?? o.Time ?? "";
      const tbl = escapeHtml(o.tableNumber ?? o.TableNumber ?? "");
      return (
        "<div class='order-card'>" +
        "<button type='button' class='order-card__hit' data-open-pt='" + id + "'>" +
        "<div class='muted' style='font-size:11px;margin-bottom:4px;'>" + escapeHtml(t("tapForDetails", "Tap for details")) + "</div>" +
        "<div><strong>" + escapeHtml(oid) + "</strong> <span class='muted'>" + escapeHtml(translateOrderStatus(st)) + "</span> · " + escapeHtml(time) + "</div>" +
        orderCardCodeHtml(o) +
        "<div class='muted'>" + tbl + " · " + fmtUsd(o.total ?? o.Total) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(st) + "'>" + escapeHtml(t("printTicket", "Print ticket")) + "</button>" +
        "</div></div>"
      );
    }).join("");
    el.querySelectorAll("[data-open-pt]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-pt"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
  }

  async function openOrderDetail(orderId) {
    const r = await api("/api/cashier/orders/" + orderId + "/invoice");
    if (!r.ok) { alert(r.body?.message || t("couldNotLoadOrder", "Could not load order")); return; }
    const d = r.body;
    detailOrderId = orderId;
    const linesRaw = d.lines ?? d.Lines ?? [];
    const code = d.orderCode ?? d.OrderCode ?? "";
    const st = d.status ?? d.Status ?? "";
    detailOrderStatus = String(st);
    $("detailOrderCode").textContent = code || "—";
    setDetailConfirmationCode(orderConfirmationCode(d));
    const pill = $("detailStatusPill");
    pill.textContent = translateOrderStatus(st) || st || "—";
    pill.className = "od-status-pill " + orderDetailStatusPillClass(st);
    const sub = d.subtotalUsd ?? d.SubtotalUsd ?? 0;
    const disc = d.discountAppliedUsd ?? d.DiscountAppliedUsd ?? 0;
    const tax = d.taxUsd ?? d.TaxUsd ?? 0;
    const svc = d.serviceUsd ?? d.ServiceUsd ?? 0;
    const gusd = d.grandTotalUsd ?? d.GrandTotalUsd ?? 0;
    const gfc = d.grandTotalFc ?? d.GrandTotalFc ?? 0;
    const cn = d.customerNotes ?? d.CustomerNotes ?? "-";
    const an = d.allergyNotes ?? d.AllergyNotes ?? "-";
    const origin = d.orderOrigin ?? d.OrderOrigin ?? "";
    const src = d.orderSource ?? d.OrderSource ?? "";
    const pt = d.paymentTiming ?? d.PaymentTiming ?? "";
    const dFee = Number(d.deliveryFeeUsd ?? d.DeliveryFeeUsd ?? 0);
    const merch = Number(d.merchandiseGrandUsd ?? d.MerchandiseGrandUsd ?? 0);
    const taxable = Number(d.taxableSubtotalUsd ?? d.TaxableSubtotalUsd ?? 0);
    const tableLabel = d.tableLabel ?? d.TableLabel ?? "";
    const serverName = d.serverName ?? d.ServerName ?? "";
    const lineRows = linesRaw.map(l => {
      const q = l.quantity ?? l.Quantity;
      const name = escapeHtml(String(l.name ?? l.Name ?? ""));
      const up = fmtUsd(l.unitPrice ?? l.UnitPrice);
      const lt = fmtUsd(l.lineTotal ?? l.LineTotal);
      return (
        "<div class=\"od-line-row\" role=\"row\">" +
        "<span class=\"od-line-qty\" role=\"cell\">" + escapeHtml(String(q)) + "</span>" +
        "<span class=\"od-line-name\" role=\"cell\">" + name + "</span>" +
        "<span class=\"od-num\" role=\"cell\">" + escapeHtml(up) + "</span>" +
        "<span class=\"od-num\" role=\"cell\">" + escapeHtml(lt) + "</span>" +
        "</div>"
      );
    }).join("");
    const itemsBlock = linesRaw.length
      ? (
          "<div class=\"od-items-table\" role=\"table\">" +
          "<div class=\"od-items-head\" role=\"row\"><span>" + escapeHtml(t("detail.qty", "Qty")) + "</span><span>" + escapeHtml(t("detail.item", "Item")) + "</span><span class=\"od-num\">" + escapeHtml(t("detail.unit", "Unit")) + "</span><span class=\"od-num\">" + escapeHtml(t("detail.line", "Line")) + "</span></div>" +
          lineRows +
          "</div>"
        )
      : "<div class=\"muted\" style=\"padding:12px 14px;font-size:13px;\">" + escapeHtml(t("detail.noLineItems", "No line items.")) + "</div>";
    const deliveryRow = dFee > 0
      ? "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.deliveryFee", "Delivery fee (20%)")) + "</span><span>" + escapeHtml(fmtUsd(dFee)) + "</span></div>"
      : "";
    const grandLine = escapeHtml(fmtUsd(gusd) + " (" + fmtFc(gfc) + ")");
    $("detailBodyScroll").innerHTML =
      "<section class=\"od-section\" aria-label=\"" + escapeHtml(t("detail.details", "Details")) + "\">" +
      "<h4 class=\"od-section-title\">" + escapeHtml(t("detail.details", "Details")) + "</h4>" +
      "<div class=\"od-meta-grid\">" +
      (orderConfirmationCode(d)
        ? "<div class=\"od-meta-cell\" style=\"grid-column:1/-1;\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.confirmationCode", "Confirmation code")) + "</span><span class=\"od-meta-v\" style=\"font-family:ui-monospace,monospace;font-size:1.15rem;font-weight:700;letter-spacing:0.14em;\">" + escapeHtml(orderConfirmationCode(d)) + "</span></div>"
        : "") +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.table", "Table")) + "</span><span class=\"od-meta-v\">" + escapeHtml(translateMetaValue("table", tableLabel)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.server", "Server")) + "</span><span class=\"od-meta-v\">" + escapeHtml(String(serverName)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.origin", "Origin")) + "</span><span class=\"od-meta-v\">" + escapeHtml(translateMetaValue("origin", origin)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.source", "Source")) + "</span><span class=\"od-meta-v\">" + escapeHtml(translateMetaValue("source", src)) + "</span></div>" +
      "<div class=\"od-meta-cell\" style=\"grid-column:1/-1;\"><span class=\"od-meta-k\">" + escapeHtml(t("detail.paymentTiming", "Payment timing")) + "</span><span class=\"od-meta-v\">" + escapeHtml(translateMetaValue("payment", pt)) + "</span></div>" +
      "</div></section>" +
      "<section class=\"od-section\" aria-label=\"" + escapeHtml(t("detail.lineItems", "Line items")) + "\">" +
      "<h4 class=\"od-section-title\">" + escapeHtml(t("detail.lineItems", "Line items")) + "</h4>" + itemsBlock + "</section>" +
      "<section class=\"od-section\" aria-label=\"" + escapeHtml(t("detail.totals", "Totals")) + "\">" +
      "<h4 class=\"od-section-title\">" + escapeHtml(t("detail.totals", "Totals")) + "</h4>" +
      "<div class=\"od-totals\">" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.lineSubtotal", "Line subtotal")) + "</span><span>" + escapeHtml(fmtUsd(sub)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.taxableAfterDiscount", "Taxable (after discount)")) + "</span><span>" + escapeHtml(fmtUsd(taxable)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.discount", "Discount")) + "</span><span>" + escapeHtml(fmtUsd(disc)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.tax", "Tax")) + "</span><span>" + escapeHtml(fmtUsd(tax)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.service", "Service")) + "</span><span>" + escapeHtml(fmtUsd(svc)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>" + escapeHtml(t("detail.merchandiseTotal", "Merchandise total")) + "</span><span>" + escapeHtml(fmtUsd(merch)) + "</span></div>" +
      deliveryRow +
      "<div class=\"od-total-row od-total-row--grand\"><span>" + escapeHtml(t("detail.grandTotal", "Grand total")) + "</span><span>" + grandLine + "</span></div>" +
      "</div></section>" +
      "<section class=\"od-section\" aria-label=\"" + escapeHtml(t("detail.notesAllergies", "Notes & allergies")) + "\">" +
      "<h4 class=\"od-section-title\">" + escapeHtml(t("detail.notesAllergies", "Notes & allergies")) + "</h4>" +
      "<div class=\"od-notes-grid\">" +
      "<div class=\"od-note\"><div class=\"od-note-title\">" + escapeHtml(t("detail.customerNotes", "Customer notes")) + "</div><div class=\"od-note-body\">" + orderDetailNoteInnerHtml(cn) + "</div></div>" +
      "<div class=\"od-note od-note--allergy\"><div class=\"od-note-title\">" + escapeHtml(t("detail.allergy", "Allergy")) + "</div><div class=\"od-note-body\">" + orderDetailNoteInnerHtml(an) + "</div></div>" +
      "</div></section>";
    $("detailBodyScroll").scrollTop = 0;
    $("orderDetailModal").classList.remove("hidden");
    applyCashierStaticI18n($("orderDetailModal"));
  }

  function closeOrderDetail() {
    $("orderDetailModal").classList.add("hidden");
    setDetailConfirmationCode("");
    detailOrderId = 0;
    detailOrderStatus = "";
  }

  function parseAmount(text) {
    const t = String(text ?? "").trim();
    if (!t) return 0;
    const inv = Number(t);
    return Number.isFinite(inv) && inv >= 0 ? inv : 0;
  }

  const PAY_AMOUNT_INPUT_IDS = {
    PaidUsd: "payUsdInput",
    PaidFc: "payFcInput",
    ChangeUsd: "chgUsdInput",
    ChangeFc: "chgFcInput"
  };

  function getPayAmountInputText(target) {
    switch (target) {
      case "PaidUsd": return paidUsdInput;
      case "PaidFc": return paidFcInput;
      case "ChangeUsd": return changeUsdInput;
      case "ChangeFc": return changeFcInput;
      default: return "";
    }
  }

  function setPayAmountInputText(target, value) {
    const v = String(value ?? "");
    switch (target) {
      case "PaidUsd": paidUsdInput = v; break;
      case "PaidFc": paidFcInput = v; break;
      case "ChangeUsd": changeUsdInput = v; break;
      case "ChangeFc": changeFcInput = v; break;
      default: return;
    }
  }

  function getPayAmountInputEl(target) {
    const id = PAY_AMOUNT_INPUT_IDS[target];
    return id ? $(id) : null;
  }

  function sanitizePayAmountInput(raw) {
    let s = String(raw ?? "").replace(/[^\d.]/g, "");
    const dot = s.indexOf(".");
    if (dot >= 0) s = s.slice(0, dot + 1) + s.slice(dot + 1).replace(/\./g, "");
    return s;
  }

  function syncPayAmountInputsFromState() {
    document.querySelectorAll("#paymentModal .pay-amount-input[data-numpad-target]").forEach(el => {
      const t = el.getAttribute("data-numpad-target");
      if (el === document.activeElement && t === numpadTarget) return;
      el.value = getPayAmountInputText(t);
    });
  }

  function focusPayAmountInput(target) {
    const el = getPayAmountInputEl(target);
    if (!el) return;
    el.focus();
    try { el.select(); } catch (_) {}
  }

  function paymentDueFc() {
    return Math.round(toFc(paymentDueUsd));
  }

  function getPaidUsd() { return parseAmount(paidUsdInput); }
  function getPaidFc() { return parseAmount(paidFcInput); }
  function getTotalPaidUsdEq() {
    return Math.round((getPaidUsd() + fcToUsd(getPaidFc())) * 100) / 100;
  }
  function getRemainingUsd() {
    return Math.max(0, Math.round((paymentDueUsd - getTotalPaidUsdEq()) * 100) / 100);
  }
  function getChangeUsd() {
    return Math.max(0, Math.round((getTotalPaidUsdEq() - paymentDueUsd) * 100) / 100);
  }
  function getChangeAllocUsd() { return parseAmount(changeUsdInput); }
  function getChangeAllocFc() { return parseAmount(changeFcInput); }
  function getChangeAllocUsdEq() {
    return Math.round((getChangeAllocUsd() + fcToUsd(getChangeAllocFc())) * 100) / 100;
  }
  function getRemainingChangeToAlloc() {
    return Math.max(0, Math.round((getChangeUsd() - getChangeAllocUsdEq()) * 100) / 100);
  }
  function canGoToChange() {
    return getRemainingUsd() <= 0.001 && (getPaidUsd() > 0 || getPaidFc() > 0);
  }
  function canConfirmChange() {
    return Math.abs(getChangeAllocUsdEq() - getChangeUsd()) <= 0.02;
  }

  function getNumpadTargetText() {
    return getPayAmountInputText(numpadTarget);
  }

  function setNumpadTargetText(value) {
    const v = String(value ?? "");
    setPayAmountInputText(numpadTarget, v);
    const el = getPayAmountInputEl(numpadTarget);
    if (el) el.value = v;
    updatePaymentFlowUI();
  }

  function setNumpadTarget(target, opts) {
    if (target !== "PaidUsd" && target !== "PaidFc" && target !== "ChangeUsd" && target !== "ChangeFc") return;
    numpadTarget = target;
    updatePaymentFlowUI();
    if (opts?.focus !== false) focusPayAmountInput(target);
  }

  function onPayAmountFieldInput(e) {
    const inp = e.target.closest(".pay-amount-input[data-numpad-target]");
    if (!inp) return;
    const target = inp.getAttribute("data-numpad-target");
    const sanitized = sanitizePayAmountInput(inp.value);
    if (inp.value !== sanitized) inp.value = sanitized;
    numpadTarget = target;
    setPayAmountInputText(target, sanitized);
    updatePaymentFlowUI();
  }

  function appendNumpadDigit(digit) {
    if (!digit) return;
    setNumpadTargetText(getNumpadTargetText() + String(digit).trim());
  }

  function appendNumpadDot() {
    const current = getNumpadTargetText();
    if (current.includes(".")) return;
    setNumpadTargetText(current ? current + "." : "0.");
  }

  function backspaceNumpad() {
    const current = getNumpadTargetText();
    if (!current) return;
    setNumpadTargetText(current.slice(0, -1));
  }

  function showPaymentStep(step) {
    paymentStep = step;
    $("payStepPayment").classList.toggle("hidden", step !== "payment");
    $("payStepChange").classList.toggle("hidden", step !== "change");
  }

  function updatePaymentFlowUI() {
    const dueFc = paymentDueFc();
    const remUsd = getRemainingUsd();
    const chgUsd = getChangeUsd();
    const remFc = Math.round(toFc(remUsd));
    const chgFc = Math.round(toFc(chgUsd));

    $("payOrderCode").textContent = paymentOrderCode || "—";
    $("payChangeOrderCode").textContent = paymentOrderCode || "—";
    $("payDueUsd").textContent = fmtUsd(paymentDueUsd);
    $("payDueFc").textContent = fmtFc(dueFc);
    $("payKpiDueUsd").textContent = fmtUsd(paymentDueUsd);
    $("payKpiDueFc").textContent = fmtFc(dueFc);
    $("payKpiRemUsd").textContent = fmtUsd(remUsd);
    $("payKpiRemFc").textContent = fmtFc(remFc);
    $("payKpiChgUsd").textContent = fmtUsd(chgUsd);
    $("payKpiChgFc").textContent = fmtFc(chgFc);

    syncPayAmountInputsFromState();
    $("payGoToChange").disabled = !canGoToChange();

    $("payChangeDueUsd").textContent = fmtUsd(chgUsd);
    $("payChangeDueFc").textContent = fmtFc(chgFc);
    $("payAllocDueUsd").textContent = t("pay.duePrefix", "Due") + " " + fmtUsd(chgUsd);
    $("payAllocDueFc").textContent = t("pay.duePrefix", "Due") + " " + fmtFc(chgFc);

    const remAllocUsd = getRemainingChangeToAlloc();
    $("payRemAllocUsd").textContent = fmtUsd(remAllocUsd);
    $("payRemAllocFc").textContent = fmtFc(Math.round(toFc(remAllocUsd)));
    $("payConfirm").disabled = !canConfirmChange();

    document.querySelectorAll("#paymentModal .pay-amount-input[data-numpad-target]").forEach(el => {
      const t = el.getAttribute("data-numpad-target");
      el.classList.toggle("is-active", t === numpadTarget);
    });
  }

  function resetPaymentFlowState() {
    paymentStep = "payment";
    numpadTarget = "PaidUsd";
    paidUsdInput = "";
    paidFcInput = "";
    changeUsdInput = "";
    changeFcInput = "";
    showPaymentStep("payment");
  }

  function openPaymentModal(orderId) {
    paymentTargetOrderId = orderId;
    resetPaymentFlowState();
    api("/api/cashier/orders/" + orderId + "/invoice").then(r => {
      if (!r.ok) { alert(r.body?.message || t("loadFailed", "Load failed")); return; }
      paymentDueUsd = Number(r.body.grandTotalUsd ?? r.body.GrandTotalUsd ?? 0);
      paymentOrderCode = String(r.body.orderCode ?? r.body.OrderCode ?? orderId);
      const clientName = r.body.clientFullName ?? r.body.ClientFullName ?? "";
      const clientDebt = Number(r.body.clientDebtBalanceUsd ?? r.body.ClientDebtBalanceUsd ?? 0);
      paymentHasLinkedClient = !!(r.body.restaurantClientId ?? r.body.RestaurantClientId);
      paymentCanAddToDebt = !!(r.body.canAddToDebt ?? r.body.CanAddToDebt);
      const block = $("payClientBlock");
      if (block) {
        if (paymentHasLinkedClient && clientName) {
          block.classList.remove("hidden");
          $("payClientName").textContent = clientName;
          $("payClientDebt").textContent = fmtUsd(clientDebt);
          const debtRadio = $("payOnAccountRadio");
          const debtLbl = $("payOnAccountLabel");
          if (debtRadio) debtRadio.disabled = !paymentCanAddToDebt;
          if (debtLbl) debtLbl.title = paymentCanAddToDebt ? "" : t("pay.debtCapTitle", "Debt cap reached — collect payment first.");
          document.querySelectorAll('input[name="paySettlement"]').forEach(el => { el.checked = el.value === "PayNow"; });
        } else {
          block.classList.add("hidden");
        }
      }
      updatePaymentFlowUI();
      $("paymentModal").classList.remove("hidden");
      applyCashierStaticI18n($("paymentModal"));
    });
  }

  function closePaymentModal() {
    $("paymentModal").classList.add("hidden");
    paymentTargetOrderId = 0;
    paymentOrderCode = "";
    paymentDueUsd = 0;
    resetPaymentFlowState();
  }

  function goToChangeStep() {
    if (!canGoToChange()) {
      if (getPaidUsd() <= 0 && getPaidFc() <= 0) alert(t("pay.enterAmountPaid", "Enter amount paid."));
      else alert(t("pay.lessThanDue", "Payment is less than amount due."));
      return;
    }
    const chg = getChangeUsd();
    changeUsdInput = chg > 0 ? String(chg) : "";
    changeFcInput = "";
    numpadTarget = "ChangeUsd";
    showPaymentStep("change");
    updatePaymentFlowUI();
  }

  function backToPaymentStep() {
    showPaymentStep("payment");
    numpadTarget = "PaidUsd";
    updatePaymentFlowUI();
  }

  function setView(v) {
    currentView = v;
    $("viewOrders").classList.toggle("hidden", v !== "orders");
    $("viewMenu").classList.toggle("hidden", v !== "menu");
    $("navOrders").classList.toggle("active", v === "orders");
    $("navMenu").classList.toggle("active", v === "menu");
    if (v === "orders") loadOrdersTab().catch(() => {});
    if (v === "menu") loadMenu().catch(e => alert(e.message || String(e)));
  }

  function setHubPill(state) {
    hubPillState = state;
    const el = $("hubPill");
    el.classList.remove("ok", "warn", "off");
    if (state === "live") { el.textContent = t("hubLive", "Live: connected"); el.classList.add("ok"); }
    else if (state === "degraded") { el.textContent = t("hubReconnecting", "Live: reconnecting"); el.classList.add("warn"); }
    else { el.textContent = t("hubPolling", "Live: polling"); el.classList.add("off"); }
  }

  let orderReadyHubDebounce = null;
  function scheduleOrderReadyFlash(msg) {
    const el = $("orderReadyFlash");
    if (!el) return;
    el.textContent = msg || "";
    if (orderReadyHubDebounce) clearTimeout(orderReadyHubDebounce);
    orderReadyHubDebounce = setTimeout(() => { el.textContent = ""; orderReadyHubDebounce = null; }, 10000);
  }

  /**
   * Browsers often block audio until a user gesture. Logging in counts as one; we warm the AudioContext then.
   * Hub-driven beeps after that may still be silenced in strict modes — silent failure is OK.
   */
  function unlockCashierAudioFromUserGesture() {
    if (window.EliteOrderStageAlert) window.EliteOrderStageAlert.unlockAudio();
    else {
      try {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return;
        const ctx = new Ctx();
        if (ctx.state === "suspended") void ctx.resume();
        void ctx.close();
      } catch (_) {}
    }
  }

  function playOrderReadyBeep() {
    if (window.EliteOrderStageAlert) {
      window.EliteOrderStageAlert.playRing();
      return;
    }
    try {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      if (!Ctx) return;
      const ctx = new Ctx();
      const o = ctx.createOscillator();
      const g = ctx.createGain();
      o.type = "sine";
      o.frequency.value = 880;
      o.connect(g);
      g.connect(ctx.destination);
      g.gain.setValueAtTime(0.0001, ctx.currentTime);
      g.gain.exponentialRampToValueAtTime(0.07, ctx.currentTime + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.14);
      o.start(ctx.currentTime);
      o.stop(ctx.currentTime + 0.15);
      void ctx.resume().finally(() => {
        setTimeout(() => {
          try { ctx.close(); } catch (_) {}
        }, 300);
      });
    } catch (_) {}
  }

  async function startOrderHub() {
    if (typeof signalR === "undefined") return;
    if (orderHubConnection) {
      try { await orderHubConnection.stop(); } catch (_) {}
      orderHubConnection = null;
    }
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(location.origin + "/hubs/order", { accessTokenFactory: () => token || "" })
      .withAutomaticReconnect()
      .build();
    function refreshCashierOrdersFromHub() {
      void loadCashierAlerts();
      if (currentView === "orders") loadOrdersTab().catch(() => {});
      else {
        api("/api/cashier/orders/pending").then(r => {
          if (r.ok && Array.isArray(r.body)) {
            pendingRows = r.body;
            updateOrdersNavBadge();
          }
        }).catch(() => {});
      }
    }

    conn.on("CashierOrderBoardChanged", () => {
      refreshCashierOrdersFromHub();
    });
    if (window.EliteOrderStageAlert) {
      EliteOrderStageAlert.wireHubConnection(conn, {
        audience: "Cashier",
        onFlash: scheduleOrderReadyFlash,
        onNotify: refreshCashierOrdersFromHub
      });
    }
    conn.on("OrderReady", (payload) => {
      const p = payload && typeof payload === "object" ? payload : {};
      const code = (p.orderCode ?? p.OrderCode ?? "").toString().replace(/^#/, "");
      const table = (p.tableLabel ?? p.TableLabel ?? "").toString();
      const guest = (p.guestLabel ?? p.GuestLabel ?? "").toString();
      const disp = (p.customerFulfillmentDisplay ?? p.CustomerFulfillmentDisplay ?? "").toString();
      const origin = (p.orderOrigin ?? p.OrderOrigin ?? "").toString();
      const loc = table.trim() || guest.trim() || "—";
      const codePart = code ? "#" + code : "";
      const bits = [t("orderReadyPrefix", "Order ready"), codePart, loc, disp].filter(x => x && String(x).trim());
      const msg = bits.join(" · ") + (origin ? " (" + origin + ")" : "");
      scheduleOrderReadyFlash(msg);
      refreshCashierOrdersFromHub();
    });
    conn.onreconnecting(() => setHubPill("degraded"));
    conn.onreconnected(() => {
      setHubPill("live");
      conn.invoke("JoinServer").catch(() => {});
      conn.invoke("JoinCashierDashboard").catch(() => {});
    });
    conn.onclose(() => setHubPill("off"));
    if (window.EliteSignalRBanner) {
      EliteSignalRBanner.wire(conn, function () {
        setHubPill("live");
        conn.invoke("JoinServer").catch(function () {});
        conn.invoke("JoinCashierDashboard").catch(function () {});
        refreshCashierOrdersFromHub();
      });
    }
    try {
      await conn.start();
      await conn.invoke("JoinServer");
      await conn.invoke("JoinCashierDashboard");
      orderHubConnection = conn;
      setHubPill("live");
    } catch (e) {
      console.warn("[Cashier] hub", e);
      orderHubConnection = null;
      setHubPill("off");
    }
  }

  function stopHubAndPoll() {
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
    if (orderHubConnection) {
      try { orderHubConnection.stop(); } catch (_) {}
      orderHubConnection = null;
    }
  }

  function startPolling() {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = setInterval(() => {
      if (!token) return;
      if (typeof signalR !== "undefined" && orderHubConnection && orderHubConnection.state === signalR.HubConnectionState.Connected) return;
      if (currentView === "orders") loadOrdersTab().catch(() => {});
      loadCashierAlerts().catch(() => {});
    }, 28000);
  }

  $("btnLogin").onclick = async () => {
    $("loginErr").classList.add("hidden");
    const staffId = ($("staffId").value || "").trim();
    const pin = $("pin").value || "";
    if (!staffId || !pin) {
      $("loginErr").textContent = t("auth.enterIdAndPin", "Enter sign-in ID and PIN.");
      $("loginErr").classList.remove("hidden");
      return;
    }
    const res = await api("/api/auth/login", "POST", { staffId, pin, portal: "Cashier" }, false, { silent: true });
    if (!res.ok || !res.body?.accessToken) {
      $("loginErr").textContent = t("loginFailed", "Login failed ({{status}}). {{detail}}", {
        status: String(res.status),
        detail: res.body?.message || res.body?.title || t("loginFailedDetail", "Check cashier role.")
      });
      $("loginErr").classList.remove("hidden");
      return;
    }
    token = res.body.accessToken;
    me = res.body;
    if (window.ElitePortalSession) window.ElitePortalSession.save(PORTAL_ID, token, me);
    $("sessionLabel").textContent = (me.name || "") + " (" + (me.signInId || me.employeeUniqueId || "") + ")";
    $("loginWrap").classList.add("hidden");
    $("app").classList.remove("hidden");
    await loadPortalData();
    await loadOrdersTab();
    setView("orders");
    void startOrderHub();
    unlockCashierAudioFromUserGesture();
    startPolling();
  };

  function clearCashierSession() {
    stopHubAndPoll();
    if (window.ElitePortalSession) window.ElitePortalSession.clear(PORTAL_ID);
    token = "";
    me = null;
    revokeImgBlob($("brandLogo"));
    revokeImgBlob($("staffPhoto"));
    $("brandLogo").classList.remove("show");
    $("staffPhoto").classList.remove("show");
    $("app").classList.add("hidden");
    $("loginWrap").classList.remove("hidden");
    setHubPill("off");
  }

  $("btnLogout").onclick = () => clearCashierSession();

  async function tryRestoreCashierSession() {
    if (!window.ElitePortalSession) return;
    const saved = window.ElitePortalSession.load(PORTAL_ID);
    if (!saved.token) return;
    token = saved.token;
    me = saved.me;
    $("sessionLabel").textContent = me
      ? (me.name || "") + " (" + (me.signInId || me.employeeUniqueId || "") + ")"
      : t("cashierSession", "Cashier session");
    $("loginWrap").classList.add("hidden");
    $("app").classList.remove("hidden");
    const ok = await loadPortalData();
    if (!ok) {
      clearCashierSession();
      return;
    }
    await loadOrdersTab();
    setView("orders");
    void startOrderHub();
    startPolling();
  }

  $("btnRefreshAll").onclick = async () => {
    await loadPortalData();
    await loadOrdersTab();
  };

  $("navOrders").onclick = () => setView("orders");
  $("navMenu").onclick = () => setView("menu");
  $("btnRefreshMenu").onclick = () => loadMenu().catch(e => alert(e.message || String(e)));
  $("menuSearch").oninput = () => renderMenuFromCatalog();

  $("detailClose").onclick = closeOrderDetail;
  $("detailPrintTicket").onclick = () => printOrderTicket(detailOrderId, detailOrderStatus);
  $("payPrintTicket").onclick = () => printOrderTicket(paymentTargetOrderId, "");
  $("orderDetailModal").addEventListener("click", e => { if (e.target.id === "orderDetailModal") closeOrderDetail(); });

  $("payCancel").onclick = closePaymentModal;
  $("payChangeCancel").onclick = closePaymentModal;
  $("payChangeBack").onclick = backToPaymentStep;
  $("payGoToChange").onclick = goToChangeStep;
  $("paymentModal").addEventListener("click", e => { if (e.target.id === "paymentModal") closePaymentModal(); });

  $("paymentModal").addEventListener("input", onPayAmountFieldInput);
  $("paymentModal").addEventListener("focusin", e => {
    const inp = e.target.closest(".pay-amount-input[data-numpad-target]");
    if (!inp) return;
    setNumpadTarget(inp.getAttribute("data-numpad-target"), { focus: false });
  });

  $("paymentModal").addEventListener("click", e => {
    const selectBtn = e.target.closest("[data-select-numpad]");
    if (selectBtn) {
      setNumpadTarget(selectBtn.getAttribute("data-select-numpad"));
      return;
    }
    const digitBtn = e.target.closest("[data-numpad-digit]");
    if (digitBtn) {
      appendNumpadDigit(digitBtn.getAttribute("data-numpad-digit"));
      return;
    }
    if (e.target.closest("[data-numpad-dot]")) {
      appendNumpadDot();
      return;
    }
    if (e.target.closest("[data-numpad-backspace]")) {
      backspaceNumpad();
    }
  });

  function selectedPaymentSettlement() {
    const el = document.querySelector('input[name="paySettlement"]:checked');
    return el ? el.value : "PayNow";
  }

  $("payConfirm").onclick = async () => {
    if (!canGoToChange()) {
      if (getPaidUsd() <= 0 && getPaidFc() <= 0) { alert(t("pay.enterAmountPaid", "Enter amount paid.")); return; }
      alert(t("pay.lessThanDue", "Payment is less than amount due."));
      return;
    }
    if (!canConfirmChange()) {
      alert(t("pay.allocMustMatch", "Change allocation must match change due."));
      return;
    }
    const paidUsd = getPaidUsd();
    const paidFc = getPaidFc();
    const cUsd = getChangeAllocUsd();
    const cFc = getChangeAllocFc();
    const settlement = paymentHasLinkedClient ? selectedPaymentSettlement() : "PayNow";
    if (settlement === "OnAccount") {
      if (!paymentCanAddToDebt) { alert(t("pay.cannotAddDebt", "Cannot add more debt — cap reached.")); return; }
      if (!confirm(t("pay.confirmAddDebt", "Add this ticket total to the client's account (no cash collected now)?"))) return;
      const rDebt = await api("/api/cashier/orders/" + paymentTargetOrderId + "/complete", "POST", {
        paymentCurrencyCode: "MIXED",
        paidUsd: 0,
        paidFc: 0,
        changeUsd: 0,
        changeFc: 0,
        settlement: "OnAccount"
      });
      if (!rDebt.ok) { alert(rDebt.body?.message || t("pay.completeFailed", "Complete failed")); return; }
      closePaymentModal();
      closeOrderDetail();
      await loadOrdersTab();
      return;
    }
    const r = await api("/api/cashier/orders/" + paymentTargetOrderId + "/complete", "POST", {
      paymentCurrencyCode: "MIXED",
      paidUsd: paidUsd,
      paidFc: paidFc,
      changeUsd: cUsd,
      changeFc: cFc,
      settlement: "PayNow"
    });
    if (!r.ok) { alert(r.body?.message || t("pay.completeFailed", "Complete failed")); return; }
    closePaymentModal();
    closeOrderDetail();
    await loadOrdersTab();
  };

  $("activeSearch").addEventListener("input", renderActiveOrders);
  $("pastSearch").addEventListener("input", renderPastOrders);
  $("pastDaySelect").addEventListener("change", renderPastOrders);

  if (window.location.protocol === "file:") {
    $("loginErr").textContent = t("fileProtocolHint", "Open this page from the API site (e.g. http://localhost:8080/cashier/) so login works.");
    $("loginErr").classList.remove("hidden");
  } else {
    void tryRestoreCashierSession();
  }

  void (async function initCashierI18n() {
    if (!window.EliteI18n) return;
    await EliteI18n.init();
    applyCashierStaticI18n();
    EliteI18n.mountSwitcher("#cashierLangLogin");
    EliteI18n.mountSwitcher("#cashierLangSidebar");
  })();
})();
