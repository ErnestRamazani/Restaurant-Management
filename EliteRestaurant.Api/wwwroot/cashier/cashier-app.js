(function () {
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
    if (s.includes("pending")) return "od-status-pill--pending";
    if (s.includes("waiting")) return "od-status-pill--wait";
    if (s.includes("kitchen")) return "od-status-pill--kitchen";
    if (s.includes("ready")) return "od-status-pill--ready";
    if (s.includes("served")) return "od-status-pill--served";
    return "";
  }

  function orderDetailNoteInnerHtml(bodyRaw) {
    const t = String(bodyRaw ?? "").trim();
    if (!t || t === "-") return "<span class=\"od-note-empty\">None noted</span>";
    return escapeHtml(t);
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
    const url = "/api/cashier/orders/" + orderId + "/ticket.pdf?variant=" + encodeURIComponent(variant);
    try {
      const res = await fetch(url, { headers: { Authorization: "Bearer " + token } });
      if (!res.ok) {
        let msg = "Could not load ticket PDF.";
        try {
          const j = await res.json();
          msg = j.message || j.title || msg;
        } catch (_) {
          const t = await res.text();
          if (t) msg = t.slice(0, 200);
        }
        alert(msg);
        return;
      }
      const blob = await res.blob();
      const blobUrl = URL.createObjectURL(blob);
      const w = window.open(blobUrl, "_blank", "noopener");
      if (w) {
        w.addEventListener("load", () => {
          try { w.focus(); w.print(); } catch (_) {}
        }, { once: true });
      } else {
        alert("Allow pop-ups to print the ticket PDF.");
      }
      setTimeout(() => URL.revokeObjectURL(blobUrl), 120000);
    } catch (e) {
      alert(e.message || "Print failed");
    }
  }

  async function api(url, method, body, auth) {
    const headers = {};
    if (body) headers["Content-Type"] = "application/json";
    if (auth !== false && token) headers["Authorization"] = "Bearer " + token;
    const res = await fetch(url, { method, headers, body: body ? JSON.stringify(body) : undefined });
    const txt = await res.text();
    let json;
    try { json = JSON.parse(txt); } catch { json = { raw: txt }; }
    return { ok: res.ok, status: res.status, body: json };
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
        q ? '<p class="muted">No dishes match your search.</p>' : '<p class="muted">No products.</p>';
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
          html += '<div class="ing"><strong>Ingredients</strong> · ' + escapeHtml(row.ingText || "—") + '</div>';
          if (p.composition)
            html += '<div class="comp"><strong>Composition</strong> · ' + escapeHtml(p.composition) + '</div>';
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
    if (!rp.ok) throw new Error(rp.body?.message || "Failed to load products");
    if (!rpi.ok) throw new Error(rpi.body?.message || "Failed to load product ingredients");
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
    $("ordersUpdated").textContent = "Updated " + new Date().toLocaleTimeString();
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
    if (!pendingRows.length) { el.innerHTML = "<div class='muted' style='padding:16px;'>No tickets awaiting validation.</div>"; return; }
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
        "<div class='muted'>Server: " + escapeHtml(srv) + " · " + escapeHtml(cat) + "</div>" +
        "<div class='muted'>" + escapeHtml(lines) + "</div><div>" + escapeHtml(gt) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(o.status ?? o.Status ?? "") + "'>Print ticket</button>" +
        "<button type='button' class='btn btn-primary btn-sm' data-release='" + id + "'>Release to kitchen</button>" +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-p='" + id + "'>Cancel</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open-p]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-p"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
    el.querySelectorAll("[data-release]").forEach(b => b.onclick = async () => {
      const id = Number(b.getAttribute("data-release"));
      if (!confirm("Release to kitchen? Inventory will be deducted.")) return;
      const r = await api("/api/cashier/orders/pending/" + id + "/release", "POST");
      if (!r.ok) { alert(r.body?.message || "Release failed"); return; }
      await loadOrdersTab();
    });
    el.querySelectorAll("[data-cancel-p]").forEach(b => b.onclick = async () => {
      const id = Number(b.getAttribute("data-cancel-p"));
      if (!confirm("Cancel this ticket? Stock has not been deducted.")) return;
      await api("/api/cashier/orders/pending/" + id + "/cancel", "POST");
      await loadOrdersTab();
    });
  }

  function renderActiveOrders() {
    const el = $("activeOrders");
    const needle = ($("activeSearch") && $("activeSearch").value) || "";
    const rows = filterRows(activeOrderRows, needle);
    if (!rows.length) { el.innerHTML = "<div class='muted' style='padding:16px;'>No active orders.</div>"; return; }
    el.innerHTML = rows.map(o => {
      const id = o.id ?? o.Id;
      const oid = o.orderId ?? o.OrderId ?? "";
      const st = o.status ?? o.Status ?? "";
      const complete = o.showCompleteInOrders ?? o.ShowCompleteInOrders;
      return (
        "<div class='order-card'>" +
        "<button type='button' class='order-card__hit' data-open='" + id + "'>" +
        "<div><strong>" + escapeHtml(oid) + "</strong> <span class='muted'>" + escapeHtml(st) + "</span></div>" +
        "<div class='muted'>" + escapeHtml(o.tableNumber ?? o.TableNumber ?? "") + "</div>" +
        "<div class='muted'>Server: " + escapeHtml(o.serverName ?? o.ServerName ?? "") + "</div>" +
        "<div class='muted'>" + escapeHtml(o.items ?? o.Items ?? "") + "</div>" +
        "<div style=\"font-size:1.05rem;font-weight:700;margin-top:6px;\">" + fmtUsd(o.total ?? o.Total) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(st) + "'>Print ticket</button>" +
        (complete ? "<button type='button' class='btn btn-primary btn-sm' data-complete='" + id + "'>Complete payment</button>" : "") +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-o='" + id + "'>Cancel</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
    el.querySelectorAll("[data-complete]").forEach(b => b.onclick = () => openPaymentModal(Number(b.getAttribute("data-complete"))));
    el.querySelectorAll("[data-cancel-o]").forEach(b => b.onclick = async () => {
      if (!confirm("Cancel this order?")) return;
      await api("/api/cashier/orders/" + Number(b.getAttribute("data-cancel-o")) + "/cancel", "POST");
      await loadOrdersTab();
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
      el.innerHTML = "<div class='muted' style='padding:16px;'>No past orders.</div>";
      return;
    }
    let rows;
    if (!dayKeys.length) {
      sel.innerHTML = "<option value=\"\">All past orders</option>";
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
        ? "No past orders match your search."
        : (!dayKeys.length ? "No past orders to show." : "No past orders for this day.");
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
        "<div class='muted' style='font-size:11px;margin-bottom:4px;'>Tap for details</div>" +
        "<div><strong>" + escapeHtml(oid) + "</strong> <span class='muted'>" + escapeHtml(st) + "</span> · " + escapeHtml(time) + "</div>" +
        "<div class='muted'>" + tbl + " · " + fmtUsd(o.total ?? o.Total) + "</div>" +
        "</button>" +
        "<div class='order-card__actions'>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-print-ticket='" + id + "' data-print-status='" + escapeHtml(st) + "'>Print ticket</button>" +
        "</div></div>");
    }).join("");
    el.querySelectorAll("[data-open-pt]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-pt"))));
    el.querySelectorAll("[data-print-ticket]").forEach(b => b.onclick = () =>
      printOrderTicket(Number(b.getAttribute("data-print-ticket")), b.getAttribute("data-print-status")));
  }

  async function openOrderDetail(orderId) {
    const r = await api("/api/cashier/orders/" + orderId + "/invoice");
    if (!r.ok) { alert(r.body?.message || "Could not load order"); return; }
    const d = r.body;
    detailOrderId = orderId;
    const linesRaw = d.lines ?? d.Lines ?? [];
    const code = d.orderCode ?? d.OrderCode ?? "";
    const st = d.status ?? d.Status ?? "";
    detailOrderStatus = String(st);
    $("detailOrderCode").textContent = code || "—";
    const pill = $("detailStatusPill");
    pill.textContent = st || "—";
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
          "<div class=\"od-items-head\" role=\"row\"><span>Qty</span><span>Item</span><span class=\"od-num\">Unit</span><span class=\"od-num\">Line</span></div>" +
          lineRows +
          "</div>"
        )
      : "<div class=\"muted\" style=\"padding:12px 14px;font-size:13px;\">No line items.</div>";
    const deliveryRow = dFee > 0
      ? "<div class=\"od-total-row\"><span>Delivery fee (20%)</span><span>" + escapeHtml(fmtUsd(dFee)) + "</span></div>"
      : "";
    const grandLine = escapeHtml(fmtUsd(gusd) + " (" + fmtFc(gfc) + ")");
    $("detailBodyScroll").innerHTML =
      "<section class=\"od-section\" aria-label=\"Order details\">" +
      "<h4 class=\"od-section-title\">Details</h4>" +
      "<div class=\"od-meta-grid\">" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">Table</span><span class=\"od-meta-v\">" + escapeHtml(String(tableLabel)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">Server</span><span class=\"od-meta-v\">" + escapeHtml(String(serverName)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">Origin</span><span class=\"od-meta-v\">" + escapeHtml(String(origin)) + "</span></div>" +
      "<div class=\"od-meta-cell\"><span class=\"od-meta-k\">Source</span><span class=\"od-meta-v\">" + escapeHtml(String(src)) + "</span></div>" +
      "<div class=\"od-meta-cell\" style=\"grid-column:1/-1;\"><span class=\"od-meta-k\">Payment timing</span><span class=\"od-meta-v\">" + escapeHtml(String(pt)) + "</span></div>" +
      "</div></section>" +
      "<section class=\"od-section\" aria-label=\"Line items\">" +
      "<h4 class=\"od-section-title\">Line items</h4>" + itemsBlock + "</section>" +
      "<section class=\"od-section\" aria-label=\"Totals\">" +
      "<h4 class=\"od-section-title\">Totals</h4>" +
      "<div class=\"od-totals\">" +
      "<div class=\"od-total-row\"><span>Line subtotal</span><span>" + escapeHtml(fmtUsd(sub)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>Taxable (after discount)</span><span>" + escapeHtml(fmtUsd(taxable)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>Discount</span><span>" + escapeHtml(fmtUsd(disc)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>Tax</span><span>" + escapeHtml(fmtUsd(tax)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>Service</span><span>" + escapeHtml(fmtUsd(svc)) + "</span></div>" +
      "<div class=\"od-total-row\"><span>Merchandise total</span><span>" + escapeHtml(fmtUsd(merch)) + "</span></div>" +
      deliveryRow +
      "<div class=\"od-total-row od-total-row--grand\"><span>Grand total</span><span>" + grandLine + "</span></div>" +
      "</div></section>" +
      "<section class=\"od-section\" aria-label=\"Notes and allergies\">" +
      "<h4 class=\"od-section-title\">Notes &amp; allergies</h4>" +
      "<div class=\"od-notes-grid\">" +
      "<div class=\"od-note\"><div class=\"od-note-title\">Customer notes</div><div class=\"od-note-body\">" + orderDetailNoteInnerHtml(cn) + "</div></div>" +
      "<div class=\"od-note od-note--allergy\"><div class=\"od-note-title\">Allergy</div><div class=\"od-note-body\">" + orderDetailNoteInnerHtml(an) + "</div></div>" +
      "</div></section>";
    $("detailBodyScroll").scrollTop = 0;
    $("orderDetailModal").classList.remove("hidden");
  }

  function closeOrderDetail() {
    $("orderDetailModal").classList.add("hidden");
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
    $("payAllocDueUsd").textContent = "Due " + fmtUsd(chgUsd);
    $("payAllocDueFc").textContent = "Due " + fmtFc(chgFc);

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
      if (!r.ok) { alert(r.body?.message || "Load failed"); return; }
      paymentDueUsd = Number(r.body.grandTotalUsd ?? r.body.GrandTotalUsd ?? 0);
      paymentOrderCode = String(r.body.orderCode ?? r.body.OrderCode ?? orderId);
      updatePaymentFlowUI();
      $("paymentModal").classList.remove("hidden");
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
      if (getPaidUsd() <= 0 && getPaidFc() <= 0) alert("Enter amount paid.");
      else alert("Payment is less than amount due.");
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
    const el = $("hubPill");
    el.classList.remove("ok", "warn", "off");
    if (state === "live") { el.textContent = "Live: connected"; el.classList.add("ok"); }
    else if (state === "degraded") { el.textContent = "Live: reconnecting"; el.classList.add("warn"); }
    else { el.textContent = "Live: polling"; el.classList.add("off"); }
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
    try {
      const Ctx = window.AudioContext || window.webkitAudioContext;
      if (!Ctx) return;
      const ctx = new Ctx();
      if (ctx.state === "suspended") void ctx.resume();
      void ctx.close();
    } catch (_) {}
  }

  function playOrderReadyBeep() {
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
    conn.on("CashierOrderBoardChanged", (payload) => {
      const p = payload && typeof payload === "object" ? payload : {};
      const code = (p.orderCode ?? p.OrderCode ?? "").toString().replace(/^#/, "");
      const reason = (p.reason ?? p.Reason ?? "").toString();
      const codePart = code ? "#" + code + " " : "";
      if (reason === "online-order-submitted" || reason === "server-order-submitted" || reason === "admin-order-submitted") {
        scheduleOrderReadyFlash(codePart + "new ticket awaiting validation");
      } else if (reason === "released-to-kitchen" || reason === "pending-cancelled") {
        scheduleOrderReadyFlash(codePart + (reason === "pending-cancelled" ? "ticket removed" : "released to kitchen"));
      }
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
    });
    conn.on("OrderReady", (payload) => {
      const p = payload && typeof payload === "object" ? payload : {};
      const code = (p.orderCode ?? p.OrderCode ?? "").toString().replace(/^#/, "");
      const table = (p.tableLabel ?? p.TableLabel ?? "").toString();
      const guest = (p.guestLabel ?? p.GuestLabel ?? "").toString();
      const disp = (p.customerFulfillmentDisplay ?? p.CustomerFulfillmentDisplay ?? "").toString();
      const origin = (p.orderOrigin ?? p.OrderOrigin ?? "").toString();
      const loc = table.trim() || guest.trim() || "—";
      const codePart = code ? "#" + code : "";
      const bits = ["Order ready", codePart, loc, disp].filter(x => x && String(x).trim());
      const msg = bits.join(" · ") + (origin ? " (" + origin + ")" : "");
      scheduleOrderReadyFlash(msg);
      playOrderReadyBeep();
      void loadCashierAlerts();
      if (currentView === "orders") loadOrdersTab().catch(() => {});
    });
    conn.onreconnecting(() => setHubPill("degraded"));
    conn.onreconnected(() => {
      setHubPill("live");
      conn.invoke("JoinServer").catch(() => {});
      conn.invoke("JoinCashierDashboard").catch(() => {});
    });
    conn.onclose(() => setHubPill("off"));
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
      $("loginErr").textContent = "Enter sign-in ID and PIN.";
      $("loginErr").classList.remove("hidden");
      return;
    }
    const res = await api("/api/auth/login", "POST", { staffId, pin, portal: "Cashier" }, false);
    if (!res.ok || !res.body?.accessToken) {
      $("loginErr").textContent = "Login failed (" + res.status + "). " + (res.body?.message || res.body?.title || "Check cashier role.");
      $("loginErr").classList.remove("hidden");
      return;
    }
    token = res.body.accessToken;
    me = res.body;
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

  $("btnLogout").onclick = () => {
    stopHubAndPoll();
    token = "";
    me = null;
    revokeImgBlob($("brandLogo"));
    revokeImgBlob($("staffPhoto"));
    $("brandLogo").classList.remove("show");
    $("staffPhoto").classList.remove("show");
    $("app").classList.add("hidden");
    $("loginWrap").classList.remove("hidden");
    setHubPill("off");
  };

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

  $("payConfirm").onclick = async () => {
    if (!canGoToChange()) {
      if (getPaidUsd() <= 0 && getPaidFc() <= 0) { alert("Enter amount paid."); return; }
      alert("Payment is less than amount due.");
      return;
    }
    if (!canConfirmChange()) {
      alert("Change allocation must match change due.");
      return;
    }
    const paidUsd = getPaidUsd();
    const paidFc = getPaidFc();
    const cUsd = getChangeAllocUsd();
    const cFc = getChangeAllocFc();
    const r = await api("/api/cashier/orders/" + paymentTargetOrderId + "/complete", "POST", {
      paymentCurrencyCode: "MIXED",
      paidUsd: paidUsd,
      paidFc: paidFc,
      changeUsd: cUsd,
      changeFc: cFc
    });
    if (!r.ok) { alert(r.body?.message || "Complete failed"); return; }
    closePaymentModal();
    closeOrderDetail();
    await loadOrdersTab();
  };

  $("activeSearch").addEventListener("input", renderActiveOrders);
  $("pastSearch").addEventListener("input", renderPastOrders);
  $("pastDaySelect").addEventListener("change", renderPastOrders);

  if (window.location.protocol === "file:") {
    $("loginErr").textContent = "Open this page from the API site (e.g. http://localhost:8080/cashier/) so login works.";
    $("loginErr").classList.remove("hidden");
  }
})();
