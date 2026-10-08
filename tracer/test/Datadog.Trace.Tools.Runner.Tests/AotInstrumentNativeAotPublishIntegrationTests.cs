// <copyright file="AotInstrumentNativeAotPublishIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.Runner.Aot;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Publishes an ASP.NET Core application with NativeAOT and the Datadog.Trace.Aot targets, and checks its spans reach the
/// agent: the server span (ASP.NET Core diagnostic observer) is a child of the HttpClient span (CallTarget in
/// System.Net.Http), so the instrumentation, the duck typing proxies and the context propagation work without the
/// native tracer nor dynamic code.
/// </summary>
/// <remarks>
/// Opt-in (two publishes, one of them NativeAOT): set DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1 and DD_AOT_NATIVE_TRACER
/// (Datadog.Tracer.Native library for this host).
/// </remarks>
public class AotInstrumentNativeAotPublishIntegrationTests
{
    private const string ProgramFile = """
        // The recording run (JIT, with the profiler attached but without the managed loader) initializes the instrumentation.
        if (Environment.GetEnvironmentVariable("ASPAOT_INITIALIZE") == "1")
        {
            Datadog.Trace.ClrProfiler.Instrumentation.Initialize();
        }

        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls(args[0]);
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();

        using (var client = new HttpClient())
        {
            Console.WriteLine($"RESPONSE:{await client.GetStringAsync($"{args[0]}/hello")}");
        }

        await app.StopAsync();
        Console.WriteLine($"DYNAMIC_CODE:{System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
        """;

    private readonly ITestOutputHelper _output;

    public AotInstrumentNativeAotPublishIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public async Task AspNetCoreSpansReachTheAgent()
    {
        var nativeTracer = Environment.GetEnvironmentVariable("DD_AOT_NATIVE_TRACER");
        Skip.IfNot(Environment.GetEnvironmentVariable("DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH") == "1" && !string.IsNullOrEmpty(nativeTracer), "Set DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1 and DD_AOT_NATIVE_TRACER to publish the NativeAOT sample.");

        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl", StringComparison.Ordinal) ? RuntimeInformation.RuntimeIdentifier : $"{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux")}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
        var runner = typeof(AotInstrumentProcessor).Assembly.Location;
        var targets = Path.Combine(Path.GetDirectoryName(runner)!, "Datadog.Trace.Aot.targets");
        File.Exists(targets).Should().BeTrue("the targets are copied next to the runner");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-nativeaot-publish", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(workDirectory, "AspAot");
        Directory.CreateDirectory(project);
        try
        {
            var datadogTrace = typeof(Tracer).Assembly.Location;
            File.WriteAllText(Path.Combine(project, "AspAot.csproj"), ProjectFile(datadogTrace, targets));
            File.WriteAllText(Path.Combine(project, "Program.cs"), ProgramFile);
            var url = $"http://127.0.0.1:{GetFreePort()}";

            // 1. Record the duck typing mappings with a JIT build (PublishAot=false: dynamic code is supported) and the profiler.
            var recording = Path.Combine(workDirectory, "recording");
            var (recordingPublish, recordingPublishOutput) = Run("dotnet", project, [], "publish", "-c", "Release", "-r", runtimeIdentifier, "--self-contained", "-p:PublishAot=false", "-o", recording);
            recordingPublish.Should().Be(0, recordingPublishOutput);

            var home = Path.Combine(workDirectory, "home", "net6.0");
            Directory.CreateDirectory(home);
            File.Copy(datadogTrace, Path.Combine(home, "Datadog.Trace.dll"));
            var map = Path.Combine(workDirectory, "ducktype-map.json");
            var (recordingExit, recordingOutput) = Run(
                Path.Combine(recording, "AspAot"),
                recording,
                [
                    ("ASPAOT_INITIALIZE", "1"),
                    ("CORECLR_ENABLE_PROFILING", "1"),
                    ("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"),
                    ("CORECLR_PROFILER_PATH", nativeTracer!),
                    ("DD_DOTNET_TRACER_HOME", Path.GetDirectoryName(home)!),
                    ("DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH", map),
                ],
                url);
            recordingExit.Should().Be(0, recordingOutput);
            recordingOutput.Should().Contain("RESPONSE:world");
            File.Exists(map).Should().BeTrue(recordingOutput);

            // 2. Publish with NativeAOT: the targets instrument the application and the assemblies ILC compiles.
            var published = Path.Combine(workDirectory, "nativeaot");
            var (publishExit, publishOutput) = Run(
                "dotnet",
                project,
                [],
                "publish",
                "-c",
                "Release",
                "-r",
                runtimeIdentifier,
                $"-p:DatadogAotRunnerPath={runner}",
                $"-p:DatadogAotNativeTracerPath={nativeTracer}",
                $"-p:DatadogAotDuckTypeMaps={map}",
                "-p:DatadogAotFailOnError=true",
                "-o",
                published);
            Skip.If(publishExit != 0 && publishOutput.Contains("Platform linker", StringComparison.OrdinalIgnoreCase), "The NativeAOT toolchain isn't available.");
            publishExit.Should().Be(0, publishOutput);
            publishOutput.Should().Contain("Datadog NativeAOT instrumentation:");

            // 3. Run it: the spans reach the agent.
            using var agent = MockTracerAgent.Create(_output);
            var (exitCode, output) = Run(
                Path.Combine(published, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "AspAot.exe" : "AspAot"),
                published,
                [("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{agent.Port}")],
                url);
            exitCode.Should().Be(0, output);
            output.Should().Contain("RESPONSE:world").And.Contain("DYNAMIC_CODE:False");

            var spans = await agent.WaitForSpansAsync(2);
            var server = spans.Should().ContainSingle(s => s.Name == "aspnet_core.request").Which;
            var client = spans.Should().ContainSingle(s => s.Name == "http.request").Which;
            server.Resource.Should().Be("GET /hello");
            server.TraceId.Should().Be(client.TraceId);
            server.ParentId.Should().Be(client.SpanId, "the client propagates its context to the server");
        }
        finally
        {
            try
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort.
            }
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string ProjectFile(string datadogTrace, string targets)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <PublishAot Condition="'$(PublishAot)' == ''">true</PublishAot>
                <InvariantGlobalization>true</InvariantGlobalization>
                <NoWarn>$(NoWarn);IL2026;IL2104;IL3053;IL2072;IL2075;IL3050;IL3000;IL3002</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Datadog.Trace">
                  <HintPath>{datadogTrace}</HintPath>
                  <Private>true</Private>
                </Reference>
              </ItemGroup>
              <Import Project="{targets}" />
            </Project>
            """;

    private (int ExitCode, string Output) Run(string fileName, string workingDirectory, (string Name, string Value)[] environment, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Remove("CORECLR_ENABLE_PROFILING");
        startInfo.Environment["DD_TELEMETRY_ENABLED"] = "0";
        startInfo.Environment["DD_REMOTE_CONFIGURATION_ENABLED"] = "0";
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit(900_000).Should().BeTrue($"{fileName} {string.Join(" ", arguments)} timed out");
        var output = standardOutput.Result + Environment.NewLine + standardError.Result;
        _output.WriteLine($"{fileName} {string.Join(" ", arguments)} -> {process.ExitCode}");
        if (process.ExitCode != 0)
        {
            _output.WriteLine(output);
        }

        return (process.ExitCode, output);
    }
}
#endif
