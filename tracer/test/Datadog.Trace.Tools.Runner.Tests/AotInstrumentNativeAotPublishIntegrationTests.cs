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
using Datadog.Trace.AppSec.Rasp;
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

    private const string ManualApiProgramFile = """
        using Datadog.Trace;

        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls(args[0]);
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        app.MapGet("/file", (string path) =>
        {
            try
            {
                return File.ReadAllText(path).Length.ToString();
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        });
        await app.StartAsync();

        using (var scope = Tracer.Instance.StartActive("manual.operation"))
        {
            using var client = new HttpClient();
            Console.WriteLine($"RESPONSE:{await client.GetStringAsync($"{args[0]}/hello")}");

            // A request a WAF rule flags (security scanner user agent).
            using var attack = new HttpRequestMessage(HttpMethod.Get, $"{args[0]}/hello?q=<script>alert(1)</script>");
            attack.Headers.UserAgent.ParseAdd("Arachni/v1.5.1");
            Console.WriteLine($"ATTACK:{(int)(await client.SendAsync(attack)).StatusCode}");

            // A file access with user input (RASP local file inclusion, through the File call site aspect).
            using var lfi = await client.GetAsync($"{args[0]}/file?path=../../../../../../../../etc/passwd");
            Console.WriteLine($"LFI:{(int)lfi.StatusCode}");
            Console.WriteLine($"MANUAL_TRACE_ID:{scope.Span.TraceId}");
        }

        // A hardcoded secret (IAST): a GitHub personal access token. The analysis polls every 2 s.
        Console.WriteLine($"SECRET:{"ghp_0123456789abcdefghijklmnopqrstuvwxyz".Length}");
        await Task.Delay(3000);

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

        var runtimeIdentifier = GetRuntimeIdentifier();
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

    /// <summary>
    /// The product setup: the application references the manual API (what the Datadog.Trace package brings) and the
    /// Datadog.Trace.Aot package, and is published as it is. The package brings the tools, the full Datadog.Trace.dll ILC
    /// compiles, the duck typing mappings of Datadog.Trace (the manual API's and AppSec's among them; nothing is recorded)
    /// and libddwaf: AppSec runs the WAF on the requests, and RASP on the file access its call site instrumentation reports.
    /// </summary>
    /// <remarks>
    /// Opt-in: DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1, DD_AOT_PACKAGE_FEED (folder with the Datadog.Trace.Aot package) and
    /// DD_AOT_MANUAL_API (Datadog.Trace.Manual.dll).
    /// </remarks>
    [SkippableFact]
    public async Task ManualApiApplicationWithThePackage()
    {
        var feed = Environment.GetEnvironmentVariable("DD_AOT_PACKAGE_FEED");
        var manualApi = Environment.GetEnvironmentVariable("DD_AOT_MANUAL_API");
        Skip.IfNot(Environment.GetEnvironmentVariable("DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH") == "1" && !string.IsNullOrEmpty(feed) && !string.IsNullOrEmpty(manualApi), "Set DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1, DD_AOT_PACKAGE_FEED and DD_AOT_MANUAL_API to publish the NativeAOT sample with the package.");
        var package = Directory.GetFiles(feed!, "Datadog.Trace.Aot.*.nupkg").Single();
        var version = Path.GetFileNameWithoutExtension(package).Substring("Datadog.Trace.Aot.".Length);

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-nativeaot-package", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(workDirectory, "PkgAot");
        Directory.CreateDirectory(project);
        try
        {
            File.WriteAllText(Path.Combine(project, "PkgAot.csproj"), PackageProjectFile(manualApi!, feed!, version));
            File.WriteAllText(Path.Combine(project, "Program.cs"), ManualApiProgramFile);
            var published = Path.Combine(workDirectory, "nativeaot");
            // IAST is opt-in at build time.
            var (publishExit, publishOutput) = Run("dotnet", project, [], "publish", "-c", "Release", "-r", GetRuntimeIdentifier(), "-p:DatadogAotFailOnError=true", "-p:DatadogAotCategories=tracing%2Cappsec%2Crasp%2Ciast", "-o", published);
            Skip.If(publishExit != 0 && publishOutput.Contains("Platform linker", StringComparison.OrdinalIgnoreCase), "The NativeAOT toolchain isn't available.");
            publishExit.Should().Be(0, publishOutput);
            publishOutput.Should().Contain("Datadog NativeAOT instrumentation:").And.Contain("Datadog.Trace.Manual");

            using var agent = MockTracerAgent.Create(_output);
            var logs = Path.Combine(workDirectory, "logs");
            var (exitCode, output) = Run(
                Path.Combine(published, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "PkgAot.exe" : "PkgAot"),
                published,
                // A WAF timeout (100 ms by default) on a loaded machine would leave the requests without events.
                [
                    ("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{agent.Port}"),
                    ("DD_APPSEC_ENABLED", "true"),
                    ("DD_APPSEC_WAF_TIMEOUT", "10000000"),
                    ("DD_IAST_ENABLED", "true"),
                    ("DD_IAST_REQUEST_SAMPLING", "100"),
                    ("DD_TRACE_SAMPLING_RULES", "[{\"sample_rate\":1.0,\"name\":\"manual.operation\"}]"),
                    ("DD_TRACE_DEBUG", "1"),
                    ("DD_TRACE_LOG_DIRECTORY", logs),
                ],
                $"http://127.0.0.1:{GetFreePort()}");
            exitCode.Should().Be(0, output);
            output.Should().Contain("RESPONSE:world").And.Contain("ATTACK:200").And.Contain("LFI:200").And.Contain("DYNAMIC_CODE:False").And.NotContain("MANUAL_TRACE_ID:0");

            var spans = await agent.WaitForSpansAsync(8);
            var log = string.Concat(Directory.GetFiles(logs, "dotnet-tracer-managed-*").Select(File.ReadAllText));
            foreach (var line in log.Split('\n').Where(l => l.Contains("[ERR]") || l.Contains("[WRN]") || l.Contains("DDAS-0011") || l.Contains("RASP")))
            {
                _output.WriteLine(line);
            }

            var manual = spans.Should().ContainSingle(s => s.Name == "manual.operation").Which;
            var clients = spans.Where(s => s.Name == "http.request").ToList();
            var servers = spans.Where(s => s.Name == "aspnet_core.request").ToList();
            clients.Should().HaveCount(3).And.OnlyContain(s => s.ParentId == manual.SpanId, "the manual API scope is the active span");
            servers.Should().HaveCount(3).And.OnlyContain(s => s.TraceId == manual.TraceId && clients.Any(c => c.SpanId == s.ParentId));

            servers.Should().OnlyContain(s => s.Metrics.ContainsKey("_dd.appsec.enabled"));
            var attack = servers.Should().ContainSingle(s => s.GetTag("http.useragent") == "Arachni/v1.5.1").Which;
            attack.GetTag("appsec.event").Should().Be("true");
            AppSecEvents(attack).Should().Contain("ua0-600-12x");

            var lfi = servers.Should().ContainSingle(s => s.Resource == "GET /file").Which;
            AppSecEvents(lfi).Should().Contain("rasp-930-100", "the File call site aspect reports the access to RASP");
            lfi.Metrics.Should().ContainKey("_dd.appsec.rasp.rule.eval");

            // IAST reports the tainted path (JSON models kept for Newtonsoft), and the sampling rules are read.
            Events(lfi, "_dd.iast.json", "iast").Should().Contain("PATH_TRAVERSAL").And.Contain("http.request.parameter");
            manual.Metrics.Should().ContainKey("_dd.rule_psr");

            // The string literals the build collected for the hardcoded secrets analysis.
            var secret = spans.Should().ContainSingle(s => s.Name == "hardcoded_secret").Which;
            Events(secret, "_dd.iast.json", "iast").Should().Contain("HARDCODED_SECRET").And.Contain("github-pat");

            // libdatadog comes with the package too (hands-off configuration, tracer metadata).
            log.Should().Contain("Successfully stored tracer metadata with LibDatadog").And.NotContain("LibDatadogUnavailable");
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

    private static string? AppSecEvents(MockSpan span) => Events(span, "_dd.appsec.json", "appsec");

    /// <summary>
    /// The AppSec or IAST events of a span: in a tag, or in the meta struct once the tracer knows the agent supports it.
    /// </summary>
    private static string? Events(MockSpan span, string tag, string metaStructKey)
        => span.GetTag(tag)
        ?? (span.MetaStruct?.TryGetValue(metaStructKey, out var events) == true ? Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(MetaStructHelper.ByteArrayToObject(events)) : null);

    private static string GetRuntimeIdentifier()
        => RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl", StringComparison.Ordinal)
               ? RuntimeInformation.RuntimeIdentifier
               : $"{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux")}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";

    private static string PackageProjectFile(string manualApi, string feed, string version)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <PublishAot>true</PublishAot>
                <InvariantGlobalization>true</InvariantGlobalization>
                <RestoreAdditionalProjectSources>{feed}</RestoreAdditionalProjectSources>
                <!-- The package goes to its own folder (a rebuilt package keeps its version), the rest comes from the cache. -->
                <RestorePackagesPath>$(MSBuildThisFileDirectory)packages</RestorePackagesPath>
                <RestoreFallbackFolders>$(NuGetPackageRoot)</RestoreFallbackFolders>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Datadog.Trace.Manual">
                  <HintPath>{manualApi}</HintPath>
                  <Private>true</Private>
                </Reference>
                <PackageReference Include="Datadog.Trace.Aot" Version="{version}" />
              </ItemGroup>
            </Project>
            """;

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
