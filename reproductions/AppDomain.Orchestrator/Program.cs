using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.TestHelpers;

namespace AppDomain.Orchestrator
{
    public class Program
    {
        private static ConcurrentQueue<Process> _workersToKill = new ConcurrentQueue<Process>();

        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting AppDomain Crash Test Orchestrator");

                var workers = new List<Process>();
                var unloads = new List<Thread>();

                var appPool = EnvironmentHelper.NonProfiledHelper(typeof(Program), "AppDomain.Orchestrator", "reproductions");
                var appInstance = EnvironmentHelper.NonProfiledHelper(typeof(Program), "AppDomain.Crash", "reproductions");

                var appPoolBin = appPool.GetSampleApplicationOutputDirectory();
                var instanceBin = appInstance.GetSampleApplicationOutputDirectory();

                var deployDirectory = Path.Combine(appPool.GetSampleProjectDirectory(), "ApplicationInstance", $"AppDomain.Crash");
                if (Directory.Exists(deployDirectory))
                {
                    // Start fresh
                    var files = Directory.GetFiles(deployDirectory);
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.CreateDirectory(deployDirectory);
                }

                XCopy(instanceBin, deployDirectory);

                var processesToStart = 10;
                var processesStarted = 0;

                while (processesToStart-- > 0)
                {
                    var exePath = Path.Combine(deployDirectory, "w3wp.exe");
                    Console.WriteLine("Starting a new process.");

                    var startInfo = new ProcessStartInfo(exePath)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = false
                    };

                    var worker = Process.Start(startInfo);
                    Console.WriteLine($"Started process #{++processesStarted}");

                    _workersToKill.Enqueue(worker);

                    Thread.Sleep(15_000);

                    if (_workersToKill.Count > 3)
                    {
                        Console.WriteLine("Killing a process.");
                        SafeKillFirst();
                    }
                }

                int cyclesWaiting = 0;

                while (workers.Any(w => !w.HasExited))
                {
                    cyclesWaiting++;

                    if (cyclesWaiting > 7)
                    {
                        Console.WriteLine("Killing a process.");
                        SafeKillFirst();
                    }

                    Thread.Sleep(3000);
                }

                Console.WriteLine("No crashes! All is well!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return -10;
            }

            return 0;
        }

        private static void LogStatus(Process worker)
        {
            Console.WriteLine($"Worker {worker.Id} exited with code {worker.ExitCode}");
            var error = worker.StandardError.ReadToEnd();
            if (string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"Worker {worker.Id} has no errors to report.");
            }
            else
            {
                Console.WriteLine($"Worker {worker.Id} exception: {error}");
            }
        }

        private static void SafeKillFirst()
        {
            if (_workersToKill.TryDequeue(out var worker))
            {
                try
                {

                    if (!worker.HasExited)
                    {
                        worker.Kill();
                    }
                }
                catch (Exception killException)
                {
                    Console.WriteLine($"Unable to kill process because: {killException.Message}");
                }
            }

            LogStatus(worker);
        }

        private static void XCopy(string sourceDirectory, string targetDirectory)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = "xcopy";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = "\"" + sourceDirectory + "\"" + " " + "\"" + targetDirectory + "\"" + @" /e /y /I";

            Process xCopy = null;

            try
            {
                xCopy = Process.Start(startInfo);
                xCopy.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"XCopy has failed: {ex.Message}");
                throw;
            }
            finally
            {
                xCopy?.Dispose();
            }

            // Shenanigans to trick the profiler so we don't need to mess with environment variables
            System.IO.File.Move(
                Path.Combine(targetDirectory, "AppDomain.Crash.exe"),
                Path.Combine(targetDirectory, "w3wp.exe"));
        }
    }
}
