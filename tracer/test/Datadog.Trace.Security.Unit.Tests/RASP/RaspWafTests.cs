// <copyright file="RaspWafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Rasp.Unit.Tests;

public class RaspWafTests : WafLibraryRequiredTest
{
    [Theory]
    [InlineData("../../../../../../../../../etc/passwd", "../../../../../../../../../etc/passwd", "rasp-001-001", "rasp-rule-set.json", "customblock", BlockingAction.BlockRequestType)]
    public void GivenALfiRule_WhenActionReturnCodeIsChanged_ThenChangesAreApplied(string value, string paramValue, string rule, string ruleFile, string action, string actionType)
    {
        var args = CreateArgs(paramValue);
        var context = InitWaf(true, ruleFile, args, out var waf);

        var argsVulnerable = new Dictionary<string, object> { { AddressesConstants.FileAccess, value } };
        var resultEph = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds);
        resultEph.BlockInfo["status_code"].Should().Be("403");
        var jsonString = JsonConvert.SerializeObject(resultEph.Data);
        var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
        resultData.Rule.Id.Should().Be(rule);

        ConfigurationStatus configurationStatus = new(string.Empty);
        var newAction = CreateNewStatusAction(action, actionType, 500);
        configurationStatus.Actions[action] = newAction;
        configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
        var res = waf.UpdateWafFromConfigurationStatus(configurationStatus);
        res.Success.Should().BeTrue();

        context = waf.CreateContext();
        context.Run(args, TimeoutMicroSeconds);
        var resultEphNew = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds);
        resultEphNew.BlockInfo["status_code"].Should().Be("500");
    }

    [Theory]
    [InlineData("../../../../../../../../../etc/passwd", "../../../../../../../../../etc/passwd", "rasp-001-001", "rasp-rule-set.json", "customBlock", BlockingAction.BlockRequestType)]
    public void GivenAPathTraversalRule_WhenInsecureAccess_ThenBlock(string value, string paramValue, string rule, string ruleFile, string action, string actionType)
    {
        ExecuteRule(
            AddressesConstants.FileAccess,
            value,
            paramValue,
            rule,
            ruleFile,
            action,
            actionType);
    }

    private void ExecuteRule(string address, object value, string paramValue, string rule, string ruleFile, string expectedAction, string actionType)
    {
        ExecuteInternal(address, value, paramValue, rule, true, ruleFile, expectedAction, 1, actionType);
        ExecuteInternal(address, value, paramValue, rule, false, ruleFile, expectedAction, 1, actionType);
        ExecuteInternal(address, value, paramValue, rule, true, ruleFile, expectedAction, 5, actionType);
        ExecuteInternal(address, value, paramValue, rule, false, ruleFile, expectedAction, 5, actionType);
    }

    private void ExecuteInternal(string address, object value, string requestParam, string rule, bool newEncoder, string ruleFile, string expectedAction, int runNtimes, string actionType)
    {
        var args = CreateArgs(requestParam);
        var context = InitWaf(newEncoder, ruleFile, args, out _);

        var argsVulnerable = new Dictionary<string, object> { { address, value } };
        for (int i = 0; i < runNtimes; i++)
        {
            var resultEph = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds);
            CheckResult(rule, expectedAction, resultEph, actionType);
        }
        }

    private IContext InitWaf(bool newEncoder, string ruleFile, Dictionary<string, object> args, out Waf waf)
    {
        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: newEncoder,
            embeddedRulesetPath: ruleFile);
        waf = initResult.Waf;
        waf.Should().NotBeNull();
        var context = waf.CreateContext();
        context.Run(args, TimeoutMicroSeconds);
        return context;
    }

    private Dictionary<string, object> CreateArgs(string requestParam)
    {
        return new Dictionary<string, object>
        {
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.RequestBody, new[] { "param", requestParam } },
            { AddressesConstants.RequestMethod, "GET" }
        };
        }

    private void CheckResult(string rule, string expectedAction, IResult result, string actionType)
        {
            result.ReturnCode.Should().Be(WafReturnCode.Match);
        result.Actions.ContainsKey(actionType).Should().BeTrue();
            var jsonString = JsonConvert.SerializeObject(result.Data);
            var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
            resultData.Rule.Id.Should().Be(rule);
    }

    private Action CreateNewStatusAction(string action, string actionType, int newStatus)
    {
        var newAction = new Action();
        newAction.Id = action;
        newAction.Type = actionType;
        newAction.Parameters = new AppSec.Rcm.Models.Asm.Parameter();
        newAction.Parameters.StatusCode = newStatus;
        newAction.Parameters.Type = "auto";

        if (newAction.Type == BlockingAction.RedirectRequestType)
        {
            newAction.Parameters.Location = "/toto";
        }

        return newAction;
    }
}
