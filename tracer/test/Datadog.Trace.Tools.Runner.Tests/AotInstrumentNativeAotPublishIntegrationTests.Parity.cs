// <copyright file="AotInstrumentNativeAotPublishIntegrationTests.Parity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Functional parity of NativeAOT with JIT (F10.2): the same application, published with NativeAOT and the Datadog.Trace.Aot
/// package, and published for JIT and run with the profiler and the tracer home (the setup of every application today),
/// sends the same spans: the same operations, resources, errors and parents, the same tags (the values of the ones that
/// don't depend on the run) and the same AppSec and IAST events.
/// </summary>
public partial class AotInstrumentNativeAotPublishIntegrationTests
{
    // ASP.NET Core and HttpClient, the manual API, AppSec (WAF) and RASP, IAST (path traversal, hardcoded secret), ADO.NET
    // (SQLite) with Dapper.AOT, and an unhandled exception.
    private const string ParityProgramFile = """
        using Dapper;
        using Datadog.Trace;
        using Microsoft.Data.Sqlite;

        [module: DapperAot]

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
        app.MapGet("/sql", async (string name) =>
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await connection.ExecuteAsync("CREATE TABLE Item (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            await connection.ExecuteAsync("INSERT INTO Item (Id, Name) VALUES (@Id, @Name)", new Item { Id = 1, Name = name });
            var found = await connection.QuerySingleAsync<Item>("SELECT Id, Name FROM Item WHERE Name = @Name", new Item { Name = name });
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Item";
            return $"{found.Name}:{await command.ExecuteScalarAsync()}";
        });
        app.MapGet("/error", string () => throw new InvalidOperationException("parity"));
        await app.StartAsync();

        using (var scope = Tracer.Instance.StartActive("manual.operation"))
        {
            scope.Span.SetTag("parity.tag", "value");
            using var client = new HttpClient();
            Console.WriteLine($"RESPONSE:{await client.GetStringAsync($"{args[0]}/hello")}");

            using var attack = new HttpRequestMessage(HttpMethod.Get, $"{args[0]}/hello?q=<script>alert(1)</script>");
            attack.Headers.UserAgent.ParseAdd("Arachni/v1.5.1");
            Console.WriteLine($"ATTACK:{(int)(await client.SendAsync(attack)).StatusCode}");

            using var lfi = await client.GetAsync($"{args[0]}/file?path=../../../../../../../../etc/passwd");
            Console.WriteLine($"LFI:{(int)lfi.StatusCode}");
            Console.WriteLine($"SQL:{await client.GetStringAsync($"{args[0]}/sql?name=one")}");
            using var error = await client.GetAsync($"{args[0]}/error");
            Console.WriteLine($"ERROR:{(int)error.StatusCode}");
        }

        Console.WriteLine($"SECRET:{"ghp_0123456789abcdefghijklmnopqrstuvwxyz".Length}");
        await Task.Delay(3000);
        await app.StopAsync();

        public class Item
        {
            public int Id { get; set; }

            public string Name { get; set; } = "";
        }
        """;

    // The manual span, 5 client and 5 server spans, 4 SQLite commands and the hardcoded secret.
    private const int ParitySpans = 16;

    // Tags whose presence or value depend on the process, the run or the batching of the payloads, not on the instrumentation
    // (the AppSec and IAST events are compared on their own; Code Origin's locations are build-time data in NativeAOT).
    private static readonly Regex VolatileTag = new(@"^(runtime-id|process_id|thread\.id|_dd\.p\.tid|_dd\.p\.dm|_dd\.tags\.process|_dd\.sdk\..*|_dd\.base_service|_dd\.svc_src|_dd\.tracer_kr|_dd\.agent_psr|_dd\.rule_psr|_dd\.limit_psr|_sampling_priority_v1|_dd\.top_level|_dd\.measured|_dd\.parent_id|_dd\.appsec\..*|_dd\.iast\..*|_dd\.code_origin\..*)$", RegexOptions.Compiled);

    // The tags whose values the instrumentation sets the same way in both modes (the others only have to be there).
    private static readonly HashSet<string> ParityValueTags = new(StringComparer.Ordinal)
    {
        "component", "span.kind", "language", "http.method", "http.status_code", "http.route", "http.url", "http.useragent",
        "aspnet_core.route", "aspnet_core.endpoint", "db.type", "db.name", "error.type", "error.msg", "appsec.event",
        "parity.tag", "_dd.p.ts",
    };

    /// <remarks>
    /// Opt-in: DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1, DD_AOT_PACKAGE_FEED, DD_AOT_MANUAL_API and DD_AOT_MONITORING_HOME (the
    /// tracer home the JIT run uses). CI runs it after packing (RunNativeAotPackageTests).
    /// </remarks>
    /// <param name="targetFramework">The target framework of the application.</param>
    [SkippableTheory]
    [InlineData("net8.0")]
    [InlineData("net10.0")]
    public async Task NativeAotSpansMatchTheJitOnes(string targetFramework)
    {
        var feed = Environment.GetEnvironmentVariable("DD_AOT_PACKAGE_FEED");
        var manualApi = Environment.GetEnvironmentVariable("DD_AOT_MANUAL_API");
        var home = Environment.GetEnvironmentVariable("DD_AOT_MONITORING_HOME");
        Skip.IfNot(Environment.GetEnvironmentVariable("DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH") == "1" && !string.IsNullOrEmpty(feed) && !string.IsNullOrEmpty(manualApi) && !string.IsNullOrEmpty(home), "Set DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1, DD_AOT_PACKAGE_FEED, DD_AOT_MANUAL_API and DD_AOT_MONITORING_HOME to compare the NativeAOT and JIT spans.");
        var package = Directory.GetFiles(feed!, "Datadog.Trace.Aot.*.nupkg").Single();
        var version = Path.GetFileNameWithoutExtension(package).Substring("Datadog.Trace.Aot.".Length);
        var runtimeIdentifier = GetRuntimeIdentifier();

        // The native loader of a tracer home, or the native tracer itself (a home built locally without the loader).
        var profiler = new[] { "Datadog.Trace.ClrProfiler.Native", "Datadog.Tracer.Native" }
                      .Select(name => Path.Combine(home!, runtimeIdentifier, name + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" : ".so")))
                      .FirstOrDefault(File.Exists);
        profiler.Should().NotBeNull($"the tracer home {home} has the native tracer of {runtimeIdentifier}");

        var workDirectory = Path.Combine(Path.GetTempPath(), "dd-aot-nativeaot-parity", Guid.NewGuid().ToString("N"));
        var project = Path.Combine(workDirectory, "PkgAot");
        Directory.CreateDirectory(project);
        try
        {
            File.WriteAllText(Path.Combine(project, "PkgAot.csproj"), ParityProjectFile(targetFramework, manualApi!, feed!, version));
            File.WriteAllText(Path.Combine(project, "Program.cs"), ParityProgramFile);
            (string Name, string Value)[] environment =
            [
                ("DD_APPSEC_ENABLED", "true"),
                ("DD_APPSEC_WAF_TIMEOUT", "10000000"),
                ("DD_IAST_ENABLED", "true"),
                ("DD_IAST_REQUEST_SAMPLING", "100"),
                ("DD_TRACE_SAMPLING_RULES", "[{\"sample_rate\":1.0,\"name\":\"manual.operation\"}]"),
            ];

            // JIT: the profiler and the tracer home.
            var jit = Path.Combine(workDirectory, "jit");
            var (jitPublishExit, jitPublishOutput) = Run("dotnet", project, [], "publish", "-c", "Release", "-r", runtimeIdentifier, "--self-contained", "-p:PublishAot=false", "-o", jit);
            jitPublishExit.Should().Be(0, jitPublishOutput);
            var jitSpans = await RunParityApplication(
                Path.Combine(jit, ExecutableName()),
                jit,
                [
                    .. environment,
                    ("CORECLR_ENABLE_PROFILING", "1"),
                    ("CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"),
                    ("CORECLR_PROFILER_PATH", profiler!),
                    ("DD_DOTNET_TRACER_HOME", home!),
                ]);

            // NativeAOT: the package.
            var nativeAot = Path.Combine(workDirectory, "nativeaot");
            var (publishExit, publishOutput) = Run("dotnet", project, [], "publish", "-c", "Release", "-r", runtimeIdentifier, "-p:DatadogAotFailOnError=true", "-p:DatadogAotCategories=tracing%2Cappsec%2Crasp%2Ciast", "-o", nativeAot);
            Skip.If(publishExit != 0 && publishOutput.Contains("Platform linker", StringComparison.OrdinalIgnoreCase), "The NativeAOT toolchain isn't available.");
            publishExit.Should().Be(0, publishOutput);
            var nativeAotSpans = await RunParityApplication(Path.Combine(nativeAot, ExecutableName()), nativeAot, environment);

            jitSpans.Should().HaveCount(ParitySpans, "the JIT run is the reference");
            var jitDescriptions = DescribeForParity(jitSpans);
            var nativeAotDescriptions = DescribeForParity(nativeAotSpans);
            foreach (var description in jitDescriptions)
            {
                _output.WriteLine($"JIT: {description}");
            }

            var onlyJit = jitDescriptions.ToList();
            var onlyNativeAot = new List<string>();
            foreach (var description in nativeAotDescriptions)
            {
                if (!onlyJit.Remove(description))
                {
                    onlyNativeAot.Add(description);
                }
            }

            var differences = onlyJit.Select(d => $"JIT only: {d}").Concat(onlyNativeAot.Select(d => $"NativeAOT only: {d}")).ToList();
            foreach (var difference in differences)
            {
                _output.WriteLine(difference);
            }

            differences.Should().BeEmpty("NativeAOT must send the spans JIT sends");
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

        static string ExecutableName() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "PkgAot.exe" : "PkgAot";
    }

    /// <summary>
    /// A line per span: operation, service, type, resource, error, the operation of its parent, its AppSec and IAST events,
    /// and its tags (the values of <see cref="ParityValueTags"/>), with the ports of the run replaced.
    /// </summary>
    private static List<string> DescribeForParity(IReadOnlyList<MockSpan> spans)
    {
        var byId = spans.GroupBy(s => s.SpanId).ToDictionary(g => g.Key, g => g.First());
        return spans.Select(Describe).OrderBy(d => d, StringComparer.Ordinal).ToList();

        string Describe(MockSpan span)
        {
            var parent = span.ParentId is null or 0 ? "root" : byId.TryGetValue(span.ParentId.Value, out var p) ? p.Name : "external";
            var tags = span.Tags.Select(t => (t.Key, Value: (string?)t.Value))
                           .Concat(span.Metrics.Select(m => (m.Key, Value: (string?)m.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))))
                           .Where(t => !VolatileTag.IsMatch(t.Key))
                           .Select(t => ParityValueTags.Contains(t.Key) ? $"{t.Key}={t.Value}" : t.Key)
                           .OrderBy(t => t, StringComparer.Ordinal);
            var line = $"{span.Name} | {span.Service} | {span.Type} | {span.Resource} | error={span.Error} | parent={parent} | appsec={AppSecEvents(span) is not null} | iast={Events(span, "_dd.iast.json", "iast") is not null} | {string.Join(", ", tags)}";
            return Regex.Replace(line, @"(?<=(127\.0\.0\.1|localhost):)\d+", "PORT");
        }
    }

    private static string ParityProjectFile(string targetFramework, string manualApi, string feed, string version)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <PublishAot>true</PublishAot>
                <InvariantGlobalization>true</InvariantGlobalization>
                <!-- Dapper.AOT generates the Dapper calls at build time (interceptors): Dapper alone emits IL at runtime. -->
                <InterceptorsNamespaces>$(InterceptorsNamespaces);Dapper.AOT</InterceptorsNamespaces>
                <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Dapper.AOT</InterceptorsPreviewNamespaces>
                <RestoreAdditionalProjectSources>{feed}</RestoreAdditionalProjectSources>
                <RestorePackagesPath>$(MSBuildThisFileDirectory)packages</RestorePackagesPath>
                <RestoreFallbackFolders>$(NuGetPackageRoot)</RestoreFallbackFolders>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Datadog.Trace.Manual">
                  <HintPath>{manualApi}</HintPath>
                  <Private>true</Private>
                </Reference>
                <PackageReference Include="Datadog.Trace.Aot" Version="{version}" />
                <PackageReference Include="Dapper" Version="2.1.89" />
                <PackageReference Include="Dapper.AOT" Version="1.1.0" />
                <!-- The native SQLite of SQLitePCLRaw 2.1.12+ needs glibc 2.34: CI runs on Debian buster (glibc 2.28). -->
                <PackageReference Include="Microsoft.Data.Sqlite.Core" Version="8.0.29" />
                <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.11" />
              </ItemGroup>
            </Project>
            """;

    private async Task<IReadOnlyList<MockSpan>> RunParityApplication(string executable, string directory, (string Name, string Value)[] environment)
    {
        using var agent = MockTracerAgent.Create(_output);
        var (exitCode, output) = Run(executable, directory, [.. environment, ("DD_TRACE_AGENT_URL", $"http://127.0.0.1:{agent.Port}")], $"http://127.0.0.1:{GetFreePort()}");
        exitCode.Should().Be(0, output);
        output.Should().Contain("RESPONSE:world").And.Contain("ATTACK:200").And.Contain("LFI:200").And.Contain("SQL:one:1").And.Contain("ERROR:500");
        // Without failing: what NativeAOT doesn't send shows in the comparison.
        return await agent.WaitForSpansAsync(ParitySpans, failOnTimeout: false);
    }
}
#endif
