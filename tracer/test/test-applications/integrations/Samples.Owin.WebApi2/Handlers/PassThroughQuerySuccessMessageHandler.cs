using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Owin.WebApi2.Handlers
{
    public class PassThroughQuerySuccessMessageHandler : DelegatingHandler
    {
        private readonly string SuccessQueryKey = "ps";
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string isSuccessString = "true";

            // Parse for the "ps" query string
            foreach (var kvp in request.GetQueryNameValuePairs())
            {
                if (string.Equals(kvp.Key, SuccessQueryKey, StringComparison.OrdinalIgnoreCase))
                {
                    isSuccessString = kvp.Value;
                }
            }

            var canParse = bool.TryParse(isSuccessString, out var isSuccess);

            // If true, call the inner handler
            if (canParse && isSuccess)
            {
                return await base.SendAsync(request, cancellationToken);
            }
            // If false, throw an exception
            else
            {
                throw new ArgumentException($"Source: {nameof(PassThroughQuerySuccessMessageHandler)}. Error: Query param {SuccessQueryKey} was set to a non-true value");
            }
        }
    }
}
