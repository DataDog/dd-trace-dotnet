// <copyright file="MsmqTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Msmq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Internal;
using Datadog.Trace.Internal.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class MsmqTests
    {
        public static IEnumerable<object[]> GetOperationNameParams()
            => from schemaVersion in new object[] { SchemaVersion.V0, SchemaVersion.V1 }
               from spanKind in new[] { InternalSpanKinds.Producer, InternalSpanKinds.Consumer, InternalSpanKinds.Client }
               select new[] { schemaVersion, spanKind };

        [Theory]
        [MemberData(nameof(GetOperationNameParams))]
        public void GetOperationNameIsCorrect(object schemaVersionObject, string spanKind)
        {
            var schemaVersion = (SchemaVersion)schemaVersionObject;
            var configSourceMock = new Mock<IConfigurationSource>();
            configSourceMock.Setup(c => c.GetString(It.Is<string>(s => s.Equals(ConfigurationKeys.MetadataSchemaVersion)))).Returns(schemaVersion.ToString());
            var settings = new InternalTracerSettings(configSourceMock.Object, new ConfigurationTelemetry());
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            var tracer = new InternalTracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            MsmqCommon.GetOperationName(tracer, spanKind).Should().Be(GetExpectedOperationName(schemaVersion, spanKind));
        }

        private string GetExpectedOperationName(SchemaVersion schemaVersion, string spanKind)
        {
            if (schemaVersion.Equals(SchemaVersion.V0))
            {
                return MsmqConstants.MsmqCommand;
            }

            switch (spanKind)
            {
                case InternalSpanKinds.Producer:
                    return "msmq.send";
                case InternalSpanKinds.Consumer:
                    return "msmq.process";
                default:
                    return MsmqConstants.MsmqCommand;
            }
        }
    }
}
