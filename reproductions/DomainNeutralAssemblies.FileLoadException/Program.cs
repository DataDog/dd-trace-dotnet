using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;
using System.Web;

namespace DomainNeutralAssemblies.FileLoadException
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static int Main(string[] args)
        {
            try
            {
                CheckGAC();
                CreateAndRunAppDomain("DomainNeutralAssemblies.App.NoBindingRedirects");
                CreateAndRunAppDomain("DomainNeutralAssemblies.App.BindingRedirects");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        private static void CheckGAC()
        {
            if (!typeof(Datadog.Trace.ClrProfiler.Instrumentation).Assembly.GlobalAssemblyCache)
            {
                throw new Exception("Datadog.Trace.ClrProfiler.Managed was not loaded from the GAC. Ensure that the assembly and its dependencies are installed in the GAC before running.");
            }
        }

        private static void CreateAndRunAppDomain(string appName)
        {
            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, appName);

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile =
                Path.Combine(ads.ApplicationBase, appName + ".exe.config");

            string name = appName;
            PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
            System.AppDomain appDomain1 = System.AppDomain.CreateDomain(
                name,
                System.AppDomain.CurrentDomain.Evidence,
                ads,
                ps);

            Console.WriteLine("**********************************************");
            Console.WriteLine($"Starting code execution in AppDomain {name}");
            appDomain1.ExecuteAssemblyByName(
                appName,
                new string[0]);
            
            Console.WriteLine($"Finished code execution in AppDomain {name}");
            Console.WriteLine("**********************************************");
            Console.WriteLine();
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
