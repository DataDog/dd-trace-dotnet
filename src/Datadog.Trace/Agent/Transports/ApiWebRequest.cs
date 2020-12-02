using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.Transports
{
    internal class ApiWebRequest : IApiRequest
    {
        private readonly HttpWebRequest _request;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;

            // Default headers
            _request.Headers.Add(AgentHttpHeaderNames.Language, ".NET");
            _request.Headers.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.Version);

            // don't add automatic instrumentation to requests from this HttpClient
            _request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            _request.Method = "POST";

            _request.ContentType = "application/msgpack";
            using (var requestStream = await _request.GetRequestStreamAsync().ConfigureAwait(false))
            {
                await CachedSerializer.Instance.SerializeAsync(requestStream, traces, formatterResolver).ConfigureAwait(false);
            }

            try
            {
                var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
                return new ApiWebResponse(httpWebResponse);
            }
            catch (WebException exception)
                when (exception.Status == WebExceptionStatus.ProtocolError && exception.Response != null)
            {
                // If the exception is caused by an error status code, ignore it and let the caller handle the result
                return new ApiWebResponse((HttpWebResponse)exception.Response);
            }
        }
    }
}
