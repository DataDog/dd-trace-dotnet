using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Nuke.Common.IO;
using Logger = Serilog.Log;
#nullable enable

partial class Build
{
    const string SnapshotExplorationTestFolderName = "SnapshotExplorationTestProbes";
    const string SnapshotExplorationTestProbesFileName = "SnapshotExplorationTestProbes.json";
    const string SnapshotExplorationTestReportFolderName = "SnapshotExplorationTestReport";
    const string SnapshotExplorationRootPathKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_ROOT_PATH";
    const char SpecialSeparator = '#';

    readonly List<string> IgnoredNamespaces = new()
    {
        "nunit",
        "xunit",
        "testcentric",
        "system",
        "microsoft",
        "nuget",
        "newtonsoft",
        "mono.",
        "mscorlib",
        "netstandard",
        "datadog",
        "_build",
        "testhost"
    };

    static string GetSnapshotExplorationRootPath(string testRootPath, TargetFramework framework)
        => Path.Combine(testRootPath, SnapshotExplorationTestFolderName, framework);

    static string GetSnapshotExplorationProbesFilePath(string snapshotExplorationRootPath)
        => Path.Combine(snapshotExplorationRootPath, SnapshotExplorationTestProbesFileName);

    static string GetSnapshotExplorationReportFolderPath(string snapshotExplorationRootPath)
        => Path.Combine(snapshotExplorationRootPath, SnapshotExplorationTestReportFolderName);

    void RunSnapshotExplorationTestsInternal()
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Information($"Provided snapshot exploration test name is {ExplorationTestName}.");
            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            RunSnapshotExplorationTest(testDescription);
        }
        else
        {
            Logger.Information("Snapshot exploration test name is not provided, running all.");
            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                RunSnapshotExplorationTest(testDescription);
            }
        }
    }

    void RunSnapshotExplorationTest(ExplorationTestDescription testDescription)
    {
        if (!testDescription.ShouldRun)
        {
            Logger.Information($"Skipping exploration test: {testDescription.Name}.");
            return;
        }

        Logger.Information($"Running exploration test: {testDescription.Name}.");
        FileSystemTasks.EnsureCleanDirectory(Path.Combine(BuildDataDirectory, "logs"));

        var frameworks = Framework == null ? testDescription.SupportedFrameworks : new[] { Framework };
        foreach (var framework in frameworks)
        {
            if (!testDescription.IsFrameworkSupported(framework))
            {
                throw new InvalidOperationException($"The framework '{framework}' is not listed in the project's target frameworks of {testDescription.Name}");
            }

            testDescription.IsSnapshotScenario = true;
            var envVariables = GetEnvironmentVariables(testDescription, framework);
            var testRootPath = testDescription.GetTestTargetPath(ExplorationTestsDirectory, framework, BuildConfiguration);
            var snapshotExplorationRootPath = GetSnapshotExplorationRootPath(testRootPath, framework);
            FileSystemTasks.EnsureCleanDirectory(GetSnapshotExplorationReportFolderPath(snapshotExplorationRootPath));

            var testStopwatch = Stopwatch.StartNew();
            Test(testDescription, framework, envVariables);
            testStopwatch.Stop();

            VerifySnapshotExplorationTestResults(
                GetSnapshotExplorationProbesFilePath(snapshotExplorationRootPath),
                GetSnapshotExplorationReportFolderPath(snapshotExplorationRootPath),
                testStopwatch.Elapsed);
        }
    }

    void SetUpSnapshotExplorationTestsInternal()
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Information($"Provided snapshot exploration test name is {ExplorationTestName}.");
            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            CreateSnapshotExplorationTestProbeFile(testDescription);
        }
        else
        {
            Logger.Information("Snapshot exploration test name is not provided, running all.");
            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                CreateSnapshotExplorationTestProbeFile(testDescription);
            }
        }
    }

    void CreateSnapshotExplorationTestProbeFile(ExplorationTestDescription testDescription)
    {
        var frameworks = Framework != null ? new[] { Framework } : testDescription.SupportedFrameworks;
        foreach (var framework in frameworks)
        {
            var testRootPath = testDescription.GetTestTargetPath(ExplorationTestsDirectory, framework, BuildConfiguration);
            var snapshotExplorationRootPath = GetSnapshotExplorationRootPath(testRootPath, framework);
            FileSystemTasks.EnsureCleanDirectory(snapshotExplorationRootPath);
            var tracerAssemblyPath = GetTracerAssemblyPath(framework);
            var tracer = Assembly.LoadFile(tracerAssemblyPath);
            var extractorType = tracer.GetType("Datadog.Trace.Debugger.Symbols.SymbolExtractor");
            var createMethod = extractorType?.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            var getClassSymbols = extractorType?.GetMethod("GetClassSymbols", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
            var testAssembliesPaths = GetAllTestAssemblies(testRootPath);

            var probes = new List<SnapshotExplorationProbeDefinition>();

            foreach (var testAssemblyPath in testAssembliesPaths)
            {
                if (!TryLoadAssembly(testAssemblyPath, out var assembly))
                {
                    continue;
                }

                if (assembly.IsDynamic
                 || assembly.ManifestModule.IsResource()
                 || IgnoredNamespaces.Any(name => assembly.ManifestModule.Name.ToLower().StartsWith(name)))
                {
                    continue;
                }

                var symbolExtractor = createMethod?.Invoke(null, new object[] { assembly });
                if (symbolExtractor is null || getClassSymbols is null)
                {
                    continue;
                }

                if (getClassSymbols.Invoke(symbolExtractor, null) is not IEnumerable classSymbols)
                {
                    continue;
                }

                var processedScopes = ReflectionHelper.ProcessIEnumerable(classSymbols);

                foreach (var scope in processedScopes)
                {
                    if (!scope.TryGetValue("ScopeType", out var scopeType) || scopeType.ToString() != "Class")
                    {
                        continue;
                    }

                    var typeName = scope.TryGetValue("Name", out var typeNameValue) ? typeNameValue.ToString() : null;
                    scope.TryGetValue("Scopes", out var nestedScopes);
                    ProcessNestedScopes(nestedScopes as List<IDictionary<string, object>>, typeName, probes);
                }
            }

            WriteSnapshotExplorationProbeFile(GetSnapshotExplorationProbesFilePath(snapshotExplorationRootPath), probes);
        }

        return;

        void ProcessNestedScopes(List<IDictionary<string, object>>? scopes, string? typeName, List<SnapshotExplorationProbeDefinition> probes)
        {
            if (scopes == null || string.IsNullOrEmpty(typeName))
            {
                return;
            }

            if (IgnoredNamespaces.Any(ns => typeName.StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (!scope.TryGetValue("ScopeType", out var scopeType))
                {
                    continue;
                }

                if (scopeType.ToString() == "Closure")
                {
                    var closureName = scope.TryGetValue("Name", out var closureNameValue) ? closureNameValue.ToString() : null;
                    if (!string.IsNullOrEmpty(closureName))
                    {
                        Logger.Debug($"Skipping closure: {closureName}");
                    }

                    continue;
                }

                if (scopeType.ToString() == "Method")
                {
                    var isStatic = false;
                    scope.TryGetValue("LanguageSpecifics", out var ls);
                    if (ls?.GetType().GetProperty("Annotations")?.GetValue(ls) is IList<string> annotations)
                    {
                        // Check for static flag (0x0010) in method attributes
                        isStatic = (int.Parse(annotations[0], NumberStyles.HexNumber) & 0x0010) > 0;
                    }

                    var methodName = scope.TryGetValue("Name", out var methodNameValue) ? methodNameValue.ToString() : null;
                    if (string.IsNullOrEmpty(methodName))
                    {
                        continue;
                    }

                    if (methodName.Equals(".ctor", StringComparison.OrdinalIgnoreCase) ||
                        methodName.Equals(".cctor", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var returnType = ls?.GetType().GetProperty("ReturnType", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ls)?.ToString();
                    scope.TryGetValue("Symbols", out var symbols);
                    if (TryCreateSnapshotExplorationProbe(typeName, methodName, returnType, symbols as List<IDictionary<string, object>>, isStatic, out var probe))
                    {
                        probes.Add(probe);
                    }
                    else
                    {
                        Logger.Warning($"Error to add probe info for type: {typeName}, method: {methodName}");
                    }
                }
            }
        }
    }

    bool TryCreateSnapshotExplorationProbe(string type, string method, string? returnType, List<IDictionary<string, object>>? methodParameters, bool isStatic, [NotNullWhen(true)] out SnapshotExplorationProbeDefinition? probe)
    {
        probe = null;
        try
        {
            var typeName = SanitiseName(type);
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var methodName = SanitiseName(method);
            if (string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            // Native profiler expects 'this' as first type for instance methods, then parameter types
            // Used to disambiguate overloaded methods: This,Param1,Param2,etc
            var methodSignature = GetMethodSignature(methodParameters);

            var isInstanceMethod = !isStatic;
            probe = new SnapshotExplorationProbeDefinition(Guid.NewGuid().ToString(), typeName, methodName, methodSignature, isInstanceMethod);
            return true;
        }
        catch (Exception)
        {
            return false;
        }

        static string? SanitiseName(string? name) => name?.Replace(',', SpecialSeparator);

        string GetMethodSignature(List<IDictionary<string, object>>? symbols)
        {
            // Native profiler expects 'this' as first element for instance methods,
            // followed by actual parameter types (PDB already includes 'this' for instance methods)
            if (symbols == null)
            {
                return string.Empty;
            }

            var args = symbols
                .Where(symbol => symbol["SymbolType"].ToString() == "Arg")
                .Select(symbol => SanitiseName(symbol["Type"].ToString()))
                .Where(a => !string.IsNullOrEmpty(a))
               .ToList();

            return string.Join(SpecialSeparator, args!);
        }
    }

    private static void WriteSnapshotExplorationProbeFile(string probesFilePath, List<SnapshotExplorationProbeDefinition> probes)
    {
        using var stream = File.Create(probesFilePath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();
        foreach (var probe in probes)
        {
            writer.WriteStartObject();
            writer.WriteString("id", probe.ProbeId);
            writer.WriteString("language", "dotnet");
            writer.WriteString("type", "LOG_PROBE");
            writer.WriteStartObject("where");
            writer.WriteString("typeName", probe.TypeName);
            writer.WriteString("methodName", probe.MethodName);
            writer.WriteString("signature", probe.Signature.Replace(SpecialSeparator, ','));
            writer.WriteEndObject();
            writer.WriteBoolean("captureSnapshot", true);

            if (probe.IsInstanceMethod && ShouldSelectProbeBySignature(probe.TypeName, probe.MethodName, probe.Signature, 50))
            {
                writer.WriteStartObject("when");
                writer.WriteString("dsl", "ref this != null");
                writer.WritePropertyName("json");
                writer.WriteStartObject();
                writer.WriteStartArray("ne");
                writer.WriteStartObject();
                writer.WriteString("ref", "this");
                writer.WriteEndObject();
                writer.WriteNullValue();
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteString("str", string.Empty);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static bool ShouldSelectProbeBySignature(string type, string method, string signature, int thresholdPercent)
    {
        if (thresholdPercent <= 0)
        {
            return false;
        }

        if (thresholdPercent >= 100)
        {
            return true;
        }

        var key = $"{NormalizeForKey(type)}|{NormalizeForKey(method)}|{NormalizeForKey(signature)}";
        var bytes = Encoding.UTF8.GetBytes(key);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(bytes);
        return (BitConverter.ToUInt64(hash, 0) % 100UL) < (ulong)thresholdPercent;
    }

    private static string NormalizeForKey(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        s = s.Trim().ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        if (s.Length >= 2 &&
            ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'"))))
        {
            s = s.Substring(1, s.Length - 2);
        }

        return s;
    }

    private sealed record SnapshotExplorationProbeDefinition(string ProbeId, string TypeName, string MethodName, string Signature, bool IsInstanceMethod);

    public void VerifySnapshotExplorationTestResults(string probesFilePath, string reportFolderPath, TimeSpan testDuration)
    {
        var analysisStopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, TimeSpan>();
        var fileSizes = new Dictionary<string, long>();
        var stepWatch = Stopwatch.StartNew();

        var definedProbes = ReadDefinedProbes(probesFilePath);
        var definedProbeDetails = ReadDefinedProbeDetails(probesFilePath);
        timings["ReadDefinedProbes"] = stepWatch.Elapsed;
        if (definedProbes == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read probes file");
        }

        stepWatch.Restart();
        var installedProbeIds = ReadInstalledProbeIdsFromProbeStatusReport(reportFolderPath, definedProbes, out var probeStatusReportBytes, out var probeStatusCount);
        timings["ReadProbeStatusReport(installed)"] = stepWatch.Elapsed;
        fileSizes["ProbeStatusReport"] = probeStatusReportBytes;
        if (installedProbeIds == null || probeStatusCount == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read probe status report");
        }

        stepWatch.Restart();
        var skippedProbes = ReadSkippedProbesFromNativeLogs();
        timings["ReadNativeLogs(skipped)"] = stepWatch.Elapsed;

        stepWatch.Restart();
        var nativeRewriterFailures = ReadNativeRewriterFailuresFromLogs();
        timings["ReadNativeLogs(rewriter)"] = stepWatch.Elapsed;

        // Native rewriter log parsing currently returns a mix of:
        // - Probe-install failures (instrumentation not applied / probe not installed)
        // - Slot-level capture limitations (probe installed, but a specific arg/local was skipped as "not captured")
        // Split them here so the funnel + section titles match actual behavior in `debugger_method_rewriter.cpp`.
        var nativeCaptureLimitations = nativeRewriterFailures
            .Where(e => e.ErrorType is "ArgumentIsByRefLike" or "LocalIsByRefLike" or "ArgumentByRefLikeUnknown" or "LocalByRefLikeUnknown" or "PinnedArgOrLocal")
            .ToList();

        var nativeProbeInstallFailures = nativeRewriterFailures
            .Where(e => e.ErrorType is not ("ArgumentIsByRefLike" or "LocalIsByRefLike" or "ArgumentByRefLikeUnknown" or "LocalByRefLikeUnknown" or "PinnedArgOrLocal"))
            .ToList();

        // Check for potential log rolling that could cause incomplete data
        var logRollingWarning = CheckForLogRolling();

        stepWatch.Restart();
        var probesReport = ReadReportedSnapshotProbesIds(reportFolderPath, out var snapshotReportFileCount);
        timings["ReadProbesReport"] = stepWatch.Elapsed;
        if (probesReport == null || snapshotReportFileCount == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read report file");
        }

        // Read managed log errors (expression evaluation, serialization, etc.)
        stepWatch.Restart();
        var managedErrors = ReadErrorsFromManagedLogs(out var managedLogBytes);
        timings["ReadManagedLogs"] = stepWatch.Elapsed;
        fileSizes["ManagedLogs"] = managedLogBytes;

        var invalidOrErrorProbes = probesReport.Where(p => !p.IsValid || p.HasError).ToList();

        // Compare by probe ID only, not method name (values differ in format between sources)
        var reportedProbeIds = probesReport.Select(info => info.ProbeId).ToHashSet();
        var installedProbeIdKeys = installedProbeIds.Keys.ToHashSet();
        var definedProbeIdKeys = definedProbes.Keys.ToHashSet();

        // Probes that were installed but didn't produce a snapshot
        var installedButNoSnapshot = installedProbeIdKeys.Except(reportedProbeIds).ToHashSet();

        // Cross-reference with managed errors to understand WHY no snapshot
        var probesWithManagedErrors = managedErrors
            .Where(e => installedButNoSnapshot.Contains(e.ProbeId))
            .GroupBy(e => e.ProbeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Truly not called = installed but no snapshot AND no managed errors
        var trulyNotCalled = installedButNoSnapshot
            .Where(id => !probesWithManagedErrors.ContainsKey(id))
            .Select(id => new KeyValuePair<string, string>(id, installedProbeIds.GetValueOrDefault(id, "unknown")))
            .ToList();

        // Failed during processing = installed, no snapshot, but has managed errors
        var failedDuringProcessing = installedButNoSnapshot
            .Where(id => probesWithManagedErrors.ContainsKey(id))
            .Select(id => new KeyValuePair<string, string>(id, installedProbeIds.GetValueOrDefault(id, "unknown")))
            .ToList();

        // Probes in the generated probe file but native profiler couldn't install (signature mismatch, method doesn't exist, etc.)
        var notInstalled = definedProbeIdKeys.Except(installedProbeIdKeys)
            .Select(id => new KeyValuePair<string, string>(id, definedProbes.GetValueOrDefault(id, "unknown")))
            .ToList();

        // Statistics
        var installedCount = installedProbeIdKeys.Count;
        var reportedCount = reportedProbeIds.Count;
        var validReportedCount = probesReport.Count(p => p.IsValid && !p.HasError);

        analysisStopwatch.Stop();
        var analysisTime = analysisStopwatch.Elapsed;

        // Calculate performance metrics
        var snapshotsPerMinute = testDuration.TotalMinutes > 0 ? reportedCount / testDuration.TotalMinutes : 0;
        var avgTimePerSnapshot = reportedCount > 0 ? testDuration.TotalMilliseconds / reportedCount : 0;

        Logger.Information("╔══════════════════════════════════════════════════════════════╗");
        Logger.Information("║           SNAPSHOT EXPLORATION TEST RESULTS                  ║");
        Logger.Information("╚══════════════════════════════════════════════════════════════╝");
        Logger.Information("");
        Logger.Information("┌─────────────────────────────────────────────────────────────┐");
        Logger.Information("│ TIMING                                                      │");
        Logger.Information("├─────────────────────────────────────────────────────────────┤");
        Logger.Information($"│ Test execution time:          {FormatDuration(testDuration),12}                │");
        Logger.Information($"│ Log analysis time:            {FormatDuration(analysisTime),12}                │");
        Logger.Information($"│ Snapshots per minute:         {snapshotsPerMinute,12:F1}                │");
        Logger.Information($"│ Avg time per snapshot:        {avgTimePerSnapshot,9:F0} ms                │");
        Logger.Information("└─────────────────────────────────────────────────────────────┘");
        Logger.Information("");
        Logger.Information("┌─────────────────────────────────────────────────────────────┐");
        Logger.Information("│ ANALYSIS BREAKDOWN                                          │");
        Logger.Information("├─────────────────────────────────────────────────────────────┤");
        foreach (var timing in timings.OrderByDescending(t => t.Value))
        {
            Logger.Information($"│ {timing.Key,-30} {FormatDuration(timing.Value),10}              │");
        }
        Logger.Information("├─────────────────────────────────────────────────────────────┤");
        foreach (var size in fileSizes)
        {
            Logger.Information($"│ {size.Key + " size:",-30} {FormatBytes(size.Value),10}              │");
        }
        Logger.Information("└─────────────────────────────────────────────────────────────┘");
        Logger.Information("");
        // Calculate funnel numbers
        // Slot-level skips (e.g., byref-like/pinned args/locals) do NOT prevent probe installation.
        // Only probe-install failures should be deducted here.
        var neverJitCompiled = definedProbes.Count - installedCount - nativeProbeInstallFailures.Count;
        if (neverJitCompiled < 0) neverJitCompiled = 0; // Guard against overcounting in logs

        Logger.Information("┌─────────────────────────────────────────────────────────────┐");
        Logger.Information("│ PROBE FUNNEL (what happened at each stage)                  │");
        Logger.Information("├─────────────────────────────────────────────────────────────┤");
        Logger.Information($"│ 1. Probes defined in file            {definedProbes.Count,6}                   │");
        Logger.Information($"│    ├─ Never JIT-compiled (test didn't touch) {neverJitCompiled,6}          │");
        Logger.Information($"│    ├─ Rewriter failures (install)      {nativeProbeInstallFailures.Count,6}                   │");
        Logger.Information($"│    └─ Successfully installed         {installedCount,6}                   │");
        Logger.Information($"│                                           ↓                │");
        Logger.Information($"│ 2. Installed probes                  {installedCount,6}                   │");
        Logger.Information($"│    ├─ Method never called            {trulyNotCalled.Count,6}                   │");
        Logger.Information($"│    ├─ Failed during processing       {failedDuringProcessing.Count,6}                   │");
        Logger.Information($"│    └─ Generated snapshot             {reportedCount,6}                   │");
        Logger.Information($"│                                           ↓                │");
        Logger.Information($"│ 3. Snapshots generated               {reportedCount,6}                   │");
        Logger.Information($"│    ├─ ✗ Invalid/error                {invalidOrErrorProbes.Count,6}                   │");
        Logger.Information($"│    └─ ✓ Valid                        {validReportedCount,6}                   │");
        Logger.Information("└─────────────────────────────────────────────────────────────┘");

        // Success rate: of probes that generated snapshots, how many were valid?
        var snapshotSuccessRate = reportedCount > 0 ? (double)validReportedCount / reportedCount * 100 : 0;
        var installSuccessRate = installedCount > 0 ? (double)reportedCount / installedCount * 100 : 0;
        var overallRate = definedProbes.Count > 0 ? (double)validReportedCount / definedProbes.Count * 100 : 0;
        Logger.Information("");
        Logger.Information($"Funnel conversion: {overallRate:F1}% of defined probes → valid snapshots ({validReportedCount}/{definedProbes.Count})");
        Logger.Information($"Snapshot validity: {snapshotSuccessRate:F1}% ({validReportedCount}/{reportedCount})");
        Logger.Information($"Capture rate (of installed): {installSuccessRate:F1}% ({reportedCount}/{installedCount})");

        // === SECTION: Log Integrity Warning ===
        if (logRollingWarning != null)
        {
            Logger.Warning("");
            Logger.Warning("┌─────────────────────────────────────────────────────────────┐");
            Logger.Warning("│ ⚠ LOG DATA MAY BE INCOMPLETE                                │");
            Logger.Warning("└─────────────────────────────────────────────────────────────┘");
            Logger.Warning($"  {logRollingWarning}");
            Logger.Warning("  Error counts below may be underreported.");
        }

        // === SECTION: Native Profiler Skips (signature mismatches) ===
        if (skippedProbes.Any())
        {
            Logger.Warning("");
            Logger.Warning("┌─────────────────────────────────────────────────────────────┐");
            Logger.Warning("│ NATIVE PROFILER SKIPS (debug diagnostic)                    │");
            Logger.Warning("└─────────────────────────────────────────────────────────────┘");
            Logger.Warning("  These entries come from debug-level native logs and are informational only.");
            var reasonGroups = skippedProbes.GroupBy(p => p.Value).OrderByDescending(g => g.Count());
            foreach (var group in reasonGroups.Take(10))
            {
                Logger.Warning($"  [{group.Count()} probes] {group.Key}");
                foreach (var probe in group.Take(3))
                {
                    Logger.Warning($"    • {probe.Key}");
                }
                if (group.Count() > 3)
                {
                    Logger.Warning($"    ... and {group.Count() - 3} more");
                }
            }
        }

        // === SECTION: Native Rewriter Failures (probe install / instrumentation errors) ===
        if (nativeProbeInstallFailures.Any())
        {
            Logger.Error("");
            Logger.Error("┌─────────────────────────────────────────────────────────────┐");
            Logger.Error("│ NATIVE REWRITER FAILURES (probe not installed)              │");
            Logger.Error("└─────────────────────────────────────────────────────────────┘");
            const int topMethodsToShow = 10;
            var errorTypeGroups = nativeProbeInstallFailures.GroupBy(e => e.ErrorType).OrderByDescending(g => g.Count());
            foreach (var group in errorTypeGroups)
            {
                var uniqueMethods = group.Select(e => e.MethodName).Distinct().Count();
                var uniqueProbes = group.Select(e => e.ProbeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
                Logger.Error($"  ▸ {group.Key}: {group.Count()} occurrences ({uniqueMethods} unique methods, {uniqueProbes} probe IDs)");

                // Group by method name to avoid repeated "• Method" spam and to highlight hot spots
                var topMethods = group
                    .GroupBy(e => e.MethodName)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Take(topMethodsToShow);

                foreach (var methodGroup in topMethods)
                {
                    var methodProbeIds = methodGroup
                        .Select(e => e.ProbeId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .Take(3)
                        .ToList();
                    var probeSuffix = methodProbeIds.Count > 0 ? $" (e.g. {string.Join(", ", methodProbeIds)})" : string.Empty;
                    Logger.Error($"    • {methodGroup.Key} ({methodGroup.Count()}x){probeSuffix}");
                }

                if (uniqueMethods > topMethodsToShow)
                {
                    Logger.Error($"    ... and {uniqueMethods - topMethodsToShow} more methods");
                }
            }
        }

        // === SECTION: Native Capture Limitations (probe installed, slot not captured) ===
        if (nativeCaptureLimitations.Any())
        {
            Logger.Warning("");
            Logger.Warning("┌─────────────────────────────────────────────────────────────┐");
            Logger.Warning("│ NATIVE CAPTURE LIMITATIONS (not captured)                   │");
            Logger.Warning("└─────────────────────────────────────────────────────────────┘");

            var limitationGroups = nativeCaptureLimitations.GroupBy(e => e.ErrorType).OrderByDescending(g => g.Count());
            foreach (var group in limitationGroups)
            {
                var uniqueMethods = group.Select(e => e.MethodName).Distinct().Count();
                var uniqueProbes = group.Select(e => e.ProbeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
                Logger.Warning($"  ▸ {group.Key}: {group.Count()} occurrences ({uniqueMethods} unique methods, {uniqueProbes} probe IDs)");

                var topMethods = group
                    .GroupBy(e => e.MethodName)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Take(10);

                foreach (var methodGroup in topMethods)
                {
                    var methodProbeIds = methodGroup
                        .Select(e => e.ProbeId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .Take(3)
                        .ToList();
                    var probeSuffix = methodProbeIds.Count > 0 ? $" (e.g. {string.Join(", ", methodProbeIds)})" : string.Empty;
                    Logger.Warning($"    • {methodGroup.Key} ({methodGroup.Count()}x){probeSuffix}");
                }
            }

            // Combined view for ByRefLike issues (args + locals) to help prioritize hotspots.
            var byRefLike = nativeCaptureLimitations
                .Where(e => e.ErrorType is "ArgumentIsByRefLike" or "LocalIsByRefLike")
                .ToList();

            if (byRefLike.Count > 0)
            {
                Logger.Warning("");
                Logger.Warning("  ─ BYREFLIKE HOTSPOTS (combined args+locals)");

                var hotspots = byRefLike
                    .GroupBy(e => e.MethodName)
                    .Select(g =>
                    {
                        var argCount = g.Count(x => x.ErrorType == "ArgumentIsByRefLike");
                        var localCount = g.Count(x => x.ErrorType == "LocalIsByRefLike");
                        var total = argCount + localCount;
                        var probes = g.Select(x => x.ProbeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                        return new { Method = g.Key, ArgCount = argCount, LocalCount = localCount, Total = total, Probes = probes };
                    })
                    .OrderByDescending(x => x.Total)
                    .ThenByDescending(x => x.ArgCount)
                    .ThenBy(x => x.Method)
                    .Take(15);

                foreach (var h in hotspots)
                {
                    var sampleProbes = h.Probes.Take(3).ToList();
                    var probesSuffix = h.Probes.Count > 0 ? $" Probes={h.Probes.Count} (e.g. {string.Join(", ", sampleProbes)})" : string.Empty;
                    Logger.Warning($"    • {h.Method} (Args={h.ArgCount}, Locals={h.LocalCount}, Total={h.Total}){probesSuffix}");
                }

                Logger.Warning("");
                Logger.Warning("  ─ RECOMMENDED ACTIONS");

                var topArgByRefLike = byRefLike
                    .Where(e => e.ErrorType == "ArgumentIsByRefLike")
                    .GroupBy(e => e.MethodName)
                    .Select(g => new
                    {
                        Method = g.Key,
                        Count = g.Count(),
                        Probes = g.Select(x => x.ProbeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(2).ToList()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Method)
                    .Take(3)
                    .ToList();

                Logger.Warning("    • Top 3 methods with ByRefLike *arguments* (expected: slot not captured):");
                foreach (var m in topArgByRefLike)
                {
                    Logger.Warning($"      - {m.Method} ({m.Count}x)");
                    LogReproProbeIdsWithDetails(Logger.Warning, m.Probes, definedProbeDetails, "        ");
                }

                var topLocalByRefLike = byRefLike
                    .Where(e => e.ErrorType == "LocalIsByRefLike")
                    .GroupBy(e => e.MethodName)
                    .Select(g => new
                    {
                        Method = g.Key,
                        Count = g.Count(),
                        Probes = g.Select(x => x.ProbeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(2).ToList()
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Method)
                    .Take(3)
                    .ToList();

                Logger.Warning("    • Top 3 methods with ByRefLike *locals* (investigate; slot not captured):");
                foreach (var m in topLocalByRefLike)
                {
                    Logger.Warning($"      - {m.Method} ({m.Count}x)");
                    LogReproProbeIdsWithDetails(Logger.Warning, m.Probes, definedProbeDetails, "        ");
                }
            }
        }

        // === SECTION: CRITICAL - Instrumentation Breaking Application Code ===
        var criticalInstrumentationErrors = managedErrors.Where(e => e.ErrorType.StartsWith("CRITICAL:")).ToList();
        if (criticalInstrumentationErrors.Any())
        {
            Logger.Error("");
            Logger.Error("╔═════════════════════════════════════════════════════════════╗");
            Logger.Error("║ ⚠️  CRITICAL: INSTRUMENTATION BREAKING APPLICATION CODE  ⚠️ ║");
            Logger.Error("╠═════════════════════════════════════════════════════════════╣");
            Logger.Error("║ These errors indicate our instrumentation is corrupting     ║");
            Logger.Error("║ application state and causing exceptions in user code!      ║");
            Logger.Error("╚═════════════════════════════════════════════════════════════╝");

            var groupedByMethod = criticalInstrumentationErrors.GroupBy(e => e.MethodContext).OrderByDescending(g => g.Count());
            foreach (var group in groupedByMethod.Take(10))
            {
                var errorType = group.First().ErrorType.Replace("CRITICAL:", "");
                Logger.Error($"  ▸ {group.Key}");
                Logger.Error($"    Exception: {errorType} ({group.Count()} occurrences)");
            }
        }

        // === SECTION: Managed Processing Errors ===
        var nonCriticalErrors = managedErrors.Where(e => !e.ErrorType.StartsWith("CRITICAL:")).ToList();
        if (nonCriticalErrors.Any())
        {
            Logger.Error("");
            Logger.Error("┌─────────────────────────────────────────────────────────────┐");
            Logger.Error("│ MANAGED PROCESSING ERRORS (bugs in tracer)                  │");
            Logger.Error("└─────────────────────────────────────────────────────────────┘");

            // Group by error type
            var errorTypeGroups = nonCriticalErrors.GroupBy(e => e.ErrorType).OrderByDescending(g => g.Count());
            foreach (var group in errorTypeGroups)
            {
                Logger.Error($"  ▸ {group.Key}: {group.Count()} occurrences");

                // Sub-group by error message to find patterns
                var messageGroups = group.GroupBy(e => e.ErrorMessage).OrderByDescending(g => g.Count()).Take(5);
                foreach (var msgGroup in messageGroups)
                {
                    Logger.Error($"    [{msgGroup.Count()}x] {msgGroup.Key}");
                }
            }
        }

        // === SECTION: Failed During Processing (detailed) ===
        if (failedDuringProcessing.Any())
        {
            Logger.Error("");
            Logger.Error("┌─────────────────────────────────────────────────────────────┐");
            Logger.Error("│ PROBES FAILED DURING PROCESSING (installed but no snapshot) │");
            Logger.Error("└─────────────────────────────────────────────────────────────┘");
            Logger.Error($"  Total: {failedDuringProcessing.Count} probes");
            Logger.Error("  These probes were installed and called, but failed during:");
            Logger.Error("  - Expression evaluation (condition check)");
            Logger.Error("  - Snapshot serialization");
            Logger.Error("  - Probe processor handling");
            Logger.Error("");

            // Show some examples with their errors
            var examplesShown = 0;
            foreach (var probe in failedDuringProcessing.Take(10))
            {
                if (probesWithManagedErrors.TryGetValue(probe.Key, out var errors))
                {
                    Logger.Error($"  • {probe.Value} (ID: {probe.Key})");
                    foreach (var error in errors.Take(2))
                    {
                        Logger.Error($"      → {error.ErrorType}: {error.ErrorMessage}");
                    }
                    examplesShown++;
                }
            }
            if (failedDuringProcessing.Count > 10)
            {
                Logger.Error($"  ... and {failedDuringProcessing.Count - 10} more");
            }
        }

        // === SECTION: Invalid/Error Snapshots ===
        if (invalidOrErrorProbes.Any())
        {
            Logger.Error("");
            Logger.Error("┌─────────────────────────────────────────────────────────────┐");
            Logger.Error("│ INVALID/ERROR SNAPSHOTS                                     │");
            Logger.Error("└─────────────────────────────────────────────────────────────┘");
            foreach (var probe in invalidOrErrorProbes.Take(20))
            {
                Logger.Error($"  • {probe.Name} (ID: {probe.ProbeId}) - Valid: {probe.IsValid}, HasError: {probe.HasError}");
            }
            if (invalidOrErrorProbes.Count > 20)
            {
                Logger.Error($"  ... and {invalidOrErrorProbes.Count - 20} more");
            }
        }

        // === SECTION: Methods Truly Not Called ===
        if (trulyNotCalled.Any())
        {
            Logger.Information("");
            Logger.Information("┌─────────────────────────────────────────────────────────────┐");
            Logger.Information("│ METHODS NOT CALLED (installed but test didn't exercise)    │");
            Logger.Information("└─────────────────────────────────────────────────────────────┘");
            Logger.Information($"  Total: {trulyNotCalled.Count} probes");
            Logger.Information("  Note: These methods were JIT-compiled but not executed,");
            Logger.Information("  or executed but probe didn't fire (possible JIT timing).");
            foreach (var probe in trulyNotCalled.Take(10))
            {
                Logger.Information($"  • {probe.Value}");
            }
            if (trulyNotCalled.Count > 10)
            {
                Logger.Information($"  ... and {trulyNotCalled.Count - 10} more");
            }
        }

        // === FINAL VERDICT ===
        Logger.Information("");
        Logger.Information("╔══════════════════════════════════════════════════════════════╗");

        // Any probe-related error is a bug that should be fixed (expression evaluation, serialization, etc.)
        var probeRelatedErrors = managedErrors.Where(e => e.ProbeId != "unknown").ToList();

        // Critical native rewriter failures are bugs (ByRefLike, unsupported bytecode, locals parse failures)
        var criticalNativeFailures = nativeRewriterFailures
            .Where(e => e.ErrorType is "FailedToParseLocals" or "UnsupportedBytecode" or "TypeIsByRefLike")
            .ToList();

        var hasFailures = invalidOrErrorProbes.Any() || failedDuringProcessing.Any() || probeRelatedErrors.Any() || criticalNativeFailures.Any();

        if (hasFailures)
        {
            Logger.Error("║ RESULT: FAILED - Bugs detected in tracer                    ║");
            Logger.Error("╚══════════════════════════════════════════════════════════════╝");
            Logger.Error("");
            Logger.Error("Action items:");
            var actionItem = 1;
            if (invalidOrErrorProbes.Any())
            {
                Logger.Error($"  {actionItem++}. Fix {invalidOrErrorProbes.Count} snapshot serialization errors");
            }
            if (failedDuringProcessing.Any())
            {
                Logger.Error($"  {actionItem++}. Fix {failedDuringProcessing.Count} processing failures (installed but no snapshot)");
            }
            if (probeRelatedErrors.Any())
            {
                // Group by error type to show breakdown
                var errorsByType = probeRelatedErrors.GroupBy(e => e.ErrorType).OrderByDescending(g => g.Count());
                Logger.Error($"  {actionItem++}. Fix {probeRelatedErrors.Count} probe-related errors:");
                foreach (var group in errorsByType.Take(5))
                {
                    Logger.Error($"       - {group.Key}: {group.Count()} occurrences");
                }
            }
            if (criticalNativeFailures.Any())
            {
                var errorsByType = criticalNativeFailures.GroupBy(e => e.ErrorType).OrderByDescending(g => g.Count());
                Logger.Error($"  {actionItem++}. Fix {criticalNativeFailures.Count} native rewriter failures:");
                foreach (var group in errorsByType)
                {
                    Logger.Error($"       - {group.Key}: {group.Count()} occurrences");
                }
            }
            if (skippedProbes.Any())
            {
                Logger.Warning($"  {actionItem++}. (Optional) Inspect {skippedProbes.Count} debug-level signature mismatch log entries");
            }

            throw new Exception($"Snapshot exploration test failed: {invalidOrErrorProbes.Count} invalid snapshots, {failedDuringProcessing.Count} processing failures, {probeRelatedErrors.Count} probe errors, {criticalNativeFailures.Count} native rewriter failures, {skippedProbes.Count} signature mismatches");
        }

        if (installedCount == 0 && definedProbes.Count > 0)
        {
            Logger.Error("║ RESULT: FAILED - No probes installed                         ║");
            Logger.Error("╚══════════════════════════════════════════════════════════════╝");
            throw new Exception("Snapshot exploration test failed: No probes were installed. Check debugger initialization and probe loading.");
        }

        if (reportedCount == 0 && installedCount > 0)
        {
            Logger.Error("║ RESULT: FAILED - No snapshots collected                      ║");
            Logger.Error("╚══════════════════════════════════════════════════════════════╝");
            throw new Exception("Snapshot exploration test failed: Probes were installed but no snapshots were collected.");
        }

        Logger.Information("║ RESULT: PASSED                                               ║");
        Logger.Information("╚══════════════════════════════════════════════════════════════╝");

        if (skippedProbes.Any() || trulyNotCalled.Any())
        {
            Logger.Warning("");
            Logger.Warning("Warnings (non-blocking):");
            if (skippedProbes.Any())
            {
                Logger.Warning($"  - {skippedProbes.Count} debug-level signature mismatch log entries observed");
            }
            if (trulyNotCalled.Any())
            {
                Logger.Warning($"  - {trulyNotCalled.Count} methods not exercised by tests");
            }
        }

        // Output paths for detailed investigation
        Logger.Information("");
        Logger.Information("Log locations for detailed investigation:");
        Logger.Information($"  Logs: {BuildDataDirectory / "logs"}");
        Logger.Information($"  Report folder: {reportFolderPath}");
    }

    public static Dictionary<string, string> ReadDefinedProbes(string probesPath)
    {
        if (string.IsNullOrEmpty(probesPath))
        {
            throw new ArgumentException("Report path cannot be null or empty", nameof(probesPath));
        }

        if (!File.Exists(probesPath))
        {
            throw new FileNotFoundException("The specified report file does not exist", probesPath);
        }

        return ReadDefinedProbeDefinitions(probesPath)
                   .ToDictionary(
                        probe => probe.ProbeId,
                        probe => $"{probe.TypeName}.{probe.MethodName}"
                    );
    }

    private static Dictionary<string, string> ReadDefinedProbeDetails(string probesPath)
    {
        // Returns a compact, single-line representation of the original probe definition for quick repro/debug.
        // Format: "<Type>.<Method>, <Signature>, IsInstance=<true/false>"
        return ReadDefinedProbeDefinitions(probesPath)
                   .ToDictionary(
                        probe => probe.ProbeId,
                        probe => $"{probe.TypeName}.{probe.MethodName}, {probe.Signature}, IsInstance={probe.IsInstanceMethod}");
    }

    private static List<SnapshotExplorationProbeDefinition> ReadDefinedProbeDefinitions(string probesPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(probesPath));
        var result = new List<SnapshotExplorationProbeDefinition>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var id = element.GetProperty("id").GetString();
            if (string.IsNullOrEmpty(id) || !element.TryGetProperty("where", out var where))
            {
                continue;
            }

            var typeName = where.TryGetProperty("typeName", out var typeNameElement) ? typeNameElement.GetString() : null;
            var methodName = where.TryGetProperty("methodName", out var methodNameElement) ? methodNameElement.GetString() : null;
            var signature = where.TryGetProperty("signature", out var signatureElement) ? signatureElement.GetString() : string.Empty;
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
            {
                continue;
            }

            result.Add(new SnapshotExplorationProbeDefinition(id, typeName, methodName, signature ?? string.Empty, HasThisParameter(signature)));
        }

        return result;

        static bool HasThisParameter(string? signature)
            => !string.IsNullOrEmpty(signature) &&
               signature.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() == "this";
    }

    private static void LogReproProbeIdsWithDetails(Action<string> log, List<string?> probeIds, Dictionary<string, string> definedProbeDetails, string indent)
    {
        if (probeIds == null || probeIds.Count == 0)
        {
            return;
        }

        foreach (var id in probeIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (definedProbeDetails.TryGetValue(id, out var row))
            {
                // One probe per line to keep the report readable and still copy/paste friendly
                log($"{indent}repro: {id} | {row}");
            }
            else
            {
                log($"{indent}repro: {id}");
            }
        }
    }

    List<ProbeReportInfo> ReadReportedSnapshotProbesIds(string reportFolderPath, out int reportFileCount)
    {
        if (string.IsNullOrEmpty(reportFolderPath))
        {
            throw new ArgumentException("Report path cannot be null or empty", nameof(reportFolderPath));
        }

        if (!Directory.Exists(reportFolderPath))
        {
            throw new FileNotFoundException("The specified report path does not exist", reportFolderPath);
        }

        var reportInfo = new List<ProbeReportInfo>();
        reportFileCount = 0;

        foreach (var file in Directory.EnumerateFiles(reportFolderPath, "*_SnapshotExplorationTestReport.csv"))
        {
            reportFileCount++;
            reportInfo.AddRange(
            File.ReadLines(file)
                .Skip(1) // Skip the header row
                .Select(ParseCsvLine)
                .ToList());
        }

        return reportInfo;
    }

    ProbeReportInfo ParseCsvLine(string line)
    {
        var parts = ParseCsvFields(line);

        if (parts.Length != 4 || parts.Any(string.IsNullOrWhiteSpace))
        {
            var probeId = parts.Length > 0 ? parts[0].Trim() : "missing id";
            var type = parts.Length > 1 ? parts[1].Trim() : "missing type";
            var method = parts.Length > 2 ? parts[2].Trim() : "missing method";
            return new ProbeReportInfo(probeId, type + "." + method, false, true);
        }

        var isValid = bool.TryParse(parts[3].Trim(), out var parsedIsValid) && parsedIsValid;
        return new ProbeReportInfo(parts[0].Trim(), parts[1].Trim() + "." + parts[2].Trim(), isValid, false);

        static string[] ParseCsvFields(string csvLine)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < csvLine.Length; i++)
            {
                var current = csvLine[i];
                if (current == '"')
                {
                    if (inQuotes && i + 1 < csvLine.Length && csvLine[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (current == ',' && !inQuotes)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(current);
                }
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }
    }

    /// <summary>
    /// Checks if log files might have been rolled during test execution.
    /// Returns a warning message if rolled log files are detected, or null if logs appear complete.
    /// </summary>
    private string? CheckForLogRolling()
    {
        var logDirectory = BuildDataDirectory / "logs";
        if (!Directory.Exists(logDirectory))
        {
            return null;
        }

        var nativeLogs = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*.log");
        var managedLogs = Directory.GetFiles(logDirectory, "dotnet-tracer-managed-*.log");

        // Calculate total sizes first
        var totalNativeSize = nativeLogs.Sum(f => new FileInfo(f).Length);
        var totalManagedSize = managedLogs.Sum(f => new FileInfo(f).Length);

        // Check for rolled logs by looking for numbered suffixes.
        // Rolled files use pattern: dotnet-tracer-managed-{process}-{pid}_{sequence}.log
        var rolledNative = nativeLogs.Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"_\d+\.log$")).ToList();
        var rolledManaged = managedLogs.Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"_\d+\.log$")).ToList();

        if (rolledNative.Count > 0 || rolledManaged.Count > 0)
        {
            // We read ALL rolled files, but warn about the volume for awareness
            return $"Log rolling occurred: {rolledNative.Count} native log files, {rolledManaged.Count} managed log files. " +
                   $"All files are being analyzed, but analysis may be slow with {(totalNativeSize + totalManagedSize) / 1_000_000}MB of logs.";
        }

        // Check total log file sizes for awareness (we read all files, but large logs slow analysis)
        const long warningThreshold = 500_000_000; // 500MB warning threshold

        if (totalNativeSize > warningThreshold || totalManagedSize > warningThreshold)
        {
            return $"Large log volume: native={totalNativeSize / 1_000_000}MB, managed={totalManagedSize / 1_000_000}MB across {nativeLogs.Length + managedLogs.Length} files. " +
                   "All files analyzed. Consider reducing probe count if analysis is slow.";
        }

        return null;
    }

    private Dictionary<string, string> ReadInstalledProbeIdsFromProbeStatusReport(
        string reportFolderPath,
        Dictionary<string, string> definedProbes,
        out long totalBytesRead,
        out int probeStatusCount)
    {
        var result = new Dictionary<string, string>();
        var latestStatuses = new Dictionary<string, string>(StringComparer.Ordinal);
        totalBytesRead = 0;
        probeStatusCount = 0;

        if (!Directory.Exists(reportFolderPath))
        {
            throw new Exception($"Snapshot exploration report folder does not exist in path: {reportFolderPath}");
        }

        var reportFiles = Directory.GetFiles(reportFolderPath, "*_SnapshotExplorationProbeStatuses.csv");

        foreach (var reportFile in reportFiles)
        {
            totalBytesRead += new FileInfo(reportFile).Length;
            var lines = File.ReadAllLines(reportFile);

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 2)
                {
                    continue;
                }

                var probeId = parts[0].Trim();
                if (string.IsNullOrEmpty(probeId))
                {
                    continue;
                }

                probeStatusCount++;
                latestStatuses[probeId] = parts[1].Trim();
            }
        }

        foreach (var pair in latestStatuses)
        {
            if (pair.Value is "INSTALLED" or "INSTRUMENTED" or "EMITTING")
            {
                result[pair.Key] = definedProbes.GetValueOrDefault(pair.Key, "unknown");
            }
        }

        return result;
    }

    /// <summary>
    /// Reads optional debug-level native signature mismatch diagnostics.
    /// Format in logs: "* Skipping MethodName: reason"
    /// </summary>
    private Dictionary<string, string> ReadSkippedProbesFromNativeLogs()
    {
        var result = new Dictionary<string, string>();
        var logDirectory = BuildDataDirectory / "logs";

        if (!Directory.Exists(logDirectory))
        {
            return result;
        }

        var logFiles = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*.log");
        const string skipMarker = "* Skipping ";

        foreach (var logFile in logFiles)
        {
            var logLines = File.ReadAllLines(logFile);

            foreach (var line in logLines)
            {
                if (!line.Contains(skipMarker))
                {
                    continue;
                }

                var skipStart = line.IndexOf(skipMarker) + skipMarker.Length;
                var colonIndex = line.IndexOf(':', skipStart);
                if (colonIndex == -1) continue;

                var methodName = line.Substring(skipStart, colonIndex - skipStart).Trim();
                var reason = line.Substring(colonIndex + 1).Trim();

                // Use method name as key since we don't have probe ID in skip messages
                result.TryAdd(methodName, reason);
            }
        }

        return result;
    }

    record NativeRewriterError(string MethodName, string ErrorType, string RawLog, string? ProbeId);

    /// <summary>
    /// Reads critical native rewriter failures from native logs.
    /// These are bugs/limitations we care about:
    /// - invalid_probe_failed_to_parse_locals
    /// - non_supported_compiled_bytecode
    /// - invalid_probe_type_is_by_ref_like
    ///
    /// COUPLED SOURCE FILE (update if log format changes):
    /// - tracer/src/Datadog.Tracer.Native/debugger_method_rewriter.cpp
    ///   - "failed to parse locals signature" (line ~2220)
    ///   - "IL contain unsupported instructions" (line ~2201)
    ///   - "type we are instrumenting is By-Ref like" (line ~2323)
    ///   - "return value is By-Ref like" (line ~2309)
    ///   - "argument index = ... because it's By-Ref like" (line ~112)
    ///   - "local index = ... because it's By-Ref like" (line ~112)
    ///   - "Failed to determine if argument/local index = ... is By-Ref like." (line ~108)
    ///   - "because it's a pinned local." (line ~125)
    /// </summary>
    private List<NativeRewriterError> ReadNativeRewriterFailuresFromLogs()
    {
        var result = new List<NativeRewriterError>();
        var logDirectory = BuildDataDirectory / "logs";

        if (!Directory.Exists(logDirectory))
        {
            return result;
        }

        var logFiles = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*.log");

        // Map warning messages to error type categories
        var errorPatterns = new Dictionary<string, string>
        {
            { "failed to parse locals signature", "FailedToParseLocals" },
            { "IL contain unsupported instructions", "UnsupportedBytecode" },
            { "type we are instrumenting is By-Ref like", "TypeIsByRefLike" },
            { "return value is By-Ref like", "ReturnIsByRefLike" },

            // Slot-level limitations (probe remains installed; this value is simply not captured)
            { "Failed to determine if argument index = ", "ArgumentByRefLikeUnknown" },
            { "Failed to determine if local index = ", "LocalByRefLikeUnknown" },
            { "argument index = ", "ArgumentIsByRefLike" }, // followed by "because it's By-Ref like"
            { "local index = ", "LocalIsByRefLike" }, // followed by "because it's By-Ref like"
            { "because it's a pinned local", "PinnedArgOrLocal" },
        };

        foreach (var logFile in logFiles)
        {
            var logLines = File.ReadAllLines(logFile);

            for (int i = 0; i < logLines.Length; i++)
            {
                var line = logLines[i];

                foreach (var pattern in errorPatterns)
                {
                    if (line.Contains(pattern.Key))
                    {
                        // For ByRefLike "is" patterns, make sure the message indicates the value itself is By-Ref like,
                        // not just that the line contains an "index =" fragment (e.g., pinned locals or other messages).
                        if ((pattern.Value == "ArgumentIsByRefLike" || pattern.Value == "LocalIsByRefLike") &&
                            !line.Contains("because it's By-Ref like"))
                        {
                            continue;
                        }

                        var methodName = ExtractMethodNameFromNativeLine(line);
                        var raw = line.Length > 200 ? line.Substring(0, 200) + "..." : line;

                        result.Add(new NativeRewriterError(methodName, pattern.Value, raw, ProbeId: null));
                        break; // Don't double-count
                    }
                }
            }
        }

        return result;
    }

    private string ExtractMethodNameFromNativeLine(string line)
    {
        // Try to extract method name from patterns like "caller_name=Type.Method()"
        var match = System.Text.RegularExpressions.Regex.Match(line, @"caller_name=([^(]+)");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Try ModuleID pattern
        match = System.Text.RegularExpressions.Regex.Match(line, @"ModuleID=\d+\s+(\d+)");
        if (match.Success)
        {
            return $"Token:{match.Groups[1].Value}";
        }

        return "unknown";
    }

    record ProbeReportInfo(string ProbeId, string Name, bool IsValid, bool HasError)
    {
        public string ProbeId { get; set; } = ProbeId;
        public string Name { get; set; } = Name;
        public bool IsValid { get; set; } = IsValid;
        public bool HasError { get; set; } = HasError;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        }
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }
        if (duration.TotalSeconds >= 1)
        {
            return $"{duration.TotalSeconds:F1}s";
        }

        // Sub-second durations are common in the analysis breakdown; show them as milliseconds
        return $"{duration.TotalMilliseconds:F0}ms";
    }

    private static string FormatBytes(long bytes)
    {
        // Use binary units for clearer small-file reporting (e.g., 14KB doesn't show as "0 MB")
        const double kib = 1024;
        const double mib = kib * 1024;

        if (bytes < mib)
        {
            return $"{bytes / kib:F0} KB";
        }

        return $"{bytes / mib:F1} MB";
    }

    record ManagedLogError(string ProbeId, string ErrorType, string ErrorMessage, string MethodContext);

    /// <summary>
    /// Reads errors from managed tracer logs (expression evaluation failures, serialization errors, etc.)
    /// Catches ANY exception that occurs in probe-related context (has probeId nearby in logs).
    ///
    /// COUPLED SOURCE FILES (update if log format changes):
    /// - tracer/src/Datadog.Trace/Debugger/Expressions/ProbeProcessor.cs
    ///   - "Failed to process probe" (line ~314)
    ///   - "Failed to evaluate expression" (line ~342)
    /// - Any managed log with "[ERR]" and a probeId in context
    /// </summary>
    private List<ManagedLogError> ReadErrorsFromManagedLogs(out long totalBytesRead)
    {
        var result = new List<ManagedLogError>();
        var logDirectory = BuildDataDirectory / "logs";
        totalBytesRead = 0;

        if (!Directory.Exists(logDirectory))
        {
            return result;
        }

        var logFiles = Directory.GetFiles(logDirectory, "dotnet-tracer-managed-*.log");
        var seenErrors = new HashSet<string>(); // Dedupe by probeId+errorType+message

        foreach (var logFile in logFiles)
        {
            totalBytesRead += new FileInfo(logFile).Length;
            var logContent = File.ReadAllText(logFile);
            var lines = logContent.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Look for ANY exception that has a probeId in context
                // This catches InvalidCastException, NullReferenceException, JsonException, etc.
                if (line.Contains("Exception:") && !line.Contains("ExceptionAutoInstrumentation"))
                {
                    var probeId = ExtractProbeIdFromContext(lines, i);
                    if (probeId != "unknown") // Only count if we can associate with a probe
                    {
                        var errorType = ExtractExceptionType(line);
                        var errorMessage = ExtractExceptionMessage(line);
                        var key = $"{probeId}|{errorType}|{errorMessage}";
                        if (seenErrors.Add(key))
                        {
                            result.Add(new ManagedLogError(probeId, errorType, errorMessage, ""));
                        }
                    }
                }

                // Look for [ERR] log entries with probe context
                if (line.Contains("[ERR]"))
                {
                    var probeId = ExtractProbeIdFromLine(line);
                    if (probeId == "unknown")
                    {
                        probeId = ExtractProbeIdFromContext(lines, i);
                    }
                    if (probeId != "unknown")
                    {
                        var errorType = "LoggedError";
                        var errorMessage = line.Length > 200 ? line.Substring(0, 200) + "..." : line;
                        var key = $"{probeId}|{errorType}|{errorMessage}";
                        if (seenErrors.Add(key))
                        {
                            result.Add(new ManagedLogError(probeId, errorType, errorMessage, ""));
                        }
                    }
                }

                // CRITICAL: Look for "Error caused by our instrumentation" - these are serious bugs!
                // These errors indicate our instrumentation is breaking application code.
                if (line.Contains("Error caused by our instrumentation"))
                {
                    var exceptionMatch = FindExceptionInFollowingLines(lines, i, 5);
                    var errorType = exceptionMatch != null ? ExtractExceptionType(exceptionMatch) : "InstrumentationError";
                    var methodMatch = ExtractMethodFromStackTrace(lines, i, 10);
                    var errorMessage = exceptionMatch ?? line;
                    var key = $"instrumentation|{errorType}|{methodMatch}";
                    if (seenErrors.Add(key))
                    {
                        result.Add(new ManagedLogError("instrumentation-error", $"CRITICAL:{errorType}", errorMessage, methodMatch ?? "unknown"));
                    }
                }
            }
        }

        return result;
    }

    private string? ExtractMethodFromStackTrace(string[] lines, int startIndex, int maxLines)
    {
        // Look for "at MethodName(" pattern in stack trace
        for (int i = startIndex; i < Math.Min(lines.Length, startIndex + maxLines); i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"at\s+([^\(]+)\(");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        return null;
    }

    private string ExtractExceptionType(string line)
    {
        // Extract exception type like "InvalidCastException", "NullReferenceException", "JsonSerializationException"
        var match = System.Text.RegularExpressions.Regex.Match(line, @"(System\.)?(\w+Exception)");
        return match.Success ? match.Groups[2].Value : "UnknownException";
    }

    private string ExtractProbeIdFromLine(string line)
    {
        // Look for probeId= pattern
        var match = System.Text.RegularExpressions.Regex.Match(line, @"probeId=([a-f0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Look for "Probe Id: <guid>" pattern (e.g. "Failed to process probe. Probe Id: ...")
        match = System.Text.RegularExpressions.Regex.Match(line, @"Probe\s+Id:\s*([a-f0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    private string ExtractProbeIdFromContext(string[] lines, int currentIndex)
    {
        // Look backwards for the most recent probeId mention
        for (int i = currentIndex; i >= Math.Max(0, currentIndex - 20); i--)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"probeId=([a-f0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return "unknown";
    }

    private string? FindExceptionInFollowingLines(string[] lines, int startIndex, int maxLines)
    {
        for (int i = startIndex; i < Math.Min(lines.Length, startIndex + maxLines); i++)
        {
            if (lines[i].Contains("Exception:"))
            {
                return ExtractExceptionMessage(lines[i]);
            }
        }
        return null;
    }

    private string ExtractExceptionMessage(string line)
    {
        // Extract just the exception type and message, truncated
        var match = System.Text.RegularExpressions.Regex.Match(line, @"(System\.\w+Exception:[^.]+)");
        if (match.Success)
        {
            var msg = match.Groups[1].Value;
            return msg.Length > 150 ? msg.Substring(0, 150) + "..." : msg;
        }
        return line.Length > 150 ? line.Substring(0, 150) + "..." : line;
    }

    static class ReflectionHelper
    {
        internal static IEnumerable<IDictionary<string, object>> ProcessIEnumerable(IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                yield return GetObjectProperties(item);
            }
        }

        private static IDictionary<string, object> GetObjectProperties(object obj)
        {
            var properties = new Dictionary<string, object>();
            var type = obj.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    if (prop.Name is "Scopes" or "Symbols" or "ScopeType" or "Name" or "LanguageSpecifics" or "Type" or "SymbolType")
                    {
                        var value = prop.GetValue(obj);
                        if (value == null)
                        {
                            continue;
                        }

                        properties[prop.Name] = value;

                        if (prop.Name == "Scopes" && value is IEnumerable nestedScopes)
                        {
                            properties[prop.Name] = ProcessIEnumerable(nestedScopes).ToList();
                        }
                        else if (prop.Name == "Symbols" && value is IEnumerable symbols)
                        {
                            properties[prop.Name] = ProcessIEnumerable(symbols).ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    properties[prop.Name] = $"Error accessing property: {ex.Message}";
                }
            }

            return properties;
        }
    }
}
