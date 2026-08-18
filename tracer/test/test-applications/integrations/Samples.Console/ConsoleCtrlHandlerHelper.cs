#if NETFRAMEWORK

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Samples.Console_;

public class ConsoleCtrlHandlerHelper
{
    /// <summary>
    /// Scenarios for the System.Console.ControlCHooker crash (exit code 0xE0434352) that the tracer used to
    /// trigger by subscribing to Console.CancelKeyPress. See Datadog.Trace.PlatformHelpers.ConsoleControlHandler.
    /// </summary>
    public static void RunConsoleCtrlScenario(string mode)
    {
        if (string.Equals(mode, "console-ctrl-break-child", StringComparison.OrdinalIgnoreCase))
        {
            RunCtrlBreakChild();
            return;
        }

        if (string.Equals(mode, "console-ctrl-break-flush", StringComparison.OrdinalIgnoreCase))
        {
            RunCtrlBreakParent();
            return;
        }

        // Make sure the tracer is fully initialized, so that LifetimeManager has registered its Ctrl+C
        // handling before we do anything else.
        SampleHelpers.CreateScope("console-ctrl").Dispose();

        if (string.Equals(mode, "console-ctrl-resubscribe", StringComparison.OrdinalIgnoreCase))
        {
            // An application that manages CancelKeyPress correctly. With nothing else in
            // Console._cancelCallbacks, removing this handler empties the list, so the BCL reaches
            // ControlCHooker.Unhook() while the OS handler list is still healthy, it succeeds, and the critical
            // finalizer is permanently disarmed. A tracer sitting in that list blocks the disarm.
            ConsoleCancelEventHandler handler = (sender, e) => { };
            Console.CancelKeyPress += handler;
            Console.CancelKeyPress -= handler;
        }

        if (string.Equals(mode, "console-ctrl-control", StringComparison.OrdinalIgnoreCase))
        {
            // Deliberately no console transition, so the OS handler list stays healthy and nothing can throw.
            Console.WriteLine("Skipping the console transition");
            return;
        }

        ResetConsoleCtrlHandlerList();

        // Returning from Main runs ProcessExit and then finalizers, which is where
        // ControlCHooker.Finalize() throws its uncatchable IOException if it is still armed.
        SafeWriteLine("Returning from Main");
    }

    /// <summary>
    /// Harness half of the Ctrl+Break flush scenario.
    /// <para>
    /// The tracer registers its console control handler from Instrumentation.Initialize(), i.e. before Main runs,
    /// so a console transition in *this* process would wipe that registration and the scenario would prove
    /// nothing. Instead this process becomes a harness: it takes a private console, makes itself immune to control
    /// events, and starts a child that inherits that console without ever touching it. The child's registration is
    /// therefore intact when the event arrives.
    /// </para>
    /// <para>
    /// Taking the private console first is what stops the event reaching the test host that started us. Process
    /// group 0 means "every process attached to the calling process's console", so that ordering is load bearing.
    /// </para>
    /// </summary>
    private static void RunCtrlBreakParent()
    {
        var markerFile = Environment.GetEnvironmentVariable("DD_INTERNAL_TEST_SHUTDOWN_MARKER_FILE");

        if (string.IsNullOrEmpty(markerFile))
        {
            throw new InvalidOperationException("DD_INTERNAL_TEST_SHUTDOWN_MARKER_FILE must be set for console-ctrl-break-flush");
        }

        // Force Console.Out to bind to the redirected pipe now. AllocConsole reinitializes the process's standard
        // handles, so anything written after it would otherwise land in the invisible console we are about to
        // create, and the test would see no diagnostics at all.
        Console.WriteLine($"Ctrl+Break harness starting, profiler attached: {SampleHelpers.IsProfilerAttached()}");
        Console.Out.Flush();

        ResetConsoleCtrlHandlerList();

        // Registered *after* the transition, so it survives it. Returning true means "handled", which is how the
        // harness stays alive to report the child's exit code while the child is terminated.
        SafeWriteLine($"SetConsoleCtrlHandler(survive) = {SetConsoleCtrlHandler(SurviveCtrlBreak, true)}");

        var output = new StringBuilder();
        // Run the sample sample with a different scenario provided
        var startInfo = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, "console-ctrl-break-child")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using (var child = new Process { StartInfo = startInfo })
        {
            child.OutputDataReceived += (_, e) => AppendLine(output, e.Data);
            child.ErrorDataReceived += (_, e) => AppendLine(output, e.Data);

            child.Start();
            child.BeginOutputReadLine();
            child.BeginErrorReadLine();

            SafeWriteLine($"Started child process {child.Id}");

            if (!WaitForFile(ReadyFileFor(markerFile), TimeSpan.FromSeconds(60)))
            {
                TryKill(child);
                SafeWriteLine(output.ToString());
                throw new TimeoutException("The child process never signalled that the tracer was initialized.");
            }

            SafeWriteLine($"GenerateConsoleCtrlEvent(CTRL_BREAK_EVENT) = {GenerateConsoleCtrlEvent(CtrlBreakEvent, 0)}");

            if (!child.WaitForExit(60_000))
            {
                TryKill(child);
                SafeWriteLine(output.ToString());
                throw new TimeoutException("The child process was not terminated by the control event.");
            }

            // Written to a file rather than asserted here, so that the test owns the assertion and can report the
            // code it actually saw. 0xE0434352 would mean ControlCHooker's critical finalizer threw.
            File.WriteAllText(ExitCodeFileFor(markerFile), child.ExitCode.ToString());
            SafeWriteLine($"Child exit code: 0x{child.ExitCode:X8}");
        }

        SafeWriteLine("Child output:");
        SafeWriteLine(output.ToString());
    }

    /// <summary>
    /// Subject half of the Ctrl+Break flush scenario: creates a span, arms a marker, and waits to be killed. It
    /// deliberately performs no console transition, so the tracer's control handler registration stays valid.
    /// </summary>
    private static void RunCtrlBreakChild()
    {
        var markerFile = Environment.GetEnvironmentVariable("DD_INTERNAL_TEST_SHUTDOWN_MARKER_FILE");

        // Note: no ForceTracerFlushAsync, so this span can only reach the agent via the shutdown hooks that our
        // console control handler is responsible for running.
        SampleHelpers.CreateScope("console-ctrl").Dispose();
        SampleHelpers.AddLifetimeManagerTask(_ => File.WriteAllText(markerFile, "shutdown"));

        Console.WriteLine($"Child ready, profiler attached: {SampleHelpers.IsProfilerAttached()}, ControlCHooker: {DescribeControlCHooker()}");
        Console.Out.Flush();

        File.WriteAllText(ReadyFileFor(markerFile), "ready");

        // We expect to be killed well before this elapses.
        Thread.Sleep(TimeSpan.FromSeconds(60));
        Console.WriteLine("Still alive: the control event was never delivered");
    }

    private static string ReadyFileFor(string markerFile) => markerFile + ".ready";

    private static string ExitCodeFileFor(string markerFile) => markerFile + ".exitcode";

    private static bool WaitForFile(string path, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (line == null)
        {
            return;
        }

        lock (builder)
        {
            builder.AppendLine(line);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // Best effort
        }
    }

    /// <summary>
    /// Resets the process's console control handler list, which is what arms ControlCHooker's critical finalizer.
    /// FreeConsole only resets the list when a console is attached, and AllocConsole fails with
    /// ERROR_ACCESS_DENIED (5) when one already is, so we do both and report what happened.
    /// <para>
    /// AllocConsole reinitializes the process's standard handles, so anything that needs to reach a redirected
    /// stdout must have written at least once before this runs: .NET caches the TextWriter it built over the
    /// original pipe handle, and the pipe itself is unaffected by either call.
    /// </para>
    /// </summary>
    private static void ResetConsoleCtrlHandlerList()
    {
        var freed = FreeConsole();
        var freeError = Marshal.GetLastWin32Error();
        var allocated = AllocConsole();
        var allocError = Marshal.GetLastWin32Error();

        SafeWriteLine($"FreeConsole() = {freed} (Win32 {freeError}), AllocConsole() = {allocated} (Win32 {allocError})");

        if (!freed && !allocated)
        {
            SafeWriteLine("WARNING: neither call succeeded, so the handler list was NOT reset and this scenario proves nothing");
        }
    }

    /// <summary>
    /// Reports the state of System.Console's ControlCHooker, so that a scenario which fails to arm it is visible
    /// in the test output instead of passing vacuously.
    /// </summary>
    private static string DescribeControlCHooker()
    {
        try
        {
            var hooker = typeof(Console).GetField("_hooker", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

            if (hooker == null)
            {
                return "not created";
            }

            var hooked = hooker.GetType().GetField("_hooked", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hooker);
            return $"created, _hooked={hooked}";
        }
        catch (Exception ex)
        {
            return $"unknown ({ex.GetType().Name})";
        }
    }

    /// <summary>
    /// Writing to the console can fail once FreeConsole has detached us from it, if stdout was not redirected.
    /// </summary>
    private static void SafeWriteLine(string message)
    {
        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // Nothing we can do, and nothing worth crashing the scenario over.
        }
    }

    private const uint CtrlBreakEvent = 1;

    private delegate bool ConsoleCtrlHandlerRoutine(uint controlType);

    /// <summary>
    /// Rooted in a static field because the OS holds a raw pointer to this delegate's marshalling stub.
    /// Returning true means "handled", so the default terminating handler never runs for this process.
    /// </summary>
    private static readonly ConsoleCtrlHandlerRoutine SurviveCtrlBreak = _ => true;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, int dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerRoutine handlerRoutine, bool add);
}

#endif
