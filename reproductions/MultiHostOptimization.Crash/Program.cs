using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace MultiHostOptimization.Crash
{
    public class Program
    {
        private static ConcurrentQueue<string> _consoleMessages = new ConcurrentQueue<string>();
        private static ManualResetEventSlim _gate = new ManualResetEventSlim(initialState: false);
        private static System.AppDomain appDomain1;
        private static System.AppDomain appDomain2;

        // [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            List<Thread> threads = new List<Thread>();
            int index = 0;

            _gate.Reset();

            PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
            Func<Task> runHigherVersionDomain = () =>
            {
                _consoleMessages.Enqueue($"{DateTime.Now.Ticks} - Waiting to start higher version AppDomain");
                // _gate.Wait();
                appDomain1 = CreateAndRunAppDomain(index++, ps, "HigherVersions.AppDomain");
                return Task.FromResult(0);
            };
            Func<Task> runLowerVersionDomain = () =>
            {
                _consoleMessages.Enqueue($"{DateTime.Now.Ticks} - Waiting to start lower version AppDomain");
                // _gate.Wait();
                appDomain2 = CreateAndRunAppDomain(index++, ps, "LowerVersions.AppDomain");
                return Task.FromResult(0);
            };

            var domainTasks = new List<Task>
            {
                Task.Run(runHigherVersionDomain),
                Task.Run(runLowerVersionDomain),
            };

            _gate.Set();

            Task.WaitAll(domainTasks.ToArray());

            while (_consoleMessages.TryDequeue(out var message))
            {
                Console.WriteLine(message);
            }

            var faultedTasks = domainTasks.Where(t => t.IsFaulted).ToList();
            if (faultedTasks.Any())
            {
                foreach (var faultedTask in faultedTasks)
                {
                    Console.WriteLine(faultedTask.Exception.ToString());
                }
            }
        }

        private static System.AppDomain CreateAndRunAppDomain(int index, PermissionSet grantSet, string appName)
        {
            Console.WriteLine($"{DateTime.Now.Ticks} - Starting AppDomain {appName}");

            var applicationBase =
                Path.Combine(
                    EnvironmentHelper.GetSolutionDirectory(),
                    "reproduction-dependencies",
                    appName,
                    "bin",
                    "x64",
                    "Debug",
                    "net461",
                    "win-x64");

            var applicationPath = Path.Combine(applicationBase, $"{appName}.dll");

            if (!File.Exists(applicationPath))
            {
                throw new Exception("Application not found at: " + applicationPath);
            }

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup
            {
                ApplicationBase = applicationBase,
                // ConfigurationFile = System.AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                // DisallowBindingRedirects = false,
                // DisallowCodeDownload = true,
            };

            var name = $"{appName}";
            var appDomain = System.AppDomain.CreateDomain(
                name,
                System.AppDomain.CurrentDomain.Evidence,
                ads,
                grantSet);

            var actualAssemblyName =
                typeof(DogServer.Shared.DogServer)
                   .Assembly
                   .FullName
                   .Replace("DogServer.Shared", appName);

            var dogServer = (DogServer.Shared.DogServer)appDomain.CreateInstanceAndUnwrap(
                actualAssemblyName,
                $"{appName}.AppDomainInstanceProgram");

            var argsToPass = new string[] { name, index.ToString() };
            dogServer.StartServer(argsToPass);
            Console.WriteLine($"{DateTime.Now.Ticks} - Finished AppDomain {appName}: {dogServer.ServerInstanceId}");
            return appDomain;
        }
    }
}
