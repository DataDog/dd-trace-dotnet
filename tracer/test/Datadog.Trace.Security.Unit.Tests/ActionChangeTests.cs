// <copyright file="ActionChangeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.Unit.Tests;

public class ActionChangeTests : WafLibraryRequiredTest
{
    [Theory]
    [InlineData("dummy_rule", "test-dummy-rule", "rasp-rule-set.json", "block", BlockingAction.BlockRequestType, 500)]
    [InlineData("dummyrule2", "test-dummy-rule2", "rasp-rule-set.json", "customblock", BlockingAction.BlockRequestType, 500)]
    // Redirect status code is restricted in newer versions of the WAF to 301, 302, 303, 307
    [InlineData("dummyrule2", "test-dummy-rule2", "rasp-rule-set.json", "customblock", BlockingAction.RedirectRequestType, 303)]
    [InlineData("dummy_rule", "test-dummy-rule", "rasp-rule-set.json", "block", BlockingAction.RedirectRequestType, 303)]
    public void GivenADummyRule_WhenActionReturnCodeIsChanged_ThenChangesAreApplied(string paramValue, string rule, string ruleFile, string action, string actionType, int newStatus)
    {
        var args = CreateArgs(paramValue);
        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: true,
            embeddedRulesetPath: ruleFile);

        var waf = initResult.Waf;
        waf.Should().NotBeNull();
        using var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        result.BlockInfo["status_code"].Should().Be("403");
        var jsonString = JsonConvert.SerializeObject(result.Data);
        var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
        resultData.Rule.Id.Should().Be(rule);

        ConfigurationStatus configurationStatus = new(string.Empty);
        var newAction = CreateNewStatusAction(action, actionType, newStatus);
        configurationStatus.Actions[action] = newAction;
        configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
        var res = waf.UpdateWafFromConfigurationStatus(configurationStatus);
        res.Success.Should().BeTrue();
        using var contextNew = waf.CreateContext();
        result = contextNew.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        if (actionType == BlockingAction.BlockRequestType)
        {
            result.BlockInfo["status_code"].Should().Be(newStatus.ToString());
        }

        if (actionType == BlockingAction.RedirectRequestType)
        {
            result.RedirectInfo["status_code"].Should().Be(newStatus.ToString());
        }
    }

    [Theory]
    [InlineData("dummyrule2", "rasp-rule-set.json", "customblock", BlockingAction.BlockRequestType)]
    public void GivenADummyRule_WhenActionReturnCodeIsChangedAfterInit_ThenChangesAreApplied(string paramValue, string ruleFile, string action, string actionType)
    {
        var args = CreateArgs(paramValue);
        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: true,
            embeddedRulesetPath: ruleFile);

        var waf = initResult.Waf;
        waf.Should().NotBeNull();

        UpdateAction(action, actionType, waf);

        using var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        result.BlockInfo["status_code"].Should().Be("500");
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

    private void UpdateAction(string action, string actionType, Waf waf)
    {
        ConfigurationStatus configurationStatus = new(string.Empty);
        var newAction = new Action();
        newAction.Id = action;
        newAction.Type = actionType;
        newAction.Parameters = new AppSec.Rcm.Models.Asm.Parameter();
        newAction.Parameters.StatusCode = 500;
        newAction.Parameters.Type = "auto";
        configurationStatus.Actions[action] = newAction;
        configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
        var res = waf.UpdateWafFromConfigurationStatus(configurationStatus);
        res.Success.Should().BeTrue();
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
