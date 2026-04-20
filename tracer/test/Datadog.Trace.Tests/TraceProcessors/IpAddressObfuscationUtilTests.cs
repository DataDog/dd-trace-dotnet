// <copyright file="IpAddressObfuscationUtilTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Processors;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class IpAddressObfuscationUtilTests
    {
        // Test cases ported from https://github.com/DataDog/datadog-agent/blob/main/pkg/obfuscate/ip_address_test.go
        public static TheoryData<string, string> GetQuantizePeerIPAddresses() => new()
        {
            { "127.0.0.1", "127.0.0.1" },                                          // localhost preserved
            { "::1", "::1" },                                                        // IPv6 localhost preserved
            { "169.254.169.254", "169.254.169.254" },                                // cloud metadata preserved
            { "fd00:ec2::254", "fd00:ec2::254" },                                    // EC2 metadata preserved
            { "169.254.170.2", "169.254.170.2" },                                    // ECS metadata preserved
            { string.Empty, string.Empty },                                               // empty string
            { "foo.dog", "foo.dog" },                                                 // hostname preserved
            { "192.168.1.1", "blocked-ip-address" },                                  // private IP blocked
            { "192.168.1.1.foo", "blocked-ip-address.foo" },                          // IP with trailing hostname
            { "192.168.1.1.2.3.4.5", "blocked-ip-address.2.3.4.5" },                // IP with trailing octets
            { "192_168_1_1", "blocked-ip-address" },                                  // underscore separator
            { "192-168-1-1", "blocked-ip-address" },                                  // hyphen separator
            { "192-168-1-1.foo", "blocked-ip-address.foo" },                          // hyphen IP with dot suffix
            { "192-168-1-1-foo", "blocked-ip-address-foo" },                          // hyphen IP with hyphen suffix
            { "2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF", "blocked-ip-address" },      // IPv6 full
            { "2001:db8:3c4d:15::1a2f:1a2b", "blocked-ip-address" },                 // IPv6 compressed
            { "[fe80::1ff:fe23:4567:890a]:8080", "blocked-ip-address:8080" },         // IPv6 bracket+port
            { "[::1]:8080", "::1:8080" },                                             // Allowed IPv6 bracket+port
            { "192.168.1.1:1234", "blocked-ip-address:1234" },                        // IPv4+port
            { "192.168.1.1:999999", "blocked-ip-address:999999" },                    // IPv4+invalid port
            { "192.168.1.1:abcd", "blocked-ip-address:abcd" },                        // IPv4+invalid port
            { "dnspoll:///10.21.120.145:6400", "dnspoll:///blocked-ip-address:6400" }, // scheme prefix
            { "dnspoll:///abc.cluster.local:50051", "dnspoll:///abc.cluster.local:50051" }, // scheme+hostname preserved
            { "http://10.21.120.145:6400", "http://blocked-ip-address:6400" },
            { "https://10.21.120.145:6400", "https://blocked-ip-address:6400" },
            { "192.168.1.1:1234,10.23.1.1:53,10.23.1.1,fe80::1ff:fe23:4567:890a,foo.dog", "blocked-ip-address:1234,blocked-ip-address:53,blocked-ip-address,foo.dog" },
            { "http://172.24.160.151:8091,172.24.163.33:8091,172.24.164.111:8091,172.24.165.203:8091,172.24.168.235:8091,172.24.170.130:8091", "http://blocked-ip-address:8091,blocked-ip-address:8091" },
            { "10-60-160-172.my-service.namespace.svc.abc.cluster.local", "blocked-ip-address.my-service.namespace.svc.abc.cluster.local" },
            { "ip-10-152-4-129.ec2.internal", "ip-blocked-ip-address.ec2.internal" },
            { "1-foo", "1-foo" },
            { "1-2-foo", "1-2-foo" },
            { "1-2-3-foo", "1-2-3-foo" },
            { "1-2-3-999", "1-2-3-999" },                                            // invalid octet >255
            { "1-2-999-foo", "1-2-999-foo" },                                        // invalid octet >255
            { "1-2-3-999-foo", "1-2-3-999-foo" },                                    // invalid octet >255
            { "1-2-3-4-foo", "blocked-ip-address-foo" },                              // valid IP with suffix
            { "7-55-2-app.agent.datadoghq.com", "7-55-2-app.agent.datadoghq.com" },  // not IP (4th field non-numeric)
        };

        [Theory]
        [MemberData(nameof(GetQuantizePeerIPAddresses))]
        public void QuantizePeerIPAddresses(string input, string expected)
        {
            var result = IpAddressObfuscationUtil.QuantizePeerIpAddresses(input);
            result.Should().Be(expected);
        }
    }
}
