// <copyright file="TraceAgentHttpHeaderHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams
{
    public class TraceAgentHttpHeaderHelperTests
    {
        [Fact]
        public void WriteLeadingHeaders()
        {
            // Note that WriteLeadingHeaders should NOT write this header in WriteLeadingHeaders
            var headers = new HttpHeaders { { "x-test", "my-value" } };
            var bytes = Encoding.UTF8.GetBytes("{}"); // length = 2
            var content = new BufferContent(new ArraySegment<byte>(bytes));
            var request = new HttpRequest(
                verb: "PATCH",
                host: "my-host.com",
                path: "/some/path",
                headers,
                content);

            var helper = new TraceAgentHttpHeaderHelper();

            var tracerVersion = TracerConstants.AssemblyVersion;
            var lang = FrameworkDescription.Instance.Name;
            var fxVersion = FrameworkDescription.Instance.ProductVersion;

            var expected = "PATCH /some/path HTTP/1.1\r\nHost: my-host.com\r\nAccept-Encoding: identity\r\nContent-Length: 2\r\nDatadog-Meta-Lang: .NET\r\nDatadog-Meta-Tracer-Version: "
                         + tracerVersion + "\r\nx-datadog-tracing-enabled: false\r\nDatadog-Meta-Lang-Interpreter: "
                         + lang + "\r\nDatadog-Meta-Lang-Version: "
                         + fxVersion + "\r\nDatadog-Client-Computed-Top-Level: 1\r\n";

            var sb = new StringBuilder();
            using var textWriter = new StringWriter(sb);
            helper.WriteLeadingHeaders(request, textWriter);

            sb.ToString().Should().Be(expected);
        }

        [Fact]
        public void WriteHeader()
        {
            var header = new HttpHeaders.HttpHeader("my-key", "my-value");
            var helper = new TraceAgentHttpHeaderHelper();
            var expected = "my-key: my-value\r\n";

            var sb = new StringBuilder();
            using var textWriter = new StringWriter(sb);
            helper.WriteHeader(textWriter, header);

            sb.ToString().Should().Be(expected);
        }

        [Fact]
        public void WriteEndOfHeaders()
        {
            var helper = new TraceAgentHttpHeaderHelper();
            var expected = "Content-Type: application/msgpack\r\n\r\n";

            var sb = new StringBuilder();
            using var textWriter = new StringWriter(sb);
            helper.WriteEndOfHeaders(textWriter);

            sb.ToString().Should().Be(expected);
        }
    }
}
