using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.VSTest;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

public enum ExplorationTestUseCase
{
    Debugger, ContinuousProfiler
}

public enum ExplorationTestName
{
    eShopOnWeb, protobuf, cake, swashbuckle, paket, ilspy
}

class ExplorationTestDescription
{
    public string GitRepositoryUrl { get; set; }
    public string GitRepositoryTag { get; set; }
    public string Name { get; set; }
    public string PathToUnitTestProject { get; set; }
    public string[] TestsToIgnore { get; set; }
    public bool IsTestedByVSTest { get; set; }

    public TargetFramework[] SupportedFrameworks { get; set; }

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
                GitRepositoryUrl = "https://github.com/dotnet-architecture/eShopOnWeb.git",
                Name = "eShopOnWeb",
                GitRepositoryTag = "netcore2.1",
                PathToUnitTestProject = "tests/UnitTests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1 }
            },
            ExplorationTestName.protobuf => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/protocolbuffers/protobuf.git",
                Name = "protobuf",
                GitRepositoryTag = "v3.19.1",
                PathToUnitTestProject = "csharp/src/Google.Protobuf.Test",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP2_1, TargetFramework.NET5_0, }
            },
            ExplorationTestName.cake => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/cake-build/cake.git",
                Name = "cake",
                GitRepositoryTag = "v1.3.0",
                PathToUnitTestProject = "src/Cake.Common.Tests",
                SupportedFrameworks = new[] { TargetFramework.NETCOREAPP3_1, TargetFramework.NET5_0, TargetFramework.NET6_0 }
            },
            ExplorationTestName.swashbuckle => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/domaindrivendev/Swashbuckle.AspNetCore.git",
                Name = "Swashbuckle.AspNetCore",
                GitRepositoryTag = "v6.2.3",
                PathToUnitTestProject = "test/Swashbuckle.AspNetCore.SwaggerGen.Test",
                SupportedFrameworks = new[] { TargetFramework.NET6_0 }
            },
            ExplorationTestName.paket => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/fsprojects/Paket.git",
                Name = "Paket",
                GitRepositoryTag = "6.2.1",
                PathToUnitTestProject = "tests/Paket.Tests",
                TestsToIgnore = new[] { "Loading assembly metadata works" },
                SupportedFrameworks = new[] { TargetFramework.NET461, TargetFramework.NETCOREAPP3_1 }
            },
            ExplorationTestName.ilspy => new ExplorationTestDescription()
            {
                GitRepositoryUrl = "https://github.com/icsharpcode/ILSpy.git",
                Name = "ILSpy",
                GitRepositoryTag = "v7.1",
                PathToUnitTestProject = "ICSharpCode.Decompiler.Tests",
                TestsToIgnore = new[] { "UseMc", "_net45", "ImplicitConversions", "ExplicitConversions", "ICSharpCode_Decompiler", "NewtonsoftJson_pcl_debug", "NRefactory_CSharp", "Random_TestCase_1", "AsyncForeach", "AsyncStreams", "AsyncUsing", "CS9_ExtensionGetEnumerator", "IndexRangeTest", "InterfaceTests", "UsingVariables" },
                SupportedFrameworks = new[] { TargetFramework.NET461 },
                IsTestedByVSTest = true
            },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

        return description;
    }
}

partial class Build
{
    AbsolutePath ExplorationTestsDirectory => RootDirectory / "exploration-tests";

    [Parameter("Indicates name of exploration test to run. If not specified, will run all tests sequentially.")]
    readonly ExplorationTestName? ExplorationTestName;

    [Parameter("Indicates whether exploration tests should skip repository cloning. Useful for local development. Default false.")]
    readonly bool ExplorationTestSkipClone;

    [Parameter("Indicates whether exploration tests should run on latest repository commit. Useful if you want to update tested repositories to the latest tags. Default false.", 
               List = false)]
    readonly bool ExplorationTestCloneLatest;

    Target RunExplorationTests_Debugger
        => _ => _
               .Description("Run exploration tests for debugger.")
               .After(Clean, BuildTracerHome)
               .Executes(() =>
                {
                    SetUpExplorationTest_Debugger();

                    var envVariables = GetEnvVariables(ExplorationTestUseCase.Debugger);
                    RunExplorationTestsGitCloneAndUnitTest(envVariables);
                    RunExplorationTestAssertions_Debugger();

                    void SetUpExplorationTest_Debugger()
                    {
                        Logger.Info($"Prepare environment variables for profiler.");
                        //TODO TBD
                    }

                    void RunExplorationTestAssertions_Debugger()
                    {
                        Logger.Info($"Running assertions tests for debugger.");
                        //TODO TBD
                    }

                    MoveLogsToBuildData();
                })
        ;


    Target RunExplorationTests_ContinuousProfiler
        => _ => _
               .Description("Run exploration tests for profiler.")
               .After(Clean, BuildTracerHome)
               .Executes(() =>
                {
                    SetUpExplorationTest_ContinuousProfiler();
                    
                    var envVariables = GetEnvVariables(ExplorationTestUseCase.ContinuousProfiler);
                    RunExplorationTestsGitCloneAndUnitTest(envVariables);
                    RunExplorationTestAssertions_ContinuousProfiler();

                    void SetUpExplorationTest_ContinuousProfiler()
                    {
                        Logger.Info($"Prepare environment variables for profiler.");
                        //TODO TBD
                    }

                    void RunExplorationTestAssertions_ContinuousProfiler()
                    {
                        Logger.Info($"Running assertions tests for profiler.");
                        //TODO TBD
                    }

                    MoveLogsToBuildData();
                })
        ;

    Dictionary<string, string> GetEnvVariables(ExplorationTestUseCase usecase)
    {
        var envVariables = new Dictionary<string, string>();
        switch (usecase)
        {
            case ExplorationTestUseCase.Debugger:
                envVariables.AddDebuggerEnvironmentVariables(TracerHomeDirectory);
                break;
            case ExplorationTestUseCase.ContinuousProfiler:
                envVariables.AddContinuousProfilerEnvironmentVariables(TracerHomeDirectory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(usecase), usecase, null);
        }

        envVariables.AddExtraEnvVariables(ExtraEnvVars);
        return envVariables;
    }

    void RunExplorationTestsGitCloneAndUnitTest(Dictionary<string, string> envVariables)
    {
        if (ExplorationTestName.HasValue)
        {
            Logger.Info($"Provided exploration test name is {ExplorationTestName}.");
            
            var testDescription = ExplorationTestDescription.GetExplorationTestDescription(ExplorationTestName.Value);

            GitCloneAndRunUnitTest(testDescription, envVariables);
        }
        else
        {
            Logger.Info($"Exploration test name is not provided. Running all.");

            foreach (var testDescription in ExplorationTestDescription.GetAllExplorationTestDescriptions())
            {
                GitCloneAndRunUnitTest(testDescription, envVariables);
            }
        }
    }

    void GitCloneAndRunUnitTest(ExplorationTestDescription testDescription, Dictionary<string, string> envVariables)
    {
        Logger.Info($"Running exploration test {testDescription.Name}.");

        if (Framework != null && !testDescription.IsFrameworkSupported(Framework))
        {
            Logger.Warn($"This framework '{Framework}' is not listed in the project's target frameworks of {testDescription.Name}");
            return;
        }

        var projectPath = GitClone(testDescription);
        Logger.Info($"Test project path is {projectPath}");

        DotNetBuild(
            x => x
                .SetProjectFile(projectPath)
                .SetConfiguration(BuildConfiguration)
                .When(Framework != null, settings => settings.SetFramework(Framework))
        );

        DotNetTest(
            x =>
            {
                x = x
                   .When(!testDescription.IsTestedByVSTest, settings => settings.SetProjectFile(projectPath))
                   .When(testDescription.IsTestedByVSTest
                       , settings =>
                         {
                             var frameworkFolder = Framework ?? "*";
                             var projectName = Path.GetFileName(projectPath);

                             return settings.SetProjectFile($"{projectPath}/bin/{BuildConfiguration}/{frameworkFolder}/{projectName}.exe");
                         })
                   .EnableNoRestore()
                   .EnableNoBuild()
                   .SetConfiguration(BuildConfiguration)
                   .When(Framework != null, settings => settings.SetFramework(Framework))
                   .SetProcessEnvironmentVariables(envVariables)
                   .SetIgnoreFilter(testDescription.TestsToIgnore)
                   .WithMemoryDumpAfter(2)
                    ;

                return x;
            });
    }

    string GitClone(ExplorationTestDescription testDescription)
    {
        if (!ExplorationTestSkipClone)
        {
            var cloneCommand = ExplorationTestCloneLatest
                                   ? $"clone -c advice.detachedHead=false {testDescription.GitRepositoryUrl} {ExplorationTestsDirectory}/{testDescription.Name}"
                                   : $"clone -c advice.detachedHead=false -b {testDescription.GitRepositoryTag} {testDescription.GitRepositoryUrl} {ExplorationTestsDirectory}/{testDescription.Name}";


            GitTasks.Git(cloneCommand, logOutput: false);
        }

        var projectPath = $"{ExplorationTestsDirectory}/{testDescription.Name}/{testDescription.PathToUnitTestProject}";
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Test path '{projectPath}' doesn't exist.");
        }

        return projectPath;
    }
}
