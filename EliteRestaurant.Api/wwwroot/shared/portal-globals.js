/** Browser polyfill so inline portal scripts can safely use `global` like Node-style bundles. */
(function (w) {
  if (typeof w.global === "undefined") w.global = w;
})(typeof window !== "undefined" ? window : globalThis);
