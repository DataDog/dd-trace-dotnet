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
    [InlineData("dummy_rule", "test-dummy-rule", "block", BlockingAction.BlockRequestType, 500)]
    [InlineData("dummyrule2", "test-dummy-rule2", "customblock", BlockingAction.BlockRequestType, 500)]
    // Redirect status code is restricted in newer versions of the waf to 301, 302, 303, 307
    [InlineData("dummyrule2", "test-dummy-rule2", "customblock", BlockingAction.RedirectRequestType, 303)]
    [InlineData("dummy_rule", "test-dummy-rule", "block", BlockingAction.RedirectRequestType, 303)]
    public void GivenADummyRule_WhenActionReturnCodeIsChanged_ThenChangesAreApplied(string paramValue, string rule, string action, string actionType, int newStatus)
    {
        var args = CreateArgs(paramValue);
        var initResult = Waf.Create(
            WafLibraryInvoker,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: true,
            embeddedRulesetPath: "rasp-rule-set.json");

        var waf = initResult.Waf;
        waf.Should().NotBeNull();
        using var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
        result.BlockInfo["status_code"].Should().Be("403");
        var jsonString = JsonConvert.SerializeObject(result.Data);
        var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
        resultData.Rule.Id.Should().Be(rule);
        var newAction = CreateNewStatusAction(action, actionType, newStatus);
        UpdateWafWithActions([newAction], waf);

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
    [InlineData("dummy_rule", "block", BlockingAction.BlockRequestType, 401)]
    [InlineData("dummy_rule", "block", BlockingAction.BlockRequestType, 401, true)]
    public void GivenADummyRule_WhenDuplicateActionIsReceived_ThenBackToDefaultWafActions(string paramValue, string action, string actionType, int newStatus, bool placeInDifferentFiles = false)
    {
        var args = CreateArgs(paramValue);
        var initResult = Waf.Create(
            WafLibraryInvoker!,
            string.Empty,
            string.Empty,
            useUnsafeEncoder: true,
            embeddedRulesetPath: "rasp-rule-set.json");

        var waf = initResult.Waf;
        waf.Should().NotBeNull();
        Action[] newActions =
        [
            CreateNewStatusAction(action, actionType, newStatus), CreateNewStatusAction(action, actionType, newStatus), CreateNewStatusAction(action, actionType, newStatus),
            CreateNewStatusAction("dummy_rule", BlockingAction.BlockRequestType, 500) // add a dummy one, otherwise nothing will be updated
        ];

        UpdateWafWithActions(newActions, waf, placeInDifferentFiles);

        using var context = waf!.CreateContext();
        var result = context!.Run(args, TimeoutMicroSeconds);
        result.Should().NotBeNull();
        result!.Timeout.Should().BeFalse("Timeout should be false");
        // default waf action block
        result.BlockInfo!["status_code"].Should().Be("403");
        result.BlockInfo["grpc_status_code"].Should().Be("10");
    }

    private Dictionary<string, object> CreateArgs(string requestParam) => new() { { AddressesConstants.RequestUriRaw, "http://localhost:54587/" }, { AddressesConstants.RequestBody, new[] { "param", requestParam } }, { AddressesConstants.RequestMethod, "GET" } };

    private void UpdateWafWithActions(Action[] actions, Waf waf, bool placeInDifferentFiles = false)
    {
        ConfigurationStatus configurationStatus;
        if (placeInDifferentFiles)
        {
            var i = 0;
            var dic = actions.ToDictionary<Action, string, Action[]>(_ => $"file{i++}", action => [action]);

            configurationStatus = new(string.Empty) { ActionsByFile = dic };
        }
        else
        {
            configurationStatus = new(string.Empty) { ActionsByFile = { ["file"] = actions } };
        }

        configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
        var res = waf.UpdateWafFromConfigurationStatus(configurationStatus);
        res.Success.Should().BeTrue();
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
