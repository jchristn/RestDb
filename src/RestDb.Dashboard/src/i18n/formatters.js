import i18n, { getActiveLocale } from '.';

function getFallbackLabel() {
  return i18n.t('common.notAvailable', { ns: 'translation', defaultValue: '—' });
}

function formatUnit(value, locale, unit) {
  return new Intl.NumberFormat(locale, {
    style: 'unit',
    unit,
    unitDisplay: 'short',
    maximumFractionDigits: value % 1 === 0 ? 0 : 1
  }).format(value);
}

export function formatNumber(value, locale = getActiveLocale()) {
  return new Intl.NumberFormat(locale).format(value ?? 0);
}

export function formatDate(value, locale = getActiveLocale()) {
  if (!value) {
    return getFallbackLabel();
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium'
  }).format(date);
}

export function formatTime(value, locale = getActiveLocale()) {
  if (!value) {
    return getFallbackLabel();
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat(locale, {
    timeStyle: 'short'
  }).format(date);
}

export function formatDateTime(value, locale = getActiveLocale()) {
  if (!value) {
    return getFallbackLabel();
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date);
}

export function formatRelativeTime(value, locale = getActiveLocale()) {
  if (!value) {
    return getFallbackLabel();
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  const diffMs = date.getTime() - Date.now();
  const diffMinutes = Math.round(diffMs / 60000);
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });

  if (Math.abs(diffMinutes) < 60) {
    return formatter.format(diffMinutes, 'minute');
  }

  const diffHours = Math.round(diffMinutes / 60);
  if (Math.abs(diffHours) < 24) {
    return formatter.format(diffHours, 'hour');
  }

  const diffDays = Math.round(diffHours / 24);
  return formatter.format(diffDays, 'day');
}

export function formatDuration(milliseconds, locale = getActiveLocale()) {
  const value = Number(milliseconds ?? 0);
  if (!Number.isFinite(value)) {
    return getFallbackLabel();
  }

  if (Math.abs(value) < 1000) {
    return formatUnit(value, locale, 'millisecond');
  }

  const seconds = value / 1000;
  if (Math.abs(seconds) < 60) {
    return formatUnit(seconds, locale, 'second');
  }

  const minutes = seconds / 60;
  if (Math.abs(minutes) < 60) {
    return formatUnit(minutes, locale, 'minute');
  }

  const hours = minutes / 60;
  return formatUnit(hours, locale, 'hour');
}

export function formatBytes(value, locale = getActiveLocale()) {
  const number = Number(value ?? 0);
  if (!Number.isFinite(number)) {
    return getFallbackLabel();
  }

  const units = ['byte', 'kilobyte', 'megabyte', 'gigabyte', 'terabyte'];
  let unitIndex = 0;
  let normalizedValue = Math.abs(number);

  while (normalizedValue >= 1024 && unitIndex < units.length - 1) {
    normalizedValue /= 1024;
    unitIndex += 1;
  }

  const signedValue = number < 0 ? -normalizedValue : normalizedValue;
  return formatUnit(signedValue, locale, units[unitIndex]);
}

export function formatPercent(value, locale = getActiveLocale()) {
  return new Intl.NumberFormat(locale, {
    style: 'percent',
    maximumFractionDigits: 1
  }).format(value ?? 0);
}

export function formatList(values, locale = getActiveLocale()) {
  if (!values || values.length < 1) {
    return getFallbackLabel();
  }

  return new Intl.ListFormat(locale, {
    style: 'long',
    type: 'conjunction'
  }).format(values);
}
