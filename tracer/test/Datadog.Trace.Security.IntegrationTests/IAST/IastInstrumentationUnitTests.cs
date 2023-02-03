// <copyright file="IastInstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using Castle.Core.Internal;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Iast;

public class IastInstrumentationUnitTests : TestHelper
{
    public IastInstrumentationUnitTests(ITestOutputHelper output)
        : base("InstrumentedTests", output)
    {
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public void TestInstrumentedUnitTests()
    {
        bool isNet462x86 = !Environment.Is64BitProcess && !EnvironmentHelper.IsCoreClr();
        if (!isNet462x86)
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                EnableIast(true);
                string arguments = string.Empty;
#if NET462
                arguments = @" /Framework:"".NETFramework,Version=v4.6.2"" ";
#endif
                ProcessResult processResult = RunDotnetTestSampleAndWaitForExit(agent, arguments: arguments);
                processResult.StandardError.Should().BeEmpty("arguments: " + arguments + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
        else
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            {
                EnableIast(true);
                //  --configuration Release --collect "XPlat Code Coverage" --framework net462 --filter RunOnWindows=True&LoadFromGAC!=True&IIS!=True&Category!=AzureFunctions --logger trx --no-build --no-restore --results-directory D:\a\1\s\tracer\build_data\results\Datadog.Trace.Security.IntegrationTests /property:Platform=AnyCPU --logger:datadog -- RunConfiguration.DisableAppDomain=true DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SkipAutoProps=true DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.StrongNameKey="D:\a\1\s\Datadog.Trace.snk" DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*]Datadog.Trace.Vendors.*,[Datadog.Trace]System.*,[Datadog.Trace]Mono.*", DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include="[Datadog.Trace.ClrProfiler.*]*,[Datadog.Trace]*,[Datadog.Trace.AspNet]*"
                string arguments = " --configuration Release --framework net462 --no-build --no-restore /property:Platform=AnyCPU -- RunConfiguration.DisableAppDomain=true ";
                string sampleAppPath = string.Empty;
                sampleAppPath = EnvironmentHelper.GetSampleProjectDirectory() + "\\Samples.InstrumentedTests.csproj";

                var dir = GetDirFiles(EnvironmentHelper.GetSampleProjectDirectory());
                dir += GetDirFiles(EnvironmentHelper.GetSampleProjectDirectory() + "/bin");
                // bin\Release\net462
                dir += GetDirFiles(EnvironmentHelper.GetSampleProjectDirectory() + "/bin/Release");
                dir += GetDirFiles(EnvironmentHelper.GetSampleProjectDirectory() + "/bin/Release/net462");

                (ProcessResult processResult, var commandline) = RunDotnetTestSampleAndWaitForExit2(agent, arguments: arguments, dllPath: sampleAppPath);
                processResult.StandardError.Should().BeEmpty("commandline: " + commandline + dir + "arguments: " + arguments + Environment.NewLine + processResult.StandardError + Environment.NewLine + processResult.StandardOutput);
            }
        }
    }

    private string GetDirFiles(string directory)
    {
        string dir = directory + " files: " + Environment.NewLine;
        try
        {
            dir += string.Join(" ; ", Directory.GetFiles(directory)) + Environment.NewLine;
            dir += directory + " dirs: " + Environment.NewLine;
            dir += string.Join(" ; ", Directory.GetDirectories(directory)) + Environment.NewLine;
        }
        catch (Exception ex)
        {
            dir += ex.Message;
        }

        return dir;
    }

    private (Process Process, string Command) StartDotnetTestSample2(MockTracerAgent agent, string arguments, string packageVersion, int aspNetCorePort, string framework = "", string dllPath = "")
    {
        // get path to sample app that the profiler will attach to
        string sampleAppPath = dllPath.IsNullOrEmpty() ? EnvironmentHelper.GetTestCommandForSampleApplicationPath(packageVersion) : dllPath;
        if (!File.Exists(sampleAppPath))
        {
            throw new Exception($"application not found: {sampleAppPath}");
        }

        Output.WriteLine($"Starting Application: {sampleAppPath}");
        string testCli = EnvironmentHelper.GetDotNetTest();
        string exec = testCli;
        string appPath = testCli.StartsWith("dotnet") ? $"test {sampleAppPath}" : sampleAppPath;
        Output.WriteLine("Executable: " + exec);
        Output.WriteLine("ApplicationPath: " + appPath);
        var commandline = exec + " " + $"{appPath} {arguments ?? string.Empty}";
        var process = ProfilerHelper.StartProcessWithProfiler(
            exec,
            EnvironmentHelper,
            agent,
            $"{appPath} {arguments ?? string.Empty}",
            aspNetCorePort: aspNetCorePort,
            processToProfile: exec + ";testhost.exe");

        Output.WriteLine($"ProcessId: {process.Id}");

        return (process, commandline);
    }

    private (ProcessResult Result, string Command) RunDotnetTestSampleAndWaitForExit2(MockTracerAgent agent, string arguments = null, string packageVersion = "", string framework = "", string dllPath = "")
    {
        (var process, var commandline) = StartDotnetTestSample2(agent, arguments, packageVersion, aspNetCorePort: 5000, framework: framework, dllPath: dllPath);

        using var helper = new ProcessHelper(process);

        process.WaitForExit();
        helper.Drain();
        var exitCode = process.ExitCode;

        Output.WriteLine($"Exit Code: " + exitCode);

        var standardOutput = helper.StandardOutput;

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
        }

        var standardError = helper.ErrorOutput;

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
        }

        return (new ProcessResult(process, standardOutput, standardError, exitCode), commandline);
    }
}
