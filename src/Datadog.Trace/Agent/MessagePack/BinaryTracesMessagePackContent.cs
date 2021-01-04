#if NETCOREAPP
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class BinaryTracesMessagePackContent : HttpContent
    {
        private readonly ArraySegment<byte> _traces;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryTracesMessagePackContent"/> class.
        /// </summary>
        /// <param name="traces">Serialized traces.</param>
        public BinaryTracesMessagePackContent(ArraySegment<byte> traces)
        {
            _traces = traces;

            Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
        }

        /// <summary>Serialize the HTTP content to a stream as an asynchronous operation.</summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be <see langword="null" />.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return stream.WriteAsync(_traces.Array, _traces.Offset, _traces.Count);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _traces.Count;
            return true;
        }
    }
}
#endif
