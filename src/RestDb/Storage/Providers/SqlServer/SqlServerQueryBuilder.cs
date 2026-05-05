namespace RestDb.Storage.Providers.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// SQL Server query builder.
    /// </summary>
    internal class SqlServerQueryBuilder : ProviderQueryBuilderBase
    {
        /// <inheritdoc />
        public override string ProviderName => "SqlServer";

        /// <inheritdoc />
        public override SqlQueryDefinition BuildListTables()
        {
            return new SqlQueryDefinition(
                "SELECT TABLE_NAME AS table_name " +
                "FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_TYPE = 'BASE TABLE' " +
                "ORDER BY TABLE_SCHEMA, TABLE_NAME;");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildDescribeTable(string tableName)
        {
            SqlQueryDefinition query = new SqlQueryDefinition();
            string tableNameParam = query.AddParameter(tableName);
            query.CommandText =
                "SELECT " +
                "c.COLUMN_NAME AS column_name, " +
                "c.DATA_TYPE AS data_type, " +
                "CASE WHEN c.IS_NULLABLE = 'YES' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS is_nullable, " +
                "c.CHARACTER_MAXIMUM_LENGTH AS max_length, " +
                "CASE WHEN tc.CONSTRAINT_TYPE = 'PRIMARY KEY' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS primary_key " +
                "FROM INFORMATION_SCHEMA.COLUMNS c " +
                "LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu " +
                "ON c.TABLE_SCHEMA = kcu.TABLE_SCHEMA " +
                "AND c.TABLE_NAME = kcu.TABLE_NAME " +
                "AND c.COLUMN_NAME = kcu.COLUMN_NAME " +
                "LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc " +
                "ON kcu.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA " +
                "AND kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME " +
                "AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' " +
                "WHERE c.TABLE_NAME = " + tableNameParam + " " +
                "ORDER BY c.TABLE_SCHEMA, c.ORDINAL_POSITION;";
            return query;
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildCreateTable(string tableName, List<Column> columns)
        {
            List<string> definitions = new List<string>();
            foreach (Column curr in columns)
            {
                definitions.Add(BuildColumnDefinition(curr));
            }

            string literalTableName = EscapeSqlStringLiteral(tableName);
            return new SqlQueryDefinition(
                "IF NOT EXISTS (" +
                "SELECT 1 FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_NAME = N'" + literalTableName + "')" +
                " BEGIN " +
                "CREATE TABLE " + QuoteIdentifier(tableName) +
                " (" + string.Join(", ", definitions) + ");" +
                " END;");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildClearTable(string tableName)
        {
            return new SqlQueryDefinition("TRUNCATE TABLE " + QuoteIdentifier(tableName) + ";");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildDropTable(string tableName)
        {
            return new SqlQueryDefinition("DROP TABLE IF EXISTS " + QuoteIdentifier(tableName) + ";");
        }

        /// <inheritdoc />
        public override InsertPlan BuildInsert(string tableName, List<Column> columns, Dictionary<string, object> values)
        {
            SqlQueryDefinition query = BuildInsertStatement(tableName, columns, values);
            query.CommandText = query.CommandText.Replace("VALUES", "OUTPUT INSERTED.* VALUES", StringComparison.Ordinal);

            InsertPlan plan = new InsertPlan();
            plan.Batch.UseTransaction = true;
            plan.Batch.Queries.Add(query);
            plan.ReturnsInsertedRow = true;
            return plan;
        }

        /// <inheritdoc />
        protected override string QuoteIdentifier(string identifier)
        {
            string[] parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = "[" + parts[i].Replace("]", "]]") + "]";
            }

            return string.Join(".", parts);
        }

        /// <inheritdoc />
        protected override void AppendPagination(StringBuilder builder, SqlQueryDefinition query, int offset, int? maxResults, bool hasOrderClause)
        {
            if (!hasOrderClause) return;
            if (!maxResults.HasValue && offset <= 0) return;

            builder.Append(" OFFSET ");
            builder.Append(query.AddParameter(offset));
            builder.Append(" ROWS");

            if (maxResults.HasValue)
            {
                builder.Append(" FETCH NEXT ");
                builder.Append(query.AddParameter(maxResults.Value));
                builder.Append(" ROWS ONLY");
            }
        }

        /// <inheritdoc />
        protected override string NormalizeTypeForResponse(string dataType)
        {
            string lowered = ExtractBaseType(dataType).ToLowerInvariant();
            switch (lowered)
            {
                case "int":
                    return "int";
                case "bigint":
                    return "bigint";
                case "smallint":
                    return "smallint";
                case "bit":
                    return "bool";
                case "nvarchar":
                    return "nvarchar";
                case "varchar":
                    return "varchar";
                case "datetime2":
                case "datetime":
                    return "datetime";
                default:
                    return string.IsNullOrWhiteSpace(dataType) ? "nvarchar" : lowered;
            }
        }

        private string BuildColumnDefinition(Column column)
        {
            bool autoIncrement = IsAutoIncrementPrimaryKey(column);
            if (autoIncrement)
            {
                string baseType = ExtractBaseType(column.Type).ToLowerInvariant();
                string identityType = baseType == "bigint" ? "BIGINT" : "INT";
                return QuoteIdentifier(column.Name) + " " + identityType + " IDENTITY(1,1) NOT NULL PRIMARY KEY";
            }

            string sqlType = MapType(column);
            string nullability = (!column.Nullable || column.PrimaryKey) ? " NOT NULL" : " NULL";
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
                    return "INT";
                case "bigint":
                    return "BIGINT";
                case "smallint":
                    return "SMALLINT";
                case "bool":
                case "boolean":
                case "bit":
                    return "BIT";
                case "decimal":
                case "numeric":
                    return "DECIMAL(18,4)";
                case "double":
                case "float":
                    return "FLOAT";
                case "real":
                    return "REAL";
                case "date":
                    return "DATE";
                case "time":
                    return "TIME";
                case "datetime":
                case "datetime2":
                case "timestamp":
                    return "DATETIME2";
                case "uuid":
                case "guid":
                case "uniqueidentifier":
                    return "UNIQUEIDENTIFIER";
                case "blob":
                case "binary":
                case "varbinary":
                case "bytea":
                    return length.HasValue ? "VARBINARY(" + length.Value + ")" : "VARBINARY(MAX)";
                case "char":
                    return "CHAR(" + (length ?? 1) + ")";
                case "nchar":
                    return "NCHAR(" + (length ?? 1) + ")";
                case "varchar":
                    return "VARCHAR(" + (length?.ToString() ?? "MAX") + ")";
                case "nvarchar":
                case "string":
                    return "NVARCHAR(" + (length?.ToString() ?? "MAX") + ")";
                case "text":
                    return "NVARCHAR(MAX)";
                default:
                    return column.Type.ToUpperInvariant();
            }
        }

        private string EscapeSqlStringLiteral(string value)
        {
            return value?.Replace("'", "''") ?? string.Empty;
        }
    }
}
