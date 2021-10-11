// <copyright file="RequestHeadersHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Transports.Http;
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
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "x-forwarded-for", "80.19.10.10:32" },
                            { "true-client-ip", "81.202.236.243:82" }
                    },
               string.Empty,
               new string[0],
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
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "custom-ip-header", "193.12.13.14:81" },
                            { "x-forwarded-for", "80.19.10.10:32" },
                            { "true-client-ip", "81.202.236.243:82" },
                    },
               "custom-ip-header",
               new string[0],
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
                            { "header-custom-1", "193.12.13.14:81" },
                            { "x-forwarded-for", "192.168.1.2" },
                            { "true-client-ip", "81.202.236.243" }
                    },
               new Dictionary<string, string>
                    {
                            { "user-agent", "Mozilla firefox" },
                            { "referer", "https://example.com/" },
                            { "header-custom-1", "193.12.13.14:81" },
                            { "header-custom-2", "93.12.13.14:81" },
                            { "x-forwarded-for", "192.168.1.2" },
                            { "true-client-ip", "81.202.236.243" },
                    },
               string.Empty,
               new string[] { "header-custom-1", "header-custom-2" }, "81.202.236.243", 80
           }
       };

        [Theory]
        [MemberData(nameof(Headers))]
        public void GetRightHeadersAndIp(Dictionary<string, string> headers, Dictionary<string, string> expectedHeaders, string customIpHeader, string[] extraHeaders, string expectedIp, int expectedPort)
        {
            Func<string, string> getHeader = k => headers.TryGetValue(k, out var val) ? val : string.Empty;
            var result = RequestHeadersHelper.ExtractHeadersIpAndPort(getHeader, customIpHeader, extraHeaders, false, new IpInfo(string.Empty, 80));
            for (var i = 0; i < expectedHeaders.Count; i++)
            {
                var header = result.HeadersToSend.ElementAtOrDefault(i);
                Assert.NotNull(header.Key);
                var expectedHeader = expectedHeaders.ElementAt(i);
                Assert.Equal(header.Key, expectedHeader.Key);
                Assert.Equal(header.Value, expectedHeader.Value);
            }

            Assert.Equal(expectedIp, result.IpInfo.IpAddress);
            Assert.Equal(expectedPort, result.IpInfo.Port);
        }
    }
}
