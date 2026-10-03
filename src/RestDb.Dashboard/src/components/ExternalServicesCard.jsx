import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import CollapsibleSection from './CollapsibleSection';
import CopyButton from './CopyButton';
import RefreshIcon from './RefreshIcon';

const PROBE_TIMEOUT_MS = 3000;

function resolveBaseUrl(override, port) {
  if (override) {
    return override.replace(/\/+$/, '');
  }

  const hostname = typeof window !== 'undefined' && window.location.hostname ? window.location.hostname : 'localhost';
  const host = hostname.includes(':') ? `[${hostname}]` : hostname;
  return `http://${host}:${port}`;
}

function buildServices(t) {
  const env = import.meta.env || {};

  return [
    {
      id: 'grafana',
      name: 'Grafana',
      description: t('externalServices.grafanaDescription'),
      url: resolveBaseUrl(env.VITE_GRAFANA_URL, 3000),
      probePath: '/api/health',
      credentials: env.VITE_GRAFANA_CREDENTIALS || 'admin / admin'
    },
    {
      id: 'prometheus',
      name: 'Prometheus',
      description: t('externalServices.prometheusDescription'),
      url: resolveBaseUrl(env.VITE_PROMETHEUS_URL, 9090),
      probePath: '/-/healthy',
      credentials: null
    },
    {
      id: 'tempo',
      name: 'Tempo',
      description: t('externalServices.tempoDescription'),
      url: resolveBaseUrl(env.VITE_TEMPO_URL, 3200),
      probePath: '/ready',
      credentials: null
    },
    {
      id: 'mcp',
      name: 'MCP',
      description: t('externalServices.mcpDescription'),
      url: `${resolveBaseUrl(env.VITE_MCP_URL, 8010)}/mcp`,
      probeUrl: `${resolveBaseUrl(env.VITE_MCP_URL, 8010)}/`,
      credentials: null
    }
  ];
}

async function probe(url) {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);

  try {
    // no-cors: the response is opaque, but a resolved fetch proves the service answered.
    await fetch(url, { mode: 'no-cors', cache: 'no-store', signal: controller.signal });
    return 'reachable';
  } catch {
    return 'unreachable';
  } finally {
    window.clearTimeout(timer);
  }
}

/**
 * Links operators to Grafana and the other tools bundled with the Docker stack, showing each
 * service's browser-reachable URL, default credentials, and whether it answers from this browser.
 */
function ExternalServicesCard() {
  const { t } = useTranslation('translation');
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [statuses, setStatuses] = useState({});
  const services = useMemo(() => buildServices(t), [t]);

  const checkAll = useCallback(async () => {
    setStatuses(Object.fromEntries(services.map((service) => [service.id, 'checking'])));
    const results = await Promise.all(
      services.map(async (service) => [service.id, await probe(service.probeUrl || `${service.url}${service.probePath}`)])
    );
    setStatuses(Object.fromEntries(results));
  }, [services]);

  useEffect(() => {
    checkAll();
  }, [checkAll]);

  return (
    <CollapsibleSection
      actions={
        <div className="panel-actions">
          <button
            aria-label={t('externalServices.recheck')}
            className="icon-button"
            onClick={checkAll}
            title={t('externalServices.recheckTooltip')}
            type="button"
          >
            <RefreshIcon />
          </button>
        </div>
      }
      isCollapsed={isCollapsed}
      onToggle={() => setIsCollapsed((current) => !current)}
      subtitle={t('externalServices.subtitle')}
      title={t('externalServices.title')}
    >
      <p className="helper-copy">{t('externalServices.helper')}</p>
      <div className="external-services">
        <div className="external-services__row external-services__row--head" role="row">
          <span role="columnheader">{t('externalServices.service')}</span>
          <span role="columnheader">{t('externalServices.url')}</span>
          <span role="columnheader">{t('externalServices.credentials')}</span>
          <span role="columnheader">{t('externalServices.status')}</span>
        </div>
        {services.map((service) => {
          const status = statuses[service.id] || 'checking';
          return (
            <div className="external-services__row" key={service.id} role="row">
              <div className="external-services__service">
                <strong>{service.name}</strong>
                <span>{service.description}</span>
              </div>
              <div className="external-services__url">
                <a className="mono-token" href={service.url} rel="noreferrer" target="_blank" title={t('externalServices.open')}>
                  {service.url}
                </a>
                <CopyButton
                  className="button button-ghost button-small"
                  label={t('externalServices.copyUrl')}
                  value={service.url}
                />
              </div>
              <div>
                {service.credentials ? (
                  <code className="mono-token">{service.credentials}</code>
                ) : (
                  <span className="external-services__muted">{t('externalServices.none')}</span>
                )}
              </div>
              <div>
                <span
                  className={`status-badge status-badge--${status}`}
                  title={status === 'unreachable' ? t('externalServices.unreachableHint') : undefined}
                >
                  {t(`externalServices.${status}`)}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </CollapsibleSection>
  );
}

export default ExternalServicesCard;
