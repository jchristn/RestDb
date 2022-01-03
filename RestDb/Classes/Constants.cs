using System;
using System.Collections.Generic;
using System.Text;

namespace RestDb.Classes
{
    internal class Constants
    {
        internal static string Logo = 
            Environment.NewLine +
            Environment.NewLine +
            @"                 _      _ _      " + Environment.NewLine +
            @"   _ __ ___  ___| |_ __| | |__   " + Environment.NewLine +
            @"  | '__/ _ \/ __| __/ _  |  _ \  " + Environment.NewLine +
            @"  | | |  __/\__ \ || (_| | |_) | " + Environment.NewLine +
            @"  |_|  \___||___/\__\__,_|_.__/  " + Environment.NewLine +
            Environment.NewLine +
            Environment.NewLine;

        internal static List<string> QueryKeys = new List<string>
        {
            "_md",
            "_describe",
            "_multiple",
            "_index",
            "_max",
            "_order_by",
            "_order",
            "_return",
            "_truncate",
            "_drop",
            "_debug"
        };

        internal static string QueryMetadata = "_md";
        
        internal static string QueryDescribe = "_describe";

        internal static string QueryMultiple = "_multiple";

        internal static string QueryIndexStart = "_index";
        
        internal static string QueryMaxResults = "_max";
        
        internal static string QueryOrderBy = "_order_by";
        
        internal static string QueryOrderDirection = "_order";
        
        internal static string QueryReturnFields = "_return";
        
        internal static string QueryTruncateTable = "_truncate";
        
        internal static string QueryDropTable = "_drop";
        
        internal static string QueryDebug = "_debug";

        internal static string HeaderExpression = "x-expression";
    }
}
