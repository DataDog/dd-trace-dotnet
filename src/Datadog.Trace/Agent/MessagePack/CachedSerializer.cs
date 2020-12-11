using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class CachedSerializer
    {
        internal static readonly CachedSerializer Instance = new CachedSerializer();
        private const int InitialBufferSize = 64 * 1024;

#if DEBUG
        /// <summary>
        /// Set this variable to true in order to output the serialized traces to a file
        /// </summary>
        private readonly bool _writePayloadToFile = true;
        private readonly string _payloadFilePath = @"C:\ProgramData\Datadog .NET Tracer\logs\";
#endif

        private byte[] _buffer;

        public CachedSerializer()
        {
            _buffer = new byte[InitialBufferSize];
        }

        public async Task SerializeAsync<T>(Stream stream, T obj, IFormatterResolver resolver)
        {
            byte[] buffer = null;
            bool usingCachedBuffer = true;

            try
            {
                // Sanity check, in case the serializer is incorrectly used concurrently
                buffer = Interlocked.Exchange(ref _buffer, null);

                if (buffer == null)
                {
                    usingCachedBuffer = false;
                    buffer = new byte[InitialBufferSize];
                }

                int length = MessagePackSerializer.Serialize(ref buffer, 0, obj, resolver);

#if DEBUG
                if (_writePayloadToFile && length > 3)
                {
                    // If there are actual traces present, then write
                    // Trim down to the actual length to write to file
                    var trimmed = new byte[length];
                    System.Array.Copy(buffer, 0, trimmed, 0, length);
                    File.WriteAllBytes(Path.Combine(_payloadFilePath, $"payload-{System.DateTime.Now.Ticks}.txt"), trimmed);
                }
#endif

                await stream.WriteAsync(buffer, 0, length).ConfigureAwait(false);
            }
            finally
            {
                if (usingCachedBuffer)
                {
                    _buffer = buffer;
                }
            }
        }
    }
}
