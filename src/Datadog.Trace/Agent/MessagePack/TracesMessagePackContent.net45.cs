#if NET45
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MessagePack;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class TracesMessagePackContent : HttpContent
    {
        private readonly IFormatterResolver _resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="TracesMessagePackContent"/> class.
        /// </summary>
        /// <param name="traces">The traces to serialize into the content stream as MessagePack.</param>
        /// <param name="resolver">The <see cref="IFormatterResolver"/> to use when serializing <paramref name="traces"/>.</param>
        public TracesMessagePackContent(Span[][] traces, IFormatterResolver resolver)
        {
            Traces = traces;
            _resolver = resolver;

            Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
        }

        public Span[][] Traces { get; }

        /// <summary>Serialize the HTTP content to a stream as an asynchronous operation.</summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be <see langword="null" />.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return MessagePackSerializer.SerializeAsync(stream, Traces, _resolver);
        }

        protected override bool TryComputeLength(out long length)
        {
            // We don't want compute the length beforehand
            length = -1;
            return false;
        }
    }
}
#endif
