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

                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    HttpContext.Current.Server.TransferRequest(path, false, "GET", null);
                }
                else
                {
                    HttpContext.Current.Response.StatusCode = 500;
                }

                return;
            }
#endif

            using (var scope = SampleHelpers.CreateScope("CustomTracingExceptionHandler.handle-async"))
            {
                // Set span kind of span to server to pass through server span filtering
                SampleHelpers.TrySetTag(scope, "span.kind", "server");
            }
        }
    }
}
