using System.Collections.Specialized;
using System.Text;

namespace Datadog.Trace.TestHelpers.NamedPipes.Server
{
    public class MockHttpMessage
    {
        public const int BufferSize = 0x5000;

        public MockHttpMessage()
        {
            AllBytes = new byte[BufferSize];
            StringBuilder = new StringBuilder();
        }

        public byte[] HeaderBytes { get; set; }

        public byte[] BodyBytes { get; set; }

        public byte[] AllBytes { get; private set; }

        public NameValueCollection Headers { get; set; } = new NameValueCollection();

        public StringBuilder StringBuilder { get; private set; }
    }
}
