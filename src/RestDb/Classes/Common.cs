namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    internal static class Common
    { 
        #region Conversion

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

        #endregion

        #region Misc

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

        #endregion
    }
}
