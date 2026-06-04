/**
 * Shared staff-portal alerts: toast banner + ring on OrderStageChanged (SignalR).
 * Include after signalR script. Call EliteOrderStageAlert.unlockAudio() on login click.
 */
(function (global) {
  "use strict";

  function norm(s) {
    return s == null ? "" : String(s).trim();
  }

  function parsePayload(raw) {
    if (!raw || typeof raw !== "object") return null;
    const audiences = raw.audiences ?? raw.Audiences;
    return {
      orderId: Number(raw.orderId ?? raw.OrderId) || 0,
      orderCode: norm(raw.orderCode ?? raw.OrderCode),
      previousStatus: norm(raw.previousStatus ?? raw.PreviousStatus),
      newStatus: norm(raw.newStatus ?? raw.NewStatus),
      stage: norm(raw.stage ?? raw.Stage),
      message: norm(raw.message ?? raw.Message),
      audiences: Array.isArray(audiences)
        ? audiences.map(a => norm(a)).filter(Boolean)
        : []
    };
  }

  function isForAudience(payload, audience) {
    if (!payload || !audience) return false;
    const a = norm(audience);
    return payload.audiences.some(x => x.toLowerCase() === a.toLowerCase());
  }

  function unlockAudio() {
    try {
      const Ctx = global.AudioContext || global.webkitAudioContext;
      if (!Ctx) return;
      const ctx = new Ctx();
      if (ctx.state === "suspended") void ctx.resume();
      void ctx.close();
    } catch (_) {}
  }

  /** Triple-tone ring (shared across portals). */
  function playRing() {
    try {
      const Ctx = global.AudioContext || global.webkitAudioContext;
      if (!Ctx) return;
      const ctx = new Ctx();
      const playTone = (freq, start, dur, vol) => {
        const o = ctx.createOscillator();
        const g = ctx.createGain();
        o.type = "sine";
        o.frequency.value = freq;
        o.connect(g);
        g.connect(ctx.destination);
        g.gain.setValueAtTime(0.0001, start);
        g.gain.exponentialRampToValueAtTime(vol, start + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
        o.start(start);
        o.stop(start + dur + 0.02);
      };
      const t0 = ctx.currentTime;
      playTone(880, t0, 0.12, 0.09);
      playTone(988, t0 + 0.16, 0.12, 0.09);
      playTone(1175, t0 + 0.32, 0.18, 0.1);
      void ctx.resume().finally(() => {
        setTimeout(() => {
          try { ctx.close(); } catch (_) {}
        }, 700);
      });
    } catch (_) {}
  }

  /**
   * @param {object} options
   * @param {string} options.audience - Server | Cashier | Kitchen | Reception
   * @param {(payload: object) => void} [options.onNotify] - refresh lists, badges, etc.
   * @param {(msg: string, payload: object) => void} [options.onFlash] - visual banner
   * @param {boolean} [options.playSound=true]
   */
  function handle(raw, options) {
    const payload = parsePayload(raw);
    if (!payload) return;
    const audience = norm(options && options.audience);
    if (audience && !isForAudience(payload, audience)) return;

    const msg =
      payload.message ||
      (payload.orderCode
        ? payload.orderCode + (payload.newStatus ? " → " + payload.newStatus : "")
        : "Order update");

    if (options && typeof options.onFlash === "function") options.onFlash(msg, payload);
    if (options && typeof options.onNotify === "function") options.onNotify(payload);
    if (!options || options.playSound !== false) playRing();
  }

  function wireHubConnection(conn, options) {
    if (!conn || typeof conn.on !== "function") return;
    conn.on("OrderStageChanged", p => handle(p, options));
  }

  global.EliteOrderStageAlert = {
    parsePayload,
    isForAudience,
    unlockAudio,
    playRing,
    handle,
    wireHubConnection
  };
})(typeof window !== "undefined" ? window : globalThis);
