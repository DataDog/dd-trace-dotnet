using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.ExceptionHandling;
using ActivitySampleHelper;

namespace Samples.Owin.WebApi2.Handlers
{
    public class CustomTracingExceptionHandler : ExceptionHandler
    {
        private static readonly ActivitySourceHelper _sampleHelpers = new(nameof(CustomTracingExceptionHandler));

        public override async Task HandleAsync(ExceptionHandlerContext context, CancellationToken cancellationToken)
        {
            using (var scope = _sampleHelpers.CreateScope("CustomTracingExceptionHandler.handle-async"))
            {
                // Set span kind of span to server to pass through server span filtering
                _sampleHelpers.TrySetTag(scope, "span.kind", "server");

                await base.HandleAsync(context, cancellationToken);
            }
        }
    }
}
