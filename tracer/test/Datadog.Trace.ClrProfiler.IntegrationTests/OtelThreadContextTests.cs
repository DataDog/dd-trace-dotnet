// <copyright file="OtelThreadContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[Collection(nameof(DynamicConfigurationTests))]
public class OtelThreadContextTests : TestHelper
{
    public OtelThreadContextTests(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    [SkippableFact]
    [Trait("RunOnLinux", "True")]
    public async Task PublishesScopesThroughLibdatadogTls()
    {
        Skip.IfNot(EnvironmentTools.IsLinux(), "The OpenTelemetry thread context protocol is Linux-only.");
        Skip.IfNot(Environment.Is64BitProcess, "The OpenTelemetry thread context protocol requires a 64-bit process.");
        Skip.If(EnvironmentHelper.GetTargetFramework() == "netcoreapp2.1", "The test reader requires NativeLibrary, which was added in .NET Core 3.0.");

        SetEnvironmentVariable("DD_TRACE_OTEL_CTX_ENABLED", "true");
        SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED", "true");

        AssertElfThreadLocalSymbol();

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent, arguments: "otel-thread-context");

        ExitCodeException.ThrowIfNonZero(processResult.ExitCode, processResult.StandardError);
        processResult.StandardOutput.Should().ContainAll(
            "OTEL_THREAD_CONTEXT_ROOT_OK",
            "OTEL_THREAD_CONTEXT_NESTED_OK",
            "OTEL_THREAD_CONTEXT_PARENT-RESTORED_OK",
            "OTEL_THREAD_CONTEXT_ASYNC-TRANSITION_OK",
            "OTEL_THREAD_CONTEXT_ASYNC-RESTORED_OK",
            "OTEL_THREAD_CONTEXT_DETACHED_OK",
            "OTEL_THREAD_CONTEXT_TEST_OK");
    }

    private void AssertElfThreadLocalSymbol()
    {
        var nativeLoaderPath = EnvironmentHelper.GetNativeLoaderPath();
        var libdatadogPath = Path.Combine(
            Path.GetDirectoryName(nativeLoaderPath) ?? throw new InvalidOperationException($"Could not determine the directory for '{nativeLoaderPath}'."),
            "libdatadog_profiling.so");
        File.Exists(libdatadogPath).Should().BeTrue($"libdatadog should be packaged beside the native tracer at '{libdatadogPath}'");

        var symbols = RunReadElf($"--dyn-syms --wide \"{libdatadogPath}\"");
        var tlsSymbol = symbols
                       .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                       .FirstOrDefault(line => line.Contains("otel_thread_ctx_v1"));
        tlsSymbol.Should().NotBeNull();
        tlsSymbol.Should().Contain("TLS").And.Contain("GLOBAL");

        var relocations = RunReadElf($"--relocs --wide \"{libdatadogPath}\"");
        var tlsDescRelocation = relocations
                               .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault(line => line.Contains("otel_thread_ctx_v1") && line.Contains("TLSDESC"));
        tlsDescRelocation.Should().NotBeNull();
    }

    private string RunReadElf(string arguments)
    {
        var startInfo = new ProcessStartInfo("readelf", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start readelf.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.Should().Be(0, $"readelf failed: {standardError}");
        Output.WriteLine(standardOutput);
        return standardOutput;
    }
}
