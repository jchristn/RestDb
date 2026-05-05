import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';
import { copyTextToClipboard } from '../utils/clipboard';
import CopyIcon from './CopyIcon';
import Modal from './Modal';

function RawQueryModal({
  databaseName,
  initialQuery,
  isOpen,
  onClose,
  onExecute
}) {
  const { t } = useTranslation('translation');
  const { addToast } = useApp();
  const [query, setQuery] = useState('');
  const [result, setResult] = useState(null);
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    setQuery(initialQuery || '');
    setResult(null);
    setError('');
    setIsRunning(false);
  }, [initialQuery, isOpen]);

  async function handleExecute() {
    setIsRunning(true);
    setError('');

    try {
      const nextResult = await onExecute(query);
      setResult(nextResult);
    } catch (executeError) {
      setError(executeError?.message || t('rawSql.queryFailed'));
    } finally {
      setIsRunning(false);
    }
  }

  async function handleCopyResult() {
    try {
      await copyTextToClipboard(JSON.stringify(result, null, 2) || '');
      addToast({
        tone: 'success',
        title: t('toast.copiedTitle'),
        message: t('toast.serverResponseCopied')
      });
    } catch (copyError) {
      addToast({
        tone: 'danger',
        title: t('toast.copyFailedTitle'),
        message: t('toast.copyFailedMessage')
      });
    }
  }

  return (
    <Modal
      actions={
        <>
          <button
            className="button button-secondary"
            onClick={onClose}
            title={t('common.close', { defaultValue: 'Close' })}
            type="button"
          >
            {t('common.close')}
          </button>
          <button
            className="button button-primary"
            disabled={isRunning || !query.trim()}
            onClick={handleExecute}
            title={t('rawSql.runQueryTooltip', { defaultValue: 'Execute the SQL statement against the selected database and inspect the server response.' })}
            type="button"
          >
            {isRunning ? t('rawSql.running') : t('rawSql.runQuery')}
          </button>
        </>
      }
      isOpen={isOpen}
      onClose={onClose}
      title={t('rawSql.title', { databaseName })}
      size="large"
    >
      <div className="raw-query-modal">
        <label title={t('rawSql.statementTooltip', { defaultValue: 'Write the SQL statement you want RestDb to execute against the selected database.' })}>
          <span>{t('rawSql.statement')}</span>
          <textarea
            className="raw-query-input"
            onChange={(event) => setQuery(event.target.value)}
            rows={10}
            title={t('rawSql.statementTooltip', { defaultValue: 'Write the SQL statement you want RestDb to execute against the selected database.' })}
            value={query}
          />
        </label>

        {error ? <div className="inline-error">{error}</div> : null}

        <div className="raw-query-results">
          <div className="panel-head">
            <div>
              <p className="eyebrow">{t('rawSql.result')}</p>
              <h3>{t('rawSql.serverResponse')}</h3>
            </div>
            <button
              aria-label={t('rawSql.copyServerResponse')}
              className="icon-button"
              disabled={result === null}
              onClick={handleCopyResult}
              title={t('rawSql.copyServerResponse')}
              type="button"
            >
              <CopyIcon />
            </button>
          </div>
          <pre>{result === null ? t('rawSql.emptyResult') : JSON.stringify(result, null, 2)}</pre>
        </div>
      </div>
    </Modal>
  );
}

export default RawQueryModal;
