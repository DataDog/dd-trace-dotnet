// <copyright file="WafMemoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class WafMemoryTests : WafLibraryRequiredTest
    {
        public const int OverheadMargin = 40_000_000; // 40Mb margin

        public void InitMemoryLeakCheck()
        {
            if (EnvironmentTools.IsLinux())
            {
                throw new SkipException("This is flaky on linux, because of unreliable PrivateMemorySize64, see https://github.com/dotnet/runtime/issues/72067");
            }

            var baseline = GetMemory();

            for (var x = 0; x < 250; x++)
            {
                Execute(AddressesConstants.RequestBody, "/.adsensepostnottherenonobook", "security_scanner", "crs-913-120");
            }

            var current = GetMemory(true);

            current.Should().BeLessThanOrEqualTo(baseline + OverheadMargin);
        }

        public void RunMemoryLeakCheck()
        {
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();

            var args = new Dictionary<string, object> { { AddressesConstants.RequestBody, "/.adsensepostnottherenonobook" } };

            var baseline = GetMemory();

            for (int x = 0; x < 1000; x++)
            {
                using var context = waf.CreateContext();
                var result = context.Run(args, TimeoutMicroSeconds);
                result.Timeout.Should().BeFalse("Timeout should be false");
                result.ReturnCode.Should().Be(WafReturnCode.Match);
                var jsonString = JsonConvert.SerializeObject(result.Data);
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
                resultData.Rule.Tags.Type.Should().Be("security_scanner");
                resultData.Rule.Id.Should().Be("crs-913-120");
            }

            var current = GetMemory(true);
            current.Should().BeLessThanOrEqualTo(baseline + OverheadMargin);
        }

        public void UpdateMemoryLeakCheck()
        {
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            waf.Should().NotBeNull();

            var baseline = GetMemory();

            bool enabled = false;

            for (int x = 0; x < 200; x++)
            {
                var ruleOverrides = new List<RuleOverride>();
                var ruleOverride = new RuleOverride { Enabled = enabled, Id = "crs-913-120" };
                ruleOverrides.Add(ruleOverride);
                var configurationStatus = new ConfigurationStatus(string.Empty) { RulesOverridesByFile = { ["test"] = ruleOverrides!.ToArray() } };
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
                var result = waf!.UpdateWafFromConfigurationStatus(configurationStatus);
                result.Success.Should().BeTrue();
                result.HasErrors.Should().BeFalse();

                Execute(waf, AddressesConstants.RequestBody, "/.adsensepostnottherenonobook", "security_scanner", "crs-913-120", enabled);

                enabled = !enabled;
            }

            var current = GetMemory(true);
            current.Should().BeLessThanOrEqualTo(baseline + OverheadMargin);
        }

        private long GetMemory(bool disposePool = false)
        {
            if (disposePool)
            {
                // Size of the unmanaged pool is already MaxBytesForMaxStringLength = (WafConstants.MaxStringLength * 4) + 1 *  BlockSize = 1000
                // > ((4096 * 4) + 1) * 1000
                // > 16.385.000
                // so give more margin for execution
                Encoder.Pool.Dispose();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
            var proc = Process.GetCurrentProcess();
            proc.Refresh();
            return proc.PrivateMemorySize64;
        }

        private void Execute(string address, object value, string flow, string rule)
        {
            var initResult = Waf.Create(WafLibraryInvoker, string.Empty, string.Empty);
            using var waf = initResult.Waf;
            Execute(waf, address, value, flow, rule);
        }

        private void Execute(Waf waf, string address, object value, string flow, string rule, bool isAttack = true)
        {
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
            if (isAttack)
            {
                result.ReturnCode.Should().Be(WafReturnCode.Match);
                var jsonString = JsonConvert.SerializeObject(result.Data);
                var resultData = JsonConvert.DeserializeObject<WafMatch[]>(jsonString).FirstOrDefault();
                resultData.Rule.Tags.Type.Should().Be(flow);
                resultData.Rule.Id.Should().Be(rule);
                resultData.RuleMatches[0].Parameters[0].Address.Should().Be(address);
            }
            else
            {
                result.ReturnCode.Should().Be(WafReturnCode.Ok);
            }
        }
    }
}
