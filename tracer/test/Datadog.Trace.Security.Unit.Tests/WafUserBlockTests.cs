// <copyright file="WafUserBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafUserBlockTests : WafLibraryRequiredTest
    {
        [Fact]
        public void TestOk()
        {
            var js = JsonSerializer.Create();
            var initResult = CreateWaf();
            using var waf = initResult.Waf!;
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);

            var configurationStatus = UpdateConfigurationState();
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            var payload = new Payload { RulesData = rulesData!.ToArray() };
            AddAsmDataRemoteConfig(configurationStatus, payload, "001");
            configurationStatus.ApplyStoredFiles();
            var res = initResult.Waf!.Update(configurationStatus);
            res.Success.Should().BeTrue();
            res.HasRuleErrors.Should().BeFalse();
            using var context = waf.CreateContext()!;
            var result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user3" } },
                WafTests.TimeoutMicroSeconds);
            result!.Timeout.Should().BeFalse("Timeout should be false");
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(WafReturnCode.Match);
            result!.Actions.Should().NotBeEmpty();
            result!.Actions.Should().ContainKey(BlockingAction.BlockRequestType);
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user4" } },
                WafTests.TimeoutMicroSeconds);
            result!.Timeout.Should().BeFalse("Timeout should be false");
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(WafReturnCode.Ok);
            result!.Actions.Should().BeEmpty();
        }
    }
}
