import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Modal from './Modal';
import {
  buildPayloadFromDraft,
  createDraftFromRow,
  getColumnKind,
  validateDraft
} from '../utils/schema';

function RowEditorModal({
  isOpen,
  mode,
  onClose,
  onSave,
  row,
  table
}) {
  const { t } = useTranslation('translation');
  const [draft, setDraft] = useState({});
  const [errors, setErrors] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    setDraft(createDraftFromRow(table, row));
    setErrors({});
    setIsSubmitting(false);
  }, [isOpen, row, table]);

  async function handleSubmit(event) {
    event.preventDefault();

    const validationErrors = validateDraft(table, draft, mode, t);
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      await onSave(buildPayloadFromDraft(table, draft, mode));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Modal
      actions={
        <>
          <button
            className="button button-secondary"
            onClick={onClose}
            title={t('common.cancelTooltip', { defaultValue: 'Close the row editor without saving changes.' })}
            type="button"
          >
            {t('common.cancel')}
          </button>
          <button
            className="button button-primary"
            disabled={isSubmitting}
            form="row-editor-form"
            title={
              mode === 'edit'
                ? t('editor.saveChangesTooltip', { defaultValue: 'Save the edited row back to the selected table.' })
                : t('editor.insertRowTooltip', { defaultValue: 'Insert this new row into the selected table.' })
            }
            type="submit"
          >
            {isSubmitting
              ? t('common.saving')
              : mode === 'edit'
                ? t('editor.saveChanges')
                : t('editor.insertRow')}
          </button>
        </>
      }
      isOpen={isOpen}
      onClose={onClose}
      size="large"
      title={
        mode === 'edit'
          ? t('editor.editRowTitle', { tableName: table?.Name })
          : t('editor.insertRowTitle', { tableName: table?.Name })
      }
    >
      <form className="editor-form" id="row-editor-form" onSubmit={handleSubmit}>
        {(table?.Columns || []).map((column) => {
          const kind = getColumnKind(column);
          const value = draft[column.Name] ?? '';
          const fieldTooltip = t('editor.fieldTooltip', {
            defaultValue: `Provide a value for ${column.Name}. RestDb will send it using the provider-specific row payload format.`
          });

          return (
            <label className="editor-field" key={column.Name} title={fieldTooltip}>
              <span>
                {column.Name}
                {column.PrimaryKey || column.Name === table.PrimaryKey ? ` | ${t('schema.primaryKeyShort')}` : ''}
              </span>
              {kind === 'boolean' ? (
                <select
                  onChange={(event) =>
                    setDraft((current) => ({ ...current, [column.Name]: event.target.value }))
                  }
                  title={t('editor.booleanFieldTooltip', {
                    defaultValue: `Choose true, false, or leave ${column.Name} empty.`
                  })}
                  value={value}
                >
                  <option value="">{t('common.emptyOption')}</option>
                  <option value="true">{t('common.true')}</option>
                  <option value="false">{t('common.false')}</option>
                </select>
              ) : (
                <input
                  onChange={(event) =>
                    setDraft((current) => ({ ...current, [column.Name]: event.target.value }))
                  }
                  placeholder={kind === 'datetime' ? t('editor.datetimePlaceholder') : ''}
                  title={fieldTooltip}
                  type={kind === 'number' ? 'number' : 'text'}
                  value={value}
                />
              )}
              <small>
                {column.Type}
                {column.Nullable ? ` | ${t('editor.nullable')}` : ` | ${t('editor.required')}`}
              </small>
              {errors[column.Name] ? <div className="inline-error">{errors[column.Name]}</div> : null}
            </label>
          );
        })}
      </form>
    </Modal>
  );
}

export default RowEditorModal;
