// <copyright file="CreatedumpCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CreatedumpCommand : Command
{
    private static ClrRuntime? _runtime;

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
    private static unsafe int ResolveManagedMethod(IntPtr ip, byte* buffer, int bufferSize, int* requiredBufferSize)
    {
        if (_runtime == null)
        {
            Console.WriteLine("Runtime is not initialized");
            return -2;
        }

        var method = _runtime.GetMethodByInstructionPointer((ulong)ip);

        if (method == null)
        {
            return 1;
        }

        string name = $"{Path.GetFileName(method.Type.Module.AssemblyName)}!{method.Type}.{method.Name} +{method.GetILOffset((ulong) pc):x2}";

        var length = Encoding.ASCII.GetByteCount(name);

        if (bufferSize < length + 1)
        {
            *requiredBufferSize = length + 1;
            return -1;
        }

        Encoding.ASCII.GetBytes(name, new Span<byte>(buffer, bufferSize));

        buffer[length] = (byte)'\0';

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

        using var target = DataTarget.AttachToProcess(pid.Value, suspend: true);
        _runtime = target.ClrVersions[0].CreateRuntime();

        // extern "C" void __stdcall ReportCrash(int32_t pid, ResolveManagedMethod resolveCallback)
        var function = (delegate* unmanaged<int, IntPtr, void>)export;
        var callback = (delegate* unmanaged<IntPtr, byte*, int, int*, int>)&ResolveManagedMethod;

        function(pid.Value, (IntPtr)callback);

        AnsiConsole.WriteLine("Createdump command finished");
    }
}
