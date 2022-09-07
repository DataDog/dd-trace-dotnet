using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Target = Nuke.Common.Target;

partial class Build
{
    AbsolutePath ExplorationTestsDirectory => RootDirectory / "exploration-tests";

    [Parameter("Indicates use case of exploration test to run.")]
    readonly ExplorationTestUseCase? ExplorationTestUseCase;

    [Parameter("Indicates name of exploration test to run. If not specified, will run all tests sequentially.")]
    readonly ExplorationTestName? ExplorationTestName;

    [Parameter("Indicates whether exploration tests should run on latest repository commit. Useful if you want to update tested repositories to the latest tags. Default false.",
               List = false)]
    readonly bool ExplorationTestCloneLatest;

    Target SetUpExplorationTests
        => _ => _
               .Description("Setup exploration tests.")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome)
               .Executes(() =>
                {
                    SetUpExplorationTest();
                    GitCloneBuild();
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
        Logger.Info($"Set up exploration test for debugger.");
        //TODO TBD
    }

    void SetUpExplorationTest_ContinuousProfiler()
    {
        Logger.Info($"Prepare environment variables for continuous profiler.");
        //TODO TBD
    }

    void SetUpExplorationTest_Tracer()
    {
        Logger.Info($"Prepare environment variables for tracer.");
        //TODO TBD
    }

    void GitCloneBuild()
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Info($"Provided exploration test name is {ExplorationTestName}.");

            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            GitCloneAndBuild(testDescription);
        }
        else
        {
            Logger.Info($"Exploration test name is not provided. Running all of them.");

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
                .SetProcessArgumentConfigurator(arguments => arguments.Add("-consoleLoggerParameters:ErrorsOnly"))
                .When(Framework != null, settings => settings.SetFramework(Framework))
        );
    }

    Target RunExplorationTests
        => _ => _
               .Description("Run exploration tests.")
               .Requires(() => ExplorationTestUseCase)
               .After(Clean, BuildTracerHome, BuildNativeLoader, SetUpExplorationTests)
               .Executes(() =>
                {
                    FileSystemTasks.EnsureExistingDirectory(TestLogsDirectory);
                    try
                    {
                        var envVariables = GetEnvironmentVariables();
                        RunExplorationTestsGitUnitTest(envVariables);
                        RunExplorationTestAssertions();
                    }
                    finally
                    {
                        CopyDumpsToBuildData();
                    }
                })
        ;

    Dictionary<string, string> GetEnvironmentVariables()
    {
        var envVariables = new Dictionary<string, string>
        {
            ["DD_TRACE_LOG_DIRECTORY"] = TestLogsDirectory,
            ["DD_SERVICE"] = "exploration_tests",
            ["DD_VERSION"] = Version
        };

        switch (ExplorationTestUseCase)
        {
            case global::ExplorationTestUseCase.Debugger:
                AddDebuggerEnvironmentVariables(envVariables);
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

        return envVariables;
    }

    void RunExplorationTestsGitUnitTest(Dictionary<string, string> envVariables)
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Info($"Provided exploration test name is {ExplorationTestName}.");

            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);
            RunUnitTest(testDescription, envVariables);
        }
        else
        {
            Logger.Info($"Exploration test name is not provided. Running all.");

            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                RunUnitTest(testDescription, envVariables);
            }
        }
    }

    void RunUnitTest(ExplorationTestDescription testDescription, Dictionary<string, string> envVariables)
    {
        Logger.Info($"Running exploration test {testDescription.Name}.");

        if (Framework != null && !testDescription.IsFrameworkSupported(Framework))
        {
            throw new InvalidOperationException($"The framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
        }

        DotNetTest(
            x =>
            {
                x = x
                   .SetProjectFile(testDescription.GetTestTargetPath(ExplorationTestsDirectory, Framework, BuildConfiguration))
                   .EnableNoRestore()
                   .EnableNoBuild()
                   .SetConfiguration(BuildConfiguration)
                   .When(Framework != null, settings => settings.SetFramework(Framework))
                   .SetProcessEnvironmentVariables(envVariables)
                   .SetIgnoreFilter(testDescription.TestsToIgnore)
                   .WithMemoryDumpAfter(1)
                    ;

                return x;
            });
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
        Logger.Info($"Running assertions tests for debugger.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_ContinuousProfiler()
    {
        Logger.Info($"Running assertions tests for profiler.");
        //TODO TBD
    }

    void RunExplorationTestAssertions_Tracer()
    {
        Logger.Info($"Running assertions tests for tracer.");
        //TODO TBD
    }
}


public enum ExplorationTestUseCase
{
    Debugger, ContinuousProfiler, Tracer
}

public enum ExplorationTestName
{
    eShopOnWeb, protobuf, cake, swashbuckle, paket, RestSharp, serilog, polly, automapper, /*ilspy*/
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
                GitRepositoryTag = "netcore2.1",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "tests/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1 },
            },
            ExplorationTestName.protobuf => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.protobuf,
                GitRepositoryUrl = "https://github.com/protocolbuffers/protobuf.git",
                GitRepositoryTag = "v3.19.1",
                IsGitShallowCloneSupported = true,
                IsGitSubmodulesRequired = true,
                PathToUnitTestProject = "csharp/src/Google.Protobuf.Test",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1 },
            },
            ExplorationTestName.cake => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.cake,
                GitRepositoryUrl = "https://github.com/cake-build/cake.git",
                GitRepositoryTag = "v1.3.0",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "src/Cake.Common.Tests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0 },
            },
            ExplorationTestName.swashbuckle => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.swashbuckle,
                GitRepositoryUrl = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore.git",
                GitRepositoryTag = "v6.2.3",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "test/Swashbuckle.AspNetCore.SwaggerGen.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
            },
            ExplorationTestName.paket => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.paket,
                GitRepositoryUrl = "https://github.com/fsprojects/Paket.git",
                GitRepositoryTag = "6.2.1",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "tests/Paket.Tests",
                TestsToIgnore = new[] { "Loading assembly metadata works" },
                SupportedFrameworks = new[] { TargetFramework.NET461 },
            },
            ExplorationTestName.RestSharp => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.RestSharp,
                GitRepositoryUrl = "https://github.com/restsharp/RestSharp.git",
                GitRepositoryTag = "107.0.3",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "test/RestSharp.Tests",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
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
            ExplorationTestName.automapper => new ExplorationTestDescription()
            {
                Name = ExplorationTestName.automapper,
                GitRepositoryUrl = "https://github.com/automapper/automapper.git",
                GitRepositoryTag = "v11.0.0",
                IsGitShallowCloneSupported = true,
                PathToUnitTestProject = "src/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 },
                SupportedOSPlatforms = new[] { OSPlatform.Windows },
            },
            //ExplorationTestName.ilspy => new ExplorationTestDescription()
            //{
            //    Name = ExplorationTestName.ilspy,
            //    GitRepositoryUrl = "https://github.com/icsharpcode/ILSpy.git",
            //    GitRepositoryTag = "v7.1",
            //    IsGitSubmodulesRequired = true,
            //    PathToUnitTestProject = "ICSharpCode.Decompiler.Tests",
            //    IsTestedByVSTest = true,
            //    TestsToIgnore = new[] { "UseMc", "_net45", "ImplicitConversions", "ExplicitConversions", "ICSharpCode_Decompiler", "NewtonsoftJson_pcl_debug", "NRefactory_CSharp", "Random_TestCase_1", "AsyncForeach", "AsyncStreams", "AsyncUsing", "CS9_ExtensionGetEnumerator", "IndexRangeTest", "InterfaceTests", "UsingVariables" },
            //    SupportedFrameworks = new[] { TargetFramework.NET461 },
            //},
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

        return description;
    }
}

