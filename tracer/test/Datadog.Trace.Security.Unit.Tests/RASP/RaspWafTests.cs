// <copyright file="RaspWafTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.Unit.Tests;

public class RaspWafTests : WafLibraryRequiredTest
{
    private static bool enableDebug = false;

    [Theory]
    [InlineData(1, 1000000, false)]
    [InlineData(1000, 1, true)]
    public void GivenALfiRule_WhenInsecureAccess_TimeoutIsCorrect(int queryLength, ulong timeout, bool shouldRaiseTimeout)
    {
        var file = "folder";

        for (int i = 0; i < queryLength; i++)
        {
            file += $"/folder";
        }

        var etcPasswd = "../../../../../../../../../etc/passwd";
        file += etcPasswd;
        var args = CreateArgs(etcPasswd);
        var context = InitWaf(true, "rasp-rule-set.json", args, out _);
        var argsVulnerable = new Dictionary<string, object> { { AddressesConstants.FileAccess, file } };
        var resultEph = context.RunWithEphemeral(argsVulnerable, timeout, true);
        resultEph.Timeout.Should().Be(shouldRaiseTimeout);
        if (!shouldRaiseTimeout)
        {
            resultEph.ShouldBlock.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("../../../../../../../../../etc/passwd", "../../../../../../../../../etc/passwd", "rasp-001-001", "rasp-rule-set.json", "customblock1", BlockingAction.BlockRequestType)]
    public void GivenALfiRule_WhenActionReturnCodeIsChanged_ThenChangesAreApplied(string value, string paramValue, string rule, string ruleFile, string action, string actionType)
    {
        var args = CreateArgs(paramValue);
        var configurationState = CreateConfigurationState(ruleFile);
        var context = InitWaf(configurationState, true, args, out var waf);

        // Default config does not block
        var argsVulnerable = new Dictionary<string, object> { { AddressesConstants.FileAccess, value } };
        var resultEph = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds, true);
        resultEph.BlockInfo["status_code"].Should().Be(403);
        resultEph.Timeout.Should().BeFalse("Timeout should be false");
        var jsonString = JsonConvert.SerializeObject(resultEph.Data);
        var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
        resultData.Rule.Id.Should().Be(rule);

        // New action bloking with 500 (and remove previous one)
        var newAction1 = CreateNewStatusAction(action, actionType, 500);
        var ruleOverride = new RuleOverride { OnMatch = new[] { action }, Id = rule };
        AddAsmRemoteConfig(configurationState, new Payload { Actions = [newAction1], RuleOverrides = [ruleOverride] }, "update1");
        var updateRes1 = UpdateWaf(configurationState, waf, ref context);
        updateRes1.Success.Should().BeTrue();
        context.Run(args, TimeoutMicroSeconds);
        var resultEph1 = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds, true);
        resultEph1.Timeout.Should().BeFalse("Timeout should be false");
        resultEph1.BlockInfo["status_code"].Should().Be(500);
        resultEph1.AggregatedTotalRuntimeRasp.Should().BeGreaterThan(0);
        resultEph1.AggregatedTotalRuntimeWithBindingsRasp.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("select * from employees where name = 'John' or '1' = '1'", "John' or '1' = '1", "rasp-942-100", BlockingAction.BlockDefaultActionName, BlockingAction.BlockRequestType, AddressesConstants.DBStatement)]
    [InlineData("../../../../../../../../../etc/passwd", "../../../../../../../../../etc/passwd", "rasp-001-001", "customBlock", BlockingAction.BlockRequestType, AddressesConstants.FileAccess)]
    [InlineData("https://169.254.169.254/somewhere/in/the/app", "169.254.169.254", "rasp-002-001", BlockingAction.BlockDefaultActionName, BlockingAction.BlockRequestType, AddressesConstants.DownstreamUrl)]
    [InlineData("ls; echo hello", "echo hello", "rasp-932-100", BlockingAction.BlockDefaultActionName, BlockingAction.BlockRequestType, AddressesConstants.ShellInjection)]
    [InlineData("ls &> file; echo hello", "&> file", "rasp-932-100", BlockingAction.BlockDefaultActionName, BlockingAction.BlockRequestType, AddressesConstants.ShellInjection)]
    [InlineData(new string[] { "/usr/bin/reboot" }, "/usr/bin/reboot", "rasp-932-110", BlockingAction.BlockDefaultActionName, BlockingAction.BlockRequestType, AddressesConstants.CommandInjection)]
    public void GivenARaspRule_WhenInsecureAccess_ThenBlock(object value, string paramValue, string rule, string action, string actionType, string address)
    {
        EnableDebugInfo(enableDebug);

        ExecuteRule(
            address,
            value,
            paramValue,
            rule,
            "rasp-rule-set.json",
            action,
            actionType);
    }

    [Fact]
    public void GivenWafInstance_WhenGetKnownAddressesInParallel_ThenResultIsOk()
    {
        var args = CreateArgs("paramValue");
        var context = InitWaf(true, "rasp-rule-set.json", args, out var waf);

        Parallel.For(0, 100, i =>
        {
            var addresses = waf.GetKnownAddresses();
            addresses.Should().NotBeNull();
            addresses.Count().Should().BeGreaterThan(0);
        });
    }

    private void EnableDebugInfo(bool enable)
    {
        if (enable)
        {
            DatadogLogging.SetLogLevel(LogEventLevel.Debug);
        }
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

        if (address == AddressesConstants.DBStatement)
        {
            argsVulnerable.Add("server.db.system", "sqlite");
        }

        for (int i = 0; i < runNtimes; i++)
        {
            var resultEph = context.RunWithEphemeral(argsVulnerable, TimeoutMicroSeconds, true);
            CheckResult(rule, expectedAction, resultEph, actionType);
        }
    }

    private IContext InitWaf(bool newEncoder, string ruleFile, Dictionary<string, object> args, out Waf waf)
    {
        var configurationState = CreateConfigurationState(ruleFile);
        return InitWaf(configurationState, newEncoder, args, out waf);
    }

    private IContext InitWaf(ConfigurationState configurationState, bool newEncoder, Dictionary<string, object> args, out Waf waf)
    {
        var initResult = CreateWaf(configurationState, newEncoder, wafDebugEnabled: enableDebug);
        waf = initResult.Waf;
        var context = waf.CreateContext();
        var result = context.Run(args, TimeoutMicroSeconds);
        result.Timeout.Should().BeFalse("Timeout should be false");
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
        result.Timeout.Should().BeFalse("Timeout should be false");
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
        newAction.Parameters.Location = string.Empty;

        if (newAction.Type == BlockingAction.RedirectRequestType)
        {
            newAction.Parameters.Location = "/toto";
        }

        return newAction;
    }
}
