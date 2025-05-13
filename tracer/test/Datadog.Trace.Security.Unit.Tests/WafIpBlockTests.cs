// <copyright file="WafIpBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafIpBlockTests : WafLibraryRequiredTest
    {
        [Fact]
        public void TestOk()
        {
            // Create waf with default config
            var configurationState = CreateConfigurationState(null);
            var initResult = CreateWaf(configurationState);
            var waf = initResult.Waf!;
            waf.Should().NotBeNull();

            // Load rules data from file and send it as new ASM_Data payload
            var js = JsonSerializer.Create();
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            var payload = new Payload { RulesData = rulesData!.ToArray() };
            AddAsmDataRemoteConfig(configurationState, payload, "update1");
            var updateRes1 = UpdateWaf(configurationState, waf);
            updateRes1.Success.Should().BeTrue();
            updateRes1.HasRuleErrors.Should().BeFalse();
            using var context = waf.CreateContext();
            var result = context!.Run(new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "51.222.158.205" } }, TimeoutMicroSeconds);
            result!.Timeout.Should().BeFalse("Timeout should be false");
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(WafReturnCode.Match);
            result!.Actions.Should().NotBeEmpty();
            result!.Actions!.Should().ContainKey(BlockingAction.BlockRequestType);
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result!.Timeout.Should().BeFalse("Timeout should be false");
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(WafReturnCode.Ok);
            result.Actions.Should().BeEmpty();
        }
    }
}
