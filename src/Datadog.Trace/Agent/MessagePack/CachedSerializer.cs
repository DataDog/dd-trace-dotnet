using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent.MessagePack
{
    internal class CachedSerializer
    {
        internal static readonly CachedSerializer Instance = new CachedSerializer();

        private const int InitialBufferSize = 64 * 1024;
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

                File.WriteAllBytes($"C:\\_INSPECTION\\named-pipe-trace-payload-{System.Guid.NewGuid()}.dat", buffer);
                // await stream.WriteAsync(buffer, 0, length).ConfigureAwait(false);
                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
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
