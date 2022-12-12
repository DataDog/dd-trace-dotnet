// <copyright file="WafUpdateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection("WafTests")]
    public class WafUpdateTests
    {
        public const int TimeoutMicroSeconds = 1_000_000;

        [Theory]
        [InlineData("[$ne]|arg|crs-942-290", "attack|appscan_fingerprint|crs-913-120")]
        [InlineData("attack|appscan_fingerprint|crs-913-120", "value|sleep(10)|crs-942-160")]
        public void RuleToggling(string attack1, string attack2)
        {
            var attackParts1 = attack1.Split('|');
            attackParts1.Length.Should().Be(3);
            var attackParts2 = attack2.Split('|');
            attackParts2.Length.Should().Be(3);

            using var waf = Waf.Create(string.Empty, string.Empty);
            waf.Should().NotBeNull();
            Dictionary<string, bool> ruleStatus = new Dictionary<string, bool>();

            Execute(waf, attackParts1, true);
            Execute(waf, attackParts2, true);

            ruleStatus[attackParts1[2]] = false;
            waf.ToggleRules(ruleStatus);
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            ruleStatus[attackParts2[2]] = false;
            waf.ToggleRules(ruleStatus);
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, false);

            ruleStatus[attackParts2[2]] = true;
            waf.ToggleRules(ruleStatus);
            Execute(waf, attackParts1, false);
            Execute(waf, attackParts2, true);

            ruleStatus[attackParts1[2]] = true;
            waf.ToggleRules(ruleStatus);
            Execute(waf, attackParts1, true);
            Execute(waf, attackParts2, true);
        }

        private static void Execute(Waf waf, string[] attackParts, bool isAttack)
        {
            string address = AddressesConstants.RequestQuery;
            object value = new Dictionary<string, string[]> { { attackParts[0], new string[] { attackParts[1] } } };
            string rule = attackParts[2];
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
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(result.Data).FirstOrDefault();
                resultData.Rule.Id.Should().Be(rule);
                resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
            }
        }
    }
}
