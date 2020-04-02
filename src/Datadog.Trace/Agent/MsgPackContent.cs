using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

#pragma warning disable SA1201 // Elements must appear in the correct order
namespace Datadog.Trace.Agent
{
    internal class MsgPackContent<T> : HttpContent
    {
        public T Value { get; }

#if NET45
        private readonly MsgPack.Serialization.SerializationContext _serializationContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsgPackContent{T}"/> class.
        /// </summary>
        /// <param name="value">The value to serialize into the content stream as MessagePack.</param>
        /// <param name="serializationContext">The serialization context.</param>
        public MsgPackContent(T value, MsgPack.Serialization.SerializationContext serializationContext)
        {
            Value = value;
            Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
            _serializationContext = serializationContext;
            _serializationContext = serializationContext;
        }

        /// <summary>Serialize the HTTP content to a stream as an asynchronous operation.</summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be <see langword="null" />.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return _serializationContext.GetSerializer<T>()
                                        .PackAsync(stream, Value);
        }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="MsgPackContent{T}"/> class.
        /// </summary>
        /// <param name="value">The value to serialize into the content stream as MessagePack.</param>
        public MsgPackContent(T value)
        {
            Value = value;
        }

        /// <summary>Serialize the HTTP content to a stream as an asynchronous operation.</summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be <see langword="null" />.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return MessagePack.MessagePackSerializer.SerializeAsync(stream, Value);
        }
#endif

        protected override bool TryComputeLength(out long length)
        {
            // We don't want compute the length beforehand
            length = -1;
            return false;
        }
    }
}
#pragma warning restore SA1201 // Elements must appear in the correct order
