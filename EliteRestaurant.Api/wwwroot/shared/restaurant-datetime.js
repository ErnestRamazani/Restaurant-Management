/**
 * Restaurant wall-clock timezone (IANA). Set from portal config after login.
 * Falls back to Africa/Kinshasa when unset.
 */
(function (global) {
  const DEFAULT_TZ = "Africa/Kinshasa";
  let activeTimeZoneId = DEFAULT_TZ;

  function normalizeId(id) {
    const t = id != null ? String(id).trim() : "";
    return t || DEFAULT_TZ;
  }

  function setRestaurantTimeZone(id) {
    activeTimeZoneId = normalizeId(id);
  }

  function getRestaurantTimeZone() {
    return activeTimeZoneId;
  }

  function partsInTimeZone(date, timeZone) {
    const fmt = new Intl.DateTimeFormat("en-US", {
      timeZone,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    });
    const map = {};
    for (const p of fmt.formatToParts(date)) {
      if (p.type !== "literal") map[p.type] = p.value;
    }
    return {
      year: Number(map.year),
      month: Number(map.month),
      day: Number(map.day),
      hour: Number(map.hour),
      minute: Number(map.minute),
      second: Number(map.second),
    };
  }

  /** Interpret datetime-local value (yyyy-MM-ddTHH:mm) as restaurant wall time → UTC ISO. */
  function restaurantLocalInputToUtcIso(localValue, timeZoneId) {
    if (!localValue) return "";
    const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(String(localValue).trim());
    if (!m) return "";
    const tz = normalizeId(timeZoneId || activeTimeZoneId);
    const target = {
      year: Number(m[1]),
      month: Number(m[2]),
      day: Number(m[3]),
      hour: Number(m[4]),
      minute: Number(m[5]),
      second: 0,
    };
    let guess = Date.UTC(target.year, target.month - 1, target.day, target.hour, target.minute, 0);
    for (let i = 0; i < 4; i++) {
      const got = partsInTimeZone(new Date(guess), tz);
      const diffMin =
        (target.hour - got.hour) * 60 +
        (target.minute - got.minute) +
        (target.day - got.day) * 24 * 60;
      if (diffMin === 0) break;
      guess += diffMin * 60 * 1000;
    }
    return new Date(guess).toISOString();
  }

  function formatRestaurantDateTime(isoOrDate, options, timeZoneId) {
    const d = isoOrDate instanceof Date ? isoOrDate : new Date(isoOrDate);
    if (Number.isNaN(d.getTime())) return "";
    const tz = normalizeId(timeZoneId || activeTimeZoneId);
    return new Intl.DateTimeFormat(undefined, { timeZone: tz, ...options }).format(d);
  }

  function formatRestaurantTimeShort(isoOrDate, timeZoneId) {
    return formatRestaurantDateTime(
      isoOrDate,
      { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" },
      timeZoneId,
    );
  }

  function formatRestaurantDateTimeMedium(isoOrDate, timeZoneId) {
    return formatRestaurantDateTime(
      isoOrDate,
      { dateStyle: "medium", timeStyle: "short" },
      timeZoneId,
    );
  }

  function restaurantDatetimeLocalValue(dayOffsetFromToday, hour, minute, timeZoneId) {
    const tz = normalizeId(timeZoneId || activeTimeZoneId);
    const now = new Date();
    const p = partsInTimeZone(now, tz);
    const d = new Date(Date.UTC(p.year, p.month - 1, p.day + dayOffsetFromToday, hour, minute, 0));
    const got = partsInTimeZone(d, tz);
    const pad = (n) => String(n).padStart(2, "0");
    return `${got.year}-${pad(got.month)}-${pad(got.day)}T${pad(hour)}:${pad(minute)}`;
  }

  global.EliteRestaurantDateTime = {
    DEFAULT_TZ,
    normalizeId,
    setRestaurantTimeZone,
    getRestaurantTimeZone,
    restaurantLocalInputToUtcIso,
    formatRestaurantDateTime,
    formatRestaurantTimeShort,
    formatRestaurantDateTimeMedium,
    restaurantDatetimeLocalValue,
    partsInTimeZone,
  };
})(typeof globalThis !== "undefined" ? globalThis : window);
