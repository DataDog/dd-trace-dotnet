// <copyright file="AwsStepFunctionsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class AwsStepFunctionsCommonTests
{
    public static IEnumerable<object[]> SchemaSpanKindOperationNameData
        => new List<object[]>
        {
            new object[] { "v0", "client", "stepfunctions.request" },
            new object[] { "v0", "producer", "stepfunctions.request" },
            new object[] { "v1", "client", "aws.stepfunctions.request" },
            new object[] { "v1", "producer", "aws.stepfunctions.send" },
            new object[] { "v1", "consumer", "aws.stepfunctions.process" },
            new object[] { "v1", "server", "aws.stepfunctions.request" }
        };

    [Theory]
    [MemberData(nameof(SchemaSpanKindOperationNameData))]
    public async Task GetCorrectOperationName(string schemaVersion, string spanKind, string expected)
    {
        await using var tracer = GetTracer(schemaVersion);

        AwsStepFunctionsCommon.GetOperationName(tracer, spanKind).Should().Be(expected);
    }

    private static ScopedTracer GetTracer(string schemaVersion)
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }
}
