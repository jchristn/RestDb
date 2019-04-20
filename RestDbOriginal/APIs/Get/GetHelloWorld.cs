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
        static HttpResponse GetHelloWorld(HttpRequest req)
        {
            string ret =
                "<html>" +
                " <head>" +
                "  <title>RestDb</title>" +
                " </head>" +
                " <body>" +
                "  <p>" +
                "   <h3>RestDb</h3>" +
                "   <p>Your RestDb instance is operational!</p>" +
                "  </p>" +
                " </body>" +
                "</html>";

            return new HttpResponse(req, true, 200, null, "text/html", ret, true);
        }
    }
}
