namespace RestDb.Storage.Providers.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// SQLite query builder.
    /// </summary>
    internal class SqliteQueryBuilder : ProviderQueryBuilderBase
    {
        /// <inheritdoc />
        public override string ProviderName => "Sqlite";

        /// <inheritdoc />
        public override SqlQueryDefinition BuildListTables()
        {
            return new SqlQueryDefinition(
                "SELECT name AS table_name " +
                "FROM sqlite_master " +
                "WHERE type = 'table' " +
                "AND name NOT LIKE 'sqlite_%' " +
                "ORDER BY name;");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildDescribeTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            string literalTableName = tableName.Replace("'", "''");
            return new SqlQueryDefinition(
                "SELECT " +
                "name AS column_name, " +
                "type AS data_type, " +
                "CASE WHEN \"notnull\" = 0 THEN 1 ELSE 0 END AS is_nullable, " +
                "NULL AS max_length, " +
                "CASE WHEN pk > 0 THEN 1 ELSE 0 END AS primary_key " +
                "FROM pragma_table_info('" + literalTableName + "') " +
                "ORDER BY cid;");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildCreateTable(string tableName, List<Column> columns)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null || columns.Count < 1) throw new ArgumentException("At least one column must be supplied.", nameof(columns));

            List<string> definitions = new List<string>();
            foreach (Column curr in columns)
            {
                definitions.Add(BuildColumnDefinition(curr));
            }

            return new SqlQueryDefinition(
                "CREATE TABLE IF NOT EXISTS " + QuoteIdentifier(tableName) +
                " (" + string.Join(", ", definitions) + ");");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildClearTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            return new SqlQueryDefinition("DELETE FROM " + QuoteIdentifier(tableName) + ";");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildDropTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            return new SqlQueryDefinition("DROP TABLE IF EXISTS " + QuoteIdentifier(tableName) + ";");
        }

        /// <inheritdoc />
        public override InsertPlan BuildInsert(string tableName, List<Column> columns, Dictionary<string, object> values)
        {
            InsertPlan plan = new InsertPlan();
            SqlQueryDefinition insert = BuildInsertStatement(tableName, columns, values);
            plan.Batch.UseTransaction = true;
            plan.Batch.Queries.Add(insert);

            Column primaryKey = GetPrimaryKeyColumn(columns);
            if (primaryKey != null)
            {
                KeyValuePair<string, object>? primaryKeyValue = values
                    .FirstOrDefault(kvp => kvp.Key.Equals(primaryKey.Name, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(primaryKeyValue?.Key))
                {
                    plan.Batch.Queries.Add(BuildSelectByPrimaryKeyStatement(tableName, primaryKey.Name, primaryKeyValue.Value.Value));
                    plan.ReturnsInsertedRow = true;
                }
                else if (IsAutoIncrementPrimaryKey(primaryKey))
                {
                    plan.Batch.Queries.Add(new SqlQueryDefinition(
                        "SELECT * FROM " + QuoteIdentifier(tableName) +
                        " WHERE " + QuoteIdentifier(primaryKey.Name) + " = last_insert_rowid();"));
                    plan.ReturnsInsertedRow = true;
                }
            }

            return plan;
        }

        /// <inheritdoc />
        protected override string QuoteIdentifier(string identifier)
        {
            string[] parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = "\"" + parts[i].Replace("\"", "\"\"") + "\"";
            }

            return string.Join(".", parts);
        }

        /// <inheritdoc />
        protected override void AppendPagination(StringBuilder builder, SqlQueryDefinition query, int offset, int? maxResults, bool hasOrderClause)
        {
            if (maxResults.HasValue)
            {
                builder.Append(" LIMIT ");
                builder.Append(query.AddParameter(maxResults.Value));
                if (offset > 0)
                {
                    builder.Append(" OFFSET ");
                    builder.Append(query.AddParameter(offset));
                }
            }
            else if (offset > 0)
            {
                builder.Append(" LIMIT -1 OFFSET ");
                builder.Append(query.AddParameter(offset));
            }
        }

        /// <inheritdoc />
        protected override string NormalizeTypeForResponse(string dataType)
        {
            string baseType = ExtractBaseType(dataType).ToLowerInvariant();
            switch (baseType)
            {
                case "integer":
                    return "int";
                case "real":
                    return "double";
                case "numeric":
                    return "decimal";
                case "blob":
                    return "blob";
                case "text":
                    return "text";
                default:
                    return string.IsNullOrWhiteSpace(dataType) ? "text" : dataType.ToLowerInvariant();
            }
        }

        private string BuildColumnDefinition(Column column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            if (string.IsNullOrWhiteSpace(column.Name)) throw new ArgumentException("Column name missing.", nameof(column));
            if (string.IsNullOrWhiteSpace(column.Type)) throw new ArgumentException("Column type missing.", nameof(column));

            bool autoIncrement = IsAutoIncrementPrimaryKey(column);
            if (autoIncrement)
            {
                return QuoteIdentifier(column.Name) + " INTEGER PRIMARY KEY AUTOINCREMENT";
            }

            string sqlType = MapType(column);
            string nullability = (!column.Nullable || column.PrimaryKey) ? " NOT NULL" : string.Empty;
            string primaryKey = column.PrimaryKey ? " PRIMARY KEY" : string.Empty;
            return QuoteIdentifier(column.Name) + " " + sqlType + nullability + primaryKey;
        }

        private string MapType(Column column)
        {
            string baseType = ExtractBaseType(column.Type).ToLowerInvariant();
            int? length = ExtractDeclaredLength(column);

            switch (baseType)
            {
                case "int":
                case "integer":
                case "smallint":
                case "bigint":
                    return "INTEGER";
                case "float":
                case "double":
                case "real":
                    return "REAL";
                case "decimal":
                case "numeric":
                    return "NUMERIC";
                case "bool":
                case "boolean":
                case "bit":
                    return "INTEGER";
                case "date":
                case "time":
                case "datetime":
                case "datetime2":
                case "timestamp":
                    return "TEXT";
                case "uuid":
                case "guid":
                case "uniqueidentifier":
                    return "TEXT";
                case "blob":
                case "binary":
                case "varbinary":
                case "bytea":
                    return "BLOB";
                case "char":
                case "nchar":
                    return "CHAR(" + (length ?? 1) + ")";
                case "varchar":
                case "nvarchar":
                case "string":
                    return length.HasValue ? "VARCHAR(" + length.Value + ")" : "TEXT";
                case "text":
                    return "TEXT";
                default:
                    return column.Type.ToUpperInvariant();
            }
        }
    }
}
