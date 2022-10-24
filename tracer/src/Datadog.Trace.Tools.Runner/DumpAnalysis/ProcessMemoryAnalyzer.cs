// <copyright file="ProcessMemoryAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DumpAnalysis
{
    internal class ProcessMemoryAnalyzer
    {
        public void Analyze(int processId)
        {
            DataTarget dataTarget = DataTarget.AttachToProcess(processId, true);
            var clrInfo = dataTarget.ClrVersions.First();
            var runtime = clrInfo.CreateRuntime();

            foreach (var thread in runtime.Threads)
            {
                AnsiConsole.WriteLine("Thread {0}", thread.ManagedThreadId);
                AnsiConsole.WriteLine("Exception: {0}", thread.CurrentException?.Type.Name ?? "None");
                foreach (var clrStackFrame in thread.EnumerateStackTrace(false))
                {
                    var method = clrStackFrame?.Method;
                    AnsiConsole.WriteLine($"{Path.GetFileName(method.Type.Module.AssemblyName)}!{method.Type}.{method.Name}");
                }
            }
        }
    }
}
