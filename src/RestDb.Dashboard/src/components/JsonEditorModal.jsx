import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';
import { copyTextToClipboard } from '../utils/clipboard';
import CopyIcon from './CopyIcon';
import Modal from './Modal';
import RefreshIcon from './RefreshIcon';

function JsonEditorModal({
  error = '',
  helper = '',
  isBusy = false,
  isOpen,
  onChange,
  onClose,
  onReload,
  onSave,
  size = 'large',
  title,
  value
}) {
  const { t } = useTranslation('translation');
  const { addToast } = useApp();

  async function handleCopy() {
    try {
      await copyTextToClipboard(value || '');
      addToast({
        tone: 'success',
        title: t('toast.copiedTitle'),
        message: t('toast.serverResponseCopied')
      });
    } catch {
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
          <button className="button button-ghost" onClick={onClose} title={t('common.close', { defaultValue: 'Close' })} type="button">
            {t('common.close')}
          </button>
          <button
            className="icon-button"
            disabled={isBusy}
            onClick={onReload}
            aria-label={t('common.refresh')}
            title={t('common.refreshTooltip', { defaultValue: 'Reload this JSON document from the running RestDb server.' })}
            type="button"
          >
            <RefreshIcon />
          </button>
          <button
            className="button button-primary"
            disabled={isBusy}
            onClick={onSave}
            title={t('common.saveTooltip', { defaultValue: 'Save this JSON document back to the RestDb server.' })}
            type="button"
          >
            {isBusy ? t('common.saving') : t('common.save')}
          </button>
        </>
      }
      isOpen={isOpen}
      onClose={onClose}
      size={size}
      title={title}
    >
      <div className="json-editor-modal">
        {helper ? <p className="helper-copy">{helper}</p> : null}
        <label title={t('common.jsonEditorTooltip', { defaultValue: 'Edit the full JSON document here. Use refresh to reload the server copy and save to persist your changes.' })}>
          <div className="json-editor-modal__label">
            <span>{title}</span>
            <button
              aria-label={t('common.copyJson', { defaultValue: 'Copy JSON to the clipboard' })}
              className="icon-button"
              onClick={handleCopy}
              title={t('common.copyJson', { defaultValue: 'Copy JSON to the clipboard' })}
              type="button"
            >
              <CopyIcon />
            </button>
          </div>
          <textarea
            className="json-editor-textarea"
            onChange={(event) => onChange(event.target.value)}
            rows={20}
            title={t('common.jsonEditorTooltip', { defaultValue: 'Edit the full JSON document here. Use refresh to reload the server copy and save to persist your changes.' })}
            value={value}
          />
        </label>
        {error ? <div className="inline-error">{error}</div> : null}
      </div>
    </Modal>
  );
}

export default JsonEditorModal;
