namespace RestDb.Storage.Providers.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// MySQL query builder.
    /// </summary>
    internal class MysqlQueryBuilder : ProviderQueryBuilderBase
    {
        /// <inheritdoc />
        public override string ProviderName => "Mysql";

        /// <inheritdoc />
        public override SqlQueryDefinition BuildListTables()
        {
            return new SqlQueryDefinition(
                "SELECT table_name " +
                "FROM information_schema.tables " +
                "WHERE table_schema = DATABASE() " +
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
                "CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END AS is_nullable, " +
                "c.character_maximum_length AS max_length, " +
                "CASE WHEN c.column_key = 'PRI' THEN 1 ELSE 0 END AS primary_key " +
                "FROM information_schema.columns c " +
                "WHERE c.table_schema = DATABASE() " +
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
            InsertPlan plan = new InsertPlan();
            plan.Batch.UseTransaction = true;
            plan.Batch.Queries.Add(BuildInsertStatement(tableName, columns, values));

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
                        " WHERE " + QuoteIdentifier(primaryKey.Name) + " = LAST_INSERT_ID();"));
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
                parts[i] = "`" + parts[i].Replace("`", "``") + "`";
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
                builder.Append(" LIMIT 18446744073709551615 OFFSET ");
                builder.Append(query.AddParameter(offset));
            }
        }

        /// <inheritdoc />
        protected override string GetLikeEscapeClause()
        {
            return string.Empty;
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
                case "tinyint":
                    return "bool";
                case "varchar":
                    return "varchar";
                case "datetime":
                    return "datetime";
                default:
                    return string.IsNullOrWhiteSpace(dataType) ? "varchar" : lowered;
            }
        }

        private string BuildColumnDefinition(Column column)
        {
            bool autoIncrement = IsAutoIncrementPrimaryKey(column);
            if (autoIncrement)
            {
                string baseType = ExtractBaseType(column.Type).ToLowerInvariant();
                string identityType = baseType == "bigint" ? "BIGINT" : "INT";
                return QuoteIdentifier(column.Name) + " " + identityType + " NOT NULL AUTO_INCREMENT PRIMARY KEY";
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
                    return "BOOLEAN";
                case "decimal":
                case "numeric":
                    return "DECIMAL(18,4)";
                case "double":
                case "real":
                    return "DOUBLE";
                case "float":
                    return "FLOAT";
                case "date":
                    return "DATE";
                case "time":
                    return "TIME";
                case "datetime":
                case "datetime2":
                case "timestamp":
                    return "DATETIME";
                case "uuid":
                case "guid":
                case "uniqueidentifier":
                    return "CHAR(36)";
                case "binary":
                case "varbinary":
                    return length.HasValue ? "VARBINARY(" + length.Value + ")" : "LONGBLOB";
                case "blob":
                case "bytea":
                    return "LONGBLOB";
                case "char":
                case "nchar":
                    return "CHAR(" + (length ?? 1) + ")";
                case "varchar":
                case "nvarchar":
                case "string":
                    return length.HasValue ? "VARCHAR(" + length.Value + ")" : "LONGTEXT";
                case "text":
                    return "LONGTEXT";
                default:
                    return column.Type.ToUpperInvariant();
            }
        }
    }
}
