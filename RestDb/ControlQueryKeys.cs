using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestDb
{
    partial class RestDbServer
    {
        static List<string> _ControlQueryKeys = new List<string>
        {
            "_describe",        // indicates that table column details should be included
            "_index_start",     // starting point of the results to return
            "_max_results",     // maximum number of results to return
            "_order_by",        // how to order the results, must be URL-encoded
            "_return_fields",   // CSV list of fields to return
            "_truncate",        // bool to indicate that they want to truncate the table
            "_drop",            // bool to indicate that they want to drop the table
            "_debug"            // bool to debug certain queries
        };
    }
}
