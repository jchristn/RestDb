﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ExpressionTree;

namespace RestDb.Classes
{
    internal static class SerializationHelper
    {
        internal static string SerializeJson(object obj, bool pretty)
        {
            if (obj == null) return null;
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                  obj,
                  Newtonsoft.Json.Formatting.Indented,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                      Converters = new List<JsonConverter> { new StringEnumConverter() } 
                  });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                      Converters = new List<JsonConverter> { new StringEnumConverter() }
                  });
            }

            return json;
        }

        internal static T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));
            return JsonConvert.DeserializeObject<T>(
                json,
                new JsonSerializerSettings
                {
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                    Converters = new List<JsonConverter> { new StringEnumConverter() }
                });
        }

        internal static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return DeserializeJson<T>(Encoding.UTF8.GetString(data));
        }

        internal static T CopyObject<T>(object o)
        {
            if (o == null) return default(T);
            string json = SerializeJson(o, false);
            T ret = DeserializeJson<T>(json);
            return ret;
        }

        internal static Expr DeserializeJsonExpression(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonConvert.DeserializeObject<Expr>(
                Encoding.UTF8.GetString(data),
                new JsonSerializerSettings
                {
                    Converters = DeserializationConverters,
                    TypeNameHandling = TypeNameHandling.All
                }
            );
        }

        private static JsonConverter[] DeserializationConverters = 
        { 
            new ExpressionConverter(), 
            new StringEnumConverter() 
        };
    }
}
