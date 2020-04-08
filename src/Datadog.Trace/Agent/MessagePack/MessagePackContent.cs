#if !NET45
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MessagePack;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class MessagePackContent<T> : HttpContent
    {
        private readonly MessagePackSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackContent{T}"/> class.
        /// </summary>
        /// <param name="value">The value to serialize into the content stream as MessagePack.</param>
        /// <param name="options">The options to pass down to the <see cref="MessagePackSerializer"/>.</param>
        public MessagePackContent(T value, MessagePackSerializerOptions options)
        {
            Value = value;
            _options = options;

            Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
        }

        public T Value { get; }

        /// <summary>Serialize the HTTP content to a stream as an asynchronous operation.</summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be <see langword="null" />.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return MessagePackSerializer.SerializeAsync(stream, Value, _options);
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
