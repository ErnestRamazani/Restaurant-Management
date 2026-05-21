/**
 * EliteRestaurant Localization System (elite-i18n.js)
 * Enhanced EN/FR language switching for all portals
 */
(function (global) {
  'use strict';

  const STORAGE_KEY = 'elite_lang';
  let currentLang = 'en';
  let nestedStrings = {};
  let flatStrings = {};

  function normalizeLanguage(code) {
    const str = String(code || '').toLowerCase().trim();
    return str.indexOf('fr') === 0 ? 'fr' : 'en';
  }

  function getSavedLanguage() {
    try {
      return normalizeLanguage(localStorage.getItem(STORAGE_KEY));
    } catch (e) {
      return 'en';
    }
  }

  function unflatten(flat) {
    const root = {};
    if (!flat || typeof flat !== 'object') return root;
    Object.keys(flat).forEach(function (key) {
      const parts = key.split('.');
      let node = root;
      for (let i = 0; i < parts.length; i += 1) {
        const part = parts[i];
        if (i === parts.length - 1) node[part] = flat[key];
        else {
          if (!node[part]) node[part] = {};
          node = node[part];
        }
      }
    });
    return root;
  }

  function resolvePath(obj, path) {
    const parts = path.split('.');
    let cur = obj;
    for (let i = 0; i < parts.length; i += 1) {
      if (!cur || typeof cur !== 'object') return null;
      cur = cur[parts[i]];
    }
    return cur;
  }

  function interpolate(str, vars) {
    if (!vars || !str) return str;
    let result = String(str);
    Object.keys(vars).forEach(function (key) {
      const pattern = new RegExp('\\{\\{' + key + '\\}\\}', 'g');
      result = result.replace(pattern, vars[key]);
    });
    return result;
  }

  function applyToDOM(root) {
    const scope = root || document;
    const elements = scope.querySelectorAll('[data-i18n]');
    elements.forEach(function (el) {
      const key = el.getAttribute('data-i18n');
      const fallback = el.getAttribute('data-i18n-fallback') || key;
      const text = EliteI18n.t(key, fallback);
      let attr = el.getAttribute('data-i18n-attr');

      // Smart defaults: INPUT/TEXTAREA -> placeholder, everything else -> textContent
      if (!attr) {
        if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
          attr = 'placeholder';
        } else {
          attr = 'textContent';
        }
      }

      if (attr === 'placeholder') {
        el.setAttribute('placeholder', text);
      } else if (attr === 'title') {
        el.setAttribute('title', text);
      } else if (attr === 'aria-label') {
        el.setAttribute('aria-label', text);
      } else if (attr === 'value') {
        el.value = text;
      } else {
        el.textContent = text;
      }
    });
  }

  function mountSwitcher(container) {
    if (!container) return;
    const el = typeof container === 'string' ? document.querySelector(container) : container;
    if (!el) {
      console.warn('[EliteI18n] Switcher container not found:', container);
      return;
    }

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.id = 'elite-lang-switcher';
    btn.className = 'elite-lang-switcher';
    btn.setAttribute('aria-label', 'Toggle language');
    btn.style.cssText = [
      'padding: 8px 12px',
      'border-radius: 6px',
      'border: 1px solid rgba(255, 255, 255, 0.2)',
      'background: rgba(255, 255, 255, 0.05)',
      'color: #d1d5db',
      'cursor: pointer',
      'font-weight: 600',
      'font-size: 12px',
      'text-transform: uppercase',
      'letter-spacing: 0.05em',
      'transition: all 0.2s ease',
      'font-family: inherit'
    ].join('; ');

    function updateButton() {
      btn.textContent = EliteI18n.lang === 'fr' ? 'EN' : 'FR';
    }

    updateButton();
    btn.addEventListener('click', function () {
      EliteI18n.toggleLanguage();
    });
    document.addEventListener('elite-language-changed', updateButton);
    el.appendChild(btn);
  }

  const EliteI18n = {
    lang: getSavedLanguage(),
    nested: {},
    flat: {},
    ready: false,

    t: function (key, fallback, vars) {
      let result = resolvePath(this.nested, key) || resolvePath(this.flat, key);
      if (!result && fallback != null) {
        result = String(fallback);
      }
      if (!result) {
        result = key;
      }
      if (vars && typeof result === 'string') {
        result = interpolate(result, vars);
      }
      return result;
    },

    setLanguage: async function (lang) {
      const code = normalizeLanguage(lang);
      try {
        await this.load(code);
        currentLang = code;
        this.lang = code;
        try {
          localStorage.setItem(STORAGE_KEY, code);
        } catch (e) {}
        document.documentElement.lang = code;
        global.dispatchEvent(new CustomEvent('elite-language-changed', { detail: { language: code } }));
        return code;
      } catch (err) {
        console.error('[EliteI18n] setLanguage failed:', err);
        throw err;
      }
    },

    toggleLanguage: function () {
      return this.setLanguage(this.lang === 'fr' ? 'en' : 'fr');
    },

    load: async function (lang) {
      const code = normalizeLanguage(lang || this.lang);
      try {
        const query = '?lang=' + encodeURIComponent(code);
        const response = await fetch('/api/language/strings' + query);
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const data = await response.json();
        const strings = data.strings || data.Strings || {};
        this.flat = strings;
        this.nested = unflatten(strings);
        this.lang = code;
        this.ready = true;
        return this;
      } catch (err) {
        console.error('[EliteI18n] Load failed:', err);
        throw err;
      }
    },

    mountSwitcher: function (container) {
      mountSwitcher(container);
    },

    applyToDocument: function (root) {
      applyToDOM(root);
    },

    interpolate: function (str, vars) {
      return interpolate(str, vars);
    },

    getSavedLanguage: function () {
      return getSavedLanguage();
    },

    init: async function () {
      const saved = this.getSavedLanguage();
      currentLang = saved;
      this.lang = saved;
      try {
        await this.load(saved);
        document.documentElement.lang = saved;
        applyToDOM();
      } catch (err) {
        console.warn('[EliteI18n] Init warning:', err);
      }
      return this;
    }
  };

  if (!global.EliteI18nDisableAutoInit) {
    if (global.document.readyState === 'loading') {
      global.document.addEventListener('DOMContentLoaded', function () {
        EliteI18n.init().catch(function (e) {
          console.warn('[EliteI18n] Auto-init failed:', e);
        });
      });
    } else {
      EliteI18n.init().catch(function (e) {
        console.warn('[EliteI18n] Auto-init failed:', e);
      });
    }
  }

  global.EliteI18n = EliteI18n;
})(window);
