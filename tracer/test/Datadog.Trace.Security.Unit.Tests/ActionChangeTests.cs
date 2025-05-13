// <copyright file="ActionChangeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.Unit.Tests;

public class ActionChangeTests : WafLibraryRequiredTest
{
    [Theory]
    [InlineData("dummy_rule", "test-dummy-rule", "block", BlockingAction.BlockRequestType, 500)]
    // Redirect status code is restricted in newer versions of the waf to 301, 302, 303, 307
    [InlineData("dummy_rule", "test-dummy-rule", "block", BlockingAction.RedirectRequestType, 303)]
    public void GivenADummyRule_WhenActionReturnCodeIsChanged_ThenChangesAreApplied(string paramValue, string rule, string action, string actionType, int newStatus)
    {
        var configurationState = CreateConfigurationState("rasp-rule-set.json");
        var initResult = CreateWaf(configurationState, true);
        var waf = initResult.Waf;
        using var context = waf.CreateContext();
        var args = CreateArgs(paramValue);
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        result.BlockInfo["status_code"].Should().Be("403");
        var jsonString = JsonConvert.SerializeObject(result.Data);
        var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
        resultData.Rule.Id.Should().Be(rule);
        var newAction = CreateNewStatusAction(action, actionType, newStatus);
        UpdateWafWithActions([newAction], waf, configurationState, "update1");

        using var contextNew = waf.CreateContext();
        result = contextNew.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        if (actionType == BlockingAction.BlockRequestType)
        {
            result.BlockInfo.Should().NotBeNull();
            result.BlockInfo["status_code"].Should().Be(newStatus.ToString());
        }

        if (actionType == BlockingAction.RedirectRequestType)
        {
            result.RedirectInfo.Should().NotBeNull();
            result.RedirectInfo["status_code"].Should().Be(newStatus.ToString());
        }
    }

    [Fact]
    public void GivenADummyRule_WhenActionReturnCodeIsChangedAfterInit_ThenChangesAreApplied()
    {
        var configurationState = CreateConfigurationState("rasp-rule-set.json");
        var initResult = CreateWaf(configurationState, true);
        var waf = initResult.Waf;
        var args = CreateArgs("dummyrule2");

        UpdateWafWithActions([CreateNewStatusAction("block", BlockingAction.BlockRequestType, 500)], waf, configurationState, "update1");

        using var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        result.BlockInfo["status_code"].Should().Be("500");
    }

    private Dictionary<string, object> CreateArgs(string requestParam) => new() { { AddressesConstants.RequestUriRaw, "http://localhost:54587/" }, { AddressesConstants.RequestBody, new[] { "param", requestParam } }, { AddressesConstants.RequestMethod, "GET" } };

    private UpdateResult UpdateWafWithActions(Action[] actions, Waf waf, ConfigurationState configurationState, string updateId)
    {
        AddAsmRemoteConfig(configurationState, new Payload { Actions = actions }, updateId);
        var res = UpdateWaf(configurationState, waf);
        res.Success.Should().BeTrue();
        return res;
    }

    private Action CreateNewStatusAction(string action, string actionType, int newStatus)
    {
        var newAction = new Action { Id = action, Type = actionType, Parameters = new AppSec.Rcm.Models.Asm.Parameter { StatusCode = newStatus, Type = "auto" } };

        if (newAction.Type == BlockingAction.RedirectRequestType)
        {
            newAction.Parameters.Location = "/toto";
        }

        return newAction;
    }
}
