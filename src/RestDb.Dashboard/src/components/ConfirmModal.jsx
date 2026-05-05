import { useTranslation } from 'react-i18next';
import Modal from './Modal';

function ConfirmModal({
  body,
  confirmLabel,
  isOpen,
  onClose,
  onConfirm,
  title,
  tone = 'danger'
}) {
  const { t } = useTranslation('translation');

  return (
    <Modal
      actions={
        <>
          <button
            className="button button-secondary"
            onClick={onClose}
            title={t('common.cancelTooltip', { defaultValue: 'Close this confirmation dialog without making changes.' })}
            type="button"
          >
            {t('common.cancel')}
          </button>
          <button
            className={`button ${tone === 'danger' ? 'button-danger' : 'button-primary'}`}
            onClick={onConfirm}
            title={t('common.confirmTooltip', { defaultValue: 'Confirm this action and send the request to RestDb.' })}
            type="button"
          >
            {confirmLabel}
          </button>
        </>
      }
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="small"
    >
      <p className="modal-copy" title={body}>{body}</p>
    </Modal>
  );
}

export default ConfirmModal;
