// <copyright file="HttpMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.HttpOverStreams
{
    internal abstract class HttpMessage
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpMessage>();

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
                return EncodingHelpers.Utf8NoBom;
            }

            // text/plain; charset=utf-8
            foreach (var pair in contentType.SplitIntoSpans(';'))
            {
                var parts = pair.AsSpan();
                var index = parts.IndexOf('=');

                if (index != -1)
                {
                    var firstPart = parts.Slice(0, index).Trim();

                    if (!firstPart.Equals("charset".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var secondPart = parts.Slice(index + 1).Trim();

                    if (secondPart.Equals("utf-8".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return EncodingHelpers.Utf8NoBom;
                    }

                    if (secondPart.Equals("us-ascii".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        return Encoding.ASCII;
                    }
                }
            }

            Log.Warning("Assuming default UTF-8, Could not find an encoding for: {ContentType}", contentType);
            return EncodingHelpers.Utf8NoBom;
        }
    }
}
