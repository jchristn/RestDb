import { getColumnKind } from './schema';

function quoteIdentifier(databaseType, identifier) {
  const normalizedDatabaseType = String(databaseType || '').toLowerCase();

  if (normalizedDatabaseType === 'sqlserver') {
    return `[${String(identifier).replace(/]/g, ']]')}]`;
  }

  if (normalizedDatabaseType === 'mysql') {
    return `\`${String(identifier).replace(/`/g, '``')}\``;
  }

  return `"${String(identifier).replace(/"/g, '""')}"`;
}

function escapeTextLiteral(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

function formatBooleanLiteral(databaseType, value) {
  const normalizedDatabaseType = String(databaseType || '').toLowerCase();
  const booleanValue = value === true || value === 'true' || value === '1';

  if (normalizedDatabaseType === 'postgresql') {
    return booleanValue ? 'TRUE' : 'FALSE';
  }

  return booleanValue ? '1' : '0';
}

function formatLiteral(databaseType, column, rawValue) {
  const kind = getColumnKind(column);

  if (kind === 'number') {
    const numericValue = Number(rawValue);
    if (!Number.isFinite(numericValue)) {
      throw new Error(`Invalid numeric value for ${column?.Name || 'column'}.`);
    }

    return String(numericValue);
  }

  if (kind === 'boolean') {
    return formatBooleanLiteral(databaseType, rawValue);
  }

  return escapeTextLiteral(rawValue);
}

export function buildCountQuery(databaseType, table, columns, filters) {
  const tableIdentifier = quoteIdentifier(databaseType, table);
  const normalizedFilters = Object.entries(filters || {}).filter(([, value]) => value !== '');
  const whereClauses = normalizedFilters.map(([columnName, value]) => {
    const column = (columns || []).find((entry) => entry.Name === columnName);

    if (!column) {
      throw new Error(`Unknown column ${columnName}.`);
    }

    return `${quoteIdentifier(databaseType, columnName)} = ${formatLiteral(databaseType, column, value)}`;
  });

  const whereSql = whereClauses.length > 0 ? ` WHERE ${whereClauses.join(' AND ')}` : '';
  return `SELECT COUNT(*) AS record_count FROM ${tableIdentifier}${whereSql};`;
}

export function buildCountQueryFromExpression(databaseType, table, columns, expression) {
  const tableIdentifier = quoteIdentifier(databaseType, table);
  const whereSql = buildWhereSql(databaseType, columns, expression);
  return `SELECT COUNT(*) AS record_count FROM ${tableIdentifier}${whereSql ? ` WHERE ${whereSql}` : ''};`;
}

function buildWhereSql(databaseType, columns, expression) {
  if (!expression || !expression.Operator) {
    return '';
  }

  if (expression.Operator === 'And' || expression.Operator === 'Or') {
    const leftClause = buildWhereSql(databaseType, columns, expression.Left);
    const rightClause = buildWhereSql(databaseType, columns, expression.Right);

    if (!leftClause) {
      return rightClause;
    }

    if (!rightClause) {
      return leftClause;
    }

    return `(${leftClause} ${expression.Operator.toUpperCase()} ${rightClause})`;
  }

  const column = (columns || []).find((entry) => entry.Name === expression.Left);
  if (!column) {
    throw new Error(`Unknown column ${expression.Left}.`);
  }

  const identifier = quoteIdentifier(databaseType, column.Name);

  switch (expression.Operator) {
    case 'Equals':
      return `${identifier} = ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'NotEquals':
      return `${identifier} <> ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'GreaterThan':
      return `${identifier} > ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'GreaterThanOrEqualTo':
      return `${identifier} >= ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'LessThan':
      return `${identifier} < ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'LessThanOrEqualTo':
      return `${identifier} <= ${formatLiteral(databaseType, column, expression.Right)}`;
    case 'IsNull':
      return `${identifier} IS NULL`;
    case 'IsNotNull':
      return `${identifier} IS NOT NULL`;
    case 'Contains':
      return `${identifier} LIKE ${escapeTextLiteral(`%${expression.Right}%`)}`;
    case 'ContainsNot':
      return `${identifier} NOT LIKE ${escapeTextLiteral(`%${expression.Right}%`)}`;
    case 'StartsWith':
      return `${identifier} LIKE ${escapeTextLiteral(`${expression.Right}%`)}`;
    case 'StartsWithNot':
      return `${identifier} NOT LIKE ${escapeTextLiteral(`${expression.Right}%`)}`;
    case 'EndsWith':
      return `${identifier} LIKE ${escapeTextLiteral(`%${expression.Right}`)}`;
    case 'EndsWithNot':
      return `${identifier} NOT LIKE ${escapeTextLiteral(`%${expression.Right}`)}`;
    case 'In':
      return `${identifier} IN (${formatListLiteral(databaseType, column, expression.Right)})`;
    case 'NotIn':
      return `${identifier} NOT IN (${formatListLiteral(databaseType, column, expression.Right)})`;
    default:
      throw new Error(`Unsupported operator ${expression.Operator}.`);
  }
}

function formatListLiteral(databaseType, column, values) {
  if (!Array.isArray(values) || values.length < 1) {
    throw new Error(`Operator for ${column?.Name || 'column'} requires one or more values.`);
  }

  return values.map((value) => formatLiteral(databaseType, column, value)).join(', ');
}
