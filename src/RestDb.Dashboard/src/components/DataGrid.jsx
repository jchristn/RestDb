import { createPortal } from 'react-dom';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '../i18n/formatters';
import JsonViewerModal from './JsonViewerModal';
import MoreIcon from './MoreIcon';

const MENU_WIDTH = 180;
const MENU_HEIGHT = 124;
const MENU_OFFSET = 8;

function DataGrid({
  onDeleteRow,
  onEditRow,
  onSelectRow,
  rows,
  selectedRowKey,
  table
}) {
  const { t } = useTranslation('translation');
  const menuRef = useRef(null);
  const [menuState, setMenuState] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [jsonText, setJsonText] = useState('');

  const visibleColumns = useMemo(
    () => {
      const schemaColumns = (table?.Columns || []).map((column) => column.Name);
      const extraColumns = Object.keys(rows?.[0] || {}).filter(
        (columnName) => !(table?.Columns || []).some((column) => column.Name === columnName)
      );
      const rowNumberColumn = extraColumns.find((columnName) => columnName.toLowerCase() === '__row_num__');
      const remainingExtraColumns = extraColumns.filter((columnName) => columnName !== rowNumberColumn);

      return [
        ...(rowNumberColumn ? [rowNumberColumn] : []),
        ...schemaColumns,
        ...remainingExtraColumns
      ];
    },
    [rows, table]
  );

  useEffect(() => {
    if (!menuState) {
      return undefined;
    }

    function closeMenu() {
      setMenuState(null);
    }

    function handlePointerDown(event) {
      if (menuRef.current?.contains(event.target)) {
        return;
      }

      closeMenu();
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        closeMenu();
      }
    }

    window.addEventListener('mousedown', handlePointerDown);
    window.addEventListener('resize', closeMenu);
    window.addEventListener('scroll', closeMenu, true);
    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('mousedown', handlePointerDown);
      window.removeEventListener('resize', closeMenu);
      window.removeEventListener('scroll', closeMenu, true);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [menuState]);

  if (!rows || rows.length < 1) {
    return (
      <div className="empty-panel">
        <h3>{t('dashboard.emptyRowsTitle', { defaultValue: 'No rows in view' })}</h3>
        <p>{t('dashboard.emptyRowsBody', { defaultValue: 'Adjust the filters or insert the first record for this table.' })}</p>
      </div>
    );
  }

  function openRowMenu(event, row) {
    event.stopPropagation();

    const rect = event.currentTarget.getBoundingClientRect();
    const nextTop =
      rect.bottom + MENU_OFFSET + MENU_HEIGHT > window.innerHeight
        ? Math.max(12, rect.top - MENU_HEIGHT - MENU_OFFSET)
        : rect.bottom + MENU_OFFSET;
    const nextLeft = Math.min(
      Math.max(12, rect.left + rect.width - MENU_WIDTH),
      Math.max(12, window.innerWidth - MENU_WIDTH - 12)
    );

    setMenuState({
      left: nextLeft,
      row,
      top: nextTop
    });
  }

  function openJsonModal(row) {
    setJsonRow(row);
    setJsonText(JSON.stringify(row, null, 2));
    setMenuState(null);
  }

  return (
    <>
      <div className="data-grid">
        <table>
          <thead>
            <tr>
              {visibleColumns.map((columnName) => (
                <th key={columnName} title={t('schema.columnTooltip', { defaultValue: `Inspect values from the ${columnName} column.` })}>
                  {columnName}
                </th>
              ))}
              <th
                className="action-column"
                title={t('common.actionsTooltip', { defaultValue: 'Open the row action menu to view JSON, edit the row, or delete it.' })}
              >
                {t('common.actions', { defaultValue: 'Actions' })}
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => {
              const rowKey =
                row?.[table?.PrimaryKey] ??
                row?.__row_num__ ??
                `${table?.Name || 'row'}-${index}`;
              const isSelected = String(rowKey) === String(selectedRowKey);

              return (
                <tr
                  className={isSelected ? 'is-selected' : ''}
                  key={rowKey}
                  onClick={() => {
                    onSelectRow(row);
                    onEditRow(row);
                  }}
                  title={t('common.rowTooltip', { defaultValue: 'Click this row to edit it, or use the action menu for JSON view and delete.' })}
                >
                  {visibleColumns.map((columnName) => (
                    <td key={`${rowKey}-${columnName}`} title={String(row[columnName] ?? t('common.emptyValue', { defaultValue: '-' }))}>
                      {formatCellValue(row[columnName], t)}
                    </td>
                  ))}
                  <td className="action-column">
                    <button
                      aria-label={t('common.openRowActions', { defaultValue: 'Open row actions' })}
                      className="icon-button"
                      onClick={(event) => openRowMenu(event, row)}
                      title={t('common.openRowActionsTooltip', { defaultValue: 'Open actions for this row, including JSON view, edit, and delete.' })}
                      type="button"
                    >
                      <MoreIcon />
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {menuState && typeof document !== 'undefined'
        ? createPortal(
            <div
              className="row-menu"
              ref={menuRef}
              role="menu"
              style={{ left: `${menuState.left}px`, top: `${menuState.top}px` }}
            >
              <button
                className="row-menu__item"
                onClick={() => openJsonModal(menuState.row)}
                title={t('common.viewJsonTooltip', { defaultValue: 'Open the full JSON payload for this row and copy it to the clipboard if needed.' })}
                type="button"
              >
                {t('common.viewJson', { defaultValue: 'View JSON' })}
              </button>
              <button
                className="row-menu__item"
                onClick={() => {
                  onEditRow(menuState.row);
                  setMenuState(null);
                }}
                title={t('common.editTooltip', { defaultValue: 'Open the row editor for this record.' })}
                type="button"
              >
                {t('common.edit', { defaultValue: 'Edit' })}
              </button>
              <button
                className="row-menu__item row-menu__item--danger"
                onClick={() => {
                  onDeleteRow(menuState.row);
                  setMenuState(null);
                }}
                title={t('common.deleteTooltip', { defaultValue: 'Delete this row from the table after confirmation.' })}
                type="button"
              >
                {t('common.delete', { defaultValue: 'Delete' })}
              </button>
            </div>,
            document.body
          )
        : null}

      <JsonViewerModal
        isOpen={!!jsonRow}
        onClose={() => {
          setJsonRow(null);
          setJsonText('');
        }}
        title={t('common.viewJson', { defaultValue: 'View JSON' })}
        value={jsonText}
      />
    </>
  );
}

function formatCellValue(value, t) {
  if (value === null || value === undefined || value === '') {
    return <span className="cell-empty">{t('common.emptyValue', { defaultValue: '-' })}</span>;
  }

  if (typeof value === 'boolean') {
    return value ? t('common.true', { defaultValue: 'true' }) : t('common.false', { defaultValue: 'false' });
  }

  if (typeof value === 'string' && looksLikeDate(value)) {
    return formatDateTime(value);
  }

  if (typeof value === 'object') {
    return <code>{JSON.stringify(value)}</code>;
  }

  return String(value);
}

function looksLikeDate(value) {
  return /\d{4}-\d{2}-\d{2}|\d{1,2}\/\d{1,2}\/\d{4}/.test(value);
}

export default DataGrid;
