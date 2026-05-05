import { useTranslation } from 'react-i18next';
import ChevronIcon from './ChevronIcon';

function CollapsibleSection({
  actions = null,
  children,
  isCollapsed,
  onToggle,
  subtitle = null,
  title
}) {
  const { t } = useTranslation('translation');

  return (
    <section className="workspace-panel workspace-panel--section">
      <div className="collapsible-header">
        <button
          aria-expanded={!isCollapsed}
          className="collapsible-trigger"
          onClick={onToggle}
          title={t('common.toggleSectionTooltip', {
            defaultValue: `${isCollapsed ? 'Expand' : 'Collapse'} this section to show or hide its details.`
          })}
          type="button"
        >
          <ChevronIcon direction={isCollapsed ? 'right' : 'down'} />
          <div className="collapsible-title-block">
            <p className="eyebrow">{title}</p>
            {subtitle ? <h2>{subtitle}</h2> : null}
          </div>
        </button>
        {actions ? <div className="collapsible-actions">{actions}</div> : null}
      </div>
      {!isCollapsed ? <div className="collapsible-body">{children}</div> : null}
    </section>
  );
}

export default CollapsibleSection;
