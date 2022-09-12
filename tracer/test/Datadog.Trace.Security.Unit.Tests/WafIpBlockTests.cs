// <copyright file="WafIpBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection("WafTests")]
    public class WafIpBlockTests
    {
        [Fact]
        public void TestOk()
        {
            var js = JsonSerializer.Create();
            using var waf = Waf.Create(string.Empty, string.Empty, "ruleset-withblockips.json");
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<RuleData[]>(jsonTextReader);
            var res = waf.UpdateRules(new List<RuleData[]> { rulesData! });
            res.Should().BeTrue();
            using var context = waf.CreateContext();
            var result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "51.222.158.205" } },
                WafTests.TimeoutMicroSeconds);
            result.ReturnCode.Should().Be(ReturnCode.Match);
            result.Actions.Should().NotBeEmpty();
            result.Actions.Should().Contain("block");
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result.ReturnCode.Should().Be(ReturnCode.Ok);
            result.Actions.Should().BeEmpty();
        }

        [Fact]
        public void TestMergeWithoutWaf()
        {
            var result = Waf.MergeRuleDatas(
                new[]
                {
                    new RuleData[]
                    {
                        new()
                        {
                            Id = "id1",
                            Type = "type1",
                            Data = new[]
                            {
                                new Data { Expiration = 10, Value = "1" },
                                new Data { Expiration = 10, Value = "2" },
                                new Data { Expiration = 10, Value = "3" }
                            }
                        },
                        new()
                        {
                            Id = "id2",
                            Type = "type2",
                            Data = new[]
                            {
                                new Data { Expiration = 10, Value = "1" },
                                new Data { Expiration = null, Value = "2" },
                                new Data { Expiration = 10, Value = "3" }
                            }
                        }
                    },
                    new RuleData[]
                    {
                        new()
                        {
                            Id = "id3",
                            Type = "type3",
                            Data = new[]
                            {
                                new Data { Expiration = 55, Value = "1" },
                                new Data { Expiration = 55, Value = "2" },
                                new Data { Expiration = 10, Value = "3" }
                            }
                        },
                        new()
                        {
                            Id = "id2",
                            Type = "type2",
                            Data = new[]
                            {
                                new Data { Expiration = 30, Value = "1" },
                                new Data { Expiration = 30, Value = "2" },
                                new Data { Expiration = 30, Value = "3" }
                            }
                        }
                    }
                });

            result.Should().NotBeEmpty();
            result.Should().ContainItemsAssignableTo<IDictionary<string, object>>();
            result.Should().HaveCount(3);

            var expectedResult = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "id", "id1" },
                    { "type", "type1" },
                    {
                        "data",
                        new List<object>
                        {
                            new Dictionary<string, object> { { "expiration", 10L }, { "value", "1" } },
                            new Dictionary<string, object> { { "expiration", 10L }, { "value", "2" } },
                            new Dictionary<string, object> { { "expiration", 10L }, { "value", "3" } },
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    { "id", "id2" },
                    { "type", "type2" },
                    {
                        "data",
                        new List<object>
                        {
                            new Dictionary<string, object> { { "expiration", 30L }, { "value", "1" } },
                            new Dictionary<string, object> { { "expiration", null }, { "value", "2" } },
                            new Dictionary<string, object> { { "expiration", 30L }, { "value", "3" } },
                        }
                    }
                },
                new Dictionary<string, object>
                {
                    { "id", "id3" },
                    { "type", "type3" },
                    {
                        "data",
                        new List<object>
                        {
                            new Dictionary<string, object> { { "expiration", 55L }, { "value", "1" } },
                            new Dictionary<string, object> { { "expiration", 55L }, { "value", "2" } },
                            new Dictionary<string, object> { { "expiration", 10L }, { "value", "3" } },
                        }
                    }
                }
            };
            result.Should().BeEquivalentTo(expectedResult);
        }

        [Fact]
        public void TestMergeWithWaf()
        {
            var js = JsonSerializer.Create();
            using var waf = Waf.Create(string.Empty, string.Empty, "ruleset-withblockips.json");
            using var sr = new StreamReader("rule-data1.json");
            using var sr2 = new StreamReader("rule-data2.json");
            using var jsonTextReader = new JsonTextReader(sr);
            using var jsonTextReader2 = new JsonTextReader(sr2);
            var rulesData = js.Deserialize<RuleData[]>(jsonTextReader);
            var rulesData2 = js.Deserialize<RuleData[]>(jsonTextReader2);
            var res = waf.UpdateRules(new List<RuleData[]> { rulesData!, rulesData2! });
            res.Should().BeTrue();
            using var context = waf.CreateContext();
            var result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.RequestClientIp, "188.243.182.156" } },
                WafTests.TimeoutMicroSeconds);
            result.ReturnCode.Should().Be(ReturnCode.Match);
            result.Actions.Should().NotBeEmpty();
            result.Actions.Should().Contain("block");
        }
    }
}
