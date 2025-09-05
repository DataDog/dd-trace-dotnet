// <copyright file="RequestHeadersHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Headers.Ip;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Headers.Ip
{
    public class RequestHeadersHelpersTests
    {
        public static IEnumerable<object[]> Headers => new List<object[]>
        {
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example.com/" },
                        { "hello-world", "irrelevant" },
                        { "x-forwarded-for", "80.19.10.10:32" },
                        { "true-client-ip", "81.202.236.243:82" },
                        { "forwarded", "82.20.36.23:800" }
                    },
                    name: "http headers should be read in order",
                    customIpHeader: string.Empty,
                    expectedIp: "80.19.10.10",
                    expectedPort: 32,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example.com/" },
                        { "hello-world", "irrelevant" },
                        { "forwarded", "for=80.19.10.10:32" },
                        { "true-client-ip", "81.202.236.243:82" }
                    },
                    name: "forwarded header is parsed correctly",
                    customIpHeader: string.Empty,
                    expectedIp: "80.19.10.10",
                    expectedPort: 32,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example1.com/" },
                        { "hello-world", "irrelevant" },
                        { "forwarded", "82.20.36.23:800" },
                        { "x-forwarded-for", "80.19.10.10:32" },
                        { "custom-header3", "81.202.236.243:82" }
                    },
                    name: "custom header should prevail",
                    customIpHeader: "custom-header3",
                    expectedIp: "81.202.236.243",
                    expectedPort: 82,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example3.com/" },
                        { "header-custom-1", "93.12.13.14:81" },
                        { "x-forwarded-for", "192.168.1.2,81.202.236.243" }
                    },
                    name: "custom header is ignored if not configured, and public ip is reported instead of private",
                    customIpHeader: string.Empty,
                    expectedIp: "81.202.236.243",
                    expectedPort: 80,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new() { { "user-agent", "Mozilla firefox" }, { "referer", "https://example5.com/" } },
                    name: "peer ip is reported if nothing is found",
                    customIpHeader: string.Empty,
                    expectedIp: "80.19.14.16",
                    expectedPort: 32,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" }, { "referer", "https://example5.com/" },
                        {
                            "cf-connecting-ip",
                            "'\\'\\\"\">><<script/src='//xf.cm2.pW/m'></script>, 144.126.148.236, 64.252.190.162"
                        }
                    },
                    name: "right ip is found is there's a list with one unparsable one",
                    customIpHeader: string.Empty,
                    expectedIp: "144.126.148.236",
                    expectedPort: 80,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example5.com/" },
                        { "x-forwarded-for", "80.19.10.10:32" },
                    },
                    name: "if custom header gives nothing, nothing is reported even if other ips are present",
                    customIpHeader: "absent-custom-header",
                    expectedIp: null,
                    expectedPort: null,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            },
            new object[]
            {
                new TestScenario(
                    data: new()
                    {
                        { "user-agent", "Mozilla firefox" },
                        { "referer", "https://example5.com/" },
                        { "x-forwarded-for", "80.19.10.10:32" },
                        { "absent-custom-header", string.Empty },
                    },
                    name: "if custom header gives nothing, nothing is reported even if other ips are present",
                    customIpHeader: "absent-custom-header",
                    expectedIp: null,
                    expectedPort: null,
                    peerIp: "80.19.14.16",
                    peerPort: 32)
            }
        };

        [Theory]
        [MemberData(nameof(Headers))]
        public void RightHeadersAndIp(TestScenario scenario)
        {
            var headers = scenario.Data;
            var customIpHeader = scenario.CustomIpHeader;
            var peerIp = scenario.PeerIp;
            var peerPort = scenario.PeerPort;
            var expectedIp = scenario.ExpectedIp;
            var expectedPort = scenario.ExpectedPort;

            string GetHeader(string k) => headers.TryGetValue(k, out var val) ? val : string.Empty;
            var result = RequestIpExtractor.ExtractIpAndPort(GetHeader, customIpHeader, false, new(peerIp, peerPort));

            if (expectedIp == null && expectedPort is null)
            {
                result.Should().BeNull();
            }
            else
            {
                Assert.Equal(expectedIp, result.IpAddress);
                Assert.Equal(expectedPort, result.Port);
            }
        }
    }
}
