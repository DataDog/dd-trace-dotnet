using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Console_
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0].StartsWith("crash"))
            {
                Ready();

                if (args[0] == "crash-native")
                {
                    NativeCrash();
                }

                var exception = args[0] == "crash-datadog" ? (Exception)new BadImageFormatException("Expected") : new InvalidOperationException("Expected");

                // Add an indirection to have a BCL type on the callstack, to properly test obfuscation
                void DoCrash()
                {
                    SynchronizationContext.SetSynchronizationContext(new DummySynchronizationContext());
                    IProgress<Exception> progress = new Progress<Exception>(DumpCallstackAndThrow);
                    progress.Report(exception);
                }

                if (args[0] == "crash-thread")
                {
                    var thread = new Thread(
                        () =>
                        {
                            Thread.CurrentThread.Name = "DD_thread";
                            DoCrash();
                        });

                    thread.Start();
                    thread.Join();
                }
                else
                {
                    DoCrash();
                }
            }
            else
            {
                AsyncMain(args).GetAwaiter().GetResult();
            }

            var fileToWatch = Environment.GetEnvironmentVariable("DD_INTERNAL_TEST_FILE_TO_WATCH");

            if (fileToWatch != null)
            {
                // Wait for up to 1 minute for the file to be created
                var start = DateTime.UtcNow;

                while (!File.Exists(fileToWatch) && (DateTime.UtcNow - start) < TimeSpan.FromMinutes(1))
                {
                    Thread.Sleep(500);
                }
            }
        }

        // Can't use a "real" async Main because it messes up the callstack for the crash-report tests
        private static async Task AsyncMain(string[] args)
        {
            // Just to make extra sure that the tracer is loaded, if properly configured
            _ = WebRequest.CreateHttp("http://localhost");
            await Task.Yield();

            Ready();

            if (args.Length > 0)
            {
                Console.WriteLine($"Args: {string.Join(" ", args)}");

                if (string.Equals(args[0], "traces", StringComparison.OrdinalIgnoreCase))
                {
                    var count = int.Parse(args[1]);

                    Console.WriteLine($"Sending {count} spans");

                    for (int i = 0; i < count; i++)
                    {
                        SampleHelpers.CreateScope("test").Dispose();
                    }

                    await SampleHelpers.ForceTracerFlushAsync();
                    return;
                }

                if (string.Equals(args[0], "echo", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Ready");
                    Console.WriteLine($"Echo: {Console.ReadLine()}");
                    return;
                }

                if (string.Equals(args[0], "wait", StringComparison.OrdinalIgnoreCase))
                {
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DumpCallstackAndThrow(Exception exception)
        {
            var stackTrace = new StackTrace();

            foreach (var frame in stackTrace.GetFrames())
            {
                var method = frame.GetMethod();

                // ClrMD has a different representation of generics
                var declaringType = method.DeclaringType.FullName;
                var methodName = method.Name;

                var symbol = method.Module.Assembly == typeof(Program).Assembly ? "REDACTED" : $"{declaringType}.{methodName}";

                // .NET and ClrMD reports generics with a different syntax
                symbol = symbol.Replace("Progress`1", "Progress<System.__Canon>");

                Console.WriteLine($"Frame|{Path.GetFileName(method.Module.Assembly.Location)}!{symbol}");
            }

            Console.WriteLine("Crashing...");
            throw exception;
        }

        private static void Ready()
        {
            Console.WriteLine($"Waiting - PID: {Process.GetCurrentProcess().Id} - Profiler attached: {SampleHelpers.IsProfilerAttached()}");
        }

        private static unsafe void NativeCrash()
        {
            var iunknown = CreateCrashReport(0);

            // Get the QueryInterface method
            var vtable = *(IntPtr**)iunknown;
            var queryInterface = (delegate* unmanaged[Stdcall]<IntPtr, in Guid, out IntPtr, int>)*vtable;

            // Fetch the ICrashReport interface
            var guid = new Guid("3B3BA8A9-F807-43BF-A3A9-55E369C0C532");
            var result = queryInterface(iunknown, guid, out var crashReport);

            if (result != 0)
            {
                throw new Win32Exception(result, "Failed to get ICrashReport");
            }

            // Get the CrashProcess method
            var crashReportVtable = *(IntPtr**)crashReport;
            var crashProcess = (delegate* unmanaged[Stdcall]<IntPtr, int>)*(crashReportVtable + 11);

            Console.WriteLine("Crashing... (native)");

            // Here comes nothing
            result = crashProcess(crashReport);

            // We're not supposed to be alive :(
            throw new Exception("Failed to fail: " + result);
        }

        private static unsafe IntPtr CreateCrashReport(int pid)
        {
            var nativeLibraryType = Type.GetType("Datadog.Trace.AppSec.Waf.NativeBindings.NativeLibrary, Datadog.Trace", throwOnError: true);
            var tryLoad = nativeLibraryType.GetMethod("TryLoad", BindingFlags.NonPublic | BindingFlags.Static);
            var getExport = nativeLibraryType.GetMethod("GetExport", BindingFlags.NonPublic | BindingFlags.Static);
            
            var folder = Path.GetDirectoryName(Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH"));
            var profilerPath = Path.Combine(folder, "Datadog.Profiler.Native" + (Environment.OSVersion.Platform == PlatformID.Win32NT ? ".dll" : ".so"));
            
            var arguments = new object[] { profilerPath, null };
            tryLoad.Invoke(null, arguments);

            var handle = (IntPtr)arguments[1];

            var createCrashReport = (IntPtr)getExport.Invoke(null, [handle, "CreateCrashReport"]);

            return ((delegate* unmanaged[Stdcall]<int, IntPtr>)createCrashReport)(pid);
        }

        private class DummySynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) => d(state);

            public override void Send(SendOrPostCallback d, object state) => d(state);
        }
    }
}
