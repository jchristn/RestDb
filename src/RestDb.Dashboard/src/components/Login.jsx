import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import GithubIcon from './GithubIcon';
import LanguageSelector from './LanguageSelector';
import { useApp } from '../context/AppContext';
import {
  createDefaultServerUrl,
  DEFAULT_API_KEY_HEADER,
  DEFAULT_AUTH_MODE,
  useAuth
} from '../context/AuthContext';

const REPO_URL = 'https://github.com/jchristn/restdb';

function Login() {
  const { t } = useTranslation('translation');
  const navigate = useNavigate();
  const { addToast } = useApp();
  const { login, session } = useAuth();
  const [serverUrl, setServerUrl] = useState(session?.serverUrl || createDefaultServerUrl());
  const [apiKey, setApiKey] = useState(session?.apiKey || '');
  const [apiKeyHeader, setApiKeyHeader] = useState(session?.apiKeyHeader || DEFAULT_API_KEY_HEADER);
  const [authMode, setAuthMode] = useState(session?.authMode || DEFAULT_AUTH_MODE);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    document.title = t('app.loginTitle');
  }, [t]);

  async function handleSubmit(event) {
    event.preventDefault();
    setIsSubmitting(true);
    setError('');

    try {
      await login(serverUrl, apiKey, apiKeyHeader, authMode);
      addToast({
        tone: 'success',
        title: t('toast.connectedTitle'),
        message: t('toast.connectedMessage')
      });
      navigate('/workspace', { replace: true });
    } catch (submitError) {
      const message = submitError?.message || t('auth.loginFailed');
      setError(message);
      addToast({
        tone: 'danger',
        title: t('toast.connectionFailedTitle'),
        message
      });
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-page__atmosphere" />
      <a
        aria-label={t('navigation.github')}
        className="app-corner-link"
        href={REPO_URL}
        rel="noreferrer"
        target="_blank"
        title={t('navigation.github')}
      >
        <GithubIcon />
      </a>
      <div className="login-shell">
        <section className="login-shell__brand">
          <div className="login-logo">
            <img src="/assets/database-icon.png" alt="RestDb" />
          </div>
          <p className="eyebrow">{t('app.dashboardName')}</p>
          <h1>{t('auth.heroTitle')}</h1>
          <p className="hero-copy">{t('auth.heroCopy')}</p>
          <div className="brand-pills">
            <span>{t('auth.brandNativeProviders')}</span>
            <span>{t('auth.brandModalSafety')}</span>
            <span>{t('auth.brandDockerReady')}</span>
          </div>
        </section>

        <section className="login-shell__card">
          <div className="card-head">
            <div>
              <p className="eyebrow">{t('auth.connection')}</p>
              <h2>{t('auth.openWorkspace')}</h2>
            </div>
            <LanguageSelector />
          </div>

          <form className="login-form" onSubmit={handleSubmit}>
            <label title={t('auth.serverUrlTooltip', { defaultValue: 'Enter the full RestDb server URL, including protocol and port.' })}>
              <span>{t('auth.serverUrl')}</span>
              <input
                aria-label={t('auth.serverUrl')}
                onChange={(event) => setServerUrl(event.target.value)}
                placeholder="http://localhost:8000"
                required
                title={t('auth.serverUrlTooltip', { defaultValue: 'Enter the full RestDb server URL, including protocol and port.' })}
                type="url"
                value={serverUrl}
              />
            </label>

            <label title={t('auth.apiKeyTooltip', { defaultValue: 'Enter the RestDb API key if the server requires authentication.' })}>
              <span>{t('auth.apiKey')}</span>
              <input
                aria-label={t('auth.apiKey')}
                onChange={(event) => setApiKey(event.target.value)}
                placeholder={t('auth.apiKeyPlaceholder')}
                title={t('auth.apiKeyTooltip', { defaultValue: 'Enter the RestDb API key if the server requires authentication.' })}
                type="password"
                value={apiKey}
              />
            </label>

            <details className="advanced-panel" title={t('auth.advancedOptionsTooltip', { defaultValue: 'Adjust header and bearer-token settings for servers that use non-default authentication.' })}>
              <summary title={t('auth.advancedOptionsTooltip', { defaultValue: 'Adjust header and bearer-token settings for servers that use non-default authentication.' })}>{t('auth.advancedOptions')}</summary>
              <label title={t('auth.authenticationModeTooltip', { defaultValue: 'Choose whether the API key is sent in a custom header or as a bearer token.' })}>
                <span>{t('auth.authenticationMode')}</span>
                <select
                  aria-label={t('auth.authenticationMode')}
                  onChange={(event) => setAuthMode(event.target.value)}
                  title={t('auth.authenticationModeTooltip', { defaultValue: 'Choose whether the API key is sent in a custom header or as a bearer token.' })}
                  value={authMode}
                >
                  <option value="header">{t('auth.authenticationModeHeader')}</option>
                  <option value="bearer">{t('auth.authenticationModeBearer')}</option>
                </select>
              </label>
              {authMode === 'header' ? (
                <label title={t('auth.apiKeyHeaderTooltip', { defaultValue: 'Specify the header name RestDb should read when header-based authentication is enabled.' })}>
                  <span>{t('auth.apiKeyHeader')}</span>
                  <input
                    aria-label={t('auth.apiKeyHeader')}
                    onChange={(event) => setApiKeyHeader(event.target.value)}
                    placeholder="x-api-key"
                    title={t('auth.apiKeyHeaderTooltip', { defaultValue: 'Specify the header name RestDb should read when header-based authentication is enabled.' })}
                    value={apiKeyHeader}
                  />
                </label>
              ) : null}
            </details>

            <p className="helper-copy">
              {authMode === 'bearer'
                ? t('auth.bearerHelp')
                : t('auth.help')}
            </p>
            {error ? <div className="inline-error">{error}</div> : null}

            <button
              className="button button-primary button-block"
              disabled={isSubmitting}
              title={t('auth.connectTooltip', { defaultValue: 'Connect to the RestDb server using the values from this form.' })}
              type="submit"
            >
              {isSubmitting ? t('auth.connecting') : t('auth.connect')}
            </button>
          </form>
        </section>
      </div>
    </div>
  );
}

export default Login;
