import { useTranslation } from 'react-i18next';

function Sidebar({
  databaseNames,
  isLoadingDatabaseDetails = false,
  selectedDatabase,
  selectedDatabaseName,
  selectedTableName,
  onSelectDatabase,
  onSelectTable
}) {
  const { t } = useTranslation('translation');
  const shouldShowTableLoading =
    !!selectedDatabaseName &&
    (isLoadingDatabaseDetails || selectedDatabase?.Name !== selectedDatabaseName);

  return (
    <div className="sidebar">
      <div className="sidebar__brand">
        <img src="/assets/database-icon.png" alt="RestDb" />
        <div>
          <p className="eyebrow">RestDb</p>
          <h2>{t('dashboard.workspace')}</h2>
        </div>
      </div>

      <div className="sidebar__section">
        <p className="sidebar__heading">{t('dashboard.databases')}</p>
        <div className="sidebar__list">
          {(databaseNames || []).map((databaseName) => (
            <button
              className={databaseName === selectedDatabaseName ? 'nav-pill nav-pill--active' : 'nav-pill'}
              key={databaseName}
              onClick={() => onSelectDatabase(databaseName)}
              title={t('dashboard.databaseNavTooltip', {
                databaseName,
                defaultValue: `Open ${databaseName} and load its tables in the workspace.`
              })}
              type="button"
            >
              <span>{databaseName}</span>
            </button>
          ))}
        </div>
      </div>

      {selectedDatabaseName ? (
        <div className="sidebar__section" key={`${selectedDatabaseName}-tables`}>
          <p className="sidebar__heading">{t('dashboard.tables')}</p>
          {shouldShowTableLoading ? (
            <div className="sidebar__empty">
              <div className="loading-spinner" />
              <p>{t('dashboard.loadingTables')}</p>
            </div>
          ) : (
            <div className="sidebar__table-list">
              {(selectedDatabase?.TableNames || []).map((tableName) => (
                <button
                  className={tableName === selectedTableName ? 'table-link table-link--active' : 'table-link'}
                  key={tableName}
                  onClick={() => onSelectTable(tableName)}
                  title={t('dashboard.tableNavTooltip', {
                    tableName,
                    defaultValue: `Select ${tableName} to inspect its schema, rows, and context.`
                  })}
                  type="button"
                >
                  <span>{tableName}</span>
                </button>
              ))}
              {(selectedDatabase?.TableNames || []).length < 1 ? (
                <div className="sidebar__empty">
                  <p>{t('dashboard.emptyTables')}</p>
                </div>
              ) : null}
            </div>
          )}
        </div>
      ) : null}
    </div>
  );
}

export default Sidebar;
