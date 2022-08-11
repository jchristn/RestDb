using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks; 
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json; 

namespace RestDb
{
    internal static class Common
    {
        #region Input

        internal static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        internal static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        internal static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }

        #endregion

        #region Conversion

        internal static List<int> CsvToIntList(string csv)
        {
            try
            {
                if (String.IsNullOrEmpty(csv)) return null;

                List<int> ret = new List<int>();

                string[] array = csv.Split(',');

                if (array != null)
                {
                    if (array.Length > 0)
                    {
                        foreach (string curr in array)
                        {
                            if (String.IsNullOrEmpty(curr)) continue;

                            int val = 0;
                            if (!Int32.TryParse(curr, out val))
                            {
                                return null;
                            }

                            ret.Add(val);
                        }

                        return ret;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static List<string> CsvToStringList(string csv)
        {
            try
            {
                if (String.IsNullOrEmpty(csv)) return null;

                List<string> ret = new List<string>();

                string[] array = csv.Split(',');

                if (array != null)
                {
                    if (array.Length > 0)
                    {
                        foreach (string curr in array)
                        {
                            if (String.IsNullOrEmpty(curr)) continue;
                            ret.Add(curr.Trim());
                        }

                        return ret;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static List<T> GenericToSpecificList<T>(List<object> source)
        {
            if (source == null) return null;

            List<T> retList = new List<T>();
            int count = 0;

            foreach (object curr in source)
            {
                retList.Add((T)curr);
                count++;
            }

            return retList;
        }

        internal static List<object> DataTableToListObject(DataTable dt, string objType)
        {
            //
            // Must pass in the fully-qualified class name including namespace
            //
            // i.e. Namespace.ClassName
            //

            if (dt == null) return null;
            if (dt.Rows.Count < 1) return null;
            if (String.IsNullOrEmpty(objType)) return null;

            List<object> retList = new List<object>();
            int count = 0;

            foreach (DataRow currRow in dt.Rows)
            {
                object ret = Activator.CreateInstance(Type.GetType(objType));
                if (ret == null) return null;

                foreach (PropertyInfo prop in ret.GetType().GetProperties())
                {
                    #region Process-Each-Property

                    PropertyInfo tempProp = prop;

                    switch (prop.PropertyType.ToString().ToLower().Trim())
                    {
                        case "system.string":
                            string valStr = currRow[prop.Name].ToString().Trim();
                            tempProp.SetValue(ret, valStr, null);
                            break;

                        case "system.int32":
                        case "system.nullable`1[system.int32]":
                            int valInt32 = 0;
                            if (Int32.TryParse(currRow[prop.Name].ToString(), out valInt32)) tempProp.SetValue(ret, valInt32, null);
                            break;

                        case "system.int64":
                        case "system.nullable`1[system.int64]":
                            long valInt64 = 0;
                            if (Int64.TryParse(currRow[prop.Name].ToString(), out valInt64)) tempProp.SetValue(ret, valInt64, null);
                            break;

                        case "system.decimal":
                        case "system.nullable`1[system.decimal]":
                            decimal valDecimal = 0m;
                            if (Decimal.TryParse(currRow[prop.Name].ToString(), out valDecimal)) tempProp.SetValue(ret, valDecimal, null);
                            break;

                        case "system.datetime":
                        case "system.nullable`1[system.datetime]":
                            DateTime datetime = DateTime.Now;
                            if (DateTime.TryParse(currRow[prop.Name].ToString(), out datetime)) tempProp.SetValue(ret, datetime, null);
                            break;

                        default:
                            break;
                    }

                    #endregion
                }

                count++;
                retList.Add(ret);
            }

            return retList;
        }

        internal static object DataTableToObject(DataTable dt, string objType)
        {
            if (dt == null) return null;
            if (dt.Rows.Count != 1) return null;
            if (String.IsNullOrEmpty(objType)) return null;

            object ret = new object();

            foreach (DataRow dr in dt.Rows)
            {
                ret = Activator.CreateInstance(Type.GetType(objType));
                if (ret == null)
                {
                    return null;
                }

                foreach (PropertyInfo prop in ret.GetType().GetProperties())
                {
                    PropertyInfo tempProp = prop;

                    switch (prop.PropertyType.ToString().ToLower().Trim())
                    {
                        case "system.string":
                            string valStr = dr[prop.Name].ToString().Trim();
                            tempProp.SetValue(ret, valStr, null);
                            break;

                        case "system.int32":
                        case "system.nullable`1[system.int32]":
                            int valInt32 = 0;
                            if (Int32.TryParse(dr[prop.Name].ToString(), out valInt32)) tempProp.SetValue(ret, valInt32, null);
                            break;

                        case "system.int64":
                        case "system.nullable`1[system.int64]":
                            long valInt64 = 0;
                            if (Int64.TryParse(dr[prop.Name].ToString(), out valInt64)) tempProp.SetValue(ret, valInt64, null);
                            break;

                        case "system.decimal":
                        case "system.nullable`1[system.decimal]":
                            decimal valDecimal = 0m;
                            if (Decimal.TryParse(dr[prop.Name].ToString(), out valDecimal)) tempProp.SetValue(ret, valDecimal, null);
                            break;

                        case "system.datetime":
                        case "system.nullable`1[system.datetime]":
                            DateTime datetime = DateTime.Now;
                            if (DateTime.TryParse(dr[prop.Name].ToString(), out datetime)) tempProp.SetValue(ret, datetime, null);
                            break;

                        default:
                            break;
                    }
                }

                break;
            }

            return ret;
        }

        internal static T DataTableToObject<T>(this DataTable table) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            IList<T> result = new List<T>();

            foreach (var row in table.Rows)
            {
                var item = CreateItemFromRow<T>((DataRow)row, properties);
                return item;
            }

            return default(T);
        }

        internal static IList<T> DataTableToList<T>(this DataTable table) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            IList<T> result = new List<T>();

            foreach (var row in table.Rows)
            {
                var item = CreateItemFromRow<T>((DataRow)row, properties);
                result.Add(item);
            }

            return result;
        }

        internal static IList<T> DataTableToList<T>(this DataTable table, Dictionary<string, string> mappings) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            IList<T> result = new List<T>();

            foreach (var row in table.Rows)
            {
                var item = CreateItemFromRow<T>((DataRow)row, properties, mappings);
                result.Add(item);
            }

            return result;
        }

        private static T CreateItemFromRow<T>(DataRow row, IList<PropertyInfo> properties) where T : new()
        {
            T item = new T();
            foreach (var property in properties)
            {
                if (row[property.Name] is System.DBNull) continue;
                property.SetValue(item, row[property.Name], null);
            }
            return item;
        }

        private static T CreateItemFromRow<T>(DataRow row, IList<PropertyInfo> properties, Dictionary<string, string> mappings) where T : new()
        {
            T item = new T();
            foreach (var property in properties)
            {
                if (mappings.ContainsKey(property.Name))
                    property.SetValue(item, row[mappings[property.Name]], null);
            }
            return item;
        }

        internal static List<dynamic> DataTableToListDynamic(DataTable dt)
        {
            List<dynamic> ret = new List<dynamic>();
            if (dt == null || dt.Rows.Count < 1) return ret;

            foreach (DataRow curr in dt.Rows)
            {
                dynamic dyn = new ExpandoObject();
                foreach (DataColumn col in dt.Columns)
                {
                    var dic = (IDictionary<string, object>)dyn;
                    dic[col.ColumnName] = curr[col];
                }
                ret.Add(dyn);
            }

            return ret;
        }

        internal static dynamic DataTableToDynamic(DataTable dt)
        {
            dynamic ret = new ExpandoObject();
            if (dt == null || dt.Rows.Count < 1) return ret;

            foreach (DataRow curr in dt.Rows)
            {
                foreach (DataColumn col in dt.Columns)
                {
                    var dic = (IDictionary<string, object>)ret;
                    dic[col.ColumnName] = curr[col];
                }

                return ret;
            }

            return ret;
        }

        internal static List<Dictionary<string, object>> DataTableToListDictionary(DataTable dt)
        {
            List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>();
            if (dt == null || dt.Rows.Count < 1) return ret;

            foreach (DataRow curr in dt.Rows)
            {
                Dictionary<string, object> currDict = new Dictionary<string, object>();

                foreach (DataColumn col in dt.Columns)
                {
                    currDict.Add(col.ColumnName, curr[col]);
                }

                ret.Add(currDict);
            }

            return ret;
        }

        internal static Dictionary<string, object> DataTableToDictionary(DataTable dt)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            if (dt == null || dt.Rows.Count < 1) return ret;

            foreach (DataRow curr in dt.Rows)
            {
                foreach (DataColumn col in dt.Columns)
                {
                    ret.Add(col.ColumnName, curr[col]);
                }

                return ret;
            }

            return ret;
        }

        internal static byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        #endregion

        #region Misc

        internal static string StringRemove(string original, string remove)
        {
            if (String.IsNullOrEmpty(original)) return null;
            if (String.IsNullOrEmpty(remove)) return original;

            int index = original.IndexOf(remove);
            string ret = (index < 0)
                ? original
                : original.Remove(index, remove.Length);

            return ret;
        }

        internal static string Line(int count, string fill)
        {
            if (count < 1) return "";

            string ret = "";
            for (int i = 0; i < count; i++)
            {
                ret += fill;
            }

            return ret;
        }

        internal static string RandomString(int num_char)
        {
            string ret = "";
            if (num_char < 1) return null;
            int valid = 0;
            Random random = new Random((int)DateTime.Now.Ticks);
            int num = 0;

            for (int i = 0; i < num_char; i++)
            {
                num = 0;
                valid = 0;
                while (valid == 0)
                {
                    num = random.Next(126);
                    if (((num > 47) && (num < 58)) ||
                        ((num > 64) && (num < 91)) ||
                        ((num > 96) && (num < 123)))
                    {
                        valid = 1;
                    }
                }
                ret += (char)num;
            }

            return ret;
        }

        internal static double TotalMsFrom(DateTime start_time)
        {
            try
            {
                DateTime end_time = DateTime.Now;
                TimeSpan total_time = (end_time - start_time);
                return total_time.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        internal static bool IsLaterThanNow(DateTime? dt)
        {
            try
            {
                DateTime curr = Convert.ToDateTime(dt);
                return Common.IsLaterThanNow(curr);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool IsLaterThanNow(DateTime dt)
        {
            if (DateTime.Compare(dt, DateTime.Now) > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Environment
         
        internal static void ExitApplication(string method, string text, int returnCode)
        {
            Console.WriteLine("---");
            Console.WriteLine("");
            Console.WriteLine("The application has exited.");
            Console.WriteLine("");
            Console.WriteLine("  Requested by : " + method);
            Console.WriteLine("  Reason text  : " + text);
            Console.WriteLine("");
            Console.WriteLine("---");
            Environment.Exit(returnCode);
            return;
        }

        internal static string GetPathSeparator(string environment)
        {
            if (String.IsNullOrEmpty(environment)) throw new ArgumentNullException(nameof(environment));

            switch (environment)
            {
                case "windows":
                    return @"\";

                case "mac":
                case "linux":
                    return "/";

                default:
                    throw new ArgumentException("Unknown environment: " + environment);
            }
        }

        #endregion

        #region Dictionary

        internal static Dictionary<string, string> AddToDictionary(string key, string val, Dictionary<string, string> existing)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            if (existing == null)
            {
                ret.Add(key, val);
                return ret;
            }
            else
            {
                existing.Add(key, val);
                return existing;
            }
        }

        #endregion
    }
}
