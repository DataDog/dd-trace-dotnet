using System;
using System.IO;
using System.Security;
using System.Security.Permissions;

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

                // First load the application that does not have bindingRedirect policies
                // This will instrument at least one domain-neutral assembly with a
                // domain-neutral version of Datadog.Trace.ClrProfiler.Managed.dll
                CreateAndRunAppDomain("DomainNeutralAssemblies.App.NoBindingRedirects");

                // Next load the application that has a bindingRedirect policy on System.Net.Http.
                // This will cause a sharing violation when the domain-neutral assembly attempts
                // to call into Datadog.Trace.ClrProfiler.Managed.dll because Datadog.Trace.ClrProfiler.Managed.dll
                // can no longer be loaded shared with the bindingRedirect policy in place. This breaks
                // the consistency check of all domain-neutral assemblies only depending on other
                // domain-neutral assemblies
                CreateAndRunAppDomain("DomainNeutralAssemblies.App.HttpBindingRedirects");
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
