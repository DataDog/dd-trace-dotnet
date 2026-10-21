// <copyright file="ReadyToRunInlinerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// The sample's ReadyToRun images inline each Target into its Caller, so instrumenting a Target also needs its
    /// precompiled Caller to be ReJITed.
    /// Expected behavior: the first call to Caller creates a Target span.
    /// Wrong behavior: Caller runs its precompiled code, which inlined the original Target, and no span is created.
    /// </summary>
    public class ReadyToRunInlinerTests : TestHelper
    {
        public ReadyToRunInlinerTests(ITestOutputHelper output)
            : base("ReadyToRunInliner", "test/test-applications/regression", output, prependSamplesToAppName: false)
        {
            // The native log only reports the precompiled callers it found in debug mode.
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "1");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public Task MethodInstrumentedAfterModuleLoad()
            => RunTest("ReadyToRunInliner", "ReadyToRunInliner.InstrumentedAfterModuleLoad");

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public Task MethodInstrumentedAtModuleLoad()
            => RunTest("ReadyToRunInliner.Lib", "ReadyToRunInliner.Lib.InstrumentedAtModuleLoad");

        private async Task RunTest(string assemblyName, string typeName, [CallerMemberName] string testName = null)
        {
#if NET9_0_OR_GREATER
            // Since .NET 9, the apphost of a published x86 app doesn't find the x86 runtime that DOTNET_EXE_32 points to.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
             && Environment.GetEnvironmentVariable("DOTNET_EXE_32") is { } dotnet32BitExe)
            {
                SetEnvironmentVariable("DOTNET_ROOT(x86)", Path.GetDirectoryName(dotnet32BitExe));
            }
#endif
#if !NET6_0_OR_GREATER
            // osx-arm64 doesn't work with Ready2Run
            SkipOn.PlatformAndArchitecture(SkipOn.PlatformValue.MacOs, SkipOn.ArchitectureValue.ARM64);
#endif
            var logDirectory = Path.Combine(LogDirectory, testName);
            Directory.CreateDirectory(logDirectory);
            SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDirectory);
            SetEnvironmentVariable(ConfigurationKeys.TraceMethods, $"{typeName}[Target]");

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, usePublishWithRID: true);

            var spans = await agent.WaitForSpansAsync(1);
            spans.Should().ContainSingle(span => span.Resource == $"{typeName.Substring(typeName.LastIndexOf('.') + 1)}.Target");
            PrecompiledInliners.ShouldHaveBeenFound(logDirectory, process.Process.Id, assemblyName);
        }
    }
}
#endif
