// <copyright file="MockOtlpRawRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// An undecoded OTLP request body, captured for signals the mock agent doesn't decode
/// (currently <c>/v1/metrics</c> and <c>/v1/logs</c>).
/// </summary>
public sealed class MockOtlpRawRequest
{
    public MockOtlpRawRequest(byte[] body, NameValueCollection headers, string contentType)
    {
        Body = body;
        Headers = headers;
        ContentType = contentType;
    }

    public byte[] Body { get; }

    public NameValueCollection Headers { get; }

    public string ContentType { get; }
}
