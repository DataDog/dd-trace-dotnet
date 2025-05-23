// <copyright file="CreatedumpCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Configuration;
using Microsoft.Diagnostics.Runtime;
using Spectre.Console;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class CreatedumpCommand : Command
{
    private const int SymbolNameMaxLength = 1024;

    private const string EnvironmentForceCrash = "DD_INTERNAL_CRASHTRACKING_CRASH";
    private const string EnvironmentPassThrough = "DD_INTERNAL_CRASHTRACKING_PASSTHROUGH";
    private const string EnvironmentOutput = "DD_INTERNAL_CRASHTRACKING_OUTPUT";
    private const string EnvironmentFilteringEnabled = "DD_CRASHTRACKING_FILTERING_ENABLED";
    private const string EnvironmentLogToConsole = "DD_CRASHTRACKING_INTERNAL_LOG_TO_CONSOLE";

    private static readonly List<string> Errors = new();
    private static ClrRuntime? _runtime;
    private static DataTarget? _dataTarget;
    private static IntPtr _oldUnhandledExceptionFilter;

    private static bool? _debugOutputEnabled;

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

    internal static bool ParseArguments(string[] arguments, out int pid, out int? signal, out int? crashThread)
    {
        pid = default;
        signal = default;
        crashThread = default;

        // Parse the createdump command-line
        // Unfortunately, the pid is not necessarily at the beginning or the end, it can be between other arguments.
        // It can get tricky when arguments have values, because unless we know the argument there is no sure way
        // to figure out if whatever follows is the value of the argument or the pid.
        // For instance:
        // $ createdump -a 456 -b 789
        // Is 456 the value of a and 789 the pid, or is 456 the pid and 789 the value of b?

        // First, construct the list of known arguments. With those, at least, we can't go wrong.
        // https://github.com/dotnet/runtime/blob/99dd60d8886155bb60768c8ef5a5a3225ad95ba4/src/coreclr/debug/createdump/createdumpmain.cpp#L19-L37
        // and https://github.com/dotnet/runtime/blob/99dd60d8886155bb60768c8ef5a5a3225ad95ba4/src/coreclr/pal/src/thread/process.cpp#L2499-L2519
        var knownArguments = new Dictionary<string, bool>
        {
            { "-f", true },
            { "--name", true },
            { "-n", false },
            { "--normal", false },
            { "-h", false },
            { "--withheap", false },
            { "-t", false },
            { "--triage", false },
            { "-u", false },
            { "--full", false },
            { "-d", false },
            { "--diag", false },
            { "-v", false },
            { "--verbose", false },
            { "-l", true },
            { "--logtofile", true },
            { "--creashreport", true },
            { "--crashreportonly", false },
            { "--crashthread", true },
            { "--signal", true },
            { "--singlefile", false },
            { "--nativeaot", false },
            { "--code", true },
            { "--errno", true },
            { "--address", true }
        };

        const string pidRegex = "[0-9]+";

        // The values that might be a pid, that we need to disambiguate
        var pidCandidates = new List<string>();

        var parsedArguments = new Dictionary<string, string?>();
        var queue = new Queue<string>(arguments);

        while (queue.Count > 0)
        {
            var argument = queue.Dequeue();

            if (knownArguments.TryGetValue(argument, out var hasValue))
            {
                // The easy case: we know the argument, so we know if whatever follows is its value
                string? value = null;

                if (hasValue)
                {
                    queue.TryDequeue(out value);
                }

                parsedArguments[argument] = value;
                continue;
            }

            if (argument.StartsWith('-'))
            {
                // Unknown argument :(
                // We don't know if the argument is supposed to have a value or not, so we peek at the next token
                // and try to figure it out
                if (queue.TryPeek(out var value))
                {
                    if (value.StartsWith('-'))
                    {
                        // Probably another argument
                        parsedArguments[argument] = null;
                        continue;
                    }

                    // It's either the pid or the value of the argument
                    _ = queue.Dequeue();

                    if (Regex.IsMatch(value, pidRegex))
                    {
                        pidCandidates.Add(value);
                    }

                    parsedArguments[argument] = value;
                }
                else
                {
                    parsedArguments[argument] = null;
                }

                continue;
            }

            // Unmatched value not following an argument, it is very likely to be the pid
            if (Regex.IsMatch(argument, pidRegex))
            {
                // Add it at the beginning of the candidates, as it's almost certainly the pid
                pidCandidates.Insert(0, argument);
            }
        }

        if (parsedArguments.TryGetValue("--signal", out var rawSignal) && int.TryParse(rawSignal, out var signalValue))
        {
            signal = signalValue;
        }

        if (parsedArguments.TryGetValue("--crashthread", out var rawCrashthread) && int.TryParse(rawCrashthread, out var crashThreadValue))
        {
            crashThread = crashThreadValue;
        }

        // Have we found the pid?
        if (pidCandidates.Count == 1)
        {
            // Best case scenario
            return int.TryParse(pidCandidates[0], out pid);
        }

        // Check all the values to check if one is really a pid
        foreach (var candidate in pidCandidates)
        {
            if (int.TryParse(candidate, out pid))
            {
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById(pid);
                    return true;
                }
                catch
                {
                }
            }
        }

        return false;
    }

    private static void AddError(string error)
    {
        DebugPrint(error);
        Errors.Add(error);
    }

    private static void DebugPrint(string message)
    {
        // Note: the debug output won't be visible on Windows.
        // That's because crashtracking runs in a background process (WerFault.exe).
        // If that becomes a concern, it's worth considering using OutputDebugString instead.
        if (_debugOutputEnabled == null)
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentLogToConsole);
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                _debugOutputEnabled = true;
            }
            else
            {
                _debugOutputEnabled = false;
            }
        }

        if (_debugOutputEnabled == true)
        {
            AnsiConsole.WriteLine(message);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe int ResolveManagedCallstack(int threadId, IntPtr context, ResolveMethodData** managedCallstack, int* numberOfFrames)
    {
        try
        {
            DebugPrint($"Resolving managed callstack for thread {threadId}");

            *numberOfFrames = 0;

            if (_runtime == null)
            {
                AddError("ClrRuntime is not initialized");
                return 2;
            }

            var thread = _runtime.Threads.FirstOrDefault(t => t.OSThreadId == threadId);

            if (thread == null)
            {
                return 4;
            }

            var handle = GCHandle.FromIntPtr(context);

            if (!handle.IsAllocated || handle.Target is not List<IntPtr> bag)
            {
                return 5;
            }

            var frames = thread.EnumerateStackTrace()
                               .Where(f => f.Kind == ClrStackFrameKind.ManagedMethod)
                               .ToList();

            var nativeMemory = NativeMemory.Alloc((nuint)frames.Count, (nuint)sizeof(ResolveMethodData));
            bag.Add((IntPtr)nativeMemory);

            *managedCallstack = (ResolveMethodData*)nativeMemory;

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var resolvedFrame = *managedCallstack + i;

                resolvedFrame->Ip = frame.InstructionPointer;
                resolvedFrame->Sp = frame.StackPointer;

                string symbolName;

                if (frame.Method != null)
                {
                    resolvedFrame->SymbolAddress = frame.Method.NativeCode;
                    resolvedFrame->ModuleAddress = frame.Method.Type.Module.ImageBase;
                    resolvedFrame->IsSuspicious = IsMethodSuspicious(frame.Method);

                    var assemblyName = frame.Method.Type.Module.AssemblyName;
                    var methodName = ShouldRedactFrame(assemblyName) ? "REDACTED" : $"{frame.Method.Type}.{frame.Method.Name}";
                    symbolName = $"{Path.GetFileName(assemblyName)}!{methodName}";
                }
                else
                {
                    resolvedFrame->SymbolAddress = 0;
                    resolvedFrame->ModuleAddress = 0;
                    resolvedFrame->IsSuspicious = false;
                    symbolName = frame.FrameName ?? "<unknown>";
                }

                var length = Math.Min(Encoding.ASCII.GetByteCount(symbolName), SymbolNameMaxLength - 1); // -1 to save space for the null terminator
                var destination = new Span<byte>(resolvedFrame->Name, SymbolNameMaxLength);
                Encoding.ASCII.GetBytes(symbolName, destination);
                destination[length] = (byte)'\0';
            }

            *numberOfFrames = frames.Count;

            return 0;
        }
        catch (Exception ex)
        {
            AddError($"Error while resolving method: {ex.Message}");
            return 3;
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe int UnhandledExceptionFilter(IntPtr exceptionInfo)
    {
        // A lot of the logic is implemented on the native side (Datadog.Profiler.Native and libdatadog).
        // Unfortunately, native exceptions such as access violation can't be caught on the managed side.
        // It means that if the native side crashes, we don't have a chance to clean the ClrMD context,
        // which in turn means that the threads of the crashing process will remain suspended, preventing it from exiting.
        // To prevent that, we bypass .NET exception management and use SetUnhandledExceptionFilter to catch all exceptions.

        // We're running managed code in a completely unsupported state, so keep the amount of work to an absolute minimum.
        _dataTarget?.Dispose();

        DebugPrint("Unhandled native error");

        if (_oldUnhandledExceptionFilter != IntPtr.Zero)
        {
            var function = (delegate* unmanaged<IntPtr, int>)_oldUnhandledExceptionFilter;
            return function(exceptionInfo);
        }

        return 0; // EXCEPTION_CONTINUE_SEARCH
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr SetUnhandledExceptionFilter(IntPtr lpTopLevelExceptionFilter);

    private static bool ShouldRedactFrame(string? assemblyName)
    {
        if (assemblyName == null)
        {
            return false;
        }

        var fileName = Path.GetFileName(assemblyName);

        // It would be nice to get those names directly from the source-generated InstrumentationDefinitions.IsInstrumentedAssembly
        if (fileName.StartsWith("Datadog", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("System", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Azure", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("AWSSDK", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("AerospikeClient", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Amazon.Lambda.RuntimeSupport", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("amqmdnetstd", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Confluent.Kafka", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Couchbase.NetClient", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Elasticsearch.Net", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("GraphQL", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("GraphQL.SystemReactive", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Grpc.AspNetCore.Server", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Grpc.Core", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Grpc.Net.Client", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("HotChocolate.Execution", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("log4net", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("MongoDB.Driver.Core", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("MySql.Data", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("MySqlConnector", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("NLog", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("nunit.framework", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("OpenTelemetry.Api", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Oracle.DataAccess", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Oracle.ManagedDataAccess", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("RabbitMQ.Client", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("ServiceStack.Redis", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("StackExchange.Redis", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("StackExchange.Redis.StrongName", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("System", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("xunit.execution.desktop", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("xunit.execution.dotnet", StringComparison.OrdinalIgnoreCase)
         || fileName.StartsWith("Yarp.ReverseProxy", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsTelemetryEnabled()
    {
        var value = Environment.GetEnvironmentVariable(ConfigurationKeys.Telemetry.Enabled);

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
        {
            return false;
        }

        return true;
    }

    private static bool IsMethodSuspicious(ClrMethod method)
    {
        var assemblyName = Path.GetFileName(method.Type.Module.Name ?? string.Empty);

        if (assemblyName.StartsWith("Datadog", StringComparison.OrdinalIgnoreCase))
        {
            var typeName = method.Type.Name;

            if (typeName != null)
            {
                if (typeName.Contains("BlockingMiddleware"))
                {
                    return false;
                }

                // Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations.TaskContinuationGenerator`4.SyncCallbackHandler.<ContinuationAction>d__4.MoveNext()
                if (typeName.Contains("TaskContinuationGenerator")
                 && typeName.Contains("ContinuationAction")
                 && method.Name == "MoveNext")
                {
                    // Very likely to be an async continuation
                    return false;
                }
            }

            return true;
        }

        if (method.Type.Module.IsDynamic && assemblyName.StartsWith("DuckType"))
        {
            return true;
        }

        return false;
    }

    private void Execute(InvocationContext context)
    {
        DebugPrint($"dd-dotnet invoked with command-line: {Environment.CommandLine}");

        var allArguments = _allArguments.GetValue(context);

        try
        {
            if (IsTelemetryEnabled())
            {
                if (ParseArguments(allArguments, out var pid, out var signal, out var crashThread))
                {
                    GenerateCrashReport(pid, signal, crashThread);
                }
                else
                {
                    AddError($"Unable to parse the command-line arguments: {string.Join(" ", allArguments)}");
                }
            }
            else
            {
                DebugPrint("Telemetry is disabled, not generating crash report");
            }
        }
        catch (Exception ex)
        {
            AddError($"Unexpected exception: {ex}");
        }
        finally
        {
            if (Errors.Count > 0)
            {
                AnsiConsole.WriteLine("Datadog - Some errors occurred while analyzing the crash:");

                foreach (var error in Errors.Take(3))
                {
                    AnsiConsole.WriteLine($"- {error}");
                }

                if (Errors.Count > 3)
                {
                    AnsiConsole.WriteLine($"... and {Errors.Count - 3} more");
                }
            }

            // Note: if refactoring, make sure to dispose the ClrMD DataTarget before calling createdump,
            // otherwise the calls to ptrace from createdump will fail
            if (Environment.GetEnvironmentVariable(EnvironmentPassThrough) == "1")
            {
                if (allArguments.Length > 0)
                {
                    InvokeCreatedump(allArguments[0]);
                }
                else
                {
                    DebugPrint("Unable to call createdump, missing arguments");
                }
            }
            else
            {
                DebugPrint("No passthrough, exiting without calling createdump");
            }
        }

        DebugPrint("dd-dotnet exited normally");
    }

    private unsafe void GenerateCrashReport(int pid, int? signal, int? crashThread)
    {
        DebugPrint($"Generating crash report for pid {pid} (signal: {signal}, crashing thread id: {crashThread})");

        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dll" : "so";
        var profilerLibrary = $"Datadog.Profiler.Native.{extension}";

        IntPtr lib;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, profilerLibrary);
            DebugPrint($"Loading native library {path}");
            lib = NativeLibrary.Load(path);
        }
        catch (DllNotFoundException ex) when (ex.Message.Contains("GLIBC"))
        {
            AddError("The GLIBC version is too old");
            return;
        }

        var export = NativeLibrary.GetExport(lib, "CreateCrashReport");

        if (export == 0)
        {
            AddError("Failed to load the CreateCrashReport function");
            return;
        }

        // Do not register the unhandled exception filter if a debugger is attached,
        // because breakpoints rely on exceptions
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Debugger.IsAttached)
        {
            var unhandledExceptionFilter = (delegate* unmanaged<IntPtr, int>)&UnhandledExceptionFilter;
            _oldUnhandledExceptionFilter = SetUnhandledExceptionFilter((IntPtr)unhandledExceptionFilter);
        }

        DebugPrint($"Attaching to process {pid}");

        using var target = DataTarget.AttachToProcess(pid, suspend: true);
        _dataTarget = target;
        _runtime = target.ClrVersions[0].CreateRuntime();

        var function = (delegate* unmanaged<int, IntPtr>)export;

        var ptr = function(pid);

        if (ptr == IntPtr.Zero)
        {
            AddError("Failed to create crash report");
            return;
        }

        using var iunknown = NativeObjects.IUnknown.Wrap(ptr);

        int result = iunknown.QueryInterface(ICrashReport.Guid, out var crashReportPtr);

        if (result != 0)
        {
            AddError($"Failed to query interface: {result}");
            return;
        }

        using var crashReport = NativeObjects.ICrashReport.Wrap(crashReportPtr);

        try
        {
            DebugPrint("Initializing crash report");
            crashReport.Initialize();
        }
        catch (Win32Exception ex)
        {
            AddError($"Failed to initialize crash report: {GetLastError(crashReport, ex)}");
            return;
        }

        if (Environment.GetEnvironmentVariable(EnvironmentForceCrash) == "1")
        {
            AnsiConsole.WriteLine($"{EnvironmentForceCrash} is set, crashing on purpose...");

            try
            {
                crashReport.Panic();
            }
            catch
            {
                // The error isn't supposed to be catchable, it breaks the assumption the test is built on
                // Sleep forever to timeout so somebody will have a look
                AnsiConsole.WriteLine("Failed to crash the process, sleeping forever.");
                Thread.Sleep(Timeout.Infinite);
            }
        }

        if (crashThread == null)
        {
            var firstThreadWithException = _runtime.Threads.FirstOrDefault(t => t.CurrentException != null);

            if (firstThreadWithException != null)
            {
                crashThread = (int)firstThreadWithException.OSThreadId;
                DebugPrint($"Crashing thread is not set, using the first thread with an exception: {crashThread}");
            }
        }

        ClrException? exception = null;

        // Check if there's an exception on the crash thread
        if (crashThread != null)
        {
            exception = _runtime.Threads.FirstOrDefault(t => t.OSThreadId == crashThread.Value)?.CurrentException;
        }

        bool isSuspicious;

        var bag = new List<IntPtr>();
        var handle = GCHandle.Alloc(bag);

        try
        {
            DebugPrint("Resolving callstacks");
            var callback = (delegate* unmanaged<int, IntPtr, ResolveMethodData**, int*, int>)&ResolveManagedCallstack;
            crashReport.ResolveStacks(crashThread ?? 0, (IntPtr)callback, GCHandle.ToIntPtr(handle), out isSuspicious);
        }
        catch (Win32Exception ex)
        {
            AddError($"Failed to resolve stacks: {GetLastError(crashReport, ex)}");
            return;
        }
        finally
        {
            handle.Free();

            foreach (var nativeMemory in bag)
            {
                NativeMemory.Free((void*)nativeMemory);
            }
        }

        if (isSuspicious)
        {
            DebugPrint("Found a suspicious frame");
        }

        if (!isSuspicious && exception != null)
        {
            // The stacks aren't suspicious, but maybe the exception is
            var exceptionType = exception.Type.Name ?? string.Empty;

            var suspiciousExceptionTypes = new[]
            {
                "System.InvalidProgramException",
                "System.MissingFieldException",
                "System.MissingMemberException",
                "System.BadImageFormatException"
            };

            if (exceptionType.StartsWith("Datadog", StringComparison.OrdinalIgnoreCase) || suspiciousExceptionTypes.Contains(exceptionType))
            {
                isSuspicious = true;
            }
            else if (exceptionType == "System.TypeLoadException")
            {
                isSuspicious = exception.Message?.Contains("datadog", StringComparison.OrdinalIgnoreCase) == true
                    || exception.StackTrace.Any(f => f.Method != null && IsMethodSuspicious(f.Method));
            }

            if (isSuspicious)
            {
                DebugPrint($"Found a suspicious exception: {exceptionType}");
            }
            else
            {
                DebugPrint("Found no suspicious exception");
            }
        }

        if (!isSuspicious)
        {
            var filteringEnabled = Environment.GetEnvironmentVariable(EnvironmentFilteringEnabled) ?? string.Empty;

            if (filteringEnabled == "0"
                || filteringEnabled.Equals("false", StringComparison.OrdinalIgnoreCase)
                || filteringEnabled.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.WriteLine($"Datadog - The crash is not suspicious, but filtering has been disabled with {EnvironmentFilteringEnabled}, sending crash report...");
            }
            else
            {
                DebugPrint("The crash is not suspicious, not sending crash report");
                return;
            }
        }
        else
        {
            AnsiConsole.WriteLine("Datadog - The crash may have been caused by automatic instrumentation, sending crash report...");
        }

        if (signal.HasValue)
        {
            DebugPrint("Adding signal information to the crash report");
            _ = SetSignal(crashReport, signal.Value);
        }

        DebugPrint("Setting crash report metadata");
        _ = SetMetadata(crashReport, _runtime, exception);

        try
        {
            var outputFile = Environment.GetEnvironmentVariable(EnvironmentOutput);

            if (!string.IsNullOrEmpty(outputFile))
            {
                var path = IntPtr.Zero;

                try
                {
                    AnsiConsole.WriteLine($"Writing crash report to {outputFile}...");
                    path = Marshal.StringToHGlobalAnsi(outputFile);
                    crashReport.WriteToFile(path);
                }
                finally
                {
                    if (path != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(path);
                    }
                }
            }
            else
            {
                DebugPrint("Sending crash report");
                crashReport.Send();
            }
        }
        catch (Win32Exception ex)
        {
            AddError($"Failed to send crash report: {GetLastError(crashReport, ex)}");
            return;
        }

        AnsiConsole.WriteLine("Datadog - Crash report sent successfully");
        DebugPrint("Crash report generated successfully");
    }

    private void InvokeCreatedump(string createdumpPath)
    {
        if (File.Exists(createdumpPath))
        {
            var commandLineArgs = Environment.GetCommandLineArgs();

            if (commandLineArgs.Length <= 2)
            {
                // It should never happen, but who knows
                AnsiConsole.WriteLine($"Datadog - Unable to call createdump, missing arguments: {Environment.CommandLine}");
            }
            else
            {
                DebugPrint($"Calling createdump with arguments: {string.Join(" ", commandLineArgs[2..])}");
                System.Diagnostics.Process.Start(commandLineArgs[2], commandLineArgs[3..]).WaitForExit();
            }
        }
        else
        {
            DebugPrint($"Unable to call createdump, file not found: {createdumpPath}");
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
            AddError($"Failed to set signal info: {GetLastError(crashReport, ex)}");
            return false;
        }

        return true;
    }

    private unsafe bool SetMetadata(ICrashReport crashReport, ClrRuntime runtime, ClrException? exception)
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

        var tags = new (string Key, string Value)[] { ("language", "dotnet"), ("runtime_version", $"{flavor} {version}"), ("library_version", TracerConstants.AssemblyVersion) };

        if (exception != null)
        {
            tags = [.. tags, ("exception", exception.ToString())];
        }

        var bag = new List<IntPtr>();

        try
        {
            var libraryName = Marshal.StringToHGlobalAnsi("dd-dotnet");
            bag.Add(libraryName);

            var libraryVersion = Marshal.StringToHGlobalAnsi(TracerConstants.AssemblyVersion);
            bag.Add(libraryVersion);

            var family = Marshal.StringToHGlobalAnsi("dotnet");
            bag.Add(family);

            var nativeTags = Marshal.AllocHGlobal(sizeof(ICrashReport.Tag) * tags.Length);
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
                AddError($"Failed to set metadata: {GetLastError(crashReport, ex)}");
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
        public ulong Ip;
        public ulong Sp;
        public bool IsSuspicious;
        public fixed byte Name[SymbolNameMaxLength];
    }
}
