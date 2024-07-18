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
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafIpBlockTests : WafLibraryRequiredTest
    {
        [Fact]
        public void TestOk()
        {
            var js = JsonSerializer.Create();
            var initResult = Waf.Create(WafLibraryInvoker!, string.Empty, string.Empty);
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            initResult.Waf.Should().NotBeNull();
            var configurationStatus = new ConfigurationStatus(string.Empty) { RulesDataByFile = { ["test"] = rulesData!.ToArray() } };
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesDataKey);
            var res = initResult.Waf!.UpdateWafFromConfigurationStatus(configurationStatus);
            res.Success.Should().BeTrue();
            res.HasErrors.Should().BeFalse();
            using var context = initResult.Waf.CreateContext();
            var result = context!.Run(new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "51.222.158.205" } }, WafTests.TimeoutMicroSeconds);
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

        [Fact]
        public void TestMergeWithoutWaf()
        {
            var result = Waf.MergeRuleData(
                new RuleData[] { new() { Id = "id1", Type = "type1", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = 10, Value = "2" }, new Data { Expiration = 10, Value = "3" } } }, new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = null, Value = "2" }, new Data { Expiration = 10, Value = "3" } } }, new() { Id = "id3", Type = "type3", Data = new[] { new Data { Expiration = 55, Value = "1" }, new Data { Expiration = 55, Value = "2" }, new Data { Expiration = 10, Value = "3" } } }, new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 30, Value = "1" }, new Data { Expiration = 30, Value = "2" }, new Data { Expiration = 30, Value = "3" } } } });

            result.Should().NotBeEmpty();
            result.Should().ContainItemsAssignableTo<RuleData>();
            result.Should().HaveCount(3);

            var expectedResult = new RuleData[] { new() { Id = "id1", Type = "type1", Data = new[] { new Data { Expiration = 10, Value = "1" }, new Data { Expiration = 10, Value = "2" }, new Data { Expiration = 10, Value = "3" } } }, new() { Id = "id2", Type = "type2", Data = new[] { new Data { Expiration = 30, Value = "1" }, new Data { Expiration = null, Value = "2" }, new Data { Expiration = 30, Value = "3" } } }, new() { Id = "id3", Type = "type3", Data = new[] { new Data { Expiration = 55, Value = "1" }, new Data { Expiration = 55, Value = "2" }, new Data { Expiration = 10, Value = "3" } } } };

            result.Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public void TestMergeWithWaf()
        {
            var js = JsonSerializer.Create();
            var initResult = Waf.Create(WafLibraryInvoker!, string.Empty, string.Empty);

            using var waf = initResult.Waf;
            waf.Should().NotBeNull();
            using var sr = new StreamReader("rule-data1.json");
            using var sr2 = new StreamReader("rule-data2.json");
            using var jsonTextReader = new JsonTextReader(sr);
            using var jsonTextReader2 = new JsonTextReader(sr2);
            var rulesData = js.Deserialize<List<RuleData>>(jsonTextReader);
            var rulesData2 = js.Deserialize<List<RuleData>>(jsonTextReader2);
            var configurationStatus = new ConfigurationStatus(string.Empty) { RulesDataByFile = { ["test"] = rulesData!.Concat(rulesData2!).ToArray() } };
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesDataKey);
            var res = initResult.Waf!.UpdateWafFromConfigurationStatus(configurationStatus);
            res.Success.Should().BeTrue();
            res.HasErrors.Should().BeFalse();
            using var context = waf!.CreateContext();
            var result = context!.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result!.Timeout.Should().BeFalse("Timeout should be false");
            result.Should().NotBeNull();
            result!.ReturnCode.Should().Be(WafReturnCode.Match);
            result.Actions.Should().NotBeEmpty();
            result.Actions.Should().ContainKey(BlockingAction.BlockRequestType);
            result.BlockInfo.Should().NotBeNull();
        }
    }
}
