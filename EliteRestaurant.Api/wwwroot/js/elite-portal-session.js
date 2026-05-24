/**
 * Persist staff portal JWT in sessionStorage so refresh keeps the session (same tab).
 * Clears storage when the token is expired.
 */
(function (global) {
  function decodeJwtPayload(token) {
    try {
      const parts = String(token).split(".");
      if (parts.length < 2) return null;
      const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
      return JSON.parse(atob(padded));
    } catch {
      return null;
    }
  }

  function isJwtUsable(token) {
    if (!token || typeof token !== "string") return false;
    const payload = decodeJwtPayload(token);
    if (!payload || payload.exp == null) return true;
    const now = Math.floor(Date.now() / 1000);
    return now < Number(payload.exp) - 15;
  }

  function keys(portalId) {
    const id = String(portalId || "").trim();
    return {
      token: "elitePortalJwt:" + id,
      me: "elitePortalMe:" + id
    };
  }

  global.ElitePortalSession = {
    isJwtUsable,
    keys,
    save(portalId, token, me) {
      const k = keys(portalId);
      if (token) sessionStorage.setItem(k.token, token);
      else sessionStorage.removeItem(k.token);
      if (me != null) sessionStorage.setItem(k.me, JSON.stringify(me));
      else sessionStorage.removeItem(k.me);
    },
    load(portalId) {
      const k = keys(portalId);
      const token = sessionStorage.getItem(k.token) || "";
      if (!isJwtUsable(token)) {
        sessionStorage.removeItem(k.token);
        sessionStorage.removeItem(k.me);
        return { token: "", me: null };
      }
      let me = null;
      try {
        const raw = sessionStorage.getItem(k.me);
        me = raw ? JSON.parse(raw) : null;
      } catch {
        me = null;
      }
      return { token, me };
    },
    clear(portalId) {
      const k = keys(portalId);
      sessionStorage.removeItem(k.token);
      sessionStorage.removeItem(k.me);
    },
    /** Read legacy kitchen/bar keys written before unified storage. */
    loadLegacy(tokenKey, meKey) {
      const token = sessionStorage.getItem(tokenKey) || "";
      if (!isJwtUsable(token)) {
        sessionStorage.removeItem(tokenKey);
        sessionStorage.removeItem(meKey);
        return { token: "", me: null };
      }
      let me = null;
      try {
        const raw = sessionStorage.getItem(meKey);
        me = raw ? JSON.parse(raw) : null;
      } catch {
        me = null;
      }
      return { token, me };
    },
    saveLegacy(tokenKey, meKey, token, me) {
      if (token) sessionStorage.setItem(tokenKey, token);
      else sessionStorage.removeItem(tokenKey);
      if (me != null) sessionStorage.setItem(meKey, JSON.stringify(me));
      else sessionStorage.removeItem(meKey);
    },
    clearLegacy(tokenKey, meKey) {
      sessionStorage.removeItem(tokenKey);
      sessionStorage.removeItem(meKey);
    }
  };
})(window);
