// <copyright file="TruncatorTraceProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class TruncatorTraceProcessorTests
    {
        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate_test.go#L15-L23
        [Fact]
        public void TruncateString()
        {
            Assert.Equal(string.Empty, TruncateUTF8(string.Empty, 5));
            Assert.Equal("télé", TruncateUTF8("télé", 5));
            Assert.Equal("t", TruncateUTF8("télé", 2));
            Assert.Equal("éé", TruncateUTF8("ééééé", 5));
            Assert.Equal("ééééé", TruncateUTF8("ééééé", 18));
            Assert.Equal("ééééé", TruncateUTF8("ééééé", 10));
            Assert.Equal("ééé", TruncateUTF8("ééééé", 6));

            static string TruncateUTF8(string value, int limit)
            {
                Trace.Processors.TraceUtil.TruncateUTF8(ref value, limit);
                return value;
            }
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate_test.go#L25-L38
        [Fact]
        public void TruncateResourceTest()
        {
            Assert.Equal("resource", Trace.Processors.TruncatorTraceProcessor.TruncateResource("resource"));

            var s = new string('a', Trace.Processors.TruncatorTraceProcessor.MaxResourceLen);
            Assert.Equal(s, Trace.Processors.TruncatorTraceProcessor.TruncateResource(s + "extra string"));
        }
    }
}
