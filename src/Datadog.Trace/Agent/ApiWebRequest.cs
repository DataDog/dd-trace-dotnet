using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;
using MessagePack;

namespace Datadog.Trace.Agent
{
    internal class ApiWebRequest : IApiRequest
    {
        private HttpWebRequest _request;

        public ApiWebRequest(HttpWebRequest request)
        {
            _request = request;

            // Default headers
            _request.Headers.Add(AgentHttpHeaderNames.Language, ".NET");
            _request.Headers.Add(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);

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
#if MESSAGEPACK_1_9
                await MessagePackSerializer.SerializeAsync(requestStream, traces, formatterResolver).ConfigureAwait(false);
#elif MESSAGEPACK_2_1
                await MessagePackSerializer.SerializeAsync(requestStream, traces, formatterResolver.Options).ConfigureAwait(false);
#endif
            }

            var httpWebResponse = (HttpWebResponse)await _request.GetResponseAsync().ConfigureAwait(false);
            return new ApiWebResponse(httpWebResponse);
        }
    }
}
