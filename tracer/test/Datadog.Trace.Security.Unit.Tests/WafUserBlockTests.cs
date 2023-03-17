// <copyright file="WafUserBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafUserBlockTests : WafLibraryRequiredTest
    {
        public WafUserBlockTests(WafLibraryInvokerFixture wafLibraryInvokerFixture)
            : base(wafLibraryInvokerFixture)
        {
        }

        [Fact]
        public void TestOk()
        {
            var js = JsonSerializer.Create();
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf!;
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            var res = waf.UpdateRulesData(rulesData!);
            res.Should().BeTrue();
            using var context = waf.CreateContext()!;
            var result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user3" } },
                WafTests.TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(ReturnCode.Match);
            result!.Actions.Should().NotBeEmpty();
            result!.Actions.Should().Contain("block");
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user4" } },
                WafTests.TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(ReturnCode.Ok);
            result!.Actions.Should().BeEmpty();
        }
    }
}
