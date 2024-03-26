// <copyright file="GrpcLegacyClientCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    public class GrpcLegacyClientCommonTests
    {
        [Fact]
        public void GetNormalizedHost_ExtractsHostFromHostPort()
        {
            object channel = new();
            string target = "127.0.0.1:59510";

            GrpcLegacyClientCommon.GetNormalizedHost(channel, target).Should().Be("127.0.0.1");

            // Run it again for good measure
            GrpcLegacyClientCommon.GetNormalizedHost(channel, target).Should().Be("127.0.0.1");
        }

        [Fact]
        public void GetNormalizedHost_ExtractsHostFromHostname()
        {
            object channel = new();
            string target = "www.contoso.com:8080";

            GrpcLegacyClientCommon.GetNormalizedHost(channel, target).Should().Be("www.contoso.com");

            // Run it again for good measure
            GrpcLegacyClientCommon.GetNormalizedHost(channel, target).Should().Be("www.contoso.com");
        }

        [Fact]
        public void GetNormalizedHost_ReturnsValueFromDifferentChannels()
        {
            object channel1 = new();
            string target1 = "127.0.0.1:59510";

            object channel2 = new();
            string target2 = "www.contoso.com:8080";

            GrpcLegacyClientCommon.GetNormalizedHost(channel1, target1).Should().Be("127.0.0.1");
            GrpcLegacyClientCommon.GetNormalizedHost(channel2, target2).Should().Be("www.contoso.com");

            // Run it again for good measure
            GrpcLegacyClientCommon.GetNormalizedHost(channel1, target1).Should().Be("127.0.0.1");
            GrpcLegacyClientCommon.GetNormalizedHost(channel2, target2).Should().Be("www.contoso.com");
        }
    }
}
