function escapeCsvValue(value) {
  if (value === null || value === undefined) {
    return '';
  }

  const stringValue =
    typeof value === 'object' ? JSON.stringify(value) : String(value);

  if (/[",\r\n]/.test(stringValue)) {
    return `"${stringValue.replace(/"/g, '""')}"`;
  }

  return stringValue;
}

/**
 * Convert query results into a CSV string.
 * Accepts an array of row objects, a single object, or an array of primitives.
 * Returns an empty string when there is nothing tabular to export.
 */
export function resultsToCsv(result) {
  if (result === null || result === undefined) {
    return '';
  }

  const rows = Array.isArray(result) ? result : [result];

  if (rows.length === 0) {
    return '';
  }

  const isObjectRow = (row) =>
    row !== null && typeof row === 'object' && !Array.isArray(row);
  const hasObjectRows = rows.some(isObjectRow);

  if (!hasObjectRows) {
    // Array (or single) of primitives -> single "value" column.
    const header = 'value';
    const body = rows.map((row) => escapeCsvValue(row)).join('\r\n');
    return `${header}\r\n${body}`;
  }

  const columns = [];
  const seen = new Set();
  rows.forEach((row) => {
    if (isObjectRow(row)) {
      Object.keys(row).forEach((key) => {
        if (!seen.has(key)) {
          seen.add(key);
          columns.push(key);
        }
      });
    }
  });

  const header = columns.map(escapeCsvValue).join(',');
  const body = rows
    .map((row) =>
      columns
        .map((column) => escapeCsvValue(isObjectRow(row) ? row[column] : row))
        .join(',')
    )
    .join('\r\n');

  return `${header}\r\n${body}`;
}

/** Whether the given result can be meaningfully exported as CSV. */
export function canExportCsv(result) {
  if (Array.isArray(result)) {
    return result.length > 0;
  }

  return result !== null && result !== undefined && typeof result === 'object';
}

/** Trigger a browser download of the given CSV text. */
export function downloadCsv(filename, csvText) {
  if (typeof document === 'undefined') {
    return;
  }

  // Prepend a BOM so Excel opens UTF-8 content correctly.
  const blob = new Blob([`﻿${csvText}`], {
    type: 'text/csv;charset=utf-8;'
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}
