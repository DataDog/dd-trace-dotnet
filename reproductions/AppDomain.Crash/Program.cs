using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;
using AppDomain.Instance;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.TestHelpers;

namespace AppDomain.Crash
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting AppDomain Crash Test");

                Console.WriteLine($"Forcing load of {typeof(AdoNetIntegration).Assembly.FullName}");

                if (Instrumentation.ProfilerAttached == false)
                {
                    Console.WriteLine("Profiler is not attached!");
                    // throw new Exception("Profiler must be attached!");
                }
                else
                {
                    Console.WriteLine("Profiler is attached.");
                }

                var workers = new List<Thread>();
                var unloads = new List<Thread>();

                string commonFriendlyAppDomainName = "crash-dummy";
                int index = 1;

                var appPool = EnvironmentHelper.NonProfiledHelper(typeof(Program), "AppDomain.Crash", "reproductions");
                var appInstance = EnvironmentHelper.NonProfiledHelper(typeof(Program), "AppDomain.Instance", "reproduction-dependencies");

                var appPoolBin = appPool.GetSampleApplicationOutputDirectory();
                var instanceBin = appInstance.GetSampleApplicationOutputDirectory();

                var deployDirectory = Path.Combine(appPool.GetSampleProjectDirectory(), "ApplicationInstance", "AppDomain.Instance");


                var instanceType = typeof(AppDomainInstanceProgram);
                var instanceName = instanceType.FullName;

                System.AppDomain previousDomain = null;
                AppDomainInstanceProgram previousInstance = null;

                var domainsToInstantiate = 4;

                var usePermissionSet = true;

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

                while (domainsToInstantiate-- > 0)
                {
                    if (previousDomain != null)
                    {
                        System.AppDomain.Unload(previousDomain);
                    }

                    var appDomainName = $"{commonFriendlyAppDomainName}_{index}";

                    var setup = new AppDomainSetup
                    {
                        ApplicationBase = deployDirectory,
                        DisallowBindingRedirects = false,
                        DisallowCodeDownload = true
                    };

                    var permissionSet = new PermissionSet(PermissionState.Unrestricted);
                    
                    if (usePermissionSet)
                    {
                        permissionSet.AddPermission(new RegistryPermission(PermissionState.None));
                    }

                    var currentAppDomain = System.AppDomain.CreateDomain(appDomainName, System.AppDomain.CurrentDomain.Evidence, setup, permissionSet);
                    usePermissionSet = !usePermissionSet;

                    Console.WriteLine($"Created AppDomain root for #{index} - {commonFriendlyAppDomainName}");

                    var instanceOfProgram =
                        currentAppDomain.CreateInstanceAndUnwrap(
                            instanceType.Assembly.FullName,
                            instanceName) as AppDomainInstanceProgram;

                    Console.WriteLine($"Created AppDomain instance for #{index} - {instanceName}");

                    var argsToPass = new string[] { commonFriendlyAppDomainName, index.ToString() };

                    Console.WriteLine($"Starting instance #{index} - {instanceName}");

                    var domainWorker = new Thread(
                        () =>
                        {
                            instanceOfProgram.Main(argsToPass);
                        });

                    domainWorker.Start();

                    workers.Add(domainWorker);

                    // Give the domain some time to enjoy life
                    Thread.Sleep(10_000);

                    previousDomain = currentAppDomain;
                    previousInstance = instanceOfProgram;
                    index++;
                }

                while (workers.Any(w => w.IsAlive))
                {
                    Thread.Sleep(2000);
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
        }
    }
}
