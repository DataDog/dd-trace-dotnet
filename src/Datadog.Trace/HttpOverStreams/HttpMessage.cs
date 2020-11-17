using System.Text;
using HttpOverStream;

namespace Datadog.Trace.HttpOverStreams
{
    internal abstract class HttpMessage
    {
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public HttpMessage(HttpHeaders headers, IHttpContent content)
        {
            Headers = headers;
            Content = content;
        }

        public HttpHeaders Headers { get; }

        public IHttpContent Content { get; }

        public int? ContentLength => int.TryParse(Headers.GetValue("Content-Length"), out int length) ? length : (int?)null;

        public string ContentType => Headers.GetValue("Content-Type");

        public Encoding GetContentEncoding()
        {
            if (ContentType == null)
            {
                return null;
            }

            // text/plain; charset=utf-8
            string[] pairs = ContentType?.Split(';');

            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('=');

                if (parts.Length == 2 && string.Equals(parts[0].Trim(), "charset", System.StringComparison.OrdinalIgnoreCase))
                {
                    switch (parts[1].Trim())
                    {
                        case "utf-8":
                            return Utf8Encoding;
                        case "us-ascii":
                            return Encoding.ASCII;
                    }
                }
            }

            return null;
        }
    }
}
