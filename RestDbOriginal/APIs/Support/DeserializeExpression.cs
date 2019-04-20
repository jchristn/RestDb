using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DatabaseWrapper;
using WatsonWebserver;
using SyslogLogging;

namespace RestDb
{
    public partial class RestDbServer
    {
        public static Expression DeserializeExpression(byte[] data, bool verbose)
        {
            if (data == null || data.Length < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "DeserializeExpression null or empty byte data supplied");
                return null;
            }

            return DeserializeExpression(Encoding.UTF8.GetString(data), verbose);
        }

        public static Expression DeserializeExpression(string data, bool verbose)
        {
            if (data == null || data.Length < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "DeserializeExpression null or empty string data supplied");
                return null;
            }

            Expression deser = Common.DeserializeJson<Expression>(data);

            Expression ret = new Expression();

            if (verbose) Console.WriteLine("---");

            if (deser.LeftTerm != null)
            {
                if (deser.LeftTerm is JObject)
                {
                    if (verbose) Console.WriteLine("LeftTerm is JObject");
                    JObject temp = (JObject)deser.LeftTerm;
                    ret.LeftTerm = ParseExpressionJObject(temp, verbose);
                }
                else if (deser.LeftTerm is Dictionary<string, object>)
                {
                    if (verbose) Console.WriteLine("LeftTerm is Dict");
                    Dictionary<string, object> temp = (Dictionary<string, object>)deser.LeftTerm;
                    ret.LeftTerm = ParseExpressionDict(temp, verbose);
                }
                else if (deser.LeftTerm is string || deser.LeftTerm is int || deser.LeftTerm is long || deser.LeftTerm is decimal)
                {
                    ret.LeftTerm = deser.LeftTerm.ToString();
                }
                else
                {
                    if (verbose) Console.WriteLine("Unable to discern type of LeftTerm: " + deser.LeftTerm.GetType());
                }

                if (verbose)
                {
                    if (ret.LeftTerm != null) Console.WriteLine("LeftTerm after parse: " + ret.LeftTerm.ToString());
                    else Console.WriteLine("LeftTerm after parse: (null)");
                }
            }
            else
            {
                if (verbose) Console.WriteLine("LeftTerm after parse: (null)");
            }

            ret.Operator = deser.Operator;

            if (deser.RightTerm != null)
            {
                if (deser.RightTerm is JObject)
                {
                    if (verbose) Console.WriteLine("RightTerm is JObject");
                    JObject temp = (JObject)deser.RightTerm;
                    ret.RightTerm = ParseExpressionJObject(temp, verbose);
                }
                else if (deser.RightTerm is Dictionary<string, object>)
                {
                    if (verbose) Console.WriteLine("RightTerm is Dict");
                    Dictionary<string, object> temp = (Dictionary<string, object>)deser.RightTerm;
                    ret.RightTerm = ParseExpressionDict(temp, verbose);
                }
                else if (deser.RightTerm is string || deser.RightTerm is int || deser.RightTerm is long || deser.RightTerm is decimal)
                {
                    ret.RightTerm = deser.RightTerm.ToString();
                }
                else
                {
                    if (verbose) Console.WriteLine("Unable to discern type of RightTerm: " + deser.RightTerm.GetType());
                }

                if (verbose)
                {
                    if (ret.RightTerm != null) Console.WriteLine("RightTerm after parse: " + ret.RightTerm.ToString());
                    else Console.WriteLine("RightTerm after parse: (null)");
                }
            }
            else
            {
                if (verbose) Console.WriteLine("RightTerm after parse: (null)");
            }

            return ret;
        }

        private static Expression ParseExpressionJObject(JObject data, bool verbose)
        {
            if (data == null) return null;

            if (verbose) Console.WriteLine("---");

            Expression ret = data.ToObject<Expression>();
            if (ret.LeftTerm is JObject)
            {
                if (verbose) Console.WriteLine("LeftTerm is JObject");
                JObject temp = (JObject)ret.LeftTerm;
                ret.LeftTerm = ParseExpressionJObject(temp, verbose);
            }
            else if (ret.LeftTerm is Dictionary<string, object>)
            {
                if (verbose) Console.WriteLine("LeftTerm is Dict");
                Dictionary<string, object> temp = (Dictionary<string, object>)ret.LeftTerm;
                ret.LeftTerm = ParseExpressionDict(temp, verbose);
            }
            else
            {
                //
                // do nothing, it's already set
                //
            }
            if (verbose) Console.WriteLine("LeftTerm after parse: " + ret.LeftTerm.ToString());
            //
            // ret.Operator is already set via the .ToObject<Expression>() above
            //
            if (ret.RightTerm is JObject)
            {
                if (verbose) Console.WriteLine("RightTerm is JObject");
                JObject temp = (JObject)ret.RightTerm;
                ret.RightTerm = ParseExpressionJObject(temp, verbose);
            }
            else if (ret.RightTerm is Dictionary<string, object>)
            {
                if (verbose) Console.WriteLine("RightTerm is Dict");
                Dictionary<string, object> temp = (Dictionary<string, object>)ret.RightTerm;
                ret.RightTerm = ParseExpressionDict(temp, verbose);
            }
            else
            {
                //
                // do nothing, it's already set
                //
            }

            if (verbose) Console.WriteLine("Final: " + ret.ToString());
            return ret;
        }

        private static Expression ParseExpressionDict(Dictionary<string, object> data, bool verbose)
        {
            if (data == null) return null;

            if (verbose) Console.WriteLine("---");

            Expression ret = Common.DeserializeJson<Expression>(Common.SerializeJson(data, false));
            if (ret.LeftTerm is JObject)
            {
                if (verbose) Console.WriteLine("LeftTerm is JObject");
                JObject temp = (JObject)ret.LeftTerm;
                ret.LeftTerm = ParseExpressionJObject(temp, verbose);
            }
            else if (ret.LeftTerm is Dictionary<string, object>)
            {
                if (verbose) Console.WriteLine("LeftTerm is Dict");
                Dictionary<string, object> temp = (Dictionary<string, object>)ret.LeftTerm;
                ret.LeftTerm = ParseExpressionDict(temp, verbose);
            }
            else
            {
                //
                // do nothing, it's already set
                //
            }
            if (verbose) Console.WriteLine("LeftTerm after parse: " + ret.LeftTerm.ToString());
            //
            // ret.Operator is already set via the .ToObject<Expression>() above
            //
            if (ret.RightTerm is JObject)
            {
                if (verbose) Console.WriteLine("RightTerm is JObject");
                JObject temp = (JObject)ret.RightTerm;
                ret.RightTerm = ParseExpressionJObject(temp, verbose);
            }
            else if (ret.RightTerm is Dictionary<string, object>)
            {
                if (verbose) Console.WriteLine("RightTerm is Dict");
                Dictionary<string, object> temp = (Dictionary<string, object>)ret.RightTerm;
                ret.RightTerm = ParseExpressionDict(temp, verbose);
            }
            else
            {
                //
                // do nothing, it's already set
                //
            }

            if (verbose) Console.WriteLine("Final: " + ret.ToString());
            return ret;
        }
    }
}
