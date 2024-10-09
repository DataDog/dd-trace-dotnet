// <copyright file="ConsoleTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tools.dd_dotnet.ArtifactTests.Checks;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests;

public abstract class ConsoleTestHelper : ToolTestHelper
{
    protected ConsoleTestHelper(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    protected Task<CustomProcessHelper> StartConsole(bool enableProfiler, params (string Key, string Value)[] environmentVariables)
    {
        return StartConsole(EnvironmentHelper, enableProfiler, "wait", environmentVariables);
    }

    protected Task<CustomProcessHelper> StartConsoleWithArgs(string args, bool enableProfiler, params (string Key, string Value)[] environmentVariables)
    {
        return StartConsole(EnvironmentHelper, enableProfiler, args, environmentVariables);
    }

    protected (string Executable, string Args) PrepareSampleApp(EnvironmentHelper environmentHelper)
    {
        var sampleAppPath = environmentHelper.GetSampleApplicationPath();
        var executable = EnvironmentHelper.IsCoreClr() ? environmentHelper.GetSampleExecutionSource() : sampleAppPath;
        var args = EnvironmentHelper.IsCoreClr() ? sampleAppPath : string.Empty;

        // this is nasty, but it's the only way I could find to force
        // a .NET Framework exe to run in 32 bit if required
        if (EnvironmentTools.IsWindows()
         && !EnvironmentHelper.IsCoreClr()
         && !EnvironmentTools.IsTestTarget64BitProcess())
        {
            ProfilerHelper.SetCorFlags(executable, Output, !EnvironmentTools.IsTestTarget64BitProcess());
        }

        return (executable, args);
    }

    protected Task<CustomProcessHelper> StartConsole(EnvironmentHelper environmentHelper, bool enableProfiler, string args, params (string Key, string Value)[] environmentVariables)
    {
        var (executable, baseArgs) = PrepareSampleApp(environmentHelper);
        args = $"{baseArgs} {args}";

        return StartConsole(executable, args, environmentHelper, enableProfiler, environmentVariables);
    }

    protected async Task<CustomProcessHelper> StartConsole(string executable, string args, EnvironmentHelper environmentHelper, bool enableProfiler, params (string Key, string Value)[] environmentVariables)
    {
        var processStart = new ProcessStartInfo(executable, args) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };

        MockTracerAgent? agent = null;

        if (enableProfiler)
        {
            agent = MockTracerAgent.Create(Output, useTelemetry: true, optionalTelemetryHeaders: true);

            environmentHelper.SetEnvironmentVariables(
                agent,
                aspNetCorePort: 1000,
                processStart.Environment);
        }
        else
        {
            // We should still apply the custom environment variables
            foreach (string key in environmentHelper.CustomEnvironmentVariables.Keys)
            {
                processStart.Environment[key] = environmentHelper.CustomEnvironmentVariables[key];
            }
        }

        foreach (var (key, value) in environmentVariables)
        {
            processStart.EnvironmentVariables[key] = value;
        }

        var startedTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Action<string> callback = s =>
        {
            if (s.StartsWith("Waiting"))
            {
                startedTask.TrySetResult(true);
            }
        };

        var helper = new CustomProcessHelper(agent, Process.Start(processStart)!, callback);

        var completed = await Task.WhenAny(
                            helper.Task,
                            startedTask.Task,
                            Task.Delay(TimeSpan.FromSeconds(30)));

        if (completed == startedTask.Task)
        {
            return helper;
        }

        if (completed == helper.Task)
        {
            helper.Dispose();
            throw new Exception("The target process unexpectedly exited");
        }

        // Try to capture a memory dump before giving up
        if (MemoryDumpHelper.CaptureMemoryDump(helper.Process, new Progress<string>(Output.WriteLine)))
        {
            Output.WriteLine("Successfully captured a memory dump");
        }
        else
        {
            Output.WriteLine("Failed to capture a memory dump");
        }

        helper.Dispose();

        throw new TimeoutException("Timeout when waiting for the target process to start");
    }

    public class CustomProcessHelper : ProcessHelper
    {
        public CustomProcessHelper(MockTracerAgent? agent, Process process, Action<string>? onDataReceived = null)
            : base(process, onDataReceived)
        {
            Agent = agent;
        }

        public MockTracerAgent? Agent { get; }

        public override void Dispose()
        {
            base.Dispose();
            Agent?.Dispose();
        }
    }
}
