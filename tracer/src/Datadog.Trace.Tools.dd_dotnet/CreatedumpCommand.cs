// <copyright file="CreatedumpCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.ComponentModel;
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

    private readonly Argument<string[]> _allArguments = new("args");

    public CreatedumpCommand()
        : base("createdump")
    {
        // Use a string[] argument to match everything
        // We want to be able to forward the command to createdump even when we don't understand it
        AddArgument(_allArguments);
        IsHidden = true;

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
            methodData->IsSuspicious = IsSuspicious(method);

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

    private static bool IsSuspicious(ClrMethod method)
    {
        var assemblyName = method.Type.Module.AssemblyName;

        if (assemblyName != null && assemblyName.StartsWith("Datadog", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: we need to whitelist some methods
            return true;
        }

        if (method.Type.Module.IsDynamic && assemblyName != null && assemblyName.StartsWith("DuckType"))
        {
            return true;
        }

        return false;
    }

    private static bool ParseArguments(string[] arguments, out int pid, out int? signal, out int? crashThread)
    {
        pid = default;
        signal = default;
        crashThread = default;

        if (arguments.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(arguments[1], out pid))
        {
            return false;
        }

        for (int i = 2; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "--signal")
            {
                if (int.TryParse(arguments[i + 1], out int s))
                {
                    signal = s;
                }
            }
            else if (arguments[i] == "--crashthread")
            {
                if (int.TryParse(arguments[i + 1], out int t))
                {
                    crashThread = t;
                }
            }
        }

        return true;
    }

    private unsafe void Execute(InvocationContext context)
    {
        var allArguments = _allArguments.GetValue(context);

        try
        {
            if (ParseArguments(allArguments, out var pid, out var signal, out var crashThread))
            {
                GenerateCrashReport(pid, signal, crashThread);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Error while generating the crash report: {ex}");
        }
        finally
        {
            // Note: if refactoring, make sure to dispose the ClrMD DataTarget before calling createdump,
            // otherwise the calls to ptrace from createdump will fail
            if (Environment.GetEnvironmentVariable("DD_TRACE_CRASH_HANDLER_PASSTHROUGH") == "1")
            {
                if (allArguments.Length > 0)
                {
                    InvokeCreatedump(allArguments[0]);
                }
            }
        }
    }

    private unsafe void GenerateCrashReport(int pid, int? signal, int? crashThread)
    {
        var lib = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Datadog.Profiler.Native.so"));

        var export = NativeLibrary.GetExport(lib, "CreateCrashReport");

        if (export == 0)
        {
            AnsiConsole.WriteLine("Failed to load the CreateCrashReport function");
            return;
        }

        using var target = DataTarget.AttachToProcess(pid, suspend: true);
        _runtime = target.ClrVersions[0].CreateRuntime();

        var function = (delegate* unmanaged<int, IntPtr>)export;

        var ptr = function(pid);

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

        if (crashThread == null)
        {
            var firstThreadWithException = _runtime.Threads.FirstOrDefault(t => t.CurrentException != null);

            if (firstThreadWithException != null)
            {
                crashThread = (int)firstThreadWithException.OSThreadId;
            }
        }

        // Check if there's an exception on the crash thread
        if (crashThread != null)
        {
            var exception = _runtime.Threads.FirstOrDefault(t => t.OSThreadId == crashThread.Value)?.CurrentException;

            if (exception != null)
            {
                _ = SetException(crashReport, exception);
            }
        }

        bool isSuspicious = false;

        try
        {
            var callback = (delegate* unmanaged<IntPtr, ResolveMethodData*, int>)&ResolveManagedMethod;
            crashReport.ResolveStacks(crashThread ?? 0, (IntPtr)callback, out isSuspicious);

            if (!isSuspicious)
            {
                AnsiConsole.WriteLine("No suspicious methods found in the stack trace");
                return;
            }
        }
        catch (Win32Exception ex)
        {
            AnsiConsole.WriteLine($"Failed to resolve stacks: {GetLastError(crashReport, ex)}");
        }

        if (signal.HasValue)
        {
            _ = SetSignal(crashReport, signal.Value);
        }

        _ = SetMetadata(crashReport, _runtime);

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

    private void InvokeCreatedump(string createdumpPath)
    {
        if (File.Exists(createdumpPath))
        {
            var commandLineArgs = Environment.GetCommandLineArgs();

            if (commandLineArgs.Length <= 2)
            {
                // It shouldn't ever happen, but who knows
                AnsiConsole.WriteLine("Unable to call createdump, missing arguments");
            }
            else
            {
                AnsiConsole.WriteLine($"Calling createdump with arguments: {string.Join(" ", commandLineArgs[2..])}");
                System.Diagnostics.Process.Start(commandLineArgs[2], commandLineArgs[3..]).WaitForExit();
            }
        }
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

    private bool AddTag(ICrashReport crashReport, string key, string value)
    {
        var keyPtr = IntPtr.Zero;
        var valuePtr = IntPtr.Zero;

        try
        {
            keyPtr = Marshal.StringToHGlobalAnsi(key);
            valuePtr = Marshal.StringToHGlobalAnsi(value);

            try
            {
                crashReport.AddTag(keyPtr, valuePtr);
                return true;
            }
            catch (Win32Exception ex)
            {
                AnsiConsole.WriteLine($"Failed to add tag: {GetLastError(crashReport, ex)}");
                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
            Marshal.FreeHGlobal(valuePtr);
        }
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

    private unsafe bool SetMetadata(ICrashReport crashReport, ClrRuntime runtime)
    {
        var flavor = runtime.ClrInfo.Flavor switch
        {
            ClrFlavor.Core => ".NET Core",
            ClrFlavor.Desktop => ".NET Framework",
            ClrFlavor.NativeAOT => "NativeAOT",
            ClrFlavor f => f.ToString()
        };

        var version = string.Empty;

        if (runtime.ClrInfo.Version.Major != 0)
        {
            version = runtime.ClrInfo.Version.ToString();
        }
        else if (runtime.ClrInfo.ModuleInfo.Version.Major != 0)
        {
            version = runtime.ClrInfo.ModuleInfo.Version.ToString();
        }
        else
        {
            // Shared libraries have no version number on Linux :(
            // Make a best effort to try to figure out the version of .NET from the path
            var fileName = runtime.ClrInfo.ModuleInfo.FileName;

            if (Path.GetDirectoryName(fileName) is { } folder)
            {
                // Check if the parent folder is Microsoft.NETCore.App
                if (Path.GetDirectoryName(folder) is { } parentFolder)
                {
                    if (Path.GetFileName(parentFolder) == "Microsoft.NETCore.App")
                    {
                        version = Path.GetFileName(folder);
                    }
                }
            }
        }

        var tags = new (string Key, string Value)[]
        {
            ("language", "dotnet"),
            ("runtime_version", $"{flavor} {version}"),
            ("library_version", TracerConstants.AssemblyVersion)
        };

        var bag = new List<IntPtr>();

        try
        {
            var libraryName = Marshal.StringToHGlobalAnsi("dd-dotnet");
            bag.Add(libraryName);

            var libraryVersion = Marshal.StringToHGlobalAnsi(TracerConstants.AssemblyVersion);
            bag.Add(libraryVersion);

            var family = Marshal.StringToHGlobalAnsi("dotnet");
            bag.Add(family);

            var nativeTags = Marshal.AllocHGlobal(Marshal.SizeOf<ICrashReport.Tag>() * tags.Length);
            bag.Add(nativeTags);

            var destination = (ICrashReport.Tag*)nativeTags;

            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                var key = Marshal.StringToHGlobalAnsi(tag.Key);
                bag.Add(key);

                var value = Marshal.StringToHGlobalAnsi(tag.Value);
                bag.Add(value);

                destination[i] = new ICrashReport.Tag { Key = key, Value = value };
            }

            try
            {
                crashReport.SetMetadata(libraryName, libraryVersion, family, (ICrashReport.Tag*)nativeTags, tags.Length);
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
            foreach (var ptr in bag)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct ResolveMethodData
    {
        public ulong SymbolAddress;
        public ulong ModuleAddress;
        public bool IsSuspicious;
        public fixed byte Name[MethodNameMaxLength];
    }
}
