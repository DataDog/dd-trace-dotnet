// <copyright file="HttpMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace.HttpOverStreams
{
    internal abstract class HttpMessage
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpMessage>();
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
            // reduce getter calls
            var contentType = ContentType;

            if (contentType == null)
            {
                return null;
            }

            if (string.Equals("application/json", contentType, StringComparison.OrdinalIgnoreCase))
            {
                // Default
                return Utf8Encoding;
            }

            // text/plain; charset=utf-8
            string[] pairs = contentType.Split(';');

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

            Log.Warning("Assuming default UTF-8, Could not find an encoding for: {ContentType}", contentType);
            return Utf8Encoding;
        }
    }
}
