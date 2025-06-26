// <copyright file="RASPRcmTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class RaspRcmTests : SettingsTestsBase
{
    [Theory]
    // New operator
    [InlineData(false, "{\r\n\t\"version\": \"2.2\",\r\n\t\"metadata\": {\r\n\t\t\"rules_version\": \"1.10.0\"\r\n\t},\r\n\t\"rules\": [\r\n\t\t{\r\n\t\t\t\"id\": \"crs-932-160\",\r\n\t\t\t\"name\": \"Remote Command Execution: Unix Shell Code Found\",\r\n\t\t\t\"tags\": {\r\n\t\t\t\t\"type\": \"command_injection\",\r\n\t\t\t\t\"crs_id\": \"932160\",\r\n\t\t\t\t\"category\": \"attack_attempt\",\r\n\t\t\t\t\"cwe\": \"77\",\r\n\t\t\t\t\"capec\": \"1000/152/248/88\",\r\n\t\t\t\t\"confidence\": \"1\"\r\n\t\t\t},\r\n\t\t\t\"conditions\": [\r\n\t\t\t\t{\r\n\t\t\t\t\t\"parameters\": {\r\n\t\t\t\t\t\t\"inputs\": [\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"server.request.query\"\r\n\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"server.request.body\"\r\n\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"server.request.path_params\"\r\n\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"grpc.server.request.message\"\r\n\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"graphql.server.all_resolvers\"\r\n\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\"address\": \"graphql.server.resolver\"\r\n\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t],\r\n\t\t\t\t\t\t\"list\": [\r\n\t\t\t\t\t\t\t\"dev/zero\"\r\n\t\t\t\t\t\t]\r\n\t\t\t\t\t},\r\n\t\t\t\t\t\"operator\": \"phrase_match\"\r\n\t\t\t\t}\r\n\t\t\t],\r\n\t\t\t\"transformers\": [\r\n\t\t\t\t\"lowercase\",\r\n\t\t\t\t\"cmdLine\"\r\n\t\t\t],\r\n\t\t\t\"on_match\": [\r\n\t\t\t\t\"block\"\r\n\t\t\t]\r\n\t\t}\r\n\t]\r\n}")]
    [InlineData(true, "INVALID")]
    // Missing key error
    [InlineData(true, "{\r\n\t\"version\": \"2.2\",\r\n\t\"metadata\": {\r\n\t\t\"rules_version\": \"1.10.0\"\r\n\t},\r\n\t\"rules\": [\r\n\t\t{\r\n\t\t\t\"enabled\": true,\r\n\t\t\t\"tags\": {\r\n\t\t\t\t\"cwe\": \"77\",\r\n\t\t\t\t\"capec\": \"1000/152/248/88\",\r\n\t\t\t\t\"confidence\": \"0\",\r\n\t\t\t\t\"module\": \"rasp\"\r\n\t\t\t}\r\n\t\t}\r\n\t]\r\n}")]
    public void GivenANewOperator_WhenUpdateFromRcm_NoErrorIsReported(bool errorExpected, string rules)
    {
        var remoteConfigValues = CreateRemoteConfigValues(rules);
        var security = CreateSecurity();
        var securitytype = typeof(AppSec.Security);
        var method = securitytype.GetMethod("UpdateFromRcm", BindingFlags.NonPublic | BindingFlags.Instance);
        var result = (ApplyDetails[])method.Invoke(security, [remoteConfigValues, null]);
        result.Length.Should().Be(1);

        if (errorExpected)
        {
            AssertHasErrors(result);
        }
        else
        {
            AssertNoErrors(result);
        }
    }

    private static void AssertHasErrors(ApplyDetails[] result)
    {
        result[0].Error.Should().NotBeEmpty();
        result[0].ApplyState.Should().Be(ApplyStates.ERROR);
    }

    private static AppSec.Security CreateSecurity()
    {
        var source = CreateConfigurationSource([(ConfigurationKeys.AppSec.Enabled, "true")]);
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        var confState = new ConfigurationState(settings, true);
        //// Set it to false to avoid all the initialization
        var security = new AppSec.Security(settings, configurationState: confState);
        confState.AppsecEnabled = true;
        return security;
    }

    private static Dictionary<string, List<RemoteConfiguration>> CreateRemoteConfigValues(string rules)
    {
        var content = Encoding.UTF8.GetBytes(rules);
        RemoteConfiguration config = new RemoteConfiguration(RemoteConfigurationPath.FromPath("employee/john/doe/smith"), content, content.Length, new Dictionary<string, string>(), 33);
        var dic = new Dictionary<string, List<RemoteConfiguration>>
        {
            ["ASM_DD"] = [config]
        };
        return dic;
    }

    private void AssertNoErrors(ApplyDetails[] result)
    {
        foreach (var applyDetails in result)
        {
            applyDetails.Error.Should().BeNullOrEmpty();
            applyDetails.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        }
    }
}
