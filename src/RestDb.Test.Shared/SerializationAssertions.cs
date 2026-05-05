namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Text;
using ExpressionTree;
using RestDb;
using RestDb.Classes;

internal static class SerializationAssertions
{
    public static void TablePayloadRoundtripsWithExpectedPropertyNames()
    {
        Table table = new Table
        {
            Name = "person",
            PrimaryKey = "person_id",
            Columns = new List<Column>
            {
                new Column { Name = "person_id", Type = "int", Nullable = false, PrimaryKey = true },
                new Column { Name = "first_name", Type = "nvarchar", MaxLength = 32, Nullable = false },
                new Column { Name = "last_name", Type = "nvarchar", MaxLength = 32, Nullable = true },
                new Column { Name = "created", Type = "datetime", Nullable = true }
            }
        };

        string json = SerializationHelper.SerializeJson(table, true);
        Table roundTrip = SerializationHelper.DeserializeJson<Table>(json);

        TestAssert.Contains("\"Name\": \"person\"", json, StringComparison.Ordinal);
        TestAssert.Contains("\"PrimaryKey\": \"person_id\"", json, StringComparison.Ordinal);
        TestAssert.Contains("\"Columns\"", json, StringComparison.Ordinal);
        TestAssert.Equal("person", roundTrip.Name);
        TestAssert.Equal("person_id", roundTrip.PrimaryKey);
        TestAssert.Equal(4, roundTrip.Columns.Count);
        TestAssert.Contains(roundTrip.Columns, c => c.Name == "person_id" && c.PrimaryKey);
        TestAssert.Contains(roundTrip.Columns, c => c.Name == "first_name" && c.MaxLength == 32 && !c.Nullable);
    }

    public static void SearchExpressionPayloadDeserializesNestedExpressionTree()
    {
        string json =
            "{" +
            "\"Left\":{" +
            "\"Left\":\"age\"," +
            "\"Operator\":\"In\"," +
            "\"Right\":[18,19]" +
            "}," +
            "\"Operator\":\"Or\"," +
            "\"Right\":{" +
            "\"Left\":{\"Left\":\"created\",\"Operator\":\"IsNotNull\"}," +
            "\"Operator\":\"And\"," +
            "\"Right\":{\"Left\":\"last_name\",\"Operator\":\"StartsWith\",\"Right\":\"Chr\"}" +
            "}" +
            "}";

        Expr expr = SerializationHelper.DeserializeJsonExpression(Encoding.UTF8.GetBytes(json));

        TestAssert.NotNull(expr);
        TestAssert.Equal(OperatorEnum.Or, expr.Operator);

        Expr left = TestAssert.IsType<Expr>(expr.Left);
        TestAssert.Equal(OperatorEnum.In, left.Operator);
        List<object> leftValues = TestAssert.IsType<List<object>>(left.Right);
        TestAssert.Equal(2, leftValues.Count);

        Expr right = TestAssert.IsType<Expr>(expr.Right);
        TestAssert.Equal(OperatorEnum.And, right.Operator);

        Expr rightLeft = TestAssert.IsType<Expr>(right.Left);
        TestAssert.Equal(OperatorEnum.IsNotNull, rightLeft.Operator);
        TestAssert.Equal("created", TestAssert.IsType<string>(rightLeft.Left));

        Expr rightRight = TestAssert.IsType<Expr>(right.Right);
        TestAssert.Equal(OperatorEnum.StartsWith, rightRight.Operator);
        TestAssert.Equal("last_name", TestAssert.IsType<string>(rightRight.Left));
        TestAssert.Equal("Chr", TestAssert.IsType<string>(rightRight.Right));
    }
}
