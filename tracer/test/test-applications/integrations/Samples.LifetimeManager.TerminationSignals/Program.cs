using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Samples.LifetimeManager.TerminationSignals;

internal static class Program
{
    private const string ReadyMarker = "READY";
    private const string ShutdownMarker = "SHUTDOWN";
    private static readonly TimeSpan TracerAssemblyWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TracerAssemblyPollInterval = TimeSpan.FromMilliseconds(100);

    public static int Main(string[] args)
    {
        try
        {
            var shutdownFile = Environment.GetEnvironmentVariable("DD_LIFETIME_SHUTDOWN_FILE");
            RegisterShutdownHook(shutdownFile);

            WriteReady();
            Run(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup failed: {ex}");
            return 1;
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties, "Datadog.Trace.LifetimeManager", "Datadog.Trace")]
    private static void RegisterShutdownHook(string? shutdownFile)
    {
        var tracerAssembly = WaitForTracerAssembly();
        var lifetimeManagerType = tracerAssembly.GetType("Datadog.Trace.LifetimeManager", throwOnError: true);
        var instanceProperty = lifetimeManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        var addShutdownTaskMethod = lifetimeManagerType.GetMethod("AddShutdownTask", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Action<Exception?>) }, null);

        if (instanceProperty is null || addShutdownTaskMethod is null)
        {
            throw new InvalidOperationException("Unable to find LifetimeManager.Instance or AddShutdownTask.");
        }

        var instance = instanceProperty.GetValue(null);
        if (instance is null)
        {
            throw new InvalidOperationException("LifetimeManager.Instance returned null.");
        }

        Action<Exception?> hook = exception => WriteShutdown(shutdownFile, exception);
        addShutdownTaskMethod.Invoke(instance, new object[] { hook });
    }

    private static Assembly WaitForTracerAssembly()
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TracerAssemblyWaitTimeout)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                                     .FirstOrDefault(a => string.Equals(a.GetName().Name, "Datadog.Trace", StringComparison.Ordinal));
            if (assembly is not null)
            {
                return assembly;
            }

            Thread.Sleep(TracerAssemblyPollInterval);
        }

        throw new InvalidOperationException($"Datadog.Trace assembly not loaded within {TracerAssemblyWaitTimeout.TotalSeconds} seconds.");
    }

    private static void WriteReady()
    {
        Console.WriteLine(ReadyMarker);

        var readyFile = Environment.GetEnvironmentVariable("DD_LIFETIME_READY_FILE");
        if (!string.IsNullOrEmpty(readyFile))
        {
            WriteFile(readyFile, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    private static void Run(string[] args)
    {
        if (args.Length == 0 || string.Equals(args[0], "--wait", StringComparison.OrdinalIgnoreCase))
        {
            Thread.Sleep(Timeout.Infinite);
            return;
        }

        if (string.Equals(args[0], "--sleep", StringComparison.OrdinalIgnoreCase)
            && args.Length > 1
            && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var delayMs)
            && delayMs >= 0)
        {
            Thread.Sleep(delayMs);
            return;
        }

        Thread.Sleep(Timeout.Infinite);
    }

    private static void WriteShutdown(string? shutdownFile, Exception? exception)
    {
        Console.WriteLine(ShutdownMarker);
        if (string.IsNullOrEmpty(shutdownFile))
        {
            return;
        }

        var exceptionType = exception?.GetType().FullName ?? string.Empty;
        var line = $"shutdown|{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}|{exceptionType}";
        AppendLine(shutdownFile, line);
    }

    private static void WriteFile(string path, string contents)
    {
        EnsureDirectory(path);
        File.WriteAllText(path, contents);
    }

    private static void AppendLine(string path, string line)
    {
        EnsureDirectory(path);
        File.AppendAllText(path, line + Environment.NewLine);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
