import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatNumber } from '../i18n/formatters';
import {
  FirstPageIcon,
  LastPageIcon,
  NextPageIcon,
  PreviousPageIcon
} from './PaginationIcons';
import RefreshIcon from './RefreshIcon';

function PaginationBar({
  canGoNext,
  isRefreshing = false,
  onChangePageSize,
  onGoToFirstPage,
  onGoToLastPage,
  onGoToNextPage,
  onGoToPage,
  onGoToPreviousPage,
  onRefresh,
  onSortColumnChange,
  onSortDirectionChange,
  pageIndex,
  pageSize,
  sortColumn,
  sortDirection,
  sortableColumns,
  totalPages,
  totalRecords
}) {
  const { t, i18n } = useTranslation('translation');
  const [pageInput, setPageInput] = useState(String(pageIndex + 1));

  useEffect(() => {
    setPageInput(String(pageIndex + 1));
  }, [pageIndex]);

  function submitSpecificPage() {
    const parsedPage = Number(pageInput);
    if (!Number.isFinite(parsedPage)) {
      setPageInput(String(pageIndex + 1));
      return;
    }

    const clampedPage = Math.min(Math.max(1, parsedPage), Math.max(totalPages, 1));
    setPageInput(String(clampedPage));
    onGoToPage(clampedPage - 1);
  }

  return (
    <div className="pagination-bar">
      <div className="pagination-bar__group">
        <button
          aria-label={t('pagination.firstPage')}
          className="icon-button"
          disabled={pageIndex === 0}
          onClick={onGoToFirstPage}
          title={t('pagination.firstPage')}
          type="button"
        >
          <FirstPageIcon />
        </button>
        <button
          aria-label={t('pagination.previousPage')}
          className="icon-button"
          disabled={pageIndex === 0}
          onClick={onGoToPreviousPage}
          title={t('pagination.previousPage')}
          type="button"
        >
          <PreviousPageIcon />
        </button>
        <div className="pagination-bar__page-jump">
          <span title={t('pagination.pageTooltip', { defaultValue: 'Jump directly to a specific page of records.' })}>{t('pagination.page')}</span>
          <input
            aria-label={t('pagination.pageNumber')}
            inputMode="numeric"
            onBlur={submitSpecificPage}
            onChange={(event) => setPageInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                submitSpecificPage();
              }
            }}
            type="number"
            min="1"
            max={Math.max(totalPages, 1)}
            title={t('pagination.pageNumberTooltip', { defaultValue: 'Type a page number and press Enter to jump directly to that page.' })}
            value={pageInput}
          />
          <span>
            {t('pagination.ofPages', {
              count: totalPages,
              value: formatNumber(totalPages, i18n.resolvedLanguage)
            })}
          </span>
        </div>
        <button
          aria-label={t('pagination.nextPage')}
          className="icon-button"
          disabled={!canGoNext}
          onClick={onGoToNextPage}
          title={t('pagination.nextPage')}
          type="button"
        >
          <NextPageIcon />
        </button>
        <button
          aria-label={t('pagination.lastPage')}
          className="icon-button"
          disabled={!canGoNext}
          onClick={onGoToLastPage}
          title={t('pagination.lastPage')}
          type="button"
        >
          <LastPageIcon />
        </button>
      </div>

      <div className="pagination-bar__group">
        <label className="compact-field">
          <span title={t('pagination.pageSizeTooltip', { defaultValue: 'Choose how many records RestDb should request for each page.' })}>{t('pagination.pageSize')}</span>
          <select onChange={(event) => onChangePageSize(Number(event.target.value))} title={t('pagination.pageSizeTooltip', { defaultValue: 'Choose how many records RestDb should request for each page.' })} value={pageSize}>
            <option value="10">10</option>
            <option value="25">25</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
        </label>

        <label className="compact-field">
          <span title={t('pagination.sortByTooltip', { defaultValue: 'Choose the column RestDb should use when ordering the visible records.' })}>{t('pagination.sortBy')}</span>
          <select onChange={(event) => onSortColumnChange(event.target.value)} title={t('pagination.sortByTooltip', { defaultValue: 'Choose the column RestDb should use when ordering the visible records.' })} value={sortColumn}>
            <option value="">{t('pagination.primaryKeyDefault')}</option>
            {(sortableColumns || []).map((column) => (
              <option key={column.Name} value={column.Name}>
                {column.Name}
              </option>
            ))}
          </select>
        </label>

        <label className="compact-field">
          <span title={t('pagination.directionTooltip', { defaultValue: 'Choose whether records should be sorted from lowest to highest or highest to lowest.' })}>{t('pagination.direction')}</span>
          <select onChange={(event) => onSortDirectionChange(event.target.value)} title={t('pagination.directionTooltip', { defaultValue: 'Choose whether records should be sorted from lowest to highest or highest to lowest.' })} value={sortDirection}>
            <option value="asc">{t('pagination.ascending')}</option>
            <option value="desc">{t('pagination.descending')}</option>
          </select>
        </label>

        <button
          className="icon-button"
          disabled={isRefreshing}
          onClick={onRefresh}
          aria-label={t('common.refresh')}
          title={t('pagination.refreshTooltip', { defaultValue: 'Reload the active table schema, records, and record count from RestDb.' })}
          type="button"
        >
          <RefreshIcon />
        </button>
      </div>

      <div className="pagination-bar__summary">
        <span title={t('pagination.totalRecordsTooltip', { defaultValue: 'The total number of records matching the current filter set.' })}>{t('pagination.totalRecords')}</span>
        <strong>
          {totalRecords === null
            ? t('common.notAvailable')
            : formatNumber(totalRecords, i18n.resolvedLanguage)}
        </strong>
      </div>
    </div>
  );
}

export default PaginationBar;
