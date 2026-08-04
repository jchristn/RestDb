import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import CopyButton from '../components/CopyButton';
import GithubIcon from '../components/GithubIcon';
import LanguageSelector from '../components/LanguageSelector';
import Shell from '../components/Shell';
import ThemeIcon from '../components/ThemeIcon';
import { useApp } from '../context/AppContext';
import { useAuth } from '../context/AuthContext';
import { canExportCsv, downloadCsv, resultsToCsv } from '../utils/csv';

const REPO_URL = 'https://github.com/jchristn/restdb';

function DatabaseIcon() {
  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M12 4c3.9 0 7 1.1 7 2.5S15.9 9 12 9 5 7.9 5 6.5 8.1 4 12 4Z"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
      <path
        d="M5 6.5v11C5 18.9 8.1 20 12 20s7-1.1 7-2.5v-11M5 12c0 1.4 3.1 2.5 7 2.5s7-1.1 7-2.5"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function BackIcon() {
  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M14 6l-6 6 6 6"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function LogoutIcon() {
  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M10 4H5a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h5"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
      <path
        d="M14 16l4-4-4-4M9 12h9"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function formatCell(value, t) {
  if (value === null || value === undefined || value === '') {
    return <span className="cell-empty">{t('common.emptyValue', { defaultValue: '-' })}</span>;
  }

  if (typeof value === 'boolean') {
    return value ? t('common.true', { defaultValue: 'true' }) : t('common.false', { defaultValue: 'false' });
  }

  if (typeof value === 'object') {
    return <code>{JSON.stringify(value)}</code>;
  }

  return String(value);
}

function QueryView() {
  const { t } = useTranslation('translation');
  const navigate = useNavigate();
  const { addToast, theme, toggleTheme } = useApp();
  const { apiClient, logout, session } = useAuth();

  const [databaseNames, setDatabaseNames] = useState([]);
  const [selectedDatabaseName, setSelectedDatabaseName] = useState('');
  const [query, setQuery] = useState('');
  const [result, setResult] = useState(null);
  const [hasResult, setHasResult] = useState(false);
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    document.title = t('query.pageTitle', { defaultValue: 'RestDb · Query console' });
  }, [t]);

  useEffect(() => {
    let cancelled = false;

    async function loadDatabaseNames() {
      try {
        const names = await apiClient.getDatabases();
        if (cancelled) {
          return;
        }

        const normalized = Array.isArray(names) ? names : [];
        setDatabaseNames(normalized);
        setSelectedDatabaseName((current) =>
          current && normalized.includes(current) ? current : normalized[0] || ''
        );
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('toast.databaseListFailedTitle'),
            message: loadError?.message || t('toast.databaseListFailedMessage')
          });
        }
      }
    }

    loadDatabaseNames();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, t]);

  const jsonText = useMemo(
    () => (hasResult ? JSON.stringify(result, null, 2) : ''),
    [hasResult, result]
  );

  const tableRows = useMemo(() => {
    if (!hasResult) {
      return [];
    }

    if (Array.isArray(result)) {
      return result;
    }

    if (result !== null && typeof result === 'object') {
      return [result];
    }

    return [];
  }, [hasResult, result]);

  const columns = useMemo(() => {
    const isObjectRow = (row) => row !== null && typeof row === 'object' && !Array.isArray(row);
    const hasObjectRows = tableRows.some(isObjectRow);

    if (!hasObjectRows) {
      return tableRows.length > 0 ? ['value'] : [];
    }

    const seen = new Set();
    const ordered = [];
    tableRows.forEach((row) => {
      if (isObjectRow(row)) {
        Object.keys(row).forEach((key) => {
          if (!seen.has(key)) {
            seen.add(key);
            ordered.push(key);
          }
        });
      }
    });

    return ordered;
  }, [tableRows]);

  const rowCount = Array.isArray(result) ? result.length : hasResult ? 1 : 0;
  const canDownloadCsv = hasResult && canExportCsv(result);

  async function handleExecute() {
    if (!selectedDatabaseName || !query.trim() || isRunning) {
      return;
    }

    setIsRunning(true);
    setError('');

    try {
      const nextResult = await apiClient.runRawQuery(selectedDatabaseName, query);
      setResult(nextResult);
      setHasResult(true);
      addToast({
        tone: 'success',
        title: t('toast.queryExecutedTitle'),
        message: t('toast.queryExecutedMessage')
      });
    } catch (executeError) {
      setResult(null);
      setHasResult(false);
      setError(executeError?.message || t('rawSql.queryFailed'));
    } finally {
      setIsRunning(false);
    }
  }

  function handleKeyDown(event) {
    if ((event.metaKey || event.ctrlKey) && event.key === 'Enter') {
      event.preventDefault();
      handleExecute();
    }
  }

  function handleDownloadCsv() {
    const csvText = resultsToCsv(result);
    if (!csvText) {
      return;
    }

    const safeName = (selectedDatabaseName || 'query').replace(/[^a-z0-9_-]+/gi, '_');
    downloadCsv(`${safeName}-results.csv`, csvText);
  }

  function handleLogout() {
    logout();
    navigate('/', { replace: true });
  }

  const sidebar = (
    <div className="sidebar">
      <div className="sidebar__brand">
        <img src="/assets/database-icon.png" alt="RestDb" />
        <div>
          <p className="eyebrow">RestDb</p>
          <h2>{t('query.consoleShort', { defaultValue: 'Query console' })}</h2>
        </div>
      </div>

      <div className="sidebar__section">
        <p className="sidebar__heading">{t('dashboard.navigation', { defaultValue: 'Navigation' })}</p>
        <div className="sidebar__list">
          <button
            className="nav-pill"
            onClick={() => navigate('/workspace')}
            title={t('query.backToWorkspaceTooltip', {
              defaultValue: 'Return to the workspace to browse tables, schema, and rows.'
            })}
            type="button"
          >
            <span>{t('query.workspaceLink', { defaultValue: 'Workspace' })}</span>
          </button>
          <button className="nav-pill nav-pill--active" type="button">
            <span>{t('query.consoleShort', { defaultValue: 'Query console' })}</span>
          </button>
        </div>
      </div>

      <div className="sidebar__section">
        <p className="sidebar__heading">{t('dashboard.databases')}</p>
        <div className="sidebar__list">
          {databaseNames.map((databaseName) => (
            <button
              className={
                databaseName === selectedDatabaseName ? 'nav-pill nav-pill--active' : 'nav-pill'
              }
              key={databaseName}
              onClick={() => setSelectedDatabaseName(databaseName)}
              title={t('query.selectDatabaseTooltip', {
                databaseName,
                defaultValue: `Run queries against ${databaseName}.`
              })}
              type="button"
            >
              <span>{databaseName}</span>
            </button>
          ))}
          {databaseNames.length < 1 ? (
            <div className="sidebar__empty">
              <p>{t('query.noDatabases', { defaultValue: 'No databases available.' })}</p>
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );

  const topbar = (
    <div className="topbar">
      <div className="topbar__title-block">
        <p className="eyebrow">{t('query.eyebrow', { defaultValue: 'SQL' })}</p>
        <h1>{t('query.title', { defaultValue: 'Query console' })}</h1>
        <p className="helper-copy">{session?.serverUrl}</p>
      </div>

      <div className="topbar__actions">
        <button
          className="button button-secondary"
          onClick={() => navigate('/workspace')}
          title={t('query.backToWorkspaceTooltip', {
            defaultValue: 'Return to the workspace to browse tables, schema, and rows.'
          })}
          type="button"
        >
          <BackIcon />
          <span>{t('query.workspaceLink', { defaultValue: 'Workspace' })}</span>
        </button>
        <LanguageSelector />
        <button
          aria-label={theme === 'dark' ? t('theme.switchToLight') : t('theme.switchToDark')}
          className="icon-button"
          onClick={toggleTheme}
          title={theme === 'dark' ? t('theme.switchToLight') : t('theme.switchToDark')}
          type="button"
        >
          <ThemeIcon theme={theme} />
        </button>
        <a
          aria-label={t('navigation.github')}
          className="icon-button"
          href={REPO_URL}
          rel="noreferrer"
          target="_blank"
          title={t('navigation.github')}
        >
          <GithubIcon />
        </a>
        <button
          aria-label={t('auth.disconnect')}
          className="icon-button"
          onClick={handleLogout}
          title={t('auth.disconnect')}
          type="button"
        >
          <LogoutIcon />
        </button>
      </div>
    </div>
  );

  return (
    <Shell sidebar={sidebar} topbar={topbar}>
      <div className="query-console">
        <section className="query-console__editor">
          <div className="panel-head">
            <div>
              <p className="eyebrow">{t('query.eyebrow', { defaultValue: 'SQL' })}</p>
              <h3>{t('query.editorTitle', { defaultValue: 'Compose query' })}</h3>
            </div>
            <div className="query-console__db-picker">
              <DatabaseIcon />
              <label className="visually-hidden" htmlFor="query-database">
                {t('query.databaseLabel', { defaultValue: 'Database' })}
              </label>
              <select
                id="query-database"
                onChange={(event) => setSelectedDatabaseName(event.target.value)}
                title={t('query.databaseSelectTooltip', {
                  defaultValue: 'Choose the database to run this query against.'
                })}
                value={selectedDatabaseName}
              >
                {databaseNames.length < 1 ? (
                  <option value="">{t('query.noDatabases', { defaultValue: 'No databases available.' })}</option>
                ) : null}
                {databaseNames.map((databaseName) => (
                  <option key={databaseName} value={databaseName}>
                    {databaseName}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <textarea
            className="raw-query-input query-console__input"
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={t('query.placeholder', {
              defaultValue: 'SELECT * FROM my_table LIMIT 100;'
            })}
            rows={10}
            spellCheck={false}
            title={t('query.inputTooltip', {
              defaultValue: 'Write the SQL statement to execute. Press Ctrl/Cmd+Enter to run.'
            })}
            value={query}
          />

          {error ? <div className="inline-error">{error}</div> : null}

          <div className="query-console__actions">
            <span className="query-console__hint">
              {t('query.runHint', { defaultValue: 'Tip: press Ctrl/Cmd + Enter to run.' })}
            </span>
            <button
              className="button button-primary"
              disabled={isRunning || !selectedDatabaseName || !query.trim()}
              onClick={handleExecute}
              title={t('query.runTooltip', {
                defaultValue: 'Execute the SQL statement against the selected database.'
              })}
              type="button"
            >
              {isRunning
                ? t('rawSql.running', { defaultValue: 'Running…' })
                : t('query.run', { defaultValue: 'Run query' })}
            </button>
          </div>
        </section>

        <section className="query-console__results">
          <div className="panel-head">
            <div>
              <p className="eyebrow">{t('rawSql.result', { defaultValue: 'Result' })}</p>
              <h3>
                {hasResult
                  ? t('query.resultCount', {
                      count: rowCount,
                      defaultValue: `${rowCount} row(s)`
                    })
                  : t('query.resultsTitle', { defaultValue: 'Results' })}
              </h3>
            </div>
            <div className="panel-actions">
              <CopyButton
                className="button button-secondary"
                disabled={!hasResult}
                label={t('query.copyJson', { defaultValue: 'Copy JSON' })}
                title={t('query.copyJsonTooltip', {
                  defaultValue: 'Copy the JSON results to the clipboard.'
                })}
                value={jsonText}
              />
              <button
                className="button button-secondary"
                disabled={!canDownloadCsv}
                onClick={handleDownloadCsv}
                title={t('query.downloadCsvTooltip', {
                  defaultValue: 'Download the results as a CSV file.'
                })}
                type="button"
              >
                {t('query.downloadCsv', { defaultValue: 'Download CSV' })}
              </button>
            </div>
          </div>

          {!hasResult ? (
            <div className="empty-panel">
              <h3>{t('query.emptyTitle', { defaultValue: 'No results yet' })}</h3>
              <p>
                {t('query.emptyBody', {
                  defaultValue: 'Select a database, write a query, and run it to see results here.'
                })}
              </p>
            </div>
          ) : columns.length > 0 ? (
            <div className="data-grid query-console__grid">
              <table>
                <thead>
                  <tr>
                    {columns.map((column) => (
                      <th key={column}>{column}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {tableRows.map((row, rowIndex) => {
                    const isObjectRow = row !== null && typeof row === 'object' && !Array.isArray(row);

                    return (
                      <tr key={rowIndex}>
                        {columns.map((column) => {
                          const cellValue =
                            columns.length === 1 && columns[0] === 'value' && !isObjectRow
                              ? row
                              : isObjectRow
                                ? row[column]
                                : undefined;

                          return <td key={column}>{formatCell(cellValue, t)}</td>;
                        })}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : (
            <pre className="query-console__json">{jsonText}</pre>
          )}
        </section>
      </div>
    </Shell>
  );
}

export default QueryView;
