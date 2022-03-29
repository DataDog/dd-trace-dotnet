using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.AspNetMvc5.Handlers
{
    public class TerminatingQuerySuccessMessageHandler : DelegatingHandler
    {
        private readonly string SuccessQueryKey = "ts";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Parse for the "ts" query string
            var queryDictionary = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            var isSuccessString = queryDictionary.GetValues(SuccessQueryKey)?.FirstOrDefault() ?? @"true";

            var canParse = bool.TryParse(isSuccessString, out var isSuccess);

            // If true, compose a message
            if (canParse && isSuccess)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Hello!")
                };


                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(response); // Also sets the task state to "RanToCompletion"
                return tsc.Task;
            }
            // If false, throw an exception
            else
            {
                throw new ArgumentException($"Source: {nameof(TerminatingQuerySuccessMessageHandler)}. Error: Query param {SuccessQueryKey} was set to a non-true value");
            }
        }
    }
}
