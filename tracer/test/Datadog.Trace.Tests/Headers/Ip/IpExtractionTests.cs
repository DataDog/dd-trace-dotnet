// <copyright file="IpExtractionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Headers.Ip;
using Xunit;

namespace Datadog.Trace.Tests.Headers.Ip
{
    public class IpExtractionTests
    {
        [Theory]
        [InlineData("172.217.22.14", 80, "172.217.22.14")]
        [InlineData("81.202.236.243", 5001,  "81.202.236.243:5001")]
        [InlineData("83.204.236.243", 443, "172.16.2.4, 172.31.255.255, 192.168.255.255, 10.145.255.255, 83.204.236.243:443")]
        [InlineData("192.168.1.1", 80, "192.168.1.1, 172.16.32.41, 172.16.32.43")]
        [InlineData("83.204.236.243", 80, "127.0.0.1, 83.204.236.243")]
        [InlineData("83.204.236.243", 80, "169.254.0.3, 83.204.236.243")]
        public void Ipv4PublicDetectedLocalIgnored(string expectedIp, int expectedPort, string headerValue)
        {
            var ip = IpExtractor.RealIpFromValue(headerValue, https: false);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expectedPort, ip.Port);
        }

        [Theory]
        [InlineData("2001:db8:3333:4444:5555:6666:7777:8888", 80, "fe80::20e:cff:fe3b:883c, 2001:db8:3333:4444:5555:6666:7777:8888")]
        [InlineData("2001:db8:3333:4444:5555:6666:7777:8888", 53, "fe80::20e:cff:fe3b:883c, fe80::5525:2a3f:6fa6:cd4e%14, [2001:db8:3333:4444:5555:6666:7777:8888]:53")]
        [InlineData("fe80::20e:cff:fe3b:883c", 80, "fe80::20e:cff:fe3b:883c, fe80::5525:2a3f:6fa6:cd4e%14, FE80::240:D0FF:FE48:4672")]
        public void Ipv6PublicDetectedPrivateIgnored(string expectedIp, int expectedPort, string headerValue)
        {
            var ip = IpExtractor.RealIpFromValue(headerValue, https: false);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expectedPort, ip.Port);
        }

        [Theory]
        [InlineData("169.219.13.133", 80, "::FFFF:169.219.13.133 , ::FFFF:129.144.52.38, ::ffff:191.239.213.197")]
        [InlineData("129.144.52.38", 80, "::FFFF:192.168.1.26, ::FFFF:129.144.52.38, ::ffff:191.239.213.197")]
        [InlineData("129.144.52.37", 553, "::FFFF:192.168.1.26, [::FFFF:129.144.52.37]:553, ::ffff:191.239.213.197")]
        public void Ipv4OverIpv6(string expectedIp, int expectedPort, string headerValue)
        {
            var ip = IpExtractor.RealIpFromValue(headerValue,   https: false);
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
            var result = IpExtractor.ExtractAddressAndPort(ip, defaultPort: defaultport);
            Assert.Equal(expectedIp, result.IpAddress);
            Assert.Equal(expectedPort, result.Port);
        }

        [Fact]
        public void Ipv6UnicastLocalIgnored()
        {
            var expectedIp = "81.202.236.243";
            var ip = IpExtractor.RealIpFromValue($"fdf8:f53b:82e4::53, {expectedIp}:82", false);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expected: 82, ip.Port);
        }

        [Fact]
        public void Ipv6LinkLocalIgnored()
        {
            const string expectedIp = "81.202.236.243";
            var ip = IpExtractor.RealIpFromValue("fe80::9656:d028:8652:66b6, 81.202.236.243:82", https: false);
            Assert.Equal(expectedIp, ip.IpAddress);
            Assert.Equal(expected: 82, ip.Port);
        }
    }
}
