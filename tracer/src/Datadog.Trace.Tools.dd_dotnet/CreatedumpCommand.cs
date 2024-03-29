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
    private const int MethodNameMaxLength = 1024;

    private static ClrRuntime? _runtime;

    private readonly Argument<int?> _pidArgument = new("pid") { Arity = ArgumentArity.ExactlyOne };
    private readonly Option<bool> _fullOption = new("--full");
    private readonly Option<int> _signalOption = new("--signal");
    private readonly Option<int> _crashthreadOption = new("--crashthread");

    public CreatedumpCommand()
        : base("createdump")
    {
        AddArgument(_pidArgument);
        AddOption(_fullOption);
        AddOption(_signalOption);
        AddOption(_crashthreadOption);
        this.SetHandler(Execute);
    }

    [UnmanagedCallersOnly]
    private static unsafe int ResolveManagedMethod(IntPtr ip, ResolveMethodData* methodData)
    {
        try
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

            methodData->SymbolAddress = method.NativeCode;
            methodData->ModuleAddress = method.Type.Module.ImageBase;

            var name = $"{Path.GetFileName(method.Type.Module.AssemblyName)}!{method.Type}.{method.Name}";

            var length = Math.Min(Encoding.ASCII.GetByteCount(name), MethodNameMaxLength - 1); // -1 to save space for the null terminator

            var destination = new Span<byte>(methodData->Name, MethodNameMaxLength);

            Encoding.ASCII.GetBytes(name, destination);

            destination[length] = (byte)'\0';

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resolving managed method: {ex}");
            return -1;
        }
    }

    private unsafe void Execute(InvocationContext context)
    {
        var pid = _pidArgument.GetValue(context)!;

        AnsiConsole.WriteLine($"Capturing crash info for process {pid}");

        const string path = @"/home/kgosse/git/dd-trace-dotnet/shared/bin/monitoring-home/linux-x64/";

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
        var callback = (delegate* unmanaged<IntPtr, ResolveMethodData*, int>)&ResolveManagedMethod;

        function(pid.Value, (IntPtr)callback);

        AnsiConsole.WriteLine("Createdump command finished");
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ResolveMethodData
    {
        public ulong SymbolAddress;
        public ulong ModuleAddress;
        public fixed byte Name[MethodNameMaxLength];
    }
}
