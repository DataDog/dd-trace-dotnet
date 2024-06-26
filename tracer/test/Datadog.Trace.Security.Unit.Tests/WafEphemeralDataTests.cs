// <copyright file="WafEphemeralDataTests.cs" company="Datadog">
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
    public class WafEphemeralDataTests : WafLibraryRequiredTest
    {
        [Theory]
        [InlineData("appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("<script>", "xss", "crs-941-110")]
        [InlineData("sleep(10)", "sql_injection", "crs-942-160")]
        public void FirstItemIsAttack(string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestQuery,
                new object[] { attack, "boring1", "boring2", },
                new[] { true, false, false },
                flow,
                rule);
        }

        [Theory]
        [InlineData("appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("<script>", "xss", "crs-941-110")]
        [InlineData("sleep(10)", "sql_injection", "crs-942-160")]
        public void MiddleItemIsAttack(string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestQuery,
                new object[] { "boring1", attack, "boring2" },
                new[] { false, true, false },
                flow,
                rule);
        }

        [Theory]
        [InlineData("appscan_fingerprint", "security_scanner", "crs-913-120")]
        [InlineData("<script>", "xss", "crs-941-110")]
        [InlineData("sleep(10)", "sql_injection", "crs-942-160")]
        public void LastItemIsAttack(string attack, string flow, string rule)
        {
            Execute(
                AddressesConstants.RequestQuery,
                new object[] { "boring1", "boring2", attack },
                new[] { false, false, true },
                flow,
                rule);
        }

        [Fact]
        public void AllItemsAreAttack()
        {
            Execute(
                AddressesConstants.RequestQuery,
                new object[] { "appscan_fingerprint", "<script>", "sleep(10)" },
                new[] { true, true, true },
                null,
                null);
        }

        private static Dictionary<string, object> MakeDictionary(string address, object value)
        {
            var args = new Dictionary<string, object> { { address, value } };

            if (!args.ContainsKey(AddressesConstants.RequestUriRaw))
            {
                args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
            }

            if (!args.ContainsKey(AddressesConstants.RequestMethod))
            {
                args.Add(AddressesConstants.RequestMethod, "GET");
            }

            return args;
        }

        private void Execute(string address, object[] values, bool[] matches, string flow = null, string rule = null)
        {
            ExecuteInternal(address, values, matches, flow, rule, true);
            ExecuteInternal(address, values, matches, flow, rule, false);
        }

        private void ExecuteInternal(string address, object[] values, bool[] matches, string flow, string rule, bool newEncoder)
        {
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty, useUnsafeEncoder: newEncoder);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            using var context = waf.CreateContext();

            for (var i = 0; i < values.Length; i++)
            {
                var args = MakeDictionary(address, values[i]);
                var result = context.RunWithEphemeral(args, TimeoutMicroSeconds, false);
                result.Timeout.Should().BeFalse("Timeout should be false");

                // by convention attack is last item in the array
                if (matches[i])
                {
                    result.ReturnCode.Should().Be(WafReturnCode.Match);
                    var jsonString = JsonConvert.SerializeObject(result.Data);
                    var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
                    if (flow != null)
                    {
                        resultData.Rule.Tags.Type.Should().Be(flow);
                    }

                    if (rule != null)
                    {
                        resultData.Rule.Id.Should().Be(rule);
                    }

                    resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
                }
                else
                {
                    result.ReturnCode.Should().Be(WafReturnCode.Ok);
                }
            }
        }
    }
}
