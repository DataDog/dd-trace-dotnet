// <copyright file="DebugLogReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging.TracerFlare;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class DebugLogReaderTests(ITestOutputHelper output)
{
    [Fact]
    public void TryToCreateSentinelFile_CreateSentinelInExistingDirectory_Succeeds()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id = Guid.NewGuid().ToString();
        var result = DebugLogReader.TryToCreateSentinelFile(directory, id);
        result.Should().BeTrue();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSecondSentinelInExistingDirectory_Succeeds()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id1 = Guid.NewGuid().ToString();
        var id2 = Guid.NewGuid().ToString();

        var result1 = DebugLogReader.TryToCreateSentinelFile(directory, id1);
        var result2 = DebugLogReader.TryToCreateSentinelFile(directory, id2);

        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSameSentinel_Fails()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);

        var id = Guid.NewGuid().ToString();

        var result1 = DebugLogReader.TryToCreateSentinelFile(directory, id);
        var result2 = DebugLogReader.TryToCreateSentinelFile(directory, id);

        result1.Should().BeTrue();
        result2.Should().BeFalse();
    }

    [Fact]
    public void TryToCreateSentinelFile_CreateSentinelInNonExistingDirectory_Fails()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var id = Guid.NewGuid().ToString();

        var result = DebugLogReader.TryToCreateSentinelFile(directory, id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WriteDebugLogArchiveToStream_WithMultipleFiles_CreatesArchive()
    {
        // create files
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        output.WriteLine("Creating log directory " + directory);
        Directory.CreateDirectory(directory);
        var files = new[]
        {
            "dotnet-tracer-managed-dotnet-20231213.log",
            "DD-DotNet-Profiler-Native-dotnet-16272.log",
            "dotnet-tracer-native-dotnet-16272.log",
            "dotnet-tracer-native-dotnet-28504.log",
        };
        foreach (var file in files)
        {
            await CreateLogFile(directory, file);
        }

        // These are chonky, going to do a lot of resizing, but meh
        using var ms = new MemoryStream();

        // zip, scrub, write to stream
        await DebugLogReader.WriteDebugLogArchiveToStream(ms, directory);

        ms.Seek(offset: 0, SeekOrigin.Begin);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        archive.Entries
               .Should()
               .Contain(x => files.Contains(x.Name));

        var extractTo = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        output.WriteLine("Extracting log directory to " + extractTo);

        archive.ExtractToDirectory(extractTo);
        // confirm all the files are identical
        foreach (var filename in files)
        {
            var originalPath = Path.Combine(directory, filename);
            var extractedPath = Path.Combine(extractTo, filename);

            File.Exists(extractedPath).Should().BeTrue();
            var originalContents = File.ReadAllText(originalPath);
            var extractedContents = File.ReadAllText(extractedPath);

            extractedContents.Should().Be(originalContents);
        }

        return;

        static async Task CreateLogFile(string directory, string filename)
        {
            // These lines don't include anything that needs scrubbing so can be round-tripped
            const string logLines =
                """
                2023-12-13 10:04:16.335 +00:00 [WRN] Error discovering available agent services  { MachineName: ".", Process: "[30856 dotnet]", AppDomain: "[1 ReSharperTestRunner]", AssemblyLoadContext: "\"Default\" System.Runtime.Loader.DefaultAssemblyLoadContext #0", TracerVersion: "2.44.0.0" }
                2023-12-13 18:11:55.360 +00:00 [INF] Sentinel file for config 1a6e1e13-c487-4900-bd63-65dd5c4dde0f found in C:\Users\andrew.lock\AppData\Local\Temp\99e04cab-b68c-4d1f-9586-b2c02f0f03a9  { MachineName: ".", Process: "[50524 dotnet]", AppDomain: "[1 ReSharperTestRunner]", AssemblyLoadContext: "\"Default\" System.Runtime.Loader.DefaultAssemblyLoadContext #0", TracerVersion: "2.44.0.0" }
                2023-12-06 11:29:18.051 +00:00 [INF] DATADOG TRACER CONFIGURATION - {"date":"2023-12-06T11:29:18.047752+00:00","os_name":"Windows","os_version":"Microsoft Windows NT 10.0.19045.0","version":"2.43.0.0","native_tracer_version":"2.43.0","platform":"x64","lang":".NET","lang_version":"8.0.0","env":"andrew","enabled":true,"service":"BlazorWebApp","agent_url":"http://127.0.0.1:8126/","agent_transport":"Default","debug":true,"health_checks_enabled":false,"analytics_enabled":false,"sample_rate":null,"sampling_rules":null,"tags":[],"log_injection_enabled":false,"runtime_metrics_enabled":false,"disabled_integrations":["OpenTelemetry"],"routetemplate_resourcenames_enabled":true,"routetemplate_expansion_enabled":false,"querystring_reporting_enabled":true,"obfuscation_querystring_regex_timout":200.0,"obfuscation_querystring_size":5000,"partialflush_enabled":false,"partialflush_minspans":500,"runtime_id":"b3eef1ac-546a-408a-994c-fdc69456c36c","agent_reachable":true,"agent_error":"","appsec_enabled":false,"appsec_trace_rate_limit":100,"appsec_rules_file_path":"(default)","appsec_libddwaf_version":"(none)","iast_enabled":false,"iast_deduplication_enabled":true,"iast_weak_hash_algorithms":"HMACMD5,MD5,HMACSHA1,SHA1","iast_weak_cipher_algorithms":"DES,TRIPLEDES,RC2","direct_logs_submission_enabled_integrations":[],"direct_logs_submission_enabled":false,"direct_logs_submission_error":"","exporter_settings_warning":["No transport configuration found, using default values"],"dd_trace_methods":"","activity_listener_enabled":false,"profiler_enabled":false,"code_hotspots_enabled":false,"wcf_obfuscation_enabled":true,"data_streams_enabled":false,"span_sampling_rules":null,"stats_computation_enabled":false,"dbm_propagation_mode":"Disabled","header_tags":[],"service_mapping":[]}  { MachineName: ".", Process: "[35160 dotnet]", AppDomain: "[1 BlazorWebApp]", AssemblyLoadContext: "\"\" Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext #1", TracerVersion: "2.43.0.0" }
                """;

            using var writer = File.CreateText(Path.Combine(directory, filename));
            foreach (string line in Enumerable.Repeat(logLines, 5000))
            {
                await writer.WriteLineAsync(line);
            }

            await writer.FlushAsync();
        }
    }
}
