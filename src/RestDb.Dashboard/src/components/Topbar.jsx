import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext';
import GithubIcon from './GithubIcon';
import LanguageSelector from './LanguageSelector';
import ThemeIcon from './ThemeIcon';

const REPO_URL = 'https://github.com/jchristn/restdb';

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
        d="M14 16l4-4-4-4"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
      <path
        d="M9 12h9"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function SettingsIcon() {
  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M12 8.5a3.5 3.5 0 1 0 0 7a3.5 3.5 0 0 0 0-7Z"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
      <path
        d="m19.4 15l1.1 1.9l-2 3.4l-2.2-.5a7.8 7.8 0 0 1-1.7 1l-.5 2.2H9.9l-.5-2.2a7.8 7.8 0 0 1-1.7-1l-2.2.5l-2-3.4L4.6 15a8 8 0 0 1 0-2l-1.1-1.9l2-3.4l2.2.5c.5-.4 1.1-.7 1.7-1l.5-2.2h4.2l.5 2.2c.6.3 1.2.6 1.7 1l2.2-.5l2 3.4L19.4 13a8 8 0 0 1 0 2Z"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.5"
      />
    </svg>
  );
}

function ContextIcon() {
  return (
    <svg aria-hidden="true" className="icon" viewBox="0 0 24 24">
      <path
        d="M6 4.5h11a2 2 0 0 1 2 2v12.8a.7.7 0 0 1-1.1.6l-1.8-1.2l-1.8 1.2a.7.7 0 0 1-.8 0l-1.5-1l-1.5 1a.7.7 0 0 1-.8 0L8 18.8l-1.8 1.2a.7.7 0 0 1-1.1-.6V6.5a2 2 0 0 1 2-2Z"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
      <path
        d="M8.5 9h7M8.5 12h7M8.5 15h4.5"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function Topbar({
  onOpenContextDocument,
  onOpenServerSettings,
  onLogout,
  selectedDatabase,
  selectedTable,
  session
}) {
  const { t } = useTranslation('translation');
  const { theme, toggleTheme } = useApp();

  return (
    <div className="topbar">
      <div className="topbar__title-block">
        <p className="eyebrow">{t('dashboard.workspace')}</p>
        <h1>
          {selectedTable?.Name
            ? `${selectedDatabase?.Name} / ${selectedTable.Name}`
            : selectedDatabase?.Name || t('dashboard.chooseDatabase')}
        </h1>
        <p className="helper-copy">{session?.serverUrl}</p>
      </div>

      <div className="topbar__actions">
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
        <button
          aria-label={t('dashboard.contextDocument', { defaultValue: 'Context document' })}
          className="icon-button"
          onClick={onOpenContextDocument}
          title={t('dashboard.contextDocument', { defaultValue: 'Context document' })}
          type="button"
        >
          <ContextIcon />
        </button>
        <button
          aria-label={t('dashboard.serverSettings', { defaultValue: 'Server settings' })}
          className="icon-button"
          onClick={onOpenServerSettings}
          title={t('dashboard.serverSettings', { defaultValue: 'Server settings' })}
          type="button"
        >
          <SettingsIcon />
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
          onClick={onLogout}
          title={t('auth.disconnect')}
          type="button"
        >
          <LogoutIcon />
        </button>
      </div>
    </div>
  );
}

export default Topbar;
