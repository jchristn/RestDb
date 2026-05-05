export const LOCALE_STORAGE_KEY = 'restdb.dashboard.locale';
export const DEFAULT_LOCALE = 'en-US';

export const SUPPORTED_LOCALES = [
  {
    code: 'en-US',
    englishLabel: 'English (US)',
    nativeLabel: 'English (US)',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'de-DE',
    englishLabel: 'German',
    nativeLabel: 'Deutsch',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'es-ES',
    englishLabel: 'Spanish',
    nativeLabel: 'Español',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'fr-FR',
    englishLabel: 'French',
    nativeLabel: 'Français',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'ja-JP',
    englishLabel: 'Japanese',
    nativeLabel: '日本語',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'ko-KR',
    englishLabel: 'Korean',
    nativeLabel: '한국어',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'zh-CN',
    englishLabel: 'Mandarin Chinese',
    nativeLabel: '简体中文',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'zh-HK',
    englishLabel: 'Cantonese',
    nativeLabel: '廣東話',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'zh-TW',
    englishLabel: 'Traditional Chinese',
    nativeLabel: '繁體中文',
    dir: 'ltr',
    fallbackLocale: DEFAULT_LOCALE
  },
  {
    code: 'ar-SA',
    englishLabel: 'Arabic',
    nativeLabel: 'العربية',
    dir: 'rtl',
    fallbackLocale: DEFAULT_LOCALE
  }
];

const LOCALE_ALIASES = {
  ar: 'ar-SA',
  'ar-ae': 'ar-SA',
  'ar-eg': 'ar-SA',
  de: 'de-DE',
  en: 'en-US',
  'en-gb': 'en-US',
  es: 'es-ES',
  'es-mx': 'es-ES',
  fr: 'fr-FR',
  ja: 'ja-JP',
  kanji: 'ja-JP',
  ko: 'ko-KR',
  yue: 'zh-HK',
  'yue-hk': 'zh-HK',
  zh: 'zh-CN',
  'zh-cn': 'zh-CN',
  'zh-hans': 'zh-CN',
  'zh-hans-cn': 'zh-CN',
  'zh-hk': 'zh-HK',
  'zh-mo': 'zh-HK',
  'zh-tw': 'zh-TW',
  'zh-hant': 'zh-TW',
  'zh-hant-tw': 'zh-TW'
};

export function resolveLocale(localeCode) {
  if (!localeCode) {
    return DEFAULT_LOCALE;
  }

  const normalizedCode = String(localeCode).trim();
  if (!normalizedCode) {
    return DEFAULT_LOCALE;
  }

  const exactLocale = SUPPORTED_LOCALES.find(
    (locale) => locale.code.toLowerCase() === normalizedCode.toLowerCase()
  );
  if (exactLocale) {
    return exactLocale.code;
  }

  const normalizedAlias = normalizedCode.toLowerCase();
  const languageCode = normalizedAlias.split('-')[0];
  return LOCALE_ALIASES[normalizedAlias] || LOCALE_ALIASES[languageCode] || DEFAULT_LOCALE;
}

export function getLocaleMetadata(localeCode) {
  const resolvedCode = resolveLocale(localeCode);
  return SUPPORTED_LOCALES.find((locale) => locale.code === resolvedCode) || SUPPORTED_LOCALES[0];
}
