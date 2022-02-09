using Datadog.Trace;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.ExceptionHandling;

namespace Samples.AspNetMvc5.Handlers
{
    public class CustomTracingExceptionHandler : ExceptionHandler
    {
        public override void Handle(ExceptionHandlerContext context)
        {
#if !OWIN
            if (HttpContext.Current.Items["ErrorStatusCode"] is int statusCode)
            {
                string path = $"~/api/statuscode/{statusCode}";
                HttpContext.Current.Server.TransferRequest(path, false, "GET", null);
                return;
            }
#endif

            using (var scope = Tracer.Instance.StartActive("CustomTracingExceptionHandler.handle-async"))
            {
                // Set span kind of span to server to pass through server span filtering
                scope.Span.SetTag(Tags.SpanKind, SpanKinds.Server);
            }
        }
    }
}
