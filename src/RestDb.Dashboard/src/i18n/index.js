import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import {
  DEFAULT_LOCALE,
  getLocaleMetadata,
  LOCALE_STORAGE_KEY,
  resolveLocale,
  SUPPORTED_LOCALES
} from './localeRegistry.js';
import { resources } from './dashboardResources.js';

function detectInitialLocale() {
  if (typeof window === 'undefined') {
    return DEFAULT_LOCALE;
  }

  const storedLocale = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  return resolveLocale(storedLocale || window.navigator.language || DEFAULT_LOCALE);
}

function applyDocumentLocale(localeCode) {
  if (typeof document === 'undefined') {
    return;
  }

  const locale = getLocaleMetadata(localeCode);
  document.documentElement.lang = locale.code;
  document.documentElement.dir = locale.dir;
}

if (!i18n.isInitialized) {
  const initialLocale = detectInitialLocale();

  i18n
    .use(initReactI18next)
    .init({
      fallbackLng: [DEFAULT_LOCALE],
      debug: false,
      lng: initialLocale,
      defaultNS: 'translation',
      ns: ['translation'],
      interpolation: {
        escapeValue: false
      },
      react: {
        useSuspense: false
      },
      resources,
      returnEmptyString: false,
      returnNull: false
    });

  applyDocumentLocale(initialLocale);
  i18n.on('languageChanged', (localeCode) => {
    const resolvedLocale = resolveLocale(localeCode);
    applyDocumentLocale(resolvedLocale);

    if (typeof window !== 'undefined') {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, resolvedLocale);
    }
  });
}

export function getActiveLocale() {
  return resolveLocale(i18n.resolvedLanguage || i18n.language || DEFAULT_LOCALE);
}

export function setActiveLocale(localeCode) {
  return i18n.changeLanguage(resolveLocale(localeCode));
}

export default i18n;
