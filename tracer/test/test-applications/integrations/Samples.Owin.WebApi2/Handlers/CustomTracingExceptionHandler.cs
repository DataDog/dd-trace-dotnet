using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;

namespace Samples.Owin.WebApi2.Handlers
{
    public class CustomTracingExceptionHandler : ExceptionHandler
    {
        public override async Task HandleAsync(ExceptionHandlerContext context, CancellationToken cancellationToken)
        {
            using (var scope = SampleHelpers.CreateScope("CustomTracingExceptionHandler.handle-async"))
            {
                // Set span kind of span to server to pass through server span filtering
                SampleHelpers.TrySetTag(scope, "span.kind", "server");

                await base.HandleAsync(context, cancellationToken);
            }
        }
    }
}
