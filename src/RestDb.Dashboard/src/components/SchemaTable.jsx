import { useTranslation } from 'react-i18next';
import { formatColumnType } from '../utils/schema';

function SchemaTable({ table }) {
  const { t } = useTranslation('translation');

  if (!table) {
    return (
      <div className="empty-panel">
        <h3>{t('schema.noTableSelectedTitle')}</h3>
        <p>{t('schema.noTableSelectedBody')}</p>
      </div>
    );
  }

  return (
    <div className="schema-table__content">
      <div className="schema-pills">
        <span title={t('schema.columnCountTooltip', { defaultValue: 'The number of columns exposed by the selected table schema.' })}>{t('schema.columnCount', { count: table.Columns?.length || 0 })}</span>
        <span title={t('schema.primaryKeyTooltip', { defaultValue: 'The primary key column RestDb uses for row-level update and delete operations.' })}>
          {t('schema.primaryKeyLabel')}: {table.PrimaryKey || t('common.none')}
        </span>
      </div>
      <table>
        <thead>
          <tr>
            <th title={t('schema.columnHeaderTooltip', { defaultValue: 'The physical column name in the selected table.' })}>{t('schema.column')}</th>
            <th title={t('schema.typeHeaderTooltip', { defaultValue: 'The provider-specific data type for this column.' })}>{t('schema.type')}</th>
            <th title={t('schema.nullableHeaderTooltip', { defaultValue: 'Shows whether the column accepts NULL values.' })}>{t('schema.nullable')}</th>
            <th title={t('schema.keyHeaderTooltip', { defaultValue: 'Indicates whether the column participates in the table primary key.' })}>{t('schema.key')}</th>
          </tr>
        </thead>
        <tbody>
          {(table.Columns || []).map((column) => (
            <tr key={column.Name}>
              <td title={column.Name}>{column.Name}</td>
              <td title={formatColumnType(column)}>{formatColumnType(column)}</td>
              <td title={column.Nullable ? t('common.yes') : t('common.no')}>{column.Nullable ? t('common.yes') : t('common.no')}</td>
              <td title={column.PrimaryKey || column.Name === table.PrimaryKey ? t('schema.primaryKey') : t('common.emptyValue')}>
                {column.PrimaryKey || column.Name === table.PrimaryKey
                  ? t('schema.primaryKey')
                  : t('common.emptyValue')}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default SchemaTable;
