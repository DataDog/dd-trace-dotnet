// <copyright file="WafUpdateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafUpdateTests : WafLibraryRequiredTest
    {
        [Fact]
        public void RulesUpdate()
        {
            var initResult = CreateWaf();
            using var waf = initResult.Waf;
            Execute(waf, ["testrule", "testrule", "none"], false);
            var configurationState = UpdateConfigurationState();

            var result = WafConfigurator.DeserializeEmbeddedOrStaticRules("remote-rules.json");
            result.Should().NotBeNull();
            var ruleSet = RuleSet.From(result!);
            ruleSet.Should().NotBeNull();
            AddAsmDDRemoteConfig(configurationState, ruleSet, "update1");
            var res = UpdateWaf(configurationState, waf);
            res.Success.Should().BeTrue();
            res.ReportedDiagnostics.Rules.Loaded.Should().Be(1);
            res.RuleErrors.Should().BeNullOrEmpty();
            Execute(waf, new[] { "testrule", "testrule", "crs-942-290-new" }, true, BlockingAction.BlockRequestType);
        }

        [Theory]
        [InlineData("[$ne]|arg|crs-942-290", "attack|appscan_fingerprint|crs-913-120")]
        [InlineData("attack|appscan_fingerprint|crs-913-120", "value|sleep(10)|crs-942-160")]
        public void RuleToggling(string attack1, string attack2)
        {
            var attackParts1 = attack1.Split('|');
            attackParts1.Length.Should().Be(3);
            var attackParts2 = attack2.Split('|');
            attackParts2.Length.Should().Be(3);
            var initResult = CreateWaf();
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            var configurationState = UpdateConfigurationState();

            Execute(waf, attackParts1, true);
            Execute(waf, attackParts2, true);

            var ruleOverride1 = new RuleOverride { Enabled = false, Id = attackParts1[2] };
            AddAsmRemoteConfig(configurationState, new Payload { RuleOverrides = [ruleOverride1] }, "update1");
            var result1 = UpdateWaf(configurationState, waf);
            result1.Success.Should().BeTrue();
            result1.HasRuleErrors.Should().BeFalse();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            var ruleOverride2 = new RuleOverride { Enabled = false, Id = attackParts2[2] };
            AddAsmRemoteConfig(configurationState, new Payload { RuleOverrides = [ruleOverride2] }, "update2");
            var result2 = UpdateWaf(configurationState, waf);
            result2.Success.Should().BeTrue();
            result2.HasRuleErrors.Should().BeFalse();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, false);

            RemoveAsmRemoteConfig(configurationState, "update2");
            var result3 = UpdateWaf(configurationState, waf);
            result3.Success.Should().BeTrue();
            result3.HasRuleErrors.Should().BeFalse();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            RemoveAsmRemoteConfig(configurationState, "update1");
            var result4 = UpdateWaf(configurationState, waf);
            result4.Success.Should().BeTrue();
            result4.HasRuleErrors.Should().BeFalse();
            Execute(waf, attackParts1, true);
            Execute(waf, attackParts2, true);
        }

        [Theory]
        [InlineData("[$ne]|arg|crs-942-290", "attack|appscan_fingerprint|crs-913-120")]
        [InlineData("attack|appscan_fingerprint|crs-913-120", "value|sleep(10)|crs-942-160")]
        public void RuleActions(string attack1, string attack2)
        {
            var attackParts1 = attack1.Split('|');
            attackParts1.Length.Should().Be(3);
            var attackParts2 = attack2.Split('|');
            attackParts2.Length.Should().Be(3);
            var initResult = CreateWaf();
            var configurationState = UpdateConfigurationState();
            using (var waf = initResult.Waf)
            {
                waf.Should().NotBeNull();

                Execute(waf, attackParts1, true);
                Execute(waf, attackParts2, true);

                var ruleOverride = new RuleOverride { OnMatch = ["block"], Id = attackParts1[2] };
                AddAsmRemoteConfig(configurationState, new Payload { RuleOverrides = [ruleOverride] }, "update1");
                var result = UpdateWaf(configurationState, waf);
                result.Success.Should().BeTrue();
                result.HasRuleErrors.Should().BeFalse();
                Execute(waf, attackParts1, true, BlockingAction.BlockRequestType);
                Execute(waf, attackParts2, true);
            }
        }

        private static void Execute(Waf waf, string[] attackParts, bool isAttack, string expectedActionType = null)
        {
            var address = AddressesConstants.RequestQuery;
            object value = new Dictionary<string, string[]> { { attackParts[0], new[] { attackParts[1] } } };
            var args = new Dictionary<string, object> { { address, value } };
            if (!args.ContainsKey(AddressesConstants.RequestUriRaw))
            {
                args.Add(AddressesConstants.RequestUriRaw, "http://localhost:54587/");
            }

            if (!args.ContainsKey(AddressesConstants.RequestMethod))
            {
                args.Add(AddressesConstants.RequestMethod, "GET");
            }

            waf.Should().NotBeNull();
            using var context = waf.CreateContext();
            var result = context.Run(args, TimeoutMicroSeconds);
            result.Timeout.Should().BeFalse("Timeout should be false");
            var spectedResult = isAttack ? WafReturnCode.Match : WafReturnCode.Ok;
            result.ReturnCode.Should().Be(spectedResult);
            if (spectedResult == WafReturnCode.Match)
            {
                var rule = attackParts[2];
                var jsonString = JsonConvert.SerializeObject(result.Data);
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
                resultData.Rule.Id.Should().Be(rule);
                resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
            }

            if (expectedActionType != null)
            {
                result.ShouldBlock.Should().BeTrue();
                result.BlockInfo.Should().NotBeNull();
                result.Actions.Keys.Should().OnlyContain(s => s == expectedActionType);
            }
        }
    }
}
