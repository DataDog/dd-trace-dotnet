// <copyright file="IpInfoTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Transports.Http;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class IpInfoTests
    {
        [Theory]
        [InlineData("172.82.32.18", 80, false, "172.82.32.18:80")]
        [InlineData("2607:f0d0:1002:51::4", 80, true, "[2607:f0d0:1002:51::4]:80")]
        public void PrintIpv4(string ip, int port, bool ipv6, string expected)
        {
            var ipInfos = new IpInfo(ip, port, ipv6);
            Assert.Equal(expected, ipInfos.ToString());
        }
    }
}
