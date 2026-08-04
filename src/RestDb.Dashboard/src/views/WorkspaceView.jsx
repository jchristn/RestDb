import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import CollapsibleSection from '../components/CollapsibleSection';
import ConfirmModal from '../components/ConfirmModal';
import DataGrid from '../components/DataGrid';
import JsonEditorModal from '../components/JsonEditorModal';
import PaginationBar from '../components/PaginationBar';
import PlusIcon from '../components/PlusIcon';
import RawQueryModal from '../components/RawQueryModal';
import RefreshIcon from '../components/RefreshIcon';
import RowEditorModal from '../components/RowEditorModal';
import SchemaTable from '../components/SchemaTable';
import Shell from '../components/Shell';
import Sidebar from '../components/Sidebar';
import Topbar from '../components/Topbar';
import TrashIcon from '../components/TrashIcon';
import { useApp } from '../context/AppContext';
import { formatNumber } from '../i18n/formatters';
import { useAuth } from '../context/AuthContext';
import {
  buildFilterExpression,
  buildFilterObject,
  FILTER_OPERATORS,
  filterOperatorRequiresValue,
  getColumnKind,
  getPrimaryKeyColumn
} from '../utils/schema';

const DEFAULT_PAGE_SIZE = 25;
const DEFAULT_FILTER = { id: 1, column: '', operator: 'Equals', value: '' };

function WorkspaceView() {
  const { t, i18n } = useTranslation('translation');
  const navigate = useNavigate();
  const { addToast } = useApp();
  const { apiClient, logout, session } = useAuth();
  const [databaseNames, setDatabaseNames] = useState([]);
  const [selectedDatabaseName, setSelectedDatabaseName] = useState('');
  const [selectedDatabase, setSelectedDatabase] = useState(null);
  const [selectedTableName, setSelectedTableName] = useState('');
  const [selectedTable, setSelectedTable] = useState(null);
  const [rows, setRows] = useState([]);
  const [totalRowCount, setTotalRowCount] = useState(null);
  const [isLoadingDatabaseNames, setIsLoadingDatabaseNames] = useState(true);
  const [isLoadingDatabaseDetails, setIsLoadingDatabaseDetails] = useState(false);
  const [isLoadingRows, setIsLoadingRows] = useState(false);
  const [isLoadingSchema, setIsLoadingSchema] = useState(false);
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [sortColumn, setSortColumn] = useState('');
  const [sortDirection, setSortDirection] = useState('asc');
  const [filters, setFilters] = useState([DEFAULT_FILTER]);
  const [selectedRow, setSelectedRow] = useState(null);
  const [editorState, setEditorState] = useState({ isOpen: false, mode: 'insert', row: null });
  const [confirmState, setConfirmState] = useState({
    body: '',
    confirmLabel: '',
    isOpen: false,
    onConfirm: null,
    title: ''
  });
  const [isRawQueryOpen, setIsRawQueryOpen] = useState(false);
  const [isContextCollapsed, setIsContextCollapsed] = useState(false);
  const [isSchemaCollapsed, setIsSchemaCollapsed] = useState(false);
  const [isRecordsCollapsed, setIsRecordsCollapsed] = useState(false);
  const [catalogReloadToken, setCatalogReloadToken] = useState(0);
  const [databaseReloadToken, setDatabaseReloadToken] = useState(0);
  const [schemaReloadToken, setSchemaReloadToken] = useState(0);
  const [rowsReloadToken, setRowsReloadToken] = useState(0);
  const [countReloadToken, setCountReloadToken] = useState(0);
  const [databaseContext, setDatabaseContext] = useState(null);
  const [databaseContextDraft, setDatabaseContextDraft] = useState('');
  const [tableContext, setTableContext] = useState(null);
  const [tableContextDraft, setTableContextDraft] = useState('');
  const [isLoadingDatabaseContext, setIsLoadingDatabaseContext] = useState(false);
  const [isLoadingTableContext, setIsLoadingTableContext] = useState(false);
  const [isSavingDatabaseContext, setIsSavingDatabaseContext] = useState(false);
  const [isSavingTableContext, setIsSavingTableContext] = useState(false);
  const [isServerSettingsOpen, setIsServerSettingsOpen] = useState(false);
  const [isContextDocumentOpen, setIsContextDocumentOpen] = useState(false);
  const [serverSettingsText, setServerSettingsText] = useState('');
  const [contextDocumentText, setContextDocumentText] = useState('');
  const [serverSettingsError, setServerSettingsError] = useState('');
  const [contextDocumentError, setContextDocumentError] = useState('');
  const [isSavingServerSettings, setIsSavingServerSettings] = useState(false);
  const [isSavingContextDocument, setIsSavingContextDocument] = useState(false);

  const activeFilters = useMemo(() => buildFilterObject(filters), [filters]);
  const activeFilterExpression = useMemo(
    () => buildFilterExpression(filters, selectedTable?.Columns || []),
    [filters, selectedTable]
  );
  const totalPages = totalRowCount === null ? 1 : Math.max(1, Math.ceil(totalRowCount / pageSize));
  const canGoNext = totalRowCount === null ? rows.length >= pageSize : pageIndex < totalPages - 1;
  const isRefreshingWorkspace =
    isLoadingDatabaseNames || isLoadingDatabaseDetails || isLoadingSchema || isLoadingRows;
  const contextSubtitle = selectedTableName
    ? `${selectedDatabaseName} / ${selectedTableName}`
    : selectedDatabaseName || t('dashboard.chooseDatabase');

  useEffect(() => {
    document.title = selectedTableName
      ? t('app.workspaceTitleWithTable', { databaseName: selectedDatabaseName, tableName: selectedTableName })
      : t('app.workspaceTitle');
  }, [selectedDatabaseName, selectedTableName, t]);

  useEffect(() => {
    let cancelled = false;

    async function loadDatabaseNames() {
      setIsLoadingDatabaseNames(true);

      try {
        const nextDatabaseNames = await apiClient.getDatabases();
        if (cancelled) {
          return;
        }

        const normalizedDatabaseNames = Array.isArray(nextDatabaseNames) ? nextDatabaseNames : [];
        setDatabaseNames(normalizedDatabaseNames);
        setSelectedDatabaseName((current) =>
          normalizedDatabaseNames.includes(current) ? current : normalizedDatabaseNames[0] || ''
        );
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('toast.databaseListFailedTitle'),
            message: loadError?.message || t('toast.databaseListFailedMessage')
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingDatabaseNames(false);
        }
      }
    }

    loadDatabaseNames();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, catalogReloadToken, t]);

  useEffect(() => {
    if (!selectedDatabaseName) {
      setSelectedDatabase(null);
      setSelectedTableName('');
      setSelectedTable(null);
      setRows([]);
      setTotalRowCount(null);
      return;
    }

    let cancelled = false;

    async function loadSelectedDatabase() {
      setIsLoadingDatabaseDetails(true);

      try {
        const database = await apiClient.getDatabase(selectedDatabaseName);
        if (cancelled) {
          return;
        }

        setSelectedDatabase(database);
        setSelectedTableName((current) => {
          const tableNames = database?.TableNames || [];
          if (current && tableNames.includes(current)) {
            return current;
          }

          return tableNames[0] || '';
        });
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('toast.databaseMetadataFailedTitle'),
            message: loadError?.message || t('toast.databaseMetadataFailedMessage')
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingDatabaseDetails(false);
        }
      }
    }

    loadSelectedDatabase();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, databaseReloadToken, selectedDatabaseName, t]);

  useEffect(() => {
    const tableNames = selectedDatabase?.TableNames || [];

    if (tableNames.length < 1) {
      setSelectedTableName('');
      setSelectedTable(null);
      setRows([]);
      setTotalRowCount(null);
      return;
    }

    if (!selectedTableName || !tableNames.includes(selectedTableName)) {
      setSelectedTableName(tableNames[0]);
    }
  }, [selectedDatabase, selectedTableName]);

  useEffect(() => {
    if (!selectedDatabaseName) {
      setDatabaseContext(null);
      setDatabaseContextDraft('');
      return;
    }

    let cancelled = false;

    async function loadDatabaseContext() {
      setIsLoadingDatabaseContext(true);

      try {
        const nextContext = await apiClient.getDatabaseContext(selectedDatabaseName);
        if (!cancelled) {
          setDatabaseContext(nextContext);
          setDatabaseContextDraft(nextContext?.Context || '');
        }
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('dashboard.databaseContext', { defaultValue: 'Database context' }),
            message:
              loadError?.message ||
              t('dashboard.contextLoadFailed', { defaultValue: 'Unable to load context.' })
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingDatabaseContext(false);
        }
      }
    }

    loadDatabaseContext();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, selectedDatabaseName, t]);

  useEffect(() => {
    if (!selectedDatabaseName || !selectedTableName) {
      setTableContext(null);
      setTableContextDraft('');
      return;
    }

    let cancelled = false;

    async function loadTableContext() {
      setIsLoadingTableContext(true);

      try {
        const nextContext = await apiClient.getTableContext(selectedDatabaseName, selectedTableName);
        if (!cancelled) {
          setTableContext(nextContext);
          setTableContextDraft(nextContext?.Context || '');
        }
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('dashboard.tableContext', { defaultValue: 'Table context' }),
            message:
              loadError?.message ||
              t('dashboard.contextLoadFailed', { defaultValue: 'Unable to load context.' })
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingTableContext(false);
        }
      }
    }

    loadTableContext();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, selectedDatabaseName, selectedTableName, t]);

  useEffect(() => {
    if (!selectedDatabaseName || !selectedTableName) {
      setSelectedTable(null);
      return;
    }

    let cancelled = false;

    async function loadTableSchema() {
      setIsLoadingSchema(true);

      try {
        const schema = await apiClient.getTableSchema(selectedDatabaseName, selectedTableName);

        if (!cancelled) {
          setSelectedTable(schema);
          setSortColumn((current) =>
            current && schema?.Columns?.some((column) => column.Name === current) ? current : ''
          );
        }
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('toast.schemaFailedTitle'),
            message: loadError?.message || t('toast.schemaFailedMessage')
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingSchema(false);
        }
      }
    }

    loadTableSchema();

    return () => {
      cancelled = true;
    };
  }, [addToast, apiClient, schemaReloadToken, selectedDatabaseName, selectedTableName, t]);

  useEffect(() => {
    if (!selectedDatabaseName || !selectedTableName) {
      setRows([]);
      return;
    }

    let cancelled = false;

    async function loadRows() {
      setIsLoadingRows(true);

      try {
        const rowOptions = {
          filters: activeFilters,
          index: pageIndex * pageSize,
          max: pageSize,
          orderBy: sortColumn,
          orderDirection: sortDirection
        };
        const nextRows = activeFilterExpression
          ? await apiClient.searchRows(selectedDatabaseName, selectedTableName, activeFilterExpression, rowOptions)
          : await apiClient.getRows(selectedDatabaseName, selectedTableName, rowOptions);

        if (!cancelled) {
          setRows(Array.isArray(nextRows) ? nextRows : []);
          setSelectedRow(null);
        }
      } catch (loadError) {
        if (!cancelled) {
          addToast({
            tone: 'danger',
            title: t('toast.rowsFailedTitle'),
            message: loadError?.message || t('toast.rowsFailedMessage')
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoadingRows(false);
        }
      }
    }

    loadRows();

    return () => {
      cancelled = true;
    };
  }, [
    activeFilterExpression,
    activeFilters,
    addToast,
    apiClient,
    pageIndex,
    pageSize,
    rowsReloadToken,
    selectedDatabaseName,
    selectedTableName,
    sortColumn,
    sortDirection,
    t
  ]);

  useEffect(() => {
    if (!selectedDatabaseName || !selectedTableName || !selectedDatabase?.Type || !selectedTable?.Columns) {
      setTotalRowCount(null);
      return;
    }

    let cancelled = false;

    async function loadTotalRowCount() {
      try {
        const nextCount = await apiClient.getRowCount(
          selectedDatabaseName,
          selectedDatabase.Type,
          selectedTableName,
          selectedTable.Columns,
          activeFilters,
          activeFilterExpression
        );

        if (!cancelled) {
          setTotalRowCount(nextCount);
        }
      } catch {
        if (!cancelled) {
          setTotalRowCount(null);
        }
      }
    }

    loadTotalRowCount();

    return () => {
      cancelled = true;
    };
  }, [
    activeFilterExpression,
    activeFilters,
    apiClient,
    countReloadToken,
    selectedDatabase,
    selectedDatabaseName,
    selectedTable,
    selectedTableName
  ]);

  useEffect(() => {
    if (totalRowCount === null) {
      return;
    }

    const lastPageIndex = Math.max(0, Math.ceil(totalRowCount / pageSize) - 1);
    if (pageIndex > lastPageIndex) {
      setPageIndex(lastPageIndex);
    }
  }, [pageIndex, pageSize, totalRowCount]);

  function triggerTableReload({ includeSchema = false, includeCatalog = false, includeDatabase = false } = {}) {
    if (includeCatalog) {
      setCatalogReloadToken((current) => current + 1);
    }

    if (includeCatalog || includeDatabase) {
      setDatabaseReloadToken((current) => current + 1);
    }

    if (includeSchema) {
      setSchemaReloadToken((current) => current + 1);
    }

    setRowsReloadToken((current) => current + 1);
    setCountReloadToken((current) => current + 1);
  }

  function handleRefreshWorkspace() {
    triggerTableReload({ includeDatabase: true, includeSchema: true });
  }

  function handleDisconnect() {
    logout();
    navigate('/', { replace: true });
  }

  function resetTableView() {
    setPageIndex(0);
    setSortColumn('');
    setSortDirection('asc');
      setFilters([DEFAULT_FILTER]);
    setRows([]);
    setSelectedRow(null);
    setTotalRowCount(null);
  }

  function handleAddFilter() {
    setFilters((current) => [
      ...current,
      { id: Date.now(), column: '', operator: 'Equals', value: '' }
    ]);
  }

  function handleFilterChange(filterId, field, value) {
    setFilters((current) =>
      current.map((filter) => {
        if (filter.id !== filterId) {
          return filter;
        }

        if (field === 'column') {
          const nextOperators = getAvailableOperators(value);
          const nextOperator = nextOperators.some((entry) => entry.value === filter.operator)
            ? filter.operator
            : nextOperators[0]?.value || 'Equals';

          return {
            ...filter,
            column: value,
            operator: nextOperator,
            ...(filterOperatorRequiresValue(nextOperator) ? {} : { value: '' })
          };
        }

        return {
          ...filter,
          [field]: value,
          ...(field === 'operator' && !filterOperatorRequiresValue(value) ? { value: '' } : {})
        };
      })
    );
    setPageIndex(0);
    setCountReloadToken((current) => current + 1);
  }

  function getAvailableOperators(columnName) {
    const column = (selectedTable?.Columns || []).find((entry) => entry.Name === columnName);
    const columnKind = getColumnKind(column);

    if (columnKind === 'number' || columnKind === 'datetime') {
      return FILTER_OPERATORS.filter((entry) =>
        [
          'Equals',
          'NotEquals',
          'GreaterThan',
          'GreaterThanOrEqualTo',
          'LessThan',
          'LessThanOrEqualTo',
          'IsNull',
          'IsNotNull'
        ].includes(entry.value)
      );
    }

    if (columnKind === 'boolean') {
      return FILTER_OPERATORS.filter((entry) =>
        ['Equals', 'NotEquals', 'IsNull', 'IsNotNull'].includes(entry.value)
      );
    }

    return FILTER_OPERATORS;
  }

  function handleRemoveFilter(filterId) {
    setFilters((current) =>
      current.length === 1
        ? [DEFAULT_FILTER]
        : current.filter((filter) => filter.id !== filterId)
    );
    setPageIndex(0);
    setCountReloadToken((current) => current + 1);
  }

  function openInsertModal() {
    setEditorState({
      isOpen: true,
      mode: 'insert',
      row: null
    });
  }

  function openEditModal(row) {
    if (!getPrimaryKeyColumn(selectedTable)) {
      addToast({
        tone: 'danger',
        title: t('toast.primaryKeyRequiredTitle'),
        message: t('toast.updatePrimaryKeyRequiredMessage')
      });
      return;
    }

    setEditorState({
      isOpen: true,
      mode: 'edit',
      row
    });
  }

  async function handleSaveRow(payload) {
    if (!selectedDatabaseName || !selectedTableName || !selectedTable) {
      return;
    }

    const primaryKeyColumn = getPrimaryKeyColumn(selectedTable);

    if (editorState.mode === 'edit' && !primaryKeyColumn) {
      addToast({
        tone: 'danger',
        title: t('toast.primaryKeyRequiredTitle'),
        message: t('toast.updatePrimaryKeyRequiredMessage')
      });
      return;
    }

    if (editorState.mode === 'edit' && primaryKeyColumn) {
      await apiClient.updateRow(
        selectedDatabaseName,
        selectedTableName,
        editorState.row[primaryKeyColumn.Name],
        payload
      );
      addToast({
        tone: 'success',
        title: t('toast.rowUpdatedTitle'),
        message: t('toast.rowUpdatedMessage', { tableName: selectedTableName })
      });
    } else {
      await apiClient.insertRow(selectedDatabaseName, selectedTableName, payload);
      addToast({
        tone: 'success',
        title: t('toast.rowInsertedTitle'),
        message: t('toast.rowInsertedMessage', { tableName: selectedTableName })
      });
    }

    setEditorState({ isOpen: false, mode: 'insert', row: null });
    triggerTableReload();
  }

  function openDeleteRowModal(row) {
    const primaryKeyColumn = getPrimaryKeyColumn(selectedTable);
    if (!primaryKeyColumn) {
      addToast({
        tone: 'danger',
        title: t('toast.primaryKeyRequiredTitle'),
        message: t('toast.deletePrimaryKeyRequiredMessage')
      });
      return;
    }

    const primaryKeyValue = row?.[primaryKeyColumn.Name];

    setConfirmState({
      body: t('confirm.deleteRowBody', {
        columnName: primaryKeyColumn.Name,
        value: primaryKeyValue ?? ''
      }),
      confirmLabel: t('confirm.deleteRowConfirm'),
      isOpen: true,
      onConfirm: async () => {
        await apiClient.deleteRow(
          selectedDatabaseName,
          selectedTableName,
          row[primaryKeyColumn.Name]
        );
        addToast({
          tone: 'success',
          title: t('toast.rowDeletedTitle'),
          message: t('toast.rowDeletedMessage', { tableName: selectedTableName })
        });
        setConfirmState({ body: '', confirmLabel: '', isOpen: false, onConfirm: null, title: '' });
        triggerTableReload();
      },
      title: t('confirm.deleteRowTitle')
    });
  }

  function openTruncateModal() {
    setConfirmState({
      body: t('confirm.truncateTableBody', { tableName: selectedTableName }),
      confirmLabel: t('confirm.truncateTableConfirm'),
      isOpen: true,
      onConfirm: async () => {
        await apiClient.truncateTable(selectedDatabaseName, selectedTableName);
        addToast({
          tone: 'success',
          title: t('toast.tableTruncatedTitle'),
          message: t('toast.tableTruncatedMessage', { tableName: selectedTableName })
        });
        setConfirmState({ body: '', confirmLabel: '', isOpen: false, onConfirm: null, title: '' });
        triggerTableReload();
      },
      title: t('confirm.truncateTableTitle')
    });
  }

  function openDropModal() {
    setConfirmState({
      body: t('confirm.dropTableBody', { tableName: selectedTableName }),
      confirmLabel: t('confirm.dropTableConfirm'),
      isOpen: true,
      onConfirm: async () => {
        await apiClient.dropTable(selectedDatabaseName, selectedTableName);
        addToast({
          tone: 'success',
          title: t('toast.tableDroppedTitle'),
          message: t('toast.tableDroppedMessage', {
            databaseName: selectedDatabaseName,
            tableName: selectedTableName
          })
        });
        setConfirmState({ body: '', confirmLabel: '', isOpen: false, onConfirm: null, title: '' });
        resetTableView();
        setSelectedTableName('');
        setSelectedTable(null);
        triggerTableReload({ includeCatalog: true });
      },
      title: t('confirm.dropTableTitle')
    });
  }

  async function handleRunRawQuery(query) {
    const result = await apiClient.runRawQuery(selectedDatabaseName, query);
    addToast({
      tone: 'success',
      title: t('toast.queryExecutedTitle'),
      message: t('toast.queryExecutedMessage')
    });
    triggerTableReload({ includeCatalog: true, includeSchema: true });
    return result;
  }

  async function openServerSettingsEditor() {
    setIsServerSettingsOpen(true);
    setServerSettingsError('');

    try {
      const settings = await apiClient.getServerSettings();
      setServerSettingsText(JSON.stringify(settings, null, 2));
    } catch (loadError) {
      setServerSettingsError(loadError?.message || t('dashboard.settingsLoadFailed', { defaultValue: 'Unable to load server settings.' }));
    }
  }

  async function openContextDocumentEditor() {
    setIsContextDocumentOpen(true);
    setContextDocumentError('');

    try {
      const contextDocument = await apiClient.getContextDocument();
      setContextDocumentText(JSON.stringify(contextDocument, null, 2));
    } catch (loadError) {
      setContextDocumentError(loadError?.message || t('dashboard.contextLoadFailed', { defaultValue: 'Unable to load context.' }));
    }
  }

  async function handleSaveServerSettings() {
    setIsSavingServerSettings(true);
    setServerSettingsError('');

    try {
      const parsedSettings = JSON.parse(serverSettingsText);
      const response = await apiClient.updateServerSettings(parsedSettings);
      const nextSettings = response?.Settings || parsedSettings;
      setServerSettingsText(JSON.stringify(nextSettings, null, 2));
      addToast({
        tone: 'success',
        title: t('dashboard.serverSettings', { defaultValue: 'Server settings' }),
        message:
          response?.Result?.Message ||
          t('dashboard.settingsSaved', { defaultValue: 'Server settings saved.' })
      });
    } catch (saveError) {
      setServerSettingsError(saveError?.message || t('dashboard.settingsSaveFailed', { defaultValue: 'Unable to save server settings.' }));
    } finally {
      setIsSavingServerSettings(false);
    }
  }

  async function handleReloadServerSettings() {
    setIsSavingServerSettings(true);
    setServerSettingsError('');

    try {
      const response = await apiClient.reloadServerSettings();
      const nextSettings = response?.Settings || {};
      setServerSettingsText(JSON.stringify(nextSettings, null, 2));
      addToast({
        tone: 'success',
        title: t('dashboard.serverSettings', { defaultValue: 'Server settings' }),
        message:
          response?.Result?.Message ||
          t('dashboard.settingsReloaded', { defaultValue: 'Server settings reloaded.' })
      });
    } catch (reloadError) {
      setServerSettingsError(reloadError?.message || t('dashboard.settingsReloadFailed', { defaultValue: 'Unable to reload server settings.' }));
    } finally {
      setIsSavingServerSettings(false);
    }
  }

  async function handleSaveContextDocument() {
    setIsSavingContextDocument(true);
    setContextDocumentError('');

    try {
      const parsedContext = JSON.parse(contextDocumentText);
      const response = await apiClient.updateContextDocument(parsedContext);
      const nextContext = response?.Context || parsedContext;
      setContextDocumentText(JSON.stringify(nextContext, null, 2));
      addToast({
        tone: 'success',
        title: t('dashboard.contextDocument', { defaultValue: 'Context document' }),
        message:
          response?.Result?.Message ||
          t('dashboard.contextSaved', { defaultValue: 'Context saved.' })
      });
    } catch (saveError) {
      setContextDocumentError(saveError?.message || t('dashboard.contextSaveFailed', { defaultValue: 'Unable to save context.' }));
    } finally {
      setIsSavingContextDocument(false);
    }
  }

  async function handleReloadContextDocument() {
    setIsSavingContextDocument(true);
    setContextDocumentError('');

    try {
      const response = await apiClient.reloadContextDocument();
      const nextContext = response?.Context || {};
      setContextDocumentText(JSON.stringify(nextContext, null, 2));
      addToast({
        tone: 'success',
        title: t('dashboard.contextDocument', { defaultValue: 'Context document' }),
        message:
          response?.Result?.Message ||
          t('dashboard.contextReloaded', { defaultValue: 'Context reloaded.' })
      });
    } catch (reloadError) {
      setContextDocumentError(reloadError?.message || t('dashboard.contextReloadFailed', { defaultValue: 'Unable to reload context.' }));
    } finally {
      setIsSavingContextDocument(false);
    }
  }

  async function handleSaveDatabaseContext() {
    if (!selectedDatabaseName) {
      return;
    }

    setIsSavingDatabaseContext(true);

    try {
      const response = await apiClient.updateDatabaseContext(selectedDatabaseName, {
        database: selectedDatabaseName,
        context: databaseContextDraft,
        tables: databaseContext?.Tables || {}
      });

      const nextContext = response?.Context || response;
      setDatabaseContext(nextContext);
      setDatabaseContextDraft(nextContext?.Context || '');
      addToast({
        tone: 'success',
        title: t('dashboard.databaseContext', { defaultValue: 'Database context' }),
        message:
          response?.Result?.Message ||
          t('dashboard.contextSaved', { defaultValue: 'Context saved.' })
      });
    } catch (saveError) {
      addToast({
        tone: 'danger',
        title: t('dashboard.databaseContext', { defaultValue: 'Database context' }),
        message:
          saveError?.message ||
          t('dashboard.contextSaveFailed', { defaultValue: 'Unable to save context.' })
      });
    } finally {
      setIsSavingDatabaseContext(false);
    }
  }

  async function handleSaveTableContext() {
    if (!selectedDatabaseName || !selectedTableName) {
      return;
    }

    setIsSavingTableContext(true);

    try {
      const response = await apiClient.updateTableContext(selectedDatabaseName, selectedTableName, {
        database: selectedDatabaseName,
        table: selectedTableName,
        context: tableContextDraft
      });

      const nextContext = response?.Context || response;
      setTableContext(nextContext);
      setTableContextDraft(nextContext?.Context || '');
      addToast({
        tone: 'success',
        title: t('dashboard.tableContext', { defaultValue: 'Table context' }),
        message:
          response?.Result?.Message ||
          t('dashboard.contextSaved', { defaultValue: 'Context saved.' })
      });
    } catch (saveError) {
      addToast({
        tone: 'danger',
        title: t('dashboard.tableContext', { defaultValue: 'Table context' }),
        message:
          saveError?.message ||
          t('dashboard.contextSaveFailed', { defaultValue: 'Unable to save context.' })
      });
    } finally {
      setIsSavingTableContext(false);
    }
  }

  async function handleReloadDatabaseContext() {
    if (!selectedDatabaseName) {
      return;
    }

    setIsLoadingDatabaseContext(true);

    try {
      const nextContext = await apiClient.getDatabaseContext(selectedDatabaseName);
      setDatabaseContext(nextContext);
      setDatabaseContextDraft(nextContext?.Context || '');
    } catch (reloadError) {
      addToast({
        tone: 'danger',
        title: t('dashboard.databaseContext', { defaultValue: 'Database context' }),
        message:
          reloadError?.message ||
          t('dashboard.contextReloadFailed', { defaultValue: 'Unable to reload context.' })
      });
    } finally {
      setIsLoadingDatabaseContext(false);
    }
  }

  async function handleReloadTableContext() {
    if (!selectedDatabaseName || !selectedTableName) {
      return;
    }

    setIsLoadingTableContext(true);

    try {
      const nextContext = await apiClient.getTableContext(selectedDatabaseName, selectedTableName);
      setTableContext(nextContext);
      setTableContextDraft(nextContext?.Context || '');
    } catch (reloadError) {
      addToast({
        tone: 'danger',
        title: t('dashboard.tableContext', { defaultValue: 'Table context' }),
        message:
          reloadError?.message ||
          t('dashboard.contextReloadFailed', { defaultValue: 'Unable to reload context.' })
      });
    } finally {
      setIsLoadingTableContext(false);
    }
  }

  const overviewStats = [
    {
      label: t('dashboard.provider'),
      value: selectedDatabase?.Type || t('common.notAvailable')
    },
    {
      label: t('dashboard.totalRecords'),
      value:
        totalRowCount === null
          ? t('common.notAvailable')
          : formatNumber(totalRowCount, i18n.resolvedLanguage)
    },
    {
      label: t('dashboard.currentPage'),
      value: formatNumber(pageIndex + 1, i18n.resolvedLanguage)
    },
    {
      label: t('dashboard.pageSize'),
      value: formatNumber(pageSize, i18n.resolvedLanguage)
    }
  ];

  return (
    <Shell
      sidebar={
        <Sidebar
          databaseNames={databaseNames}
          isLoadingDatabaseDetails={isLoadingDatabaseDetails}
          onSelectDatabase={(databaseName) => {
            setSelectedDatabaseName(databaseName);
            setSelectedTableName('');
            setSelectedTable(null);
            resetTableView();
          }}
          onSelectTable={(tableName) => {
            setSelectedTableName(tableName);
            setSelectedTable(null);
            setPageIndex(0);
            setSelectedRow(null);
            setTotalRowCount(null);
          }}
          selectedDatabase={selectedDatabase}
          selectedDatabaseName={selectedDatabaseName}
          selectedTableName={selectedTableName}
        />
      }
      topbar={
        <Topbar
          onOpenContextDocument={openContextDocumentEditor}
          onOpenServerSettings={openServerSettingsEditor}
          onLogout={handleDisconnect}
          selectedDatabase={selectedDatabase}
          selectedTable={selectedTable}
          session={session}
        />
      }
    >
      <div className="workspace-grid">
        <section className="overview-panel">
          {overviewStats.map((stat) => (
            <div className="stat-card stat-card--compact" key={stat.label}>
              <span>{stat.label}</span>
              <strong>{stat.value}</strong>
            </div>
          ))}
        </section>

        <CollapsibleSection
          actions={
            <div className="panel-actions">
              <button
                className="icon-button"
                disabled={!selectedDatabaseName || isLoadingDatabaseContext}
                onClick={handleReloadDatabaseContext}
                aria-label={t('common.refresh')}
                title={t('dashboard.reloadDatabaseContextTooltip', {
                  defaultValue: 'Reload the saved database context for the selected database from the server.'
                })}
                type="button"
              >
                <RefreshIcon />
              </button>
              <button
                className="button button-primary"
                disabled={!selectedDatabaseName || isSavingDatabaseContext}
                onClick={handleSaveDatabaseContext}
                title={t('dashboard.saveDatabaseContextTooltip', {
                  defaultValue: 'Save the database-level context so future users and agents can understand this database faster.'
                })}
                type="button"
              >
                {isSavingDatabaseContext ? t('common.saving') : t('common.save')}
              </button>
            </div>
          }
          isCollapsed={isContextCollapsed}
          onToggle={() => setIsContextCollapsed((current) => !current)}
          subtitle={contextSubtitle}
          title={t('dashboard.context', { defaultValue: 'Context' })}
        >
          <div className="context-grid">
            <div className="context-editor" title={t('dashboard.databaseContextTooltip', {
              defaultValue: 'Describe the database as a whole, including its purpose, domain, and any broad retrieval rules.'
            })}>
              <div className="context-editor__header">
                <div>
                  <p className="eyebrow">{t('dashboard.database', { defaultValue: 'Database' })}</p>
                  <h3>{selectedDatabaseName || t('dashboard.chooseDatabase')}</h3>
                </div>
              </div>
              <textarea
                className="context-editor__textarea"
                disabled={!selectedDatabaseName || isLoadingDatabaseContext}
                onChange={(event) => setDatabaseContextDraft(event.target.value)}
                placeholder={t('dashboard.databaseContextPlaceholder', {
                  defaultValue: 'Describe what this database contains, how it is used, and what an agent should know before querying it.'
                })}
                rows={6}
                title={t('dashboard.databaseContextTooltip', {
                  defaultValue: 'Describe the database as a whole, including its purpose, domain, and any broad retrieval rules.'
                })}
                value={databaseContextDraft}
              />
            </div>

            <div className="context-editor" title={t('dashboard.tableContextTooltip', {
              defaultValue: 'Describe the selected table, including its primary key, relationships, and any retrieval hints worth preserving.'
            })}>
              <div className="context-editor__header">
                <div>
                  <p className="eyebrow">{t('dashboard.table', { defaultValue: 'Table' })}</p>
                  <h3>{selectedTableName || t('dashboard.chooseTable')}</h3>
                </div>
                <div className="panel-actions">
                  <button
                    className="icon-button"
                    disabled={!selectedTableName || isLoadingTableContext}
                    onClick={handleReloadTableContext}
                    aria-label={t('common.refresh')}
                    title={t('dashboard.reloadTableContextTooltip', {
                      defaultValue: 'Reload the saved table context for the selected table from the server.'
                    })}
                    type="button"
                  >
                    <RefreshIcon />
                  </button>
                  <button
                    className="button button-primary"
                    disabled={!selectedTableName || isSavingTableContext}
                    onClick={handleSaveTableContext}
                    title={t('dashboard.saveTableContextTooltip', {
                      defaultValue: 'Save the table-level context so future users and agents can understand this table faster.'
                    })}
                    type="button"
                  >
                    {isSavingTableContext ? t('common.saving') : t('common.save')}
                  </button>
                </div>
              </div>
              <textarea
                className="context-editor__textarea"
                disabled={!selectedTableName || isLoadingTableContext}
                onChange={(event) => setTableContextDraft(event.target.value)}
                placeholder={t('dashboard.tableContextPlaceholder', {
                  defaultValue: 'Describe what this table stores, its primary key, and any semantic rules or relationships that help future retrievals.'
                })}
                rows={6}
                title={t('dashboard.tableContextTooltip', {
                  defaultValue: 'Describe the selected table, including its primary key, relationships, and any retrieval hints worth preserving.'
                })}
                value={tableContextDraft}
              />
            </div>
          </div>
        </CollapsibleSection>

        <CollapsibleSection
          isCollapsed={isSchemaCollapsed}
          onToggle={() => setIsSchemaCollapsed((current) => !current)}
          subtitle={selectedTable?.Name || selectedTableName || t('dashboard.chooseTable')}
          title={t('dashboard.schema')}
        >
          <SchemaTable table={selectedTable} />
        </CollapsibleSection>

        <CollapsibleSection
          actions={
            <div className="panel-actions">
              <button
                className="button button-secondary"
                disabled={!selectedDatabaseName}
                onClick={() => setIsRawQueryOpen(true)}
                title={t('dashboard.rawSqlTooltip', {
                  defaultValue: 'Open the raw SQL modal to run a provider-specific statement against the selected database.'
                })}
                type="button"
              >
                {t('dashboard.rawSql')}
              </button>
              <button
                className="button button-primary"
                disabled={!selectedTable}
                onClick={openInsertModal}
                title={t('editor.insertRowTooltip', {
                  defaultValue: 'Create a new row in the selected table.'
                })}
                type="button"
              >
                {t('editor.insertRow')}
              </button>
              <button
                className="button button-danger"
                disabled={!selectedTable}
                onClick={openTruncateModal}
                title={t('dashboard.truncateTooltip', {
                  defaultValue: 'Remove every row from the selected table while keeping the schema.'
                })}
                type="button"
              >
                {t('dashboard.truncate')}
              </button>
              <button
                className="button button-danger"
                disabled={!selectedTable}
                onClick={openDropModal}
                title={t('dashboard.dropTooltip', {
                  defaultValue: 'Drop the selected table and remove its schema and rows.'
                })}
                type="button"
              >
                {t('dashboard.drop')}
              </button>
            </div>
          }
          isCollapsed={isRecordsCollapsed}
          onToggle={() => setIsRecordsCollapsed((current) => !current)}
          subtitle={selectedTable?.Name || selectedTableName || t('dashboard.chooseTable')}
          title={t('dashboard.records')}
        >
          <div className="filter-bar">
            {(filters || []).map((filter, index) => (
              <div className="filter-chip" key={filter.id}>
                <select
                  aria-label={t('filters.column')}
                  onChange={(event) => handleFilterChange(filter.id, 'column', event.target.value)}
                  title={t('filters.columnTooltip', {
                    defaultValue: 'Choose which column RestDb should filter on for this row of the filter builder.'
                  })}
                  value={filter.column}
                >
                  <option value="">{t('filters.column')}</option>
                  {(selectedTable?.Columns || []).map((column) => (
                    <option key={column.Name} value={column.Name}>
                      {column.Name}
                    </option>
                  ))}
                </select>
                <select
                  aria-label={t('filters.operator', { defaultValue: 'Operator' })}
                  onChange={(event) => handleFilterChange(filter.id, 'operator', event.target.value)}
                  title={t('filters.operatorTooltip', {
                    defaultValue: 'Choose how RestDb should compare the selected column with the supplied value.'
                  })}
                  value={filter.operator}
                >
                  {getAvailableOperators(filter.column).map((operator) => (
                    <option key={operator.value} value={operator.value}>
                      {t(operator.labelKey)}
                    </option>
                  ))}
                </select>
                <input
                  aria-label={t('filters.equalsValue')}
                  disabled={!filterOperatorRequiresValue(filter.operator)}
                  onChange={(event) => handleFilterChange(filter.id, 'value', event.target.value)}
                  placeholder={
                    filterOperatorRequiresValue(filter.operator)
                      ? t('filters.equalsValue')
                      : t('filters.noValueRequired', { defaultValue: 'No value required' })
                  }
                  title={t('filters.equalsValueTooltip', {
                    defaultValue: 'Enter the comparison value RestDb should use for the selected column and operator.'
                  })}
                  value={filter.value}
                />
                <div className="filter-chip__actions">
                  <button
                    aria-label={t('filters.remove', { defaultValue: 'Remove filter' })}
                    className="icon-button"
                    onClick={() => handleRemoveFilter(filter.id)}
                    title={t('filters.removeTooltip', {
                      defaultValue: 'Remove this filter row from the current query.'
                    })}
                    type="button"
                  >
                    <TrashIcon />
                  </button>
                  {index === filters.length - 1 ? (
                    <button
                      aria-label={t('filters.add', { defaultValue: 'Add filter' })}
                      className="icon-button"
                      onClick={handleAddFilter}
                      title={t('filters.addTooltip', {
                        defaultValue: 'Append another filter row to the current query.'
                      })}
                      type="button"
                    >
                      <PlusIcon />
                    </button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>

          <PaginationBar
            canGoNext={canGoNext}
            isRefreshing={isRefreshingWorkspace}
            onChangePageSize={(value) => {
              setPageSize(value);
              setPageIndex(0);
            }}
            onGoToFirstPage={() => setPageIndex(0)}
            onGoToLastPage={() => setPageIndex(Math.max(0, totalPages - 1))}
            onGoToNextPage={() => setPageIndex((current) => current + 1)}
            onGoToPage={(nextPageIndex) => setPageIndex(Math.max(0, nextPageIndex))}
            onGoToPreviousPage={() => setPageIndex((current) => Math.max(0, current - 1))}
            onRefresh={handleRefreshWorkspace}
            onSortColumnChange={(value) => {
              setSortColumn(value);
              setPageIndex(0);
            }}
            onSortDirectionChange={(value) => {
              setSortDirection(value);
              setPageIndex(0);
            }}
            pageIndex={pageIndex}
            pageSize={pageSize}
            sortColumn={sortColumn}
            sortDirection={sortDirection}
            sortableColumns={selectedTable?.Columns || []}
            totalPages={totalPages}
            totalRecords={totalRowCount}
          />

          {isLoadingRows || isLoadingSchema ? (
            <div className="inline-loader">
              <div className="loading-spinner" />
              <span>{t('dashboard.loadingTableData')}</span>
            </div>
          ) : (
            <DataGrid
              onDeleteRow={openDeleteRowModal}
              onEditRow={openEditModal}
              onSelectRow={setSelectedRow}
              rows={rows}
              selectedRowKey={
                selectedRow && selectedTable?.PrimaryKey
                  ? selectedRow[selectedTable.PrimaryKey]
                  : null
              }
              table={selectedTable}
            />
          )}
        </CollapsibleSection>
      </div>

      <RowEditorModal
        isOpen={editorState.isOpen}
        mode={editorState.mode}
        onClose={() => setEditorState({ isOpen: false, mode: 'insert', row: null })}
        onSave={handleSaveRow}
        row={editorState.row}
        table={selectedTable}
      />

      <RawQueryModal
        databaseName={selectedDatabaseName}
        initialQuery={selectedTableName ? `SELECT * FROM ${selectedTableName};` : 'SELECT 1;'}
        isOpen={isRawQueryOpen}
        onClose={() => setIsRawQueryOpen(false)}
        onExecute={handleRunRawQuery}
      />

      <ConfirmModal
        body={confirmState.body}
        confirmLabel={confirmState.confirmLabel}
        isOpen={confirmState.isOpen}
        onClose={() =>
          setConfirmState({ body: '', confirmLabel: '', isOpen: false, onConfirm: null, title: '' })
        }
        onConfirm={async () => {
          if (confirmState.onConfirm) {
            await confirmState.onConfirm();
          }
        }}
        title={confirmState.title}
      />

      <JsonEditorModal
        error={serverSettingsError}
        helper={t('dashboard.serverSettingsHelp', {
          defaultValue: 'Edit the full restdb.json document. Save writes to disk through the API; refresh reloads the running file state. Changes to listener host, listener port, TLS, certificate, or similar transport settings require a server restart before they take effect.'
        })}
        isBusy={isSavingServerSettings}
        isOpen={isServerSettingsOpen}
        onChange={setServerSettingsText}
        onClose={() => setIsServerSettingsOpen(false)}
        onReload={handleReloadServerSettings}
        onSave={handleSaveServerSettings}
        size="large"
        title={t('dashboard.serverSettings', { defaultValue: 'Server settings' })}
        value={serverSettingsText}
      />

      <JsonEditorModal
        error={contextDocumentError}
        helper={t('dashboard.contextDocumentHelp', {
          defaultValue: 'Edit the shared context.json document. Save persists the full document; refresh reloads it from the running server.'
        })}
        isBusy={isSavingContextDocument}
        isOpen={isContextDocumentOpen}
        onChange={setContextDocumentText}
        onClose={() => setIsContextDocumentOpen(false)}
        onReload={handleReloadContextDocument}
        onSave={handleSaveContextDocument}
        size="xwide"
        title={t('dashboard.contextDocument', { defaultValue: 'Context document' })}
        value={contextDocumentText}
      />
    </Shell>
  );
}

export default WorkspaceView;
