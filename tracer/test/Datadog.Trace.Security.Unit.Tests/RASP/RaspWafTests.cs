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
    [Theory]
    [InlineData("../../../../../../../../../etc/passwd", "../../../../../../../../../etc/passwd", "rasp-001-001", "rasp-rule-set.json", "block")]
    public void PathTraversalRule(string value, string paramValue, string rule, string ruleFile, string action)
    {
        Execute(
            AddressesConstants.FileAccess,
            value,
            paramValue,
            rule,
            ruleFile,
            action);
    }

    private void Execute(string address, object value, string vulnerabilityType, string rule = null, string ruleFile = null, string expectedAction = null)
    {
        ExecuteInternal(address, value, vulnerabilityType, rule, true, ruleFile, true, expectedAction);
        ExecuteInternal(address, value, vulnerabilityType, rule, true, ruleFile, false, expectedAction);
        ExecuteInternal(address, value, vulnerabilityType, rule, false, ruleFile, true, expectedAction);
        ExecuteInternal(address, value, vulnerabilityType, rule, false, ruleFile, false, expectedAction);
    }

    private void ExecuteInternal(string address, object value, string requestParam, string rule, bool newEncoder, string ruleFile, bool useTwoCalls, string expectedAction = null)
    {
        var args = new Dictionary<string, object>();

        if (!useTwoCalls)
        {
            args[address] = value;
        }

        args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
        args.Add(AddressesConstants.RequestBody, new[] { "param", requestParam });
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

        if (requestParam is not null)
        {
            result.ReturnCode.Should().Be(WafReturnCode.Match);
            if (!string.IsNullOrEmpty(expectedAction))
            {
                result.Actions[0].Should().BeEquivalentTo(expectedAction);
            }

            var jsonString = JsonConvert.SerializeObject(result.Data);
            var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
            resultData.Rule.Id.Should().Be(rule);
        }
    }
}
