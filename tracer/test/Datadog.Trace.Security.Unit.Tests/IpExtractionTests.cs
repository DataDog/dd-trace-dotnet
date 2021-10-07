// <copyright file="IpExtractionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Transports.Http;
using Xunit;

namespace Datadog.Trace.Security.UnitTests
{
    public class IpExtractionTests
    {
        [Theory]
        [InlineData("172.217.22.14", 80, new[] { "192.168.1.1", "172.16.32.41", "172.16.32.41", "172.217.22.14", "172.217.25.17:83" })]
        [InlineData("81.202.236.243", 5001, new[] { "192.168.0.2", "172.17.2.5:84", "10.11.12.14", "192.168.200.253:98", "81.202.236.243:5001" })]
        [InlineData("83.204.236.243", 443, new[] { "172.16.2.4", "172.31.255.255", "192.168.255.255", "10.145.255.255", "83.204.236.243:443" })]
        [InlineData("172.16.32.43", 80, new[] { "192.168.1.1", "172.16.32.41", "172.16.32.43" })]
        public void Ipv4PublicDetectedLocalIgnoredIfPublic(string expectedIp, int expectedPort, string[] ips)
        {
            var ip = IpExtractor.GetRealIpFromValues(ips, 80);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expectedPort, ip.Port);
        }

        [Theory]
        [InlineData("2001:db8:3333:4444:5555:6666:7777:8888", 80, new[] { "fe80::20e:cff:fe3b:883c", "2001:db8:3333:4444:5555:6666:7777:8888" })]
        [InlineData("2001:db8:3333:4444:5555:6666:7777:8888", 53, new[] { "fe80::20e:cff:fe3b:883c", "fe80::5525:2a3f:6fa6:cd4e%14", "[2001:db8:3333:4444:5555:6666:7777:8888]:53" })]
        [InlineData("FE80::240:D0FF:FE48:4672", 53, new[] { "fe80::20e:cff:fe3b:883c", "fe80::5525:2a3f:6fa6:cd4e%14", "FE80::240:D0FF:FE48:4672" })]
        public void Ipv6PublicDetectedPrivateIgnored(string expectedIp, int expectedPort, string[] ips)
        {
            var ip = IpExtractor.GetRealIpFromValues(ips, 80);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expectedPort, ip.Port);
        }

        [Theory]
        [InlineData("169.219.13.133", 80, new[] { "::FFFF:169.219.13.133", "::FFFF:129.144.52.38", "::ffff:191.239.213.197" })]
        [InlineData("129.144.52.38", 80, new[] { "::FFFF:192.168.1.26", "::FFFF:129.144.52.38", "::ffff:191.239.213.197" })]
        [InlineData("129.144.52.37", 553, new[] { "::FFFF:192.168.1.26", "[::FFFF:129.144.52.37]:553", "::ffff:191.239.213.197" })]
        public void Ipv4OverIpv6(string expectedIp, int expectedPort, string[] ips)
        {
            var ip = IpExtractor.GetRealIpFromValues(ips, 80);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expectedPort, ip.Port);
        }

        [Theory]
        [InlineData("81.202.236.243", 82, "81.202.236.243:82", 80)]
        [InlineData("2a00:1397:4:2a02::a1", 50434, "[2a00:1397:4:2a02::a1]:50434", 80)]
        [InlineData("2a00:1397:4:2a02::a1", 80, "2a00:1397:4:2a02::a1", 80)]
        [InlineData("::FFFF:101.45.75.219", 80, "::FFFF:101.45.75.219", 80)]
        public void ExtractAddressAndPort(string expectedIp, int expectedPort, string ip, int defaultport)
        {
            var result = IpExtractor.ExtractAddressAndPort(ip, defaultport);
            Assert.Equal(expectedIp, result.IpAddress);
            Assert.Equal(expectedPort, result.Port);
        }
    }
}
