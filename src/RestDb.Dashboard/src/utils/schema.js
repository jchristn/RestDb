export function getPrimaryKeyColumn(table) {
  if (!table?.Columns) {
    return null;
  }

  return (
    table.Columns.find((column) => column.PrimaryKey) ||
    table.Columns.find((column) => column.Name === table.PrimaryKey) ||
    null
  );
}

export const FILTER_OPERATORS = [
  { value: 'Equals', labelKey: 'filters.operatorEquals', requiresValue: true },
  { value: 'NotEquals', labelKey: 'filters.operatorNotEquals', requiresValue: true },
  { value: 'Contains', labelKey: 'filters.operatorLike', requiresValue: true },
  { value: 'ContainsNot', labelKey: 'filters.operatorNotLike', requiresValue: true },
  { value: 'StartsWith', labelKey: 'filters.operatorStartsWith', requiresValue: true },
  { value: 'EndsWith', labelKey: 'filters.operatorEndsWith', requiresValue: true },
  { value: 'GreaterThan', labelKey: 'filters.operatorGreaterThan', requiresValue: true },
  { value: 'GreaterThanOrEqualTo', labelKey: 'filters.operatorGreaterThanOrEqualTo', requiresValue: true },
  { value: 'LessThan', labelKey: 'filters.operatorLessThan', requiresValue: true },
  { value: 'LessThanOrEqualTo', labelKey: 'filters.operatorLessThanOrEqualTo', requiresValue: true },
  { value: 'IsNull', labelKey: 'filters.operatorIsNull', requiresValue: false },
  { value: 'IsNotNull', labelKey: 'filters.operatorIsNotNull', requiresValue: false }
];

export function getColumnKind(column) {
  const normalizedType = (column?.Type || '').toLowerCase();

  if (
    normalizedType.includes('int') ||
    normalizedType.includes('decimal') ||
    normalizedType.includes('numeric') ||
    normalizedType.includes('float') ||
    normalizedType.includes('double') ||
    normalizedType.includes('real')
  ) {
    return 'number';
  }

  if (
    normalizedType.includes('bool') ||
    normalizedType.includes('bit')
  ) {
    return 'boolean';
  }

  if (
    normalizedType.includes('date') ||
    normalizedType.includes('time')
  ) {
    return 'datetime';
  }

  return 'text';
}

export function formatColumnType(column) {
  if (!column) {
    return 'unknown';
  }

  if (column.MaxLength) {
    return `${column.Type} (${column.MaxLength})`;
  }

  return column.Type;
}

export function createDraftFromRow(table, row = null) {
  const draft = {};

  (table?.Columns || []).forEach((column) => {
    const sourceValue = row?.[column.Name];
    draft[column.Name] =
      sourceValue === null || sourceValue === undefined ? '' : String(sourceValue);
  });

  return draft;
}

export function buildPayloadFromDraft(table, draft, mode = 'insert') {
  const payload = {};
  const primaryKeyColumn = getPrimaryKeyColumn(table);

  (table?.Columns || []).forEach((column) => {
    const rawValue = draft[column.Name];
    const value = typeof rawValue === 'string' ? rawValue.trim() : rawValue;
    const columnKind = getColumnKind(column);

    if (value === '' || value === undefined) {
      if (mode === 'insert' && primaryKeyColumn?.Name === column.Name) {
        return;
      }

      if (column.Nullable) {
        payload[column.Name] = null;
      }

      return;
    }

    if (columnKind === 'number') {
      payload[column.Name] = Number(value);
      return;
    }

    if (columnKind === 'boolean') {
      payload[column.Name] = value === true || value === 'true' || value === '1';
      return;
    }

    payload[column.Name] = value;
  });

  return payload;
}

export function buildFilterObject(filters) {
  const filterObject = {};

  (filters || []).forEach((filter) => {
    if (!filter?.column || filter.value === undefined || filter.value === null) {
      return;
    }

    const trimmedValue = String(filter.value).trim();
    if (!trimmedValue) {
      return;
    }

    filterObject[filter.column] = trimmedValue;
  });

  return filterObject;
}

export function buildFilterExpression(filters, columns) {
  const expressions = (filters || [])
    .map((filter) => buildSingleFilterExpression(filter, columns))
    .filter(Boolean);

  if (expressions.length < 1) {
    return null;
  }

  return expressions.slice(1).reduce(
    (current, next) => ({
      Left: next,
      Operator: 'And',
      Right: current
    }),
    expressions[0]
  );
}

export function filterOperatorRequiresValue(operator) {
  const match = FILTER_OPERATORS.find((entry) => entry.value === operator);
  return match ? match.requiresValue : true;
}

function buildSingleFilterExpression(filter, columns) {
  if (!filter?.column) {
    return null;
  }

  const operator = filter.operator || 'Equals';
  const requiresValue = filterOperatorRequiresValue(operator);
  const column = (columns || []).find((entry) => entry.Name === filter.column);

  if (!column) {
    return null;
  }

  if (!requiresValue) {
    return {
      Left: filter.column,
      Operator: operator
    };
  }

  const trimmedValue = String(filter.value ?? '').trim();
  if (!trimmedValue) {
    return null;
  }

  return {
    Left: filter.column,
    Operator: operator,
    Right: normalizeFilterValue(column, trimmedValue)
  };
}

function normalizeFilterValue(column, rawValue) {
  const kind = getColumnKind(column);

  if (kind === 'number') {
    const numericValue = Number(rawValue);
    return Number.isFinite(numericValue) ? numericValue : rawValue;
  }

  if (kind === 'boolean') {
    return rawValue === true || rawValue === 'true' || rawValue === '1';
  }

  return rawValue;
}

export function validateDraft(table, draft, mode = 'insert', t = null) {
  const errors = {};
  const primaryKeyColumn = getPrimaryKeyColumn(table);

  (table?.Columns || []).forEach((column) => {
    const value = typeof draft[column.Name] === 'string'
      ? draft[column.Name].trim()
      : draft[column.Name];

    if (mode === 'insert' && primaryKeyColumn?.Name === column.Name && !value) {
      return;
    }

    if (!column.Nullable && (value === '' || value === undefined || value === null)) {
      errors[column.Name] = t ? t('editor.fieldRequired') : 'This field is required.';
    }
  });

  return errors;
}
