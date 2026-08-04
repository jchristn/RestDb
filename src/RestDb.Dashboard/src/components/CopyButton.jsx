import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';
import { copyTextToClipboard } from '../utils/clipboard';
import CheckIcon from './CheckIcon';
import CopyIcon from './CopyIcon';

const COPIED_RESET_MS = 1600;

/**
 * A copy-to-clipboard control that works across secure and non-secure origins
 * (http, https, localhost, and remote hosts) and briefly turns into a green
 * checkmark to confirm a successful copy.
 */
function CopyButton({
  value,
  label,
  copiedLabel,
  className = 'button button-secondary',
  disabled = false,
  title
}) {
  const { t } = useTranslation('translation');
  const { addToast } = useApp();
  const [isCopied, setIsCopied] = useState(false);
  const resetTimer = useRef(null);

  const resolvedLabel = label ?? t('common.copy', { defaultValue: 'Copy' });
  const resolvedCopiedLabel = copiedLabel ?? t('common.copied', { defaultValue: 'Copied' });

  useEffect(() => {
    return () => {
      if (resetTimer.current) {
        window.clearTimeout(resetTimer.current);
      }
    };
  }, []);

  async function handleCopy() {
    try {
      await copyTextToClipboard(typeof value === 'function' ? value() : value ?? '');
      setIsCopied(true);

      if (resetTimer.current) {
        window.clearTimeout(resetTimer.current);
      }

      resetTimer.current = window.setTimeout(() => {
        setIsCopied(false);
        resetTimer.current = null;
      }, COPIED_RESET_MS);
    } catch (copyError) {
      addToast({
        tone: 'danger',
        title: t('toast.copyFailedTitle'),
        message: copyError?.message || t('toast.copyFailedMessage')
      });
    }
  }

  return (
    <button
      className={`copy-button${isCopied ? ' copy-button--copied' : ''} ${className}`.trim()}
      disabled={disabled}
      onClick={handleCopy}
      title={title || resolvedLabel}
      type="button"
    >
      {isCopied ? <CheckIcon /> : <CopyIcon />}
      <span>{isCopied ? resolvedCopiedLabel : resolvedLabel}</span>
    </button>
  );
}

export default CopyButton;
