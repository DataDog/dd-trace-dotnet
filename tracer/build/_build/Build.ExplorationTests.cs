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
    serilog_1, serilog_2, serilog_3, serilog_4, serilog_5, serilog_6, serilog_7, serilog_8, serilog_9, serilog_10, serilog_11, serilog_12, serilog_13, serilog_14, serilog_15, serilog_16, serilog_17, serilog_18, serilog_19, serilog_20, serilog_21, serilog_22, serilog_23, serilog_24, serilog_25, serilog_26, serilog_27, serilog_28, serilog_29, serilog_30, serilog_31, serilog_32, serilog_33, serilog_34, serilog_35, serilog_36, serilog_37, serilog_38, serilog_39, serilog_40
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
        return new ExplorationTestDescription
        {
            Name = name,
            GitRepositoryUrl = "https://github.com/serilog/serilog.git",
            GitRepositoryTag = "v2.10.0",
            IsGitShallowCloneSupported = true,
            PathToUnitTestProject = "test/Serilog.Tests",
            TestsToIgnore = new[] { "DisconnectRemoteObjectsAfterCrossDomainCallsOnDispose" },
            SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1 },
        };
    }
}

