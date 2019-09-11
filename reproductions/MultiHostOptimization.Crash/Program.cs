using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AppDomain.Instance;
using Datadog.Trace.TestHelpers;

namespace MultiHostOptimization.Crash
{
    public class Program
    {
        private static ManualResetEventSlim _gate = new ManualResetEventSlim(initialState: false);
        private static System.AppDomain appDomain1;
        private static System.AppDomain appDomain2;

        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            List<Thread> threads = new List<Thread>();
            int index = 0;

            PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
            Func<Task> runFirst = () =>
            {
                _gate.Wait();
                appDomain1 = CreateAndRunAppDomain(index++, ps, "HigherVersion.WithNoRef");
                return Task.FromResult(0);
            };
            Func<Task> runSecond = () =>
            {
                _gate.Wait();
                appDomain2 = CreateAndRunAppDomain(index++, ps, "LowerVersion.WithNuget");
                return Task.FromResult(0);
            };

            var firstTask = runFirst();
            var secondTask = runSecond();

            Task.WaitAll(firstTask, secondTask);

            if (firstTask.IsFaulted)
            {
                throw firstTask.Exception;
            }

            if (secondTask.IsFaulted)
            {
                throw secondTask.Exception;
            }
        }

        private static System.AppDomain CreateAndRunAppDomain(int index, PermissionSet grantSet, string appName)
        {
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

            var programInstance = appDomain.CreateInstanceAndUnwrap(
                appName,
                $"{appName}.AppDomainInstanceProgram");

            var argsToPass = new object[] { name, index.ToString() };

            var programType = programInstance.GetType();
            var mainMethod = 
                programType
                   .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                   .Single(m => m.Name == "Main");

            mainMethod.Invoke(programInstance, argsToPass);

            Console.WriteLine("**********************************************");
            Console.WriteLine($"Finished executing in AppDomain {name}");
            Console.WriteLine("**********************************************");
            return appDomain;
        }
    }
}
