// <copyright file="CreatedumpCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
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
    private readonly Option<int?> _signalOption = new("--signal");
    private readonly Option<int?> _crashthreadOption = new("--crashthread");

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
        Console.WriteLine($"Command: {Environment.CommandLine}");

        var pid = _pidArgument.GetValue(context)!;
        var signal = _signalOption.GetValue(context);

        var crashThread = _crashthreadOption.GetValue(context);

        if (crashThread.HasValue)
        {
            AnsiConsole.WriteLine($"Crash thread: {crashThread}");
        }

        AnsiConsole.WriteLine($"Capturing crash info for process {pid}");

        var lib = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Datadog.Profiler.Native.so"));

        var export = NativeLibrary.GetExport(lib, "CreateCrashReport");

        if (export == 0)
        {
            AnsiConsole.WriteLine("Failed to load the CreateCrashReport function");
            return;
        }

        using var target = DataTarget.AttachToProcess(pid.Value, suspend: true);
        _runtime = target.ClrVersions[0].CreateRuntime();

        var function = (delegate* unmanaged<int, IntPtr>)export;

        var ptr = function(pid.Value);

        if (ptr == IntPtr.Zero)
        {
            AnsiConsole.WriteLine("Failed to create crash report");
            return;
        }

        using var iunknown = NativeObjects.IUnknown.Wrap(ptr);

        int result = iunknown.QueryInterface(ICrashReport.Guid, out var crashReportPtr);

        if (result != 0)
        {
            AnsiConsole.WriteLine($"Failed to query interface: {result}");
            return;
        }

        using var crashReport = NativeObjects.ICrashReport.Wrap(crashReportPtr);

        try
        {
            crashReport.Initialize();
        }
        catch (Win32Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to initialize crash report: {GetLastError(crashReport, ex)}");
            return;
        }

        // Check if there's an exception on the crash thread
        if (crashThread != null)
        {
            var exception = _runtime.Threads.FirstOrDefault(t => t.ManagedThreadId == crashThread.Value)?.CurrentException;

            if (exception != null)
            {
                AnsiConsole.WriteLine($"Managed exception found: {exception}");
                _ = SetException(crashReport, exception);
            }
        }

        if (signal.HasValue)
        {
            _ = SetSignal(crashReport, signal.Value);
        }

        _ = SetMetadata(crashReport);

        try
        {
            var callback = (delegate* unmanaged<IntPtr, ResolveMethodData*, int>)&ResolveManagedMethod;
            crashReport.ResolveStacks(crashThread ?? 0, (IntPtr)callback);
        }
        catch (Win32Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to resolve stacks: {GetLastError(crashReport, ex)}");
        }

        try
        {
            crashReport.Send();
        }
        catch (Win32Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to send crash report: {GetLastError(crashReport, ex)}");
            return;
        }

        AnsiConsole.WriteLine("Crash report sent successfully");
    }

    private string GetLastError(ICrashReport crashReport, Win32Exception exception)
    {
        if (exception.NativeErrorCode == 1)
        {
            // libdatadog error
            crashReport.GetLastError(out var message, out var length);

            if (message == IntPtr.Zero)
            {
                return "unknown error";
            }

            return Marshal.PtrToStringAnsi(message, length);
        }

        return $"unspecified error: {exception.NativeErrorCode}";
    }

    private bool SetSignal(ICrashReport crashReport, int signal)
    {
        try
        {
            crashReport.SetSignalInfo(signal, IntPtr.Zero);
        }
        catch (Win32Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to set signal info: {GetLastError(crashReport, ex)}");
            return false;
        }

        return true;
    }

    private bool SetException(ICrashReport crashReport, ClrException exception)
    {
        var key = IntPtr.Zero;
        var value = IntPtr.Zero;

        try
        {
            key = Marshal.StringToHGlobalAnsi("exception");
            value = Marshal.StringToHGlobalAnsi(exception.ToString());

            try
            {
                crashReport.AddTag(key, value);
                return true;
            }
            catch (Win32Exception ex)
            {
                AnsiConsole.WriteLine($"Failed to add exception tag: {GetLastError(crashReport, ex)}");
                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(key);
            Marshal.FreeHGlobal(value);
        }
    }

    private bool SetMetadata(ICrashReport crashReport)
    {
        var libraryName = IntPtr.Zero;
        var libraryVersion = IntPtr.Zero;
        var family = IntPtr.Zero;

        try
        {
            libraryName = Marshal.StringToHGlobalAnsi("dd-dotnet");
            libraryVersion = Marshal.StringToHGlobalAnsi("1.0.0"); // TODO: Extract the version
            family = Marshal.StringToHGlobalAnsi("csharp");

            try
            {
                crashReport.SetMetadata(libraryName, libraryVersion, family);
            }
            catch (Win32Exception ex)
            {
                AnsiConsole.WriteLine($"Failed to set metadata: {GetLastError(crashReport, ex)}");
                return false;
            }

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(libraryName);
            Marshal.FreeHGlobal(libraryVersion);
            Marshal.FreeHGlobal(family);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Tag
    {
        public IntPtr Key;
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ResolveMethodData
    {
        public ulong SymbolAddress;
        public ulong ModuleAddress;
        public fixed byte Name[MethodNameMaxLength];
    }
}
