// <copyright file="HttpHeaderHelperBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams
{
    internal abstract class HttpHeaderHelperBase
    {
        protected abstract string MetadataHeaders { get; }

        protected abstract string ContentType { get; }

        public Task WriteLeadingHeaders(HttpRequest request, TextWriter writer)
        {
            var contentLengthHeader = request.Content?.Length is { } contentLength
                                    ? $"Content-Length: {contentLength}{DatadogHttpValues.CrLf}"
                                    : string.Empty;

            var leadingHeaders = $"{request.Verb} {request.Path} HTTP/1.1{DatadogHttpValues.CrLf}Host: {request.Host}{DatadogHttpValues.CrLf}Accept-Encoding: identity{DatadogHttpValues.CrLf}{contentLengthHeader}{MetadataHeaders}";
            return writer.WriteAsync(leadingHeaders);
        }

        public Task WriteHeader(TextWriter writer, HttpHeaders.HttpHeader header)
        {
            return writer.WriteAsync($"{header.Name}: {header.Value}{DatadogHttpValues.CrLf}");
        }
    }
}
