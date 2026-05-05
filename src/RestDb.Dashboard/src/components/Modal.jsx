import { createPortal } from 'react-dom';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

function Modal({
  children,
  isOpen,
  onClose,
  title,
  actions = null,
  size = 'medium'
}) {
  const { t } = useTranslation('translation');

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        onClose?.();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  return createPortal(
    <div
      className="modal-backdrop"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose?.();
        }
      }}
      role="presentation"
    >
      <div
        aria-labelledby="modal-title"
        aria-modal="true"
        className={`modal-card modal-card--${size}`}
        role="dialog"
      >
        <div className="modal-card__header">
          <div>
            <p className="eyebrow">{t('app.dashboardName')}</p>
            <h2 id="modal-title">{title}</h2>
          </div>
        </div>
        <div className="modal-card__content">{children}</div>
        {actions ? <div className="modal-card__actions">{actions}</div> : null}
      </div>
    </div>,
    document.body
  );
}

export default Modal;
