// <copyright file="ProcessMemoryAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Runtime;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DumpAnalysis
{
    internal static class ProcessMemoryAnalyzer
    {
        internal static void Analyze(int processId)
        {
            var dataTarget = DataTarget.AttachToProcess(processId, suspend: true);
            var clrInfo = dataTarget.ClrVersions.First();
            var runtime = clrInfo.CreateRuntime();

            foreach (var thread in runtime.Threads)
            {
                AnsiConsole.WriteLine("Thread {0}", thread.ManagedThreadId);
                AnsiConsole.WriteLine("Exception: {0}", thread.CurrentException?.Type.Name ?? "None");
                foreach (var clrStackFrame in thread.EnumerateStackTrace(includeContext: false))
                {
                    var method = clrStackFrame?.Method;

                    if (method?.Type?.Module != null)
                    {
                        var assemblyName = method.Type.Module.AssemblyName;

                        if (IsInAllowList(method.Type.Module))
                        {
                            AnsiConsole.WriteLine($"{Path.GetFileName(assemblyName)}!{method.Type}.{method.Name}");
                        }
                        else
                        {
                            AnsiConsole.WriteLine($"[redacted stack frame]");
                        }
                    }
                }
            }
        }

        private static bool IsInAllowList(ClrModule module)
        {
            return module.AssemblyName != null &&
                   (module.AssemblyName.StartsWith("Datadog.Trace") ||
                    BCLAssemblyDetector.IsBCLAssembly(module.Name));
        }
    }
}
