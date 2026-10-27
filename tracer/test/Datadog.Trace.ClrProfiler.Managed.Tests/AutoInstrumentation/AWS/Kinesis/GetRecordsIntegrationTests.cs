// <copyright file="GetRecordsIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Amazon.Kinesis.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Kinesis;

public class GetRecordsIntegrationTests
{
    // OnMethodBegin requires TGetRecordsRequest : IGetRecordsRequest, IDuckType, which CallTarget satisfies with the generated proxy type.
    internal interface IGetRecordsRequestProxy : IGetRecordsRequest, IDuckType
    {
    }

    [Fact]
    public void OnMethodBegin_PassesStreamNameInState()
    {
        var request = new GetRecordsRequest { StreamARN = "arn:aws:kinesis:us-east-1:123456789012:stream/MyStreamName" };

        var state = GetRecordsIntegration.OnMethodBegin<object, IGetRecordsRequestProxy>(null, request.DuckCast<IGetRecordsRequestProxy>());

        state.Scope?.Dispose();
        state.State.Should().Be("MyStreamName");
    }
}
