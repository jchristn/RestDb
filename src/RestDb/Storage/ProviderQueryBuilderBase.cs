namespace RestDb.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ExpressionTree;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using RestDb.Storage.Interfaces;

    /// <summary>
    /// Common provider query builder functionality.
    /// </summary>
    internal abstract class ProviderQueryBuilderBase : IRestDbQueryBuilder
    {
        /// <inheritdoc />
        public abstract string ProviderName { get; }

        /// <inheritdoc />
        public virtual List<string> ReadTableNames(DataTable result)
        {
            List<string> ret = new List<string>();
            if (result == null || result.Rows.Count < 1) return ret;

            foreach (DataRow row in result.Rows)
            {
                if (result.Columns.Count < 1) continue;
                string tableName = row[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(tableName)) ret.Add(tableName);
            }

            return ret;
        }

        /// <inheritdoc />
        public virtual List<Column> ReadColumns(DataTable result)
        {
            List<Column> ret = new List<Column>();
            if (result == null || result.Rows.Count < 1) return ret;

            foreach (DataRow row in result.Rows)
            {
                Column col = new Column
                {
                    Name = ReadString(row, "column_name"),
                    Type = NormalizeTypeForResponse(ReadString(row, "data_type")),
                    Nullable = ReadBoolean(row, "is_nullable", true),
                    MaxLength = ReadNullableInt(row, "max_length"),
                    PrimaryKey = ReadBoolean(row, "primary_key", false)
                };

                if (!string.IsNullOrWhiteSpace(col.Name))
                {
                    ret.Add(col);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public abstract SqlQueryDefinition BuildListTables();

        /// <inheritdoc />
        public abstract SqlQueryDefinition BuildDescribeTable(string tableName);

        /// <inheritdoc />
        public abstract SqlQueryDefinition BuildCreateTable(string tableName, List<Column> columns);

        /// <inheritdoc />
        public abstract SqlQueryDefinition BuildClearTable(string tableName);

        /// <inheritdoc />
        public abstract SqlQueryDefinition BuildDropTable(string tableName);

        /// <inheritdoc />
        public virtual SqlQueryDefinition BuildSelect(
            string tableName,
            List<Column> columns,
            int? indexStart,
            int? maxResults,
            List<string> returnFields,
            Expr filter,
            ResultOrder[] resultOrder)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));

            SqlQueryDefinition query = new SqlQueryDefinition();
            string projection = BuildProjection(columns, returnFields);
            string whereClause = BuildWhereClause(columns, filter, query);
            string orderClause = BuildOrderClause(columns, resultOrder, indexStart != null || maxResults != null);
            int offset = Math.Max(0, (indexStart ?? 1) - 1);

            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT ");
            sb.Append(projection);
            sb.Append(" FROM ");
            sb.Append(QuoteIdentifier(tableName));

            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                sb.Append(" WHERE ");
                sb.Append(whereClause);
            }

            if (!string.IsNullOrWhiteSpace(orderClause))
            {
                sb.Append(" ORDER BY ");
                sb.Append(orderClause);
            }

            AppendPagination(sb, query, offset, maxResults, !string.IsNullOrWhiteSpace(orderClause));
            sb.Append(";");
            query.CommandText = sb.ToString();
            return query;
        }

        /// <inheritdoc />
        public virtual InsertPlan BuildInsert(string tableName, List<Column> columns, Dictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (values == null) throw new ArgumentNullException(nameof(values));

            InsertPlan plan = new InsertPlan();
            plan.Batch.Queries.Add(BuildInsertStatement(tableName, columns, values));
            plan.Batch.UseTransaction = true;
            plan.ReturnsInsertedRow = false;
            return plan;
        }

        /// <inheritdoc />
        public virtual SqlBatchDefinition BuildInsertMultiple(string tableName, List<Column> columns, List<Dictionary<string, object>> valuesList)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (valuesList == null) throw new ArgumentNullException(nameof(valuesList));

            SqlBatchDefinition batch = new SqlBatchDefinition();
            batch.UseTransaction = true;

            foreach (Dictionary<string, object> values in valuesList)
            {
                batch.Queries.Add(BuildInsertStatement(tableName, columns, values));
            }

            return batch;
        }

        /// <inheritdoc />
        public virtual SqlQueryDefinition BuildUpdate(string tableName, List<Column> columns, Dictionary<string, object> values, Expr filter)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count < 1) throw new ArgumentException("No values supplied.", nameof(values));

            SqlQueryDefinition query = new SqlQueryDefinition();
            List<string> setClauses = new List<string>();

            foreach (KeyValuePair<string, object> kvp in values)
            {
                Column actualColumn = ResolveColumn(columns, kvp.Key);
                string param = query.AddParameter(NormalizeValueForColumn(actualColumn, kvp.Value));
                setClauses.Add(QuoteIdentifier(actualColumn.Name) + " = " + param);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("UPDATE ");
            sb.Append(QuoteIdentifier(tableName));
            sb.Append(" SET ");
            sb.Append(string.Join(", ", setClauses));

            string whereClause = BuildWhereClause(columns, filter, query);
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                sb.Append(" WHERE ");
                sb.Append(whereClause);
            }

            sb.Append(";");
            query.CommandText = sb.ToString();
            return query;
        }

        /// <inheritdoc />
        public virtual SqlQueryDefinition BuildDelete(string tableName, List<Column> columns, Expr filter)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));

            SqlQueryDefinition query = new SqlQueryDefinition();
            StringBuilder sb = new StringBuilder();
            sb.Append("DELETE FROM ");
            sb.Append(QuoteIdentifier(tableName));

            string whereClause = BuildWhereClause(columns, filter, query);
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                sb.Append(" WHERE ");
                sb.Append(whereClause);
            }

            sb.Append(";");
            query.CommandText = sb.ToString();
            return query;
        }

        /// <inheritdoc />
        public virtual SqlQueryDefinition BuildRawSql(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));
            return new SqlQueryDefinition(query);
        }

        /// <summary>
        /// Quote an identifier.
        /// </summary>
        /// <param name="identifier">Identifier.</param>
        /// <returns>Quoted identifier.</returns>
        protected abstract string QuoteIdentifier(string identifier);

        /// <summary>
        /// Append provider-specific pagination.
        /// </summary>
        /// <param name="builder">Builder.</param>
        /// <param name="query">Query.</param>
        /// <param name="offset">Offset.</param>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="hasOrderClause">Whether an ORDER BY exists.</param>
        protected abstract void AppendPagination(StringBuilder builder, SqlQueryDefinition query, int offset, int? maxResults, bool hasOrderClause);

        /// <summary>
        /// Normalize a described column type for API responses.
        /// </summary>
        /// <param name="dataType">Data type.</param>
        /// <returns>Normalized type.</returns>
        protected abstract string NormalizeTypeForResponse(string dataType);

        /// <summary>
        /// Retrieve any provider-specific LIKE escape clause suffix.
        /// </summary>
        /// <returns>Escape clause suffix.</returns>
        protected virtual string GetLikeEscapeClause()
        {
            return " ESCAPE '\\'";
        }

        /// <summary>
        /// Build a plain insert statement without readback queries.
        /// </summary>
        /// <param name="tableName">Table.</param>
        /// <param name="columns">Columns.</param>
        /// <param name="values">Values.</param>
        /// <returns>Query definition.</returns>
        protected SqlQueryDefinition BuildInsertStatement(string tableName, List<Column> columns, Dictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count < 1) throw new ArgumentException("No values supplied.", nameof(values));

            SqlQueryDefinition query = new SqlQueryDefinition();
            List<string> fieldNames = new List<string>();
            List<string> parameterNames = new List<string>();

            foreach (KeyValuePair<string, object> kvp in values)
            {
                Column actualColumn = ResolveColumn(columns, kvp.Key);
                fieldNames.Add(QuoteIdentifier(actualColumn.Name));
                parameterNames.Add(query.AddParameter(NormalizeValueForColumn(actualColumn, kvp.Value)));
            }

            query.CommandText =
                "INSERT INTO " + QuoteIdentifier(tableName) +
                " (" + string.Join(", ", fieldNames) + ")" +
                " VALUES (" + string.Join(", ", parameterNames) + ");";

            return query;
        }

        /// <summary>
        /// Build a select-by-primary-key statement.
        /// </summary>
        /// <param name="tableName">Table.</param>
        /// <param name="primaryKeyColumn">Primary key column.</param>
        /// <param name="primaryKeyValue">Primary key value.</param>
        /// <returns>Query definition.</returns>
        protected SqlQueryDefinition BuildSelectByPrimaryKeyStatement(string tableName, string primaryKeyColumn, object primaryKeyValue)
        {
            SqlQueryDefinition query = new SqlQueryDefinition();
            string pkParam = query.AddParameter(NormalizeValue(primaryKeyValue));
            query.CommandText =
                "SELECT * FROM " + QuoteIdentifier(tableName) +
                " WHERE " + QuoteIdentifier(primaryKeyColumn) +
                " = " + pkParam + ";";
            return query;
        }

        /// <summary>
        /// Determine if the column should be treated as auto-increment when used as the primary key.
        /// </summary>
        /// <param name="column">Column.</param>
        /// <returns>Boolean.</returns>
        protected bool IsAutoIncrementPrimaryKey(Column column)
        {
            if (column == null || !column.PrimaryKey || string.IsNullOrWhiteSpace(column.Type)) return false;
            string lowered = ExtractBaseType(column.Type).ToLowerInvariant();
            return lowered == "int"
                || lowered == "integer"
                || lowered == "bigint"
                || lowered == "smallint";
        }

        /// <summary>
        /// Resolve the primary key column if one exists.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <returns>Column.</returns>
        protected Column GetPrimaryKeyColumn(List<Column> columns)
        {
            if (columns == null) return null;
            return columns.FirstOrDefault(c => c != null && c.PrimaryKey);
        }

        /// <summary>
        /// Resolve a user-supplied column name against the table schema.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="requestedName">Requested name.</param>
        /// <returns>Actual name.</returns>
        protected string ResolveColumnName(List<Column> columns, string requestedName)
        {
            return ResolveColumn(columns, requestedName).Name;
        }

        /// <summary>
        /// Resolve a user-supplied column name against the table schema.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="requestedName">Requested name.</param>
        /// <returns>Column.</returns>
        protected Column ResolveColumn(List<Column> columns, string requestedName)
        {
            if (columns == null || columns.Count < 1) throw new ArgumentException("No table schema is available.", nameof(columns));
            if (string.IsNullOrWhiteSpace(requestedName)) throw new ArgumentNullException(nameof(requestedName));

            Column match = columns.FirstOrDefault(c => c != null && !string.IsNullOrWhiteSpace(c.Name) && c.Name.Equals(requestedName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new ArgumentException("Unknown column '" + requestedName + "'.", nameof(requestedName));
            }

            return match;
        }

        /// <summary>
        /// Build the SELECT projection.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="returnFields">Return fields.</param>
        /// <returns>Projection.</returns>
        protected string BuildProjection(List<Column> columns, List<string> returnFields)
        {
            if (returnFields == null || returnFields.Count < 1)
            {
                return "*";
            }

            List<string> fields = new List<string>();

            foreach (string curr in returnFields)
            {
                string actualColumn = ResolveColumnName(columns, curr);
                fields.Add(QuoteIdentifier(actualColumn));
            }

            return string.Join(", ", fields);
        }

        /// <summary>
        /// Build the ORDER BY clause.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="resultOrder">Requested ordering.</param>
        /// <param name="paginationRequested">Pagination requested.</param>
        /// <returns>Order by clause.</returns>
        protected string BuildOrderClause(List<Column> columns, ResultOrder[] resultOrder, bool paginationRequested)
        {
            List<string> orderClauses = new List<string>();

            if (resultOrder != null && resultOrder.Length > 0)
            {
                foreach (ResultOrder curr in resultOrder)
                {
                    if (curr == null || string.IsNullOrWhiteSpace(curr.Column)) continue;
                    string actualColumn = ResolveColumnName(columns, curr.Column);
                    orderClauses.Add(QuoteIdentifier(actualColumn) + " " + (curr.Direction == OrderDirectionEnum.Descending ? "DESC" : "ASC"));
                }
            }

            if (orderClauses.Count < 1 && paginationRequested && columns != null && columns.Count > 0)
            {
                Column primaryKey = GetPrimaryKeyColumn(columns);
                if (primaryKey != null)
                {
                    orderClauses.Add(QuoteIdentifier(primaryKey.Name) + " ASC");
                }
                else
                {
                    orderClauses.Add(QuoteIdentifier(columns[0].Name) + " ASC");
                }
            }

            if (orderClauses.Count < 1) return null;
            return string.Join(", ", orderClauses);
        }

        /// <summary>
        /// Build the WHERE clause.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="filter">Filter.</param>
        /// <param name="query">Query definition.</param>
        /// <returns>Where clause.</returns>
        protected string BuildWhereClause(List<Column> columns, Expr filter, SqlQueryDefinition query)
        {
            if (filter == null) return null;
            return BuildExpression(columns, filter, query);
        }

        private string BuildExpression(List<Column> columns, Expr expr, SqlQueryDefinition query)
        {
            if (expr == null) throw new ArgumentNullException(nameof(expr));

            switch (expr.Operator)
            {
                case OperatorEnum.And:
                    return "(" + BuildLogicalOperand(columns, expr.Left, query) + " AND " + BuildLogicalOperand(columns, expr.Right, query) + ")";
                case OperatorEnum.Or:
                    return "(" + BuildLogicalOperand(columns, expr.Left, query) + " OR " + BuildLogicalOperand(columns, expr.Right, query) + ")";
                case OperatorEnum.Equals:
                    return BuildComparison(columns, query, expr.Left, "=", expr.Right);
                case OperatorEnum.NotEquals:
                    return BuildComparison(columns, query, expr.Left, "<>", expr.Right);
                case OperatorEnum.GreaterThan:
                    return BuildComparison(columns, query, expr.Left, ">", expr.Right);
                case OperatorEnum.GreaterThanOrEqualTo:
                    return BuildComparison(columns, query, expr.Left, ">=", expr.Right);
                case OperatorEnum.LessThan:
                    return BuildComparison(columns, query, expr.Left, "<", expr.Right);
                case OperatorEnum.LessThanOrEqualTo:
                    return BuildComparison(columns, query, expr.Left, "<=", expr.Right);
                case OperatorEnum.IsNull:
                    return "(" + BuildIdentifierOperand(columns, expr.Left) + " IS NULL)";
                case OperatorEnum.IsNotNull:
                    return "(" + BuildIdentifierOperand(columns, expr.Left) + " IS NOT NULL)";
                case OperatorEnum.Contains:
                    if (IsEnumerableValue(expr.Right)) return BuildInClause(columns, query, expr.Left, expr.Right, false);
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, "%" + EscapeLikeValue(NormalizeString(expr.Right)) + "%", false);
                case OperatorEnum.ContainsNot:
                    if (IsEnumerableValue(expr.Right)) return BuildInClause(columns, query, expr.Left, expr.Right, true);
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, "%" + EscapeLikeValue(NormalizeString(expr.Right)) + "%", true);
                case OperatorEnum.StartsWith:
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, EscapeLikeValue(NormalizeString(expr.Right)) + "%", false);
                case OperatorEnum.StartsWithNot:
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, EscapeLikeValue(NormalizeString(expr.Right)) + "%", true);
                case OperatorEnum.EndsWith:
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, "%" + EscapeLikeValue(NormalizeString(expr.Right)), false);
                case OperatorEnum.EndsWithNot:
                    return BuildLikeClause(columns, query, expr.Left, expr.Right, "%" + EscapeLikeValue(NormalizeString(expr.Right)), true);
                case OperatorEnum.In:
                    return BuildInClause(columns, query, expr.Left, expr.Right, false);
                case OperatorEnum.NotIn:
                    return BuildInClause(columns, query, expr.Left, expr.Right, true);
                default:
                    throw new NotSupportedException("Unsupported operator: " + expr.Operator);
            }
        }

        private string BuildComparison(List<Column> columns, SqlQueryDefinition query, object left, string op, object right)
        {
            Column column = ResolveIdentifierColumn(columns, left);
            return "(" + QuoteIdentifier(column.Name) + " " + op + " " + BuildRightOperand(columns, query, column, right) + ")";
        }

        private string BuildLikeClause(List<Column> columns, SqlQueryDefinition query, object left, object right, string pattern, bool negate)
        {
            string leftOperand = QuoteIdentifier(ResolveIdentifierColumn(columns, left).Name);
            string param = query.AddParameter(pattern);
            return "(" + leftOperand + (negate ? " NOT LIKE " : " LIKE ") + param + GetLikeEscapeClause() + ")";
        }

        private string BuildInClause(List<Column> columns, SqlQueryDefinition query, object left, object right, bool negate)
        {
            Column column = ResolveIdentifierColumn(columns, left);
            string leftOperand = QuoteIdentifier(column.Name);
            List<object> values = ToEnumerableValues(right);
            if (values.Count < 1)
            {
                return negate ? "(1 = 1)" : "(1 = 0)";
            }

            List<string> parameters = new List<string>();
            foreach (object curr in values)
            {
                parameters.Add(query.AddParameter(NormalizeValueForColumn(column, curr)));
            }

            return "(" + leftOperand + (negate ? " NOT IN (" : " IN (") + string.Join(", ", parameters) + "))";
        }

        private string BuildLogicalOperand(List<Column> columns, object term, SqlQueryDefinition query)
        {
            if (term is Expr expr)
            {
                return BuildExpression(columns, expr, query);
            }

            throw new ArgumentException("Logical operators require nested expressions.");
        }

        private string BuildIdentifierOperand(List<Column> columns, object term)
        {
            return QuoteIdentifier(ResolveIdentifierColumn(columns, term).Name);
        }

        private string BuildRightOperand(List<Column> columns, SqlQueryDefinition query, Column column, object term)
        {
            if (term is Expr expr)
            {
                return "(" + BuildExpression(columns, expr, query) + ")";
            }

            return query.AddParameter(NormalizeValueForColumn(column, term));
        }

        private Column ResolveIdentifierColumn(List<Column> columns, object term)
        {
            if (!(term is string fieldName))
            {
                throw new ArgumentException("Left side of a comparison must be a field name.");
            }

            return ResolveColumn(columns, fieldName);
        }

        private List<object> ToEnumerableValues(object value)
        {
            List<object> ret = new List<object>();
            if (value == null) return ret;

            if (value is JArray jArray)
            {
                foreach (JToken token in jArray)
                {
                    ret.Add(NormalizeValue(token));
                }
                return ret;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (object curr in enumerable)
                {
                    ret.Add(NormalizeValue(curr));
                }
                return ret;
            }

            ret.Add(NormalizeValue(value));
            return ret;
        }

        private bool IsEnumerableValue(object value)
        {
            return value is JArray
                || (value is IEnumerable && !(value is string));
        }

        private string EscapeLikeValue(string value)
        {
            if (value == null) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[")
                .Replace("]", "\\]");
        }

        private string NormalizeString(object value)
        {
            object normalized = NormalizeValue(value);
            return normalized?.ToString() ?? string.Empty;
        }

        protected object NormalizeValue(object value)
        {
            if (value == null) return null;
            if (value is DBNull) return null;
            if (value is JValue jValue) return jValue.Value;
            if (value is JToken jToken) return jToken.ToString(Formatting.None);
            if (value is DateTime dateTime) return dateTime;
            if (value is MemoryStream memoryStream) return memoryStream.ToArray();
            return value;
        }

        protected object NormalizeValueForColumn(Column column, object value)
        {
            object normalized = NormalizeValue(value);
            if (column == null || normalized == null) return normalized;

            string baseType = ExtractBaseType(column.Type).ToLowerInvariant();

            if (normalized is string str)
            {
                if (TryConvertStringForColumn(baseType, str, out object convertedFromString))
                {
                    return convertedFromString;
                }

                return str;
            }

            if (TryConvertNonStringForColumn(baseType, normalized, out object converted))
            {
                return converted;
            }

            return normalized;
        }

        private bool TryConvertStringForColumn(string baseType, string value, out object converted)
        {
            converted = null;
            if (value == null) return false;

            switch (baseType)
            {
                case "int":
                case "integer":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                    {
                        converted = parsedInt;
                        return true;
                    }
                    break;
                case "bigint":
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong))
                    {
                        converted = parsedLong;
                        return true;
                    }
                    break;
                case "smallint":
                    if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short parsedShort))
                    {
                        converted = parsedShort;
                        return true;
                    }
                    break;
                case "bool":
                case "boolean":
                case "bit":
                    if (bool.TryParse(value, out bool parsedBool))
                    {
                        converted = parsedBool;
                        return true;
                    }

                    if (value == "1")
                    {
                        converted = true;
                        return true;
                    }

                    if (value == "0")
                    {
                        converted = false;
                        return true;
                    }
                    break;
                case "decimal":
                case "numeric":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedDecimal))
                    {
                        converted = parsedDecimal;
                        return true;
                    }
                    break;
                case "double":
                case "float":
                    if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedDouble))
                    {
                        converted = parsedDouble;
                        return true;
                    }
                    break;
                case "real":
                    if (float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsedFloat))
                    {
                        converted = parsedFloat;
                        return true;
                    }
                    break;
                case "date":
                case "time":
                case "datetime":
                case "datetime2":
                case "timestamp":
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out DateTime parsedDateTime)
                        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out parsedDateTime))
                    {
                        converted = parsedDateTime;
                        return true;
                    }
                    break;
                case "uuid":
                case "guid":
                case "uniqueidentifier":
                    if (Guid.TryParse(value, out Guid parsedGuid))
                    {
                        converted = parsedGuid;
                        return true;
                    }
                    break;
                case "blob":
                case "binary":
                case "varbinary":
                case "bytea":
                    try
                    {
                        converted = Convert.FromBase64String(value);
                        return true;
                    }
                    catch
                    {
                    }
                    break;
            }

            return false;
        }

        private bool TryConvertNonStringForColumn(string baseType, object value, out object converted)
        {
            converted = null;

            try
            {
                switch (baseType)
                {
                    case "int":
                    case "integer":
                        converted = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    case "bigint":
                        converted = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        return true;
                    case "smallint":
                        converted = Convert.ToInt16(value, CultureInfo.InvariantCulture);
                        return true;
                    case "bool":
                    case "boolean":
                    case "bit":
                        converted = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        return true;
                    case "decimal":
                    case "numeric":
                        converted = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                        return true;
                    case "double":
                    case "float":
                        converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return true;
                    case "real":
                        converted = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                        return true;
                    case "date":
                    case "time":
                    case "datetime":
                    case "datetime2":
                    case "timestamp":
                        if (value is DateTime dateTime)
                        {
                            converted = dateTime;
                            return true;
                        }
                        break;
                    case "uuid":
                    case "guid":
                    case "uniqueidentifier":
                        if (value is Guid guid)
                        {
                            converted = guid;
                            return true;
                        }
                        break;
                }
            }
            catch
            {
            }

            return false;
        }

        protected string ReadString(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            return row[columnName]?.ToString();
        }

        protected bool ReadBoolean(DataRow row, string columnName, bool defaultValue)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return defaultValue;

            object value = row[columnName];
            if (value is bool boolValue) return boolValue;
            if (value is byte byteValue) return byteValue != 0;
            if (value is short shortValue) return shortValue != 0;
            if (value is int intValue) return intValue != 0;

            string str = value.ToString();
            if (string.Equals(str, "YES", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(str, "NO", StringComparison.OrdinalIgnoreCase)) return false;
            if (bool.TryParse(str, out bool parsedBool)) return parsedBool;
            if (int.TryParse(str, out int parsedInt)) return parsedInt != 0;
            return defaultValue;
        }

        protected int? ReadNullableInt(DataRow row, string columnName)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value) return null;
            object value = row[columnName];
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (int.TryParse(value.ToString(), out int parsed)) return parsed;
            return null;
        }

        protected string ExtractBaseType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return string.Empty;
            int idx = typeName.IndexOf('(');
            if (idx >= 0) return typeName.Substring(0, idx).Trim();
            return typeName.Trim();
        }

        protected int? ExtractDeclaredLength(Column column)
        {
            if (column == null) return null;
            if (column.MaxLength.HasValue) return column.MaxLength.Value;
            if (string.IsNullOrWhiteSpace(column.Type)) return null;

            int leftParen = column.Type.IndexOf('(');
            int rightParen = column.Type.IndexOf(')');
            if (leftParen < 0 || rightParen <= leftParen) return null;

            string inner = column.Type.Substring(leftParen + 1, rightParen - leftParen - 1);
            string[] parts = inner.Split(',');
            if (parts.Length < 1) return null;
            if (int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
