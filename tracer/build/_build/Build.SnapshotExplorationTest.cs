using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Logger = Serilog.Log;

partial class Build
{
    const string SnapshotExplorationTestProbesFileName = "SnapshotExplorationTestProbes.csv";
    const string SnapshotExplorationTestReportFileName = "SnapshotExplorationTestReport.csv";
    const string SnapshotExplorationEnabledKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_ENABLED";
    const string SnapshotExplorationProbesPathKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_PROBES_PATH";
    const string SnapshotExplorationReportPathKey = "DD_INTERNAL_SNAPSHOT_EXPLORATION_TEST_REPORT_PATH";
    const char SpecialSeparator = '#';

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
            Test(testDescription, framework, envVariables);
            VerifySnapshotExplorationTestResults(envVariables[SnapshotExplorationProbesPathKey], envVariables[SnapshotExplorationReportPathKey]);
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
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("type name (FQN),method name,method signature,probeId,is instance method");
        var frameworks = Framework != null ? new[] { Framework } : testDescription.SupportedFrameworks;

        foreach (var framework in frameworks)
        {
            var testRootPath = testDescription.GetTestTargetPath(ExplorationTestsDirectory, framework, BuildConfiguration);
            var tracerAssemblyPath = GetTracerAssemblyPath(framework);
            var tracer = Assembly.LoadFile(tracerAssemblyPath);
            var extractorType = tracer.GetType("Datadog.Trace.Debugger.Symbols.SymbolExtractor");
            var createMethod = extractorType?.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            var getClassSymbols = extractorType?.GetMethod("GetClassSymbols", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
            var testAssembliesPaths = GetAllTestAssemblies(testRootPath);

            foreach (var testAssemblyPath in testAssembliesPaths)
            {
                var assembly = Assembly.LoadFile(testAssemblyPath);
                if (assembly.IsDynamic
                 || assembly.ManifestModule.IsResource()
                 || new[] { "mscorlib", "system", "microsoft", "nunit", "xunit", "datadog", "_build" }.Any(name => assembly.ManifestModule.Name.ToLower().StartsWith(name)))
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

            File.WriteAllText(Path.Combine(testRootPath, SnapshotExplorationTestProbesFileName), csvBuilder.ToString());
        }

        return;

        void ProcessNestedScopes(List<IDictionary<string, object>> scopes, string typeName, StringBuilder csvBuilder)
        {
            if (scopes == null)
            {
                return;
            }

            foreach (var scope in scopes)
            {
                if (scope["ScopeType"].ToString() == "Class")
                {
                    var nestedTypeName = scope["Name"].ToString();
                    ProcessNestedScopes((List<IDictionary<string, object>>)scope["Scopes"], nestedTypeName, csvBuilder);
                    continue;
                }

                if (scope["ScopeType"].ToString() == "Method") // todo: closure
                {
                    var isStatic = false;
                    var ls = scope["LanguageSpecifics"];
                    if (ls?.GetType().GetProperty("Annotaions")?.GetValue(scope["Annotations"]) is IList<string> annotations)
                    {
                        isStatic = (int.Parse(annotations[0], NumberStyles.HexNumber) & 0x0010) > 0;
                    }

                    var returnType = ls?.GetType().GetProperty("ReturnType", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ls)?.ToString();
                    if (TryGetLine(typeName, scope["Name"].ToString(), returnType, (List<IDictionary<string, object>>)scope["Symbols"], Guid.NewGuid(), isStatic, out var line))
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

    bool TryGetLine(string type, string method, string returnType, List<IDictionary<string, object>> methodParameters, Guid guid, bool isStatic, out string line)
    {
        try
        {
            var typeName = SanitiseName(type);
            var methodName = SanitiseName(method);
            var methodSignature = GetMethodSignature(returnType, methodParameters);
            line = $"{typeName},{methodName},{SanitiseName(methodSignature)},{Guid.NewGuid()},{isStatic}";
            return !string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(methodName) && !string.IsNullOrEmpty(returnType);
        }
        catch (Exception e)
        {
            line = $"Type: {type}, Method: {method}";
            return false;
        }

        string SanitiseName(string name) => name == null ? string.Empty : name.Replace(',', SpecialSeparator);

        string GetMethodSignature(string returnType, List<IDictionary<string, object>> symbols)
        {
            if (symbols == null)
            {
                return string.Empty;
            }

            var parameterTypes =
                (from symbol in symbols
                 where symbol["SymbolType"].ToString() == "Arg"
                 select symbol["Type"].ToString())
               .ToList();
            return $"{returnType} ({string.Join(SpecialSeparator, parameterTypes)})";
        }
    }

    public void VerifySnapshotExplorationTestResults(string probesPath, string reportPath)
    {
        var definedProbes = ReadDefinedProbes(probesPath);
        if (definedProbes == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read probes file");
        }

        var installedProbeIds = ReadInstalledProbeIdsFromNativeLogs();
        if (installedProbeIds == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read installed probes file");
        }

        var probesReport = ReadReportedSnapshotProbesIds(reportPath);
        if (probesReport == null || definedProbes.Count == 0)
        {
            throw new Exception("Snapshot exploration test failed. Could not read report file");
        }

        var invalidOrErrorProbes = probesReport.Where(p => !p.IsValid || p.HasError).ToList();
        var missingFromReport = installedProbeIds.Except(probesReport.ToDictionary(info => info.ProbeId, info => info.Name)).ToList();
        var notInstalled = definedProbes.Except(installedProbeIds).ToList();

        LogProbeCollection("Invalid or error probes", invalidOrErrorProbes.ToDictionary(info => info.ProbeId, info => info.Name).ToList());
        LogProbeCollection("Probes missing from report", missingFromReport);
        LogProbeCollection("Defined probes not installed", notInstalled);

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
                   .Where(parts => parts.Length >= 5)
                   .ToDictionary(
                        parts => parts[3].Trim(), // probe_id as key
                        parts => $"{parts[1].Trim()} {parts[2].Trim()}" // method_name + signature as value
                    );
    }

    List<ProbeReportInfo> ReadReportedSnapshotProbesIds(string reportPath)
    {
        if (string.IsNullOrEmpty(reportPath))
        {
            throw new ArgumentException("Report path cannot be null or empty", nameof(reportPath));
        }

        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException("The specified report file does not exist", reportPath);
        }

        return File.ReadLines(reportPath)
                   .Skip(1) // Skip the header row
                   .Select(ParseCsvLine)
                   .ToList();
    }

    ProbeReportInfo ParseCsvLine(string line)
    {
        var parts = line.Split(',');

        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
        {
            var probeId = parts.Length > 0 ? parts[0].Trim() : "missing id";
            var name = parts.Length > 1 ? parts[1].Trim() : "missing name";
            return new ProbeReportInfo(probeId, name, false, true);
        }

        var isValid = bool.TryParse(parts[2].Trim(), out var parsedIsValid) && parsedIsValid;
        return new ProbeReportInfo(parts[0].Trim(), parts[1].Trim(), isValid, false);
    }

    Dictionary<string, string> ReadInstalledProbeIdsFromNativeLogs()
    {
        return new Dictionary<string, string> { { "id", "method name" } };
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
