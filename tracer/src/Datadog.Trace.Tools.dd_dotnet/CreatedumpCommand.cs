// <copyright file="CreatedumpCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CreatedumpCommand : Command
{
    private readonly Argument<string?> _pathArgument = new("path") { Arity = ArgumentArity.ExactlyOne };
    private readonly Argument<int?> _pidArgument = new("pid") { Arity = ArgumentArity.ExactlyOne };

    public CreatedumpCommand()
        : base("createdump")
    {
        AddArgument(_pathArgument);
        AddArgument(_pidArgument);
        this.SetHandler(Execute);
    }

    [UnmanagedCallersOnly]
    private static unsafe int ResolveManagedMethod(IntPtr ip, char* buffer, int bufferSize, int* requiredBufferSize)
    {
        string name = "plop";

        if (bufferSize < name.Length + 1)
        {
            *requiredBufferSize = name.Length + 1;
            return -1;
        }

        buffer[0] = 'p';
        buffer[1] = 'l';
        buffer[2] = 'o';
        buffer[3] = 'p';
        buffer[4] = '\0';

        return 0;
    }

    private unsafe void Execute(InvocationContext context)
    {
        AnsiConsole.WriteLine("Createdump command");

        var path = _pathArgument.GetValue(context)!;
        var pid = _pidArgument.GetValue(context)!;

        AnsiConsole.WriteLine($"Loading Datadog.Profiler.Native.so from {path}");

        var lib = NativeLibrary.Load(Path.Combine(path, "Datadog.Profiler.Native.so"));

        var export = NativeLibrary.GetExport(lib, "ReportCrash");

        if (export == 0)
        {
            AnsiConsole.WriteLine("Failed to load ReportCrash function");
            return;
        }

        // extern "C" void __stdcall ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback)
        var function = (delegate* unmanaged<int, IntPtr, void>)export;


        function(pid, );


        AnsiConsole.WriteLine("Createdump command finished");
    }
}
