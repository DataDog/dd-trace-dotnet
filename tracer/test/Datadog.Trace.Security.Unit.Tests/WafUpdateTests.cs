// <copyright file="WafUpdateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafUpdateTests : WafLibraryRequiredTest
    {
        public const int TimeoutMicroSeconds = 1_000_000;

        public WafUpdateTests(WafLibraryInvokerFixture wafLibraryInvokerFixture)
            : base(wafLibraryInvokerFixture)
        {
        }

        [Fact]
        public void RulesUpdate()
        {
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            Execute(waf, new[] { "testrule", "testrule", "none" }, false);

            using var sr = new StreamReader("remote-rules.json");
            var res = waf!.UpdateRules(sr.ReadToEnd());
            res.Success.Should().BeTrue();
            res.LoadedRules.Should().Be(1);
            res.Errors.Should().BeEmpty();
            Execute(waf, new[] { "testrule", "testrule", "crs-942-290-new" }, true, "block");
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
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            var ruleStatuses = new List<RuleOverride>();

            Execute(waf, attackParts1, true);
            Execute(waf, attackParts2, true);

            var ruleOverride = new RuleOverride { Enabled = false, Id = attackParts1[2] };
            ruleStatuses.Add(ruleOverride);
            var result = waf.UpdateRulesStatus(ruleStatuses, new List<JToken>());
            result.Should().BeTrue();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            ruleStatuses.Add(new RuleOverride { Enabled = false, Id = attackParts2[2] });
            result = waf.UpdateRulesStatus(ruleStatuses, new List<JToken>());
            result.Should().BeTrue();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, false);

            ruleStatuses.RemoveAt(1);
            ruleStatuses.Add(new RuleOverride { Enabled = true, Id = attackParts2[2] });
            result = waf.UpdateRulesStatus(ruleStatuses, new List<JToken>());
            result.Should().BeTrue();
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            ruleStatuses.RemoveAt(0);
            ruleStatuses.Add(new RuleOverride { Enabled = true, Id = attackParts1[2] });
            result = waf.UpdateRulesStatus(ruleStatuses, new List<JToken>());
            result.Should().BeTrue();
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
            var initresult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using (var waf = initresult.Waf)
            {
                waf.Should().NotBeNull();
                var ruleStatuses = new List<RuleOverride>();

                Execute(waf, attackParts1, true);
                Execute(waf, attackParts2, true);

                var ruleOverride = new RuleOverride { OnMatch = new[] { "block" }, Id = attackParts1[2] };
                ruleStatuses.Add(ruleOverride);
                var result = waf.UpdateRulesStatus(ruleStatuses, new List<JToken>());
                result.Should().BeTrue();
                Execute(waf, attackParts1, true, "block");
                Execute(waf, attackParts2, true, null);
            }
        }

        private static void Execute(Waf waf, string[] attackParts, bool isAttack, string expectedAction = null)
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
            var spectedResult = isAttack ? ReturnCode.Match : ReturnCode.Ok;
            result.ReturnCode.Should().Be(spectedResult);
            if (spectedResult == ReturnCode.Match)
            {
                var rule = attackParts[2];
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(result.Data).FirstOrDefault();
                resultData.Rule.Id.Should().Be(rule);
                resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
            }

            if (expectedAction != null)
            {
                result.ShouldBlock.Should().BeTrue();
                result.Actions.Should().OnlyContain(s => s == expectedAction);
            }
        }
    }
}
