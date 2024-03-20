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

    public CreatedumpCommand()
        : base("createdump")
    {
        AddArgument(_pathArgument);
        this.SetHandler(Execute);
    }

    private unsafe void Execute(InvocationContext context)
    {
        AnsiConsole.WriteLine("Createdump command");

        var path = _pathArgument.GetValue(context)!;

        AnsiConsole.WriteLine($"Loading Datadog.Profiler.Native.dll from {path}");

        var lib = NativeLibrary.Load(Path.Combine(path, "Datadog.Profiler.Native.dll"));

        var export = NativeLibrary.GetExport(lib, "ReportCrash");

        if (export == 0)
        {
            AnsiConsole.WriteLine("Failed to load ReportCrash function");
            return;
        }

        // extern "C" void __stdcall ReportCrash(char** frames, int count, char* threadId)
        var function = (delegate* unmanaged<IntPtr, int, IntPtr, void>)export;

        var frames = new string[]
        {
            "frame1",
            "frame2",
            "frame3",
        };

        var framesPtr = Marshal.AllocHGlobal(frames.Length * IntPtr.Size);

        for (var i = 0; i < frames.Length; i++)
        {
            var frame = Marshal.StringToHGlobalAnsi(frames[i]);
            Marshal.WriteIntPtr(framesPtr, i * IntPtr.Size, frame);
        }

        var threadId = Marshal.StringToHGlobalAnsi("1234");

        function(framesPtr, frames.Length, threadId);

        Marshal.FreeHGlobal(framesPtr);
        Marshal.FreeHGlobal(threadId);

        NativeLibrary.Free(lib);

        AnsiConsole.WriteLine("Createdump command finished");
    }
}
