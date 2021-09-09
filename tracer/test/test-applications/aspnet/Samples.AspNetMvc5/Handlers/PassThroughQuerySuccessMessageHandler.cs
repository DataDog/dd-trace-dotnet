using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.AspNetMvc5.Handlers
{
    public class PassThroughQuerySuccessMessageHandler : DelegatingHandler
    {
        private readonly string SuccessQueryKey = "ps";
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Parse for the "ps" query string
            var queryDictionary = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            var isSuccessString = queryDictionary.GetValues(SuccessQueryKey)?.FirstOrDefault() ?? @"true";

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
