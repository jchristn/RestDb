using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;
using WatsonWebserver;

namespace RestDb
{
    partial class RestDbServer
    {
        static HttpResponse GetDatabases(HttpRequest req)
        {
            return new HttpResponse(req, true, 200, null, null, 
                Common.SerializeJson(_Settings.DatabaseNames(), true), true);
        }
    }
}
