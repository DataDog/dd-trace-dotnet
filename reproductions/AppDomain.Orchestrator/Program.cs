using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using AppDomain.Instance;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.TestHelpers;

namespace AppDomain.Crash
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting AppDomain Crash Test Orchestrator");

                var workers = new List<Process>();
                var unloads = new List<Thread>();

                string commonFriendlyAppDomainName = "crash-dummy";
                int index = 1;

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

                while (processesToStart-- > 0)
                {


                    var exePath = Path.Combine(deployDirectory, "w3wp.exe");
                    var worker = Process.Start(exePath);

                    workers.Add(worker);

                    Thread.Sleep(8000);

                    index++;

                    if (workers.Count > 3)
                    {
                        SafeKillFirst(workers);
                    }
                }

                int cyclesWaiting = 0;

                while (workers.Any(w => !w.HasExited))
                {
                    cyclesWaiting++;

                    if (cyclesWaiting > 7)
                    {
                        SafeKillFirst(workers);
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

        private static void SafeKillFirst(List<Process> workers)
        {
            try
            {
                workers.FirstOrDefault(w => !w.HasExited)?.Kill();
            }
            catch (Exception killException)
            {
                Console.WriteLine($"Unable to kill process because: {killException.Message}");
            }
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
