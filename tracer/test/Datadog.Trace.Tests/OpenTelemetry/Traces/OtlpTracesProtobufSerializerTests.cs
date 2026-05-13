// <copyright file="OtlpTracesProtobufSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Traces;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

public class OtlpTracesProtobufSerializerTests
{
    [Fact]
    public void FinishBody_ReturnsZero_WhenNothingSerialized()
    {
        var serializer = new OtlpTracesProtobufSerializer();
        var buffer = new byte[1024];

        var written = serializer.FinishBody(ref buffer, offset: 0, maxSize: buffer.Length);

        written.Should().Be(0);
    }
}

#endif
