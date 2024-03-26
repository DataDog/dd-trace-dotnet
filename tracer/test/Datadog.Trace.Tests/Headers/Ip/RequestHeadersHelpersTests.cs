// <copyright file="RequestHeadersHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers.Ip;
using Datadog.Trace.TestHelpers;
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
                new SerializableDictionary
                {
                    { "user-agent", "Mozilla firefox" },
                    { "referer", "https://example.com/" },
                    { "hello-world", "irrelevant" },
                    { "x-forwarded-for", "80.19.10.10:32" },
                    { "true-client-ip", "81.202.236.243:82" }
                },
                string.Empty, "80.19.10.10", 32, "80.19.14.16", 32
            },
            new object[]
            {
                new SerializableDictionary
                {
                    { "user-agent", "Mozilla firefox" },
                    { "referer", "https://example1.com/" },
                    { "hello-world", "irrelevant" },
                    { "x-forwarded-for", "80.19.10.10:32" },
                    { "custom-header3", "81.202.236.243:82" }
                },
                "custom-header3", "81.202.236.243", 82, "80.19.14.16", 32
            },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example2.com/" }, { "hello-world", "irrelevant" }, { "custom-ip-header", "193.12.13.14:81" } }, "custom-ip-header", "193.12.13.14", 81, "80.19.14.16", 32 },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example3.com/" }, { "header-custom-1", "93.12.13.14:81" }, { "x-forwarded-for", "192.168.1.2,81.202.236.243" } }, string.Empty, "81.202.236.243", 80, "80.19.14.16", 32 },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example4.com/" }, { "header-custom-2", "93.12.13.14:81" }, }, "header-custom-2", "93.12.13.14", 81, "80.19.14.16", 32 },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example5.com/" } }, string.Empty, "80.19.14.16", 32, "80.19.14.16", 32 },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example5.com/" }, { "x-forwarded-for", "'\\'\\\"\">><<script/src='//xf.cm2.pW/m'></script>, 144.126.148.236, 64.252.190.162" } }, string.Empty, "144.126.148.236", 80, "80.19.14.16", 32 },
            new object[] { new SerializableDictionary { { "user-agent", "Mozilla firefox" }, { "referer", "https://example5.com/" }, { "x-forwarded-for", "'\\'\\\"\">><<script/src='//xf.cm2.pW/m'></script>" }, { "cf-connecting-ip", "144.126.148.236" } }, string.Empty, "144.126.148.236", 80, "80.19.14.16", 32 },
        };

        [Theory]
        [MemberData(nameof(Headers))]
        public void RightHeadersAndIp(SerializableDictionary headers, string customIpHeader, string expectedIp, int expectedPort, string peerIp, int peerPort)
        {
            string GetHeader(string k) => headers.TryGetValue(k, out var val) ? val : string.Empty;
            var result = RequestIpExtractor.ExtractIpAndPort(GetHeader, customIpHeader, false, new IpInfo(peerIp, peerPort));

            if (expectedIp == null)
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
