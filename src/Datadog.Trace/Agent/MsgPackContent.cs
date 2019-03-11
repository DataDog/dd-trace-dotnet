using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MsgPack.Serialization;

namespace Datadog.Trace.Agent
{
    internal class MsgPackContent<T> : HttpContent
    {
        private readonly SerializationContext _serializationContext;

        public MsgPackContent(T value, SerializationContext serializationContext)
        {
            Value = value;
            Headers.ContentType = new MediaTypeHeaderValue("application/msgpack");
            _serializationContext = serializationContext;
        }

        public T Value { get; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return _serializationContext.GetSerializer<T>()
                                        .PackAsync(stream, Value);
        }

        protected override bool TryComputeLength(out long length)
        {
            // We can't compute the length beforehand
            length = -1;
            return false;
        }
    }
}
