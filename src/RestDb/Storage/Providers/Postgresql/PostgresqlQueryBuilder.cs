namespace RestDb.Storage.Providers.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// PostgreSQL query builder.
    /// </summary>
    internal class PostgresqlQueryBuilder : ProviderQueryBuilderBase
    {
        /// <inheritdoc />
        public override string ProviderName => "Postgresql";

        /// <inheritdoc />
        public override SqlQueryDefinition BuildListTables()
        {
            return new SqlQueryDefinition(
                "SELECT table_name " +
                "FROM information_schema.tables " +
                "WHERE table_schema = current_schema() " +
                "AND table_type = 'BASE TABLE' " +
                "ORDER BY table_name;");
        }

        /// <inheritdoc />
        public override SqlQueryDefinition BuildDescribeTable(string tableName)
        {
            SqlQueryDefinition query = new SqlQueryDefinition();
            string tableNameParam = query.AddParameter(tableName);
            query.CommandText =
                "SELECT " +
                "c.column_name, " +
                "c.data_type, " +
                "CASE WHEN c.is_nullable = 'YES' THEN TRUE ELSE FALSE END AS is_nullable, " +
                "c.character_maximum_length AS max_length, " +
                "CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN TRUE ELSE FALSE END AS primary_key " +
                "FROM information_schema.columns c " +
                "LEFT JOIN information_schema.key_column_usage kcu " +
                "ON c.table_schema = kcu.table_schema " +
                "AND c.table_name = kcu.table_name " +
                "AND c.column_name = kcu.column_name " +
                "LEFT JOIN information_schema.table_constraints tc " +
                "ON kcu.constraint_schema = tc.constraint_schema " +
                "AND kcu.constraint_name = tc.constraint_name " +
                "AND tc.constraint_type = 'PRIMARY KEY' " +
                "WHERE c.table_schema = current_schema() " +
                "AND c.table_name = " + tableNameParam + " " +
                "ORDER BY c.ordinal_position;";
            return query;
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
            return new SqlQueryDefinition("TRUNCATE TABLE " + QuoteIdentifier(tableName) + " RESTART IDENTITY;");
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
            query.CommandText = query.CommandText.TrimEnd(';') + " RETURNING *;";

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
            }

            if (offset > 0)
            {
                builder.Append(" OFFSET ");
                builder.Append(query.AddParameter(offset));
            }
        }

        /// <inheritdoc />
        protected override string NormalizeTypeForResponse(string dataType)
        {
            string lowered = ExtractBaseType(dataType).ToLowerInvariant();
            switch (lowered)
            {
                case "integer":
                    return "int";
                case "bigint":
                    return "bigint";
                case "smallint":
                    return "smallint";
                case "boolean":
                    return "bool";
                case "character varying":
                    return "varchar";
                case "character":
                    return "char";
                case "timestamp without time zone":
                case "timestamp with time zone":
                    return "datetime";
                case "double precision":
                    return "double";
                default:
                    return string.IsNullOrWhiteSpace(dataType) ? "text" : lowered;
            }
        }

        private string BuildColumnDefinition(Column column)
        {
            bool autoIncrement = IsAutoIncrementPrimaryKey(column);
            if (autoIncrement)
            {
                string baseType = ExtractBaseType(column.Type).ToLowerInvariant();
                string identityType = baseType == "bigint" ? "BIGINT" : "INTEGER";
                return QuoteIdentifier(column.Name) + " " + identityType + " GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY";
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
                    return "INTEGER";
                case "bigint":
                    return "BIGINT";
                case "smallint":
                    return "SMALLINT";
                case "bool":
                case "boolean":
                case "bit":
                    return "BOOLEAN";
                case "decimal":
                case "numeric":
                    return "NUMERIC";
                case "double":
                case "float":
                    return "DOUBLE PRECISION";
                case "real":
                    return "REAL";
                case "date":
                    return "DATE";
                case "time":
                    return "TIME";
                case "datetime":
                case "datetime2":
                case "timestamp":
                    return "TIMESTAMP";
                case "uuid":
                case "guid":
                case "uniqueidentifier":
                    return "UUID";
                case "blob":
                case "binary":
                case "varbinary":
                case "bytea":
                    return "BYTEA";
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
