// <copyright file="RaspWafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Rasp.Unit.Tests;

public class RaspWafTests : WafLibraryRequiredTest
{
    public const int TimeoutMicroSeconds = 1_000_000;

    [Theory]
    [InlineData("/etc/password", "lfi", "rasp-001-001", "rasp-rule-set.json")]
    public void PathTraversalRule(string value, string vulnerabilityType, string rule, string ruleFile)
    {
        Execute(
            AddressesConstants.FileAccess,
            value,
            vulnerabilityType,
            rule,
            ruleFile);
    }

    private void Execute(string address, object value, string vulnerabilityType, string rule, string ruleFile)
    {
        ExecuteInternal(address, value, vulnerabilityType, rule, true, ruleFile, true);
        ExecuteInternal(address, value, vulnerabilityType, rule, false, ruleFile, false);
        ExecuteInternal(address, value, vulnerabilityType, rule, true, ruleFile, true);
        ExecuteInternal(address, value, vulnerabilityType, rule, false, ruleFile, false);
    }

    private void ExecuteInternal(string address, object value, string vulnerabilityType, string rule, bool newEncoder, string ruleFile, bool useTwoCalls)
    {
        var args = new Dictionary<string, object>();

        if (!useTwoCalls)
        {
            args[address] = value;
        }

        args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
        args.Add(AddressesConstants.RequestPathParams, new[] { "file", vulnerabilityType });
        args.Add(AddressesConstants.RequestMethod, "GET");

        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: newEncoder,
            embeddedRulesetPath: ruleFile);
        using var waf = initResult.Waf;
        waf.Should().NotBeNull();
        using var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);

        if (useTwoCalls)
        {
            args.Clear();
            args[address] = value;
            result = context.Run(args, TimeoutMicroSeconds);
        }

        if (vulnerabilityType is not null)
        {
            result.ReturnCode.Should().Be(WafReturnCode.Match);
            if (!string.IsNullOrEmpty(expectedAction))
            {
                result.Actions[0].Should().BeEquivalentTo(expectedAction);
            }

            var jsonString = JsonConvert.SerializeObject(result.Data);
            var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
            resultData.Rule.Tags.Type.Should().Be(vulnerabilityType);
            resultData.Rule.Id.Should().Be(rule);
            resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
        }
    }
}
