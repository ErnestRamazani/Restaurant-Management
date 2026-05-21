(function () {
  let token = "";
  let me = null;
  let tables = [];
  let config = {
    restaurantName: "Elite Restaurant",
    restaurantLogoUrl: "",
    employeePhotoUrl: ""
  };
  let currentView = "tableMenu";
  let menuCatalogRows = [];
  let invByIdForMenu = {};
  let resDetailCache = null;
  let tableDetailCache = null;
  let resScheduling = null;
  let deliveryPickupRows = [];
  let tableMenuLoadError = "";
  let orderHubConnection = null;
  let pollTimer = null;
  let deliveryReadyFlashTimer = null;

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
      const inv = invByIdForMenu[iid];
      const nm = inv ? inv.name : ("#" + iid);
      if (!byProd[pid]) byProd[pid] = [];
      byProd[pid].push({ name: nm });
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

  async function loadBranding() {
    const cfg = await api("/api/server/config");
    if (!cfg.ok) {
      console.warn("[Reception] server/config", cfg.status, cfg.body);
      return false;
    }
    const b = cfg.body;
    config = {
      restaurantName: b.restaurantName ?? b.RestaurantName ?? "Elite Restaurant",
      restaurantLogoUrl: b.restaurantLogoUrl ?? b.RestaurantLogoUrl ?? "",
      employeePhotoUrl: b.employeePhotoUrl ?? b.EmployeePhotoUrl ?? ""
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

  async function loadTables() {
    tableMenuLoadError = "";
    const t = await api("/api/reception/tables", "GET");
    if (!t.ok) {
      const detail = (t.body && (t.body.message || t.body.title || t.body.detail)) || "";
      tableMenuLoadError =
        "Could not load tables (" + String(t.status) + "). " + String(detail).trim();
      tables = [];
      console.warn("[Reception] reception/tables", t.status, t.body);
      populateResCreateTableSelect();
      return false;
    }
    tables = Array.isArray(t.body) ? t.body.map(normalizeTable) : [];
    if (!tables.length)
      tableMenuLoadError = "";
    populateResCreateTableSelect();
    return true;
  }

  async function loadPortalData() {
    const [, tablesOk] = await Promise.all([loadBranding(), loadTables()]);
    return tablesOk;
  }

  function normalizeResScheduling(raw) {
    if (!raw || typeof raw !== "object") return null;
    const lead = Number(raw.reservationLeadDays ?? raw.ReservationLeadDays ?? 0);
    const months = Number(raw.reservationMaxMonthsAhead ?? raw.ReservationMaxMonthsAhead ?? 6);
    const step = Number(raw.suggestionSlotStepMinutes ?? raw.SuggestionSlotStepMinutes ?? 30);
    const horizon = Number(raw.suggestionHorizonDays ?? raw.SuggestionHorizonDays ?? 14);
    return {
      reservationLeadDays: Number.isFinite(lead) ? Math.max(0, lead) : 0,
      reservationMaxMonthsAhead: Number.isFinite(months) ? Math.max(1, months) : 6,
      suggestionSlotStepMinutes: Number.isFinite(step) ? Math.max(5, step) : 30,
      suggestionHorizonDays: Number.isFinite(horizon) ? Math.max(1, horizon) : 14,
      defaultDurationMinutes: Number(raw.defaultDurationMinutes ?? raw.DefaultDurationMinutes ?? 105) || 105
    };
  }

  async function loadResScheduling() {
    const r = await api("/api/cashier/reservations/scheduling", "GET");
    if (!r.ok) return false;
    resScheduling = normalizeResScheduling(r.body);
    applyResCreateWhenConstraints();
    updateResCreateScheduleHint();
    return !!resScheduling;
  }

  function localDateTimeAtEndOfDayMonthsAhead(months) {
    const d = new Date();
    d.setMonth(d.getMonth() + months);
    d.setHours(23, 59, 0, 0);
    return d;
  }

  function roundLocalToSlotStep(d, stepMinutes) {
    const stepMs = Math.max(5, stepMinutes) * 60 * 1000;
    const t = d.getTime();
    return new Date(Math.ceil(t / stepMs) * stepMs);
  }

  function applyResCreateWhenConstraints() {
    const inp = $("resCreateWhen");
    if (!inp) return;
    const stepMin = resScheduling?.suggestionSlotStepMinutes ?? 30;
    inp.step = String(Math.max(60, stepMin * 60));
    const now = new Date();
    const minDt = roundLocalToSlotStep(new Date(now.getTime() + 60 * 1000), stepMin);
    const maxMonths = resScheduling?.reservationMaxMonthsAhead ?? 6;
    const maxDt = localDateTimeAtEndOfDayMonthsAhead(maxMonths);
    inp.min = toLocalDatetimeLocalValue(minDt.toISOString());
    inp.max = toLocalDatetimeLocalValue(maxDt.toISOString());
    if (!inp.value || inp.value < inp.min) inp.value = inp.min;
    if (inp.value > inp.max) inp.value = inp.max;
  }

  function defaultResCreateWhenValue() {
    const inp = $("resCreateWhen");
    if (!inp) return "";
    applyResCreateWhenConstraints();
    return inp.min || toLocalDatetimeLocalValue(new Date().toISOString());
  }

  function updateResCreateScheduleHint() {
    const el = $("resCreateScheduleHint");
    if (!el) return;
    if (!resScheduling) {
      el.textContent = "Loading booking window from restaurant settings…";
      return;
    }
    const months = resScheduling.reservationMaxMonthsAhead;
    const step = resScheduling.suggestionSlotStepMinutes;
    el.textContent =
      "Walk-in bookings from the front desk can be scheduled from the next available slot up to " +
      months + " month(s) ahead. Time slots align every " + step + " minutes (same as online reservations).";
  }

  function validateResCreateWhenLocal(localValue) {
    if (!localValue) return "Date and time are required.";
    const d = new Date(localValue);
    if (Number.isNaN(d.getTime())) return "Invalid date and time.";
    const now = new Date();
    if (d <= now) return "Reservation time must be in the future.";
    if (!resScheduling) return null;
    const max = localDateTimeAtEndOfDayMonthsAhead(resScheduling.reservationMaxMonthsAhead);
    if (d > max) {
      return "Reservations may be booked up to " + resScheduling.reservationMaxMonthsAhead + " month(s) ahead.";
    }
    const step = resScheduling.suggestionSlotStepMinutes;
    const rounded = roundLocalToSlotStep(d, step);
    if (Math.abs(rounded.getTime() - d.getTime()) > 60 * 1000) {
      return "Choose a time on " + step + "-minute boundaries (e.g. :00 or :30).";
    }
    return null;
  }

  function openResCreateModal() {
    $("resCreateMsg").textContent = "";
    $("resCreateMsg").classList.remove("ok");
    $("resCreateName").value = "";
    $("resCreatePhone").value = "";
    $("resCreateEmail").value = "";
    $("resCreateNotes").value = "";
    $("resCreateParty").value = "2";
    populateResCreateTableSelect();
    $("resCreateWhen").value = defaultResCreateWhenValue();
    void loadResScheduling();
    $("resCreateModal").classList.remove("hidden");
    $("resCreateName").focus();
  }

  function closeResCreateModal() {
    $("resCreateModal").classList.add("hidden");
    $("resCreateMsg").textContent = "";
    $("resCreateMsg").classList.remove("ok");
  }

  function populateResCreateTableSelect() {
    const sel = $("resCreateTable");
    if (!sel) return;
    const prev = sel.value;
    const rows = tables.slice().sort((a, b) => (Number(a.tableNumber) || 0) - (Number(b.tableNumber) || 0));
    sel.innerHTML = '<option value="">No table assigned</option>' +
      rows.map(t => {
        const id = t.id;
        const label = "Table " + (t.tableNumber ?? "") + (t.name ? " · " + t.name : "");
        return '<option value="' + escapeHtml(String(id)) + '">' + escapeHtml(label) + "</option>";
      }).join("");
    if (prev) sel.value = prev;
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

  function tableAvailFromStatus(status) {
    const s = String(status || "").trim().toLowerCase();
    if (s === "occupied") return { label: "Occupied", cls: "tbl-avail-pill--occupied", rail: "" };
    if (s === "available") return { label: "Available", cls: "tbl-avail-pill--available", rail: " tbl-card__rail--available" };
    return { label: status || "—", cls: "tbl-avail-pill--other", rail: "" };
  }

  function formatUsd(n) {
    const v = Number(n);
    return Number.isFinite(v) ? "$ " + v.toFixed(2) : "—";
  }

  function buildTableDetailBody(t, checks, checksErr) {
    const avail = tableAvailFromStatus(t.status);
    const info = resSectionHtml("Table", [
      ["Table number", String(t.tableNumber ?? "—")],
      ["Name", t.name || "—"],
      ["Capacity", t.capacity != null ? String(t.capacity) : "—"],
      ["Floor status", t.status || "—"],
      ["Availability", avail.label],
      ["Table id", String(t.id ?? "—")],
      ["Unique id", t.uniqueId || "—"],
    ]);
    const server = resSectionHtml("Service", [
      ["Assigned server", t.assignedServerName || "—"],
      ["Server id", t.assignedServerId != null ? String(t.assignedServerId) : "—"],
    ]);
    let checksHtml = "";
    if (checksErr) {
      checksHtml = "<p class='tbl-open-checks-loading'>" + escapeHtml(checksErr) + "</p>";
    } else if (!checks || !checks.length) {
      checksHtml = "<p class='tbl-open-checks-loading'>No open checks on this table.</p>";
    } else {
      checksHtml = checks.map(ch => {
        const code = ch.orderCode ?? ch.OrderCode ?? "";
        const st = ch.status ?? ch.Status ?? "";
        const kind = ch.checkKind ?? ch.CheckKind ?? "";
        const grand = ch.grandTotalUsd ?? ch.GrandTotalUsd ?? 0;
        const lines = (ch.lines ?? ch.Lines ?? []).map(ln => {
          const nm = ln.name ?? ln.Name ?? "Item";
          const qty = ln.quantity ?? ln.Quantity ?? 0;
          return nm + " ×" + qty;
        });
        const lineText = lines.length ? lines.join(", ") : "No lines";
        return (
          "<div class='tbl-open-check'>" +
          "<div class='tbl-open-check__top'><span>" + escapeHtml(String(code)) + "</span>" +
          "<span class='muted'>" + escapeHtml(formatUsd(grand)) + "</span></div>" +
          "<div class='tbl-open-check__meta'>" + escapeHtml(String(st)) +
          (kind ? " · " + escapeHtml(String(kind)) : "") + "</div>" +
          "<div class='tbl-open-check__lines'>" + escapeHtml(lineText) + "</div></div>");
      }).join("");
    }
    const checksSection =
      "<div class='res-detail-section'><p class='res-detail-section-title'>Open checks</p>" + checksHtml + "</div>";
    const url = guestMenuUrlForTableId(t.id);
    const guest =
      "<div class='res-detail-section'><p class='res-detail-section-title'>Guest menu</p>" +
      "<div class='res-kv'><span class='res-kv-lbl'>Link</span>" +
      "<span class='res-kv-val'><a class='tbl-guest-link' href='" + escapeHtml(url) + "' target='_blank' rel='noopener noreferrer'>" +
      escapeHtml(url) + "</a></span></div></div>";
    return info + server + checksSection + guest;
  }

  function closeTableDetail() {
    $("tableDetailModal").classList.add("hidden");
    tableDetailCache = null;
  }

  async function openTableDetail(id) {
    const t = tables.find(x => Number(x.id) === Number(id));
    if (!t) return;
    tableDetailCache = t;
    const avail = tableAvailFromStatus(t.status);
    const num = t.tableNumber ?? "";
    const name = t.name ? " · " + t.name : "";
    $("tableDetailTitle").textContent = "Table " + num + name;
    $("tableDetailMeta").innerHTML =
      "<span class='tbl-ref'>#" + escapeHtml(String(t.id)) + "</span>" +
      "<span class='tbl-avail-pill " + avail.cls + "'>" + escapeHtml(avail.label) + "</span>";
    $("tableDetailBody").innerHTML = buildTableDetailBody(t, null, "Loading open checks…");
    const actions = $("tableDetailActions");
    const sid = String(t.id);
    actions.innerHTML =
      "<button type='button' class='btn btn-primary btn-sm' data-tbl-open-guest='" + sid + "'>Open guest menu</button>" +
      "<button type='button' class='btn btn-ghost btn-sm' data-tbl-copy-link='" + sid + "'>Copy guest link</button>";
    actions.querySelector("[data-tbl-open-guest]").onclick = () => {
      window.open(guestMenuUrlForTableId(sid), "_blank", "noopener,noreferrer");
    };
    actions.querySelector("[data-tbl-copy-link]").onclick = async ev => {
      const btn = ev.currentTarget;
      const url = guestMenuUrlForTableId(sid);
      try {
        await navigator.clipboard.writeText(url);
        const prev = btn.textContent;
        btn.textContent = "Copied!";
        setTimeout(() => { btn.textContent = prev; }, 2000);
      } catch {
        window.prompt("Copy this URL:", url);
      }
    };
    $("tableDetailModal").classList.remove("hidden");

    let checks = [];
    let checksErr = "";
    const r = await api("/api/server/tables/" + encodeURIComponent(String(id)) + "/open-checks", "GET");
    if (!r.ok) {
      checksErr = (r.body && (r.body.message || r.body.title)) || "Could not load open checks.";
    } else {
      const body = r.body || {};
      checks = Array.isArray(body.checks) ? body.checks : (Array.isArray(body.Checks) ? body.Checks : []);
    }
    if (tableDetailCache && Number(tableDetailCache.id) === Number(id))
      $("tableDetailBody").innerHTML = buildTableDetailBody(t, checks, checksErr);
  }

  function renderTableMenuList() {
    const el = $("tableMenuList");
    if (!el) return;
    if (tableMenuLoadError) {
      el.innerHTML =
        "<div class='danger' style='padding:12px;'>" + escapeHtml(tableMenuLoadError) +
        " <span class='muted'>Try Refresh tables.</span></div>";
      return;
    }
    const rows = tables.slice().sort((a, b) => (Number(a.tableNumber) || 0) - (Number(b.tableNumber) || 0));
    if (!rows.length) {
      el.innerHTML =
        "<div class='muted' style='padding:16px;'>No tables loaded. Use <strong>Refresh tables</strong> after tables are added in the back office.</div>";
      return;
    }
    el.innerHTML = rows.map(t => {
      const id = t.id;
      const num = escapeHtml(String(t.tableNumber ?? ""));
      const name = escapeHtml(t.name || "Unnamed");
      const avail = tableAvailFromStatus(t.status);
      const srv = t.assignedServerName ? escapeHtml(t.assignedServerName) : "—";
      const sid = String(id);
      return (
        "<div class='tbl-card'>" +
        "<button type='button' class='tbl-card__hit' data-tbl-open='" + sid + "' aria-label='Table " + num + " details'>" +
        "<div class='tbl-card__rail" + avail.rail + "' aria-hidden='true'></div>" +
        "<div class='tbl-card__body'>" +
        "<div class='tbl-card__main'>" +
        "<div class='tbl-card__num'>Table " + num + "</div>" +
        "<div class='tbl-card__name'>" + name + "</div>" +
        "<div class='tbl-card__server'><strong>Server</strong> " + srv + "</div>" +
        "</div>" +
        "<span class='tbl-avail-pill " + avail.cls + "'>" + escapeHtml(avail.label) + "</span>" +
        "</div></button></div>");
    }).join("");
    el.querySelectorAll("[data-tbl-open]").forEach(b => {
      b.onclick = () => { void openTableDetail(Number(b.getAttribute("data-tbl-open"))); };
    });
  }

  async function refreshTableMenuView() {
    const el = $("tableMenuList");
    if (!token) {
      tableMenuLoadError = "Sign in to load tables.";
      renderTableMenuList();
      return;
    }
    if (el) el.innerHTML = "<div class='muted' style='padding:16px;'>Loading tables…</div>";
    try {
      await loadTables();
    } catch (e) {
      tableMenuLoadError = "Could not load tables. " + (e && e.message ? e.message : String(e));
      tables = [];
      console.warn("[Reception] refresh tables", e);
    }
    renderTableMenuList();
  }

  function deliveryStatusPillClass(st) {
    const s = String(st || "").toLowerCase();
    if (s.includes("ready")) return "dp-pill dp-pill--ready";
    if (s.includes("kitchen") || s.includes("waiting")) return "dp-pill dp-pill--kitchen";
    return "dp-pill";
  }

  function updateDeliveryNavBadge() {
    const badge = $("navDeliveryBadge");
    if (!badge) return;
    const readyCount = deliveryPickupRows.filter(r =>
      r.isReadyForHandoff || r.IsReadyForHandoff ||
      String(r.status ?? r.Status ?? "").toLowerCase().includes("ready")).length;
    if (readyCount > 0) {
      badge.textContent = String(readyCount);
      badge.classList.remove("hidden");
      badge.classList.add("nav-badge--ready");
    } else {
      badge.classList.add("hidden");
      badge.classList.remove("nav-badge--ready");
      badge.textContent = "";
    }
  }

  function renderDeliveryReadyBanner() {
    const el = $("dpReadyBanner");
    if (!el) return;
    const ready = deliveryPickupRows.filter(r =>
      r.isReadyForHandoff || r.IsReadyForHandoff ||
      String(r.status ?? r.Status ?? "").toLowerCase().includes("ready"));
    if (!ready.length) {
      el.classList.add("hidden");
      el.textContent = "";
      return;
    }
    const names = ready.slice(0, 4).map(r => {
      const code = r.orderCode ?? r.OrderCode ?? "";
      const guest = r.guestName ?? r.GuestName ?? "Guest";
      return (code ? "#" + String(code).replace(/^#/, "") + " " : "") + guest;
    });
    el.textContent = ready.length === 1
      ? "Ready for handoff: " + names[0]
      : ready.length + " orders ready for handoff — " + names.join(" · ");
    el.classList.remove("hidden");
  }

  function renderDeliveryPickupList() {
    const el = $("deliveryPickupList");
    if (!el) return;
    if (!deliveryPickupRows.length) {
      el.innerHTML =
        "<div class='muted' style='padding:16px;'>No active online delivery or pickup orders.</div>";
      renderDeliveryReadyBanner();
      updateDeliveryNavBadge();
      return;
    }
    el.innerHTML = deliveryPickupRows.map(r => {
      const isReady = !!(r.isReadyForHandoff || r.IsReadyForHandoff);
      const code = escapeHtml(String(r.orderCode ?? r.OrderCode ?? ""));
      const guest = escapeHtml(String(r.guestName ?? r.GuestName ?? "Guest"));
      const phone = escapeHtml(String(r.guestPhone ?? r.GuestPhone ?? ""));
      const ft = escapeHtml(String(r.fulfillmentType ?? r.FulfillmentType ?? ""));
      const st = escapeHtml(String(r.status ?? r.Status ?? ""));
      const when = escapeHtml(String(r.createdAtDisplay ?? r.CreatedAtDisplay ?? ""));
      const items = escapeHtml(String(r.itemsSummary ?? r.ItemsSummary ?? ""));
      const pill = deliveryStatusPillClass(st);
      return (
        "<div class='dp-order-card" + (isReady ? " dp-order-card--ready" : "") + "'>" +
        "<div class='dp-order-card__top'>" +
        "<span class='dp-order-card__code'>" + code + "</span>" +
        "<span class='" + pill + "'>" + st + "</span></div>" +
        "<div><strong>" + guest + "</strong>" + (phone ? " · " + phone : "") + "</div>" +
        "<div class='muted' style='margin-top:4px;'>" + ft + " · " + when + "</div>" +
        "<div style='margin-top:6px;'>" + items + "</div></div>");
    }).join("");
    renderDeliveryReadyBanner();
    updateDeliveryNavBadge();
  }

  async function loadDeliveryPickupOrders() {
    const r = await api("/api/reception/delivery-pickup-orders", "GET");
    if (!r.ok) {
      const detail = (r.body && (r.body.message || r.body.title || r.body.detail)) || "";
      $("deliveryPickupList").innerHTML =
        "<div class='danger' style='padding:12px;'>Could not load delivery / pickup orders (" +
        escapeHtml(String(r.status)) + "). " + escapeHtml(String(detail)) + "</div>";
      console.warn("[Reception] delivery-pickup-orders", r.status, r.body);
      return;
    }
    deliveryPickupRows = Array.isArray(r.body) ? r.body : [];
    renderDeliveryPickupList();
  }

  function scheduleDeliveryReadyFlash(msg) {
    const el = $("deliveryReadyFlash");
    if (!el) return;
    el.textContent = msg || "";
    if (deliveryReadyFlashTimer) clearTimeout(deliveryReadyFlashTimer);
    deliveryReadyFlashTimer = setTimeout(() => {
      el.textContent = "";
      deliveryReadyFlashTimer = null;
    }, 12000);
  }

  function setHubPill(state) {
    const el = $("hubPill");
    if (!el) return;
    el.classList.remove("ok", "warn", "off");
    if (state === "live") { el.textContent = "Live: connected"; el.classList.add("ok"); }
    else if (state === "degraded") { el.textContent = "Live: reconnecting"; el.classList.add("warn"); }
    else { el.textContent = "Live: polling"; el.classList.add("off"); }
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
    conn.on("ReceptionDeliveryPickupChanged", payload => {
      const p = payload && typeof payload === "object" ? payload : {};
      const code = (p.orderCode ?? p.OrderCode ?? "").toString();
      const reason = (p.reason ?? "").toString();
      const isReady = !!(p.isReady ?? p.IsReady);
      if (isReady || reason === "order-ready") {
        scheduleDeliveryReadyFlash(
          (code ? "#" + code.replace(/^#/, "") + " " : "") + "ready for front-desk handoff");
      } else if (reason === "online-order-submitted") {
        scheduleDeliveryReadyFlash(
          (code ? "#" + code.replace(/^#/, "") + " " : "") + "new online order");
      }
      void loadDeliveryPickupOrders();
    });
    conn.onreconnecting(() => setHubPill("degraded"));
    conn.onreconnected(() => {
      setHubPill("live");
      conn.invoke("JoinReception").catch(() => {});
    });
    conn.onclose(() => setHubPill("off"));
    try {
      await conn.start();
      await conn.invoke("JoinReception");
      orderHubConnection = conn;
      setHubPill("live");
    } catch (e) {
      console.warn("[Reception] hub", e);
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
      if (typeof signalR !== "undefined" && orderHubConnection &&
        orderHubConnection.state === signalR.HubConnectionState.Connected) return;
      if (currentView === "deliveryPickup") void loadDeliveryPickupOrders();
      if (currentView === "reservations") void loadReservations();
    }, 28000);
  }

  async function submitWalkInReservation() {
    const name = ($("resCreateName").value || "").trim();
    const phone = ($("resCreatePhone").value || "").trim();
    const email = ($("resCreateEmail").value || "").trim();
    const whenLocal = $("resCreateWhen").value;
    const party = Number($("resCreateParty").value || 2);
    const tableId = $("resCreateTable").value ? Number($("resCreateTable").value) : null;
    const notes = ($("resCreateNotes").value || "").trim();
    const msgEl = $("resCreateMsg");
    const btn = $("btnCreateReservation");
    msgEl.textContent = "";
    msgEl.classList.remove("ok");
    if (!name || !phone || !whenLocal) {
      msgEl.textContent = "Name, phone, and date/time are required.";
      return;
    }
    const whenErr = validateResCreateWhenLocal(whenLocal);
    if (whenErr) {
      msgEl.textContent = whenErr;
      return;
    }
    if (party < 1 || party > 99) {
      msgEl.textContent = "Party size must be between 1 and 99.";
      return;
    }
    const body = {
      guestName: name,
      guestPhone: phone,
      guestEmail: email || null,
      plannedStartUtc: new Date(whenLocal).toISOString(),
      partySize: party,
      tableId: tableId,
      userNotes: notes
    };
    btn.disabled = true;
    const r = await api("/api/cashier/reservations/engagements", "POST", body);
    btn.disabled = false;
    if (!r.ok) {
      msgEl.textContent = r.body?.message || "Could not create reservation.";
      return;
    }
    closeResCreateModal();
    await loadReservations();
  }

  function setView(v) {
    currentView = v;
    $("viewTableMenu").classList.toggle("hidden", v !== "tableMenu");
    $("viewMenu").classList.toggle("hidden", v !== "menu");
    $("viewReservations").classList.toggle("hidden", v !== "reservations");
    $("viewDeliveryPickup").classList.toggle("hidden", v !== "deliveryPickup");
    $("navTableMenu").classList.toggle("active", v === "tableMenu");
    $("navMenu").classList.toggle("active", v === "menu");
    $("navRes").classList.toggle("active", v === "reservations");
    $("navDelivery").classList.toggle("active", v === "deliveryPickup");
    if (v === "tableMenu") renderTableMenuList();
    if (v === "menu") loadMenu().catch(e => alert(e.message || String(e)));
    if (v === "reservations") {
      void loadResScheduling();
      loadReservations();
    }
    if (v === "deliveryPickup") loadDeliveryPickupOrders();
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
    const res = await api("/api/auth/login", "POST", { staffId, pin, portal: "Reception" }, false);
    if (!res.ok || !res.body?.accessToken) {
      $("loginErr").textContent = "Login failed (" + res.status + "). " + (res.body?.message || res.body?.title || "Check Front desk, cashier, admin, or manager role.");
      $("loginErr").classList.remove("hidden");
      return;
    }
    token = res.body.accessToken;
    me = res.body;
    $("sessionLabel").textContent = (me.name || "") + " (" + (me.signInId || me.employeeUniqueId || "") + ")";
    $("loginWrap").classList.add("hidden");
    $("app").classList.remove("hidden");
    await loadPortalData();
    renderTableMenuList();
    void loadResScheduling();
    setView("tableMenu");
    void startOrderHub();
    startPolling();
  };

  $("btnLogout").onclick = () => {
    stopHubAndPoll();
    token = "";
    me = null;
    tables = [];
    deliveryPickupRows = [];
    revokeImgBlob($("brandLogo"));
    revokeImgBlob($("staffPhoto"));
    $("brandLogo").classList.remove("show");
    $("staffPhoto").classList.remove("show");
    $("app").classList.add("hidden");
    $("loginWrap").classList.remove("hidden");
    setHubPill("off");
  };

  $("navTableMenu").onclick = () => setView("tableMenu");
  $("navMenu").onclick = () => setView("menu");
  $("navRes").onclick = () => setView("reservations");
  $("navDelivery").onclick = () => setView("deliveryPickup");
  $("btnRefreshDelivery").onclick = () => { void loadDeliveryPickupOrders(); };
  $("btnOpenCreateReservation").onclick = () => openResCreateModal();
  $("btnCreateReservation").onclick = () => { void submitWalkInReservation(); };
  $("btnResCreateCancel").onclick = closeResCreateModal;
  $("resCreateModal").addEventListener("click", e => { if (e.target.id === "resCreateModal") closeResCreateModal(); });
  $("btnRefreshTableMenu").onclick = () => { void refreshTableMenuView(); };
  $("btnRefreshMenu").onclick = () => loadMenu().catch(e => alert(e.message || String(e)));
  $("btnRefreshRes").onclick = () => loadReservations();
  $("menuSearch").oninput = () => renderMenuFromCatalog();

  $("tableDetailClose").onclick = closeTableDetail;
  $("tableDetailModal").addEventListener("click", e => { if (e.target.id === "tableDetailModal") closeTableDetail(); });
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

  if (window.location.protocol === "file:") {
    $("loginErr").textContent = "Open this page from the API site (e.g. http://localhost:8080/reception/) so login works.";
    $("loginErr").classList.remove("hidden");
  }
})();
