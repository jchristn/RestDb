export async function copyTextToClipboard(text) {
  const normalizedText = text ?? '';

  if (
    typeof navigator !== 'undefined' &&
    navigator.clipboard?.writeText &&
    typeof window !== 'undefined' &&
    window.isSecureContext
  ) {
    await navigator.clipboard.writeText(normalizedText);
    return true;
  }

  if (typeof document === 'undefined') {
    throw new Error('Clipboard access is unavailable in this environment.');
  }

  const textArea = document.createElement('textarea');
  textArea.value = normalizedText;
  textArea.setAttribute('readonly', 'true');
  textArea.style.position = 'fixed';
  textArea.style.opacity = '0';
  textArea.style.pointerEvents = 'none';
  textArea.style.top = '0';
  textArea.style.left = '0';

  document.body.appendChild(textArea);
  textArea.focus();
  textArea.select();
  textArea.setSelectionRange(0, normalizedText.length);

  try {
    const didCopy = document.execCommand('copy');
    if (!didCopy) {
      throw new Error('The browser rejected the copy request.');
    }

    return true;
  } finally {
    document.body.removeChild(textArea);
  }
}
