// <copyright file="WafUserBlockTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
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
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    [Collection("WafTests")]
    public class WafUserBlockTests
    {
        [Fact]
        public void TestOk()
        {
            var libInitResult = WafLibraryInvoker.Initialize();
            if (!libInitResult.Success)
            {
                throw new ArgumentException("Waf couldn't load");
            }

            var js = JsonSerializer.Create();
            var initResult = Waf.Create(libInitResult.WafLibraryInvoker!, string.Empty, string.Empty);
            using var waf = initResult.Waf!;
            using var sr = new StreamReader("rule-data1.json");
            using var jsonTextReader = new JsonTextReader(sr);
            var rulesData = js.Deserialize<RuleData[]>(jsonTextReader);
            var res = waf.UpdateRulesData(rulesData!);
            res.Should().BeTrue();
            var readwriteLocker = new AppSec.Concurrency.ReaderWriterLock();
            using var context = waf.CreateContext(readwriteLocker)!;
            var result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user3" } },
                WafTests.TimeoutMicroSeconds);
            result.ReturnCode.Should().Be(ReturnCode.Match);
            result.Actions.Should().NotBeEmpty();
            result.Actions.Should().Contain("block");
            result = context.Run(
                new Dictionary<string, object> { { AddressesConstants.UserId, "user4" } },
                WafTests.TimeoutMicroSeconds);
            result.ReturnCode.Should().Be(ReturnCode.Ok);
            result.Actions.Should().BeEmpty();
        }
    }
}
