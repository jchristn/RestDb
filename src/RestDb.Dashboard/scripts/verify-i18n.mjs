import { resources } from '../src/i18n/dashboardResources.js';
import { SUPPORTED_LOCALES } from '../src/i18n/localeRegistry.js';

const SOURCE_LOCALE = 'en-US';
const SUSPICIOUS_MOJIBAKE = /(Ã.|Â.|â[\u0080-\u00bf]|æ[\u0080-\u00bf]|ã[\u0080-\u00bf]|Ø[\u0080-\u00bf]|Ù[\u0080-\u00bf])/;

function flattenObject(input, prefix = '') {
  if (!input || typeof input !== 'object' || Array.isArray(input)) {
    return prefix ? { [prefix]: input } : {};
  }

  return Object.entries(input).reduce((accumulator, [key, value]) => {
    const nextPrefix = prefix ? `${prefix}.${key}` : key;
    return {
      ...accumulator,
      ...flattenObject(value, nextPrefix)
    };
  }, {});
}

function formatPercent(value) {
  return `${(value * 100).toFixed(1)}%`;
}

const failures = [];
const sourceEntries = flattenObject(resources[SOURCE_LOCALE]?.translation || {});
const sourceKeys = Object.keys(sourceEntries).sort();

for (const locale of SUPPORTED_LOCALES) {
  if (!resources[locale.code]?.translation) {
    failures.push(`${locale.code}: missing translation resource`);
    continue;
  }

  if (SUSPICIOUS_MOJIBAKE.test(locale.nativeLabel) || SUSPICIOUS_MOJIBAKE.test(locale.englishLabel)) {
    failures.push(`${locale.code}: locale label appears to contain mojibake`);
  }

  const localizedEntries = flattenObject(resources[locale.code].translation);
  const localizedKeys = Object.keys(localizedEntries).sort();
  const missingKeys = sourceKeys.filter((key) => !(key in localizedEntries));
  const extraKeys = localizedKeys.filter((key) => !(key in sourceEntries));

  if (missingKeys.length > 0) {
    failures.push(`${locale.code}: missing keys -> ${missingKeys.join(', ')}`);
  }

  if (extraKeys.length > 0) {
    failures.push(`${locale.code}: extra keys -> ${extraKeys.join(', ')}`);
  }

  const suspiciousKey = Object.entries(localizedEntries).find(
    ([, value]) => typeof value === 'string' && SUSPICIOUS_MOJIBAKE.test(value)
  );
  if (suspiciousKey) {
    failures.push(`${locale.code}: suspicious mojibake in key ${suspiciousKey[0]}`);
  }

  if (locale.code !== SOURCE_LOCALE) {
    const identicalCount = sourceKeys.reduce((count, key) => {
      return count + (localizedEntries[key] === sourceEntries[key] ? 1 : 0);
    }, 0);
    const identicalRatio = identicalCount / sourceKeys.length;
    console.log(`${locale.code}: identical-to-source ratio ${formatPercent(identicalRatio)}`);

    if (identicalRatio > 0.85) {
      failures.push(`${locale.code}: translation appears to fall back to English too often (${formatPercent(identicalRatio)})`);
    }
  }
}

if (failures.length > 0) {
  console.error('i18n verification failed:');
  failures.forEach((failure) => console.error(`- ${failure}`));
  process.exit(1);
}

console.log(`i18n verification passed for ${SUPPORTED_LOCALES.length} locales.`);
