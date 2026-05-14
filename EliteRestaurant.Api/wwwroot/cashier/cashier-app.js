(function () {
  let token = "";
  let me = null;
  let tables = [];
  let products = [];
  let activeOrderRows = [];
  let pastOrderRows = [];
  let pendingRows = [];
  let paymentTargetOrderId = 0;
  let paymentDueUsd = 0;
  let drafts = [];
  let config = { restaurantName: "Elite Restaurant", restaurantLogoUrl: "", employeePhotoUrl: "", currencyDisplayMode: "Dual", usdToFcRate: 2250, taxPercent: 7, servicePercent: 10 };
  const cart = new Map();
  let currentView = "take";
  let menuCatalogRows = [];
  let invByIdForMenu = {};
  let orderHubConnection = null;
  let pollTimer = null;
  let hubDebounce = null;
  let resDetailCache = null;

  function $(id) { return document.getElementById(id); }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));
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
          html += '<button type="button" class="menu-card" data-menu-search="' + escapeHtml(p.name) + '">' + thumb + '<div class="menu-card-text">';
          html += '<div class="title">' + escapeHtml(p.name) + '</div>';
          html += '<div class="price">$ ' + p.price.toFixed(2) + '</div>';
          if (p.description)
            html += '<div class="muted" style="margin-bottom:8px;font-size:12px;">' + escapeHtml(p.description) + '</div>';
          html += '<div class="ing"><strong>Ingredients</strong> · ' + escapeHtml(row.ingText) + '</div>';
          html += '<div class="muted" style="margin-top:8px;font-size:11px;">Tap to jump to Take order with this search</div>';
          html += '</div></button>';
        }
        html += '</div>';
      }
      html += '</div>';
    }
    $("menuBody").innerHTML = html;
    $("menuBody").querySelectorAll(".menu-card").forEach(btn => {
      btn.addEventListener("click", () => {
        const q = btn.getAttribute("data-menu-search") || "";
        setView("take");
        $("prodSearch").value = q;
        renderProducts();
        $("takeOrderCartPanel")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      });
    });
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
        ? ing.map(x => x.name + (x.qty ? " (" + x.qty + (x.unit ? " " + x.unit : "") + ")" : "")).join(", ")
        : (p.composition || "—");
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

  function normalizeDiscount() {
    const mode = $("discountMode").value;
    let value = Number($("discountValue").value || 0);
    if (mode === "Percent") value = Math.max(0, Math.min(100, value));
    if (mode === "Usd") value = Math.max(0, value);
    if (mode === "None") value = 0;
    return { mode, value };
  }

  function computeTotals() {
    const lines = [...cart.entries()].map(([pid, qty]) => {
      const p = products.find(x => x.id === pid);
      return p ? { price: Number(p.price), qty: Number(qty) } : null;
    }).filter(Boolean);
    const subtotal = lines.reduce((s, x) => s + (x.price * x.qty), 0);
    const d = normalizeDiscount();
    let discount = 0;
    if (d.mode === "Percent") discount = subtotal * d.value / 100;
    if (d.mode === "Usd") discount = Math.min(subtotal, d.value);
    const taxable = Math.max(0, subtotal - discount);
    const tax = taxable * (Number(config.taxPercent) / 100);
    const service = taxable * (Number(config.servicePercent) / 100);
    const grand = taxable + tax + service;
    return { subtotal, discount, taxable, tax, service, grand };
  }

  function renderTotals() {
    const t = computeTotals();
    $("itemCount").textContent = String([...cart.values()].reduce((a, b) => a + b, 0));
    $("subtotalUsd").textContent = fmtUsd(t.subtotal);
    $("taxAmount").textContent = fmtUsd(t.tax);
    $("serviceAmount").textContent = fmtUsd(t.service);
    $("discountAmount").textContent = fmtUsd(t.discount);
    const pay = $("paymentCurrency").value;
    $("grandTotal").textContent = pay === "FC" ? fmtFc(toFc(t.grand)) : fmtUsd(t.grand);
  }

  function currentFilteredProducts() {
    const s = ($("prodSearch").value || "").trim().toLowerCase();
    const c = $("category").value;
    const sub = $("subCategory").value;
    return products
      .filter(p => c === "All" || p.category === c)
      .filter(p => sub === "All" || p.subCategory === sub)
      .filter(p => !s || p.name.toLowerCase().includes(s) || p.uniqueId.toLowerCase().includes(s));
  }

  function renderCategories() {
    const category = $("category");
    const sub = $("subCategory");
    const cats = ["All", ...new Set(products.map(p => p.category).filter(Boolean))];
    category.innerHTML = cats.map(c => "<option>" + escapeHtml(c) + "</option>").join("");
    const applySub = () => {
      const selected = category.value;
      const subs = ["All", ...new Set(products.filter(p => selected === "All" || p.category === selected).map(p => p.subCategory).filter(Boolean))];
      sub.innerHTML = subs.map(x => "<option>" + escapeHtml(x) + "</option>").join("");
      renderProducts();
    };
    category.onchange = applySub;
    sub.onchange = renderProducts;
    applySub();
  }

  function renderProducts() {
    const el = $("products");
    const rows = currentFilteredProducts();
    if (!rows.length) { el.innerHTML = "<div class='product-row'>No products found.</div>"; return; }
    el.innerHTML = rows.map(p => {
      const disabled = p.inStock === false;
      const stock = !disabled ? "" : " <span class='muted'>(out)</span>";
      const cls = "product-row" + (disabled ? " product-row--disabled" : "");
      const disAttr = disabled ? " disabled" : "";
      return (
        "<button type='button' class='" + cls + "' data-add='" + p.id + "'" + disAttr + ">" +
        "<div><strong>" + escapeHtml(p.name) + "</strong>" + stock + "<br><span class='muted'>" +
        escapeHtml(p.category) + " / " + escapeHtml(p.subCategory) + " · " + escapeHtml(p.uniqueId) + "</span></div>" +
        "<div class='row-flex' style='align-items:center;'><span class='muted'>" + fmtUsd(p.price) + "</span>" +
        (disabled ? "" : "<span class='muted' style='font-size:11px;text-transform:uppercase;'>Tap row to add</span>") +
        "</div></button>");
    }).join("");
    el.querySelectorAll("[data-add]").forEach(b => b.onclick = () => {
      if (b.disabled) return;
      const id = Number(b.getAttribute("data-add"));
      const p = products.find(x => x.id === id);
      if (p && p.inStock === false) return;
      cart.set(id, (cart.get(id) || 0) + 1);
      renderCart();
    });
  }

  function renderCart() {
    const el = $("cartItems");
    const rows = [...cart.entries()].map(([pid, qty]) => {
      const p = products.find(x => x.id === pid);
      return p ? { p, qty } : null;
    }).filter(Boolean);
    if (!rows.length) {
      el.innerHTML = "<div class='cart-row'>Cart is empty.</div>";
      renderTotals();
      return;
    }
    el.innerHTML = rows.map(r =>
      "<div class='cart-row'>" +
      "<div>" + escapeHtml(r.p.name) + "<br><span class='muted'>" + fmtUsd(r.p.price) + " each</span></div>" +
      "<div class='row-flex'>" +
      "<button type='button' class='btn btn-ghost btn-sm' data-minus='" + r.p.id + "'>−</button><span>" + r.qty + "</span>" +
      "<button type='button' class='btn btn-ghost btn-sm' data-plus='" + r.p.id + "'>+</button>" +
      "<button type='button' class='btn btn-danger btn-sm' data-remove='" + r.p.id + "'>Remove</button></div></div>").join("");
    el.querySelectorAll("[data-minus]").forEach(b => b.onclick = () => {
      const id = Number(b.getAttribute("data-minus"));
      const q = (cart.get(id) || 0) - 1;
      if (q <= 0) cart.delete(id); else cart.set(id, q);
      renderCart();
    });
    el.querySelectorAll("[data-plus]").forEach(b => b.onclick = () => {
      const id = Number(b.getAttribute("data-plus"));
      cart.set(id, (cart.get(id) || 0) + 1);
      renderCart();
    });
    el.querySelectorAll("[data-remove]").forEach(b => b.onclick = () => {
      cart.delete(Number(b.getAttribute("data-remove")));
      renderCart();
    });
    renderTotals();
  }

  function renderTables() {
    const sel = $("tableSelect");
    if (!tables.length) { sel.innerHTML = "<option value='0'>No tables</option>"; return; }
    sel.innerHTML = tables.map(t => {
      const srv = t.assignedServerName ? " · " + t.assignedServerName : "";
      return "<option value='" + t.id + "'>Table " + t.tableNumber + " — " + escapeHtml(t.name) + " (" + escapeHtml(t.status) + ")" + escapeHtml(srv) + "</option>";
    }).join("");
  }

  function normalizeProduct(raw) {
    return {
      id: raw.id ?? raw.Id,
      uniqueId: String(raw.uniqueId ?? raw.UniqueId ?? ""),
      name: String(raw.name ?? raw.Name ?? ""),
      category: String(raw.category ?? raw.Category ?? ""),
      subCategory: String(raw.subCategory ?? raw.SubCategory ?? ""),
      price: Number(raw.price ?? raw.Price ?? 0),
      inStock: raw.inStock ?? raw.InStock ?? true
    };
  }

  function normalizeTable(raw) {
    return {
      id: raw.id ?? raw.Id,
      uniqueId: String(raw.uniqueId ?? raw.UniqueId ?? ""),
      tableNumber: raw.tableNumber ?? raw.TableNumber,
      name: String(raw.name ?? raw.Name ?? ""),
      capacity: raw.capacity ?? raw.Capacity,
      status: String(raw.status ?? raw.Status ?? ""),
      assignedServerId: raw.assignedServerId ?? raw.AssignedServerId,
      assignedServerName: raw.assignedServerName ?? raw.AssignedServerName ?? null
    };
  }

  async function loadPortalData() {
    const cfg = await api("/api/server/config");
    const t = await api("/api/tables/my");
    const p = await api("/api/server/products");
    if (!cfg.ok || !t.ok || !p.ok) return false;
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

    tables = Array.isArray(t.body) ? t.body.map(normalizeTable) : [];
    products = Array.isArray(p.body) ? p.body.map(normalizeProduct) : [];
    renderTables();
    renderCategories();
    renderProducts();
    renderCart();
    await checkOpenCheck();
    return true;
  }

  async function loadDrafts() {
    const tableId = Number($("tableSelect").value || "0");
    const q = tableId > 0 ? "?tableId=" + tableId : "";
    const r = await api("/api/server/drafts" + q);
    drafts = r.ok && Array.isArray(r.body) ? r.body : [];
    renderDrafts();
    if (!r.ok) setDraftStatus("Draft sync failed: " + (r.body?.message || "request failed"), false);
    return r.ok;
  }

  function setDraftStatus(message, ok) {
    const el = $("draftStatus");
    el.textContent = message;
    el.className = "muted" + (ok ? " ok" : "");
    if (!ok) el.style.color = "#dc2626";
    else el.style.color = "";
  }

  function renderDrafts() {
    const sel = $("drafts");
    const options = ["<option value=''>None</option>"];
    drafts.forEach(d => {
      const id = d.id ?? d.Id;
      const label = d.label ?? d.Label ?? "";
      options.push("<option value='" + escapeHtml(id) + "'>" + escapeHtml(label) + "</option>");
    });
    sel.innerHTML = options.join("");
  }

  function getTicketMode() {
    return $("ticketModeNew").checked ? "new" : "append";
  }
  function setTicketMode(v) {
    if (v === "new") { $("ticketModeNew").checked = true; $("ticketModeAppend").checked = false; }
    else { $("ticketModeAppend").checked = true; $("ticketModeNew").checked = false; }
  }

  function updateTicketModeHint(body, tableId) {
    const el = $("ticketModeHint");
    if (!tableId) {
      el.textContent = "Select a table to see open-check status.";
      return;
    }
    if (!body) { el.textContent = ""; return; }
    const has = body.hasOpenCheck ?? body.HasOpenCheck;
    if (!has) {
      el.textContent = "No open check on this table yet. Send creates a pending ticket; both options behave the same until an open check exists.";
    } else {
      const code = body.orderCode ?? body.OrderCode ?? "";
      const st = body.status ?? body.Status ?? "";
      el.textContent = "Open check " + code + " (" + st + "). Same ticket adds lines. New ticket starts a separate check.";
    }
  }

  async function checkOpenCheck() {
    const tableId = Number($("tableSelect").value || "0");
    const label = $("openCheckInfo");
    if (!tableId) {
      label.textContent = "";
      updateTicketModeHint(null, 0);
      return;
    }
    const r = await api("/api/server/open-check?tableId=" + tableId);
    if (!r.ok) {
      label.textContent = "Cannot check open ticket.";
      updateTicketModeHint(null, tableId);
      return;
    }
    const has = r.body.hasOpenCheck ?? r.body.HasOpenCheck;
    if (!has) {
      label.textContent = "No open check on this table.";
      updateTicketModeHint(r.body, tableId);
      return;
    }
    const code = r.body.orderCode ?? r.body.OrderCode ?? "";
    const st = r.body.status ?? r.body.Status ?? "";
    label.textContent = "Open check " + code + " (" + st + ") found.";
    updateTicketModeHint(r.body, tableId);
  }

  function snapshotState() {
    const d = normalizeDiscount();
    return {
      orderSource: $("orderSource").value,
      sourceReference: $("sourceReference").value,
      tableId: Number($("tableSelect").value || "0"),
      paymentCurrencyCode: $("paymentCurrency").value,
      discountMode: d.mode,
      discountValue: d.value,
      appendMode: getTicketMode(),
      customerNotes: $("customerNotes").value,
      allergyNotes: $("allergyNotes").value,
      cart: [...cart.entries()]
    };
  }

  function applySnapshot(s) {
    if (!s) return;
    $("orderSource").value = s.orderSource || "WalkIn";
    $("sourceReference").value = s.sourceReference || "";
    $("tableSelect").value = String(s.tableId || 0);
    $("paymentCurrency").value = s.paymentCurrencyCode || "USD";
    $("discountMode").value = s.discountMode || "None";
    $("discountValue").value = String(s.discountValue || 0);
    setTicketMode(s.appendMode || "append");
    $("customerNotes").value = s.customerNotes || "";
    $("allergyNotes").value = s.allergyNotes || "";
    cart.clear();
    (s.cart || []).forEach(([pid, qty]) => {
      if (pid > 0 && qty > 0) cart.set(Number(pid), Number(qty));
    });
    $("prodSearch").value = "";
    $("category").value = "All";
    renderCategories();
    renderProducts();
    renderCart();
    checkOpenCheck();
  }

  function clearCurrentOrder() {
    cart.clear();
    $("orderSource").value = "WalkIn";
    $("sourceReference").value = "";
    $("sourceReference").disabled = true;
    $("discountMode").value = "None";
    $("discountValue").value = "";
    setTicketMode("new");
    $("customerNotes").value = "";
    $("allergyNotes").value = "";
    $("prodSearch").value = "";
    $("category").value = "All";
    renderCategories();
    renderCart();
    checkOpenCheck();
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
    $("ordersUpdated").textContent = "Updated " + new Date().toLocaleTimeString();
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
        "<button type='button' class='btn btn-primary btn-sm' data-release='" + id + "'>Release to kitchen</button>" +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-p='" + id + "'>Cancel</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open-p]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-p"))));
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
        (complete ? "<button type='button' class='btn btn-primary btn-sm' data-complete='" + id + "'>Complete payment</button>" : "") +
        "<button type='button' class='btn btn-danger btn-sm' data-cancel-o='" + id + "'>Cancel</button></div></div>");
    }).join("");
    el.querySelectorAll("[data-open]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open"))));
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
        "</button></div>");
    }).join("");
    el.querySelectorAll("[data-open-pt]").forEach(b => b.onclick = () => openOrderDetail(Number(b.getAttribute("data-open-pt"))));
  }

  async function openOrderDetail(orderId) {
    const r = await api("/api/cashier/orders/" + orderId + "/invoice");
    if (!r.ok) { alert(r.body?.message || "Could not load order"); return; }
    const d = r.body;
    const linesRaw = d.lines ?? d.Lines ?? [];
    const lines = linesRaw.map(l => {
      const q = l.quantity ?? l.Quantity;
      const name = l.name ?? l.Name;
      const up = l.unitPrice ?? l.UnitPrice;
      const lt = l.lineTotal ?? l.LineTotal;
      return q + "× " + name + "  @" + fmtUsd(up) + "  = " + fmtUsd(lt);
    }).join("\n");
    const code = d.orderCode ?? d.OrderCode ?? "";
    const st = d.status ?? d.Status ?? "";
    $("detailTitle").textContent = code + " · " + st;
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
    $("detailBody").textContent =
      "Table: " + (d.tableLabel ?? d.TableLabel) + "\nServer: " + (d.serverName ?? d.ServerName) + "\n" +
      "Origin: " + origin + " · Source: " + src + "\nPayment timing: " + pt + "\n\n" +
      lines + "\n\n" +
      "Line subtotal " + fmtUsd(sub) + "\n" +
      "Taxable (after discount) " + fmtUsd(taxable) + "\n" +
      "Discount " + fmtUsd(disc) + "\nTax " + fmtUsd(tax) + "\nService " + fmtUsd(svc) + "\n" +
      "Merchandise total " + fmtUsd(merch) + "\n" +
      (dFee > 0 ? "Delivery fee (20%) " + fmtUsd(dFee) + "\n" : "") +
      "Grand " + fmtUsd(gusd) + " (" + fmtFc(gfc) + ")\n\n" +
      "Notes: " + cn + "\nAllergy: " + an;
    $("orderDetailModal").classList.remove("hidden");
  }

  function closeOrderDetail() {
    $("orderDetailModal").classList.add("hidden");
  }

  function openPaymentModal(orderId) {
    paymentTargetOrderId = orderId;
    $("payUsd").value = "0";
    $("payFc").value = "0";
    $("chgUsd").value = "0";
    $("chgFc").value = "0";
    api("/api/cashier/orders/" + orderId + "/invoice").then(r => {
      if (!r.ok) { alert(r.body?.message || "Load failed"); return; }
      paymentDueUsd = Number(r.body.grandTotalUsd ?? r.body.GrandTotalUsd ?? 0);
      $("paymentDueLine").textContent = "Due: " + fmtUsd(paymentDueUsd) + " (≈ " + fmtFc(toFc(paymentDueUsd)) + ")";
      updatePaymentSummary();
      $("paymentModal").classList.remove("hidden");
    });
  }

  function closePaymentModal() {
    $("paymentModal").classList.add("hidden");
    paymentTargetOrderId = 0;
  }

  function updatePaymentSummary() {
    const paidUsd = Number($("payUsd").value || 0);
    const paidFc = Number($("payFc").value || 0);
    const totalEq = Math.round((paidUsd + fcToUsd(paidFc)) * 100) / 100;
    const rem = Math.max(0, Math.round((paymentDueUsd - totalEq) * 100) / 100);
    const chg = Math.max(0, Math.round((totalEq - paymentDueUsd) * 100) / 100);
    $("paymentSummary").textContent =
      "Paid (USD eq): " + fmtUsd(totalEq) + " · Remaining: " + fmtUsd(rem) + " · Change due: " + fmtUsd(chg);
    const cUsd = Number($("chgUsd").value || 0);
    const cFc = Number($("chgFc").value || 0);
    const alloc = Math.round((cUsd + fcToUsd(cFc)) * 100) / 100;
    $("changeSummary").textContent =
      "Change allocation (USD eq): " + fmtUsd(alloc) + " · must match " + fmtUsd(chg);
  }

  function engagementStatusLabel(st) {
    const s = String(st || "");
    if (/^checkedin$/i.test(s)) return "Checked in";
    return s || "—";
  }

  function engagementPillClass(stRaw) {
    const s = String(stRaw || "").toLowerCase();
    if (s === "scheduled") return "res-pill--scheduled";
    if (s === "checkedin") return "res-pill--in";
    return "res-pill--muted";
  }

  function resEngagementRailClass(stRaw) {
    const s = String(stRaw || "").toLowerCase();
    return s === "checkedin" ? "res-eng-card__rail res-eng-card__rail--in" : "res-eng-card__rail";
  }

  function resKvHtml(label, val) {
    return (
      "<div class='res-kv'><span class='res-kv-lbl'>" + escapeHtml(label) + "</span>" +
      "<span class='res-kv-val'>" + escapeHtml(val) + "</span></div>");
  }

  function resSectionHtml(title, pairs) {
    const inner = pairs.map(p => resKvHtml(p[0], p[1])).join("");
    return (
      "<div class='res-detail-section'><p class='res-detail-section-title'>" + escapeHtml(title) + "</p>" +
      "<div class='res-kv-grid'>" + inner + "</div></div>");
  }

  function buildReservationDetailBody(d) {
    const startRaw = d.plannedStartUtc ?? d.PlannedStartUtc;
    const endRaw = d.plannedEndUtc ?? d.PlannedEndUtc;
    const actS = d.actualStartUtc ?? d.ActualStartUtc;
    const actE = d.actualEndUtc ?? d.ActualEndUtc;
    const stRaw = String(d.status ?? d.Status ?? "");
    const visit = resSectionHtml("Visit", [
      ["Arrival", startRaw ? new Date(startRaw).toLocaleString() : "—"],
      ["End", endRaw ? new Date(endRaw).toLocaleString() : "—"],
      ["Party size", String(d.partySize ?? d.PartySize ?? "—")],
      ["Table", String(d.tableLabel ?? d.TableLabel ?? "—")],
    ]);
    const guest = resSectionHtml("Guest", [
      ["Phone", String(d.guestPhone ?? d.GuestPhone ?? "—")],
      ["Email", String(d.guestEmail ?? d.GuestEmail ?? "—")],
      ["Notes", String(d.userNotes ?? d.UserNotes ?? "—")],
    ]);
    const floor = resSectionHtml("Floor & record", [
      ["Status", engagementStatusLabel(stRaw)],
      ["Table id", String(d.tableId ?? d.TableId ?? "—")],
      ["Placement unit", String(d.placementUnitId ?? d.PlacementUnitId ?? "—")],
      ["Actual arrival", actS ? new Date(actS).toLocaleString() : "—"],
      ["Actual release", actE ? new Date(actE).toLocaleString() : "—"],
      ["Created", (d.createdAtUtc ?? d.CreatedAtUtc) ? new Date(d.createdAtUtc ?? d.CreatedAtUtc).toLocaleString() : "—"],
      ["Updated", (d.updatedAtUtc ?? d.UpdatedAtUtc) ? new Date(d.updatedAtUtc ?? d.UpdatedAtUtc).toLocaleString() : "—"],
    ]);
    return visit + guest + floor;
  }

  function closeResDetail() {
    $("resDetailModal").classList.add("hidden");
    $("reschedulePanel").classList.add("hidden");
    resDetailCache = null;
  }

  function toLocalDatetimeLocalValue(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    const pad = n => String(n).padStart(2, "0");
    return (
      d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) +
      "T" + pad(d.getHours()) + ":" + pad(d.getMinutes()));
  }

  async function openReservationDetail(id) {
    const r = await api("/api/cashier/reservations/engagements/" + id, "GET");
    if (!r.ok) { alert(r.body?.message || "Could not load reservation"); return; }
    const d = r.body;
    resDetailCache = d;
    const stRaw = String(d.status ?? d.Status ?? "");
    const code = "#" + id;
    $("resDetailTitle").textContent = d.guestName || d.GuestName || "Guest";
    $("resDetailMeta").innerHTML =
      "<span class='res-ref'>" + escapeHtml(code) + "</span>" +
      "<span class='res-pill " + engagementPillClass(stRaw) + "'>" + escapeHtml(engagementStatusLabel(stRaw)) + "</span>";
    $("resDetailBody").innerHTML = buildReservationDetailBody(d);
    const st = stRaw.toLowerCase();
    const actions = $("resDetailActions");
    actions.innerHTML = "";
    if (st === "scheduled") {
      actions.innerHTML =
        "<button type='button' class='btn btn-primary btn-sm' data-res-arrived='" + id + "'>Arrived</button>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-res-sched='" + id + "'>Reschedule</button>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-res-noshow='" + id + "'>No show</button>" +
        "<button type='button' class='btn btn-danger btn-sm' data-res-cancel='" + id + "'>Cancelled</button>";
      actions.querySelector("[data-res-arrived]").onclick = () => resAction(id, "arrived");
      actions.querySelector("[data-res-sched]").onclick = () => openReschedulePanel(id);
      actions.querySelector("[data-res-noshow]").onclick = () => resAction(id, "no-show");
      actions.querySelector("[data-res-cancel]").onclick = () => resAction(id, "cancel");
    } else if (st === "checkedin") {
      actions.innerHTML =
        "<p class='muted' style='margin:0;line-height:1.5;'>Checked in — use <strong>Reservation floor</strong> for seating timeline and release.</p>";
    } else {
      actions.innerHTML = "<p class='muted' style='margin:0;'>No actions for this status.</p>";
    }
    $("reschedulePanel").classList.add("hidden");
    $("resDetailModal").classList.remove("hidden");
  }

  async function resAction(id, kind) {
    const path = kind === "arrived" ? "arrived" : kind === "cancel" ? "cancel" : "no-show";
    const msg =
      kind === "cancel"
        ? "Cancel this reservation?"
        : kind === "no-show"
          ? "Mark as no-show?"
          : "Mark guest as arrived (check-in)?";
    if (!confirm(msg)) return;
    const r = await api("/api/cashier/reservations/engagements/" + id + "/" + path, "POST", {});
    if (!r.ok) { alert(r.body?.message || "Request failed"); return; }
    closeResDetail();
    await loadReservations();
  }

  function openReschedulePanel(id) {
    if (!resDetailCache) return;
    const start = resDetailCache.plannedStartUtc ?? resDetailCache.PlannedStartUtc;
    $("resSchedLocal").value = toLocalDatetimeLocalValue(start);
    $("reschedulePanel").classList.remove("hidden");
    $("reschedulePanel").dataset.engagementId = String(id);
  }

  async function loadReservations() {
    const el = $("reservationsList");
    const r = await api("/api/cashier/reservations/engagements", "GET");
    if (!r.ok) { el.innerHTML = "<div class='danger' style='padding:12px;'>Could not load reservations.</div>"; return; }
    const rows = Array.isArray(r.body) ? r.body : [];
    if (!rows.length) {
      el.innerHTML =
        "<div class='res-empty'>" +
        "<p class='res-empty-title'>No upcoming reservations</p>" +
        "<p class='res-empty-hint'>Scheduled and checked-in visits appear here. New guest bookings from the menu show up automatically.</p>" +
        "</div>";
      return;
    }
    el.innerHTML = rows.map(x => {
      const id = x.id ?? x.Id;
      const stRaw = x.status ?? x.Status ?? "";
      const st = engagementStatusLabel(stRaw);
      const pill = engagementPillClass(stRaw);
      const rail = resEngagementRailClass(stRaw);
      const name = x.guestName ?? x.GuestName ?? "Guest";
      const phone = x.guestPhone ?? x.GuestPhone ?? "";
      const tbl = x.tableLabel ?? x.TableLabel ?? "-";
      const ps = x.partySize ?? x.PartySize ?? "";
      const start = x.plannedStartUtc ?? x.PlannedStartUtc;
      const when = start ? new Date(start).toLocaleString() : "";
      const sid = String(id);
      return (
        "<div class='res-eng-card'>" +
        "<button type='button' class='res-eng-card__hit' data-res-open='" + sid + "'>" +
        "<div class='" + rail + "' aria-hidden='true'></div>" +
        "<div class='res-eng-card__body'>" +
        "<div class='res-eng-card__top'>" +
        "<span class='res-eng-card__name'>" + escapeHtml(name) + "</span>" +
        "<span class='res-pill " + pill + "'>" + escapeHtml(st) + "</span>" +
        "</div>" +
        "<div class='res-eng-card__ref'>Ref · " + escapeHtml(sid) + "</div>" +
        (when ? "<div class='res-eng-card__when'>" + escapeHtml(when) + "</div>" : "") +
        "<div class='res-eng-card__chips'>" +
        "<span class='res-chip'><strong>Table</strong> " + escapeHtml(tbl) + "</span>" +
        "<span class='res-chip'><strong>Party</strong> " + escapeHtml(String(ps)) + "</span>" +
        (phone ? "<span class='res-chip'><strong>Tel</strong> " + escapeHtml(phone) + "</span>" : "") +
        "</div></div></button></div>");
    }).join("");
    el.querySelectorAll("[data-res-open]").forEach(b => {
      b.onclick = () => openReservationDetail(Number(b.getAttribute("data-res-open")));
    });
  }

  function guestMenuUrlForTableId(tableId) {
    return location.origin + "/?table=" + encodeURIComponent(String(tableId));
  }

  function renderTableMenuList() {
    const el = $("tableMenuList");
    if (!el) return;
    const rows = tables.slice().sort((a, b) => (Number(a.tableNumber) || 0) - (Number(b.tableNumber) || 0));
    if (!rows.length) {
      el.innerHTML = "<div class='muted' style='padding:16px;'>No tables loaded. Use Refresh tables or Refresh all.</div>";
      return;
    }
    el.innerHTML = rows.map(t => {
      const id = t.id;
      const num = escapeHtml(String(t.tableNumber ?? ""));
      const name = escapeHtml(t.name || "");
      const st = escapeHtml(t.status || "");
      const srv = t.assignedServerName ? escapeHtml(t.assignedServerName) : "";
      const sid = String(id);
      return (
        "<div class='table-menu-card'>" +
        "<button type='button' class='table-menu-card__hit' data-use-tm='" + sid + "' title='Select this table in Take order'>" +
        "<div><strong>Table " + num + "</strong> — " + name +
        "<br><span class='muted'>" + st + (srv ? " · Server: " + srv : "") + "</span></div>" +
        "<div class='muted' style='font-size:11px;margin-top:6px;'>Tap row to use table · buttons for guest link</div>" +
        "</button>" +
        "<div class='table-menu-card__actions'>" +
        "<button type='button' class='btn btn-primary btn-sm' data-open-guest='" + sid + "'>Open guest menu</button>" +
        "<button type='button' class='btn btn-ghost btn-sm' data-copy-tm='" + sid + "'>Copy link</button>" +
        "</div></div>");
    }).join("");
    el.querySelectorAll(".table-menu-card__hit").forEach(b => {
      b.onclick = () => {
        const id = Number(b.getAttribute("data-use-tm"));
        $("tableSelect").value = String(id);
        void checkOpenCheck();
        void loadDrafts();
        setView("take");
        $("takeOrderCartPanel")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      };
    });
    el.querySelectorAll("[data-open-guest]").forEach(b => {
      b.onclick = () => {
        const id = b.getAttribute("data-open-guest");
        window.open(guestMenuUrlForTableId(id), "_blank", "noopener,noreferrer");
      };
    });
    el.querySelectorAll("[data-copy-tm]").forEach(b => {
      b.onclick = async () => {
        const id = b.getAttribute("data-copy-tm");
        const url = guestMenuUrlForTableId(id);
        try {
          await navigator.clipboard.writeText(url);
          const prev = b.textContent;
          b.textContent = "Copied!";
          setTimeout(() => { b.textContent = prev; }, 2000);
        } catch {
          window.prompt("Copy this URL:", url);
        }
      };
    });
  }

  async function refreshTableMenuView() {
    if (!token) return;
    await loadPortalData();
    renderTableMenuList();
  }

  function setView(v) {
    currentView = v;
    $("viewTake").classList.toggle("hidden", v !== "take");
    $("viewOrders").classList.toggle("hidden", v !== "orders");
    $("viewTableMenu").classList.toggle("hidden", v !== "tableMenu");
    $("viewMenu").classList.toggle("hidden", v !== "menu");
    $("viewReservations").classList.toggle("hidden", v !== "reservations");
    $("navTake").classList.toggle("active", v === "take");
    $("navOrders").classList.toggle("active", v === "orders");
    $("navTableMenu").classList.toggle("active", v === "tableMenu");
    $("navMenu").classList.toggle("active", v === "menu");
    $("navRes").classList.toggle("active", v === "reservations");
    if (v === "orders") loadOrdersTab().catch(() => {});
    if (v === "tableMenu") renderTableMenuList();
    if (v === "menu") loadMenu().catch(e => alert(e.message || String(e)));
    if (v === "reservations") loadReservations();
  }

  function setHubPill(state) {
    const el = $("hubPill");
    el.classList.remove("ok", "warn", "off");
    if (state === "live") { el.textContent = "Live: connected"; el.classList.add("ok"); }
    else if (state === "degraded") { el.textContent = "Live: reconnecting"; el.classList.add("warn"); }
    else { el.textContent = "Live: polling"; el.classList.add("off"); }
  }

  function scheduleDraftFlash(msg) {
    const el = $("draftFlash");
    el.textContent = msg || "";
    if (hubDebounce) clearTimeout(hubDebounce);
    hubDebounce = setTimeout(() => { el.textContent = ""; hubDebounce = null; }, 6000);
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
    conn.on("CustomerDraftArrived", () => {
      scheduleDraftFlash("New customer draft — drafts refreshed.");
      if (currentView === "take") loadDrafts();
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
    await loadDrafts();
    await loadOrdersTab();
    setView("take");
    void startOrderHub();
    unlockCashierAudioFromUserGesture();
    startPolling();
  };

  $("btnLogout").onclick = () => {
    stopHubAndPoll();
    token = "";
    me = null;
    tables = [];
    products = [];
    drafts = [];
    cart.clear();
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
    await loadDrafts();
    await loadOrdersTab();
    if (currentView === "tableMenu") renderTableMenuList();
  };

  $("navTake").onclick = () => setView("take");
  $("navOrders").onclick = () => setView("orders");
  $("navTableMenu").onclick = () => setView("tableMenu");
  $("navMenu").onclick = () => setView("menu");
  $("navRes").onclick = () => setView("reservations");
  $("btnRefreshTableMenu").onclick = () => { void refreshTableMenuView(); };
  $("btnRefreshMenu").onclick = () => loadMenu().catch(e => alert(e.message || String(e)));
  $("btnRefreshRes").onclick = () => loadReservations();
  $("menuSearch").oninput = () => renderMenuFromCatalog();

  $("prodSearch").addEventListener("input", renderProducts);
  $("discountMode").addEventListener("change", renderTotals);
  $("discountValue").addEventListener("input", renderTotals);
  $("paymentCurrency").addEventListener("change", renderTotals);
  $("tableSelect").addEventListener("change", () => {
    void checkOpenCheck();
    void loadDrafts();
  });
  $("sourceReference").disabled = true;
  $("orderSource").addEventListener("change", () => {
    const src = $("orderSource").value;
    const ref = $("sourceReference");
    ref.disabled = src !== "Delivery";
    if (src !== "Delivery") ref.value = "";
  });

  $("btnCheckOpen").onclick = checkOpenCheck;
  $("detailClose").onclick = closeOrderDetail;
  $("orderDetailModal").addEventListener("click", e => { if (e.target.id === "orderDetailModal") closeOrderDetail(); });

  $("resDetailClose").onclick = closeResDetail;
  $("resDetailModal").addEventListener("click", e => { if (e.target.id === "resDetailModal") closeResDetail(); });
  $("resSchedCancel").onclick = () => { $("reschedulePanel").classList.add("hidden"); };
  $("resSchedApply").onclick = async () => {
    const id = Number($("reschedulePanel").dataset.engagementId);
    const local = $("resSchedLocal").value;
    if (!local || !id) { alert("Pick a new time."); return; }
    const startIso = new Date(local).toISOString();
    const r = await api("/api/cashier/reservations/engagements/" + id + "/reschedule", "POST", { plannedStartUtc: startIso });
    if (!r.ok) { alert(r.body?.message || "Reschedule failed"); return; }
    closeResDetail();
    await loadReservations();
  };

  ["payUsd", "payFc", "chgUsd", "chgFc"].forEach(id => $(id).addEventListener("input", updatePaymentSummary));
  $("payCancel").onclick = closePaymentModal;
  $("paymentModal").addEventListener("click", e => { if (e.target.id === "paymentModal") closePaymentModal(); });

  $("payConfirm").onclick = async () => {
    const paidUsd = Number($("payUsd").value || 0);
    const paidFc = Number($("payFc").value || 0);
    const totalEq = Math.round((paidUsd + fcToUsd(paidFc)) * 100) / 100;
    const rem = Math.max(0, Math.round((paymentDueUsd - totalEq) * 100) / 100);
    if (rem > 0.001) { alert("Payment is less than amount due."); return; }
    if (paidUsd <= 0 && paidFc <= 0) { alert("Enter amount paid."); return; }
    const chg = Math.max(0, Math.round((totalEq - paymentDueUsd) * 100) / 100);
    const cUsd = Number($("chgUsd").value || 0);
    const cFc = Number($("chgFc").value || 0);
    const alloc = Math.round((cUsd + fcToUsd(cFc)) * 100) / 100;
    if (Math.abs(alloc - chg) > 0.02) { alert("Change allocation must match change due."); return; }
    const r = await api("/api/cashier/orders/" + paymentTargetOrderId + "/complete", "POST", {
      paymentCurrencyCode: "MIXED",
      paidUsd: paidUsd, paidFc: paidFc, changeUsd: cUsd, changeFc: cFc
    });
    if (!r.ok) { alert(r.body?.message || "Complete failed"); return; }
    closePaymentModal();
    closeOrderDetail();
    await loadOrdersTab();
  };

  $("activeSearch").addEventListener("input", renderActiveOrders);
  $("pastSearch").addEventListener("input", renderPastOrders);
  $("pastDaySelect").addEventListener("change", renderPastOrders);

  $("btnSaveDraft").onclick = async () => {
    const snapshot = snapshotState();
    const itemCount = [...cart.values()].reduce((a, b) => a + b, 0);
    const tableLabel = tables.find(tt => tt.id === snapshot.tableId)?.tableNumber || "-";
    const label = new Date().toLocaleString() + " | T" + tableLabel + " | " + itemCount + " items";
    const res = await api("/api/server/drafts", "POST", { label: label, snapshotJson: JSON.stringify(snapshot) });
    if (!res.ok) {
      setDraftStatus("Save failed: " + (res.body?.message || "request failed"), false);
      return;
    }
    await loadDrafts();
    const newId = res.body.id ?? res.body.Id;
    if (newId) $("drafts").value = newId;
    setDraftStatus("Draft saved.", true);
  };

  $("btnLoadDraft").onclick = async () => {
    const id = $("drafts").value;
    if (!id) { setDraftStatus("Select a draft first.", false); return; }
    const draft = drafts.find(d => String(d.id ?? d.Id) === String(id));
    if (!draft) { setDraftStatus("Draft not found. Refresh.", false); return; }
    const json = draft.snapshotJson ?? draft.SnapshotJson ?? "{}";
    let snapshot;
    try { snapshot = JSON.parse(json); } catch { snapshot = null; }
    if (!snapshot) { setDraftStatus("Invalid draft payload.", false); return; }
    applySnapshot(snapshot);
    setDraftStatus("Draft loaded.", true);
  };

  $("btnDeleteDraft").onclick = async () => {
    const id = $("drafts").value;
    if (!id) { setDraftStatus("Select a draft first.", false); return; }
    const tid = Number($("tableSelect").value || "0");
    const qd = tid > 0 ? "?tableId=" + tid : "";
    const res = await api("/api/server/drafts/" + id + qd, "DELETE");
    if (!res.ok) {
      setDraftStatus("Delete failed: " + (res.body?.message || ""), false);
      return;
    }
    await loadDrafts();
    $("drafts").value = "";
    setDraftStatus("Draft deleted.", true);
  };

  $("btnClearOrder").onclick = () => clearCurrentOrder();

  $("btnSubmit").onclick = async () => {
    const tableId = Number($("tableSelect").value || "0");
    const lines = [...cart.entries()].map(([productId, quantity]) => ({ productId: productId, quantity: quantity }));
    if (!tableId) { alert("Select a table first."); return; }
    if (!lines.length) { alert("Cart is empty."); return; }
    const source = $("orderSource").value;
    const sourceRef = $("sourceReference").value || "";
    if (source === "Delivery" && !sourceRef.trim()) { alert("Delivery requires a reference."); return; }
    const d = normalizeDiscount();
    const payload = {
      tableId: tableId,
      orderSource: source,
      sourceReference: sourceRef,
      discountMode: d.mode,
      discountValue: d.value,
      paymentCurrencyCode: $("paymentCurrency").value,
      appendToOpenCheck: getTicketMode() === "append",
      customerNotes: $("customerNotes").value || "",
      allergyNotes: $("allergyNotes").value || "",
      lines: lines
    };
    const res = await api("/api/server/orders", "POST", payload);
    if (!res.ok) {
      alert(res.body?.message || res.body?.title || ("Could not place order (" + res.status + ")."));
      return;
    }
    clearCurrentOrder();
    await loadPortalData();
    await loadOrdersTab();
  };

  document.querySelectorAll("[data-kpi]").forEach(tile => {
    tile.addEventListener("click", () => {
      const k = tile.getAttribute("data-kpi");
      if (k === "submit") {
        $("btnSubmit")?.focus();
        $("takeOrderCartPanel")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      } else {
        $("cartItems")?.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }
    });
  });

  if (window.location.protocol === "file:") {
    $("loginErr").textContent = "Open this page from the API site (e.g. http://localhost:8080/cashier/) so login works.";
    $("loginErr").classList.remove("hidden");
  }
})();
