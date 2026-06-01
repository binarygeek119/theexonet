/**
 * RAVA web UI translations (Weblate-managed JSON under /locales/).
 * Exonet / Offworld News (exonet.js, AI articles) is intentionally excluded.
 */

const LOCALE_STORAGE_KEY = "rava-locale";
const DEFAULT_LOCALE = "en";

/** BCP 47 codes Weblate may add; only en is shipped in-repo until translated. */
export const LOCALE_OPTIONS = [
  { code: "en", label: "English" },
  { code: "es", label: "Español" },
  { code: "fr", label: "Français" },
  { code: "de", label: "Deutsch" },
  { code: "pt", label: "Português" },
];

let activeLocale = DEFAULT_LOCALE;
let messages = {};
let namespaces = [];

function normalizeLocale(code) {
  if (!code || typeof code !== "string") {
    return DEFAULT_LOCALE;
  }
  const base = code.trim().toLowerCase().split("-")[0];
  return LOCALE_OPTIONS.some((option) => option.code === base) ? base : DEFAULT_LOCALE;
}

function interpolate(template, params) {
  if (!params) {
    return template;
  }
  return template.replace(/\{(\w+)\}/g, (_, key) =>
    params[key] !== undefined && params[key] !== null ? String(params[key]) : `{${key}}`,
  );
}

export function getLocale() {
  return activeLocale;
}

export function t(key, params) {
  const value = messages[key];
  if (value === undefined || value === null || value === "") {
    return interpolate(key, params);
  }
  return interpolate(String(value), params);
}

async function loadNamespace(locale, namespace) {
  const url = `/locales/${locale}/${namespace}.json`;
  const response = await fetch(url, { cache: "no-cache" });
  if (!response.ok) {
    if (locale !== DEFAULT_LOCALE) {
      return loadNamespace(DEFAULT_LOCALE, namespace);
    }
    console.warn(`[i18n] Missing ${url}`);
    return {};
  }
  return response.json();
}

export async function initI18n({ locale, namespaces: ns } = {}) {
  namespaces = ns ?? ["game"];
  const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
  activeLocale = normalizeLocale(locale ?? stored ?? navigator.language ?? DEFAULT_LOCALE);
  localStorage.setItem(LOCALE_STORAGE_KEY, activeLocale);

  const bundles = await Promise.all(
    namespaces.map((namespace) => loadNamespace(activeLocale, namespace)),
  );
  messages = Object.assign({}, ...bundles);

  document.documentElement.lang = activeLocale;
  const titleKey = messages["meta.pageTitle"];
  if (titleKey) {
    document.title = titleKey;
  }

  return activeLocale;
}

export async function setLocale(locale) {
  const next = normalizeLocale(locale);
  if (next === activeLocale) {
    return activeLocale;
  }
  await initI18n({ locale: next, namespaces });
  applyTranslations(document);
  document.dispatchEvent(new CustomEvent("rava:localechange", { detail: { locale: next } }));
  return next;
}

function translateElement(el) {
  const key = el.getAttribute("data-i18n");
  if (key) {
    el.textContent = t(key);
  }

  const placeholderKey = el.getAttribute("data-i18n-placeholder");
  if (placeholderKey && "placeholder" in el) {
    el.placeholder = t(placeholderKey);
  }

  const titleKey = el.getAttribute("data-i18n-title");
  if (titleKey) {
    el.title = t(titleKey);
  }

  const ariaKey = el.getAttribute("data-i18n-aria-label");
  if (ariaKey) {
    el.setAttribute("aria-label", t(ariaKey));
  }
}

export function applyTranslations(root = document) {
  root.querySelectorAll("[data-i18n], [data-i18n-placeholder], [data-i18n-title], [data-i18n-aria-label]").forEach(
    translateElement,
  );

  root.querySelectorAll("select[data-i18n-options]").forEach((select) => {
    const prefix = select.getAttribute("data-i18n-options");
    select.querySelectorAll("option[data-i18n]").forEach((option) => {
      const key = option.getAttribute("data-i18n");
      if (key) {
        option.textContent = t(key);
      } else if (prefix && option.value) {
        option.textContent = t(`${prefix}.${option.value}`, option.textContent);
      }
    });
  });
}

export function wireLocaleSelector(selectEl) {
  if (!selectEl) {
    return;
  }

  if (!selectEl.options.length) {
    for (const option of LOCALE_OPTIONS) {
      const node = document.createElement("option");
      node.value = option.code;
      node.textContent = option.label;
      selectEl.appendChild(node);
    }
  }

  selectEl.value = activeLocale;
  selectEl.addEventListener("change", () => {
    setLocale(selectEl.value).catch((error) => console.error("[i18n]", error));
  });
}

export function wireLocaleSelectors() {
  document.querySelectorAll("[data-locale-select]").forEach((el) => wireLocaleSelector(el));
}
