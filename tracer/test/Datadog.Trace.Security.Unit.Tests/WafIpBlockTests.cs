// <copyright file="WafIpBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafIpBlockTests : WafLibraryRequiredTest
    {
        public WafIpBlockTests(WafLibraryInvokerFixture wafLibraryInvokerFixture)
            : base(wafLibraryInvokerFixture)
        {
        }

        [Fact]
        public void TestOk()
        {
            var js = JsonSerializer.Create();
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            var waf = initResult.Waf;
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            waf!.Should().NotBeNull();
            var res = waf!.UpdateRulesData(rulesData!);
            res.Should().BeTrue();
            using var context = waf.CreateContext();
            var result = context!.Run(new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "51.222.158.205" } }, WafTests.TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(ReturnCode.Match);
            result!.Actions.Should().NotBeEmpty();
            result!.Actions.Should().Contain("block");
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(ReturnCode.Ok);
            result.Actions.Should().BeEmpty();
        }

        [Fact]
        public void TestMergeWithoutWaf()
        {
            var result = Waf.MergeRuleData(
                new RuleData[]
                {
                    new() { Id = "id1", Type = "type1", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = 10, Value = "2" }, new Data { Expiration = 10, Value = "3" } } },
                    new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = null, Value = "2" }, new Data { Expiration = 10, Value = "3" } } },
                    new() { Id = "id3", Type = "type3", Data = new[] { new Data { Expiration = 55, Value = "1" }, new Data { Expiration = 55, Value = "2" }, new Data { Expiration = 10, Value = "3" } } },
                    new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 30, Value = "1" }, new Data { Expiration = 30, Value = "2" }, new Data { Expiration = 30, Value = "3" } } }
                });

            result.Should().NotBeEmpty();
            result.Should().ContainItemsAssignableTo<RuleData>();
            result.Should().HaveCount(3);

            var expectedResult = new RuleData[]
            {
                new() { Id = "id1", Type = "type1", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = 10, Value = "2" }, new Data { Expiration = 10, Value = "3" } } },
                new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 30, Value = "1" }, new Data { Expiration = null, Value = "2" }, new Data { Expiration = 30, Value = "3" } } },
                new() { Id = "id3", Type = "type3", Data = new[] { new Data { Expiration = 55, Value = "1" }, new Data { Expiration = 55, Value = "2" }, new Data { Expiration = 10, Value = "3" } } }
            };

            result.Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public void TestMergeWithWaf()
        {
            var js = JsonSerializer.Create();
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);

            using var waf = initResult.Waf;
            using var sr = new StreamReader("rule-data1.json");
            using var sr2 = new StreamReader("rule-data2.json");
            using var jsonTextReader = new JsonTextReader(sr);
            using var jsonTextReader2 = new JsonTextReader(sr2);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            var rulesData2 = js.Deserialize<List<RuleData>>(jsonTextReader2);
            var res = waf!.UpdateRulesData(rulesData!.Concat(rulesData2!).ToList());
            res.Should().BeTrue();
            using var context = waf.CreateContext();
            var result = context!.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(ReturnCode.Match);
            result.Actions.Should().NotBeEmpty();
            result.Actions.Should().Contain("block");
            result.ShouldBlock.Should().BeTrue();
        }
    }
}
