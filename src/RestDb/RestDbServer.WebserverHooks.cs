namespace RestDb
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    partial class RestDbServer
    {
        private static async Task PreRouting(HttpContextBase ctxBase)
        {
            HttpContext ctx = (HttpContext)ctxBase;
            ApplyCorsHeaders(ctx);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task PostRouting(HttpContextBase ctxBase)
        {
            HttpContext ctx = (HttpContext)ctxBase;
            ctx.Timestamp.End = DateTime.UtcNow;
            ApplyCorsHeaders(ctx);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task PreflightRoute(HttpContextBase ctxBase)
        {
            HttpContext ctx = (HttpContext)ctxBase;
            ApplyCorsHeaders(ctx);
            ctx.Response.StatusCode = 200;

            if (!ctx.Response.Headers.AllKeys.Contains("Allow"))
            {
                ctx.Response.Headers.Add("Allow", "GET, PUT, POST, DELETE, OPTIONS");
            }

            await ctx.Response.Send().ConfigureAwait(false);
        }
    }
}
