using System.Diagnostics;
using System.Linq;
using System.Web;
using NGenInlinerCollision;

namespace Samples.AspNet.NGenNameHash
{
    public class HttpHandler : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            // System.Core is NGen'd, so calling it also raises the CLR callbacks that look up NGen inliners.
            var result = Enumerable.Range(0, 3).Select(Targets.M0001).Sum();

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.Write($"{{\"Pid\":{Process.GetCurrentProcess().Id},\"Result\":{result}}}");
        }
    }
}
