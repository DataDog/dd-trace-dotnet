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
    [InlineData(false, "{\r\n  \"version\": \"2.2\",\r\n  \"metadata\": {\r\n    \"rules_version\": \"1.10.0\"\r\n  },\r\n\r\n  \"rules\": [\r\n    {\r\n      \"id\": \"rasp-932-400\",\r\n      \"name\": \"New exploit\",\r\n      \"enabled\": true,\r\n      \"tags\": {\r\n        \"type\": \"new_injection\",\r\n        \"category\": \"vulnerability_trigger\",\r\n        \"cwe\": \"77\",\r\n        \"capec\": \"1000/152/248/88\",\r\n        \"confidence\": \"0\",\r\n        \"module\": \"rasp\"\r\n      },\r\n      \"conditions\": [\r\n        {\r\n          \"parameters\": {\r\n            \"resource\": [\r\n              {\r\n                \"address\": \"server.sys.shell.cmd\"\r\n              }\r\n            ],\r\n            \"params\": [\r\n              {\r\n                \"address\": \"server.request.query\"\r\n              },\r\n              {\r\n                \"address\": \"server.request.body\"\r\n              },\r\n              {\r\n                \"address\": \"server.request.path_params\"\r\n              },\r\n              {\r\n                \"address\": \"grpc.server.request.message\"\r\n              },\r\n              {\r\n                \"address\": \"graphql.server.all_resolvers\"\r\n              },\r\n\r\n              {\r\n                \"address\": \"graphql.server.resolver\"\r\n              }\r\n            ]\r\n          },\r\n          \"operator\": \"new_detector\"\r\n        }\r\n      ],\r\n      \"transformers\": [],\r\n      \"on_match\": [\r\n        \"stack_trace\"\r\n      ]\r\n    }\r\n  ]\r\n}")]
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
