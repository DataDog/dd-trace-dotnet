// <copyright file="ConsoleTestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Tools.dd_dotnet.ArtifactTests.Checks;

public abstract class ConsoleTestHelper : ToolTestHelper
{
    protected ConsoleTestHelper(ITestOutputHelper output)
        : base("Console", output)
    {
    }

    protected Task<ProcessHelper> StartConsole(bool enableProfiler, params (string Key, string Value)[] environmentVariables)
    {
        return StartConsole(EnvironmentHelper, enableProfiler, environmentVariables);
    }

    protected async Task<ProcessHelper> StartConsole(EnvironmentHelper environmentHelper, bool enableProfiler, params (string Key, string Value)[] environmentVariables)
    {
        string sampleAppPath = environmentHelper.GetSampleApplicationPath();
        var executable = EnvironmentHelper.IsCoreClr() ? environmentHelper.GetSampleExecutionSource() : sampleAppPath;
        var args = EnvironmentHelper.IsCoreClr() ? $"{sampleAppPath} wait" : "wait";

        // this is nasty, but it's the only way I could find to force
        // a .NET Framework exe to run in 32 bit if required
        if (EnvironmentTools.IsWindows()
         && !EnvironmentHelper.IsCoreClr()
         && !EnvironmentTools.IsTestTarget64BitProcess())
        {
            ProfilerHelper.SetCorFlags(executable, Output, !EnvironmentTools.IsTestTarget64BitProcess());
        }

        var processStart = new ProcessStartInfo(executable, args)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        MockTracerAgent? agent = null;

        if (enableProfiler)
        {
            agent = MockTracerAgent.Create(Output);

            environmentHelper.SetEnvironmentVariables(
                agent,
                aspNetCorePort: 1000,
                processStart.Environment);
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
                            Task.Delay(TimeSpan.FromSeconds(10)));

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

    private class CustomProcessHelper : ProcessHelper
    {
        private readonly MockTracerAgent? _agent;

        public CustomProcessHelper(MockTracerAgent? agent, Process process, Action<string>? onDataReceived = null)
            : base(process, onDataReceived)
        {
            _agent = agent;
        }

        public override void Dispose()
        {
            base.Dispose();
            _agent?.Dispose();
        }
    }
}
