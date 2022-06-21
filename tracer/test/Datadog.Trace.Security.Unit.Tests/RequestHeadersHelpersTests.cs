// <copyright file="RequestHeadersHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers.Ip;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class RequestHeadersHelpersTests
    {
        public static IEnumerable<object[]> Headers => new List<object[]>
       {
           new object[]
           {
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "hello-world", "irrelevant" },
                            { "x-forwarded-for", "80.19.10.10:32" },
                            { "true-client-ip", "81.202.236.243:82" }
                    },
               string.Empty,
               "80.19.10.10", 32
           },
           new object[]
           {
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "hello-world", "irrelevant" },
                            { "x-forwarded-for", "80.19.10.10:32" },
                            { "custom-ip-header", "193.12.13.14:81" },
                            { "true-client-ip", "81.202.236.243:82" }
                    },
               "custom-ip-header",
               "193.12.13.14", 81
           },
           new object[]
           {
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "header-custom-2", "93.12.13.14:81" },
                            { "hello-world", "irrelevant" },
                            { "x-forwarded-for", "192.168.1.2" },
                            { "true-client-ip", "81.202.236.243" }
                    },
               string.Empty, "81.202.236.243", 80
           }
       };

        [Theory]
        [MemberData(nameof(Headers))]
        public void GetRightHeadersAndIp(Dictionary<string, string> headers, string customIpHeader, string expectedIp, int expectedPort)
        {
            Func<string, string> getHeader = k => headers.TryGetValue(k, out var val) ? val : string.Empty;
            var result = RequestIpExtractor.ExtractIpAndPort(getHeader, customIpHeader, false, new IpInfo(string.Empty, 80));

            Assert.Equal(expectedIp, result.IpAddress);
            Assert.Equal(expectedPort, result.Port);
        }
    }
}
