// <copyright file="HttpMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Internal.Agent;
using Datadog.Trace.Internal.Logging;
using Datadog.Trace.Internal.Util;

namespace Datadog.Trace.Internal.HttpOverStreams
{
    internal abstract class HttpMessage
    {
        public HttpMessage(HttpHeaders headers, IHttpContent content)
        {
            Headers = headers;
            Content = content;
        }

        public HttpHeaders Headers { get; }

        public IHttpContent Content { get; }

        public int? ContentLength => int.TryParse(Headers.GetValue("Content-Length"), out int length) ? length : (int?)null;

        public string ContentType => Headers.GetValue("Content-Type");

        public Encoding GetContentEncoding() => ApiResponseExtensions.GetCharsetEncoding(ContentType);
    }
}
