// <copyright file="WafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafTests : WafLibraryRequiredTest
    {
        [Theory]
        [InlineData("[$ne]", "arg", "nosql_injection", "crs-942-290")]
        [InlineData("attack", "appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("key", "<script>", "xss", "crs-941-110")]
        [InlineData("value", "sleep(10)", "sql_injection", "crs-942-160")]
        public void QueryStringAttack(string key, string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestQuery,
                new Dictionary<string, string[]> { { key, new string[] { attack } } },
                flow,
                rule);
        }

        [Theory]
        [InlineData("something", "appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("something", "/.htaccess", "lfi", "crs-930-120")]
        public void PathParamsAttack(string key, string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestPathParams,
                new Dictionary<string, string[]> { { key, new string[] { attack } } },
                flow,
                rule);
        }

        [Fact]
        public void UrlRawAttack()
        {
            Execute(
                AddressesConstants.RequestUriRaw,
                "http://localhost:54587/waf/0x5c0x2e0x2e0x2f",
                "lfi",
                "crs-930-100");
        }

        [Theory]
        [InlineData("user-agent", "Arachni/v1", "attack_tool", "ua0-600-12x")]
        [InlineData("referer", "<script >", "xss", "crs-941-110")]
        [InlineData("x-file-name", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("x-filename", "routing.yml", "command_injection", "crs-932-180")]
        [InlineData("x_filename", "routing.yml", "command_injection", "crs-932-180")]
        public void HeadersAttack(string header, string content, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestHeaderNoCookies,
                new Dictionary<string, string> { { header, content } },
                flow,
                rule);
        }

        [Theory(Skip = "Cookies rules has been removed in rules version 1.2.7. Test on cookies are now done on custom rules scenario. Once we have rules with cookie back in the default rules set, we can re-use this class to validated this feature")]
        [InlineData("attack", ".htaccess", "lfi", "crs-930-120")]
        [InlineData("value", "/*!*/", "sql_injection", "crs-942-100")]
        [InlineData("value", ";shutdown--", "sql_injection", "crs-942-280")]
        [InlineData("key", ".cookie-;domain=", "http_protocol_violation", "crs-943-100")]
        [InlineData("x-attack", " var_dump ()", "php_code_injection", "crs-933-160")]
        [InlineData("x-attack", "o:4:\"x\":5:{d}", "php_code_injection", "crs-933-170")]
        [InlineData("key", "<script>", "xss", "crs-941-110")]
        public void CookiesAttack(string key, string content, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestCookies,
                new Dictionary<string, List<string>> { { key, new List<string> { content } } },
                flow,
                rule);
        }

        [Theory]
        [InlineData("/.adsensepostnottherenonobook", "security_scanner", "crs-913-120")]
        public void BodyAttack(string body, string flow, string rule) => Execute(AddressesConstants.RequestBody, body, flow, rule);

        [Fact]
        public void SchemaBodyExtraction()
        {
            Execute(
                AddressesConstants.RequestBody,
                new Dictionary<string, object>
                {
                    { "property1", "/.adsensepostnottherenonobook" },
                    { "property2", 2 },
                    { "property3", 3.10 },
                    { "property4", 5.10M },
                    { "property5", true },
                    { "property6", 10u }
                },
                "security_scanner",
                "crs-913-120",
                schemaExtraction: """{"_dd.appsec.s.req.body":[{"property1":[8],"property2":[4],"property3":[16],"property4":[16],"property5":[2],"property6":[4] }]}""");
        }

        [Fact]
        public void SchemaRequestExtraction()
        {
            Execute(
                AddressesConstants.RequestHeaderNoCookies,
                new Dictionary<string, object> { { "Content-Type", "/.adsensepostnottherenonobook" }, },
                schemaExtraction: """{"_dd.appsec.s.req.headers":[{"Content-Type":[8]}]}""");
        }

        private void Execute(string address, object value, string flow = null, string rule = null, string schemaExtraction = null)
        {
            ExecuteInternal(address, value, flow, rule, schemaExtraction, false);
            ExecuteInternal(address, value, flow, rule, schemaExtraction, true);
        }

        private void ExecuteInternal(string address, object value, string flow, string rule, string schemaExtraction, bool newEncoder)
        {
            var args = new Dictionary<string, object> { { address, value } };
            var extractSchema = schemaExtraction is not null;
            if (extractSchema)
            {
                args.Add(AddressesConstants.WafContextProcessor, new Dictionary<string, bool> { { "extract-schema", true } });
            }

            if (!args.ContainsKey(AddressesConstants.RequestUriRaw))
            {
                args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
            }

            if (!args.ContainsKey(AddressesConstants.RequestMethod))
            {
                args.Add(AddressesConstants.RequestMethod, "GET");
            }

            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty, useUnsafeEncoder: newEncoder);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            using var context = waf.CreateContext();
            var result = context.Run(args, TimeoutMicroSeconds);
            result.Timeout.Should().BeFalse("Timeout should be false");
            if (flow is not null)
            {
                result.ReturnCode.Should().Be(WafReturnCode.Match);
                var jsonString = JsonConvert.SerializeObject(result.Data);
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
                resultData.Rule.Tags.Type.Should().Be(flow);
                resultData.Rule.Id.Should().Be(rule);
                resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
            }

            if (extractSchema)
            {
                var serializedDerivatives = JsonConvert.SerializeObject(result.Derivatives);
                serializedDerivatives.Should().BeJsonEquivalentTo(schemaExtraction);
            }
        }
    }
}
