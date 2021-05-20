#if NETFRAMEWORK

using System.Web;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly System.Web.HttpContext context;

        public HttpTransport(HttpContext context)
        {
            this.context = context;
        }

        public void Block()
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/html";
            context.Response.Write(SecurityConstants.AttackBlockedHtml);
            context.Response.Flush();
            context.Response.End();
        }
    }
}
#endif
