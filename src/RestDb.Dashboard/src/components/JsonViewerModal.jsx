import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';
import { copyTextToClipboard } from '../utils/clipboard';
import CopyIcon from './CopyIcon';
import Modal from './Modal';

function JsonViewerModal({
  isOpen,
  onClose,
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
        <button
          className="button button-secondary"
          onClick={onClose}
          title={t('common.close', { defaultValue: 'Close' })}
          type="button"
        >
          {t('common.close')}
        </button>
      }
      isOpen={isOpen}
      onClose={onClose}
      size="large"
      title={title}
    >
      <div className="json-viewer-modal">
        <div className="json-viewer-modal__actions">
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
        <pre className="json-viewer-pre">{value}</pre>
      </div>
    </Modal>
  );
}

export default JsonViewerModal;
