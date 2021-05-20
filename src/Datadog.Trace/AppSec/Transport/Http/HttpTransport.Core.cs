#if !NETFRAMEWORK
using Datadog.Trace.DiagnosticListeners;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.Transport.Http
{
    internal class HttpTransport : ITransport
    {
        private readonly HttpContext context;

        public HttpTransport(HttpContext context)
        {
            this.context = context;
        }

        public void Block()
        {
            if (context.Items.ContainsKey(SecurityConstants.InHttpPipeKey) && context.Items[SecurityConstants.InHttpPipeKey] is bool inHttpPipe && inHttpPipe)
            {
                throw new BlockActionException();
            }
            else
            {
                context.Items[SecurityConstants.KillKey] = true;
            }
        }
    }
}
#endif
