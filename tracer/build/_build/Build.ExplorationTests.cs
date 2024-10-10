using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Target = Nuke.Common.Target;
using Logger = Serilog.Log;

partial class Build
{
    AbsolutePath ExplorationTestsDirectory => RootDirectory / "exploration-tests";

    [Parameter("Indicates use case of exploration test to run.")]
    readonly ExplorationTestUseCase? ExplorationTestUseCase;

    [Parameter("Indicates name of exploration test to run. If not specified, will run all tests sequentially.")]
    readonly ExplorationTestName? ExplorationTestName;

    [Parameter("Indicates if the Fault-Tolerant Instrumentation should be turned on.")]
    readonly bool EnableFaultTolerantInstrumentation;

    [Parameter("Indicates if the Dynamic Instrumentation product should be disabled.")]
    readonly bool DisableDynamicInstrumentationProduct;

    [Parameter("Indicates whether exploration tests should run on latest repository commit. Useful if you want to update tested repositories to the latest tags. Default false.",
               List = false)]
    readonly bool ExplorationTestCloneLatest;

    const string LineProbesFileName = "exploration_test_line_probes";

    Target SetUpExplorationTests
        => _ => _
               .Description("Setup exploration tests.")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome)
               .Executes(() =>
                {
                    GitCloneBuild();
                    SetUpExplorationTest();
                });

    void SetUpExplorationTest()
    {
        switch (ExplorationTestUseCase)
        {
            case global::ExplorationTestUseCase.Debugger:
                SetUpExplorationTest_Debugger();
                break;
            case global::ExplorationTestUseCase.ContinuousProfiler:
                SetUpExplorationTest_ContinuousProfiler();
                break;
            case global::ExplorationTestUseCase.Tracer:
                SetUpExplorationTest_Tracer();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ExplorationTestUseCase), ExplorationTestUseCase, null);
        }
    }

    void SetUpExplorationTest_Debugger()
    {
        Logger.Information($"Set up exploration test for debugger.");
        CreateLineProbesIfNeeded();
    }

    void SetUpExplorationTest_ContinuousProfiler()
    {
        Logger.Information($"Prepare environment variables for continuous profiler.");
        //TODO TBD
    }

    void SetUpExplorationTest_Tracer()
    {
        Logger.Information($"Prepare environment variables for tracer.");
        //TODO TBD
    }

    void GitCloneBuild()
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Information($"Provided exploration test name is {ExplorationTestName}.");

            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            GitCloneAndBuild(testDescription);
        }
        else
        {
            Logger.Information($"Exploration test name is not provided. Running all of them.");

            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                GitCloneAndBuild(testDescription);
            }
        }
    }

    void GitCloneAndBuild(ExplorationTestDescription testDescription)
    {
        if (Framework != null && !testDescription.IsFrameworkSupported(Framework))
        {
            throw new InvalidOperationException($"The framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
        }

        var depth = testDescription.IsGitShallowCloneSupported ? "--depth 1" : "";
        var submodules = testDescription.IsGitSubmodulesRequired ? "--recurse-submodules" : "";
        var source = ExplorationTestCloneLatest ? testDescription.GitRepositoryUrl : $"-b {testDescription.GitRepositoryTag} {testDescription.GitRepositoryUrl}";
        var target = $"{ExplorationTestsDirectory}/{testDescription.Name}";

        FileSystemTasks.EnsureCleanDirectory(target);

        var cloneCommand = $"clone -q -c advice.detachedHead=false {depth} {submodules} {source} {target}";
        GitTasks.Git(cloneCommand);

        var projectPath = $"{ExplorationTestsDirectory}/{testDescription.Name}/{testDescription.PathToUnitTestProject}";
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Test path '{projectPath}' doesn't exist.");
        }

        DotNetBuild(
            x => x
                .SetProjectFile(projectPath)
                .SetConfiguration(BuildConfiguration)
                .SetProcessArgumentConfigurator(arguments => arguments
                                                            .Add("-consoleLoggerParameters:ErrorsOnly")
                                                            .Add("-property:NuGetAudit=false"))
                .When(Framework != null, settings => settings.SetFramework(Framework))
        );
    }

    Target RunExplorationTests
        => _ => _
               .Description("Run exploration tests.")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome, BuildNativeLoader, SetUpExplorationTests)
               .DependsOn(CleanTestLogs)
               .Executes(() =>
                {
                    try
                    {
                        RunExplorationTestsGitUnitTest();
                        RunExplorationTestAssertions();
                    }
                    finally
                    {
                        CopyDumpsToBuildData();
                    }
                })
        ;

    Target SetUpSnapshotExplorationTests
        => _ => _
               .Description("Sets up the Snapshot Exploration Test")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome)
               .Executes(() =>
                {
                    if (ExplorationTestUseCase != global::ExplorationTestUseCase.Debugger)
                    {
                        return;
                    }

                    GitCloneBuild();
                    SetUpSnapshotExplorationTestsInternal();
                });

    Target RunSnapshotExplorationTests
        => _ => _
               .Description("Runs the Snapshot Exploration Test")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome, BuildNativeLoader, SetUpSnapshotExplorationTests)
               .Executes(() =>
                {
                    if (ExplorationTestUseCase != global::ExplorationTestUseCase.Debugger)
                    {
                        return;
                    }

                    FileSystemTasks.EnsureCleanDirectory(TestLogsDirectory);
                    try
                    {
                        RunSnapshotExplorationTestsInternal();
                    }
                    finally
                    {
                        CopyDumpsToBuildData();
                    }
                });

    Dictionary<string, string> GetEnvironmentVariables(ExplorationTestDescription testDescription, TargetFramework framework)
    {
        var envVariables = new Dictionary<string, string>
        {
            ["DD_TRACE_LOG_DIRECTORY"] = TestLogsDirectory,
            ["DD_SERVICE"] = "exploration_tests",
            ["DD_VERSION"] = Version,
            // Disable logs injection for exploration tests to avoid interfering with third-party test expectations
            ["DD_LOGS_INJECTION"] = "false"
        };

        switch (ExplorationTestUseCase)
        {
            case global::ExplorationTestUseCase.Debugger:
                AddDebuggerEnvironmentVariables(envVariables, testDescription, framework);
                break;
            case global::ExplorationTestUseCase.ContinuousProfiler:
                AddContinuousProfilerEnvironmentVariables(envVariables);
                break;
            case global::ExplorationTestUseCase.Tracer:
                AddTracerEnvironmentVariables(envVariables);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ExplorationTestUseCase), ExplorationTestUseCase, null);
        }

        if (testDescription.EnvironmentVariables != null)
        {
            foreach (var (key, value) in testDescription.EnvironmentVariables)
            {
                // Use TryAdd to avoid overriding the environment variables set by the caller
                envVariables.TryAdd(key, value);
            }
        }

        return envVariables;
    }

    void RunExplorationTestsGitUnitTest()
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Information($"Provided exploration test name is {ExplorationTestName}.");

            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            RunUnitTest(testDescription);
        }
        else
        {
            Logger.Information($"Exploration test name is not provided. Running all.");

            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                RunUnitTest(testDescription);
            }
        }
    }

    void RunUnitTest(ExplorationTestDescription testDescription)
    {
        if (!testDescription.ShouldRun)
        {
            Logger.Information($"Skipping the exploration test {testDescription.Name}.");
            return;
        }

        Logger.Information($"Running exploration test {testDescription.Name}.");

        if (Framework == null)
        {
            foreach (var targetFramework in testDescription.SupportedFrameworks)
            {
                var envVariables = GetEnvironmentVariables(testDescription, targetFramework);
                Test(testDescription, targetFramework, envVariables);
            }
        }
        else
        {
            if (!testDescription.IsFrameworkSupported(Framework))
            {
                throw new InvalidOperationException($"The framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
            }

            var envVariables = GetEnvironmentVariables(testDescription, Framework);
            Test(testDescription, Framework, envVariables);
        }
    }

    void Test(ExplorationTestDescription testDescription, TargetFramework targetFramework, Dictionary<string, string> envVariables)
    {
        DotNetTest(
            x =>
            {
                x = x
				   .SetDotnetPath(TargetPlatform)
                   .SetProjectFile(testDescription.GetTestTargetPath(ExplorationTestsDirectory, targetFramework, BuildConfiguration))
                   .EnableNoRestore()
                   .EnableNoBuild()
                   .SetConfiguration(BuildConfiguration)
                   .SetFramework(targetFramework)
                   .SetProcessEnvironmentVariables(envVariables)
                   .SetIgnoreFilter(testDescription.TestsToIgnore)
                   .WithMemoryDumpAfter(100)
                    ;

                return x;
            });
    }

    private void CreateLineProbesIfNeeded()
    {
        ExplorationTestDescription testDescription = null;
        if (ExplorationTestName.HasValue)
        {
            testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            if (!testDescription.LineProbesEnabled)
            {
                Logger.Information($"Provided exploration test name is {ExplorationTestName}.");
                return;
            }
        }
        else
        {
            testDescription = ExplorationTestDescription.GetExplorationTestDescription(global::ExplorationTestName.protobuf);
        }

        Logger.Information($"Provided exploration test name is {testDescription.Name}.");

        ExplorationTestDescription.GetAllExplorationTestDescriptions();

        var sw = new Stopwatch();
        sw.Start();

        CreateLineProbesFile(testDescription);

        sw.Stop();
        Logger.Information("Creating line probes file finished. Took ");
        Logger.Information(sw.Elapsed.Minutes > 0 ? $"{sw.Elapsed.Minutes:D2} minutes and {sw.Elapsed.Seconds:D2} seconds." : $"{sw.Elapsed.Seconds:D2} seconds.");

        return;

        static void UpdateProgressBar(double processedWeight, double totalWeight, int totalFiles)
        {
            double progress = processedWeight / totalWeight;
            int progressBarWidth = 50;
            int filledWidth = (int)(progress * progressBarWidth);

            int processedFiles = (int)(progress * totalFiles); //estimated

            Console.Write("\r[");
            Console.Write(new string('#', filledWidth));
            Console.Write(new string('-', progressBarWidth - filledWidth));
            Console.Write($"] {progress:P1} | Est. Files: {processedFiles}/{totalFiles}");
        }

        void CreateLineProbesFile(ExplorationTestDescription testDescription)
        {
            Logger.Information($"Creating line probes file for {testDescription.Name}.");

            var frameworks = Framework != null ? new[] { Framework } : testDescription.SupportedFrameworks;
            var allCsFiles = Directory.EnumerateFiles($"{ExplorationTestsDirectory}{Path.DirectorySeparatorChar}{testDescription.Name}", "*.cs", SearchOption.AllDirectories).
                                       Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).ToArray();

            foreach (var framework in frameworks)
            {
                var testRootPath = testDescription.GetTestTargetPath(ExplorationTestsDirectory, framework, BuildConfiguration);
                var tracerAssemblyPath = GetTracerAssemblyPath(framework);
                var tracer = Assembly.LoadFile(tracerAssemblyPath);
                var extractorType = tracer.GetType("Datadog.Trace.Debugger.Symbols.SymbolExtractor");
                var createMethod = extractorType?.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
                var testAssembliesPaths = GetAllTestAssemblies(testRootPath);
                if (testAssembliesPaths.Length == 0)
                {
                    throw new Exception($"Can't find any test assembly for {ExplorationTestsDirectory}/{framework}");
                }

                var metadataReaders = new List<Tuple<object, MethodInfo, string>>();
                foreach (var testAssemblyPath in testAssembliesPaths)
                {
                    if (!TryLoadAssembly(testAssemblyPath, out var currentAssembly))
                    {
                        continue;
                    }

                    var symbolExtractor = createMethod?.Invoke(null, new object[] { currentAssembly });
                    if (symbolExtractor == null)
                    {
                        throw new Exception($"Could not get SymbolExtractor instance for assembly: {testAssemblyPath}");
                    }

                    var metadataReader = symbolExtractor.GetType().GetProperty("DatadogMetadataReader", BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(symbolExtractor);
                    var getMethodAndOffsetMethod = metadataReader?.GetType().GetMethod("GetContainingMethodTokenAndOffset", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (getMethodAndOffsetMethod == null)
                    {
                        throw new Exception("Could not find GetContainingMethodTokenAndOffset");
                    }

                    var isPdbExist = (bool?)metadataReader.GetType().GetProperty("IsPdbExist", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(metadataReader);
                    if (!isPdbExist.HasValue || !isPdbExist.Value)
                    {
                        Logger.Debug($"Skipping assembly {testAssemblyPath} because there is no PDB info");
                        continue;
                    }

                    metadataReaders.Add(Tuple.Create(metadataReader, getMethodAndOffsetMethod, currentAssembly.ManifestModule.ModuleVersionId.ToString()));
                }

                var lineProbes = new List<string>();
                var locker = new object();
                HashSet<string> noMatchingPdbAssembly = new HashSet<string>();

                var fileWeights = allCsFiles.ToDictionary(
                    file => file,
                    file => (double)File.ReadLines(file).Count()
                );
                double totalWeight = fileWeights.Values.Sum();
                double processedWeight = 0;

                foreach (var csFile in allCsFiles)
                {
                    bool fileProcessed = false;
                    foreach (var metadataReader in metadataReaders)
                    {
                        var pdbPath = (string)metadataReader?.Item1?.GetType().GetProperty("PdbFullPath", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(metadataReader.Item1);
                        if (string.IsNullOrEmpty(pdbPath))
                        {
                            continue;
                        }

                        var assemblyName = Path.GetFileNameWithoutExtension(pdbPath);
                        if (string.IsNullOrEmpty(assemblyName))
                        {
                            continue;
                        }

                        if (!csFile.Contains(assemblyName!))
                        {
                            // if the metadata reader isn't the correct one, continue.
                            // it's not a bulletproof but probably enough for exploration test
                            if (noMatchingPdbAssembly.Add(assemblyName))
                            {
                                Logger.Debug($"Skipping assembly {assemblyName} because there is no matching PDB info");
                            }
                            continue;
                        }

                        fileProcessed = true;
                        int numberOfLines = (int)fileWeights[csFile];
                        Parallel.For(0, numberOfLines, () => new List<string>(), (i, state, localList) =>
                        {
                            int? byteCodeOffset = null;
                            // ReSharper disable once ExpressionIsAlwaysNull
                            var args = new object[] { csFile, i, /*column*/ null, byteCodeOffset };
                            var method = metadataReader.Item2.Invoke(metadataReader.Item1, args);

                            // i.e. we got a method and bytecode offset
                            if (args[3] != null && method != null)
                            {
                                localList.Add($"{metadataReader.Item3},{(int)method},{(int)args[3]}");
                            }

                            return localList;
                        }, localList =>
                        {
                            lock (locker)
                            {
                                lineProbes.AddRange(localList);
                            }
                        });
                    }

                    if (fileProcessed)
                    {
                        processedWeight += fileWeights[csFile];
                        UpdateProgressBar(processedWeight, totalWeight, allCsFiles.Count());
                    }
                }

                if (lineProbes.Count > 0)
                {
                    File.WriteAllText(Path.Combine(testRootPath, LineProbesFileName),
                                      string.Join(Environment.NewLine, lineProbes));
                    lineProbes.Clear();
                }

                Console.WriteLine();
            }
        }
    }

    static bool TryLoadAssembly(string testAssemblyPath, [NotNullWhen(true)] out Assembly assembly)
    {
        try
        {
            assembly = Assembly.LoadFile(testAssemblyPath);
            return true;
        }
        catch (BadImageFormatException)
        {
            // ignore
        }
        catch (Exception e)
        {
            Logger.Warning(e, $"Fail to load assembly: {testAssemblyPath}");
        }

        assembly = null;
        return false;
    }

    string GetTracerAssemblyPath(TargetFramework framework)
    {
        TargetFramework tracerFramework = null;
        if (framework.IsGreaterThanOrEqualTo(TargetFramework.NET6_0))
        {
            tracerFramework = TargetFramework.NET6_0;
        }

        else if (framework.IsGreaterThanOrEqualTo(TargetFramework.NETCOREAPP3_1))
        {
            tracerFramework = TargetFramework.NETCOREAPP3_1;
        }

        else if (framework == TargetFramework.NETSTANDARD2_0 ||
                 framework == TargetFramework.NETCOREAPP2_1 ||
                 framework == TargetFramework.NETCOREAPP3_0)
        {
            tracerFramework = TargetFramework.NETSTANDARD2_0;
        }

        else if (framework.ToString().StartsWith("net4"))
        {
            tracerFramework = TargetFramework.NET461;
        }

        if (tracerFramework == null)
        {
            throw new Exception("Can't determined the correct tracer framework version");
        }

        return MonitoringHomeDirectory / tracerFramework / "Datadog.Trace.dll";
    }

    static string[] GetAllTestAssemblies(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => !Exclude(f) && IsSupportedExtension(f)).ToArray();

        bool Exclude(string path)
        {
            // skip obj folder and the `testhost`process itself
            return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || Path.GetFileNameWithoutExtension(path).Equals("testhost");
        }

        bool IsSupportedExtension(string path)
        {
            var extensions = new[] { ".dll", ".exe", ".so" };
            return extensions.Any(ext => string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase));
        }
    }

    void RunExplorationTestAssertions()
    {
        switch (ExplorationTestUseCase)
        {
            case global::ExplorationTestUseCase.Debugger:
                RunExplorationTestAssertions_Debugger();
                break;
            case global::ExplorationTestUseCase.ContinuousProfiler:
                RunExplorationTestAssertions_ContinuousProfiler();
                break;
            case global::ExplorationTestUseCase.Tracer:
                RunExplorationTestAssertions_Tracer();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ExplorationTestUseCase), ExplorationTestUseCase, null);
        }
    }

    void RunExplorationTestAssertions_Debugger()
    {
        Logger.Information($"Running assertions tests for debugger.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_ContinuousProfiler()
    {
        Logger.Information($"Running assertions tests for profiler.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_Tracer()
    {
        Logger.Information($"Running assertions tests for tracer.");
        //TODO TBD
    }
}


public enum ExplorationTestUseCase
{
    Debugger, ContinuousProfiler, Tracer
}

public enum ExplorationTestName
{
    eShopOnWeb, protobuf, cake, swashbuckle, RestSharp, serilog, polly, // FIXME: .NET 10 issue with automapper automapper, // paket, FIXME: .NET 9 - Paket doesn't support .NET 9 yet
}

class ExplorationTestDescription
{
    public ExplorationTestName Name { get; set; }

    public string GitRepositoryUrl { get; set; }
    public string GitRepositoryTag { get; set; }
    public bool IsGitShallowCloneSupported { get; set; }
    public bool IsGitSubmodulesRequired { get; set; }

    public string PathToUnitTestProject { get; set; }
    public bool IsTestedByVSTest { get; set; }
    public string[] TestsToIgnore { get; set; }

    public bool ShouldRun { get; set; } = true;
    public bool LineProbesEnabled { get; set; }
    public bool IsSnapshotScenario { get; set; }
    public string GetTestTargetPath(AbsolutePath explorationTestsDirectory, TargetFramework framework, Configuration buildConfiguration)
    {
        var projectPath = $"{explorationTestsDirectory}/{Name}/{PathToUnitTestProject}";

        if (!IsTestedByVSTest)
        {
            return projectPath;
        }
        else
        {
            var frameworkFolder = framework ?? "*";
            var projectName = Path.GetFileName(projectPath);

            return $"{projectPath}/bin/{buildConfiguration}/{frameworkFolder}/{projectName}.exe";
        }
    }

    public TargetFramework[] SupportedFrameworks { get; set; }
    public OSPlatform[] SupportedOSPlatforms { get; set; }
    public (string key, string value)[] EnvironmentVariables { get; set; }

    public bool IsFrameworkSupported(TargetFramework targetFramework)
    {
        return SupportedFrameworks.Any(framework => framework.Equals(targetFramework));
    }

    public static ExplorationTestDescription[] GetAllExplorationTestDescriptions()
    {
        return Enum.GetValues<ExplorationTestName>()
                   .Select(GetExplorationTestDescription)
                   .ToArray()
            ;
    }

    public static ExplorationTestDescription GetExplorationTestDescription(ExplorationTestName name)
    {
        var description = name switch
        {
            ExplorationTestName.eShopOnWeb => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.eShopOnWeb,
                GitRepositoryUrl = "https://github.com/dotnet-architecture/eShopOnWeb.git",
                GitRepositoryTag = "main",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "tests/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NET8_0 }
            },
            ExplorationTestName.protobuf => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.protobuf,
                GitRepositoryUrl = "https://github.com/protocolbuffers/protobuf.git",
                GitRepositoryTag = "v3.23.0", // min version targeting net6.0 in tests
                IsGitShallowCloneSupported = true,
                IsGitSubmodulesRequired = true,
                PathToUnitTestProject = "csharp/src/Google.Protobuf.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
                TestsToIgnore = new string[]
                {
                    "Google.Protobuf.CodedInputStreamTest.MaliciousRecursion",
                    "Google.Protobuf.CodedInputStreamTest.MaliciousRecursion_UnknownFields",
                    "Google.Protobuf.CodedInputStreamTest.RecursionLimitAppliedWhileSkippingGroup",
                    "Google.Protobuf.JsonParserTest.MaliciousRecursion",
                    // exclude those "legacy" tests because they are on manually modified code
                    // that throws a NotImplementedException on the `Descriptor` property that we use.
                    "Google.Protobuf.LegacyGeneratedCodeTest.IntermixingOfNewAndLegacyGeneratedCodeWorksWithCodedInputStream",
                    "Google.Protobuf.LegacyGeneratedCodeTest.IntermixingOfNewAndLegacyGeneratedCodeWorksWithCodedOutputStream",
                    "Google.Protobuf.LegacyGeneratedCodeTest.LegacyGeneratedCodeThrowsWithIBufferWriter",
                    "Google.Protobuf.LegacyGeneratedCodeTest.LegacyGeneratedCodeThrowsWithReadOnlySequence"
                },
                LineProbesEnabled = true
            },
            ExplorationTestName.cake => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.cake,
                GitRepositoryUrl = "https://github.com/cake-build/cake.git",
                GitRepositoryTag = "v1.3.0",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "src/Cake.Common.Tests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0 },
                // Workaround for https://github.com/dotnet/runtime/issues/95653
                EnvironmentVariables = new[] { ("DD_CLR_ENABLE_INLINING", "0") },
            },
            ExplorationTestName.swashbuckle => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.swashbuckle,
                GitRepositoryUrl = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore.git",
                GitRepositoryTag = "v6.2.3",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "test/Swashbuckle.AspNetCore.SwaggerGen.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
                // Workaround for https://github.com/dotnet/runtime/issues/95653
                EnvironmentVariables = new[] { ("DD_CLR_ENABLE_INLINING", "0") },
            },
            // FIXME: .NET 9 - Paket doesn't support .NET 9 yet
            // ExplorationTestName.paket => new ExplorationTestDescription()
            // {
            //     Name = ExplorationTestName.paket,
            //     GitRepositoryUrl = "https://github.com/fsprojects/Paket.git",
            //     GitRepositoryTag = "6.2.1",
            //     IsGitShallowCloneSupported = true,
            //     PathToUnitTestProject = "tests/Paket.Tests",
            //     TestsToIgnore = new[] { "Loading assembly metadata works", "task priorization works" /* fails on timing */, "should normalize home path", "should parse config with home path in cache" },
            //     SupportedFrameworks = new[] { TargetFramework.NET461 },
            //     ShouldRun = false // Dictates that this exploration test should not take part in the CI
            // },
            ExplorationTestName.RestSharp => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.RestSharp,
                GitRepositoryUrl = "https://github.com/restsharp/RestSharp.git",
                GitRepositoryTag = "107.0.3",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "test/RestSharp.Tests",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
                // Workaround for https://github.com/dotnet/runtime/issues/95653
                EnvironmentVariables = new[] { ("DD_CLR_ENABLE_INLINING", "0") },
            },
            ExplorationTestName.serilog => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.serilog,
                GitRepositoryUrl = "https://github.com/serilog/serilog.git",
                GitRepositoryTag = "v2.10.0",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "test/Serilog.Tests",
                TestsToIgnore = new[] { "DisconnectRemoteObjectsAfterCrossDomainCallsOnDispose" },
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1 },
            },
            ExplorationTestName.polly => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.polly,
                GitRepositoryUrl = "https://github.com/app-vnext/polly.git",
                GitRepositoryTag = "7.2.2+9",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "src/Polly.Specs",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET461 },
            },
            // FIXME: currently fails to compile due to .NET 10 changes with latest C# lang version (14)
            //ExplorationTestName.automapper => new ExplorationTestDescription()
            //{
            //    Name = ExplorationTestName.automapper,
            //    GitRepositoryUrl = "https://github.com/automapper/automapper.git",
            //    GitRepositoryTag = "v11.0.0",
            //    IsGitShallowCloneSupported = true,
            //    PathToUnitTestProject = "src/UnitTests",
            //    SupportedFrameworks = new[] { TargetFramework.NET6_0 },
            //    SupportedOSPlatforms = new[] { OSPlatform.Windows },
            //    // Workaround for https://github.com/dotnet/runtime/issues/95653
            //    EnvironmentVariables = new[] { ("DD_CLR_ENABLE_INLINING", "0") },
            //},
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

        return description;
    }
}

