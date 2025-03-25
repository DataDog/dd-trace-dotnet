using System;
using System.Collections;
using System.Collections.Generic;
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
    const string SnapshotExplorationProbesFilePathKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_PROBES_FILE_PATH";
    const string SnapshotExplorationReportFolderPathKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_REPORT_FOLDER_PATH";
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
            FileSystemTasks.EnsureCleanDirectory(Path.Combine(testRootPath, SnapshotExplorationTestFolderName, framework, SnapshotExplorationTestReportFolderName));

            Test(testDescription, framework, envVariables);
            VerifySnapshotExplorationTestResults(envVariables[SnapshotExplorationProbesFilePathKey], envVariables[SnapshotExplorationReportFolderPathKey]);
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
            FileSystemTasks.EnsureCleanDirectory(Path.Combine(testRootPath, SnapshotExplorationTestFolderName, framework));
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

            File.WriteAllText(Path.Combine(testRootPath, SnapshotExplorationTestFolderName, framework, SnapshotExplorationTestProbesFileName), csvBuilder.ToString());
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
                    if (ls?.GetType().GetProperty("Annotaions")?.GetValue(scope["Annotations"]) is IList<string> annotations)
                    {
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

            var methodSignature = GetMethodSignature(returnType, methodParameters);
            if (string.IsNullOrEmpty(methodSignature))
            {
                return false;
            }

            line = $"{Guid.NewGuid()},{typeName},{methodName},{SanitiseName(methodSignature)},{isStatic}";
            return true;
        }
        catch (Exception)
        {
            line = $"Type: {type}, Method: {method}";
            return false;
        }

        string? SanitiseName(string? name) => name?.Replace(',', SpecialSeparator);

        string? GetMethodSignature(string? returnType, List<IDictionary<string, object>>? symbols)
        {
            if (returnType == null)
            {
                return null;
            }

            if (symbols == null)
            {
                return $"{returnType} ()";
            }

            var parameterTypes =
                (from symbol in symbols
                 where symbol["SymbolType"].ToString() == "Arg"
                 select symbol["Type"].ToString())
               .ToList();

            return $"{returnType} ({string.Join(SpecialSeparator, parameterTypes)})";
        }
    }

    public void VerifySnapshotExplorationTestResults(string probesFilePath, string reportFolderPath)
    {
        var definedProbes = ReadDefinedProbes(probesFilePath);
        if (definedProbes == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read probes file");
        }

        var installedProbeIds = ReadInstalledProbeIdsFromNativeLogs();
        if (installedProbeIds == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read installed probes file");
        }

        var probesReport = ReadReportedSnapshotProbesIds(reportFolderPath);
        if (probesReport == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read report file");
        }

        var invalidOrErrorProbes = probesReport.Where(p => !p.IsValid || p.HasError).ToList();
        var missingFromReport = installedProbeIds.Except(probesReport.ToDictionary(info => info.ProbeId, info => info.Name)).ToList();
        var notInstalled = definedProbes.Except(installedProbeIds).ToList();

        LogProbeCollection("Invalid or error probe", invalidOrErrorProbes.ToDictionary(info => info.ProbeId, info => info.Name).ToList());
        LogProbeCollection("Probe missing from report", missingFromReport);
        LogProbeCollection("Defined probe not installed", notInstalled);

        var successfullyCollectedCount = installedProbeIds.Intersect(definedProbes).Count();
        var successPercentage = (double)successfullyCollectedCount / definedProbes.Count * 100;

        Logger.Information($"Successfully collected {successPercentage:F2}% of probes.");

        if (invalidOrErrorProbes.Any() || missingFromReport.Any() /*do we want to fail in case of not installed probe?*/)
        {
            throw new Exception("Snapshot exploration test failed.");
        }
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
                        parts => $"{parts[1].Trim()}.{parts[2].Trim()}({parts[3]?.Trim()})" // type name + method name + signature as value
                    );
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

    private Dictionary<string, string> ReadInstalledProbeIdsFromNativeLogs()
    {
        var result = new Dictionary<string, string>();
        var logDirectory = BuildDataDirectory / "logs";

        if (!Directory.Exists(logDirectory))
        {
            throw new Exception($"Log folder does not exist in path: {logDirectory}");
        }

        var logFiles = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*.log");
        const string marker = "*** DebuggerMethodRewriter::Rewrite() Finished. ProbeID: ";

        foreach (var logFile in logFiles)
        {
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

    record ProbeReportInfo(string ProbeId, string Name, bool IsValid, bool HasError)
    {
        public string ProbeId { get; set; } = ProbeId;
        public string Name { get; set; } = Name;
        public bool IsValid { get; set; } = IsValid;
        public bool HasError { get; set; } = HasError;
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
