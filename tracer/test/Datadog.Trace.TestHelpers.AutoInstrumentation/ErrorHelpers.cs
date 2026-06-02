// <copyright file="ErrorHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.AutoInstrumentation;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public static class ErrorHelpers
{
    private const string Runtime127957IssueTag = "runtime_issue:127957";
    private const string RuntimeMetadataRaceRetryMetric = "dd_trace_dotnet.ci.tests.retried_due_to_runtime_metadata_race";
    private const string RuntimeMetadataRacePersistentMetric = "dd_trace_dotnet.ci.tests.persistent_runtime_metadata_race";

    /// <summary>
    /// Dispatch helper for known transient runtime crashes. Returns true when the caller should
    /// retry, false when no known fingerprint matched. Throws when the fingerprint persisted
    /// across the retry budget (test must fail loudly — exit code alone can't be trusted to
    /// surface the failure, e.g. Windows FailFast paths sometimes report exit 0).
    /// </summary>
    public static async Task<bool> HandleRuntimeSkippableErrorsAsync(
        int attempt, int maxAttempts, int exitCode, string stderr, TestHelper helper, Action<string> writeOutput)
    {
        if (!IsRuntime127957Race(exitCode, stderr))
        {
            return false;
        }

        if (attempt < maxAttempts)
        {
            writeOutput($"Detected dotnet/runtime#127957 race on attempt {attempt}/{maxAttempts}, retrying.");
            await helper.SendCIMetricAsync(RuntimeMetadataRaceRetryMetric, Runtime127957IssueTag);
            return true;
        }

        await helper.SendCIMetricAsync(RuntimeMetadataRacePersistentMetric, Runtime127957IssueTag);
        throw new Exception($"dotnet/runtime#127957 fingerprint persisted across {maxAttempts} attempts; failing the test.");
    }

    public static bool IsRuntime127957Race(int exitCode, string standardError)
    {
        if (standardError is null)
        {
            return false;
        }

        // Linux/SIGABRT — TypeLoadException with the "Undefined resource string ID" fallback.
        if (exitCode == 134
            && standardError.Contains("System.TypeLoadException")
            && standardError.Contains("Undefined resource string ID"))
        {
            return true;
        }

        // Linux/SIGABRT — MissingMethodException for ConcurrentDictionary.TryGetValue
        // (the canonical example from the runtime issue).
        if (exitCode == 134
            && standardError.Contains("System.MissingMethodException: Method not found:")
            && standardError.Contains("ConcurrentDictionary")
            && standardError.Contains("TryGetValue"))
        {
            return true;
        }

        // Windows — CLR FailFast with HRESULT 0x80131506 (COR_E_EXECUTIONENGINE) on the
        // threadpool gate thread. Exit code is unreliable here (we've seen 0 in CI even though
        // the runtime died), so we don't gate on it.
        if (standardError.Contains("Fatal error. Internal CLR error. (0x80131506)")
            && standardError.Contains("PortableThreadPool")
            && standardError.Contains("GateThread"))
        {
            return true;
        }

        // Linux/SIGABRT — BadImageFormatException with the "metadata is corrupt" message thrown
        // while building host configuration at startup (ConfigurationBuilder.Build()). We deliberately
        // anchor on that frame rather than matching the message anywhere: the retry only exists to
        // paper over this one known-benign startup manifestation of the race. A "metadata is corrupt"
        // crash on any other stack could be a genuine tracer-induced corruption and must fail loudly
        // so it gets investigated (and a new fingerprint added deliberately).
        if (exitCode == 134
            && standardError.Contains("System.BadImageFormatException")
            && standardError.Contains("The metadata is corrupt")
            && standardError.Contains("Microsoft.Extensions.Configuration.ConfigurationBuilder.Build"))
        {
            return true;
        }

        return false;
    }

    public static void CheckForKnownSkipConditions(ITestOutputHelper output, int exitCode, string standardError, EnvironmentHelper environmentHelper)
    {
#if NETCOREAPP2_1
        if (exitCode == 139)
        {
            // Segmentation faults are expected on .NET Core because of a bug in the runtime: https://github.com/dotnet/runtime/issues/11885
            throw new SkipException("Segmentation fault on .NET Core 2.1");
        }
#endif
        if (exitCode == 134
         && standardError?.Contains("System.Threading.AbandonedMutexException: The wait completed due to an abandoned mutex") == true
         && standardError?.Contains("Coverlet.Core.Instrumentation.Tracker") == true)
        {
            // Coverlet occasionally throws AbandonedMutexException during clean up
            throw new SkipException("Coverlet threw AbandonedMutexException during cleanup");
        }

#if NETCOREAPP2_1
        if (exitCode == 134 && EnvironmentTools.IsLinux())
        {
            // We see SIGABRT relatively frequently on .NET Core 2.1 on Linux, but probably not worth investigating further
            throw new SkipException("SIGABRT on .NET Core 2.1");
        }
#endif

        if (exitCode == 13)
        {
            // This is an "expected" issue, e.g. timeout talking to a required service
            // strictly a failure, but skipping to avoid flake in CI etc
            SendMetric(output, "dd_trace_dotnet.ci.tests.skipped_due_to_flake", environmentHelper).ConfigureAwait(false).GetAwaiter().GetResult();
            throw new SkipException("Exit code (13) - anticipated flake");
        }

        SkipKnownCrashes(environmentHelper.PathToCrashReport, output).Wait();
    }

    public static async Task SendMetric(ITestOutputHelper outputHelper, string metricName, EnvironmentHelper environmentHelper, params string[] extraTags)
    {
        const int maxTestFullNameLength = 200;

        var envKey = Environment.GetEnvironmentVariable("DD_LOGGER_DD_API_KEY");
        if (string.IsNullOrEmpty(envKey))
        {
            // We're probably not in CI
            return;
        }

        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("DD-API-KEY", envKey);

        var type = outputHelper.GetType();
        var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        var test = (ITest)testMember?.GetValue(outputHelper);
        var displayName = test?.TestCase.DisplayName ?? string.Empty;
        var testFullName = (displayName.StartsWith(type.FullName) ?
            displayName :
            $"{type.FullName}.{displayName}").Trim();

        testFullName = testFullName.Substring(0, Math.Min(testFullName.Length, maxTestFullNameLength));

        // In addition to logging, send a metric that will help us get more information through tags
        var srcBranch = Environment.GetEnvironmentVariable("DD_LOGGER_BUILD_SOURCEBRANCH");

        var tags = $$"""
                         "os.platform:{{SanitizeTagValue(FrameworkDescription.Instance.OSPlatform)}}",
                         "os.architecture:{{SanitizeTagValue(EnvironmentTools.GetPlatform())}}",
                         "target.framework:{{environmentHelper.GetTargetFramework()}}",
                         "test.name:{{SanitizeTagValue(testFullName)}}",
                         "git.branch:{{SanitizeTagValue(srcBranch)}}"
                     """;

        if (extraTags is { Length: > 0 })
        {
            tags += ", " + string.Join(", ", extraTags.Select(t => $"\"{t}\""));
        }

        var payload = $$"""
                            {
                                "series": [{
                                    "metric": "{{metricName}}",
                                    "type": 1,
                                    "points": [{
                                        "timestamp": {{((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()}},
                                        "value": 1
                                        }],
                                    "tags": [
                                        {{tags}}
                                    ]
                                }]
                            }
                        """;

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync("https://api.datadoghq.com/api/v2/series", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                outputHelper.WriteLine($"Failed to submit metric {metricName}. Response was: Code: {response.StatusCode}. Response: {responseContent}. Payload sent was: \"{payload}\"");
            }
        }
        catch (Exception ex)
        {
            outputHelper.WriteLine($"Failed to submit metric {metricName}. Exception: {ex.ToString()} Payload: \"{payload}\"");
        }
    }

    private static string SanitizeTagValue(string tag)
    {
        SpanTagHelper.TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag);
        return normalizedTag;
    }

    private static async Task SkipKnownCrashes(string pathToCrashReport, ITestOutputHelper output)
    {
        if (pathToCrashReport == null || !File.Exists(pathToCrashReport))
        {
            return;
        }

        try
        {
            using var crashReport = new CrashReport(pathToCrashReport, output);
            var stacktrace = await crashReport.ResolveCrashStackTrace();

            output.WriteLine("Crash stacktrace:");

            foreach (var frame in stacktrace)
            {
                output.WriteLine(frame);
            }

            // TODO: Add logic to throw SkipException on known crashes
        }
        catch (Exception ex) when (ex is not SkipException)
        {
            output.WriteLine($"Unexpected exception while analyzing the crash report: {ex}");
        }
    }
}
