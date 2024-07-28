namespace RestDb.Classes
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Converters;
    using ExpressionTree;

    /// <summary>
    /// Expression converter for JSON.NET.
    /// </summary>
    public class ExpressionConverter : JsonConverter
    {
        /// <summary>
        /// Can convert.
        /// </summary>
        /// <param name="objectType">Type.</param>
        /// <returns>Boolean.</returns>
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(Expr));
        }

        /// <summary>
        /// Read JSON.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="objectType">Object type.</param>
        /// <param name="existingValue">Existing value.</param>
        /// <param name="serializer">Serializer.</param>
        /// <returns>Object.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // see https://blog.codeinside.eu/2015/03/30/json-dotnet-deserialize-to-abstract-class-or-interface/

            Expr ret = new Expr();

            JObject jo = JObject.Load(reader);

            // Console.WriteLine("--- In Converter ---");
            // Console.WriteLine("Object   : " + jo.ToString());
            // Console.WriteLine("Left     : " + jo["Left"].ToString());
            // Console.WriteLine("Operator : " + jo["Operator"].ToString());
            // Console.WriteLine("Right    : " + jo["Right"].ToString());

            if (jo["Left"] != null)
            {
                JToken leftToken = jo["Left"];
                if (leftToken.Type == JTokenType.Object)
                {
                    // Console.WriteLine("Left: Object (recursively calling expression converter)");
                    ret.Left = JsonConvert.DeserializeObject<Expr>(
                        leftToken.ToString(),
                        new JsonSerializerSettings
                        {
                            Converters = new JsonConverter[] { new ExpressionConverter(), new StringEnumConverter() },
                            TypeNameHandling = TypeNameHandling.All
                        }
                    );
                }
                else if (leftToken.Type == JTokenType.Array)
                {
                    // Console.WriteLine("Left: Array");
                    ret.Left = leftToken.ToObject<List<object>>();
                }
                else if (leftToken.Type == JTokenType.Integer)
                {
                    // Console.WriteLine("Left: Integer");
                    ret.Left = leftToken.ToObject<decimal>();
                }
                else
                {
                    // Console.WriteLine("Left: String");
                    ret.Left = leftToken.ToObject<string>();
                }
            }

            ret.Operator = (OperatorEnum)(Enum.Parse(typeof(OperatorEnum), jo["Operator"].ToString()));

            if (jo["Right"] != null)
            {
                JToken rightToken = jo["Right"];
                if (rightToken.Type == JTokenType.Object)
                {
                    // Console.WriteLine("Right: Object (recursively calling expression converter)");
                    ret.Right = JsonConvert.DeserializeObject<Expr>(
                        rightToken.ToString(),
                        new JsonSerializerSettings
                        {
                            Converters = new JsonConverter[] { new ExpressionConverter(), new StringEnumConverter() },
                            TypeNameHandling = TypeNameHandling.All
                        }
                    );
                }
                else if (rightToken.Type == JTokenType.Array)
                {
                    // Console.WriteLine("Right: Array");
                    ret.Right = rightToken.ToObject<List<object>>();
                }
                else if (rightToken.Type == JTokenType.Integer)
                {
                    // Console.WriteLine("Right: Integer");
                    ret.Right = rightToken.ToObject<decimal>();
                }
                else
                {
                    // Console.WriteLine("Right: String");
                    ret.Right = rightToken.ToObject<string>();
                }
            }

            return ret;
        }

        /// <summary>
        /// Can write.
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Write JSON.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="serializer">Serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
