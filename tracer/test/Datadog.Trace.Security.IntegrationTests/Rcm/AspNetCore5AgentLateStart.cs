// <copyright file="AspNetCore5AgentLateStart.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AgentLateStart : RcmBase
    {
        public AspNetCore5AgentLateStart(ITestOutputHelper outputHelper)
            : base(outputHelper, testName: nameof(AspNetCore5AgentLateStart))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestAgentLateStart()
        {
            // we expect exceptions in the logs, so write them outside of the usual place
            // this is so they won't be checked in a later phase of the tests
            UseTempLogFile();

            var url = "/Health/?[$slice]=value";
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var agent = RunOnSelfHosted(enableSecurity: null, startAgent: false);
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{SampleProcessName}*");
            await logEntryWatcher.WaitForLogEntry("DATADOG TRACER CONFIGURATION", logEntryWatcherTimeout);

            agent.Start();
            await logEntryWatcher.WaitForLogEntry("Set RemoteConfigurationManager enabled", logEntryWatcherTimeout);

            agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = true } }, "1") }, "ASM_FEATURES");
            await logEntryWatcher.WaitForLogEntry(AppSecEnabledMessage, logEntryWatcherTimeout);
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            var spans = await SendRequestsAsync(agent, url);

            await VerifySpans(spans, settings, true);
        }
    }
}
#endif
