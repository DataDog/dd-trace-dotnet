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
using Nuke.Common.IO;
using Logger = Serilog.Log;
#nullable enable

partial class Build
{
    const string SnapshotExplorationTestFolderName = "SnapshotExplorationTestProbes";
    const string SnapshotExplorationTestProbesFileName = "SnapshotExplorationTestProbes.csv";
    const string SnapshotExplorationTestReportFolderName = "SnapshotExplorationTestReport";
    const string SnapshotExplorationEnabledKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_ENABLED";
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
                Logger.Information($"Skipping exploration test: {testDescription.Name}.");
                Logger.Warning($"The framework '{framework}' is not listed in the project's target frameworks of {testDescription.Name}");
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
            CreateSnapshotExplorationTestCsv(testDescription);
        }
        else
        {
            Logger.Information("Snapshot exploration test name is not provided, running all.");
            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                CreateSnapshotExplorationTestCsv(testDescription);
            }
        }
    }

    void CreateSnapshotExplorationTestCsv(ExplorationTestDescription testDescription)
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

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Probe ID,Type,Method,Signature,Is instance method");

            foreach (var testAssemblyPath in testAssembliesPaths)
            {
                var assembly = Assembly.LoadFile(testAssemblyPath);
                if (assembly.IsDynamic
                 || assembly.ManifestModule.IsResource()
                 || IgnoredNamespaces.Any(name => assembly.ManifestModule.Name.ToLower().StartsWith(name)))
                {
                    continue;
                }

                var symbolExtractor = createMethod?.Invoke(null, new object[] { assembly });
                if (getClassSymbols?.Invoke(symbolExtractor, null) is not IEnumerable classSymbols)
                {
                    continue;
                }

                var processedScopes = ReflectionHelper.ProcessIEnumerable(classSymbols);

                foreach (var scope in processedScopes)
                {
                    if (scope["ScopeType"].ToString() != "Class")
                    {
                        continue;
                    }

                    var typeName = scope["Name"].ToString();
                    ProcessNestedScopes((List<IDictionary<string, object>>)scope["Scopes"], typeName, csvBuilder);
                }
            }

            File.WriteAllText(GetSnapshotExplorationProbesFilePath(snapshotExplorationRootPath), csvBuilder.ToString());
        }

        return;

        void ProcessNestedScopes(List<IDictionary<string, object>>? scopes, string? typeName, StringBuilder csvBuilder)
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
                if (scope["ScopeType"].ToString() == "Closure")
                {
                    var closureName = scope["Name"].ToString();
                    if (!string.IsNullOrEmpty(closureName))
                    {
                        Logger.Debug($"Skipping closure: {closureName}");
                    }

                    continue;
                }

                if (scope["ScopeType"].ToString() == "Method")
                {
                    var isStatic = false;
                    var ls = scope["LanguageSpecifics"];
                    if (ls?.GetType().GetProperty("Annotations")?.GetValue(ls) is IList<string> annotations)
                    {
                        // Check for static flag (0x0010) in method attributes
                        isStatic = (int.Parse(annotations[0], NumberStyles.HexNumber) & 0x0010) > 0;
                    }

                    var methodName = scope["Name"].ToString();
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
                    if (TryGetLine(typeName, methodName, returnType, (List<IDictionary<string, object>>)scope["Symbols"], isStatic, out var line))
                    {
                        csvBuilder.AppendLine(line);
                    }
                    else
                    {
                        Logger.Warning($"Error to add probe info for: {line}");
                    }
                }
            }
        }
    }

    bool TryGetLine(string type, string method, string? returnType, List<IDictionary<string, object>>? methodParameters, bool isStatic, [NotNullWhen(true)] out string? line)
    {
        line = null;
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
            line = $"{Guid.NewGuid()},{typeName},{methodName},{methodSignature},{isInstanceMethod}";
            return true;
        }
        catch (Exception)
        {
            line = $"Type: {type}, Method: {method}";
            return false;
        }

        string? SanitiseName(string? name) => name?.Replace(',', SpecialSeparator);

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

    public void VerifySnapshotExplorationTestResults(string probesFilePath, string reportFolderPath, TimeSpan testDuration)
    {
        var analysisStopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, TimeSpan>();
        var fileSizes = new Dictionary<string, long>();
        var stepWatch = Stopwatch.StartNew();

        var definedProbes = ReadDefinedProbes(probesFilePath);
        var definedProbeRows = ReadDefinedProbeRows(probesFilePath);
        timings["ReadDefinedProbes"] = stepWatch.Elapsed;
        if (definedProbes == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read probes file");
        }

        stepWatch.Restart();
        var installedProbeIds = ReadInstalledProbeIdsFromNativeLogs(out var nativeLogBytes);
        timings["ReadNativeLogs(installed)"] = stepWatch.Elapsed;
        fileSizes["NativeLogs"] = nativeLogBytes;
        if (installedProbeIds == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read installed probes file");
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
        var probesReport = ReadReportedSnapshotProbesIds(reportFolderPath);
        timings["ReadProbesReport"] = stepWatch.Elapsed;
        if (probesReport == null || definedProbes.Count == 0)
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

        // Probes in CSV but native profiler couldn't install (signature mismatch, method doesn't exist, etc.)
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
        var neverJitCompiled = definedProbes.Count - installedCount - skippedProbes.Count - nativeProbeInstallFailures.Count;
        if (neverJitCompiled < 0) neverJitCompiled = 0; // Guard against overcounting in logs

        Logger.Information("┌─────────────────────────────────────────────────────────────┐");
        Logger.Information("│ PROBE FUNNEL (what happened at each stage)                  │");
        Logger.Information("├─────────────────────────────────────────────────────────────┤");
        Logger.Information($"│ 1. Probes defined in CSV             {definedProbes.Count,6}                   │");
        Logger.Information($"│    ├─ Never JIT-compiled (test didn't touch) {neverJitCompiled,6}          │");
        Logger.Information($"│    ├─ Skipped (signature mismatch)   {skippedProbes.Count,6}                   │");
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
            Logger.Warning("│ NATIVE PROFILER SKIPS (signature mismatches)                │");
            Logger.Warning("└─────────────────────────────────────────────────────────────┘");
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
                    LogReproProbeIdsWithCsvDetails(Logger.Warning, m.Probes, definedProbeRows, "        ");
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
                    LogReproProbeIdsWithCsvDetails(Logger.Warning, m.Probes, definedProbeRows, "        ");
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
                Logger.Warning($"  {actionItem++}. (Optional) Fix {skippedProbes.Count} signature mismatches in CSV generation");
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
                Logger.Warning($"  - {skippedProbes.Count} probes skipped due to signature mismatches");
            }
            if (trulyNotCalled.Any())
            {
                Logger.Warning($"  - {trulyNotCalled.Count} methods not exercised by tests");
            }
        }

        // === SECTION: Runtime Metrics ===
        var runtimeMetrics = ReadExplorationTestMetrics(reportFolderPath);
        if (runtimeMetrics.Count > 0)
        {
            Logger.Information("");
            Logger.Information("┌─────────────────────────────────────────────────────────────┐");
            Logger.Information("│ RUNTIME PERFORMANCE METRICS                                 │");
            Logger.Information("├─────────────────────────────────────────────────────────────┤");
            foreach (var metric in runtimeMetrics)
            {
                if (metric.Key == "CacheHitRate")
                {
                    Logger.Information($"│ {metric.Key,-25} {metric.Value.TotalMs,8:F1}%                     │");
                }
                else if (metric.Key is "CacheHits" or "CacheMisses" or "ProbesRemoved" or "SnapshotsSkipped" or "SnapshotTimeouts")
                {
                    Logger.Information($"│ {metric.Key,-25} {metric.Value.Count,8}                         │");
                }
                else
                {
                    Logger.Information($"│ {metric.Key,-25} {metric.Value.TotalMs,8:F0}ms ({metric.Value.Count} calls, avg {metric.Value.AvgMs:F2}ms) │");
                }
            }
            Logger.Information("└─────────────────────────────────────────────────────────────┘");
        }

        // Output paths for detailed investigation
        Logger.Information("");
        Logger.Information("Log locations for detailed investigation:");
        Logger.Information($"  Logs: {BuildDataDirectory / "logs"}");
        Logger.Information($"  Report folder: {reportFolderPath}");
    }

    record RuntimeMetric(double TotalMs, long Count, double AvgMs);

    private Dictionary<string, RuntimeMetric> ReadExplorationTestMetrics(string reportFolderPath)
    {
        var result = new Dictionary<string, RuntimeMetric>();
        var metricsFile = Path.Combine(reportFolderPath, "exploration_test_metrics.csv");

        if (!File.Exists(metricsFile))
        {
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(metricsFile);
            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(',');
                if (parts.Length >= 4)
                {
                    var name = parts[0];
                    var totalMs = double.TryParse(parts[1], out var t) ? t : 0;
                    var count = long.TryParse(parts[2], out var c) ? c : 0;
                    var avgMs = double.TryParse(parts[3], out var a) ? a : 0;
                    result[name] = new RuntimeMetric(totalMs, count, avgMs);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read metrics file: {ex.Message}");
        }

        return result;
    }

    void LogProbeCollection(string collectionName, List<KeyValuePair<string, string>> probes)
    {
        Logger.Error($"{collectionName} errors: {probes.Count}");
        return;
        foreach (var probe in probes)
        {
            Logger.Error($"{collectionName}: ID: {probe.Key}, Name: {probe.Value ?? string.Empty}");
        }
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

        return File.ReadLines(probesPath)
                   .Skip(1) // Skip the header row
                   .Select(line => line.Split(','))
                   .Where(parts => parts.Length == 5)
                   .ToDictionary(
                        parts => parts[0].Trim(), // Probe ID as key
                        parts => $"{parts[1].Trim()}.{parts[2].Trim()}" // type name + method name as value (signature in parts[3] used separately for matching)
                    );
    }

    private static Dictionary<string, string> ReadDefinedProbeRows(string probesPath)
    {
        // Returns a compact, single-line representation of the original probes CSV row for quick repro/debug.
        // Format: "<Type>.<Method>, <Signature>, IsInstance=<true/false>"
        return File.ReadLines(probesPath)
                   .Skip(1) // Skip the header row
                   .Select(line => line.Split(','))
                   .Where(parts => parts.Length == 5)
                   .ToDictionary(
                        parts => parts[0].Trim(),
                        parts =>
                        {
                            var type = parts[1].Trim();
                            var method = parts[2].Trim();
                            var signature = parts[3].Trim();
                            var isInstance = parts[4].Trim();
                            return $"{type}.{method}, {signature}, IsInstance={isInstance}";
                        });
    }

    private static string FormatReproProbeIdsWithCsvDetails(List<string?> probeIds, Dictionary<string, string> definedProbeRows)
    {
        if (probeIds == null || probeIds.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append(" (repro:");
        var wroteAny = false;

        foreach (var id in probeIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (wroteAny)
            {
                sb.Append(" |");
            }

            wroteAny = true;

            if (definedProbeRows.TryGetValue(id, out var row))
            {
                sb.Append($" {id} → {row}");
            }
            else
            {
                sb.Append($" {id}");
            }
        }

        sb.Append(')');
        return wroteAny ? sb.ToString() : string.Empty;
    }

    private static void LogReproProbeIdsWithCsvDetails(Action<string> log, List<string?> probeIds, Dictionary<string, string> definedProbeRows, string indent)
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

            if (definedProbeRows.TryGetValue(id, out var row))
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

    List<ProbeReportInfo> ReadReportedSnapshotProbesIds(string reportFolderPath)
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

        foreach (var file in Directory.EnumerateFiles(reportFolderPath))
        {
            // Skip the metrics file - it has a different format
            if (Path.GetFileName(file) == "exploration_test_metrics.csv")
            {
                continue;
            }

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
        var parts = line.Split(',');

        if (parts.Length != 4 || parts.Any(string.IsNullOrWhiteSpace))
        {
            var probeId = parts.Length > 0 ? parts[0].Trim() : "missing id";
            var type = parts.Length > 1 ? parts[1].Trim() : "missing type";
            var method = parts.Length > 1 ? parts[2].Trim() : "missing method";
            return new ProbeReportInfo(probeId, type + "." + method, false, true);
        }

        var isValid = bool.TryParse(parts[3].Trim(), out var parsedIsValid) && parsedIsValid;
        return new ProbeReportInfo(parts[0].Trim(), parts[1].Trim() + "." + parts[2].Trim(), isValid, false);
    }

    /// <summary>
    /// Checks if log files might have been rolled during test execution.
    /// Returns a warning message if multiple log files per process are detected, or null if logs appear complete.
    /// </summary>
    private string CheckForLogRolling()
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

        // Check for rolled logs by looking for multiple files with same PID pattern or numbered suffixes
        // Rolled files use pattern: dotnet-tracer-managed-{process}-{pid}_{sequence}.log
        var rolledNative = nativeLogs.Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"_\d+\.log$")).ToList();
        var rolledManaged = managedLogs.Where(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(f), @"_\d+\.log$")).ToList();

        if (rolledNative.Count > 1 || rolledManaged.Count > 1)
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

    /// <summary>
    /// Reads probe IDs that were successfully installed (instrumented) by the native profiler.
    ///
    /// COUPLED SOURCE FILE (update if log format changes):
    /// - tracer/src/Datadog.Tracer.Native/debugger_method_rewriter.cpp
    ///   - "*** DebuggerMethodRewriter::Rewrite() Finished. ProbeID: ... Method: ..."
    /// </summary>
    private Dictionary<string, string> ReadInstalledProbeIdsFromNativeLogs(out long totalBytesRead)
    {
        var result = new Dictionary<string, string>();
        var logDirectory = BuildDataDirectory / "logs";
        totalBytesRead = 0;

        if (!Directory.Exists(logDirectory))
        {
            throw new Exception($"Log folder does not exist in path: {logDirectory}");
        }

        var logFiles = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*.log");
        const string marker = "*** DebuggerMethodRewriter::Rewrite() Finished. ProbeID: ";

        foreach (var logFile in logFiles)
        {
            totalBytesRead += new FileInfo(logFile).Length;
            var logLines = File.ReadAllLines(logFile);

            foreach (var line in logLines)
            {
                if (!line.Contains(marker))
                {
                    continue;
                }

                var probeIdStart = line.IndexOf(marker) + marker.Length;
                var probeIdEnd = line.IndexOf(" Method: ", probeIdStart);
                if (probeIdEnd == -1) continue;

                var probeId = line.Substring(probeIdStart, probeIdEnd - probeIdStart).Trim();
                if (probeId == "null" || string.IsNullOrEmpty(probeId))
                {
                    continue;
                }

                var methodStart = probeIdEnd + " Method: ".Length;
                var methodEnd = line.IndexOf("() [IsVoid=", methodStart);
                if (methodEnd == -1) continue;

                var methodName = line.Substring(methodStart, methodEnd - methodStart).Trim();

                result.TryAdd(probeId, methodName);
            }
        }

        return result;
    }

    /// <summary>
    /// Reads probes that were skipped by the native profiler with their skip reasons.
    /// Format in logs: "* Skipping MethodName: reason"
    ///
    /// COUPLED SOURCE FILES (update if log format changes):
    /// - tracer/src/Datadog.Tracer.Native/debugger_rejit_preprocessor.cpp
    ///   - "* Skipping ... doesn't have the right number of arguments"
    ///   - "* Skipping ... doesn't have the right type of arguments"
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
        const string finishedMarker = "*** DebuggerMethodRewriter::Rewrite() Finished. ProbeID: ";

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

            // We can correlate "ByRefLike" skip lines to a method name by using the native log prefix "[pid|tid]".
            // Those skip lines don't include the method/probe id, but the subsequent "Rewrite() Finished..." line does.
            // We buffer pending errors per thread and assign them to the next finished rewrite on that thread.
            var pendingByThread = new Dictionary<string, List<(string ErrorType, string RawLog)>>();

            for (int i = 0; i < logLines.Length; i++)
            {
                var line = logLines[i];

                // Update correlation context when we see the "Finished" marker
                if (line.Contains(finishedMarker))
                {
                    var threadKey = TryExtractNativeThreadKey(line);
                    var methodName = TryExtractMethodNameFromFinishedLine(line);
                    var probeId = TryExtractProbeIdFromFinishedLine(line);
                    if (threadKey != null && methodName != null &&
                        pendingByThread.TryGetValue(threadKey, out var pending) &&
                        pending.Count > 0)
                    {
                        foreach (var p in pending)
                        {
                            result.Add(new NativeRewriterError(methodName, p.ErrorType, p.RawLog, probeId));
                        }

                        pending.Clear();
                    }
                }

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

                        // If we can't extract method name from this line, buffer it for correlation by thread.
                        if (methodName == "unknown")
                        {
                            var threadKey = TryExtractNativeThreadKey(line);
                            if (threadKey != null)
                            {
                                if (!pendingByThread.TryGetValue(threadKey, out var pending))
                                {
                                    pending = new List<(string ErrorType, string RawLog)>();
                                    pendingByThread[threadKey] = pending;
                                }

                                pending.Add((pattern.Value, raw));
                                break; // Don't double-count
                            }
                        }

                        result.Add(new NativeRewriterError(methodName, pattern.Value, raw, ProbeId: null));
                        break; // Don't double-count
                    }
                }
            }
        }

        return result;
    }

    private string? TryExtractNativeThreadKey(string line)
    {
        // Native logs include a prefix like: "... [23424|8916] [warning] ..."
        var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d+)\|(\d+)\]");
        return match.Success ? $"{match.Groups[1].Value}|{match.Groups[2].Value}" : null;
    }

    private string? TryExtractMethodNameFromFinishedLine(string line)
    {
        // Example: "*** DebuggerMethodRewriter::Rewrite() Finished. ProbeID: <id> Method: <Type>.<Method>() ..."
        var match = System.Text.RegularExpressions.Regex.Match(line, @"Method:\s+([^\[]+)$");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        match = System.Text.RegularExpressions.Regex.Match(line, @"Method:\s+(.+?)\s+\[");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? TryExtractProbeIdFromFinishedLine(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line, @"ProbeID:\s*([a-f0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
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
    /// - tracer/src/Datadog.Trace/Debugger/Expressions/ProbeExpressionEvaluator.cs
    ///   - "Failed to parse probe expression" (line ~510)
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

                // Look for "Failed to parse probe expression" - explicit probe error
                if (line.Contains("Failed to parse probe expression"))
                {
                    var probeId = ExtractProbeIdFromContext(lines, i);
                    var exceptionMatch = FindExceptionInFollowingLines(lines, i, 10);
                    var errorType = exceptionMatch != null ? ExtractExceptionType(exceptionMatch) : "ExpressionParseError";
                    var key = $"{probeId}|{errorType}|{exceptionMatch}";
                    if (seenErrors.Add(key))
                    {
                        result.Add(new ManagedLogError(probeId, errorType, exceptionMatch ?? "Unknown expression error", ""));
                    }
                }

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
            if (obj == null)
            {
                return null;
            }

            var properties = new Dictionary<string, object>();
            var type = obj.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try
                {
                    if (prop.Name is "Scopes" or "Symbols" or "ScopeType" or "Name" or "LanguageSpecifics" or "Type" or "SymbolType")
                    {
                        var value = prop.GetValue(obj);
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
