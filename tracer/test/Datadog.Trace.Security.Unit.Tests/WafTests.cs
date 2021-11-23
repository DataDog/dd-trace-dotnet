﻿// <copyright file="WafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafTests
    {
        private const string FileName = "rule-set.json";

        [Theory]
        [Trait("Category", "ArmUnsupported")]
        [InlineData("args", "[$slice]", "nosqli", "crs-942-290")]
        [InlineData("attack", "appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("key", "<script>", "xss", "crs-941-110")]
        [InlineData("value", "0000012345", "sqli", "crs-942-220")]
        public void QueryStringAttack(string key, string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestQuery,
                new Dictionary<string, List<string>>
                {
                    {
                        key, new List<string>
                        {
                            attack
                        }
                    }
                },
                flow,
                rule);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void UrlRawAttack()
        {
            Execute(
                AddressesConstants.RequestUriRaw,
                "http://localhost:54587/waf/0x5c0x2e0x2e0x2f",
                "lfi",
                "crs-930-100");
        }

        [Theory]
        [Trait("Category", "ArmUnsupported")]
        [InlineData("user-agent", "Arachni/v1", "security_scanner", "ua0-600-12x")]
        [InlineData("referer", "<script >", "xss", "crs-941-110")]
        [InlineData("x-file-name", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("x-filename", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("x_filename", "routing.yml", "command_injection", "crs-932-180")]
        public void HeadersAttack(string header, string content, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestHeaderNoCookies,
                new Dictionary<string, string>
                {
                    {
                        header, content
                    }
                },
                flow,
                rule);
        }

        [Theory]
        [Trait("Category", "ArmUnsupported")]
        [InlineData("attack", ".htaccess", "lfi", "crs-930-120")]
        [InlineData("value", "/*!*/", "sqli", "crs-942-500")]
        [InlineData("value", ";shutdown--", "sqli", "crs-942-280")]
        [InlineData("key", ".cookie-;domain=", "http_protocol_violation", "crs-943-100")]
        [InlineData("x-attack", " var_dump ()", "php_code_injection", "crs-933-160")]
        [InlineData("x-attack", "o:4:\"x\":5:{d}", "php_code_injection", "crs-933-170")]
        [InlineData("key", "<script>", "xss", "crs-941-110")]
        public void CookiesAttack(string key, string content, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestCookies,
                new Dictionary<string, string>
                {
                    {
                        key, content
                    }
                },
                flow,
                rule);
        }

        [Theory]
        [Trait("Category", "ArmUnsupported")]
        [InlineData("/.adsensepostnottherenonobook", "security_scanner", "crs-913-120")]
        public void BodyAttack(string body, string flow, string rule) => Execute(AddressesConstants.RequestBody, body, flow, rule);

        private static void Execute(string address, object value, string flow, string rule)
        {
            var args = new Dictionary<string, object>
            {
                {
                    address, value
                }
            };
            if (!args.ContainsKey(AddressesConstants.RequestUriRaw))
            {
                args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
            }

            if (!args.ContainsKey(AddressesConstants.RequestMethod))
            {
                args.Add(AddressesConstants.RequestMethod, "GET");
            }

            using var waf = Waf.Initialize(FileName);
            waf.Should().NotBeNull();
            using var context = waf.CreateContext();
            var result = context.Run(args);
            result.ReturnCode.Should().Be(ReturnCode.Monitor);
            var resultData = JsonConvert.DeserializeObject<WafMatch[]>(result.Data).FirstOrDefault();
            resultData.Rule.Tags.Type.Should().Be(flow);
            resultData.Rule.Id.Should().Be(rule);
            resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
        }
    }
}
