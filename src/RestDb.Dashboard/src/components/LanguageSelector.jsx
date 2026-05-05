import { useTranslation } from 'react-i18next';
import { setActiveLocale } from '../i18n';
import { resolveLocale, SUPPORTED_LOCALES } from '../i18n/localeRegistry';

function LanguageSelector() {
  const { i18n, t } = useTranslation('translation');
  const activeLocale = resolveLocale(i18n.resolvedLanguage || i18n.language);

  return (
    <div className="language-selector">
      <select
        aria-label={t('navigation.languageSelector')}
        onChange={(event) => setActiveLocale(event.target.value)}
        title={t('navigation.languageSelectorTooltip', { defaultValue: 'Choose the dashboard display language and locale-specific formatting.' })}
        value={activeLocale}
      >
        {SUPPORTED_LOCALES.map((locale) => (
          <option key={locale.code} value={locale.code}>
            {locale.nativeLabel}
          </option>
        ))}
      </select>
    </div>
  );
}

export default LanguageSelector;
