using System;
using System.IO;
using System.Net;
using System.Security;
using System.Security.Permissions;
using System.Threading;

namespace Samples.MultiDomainHost.Runner
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static int Main(string[] args)
        {
            Console.WriteLine("Loading System.dll early in the process");

            using (var webClient = new WebClient())
            {
            }

            try
            {
                // First load the application that does not have bindingRedirect policies
                // This will instrument at least one domain-neutral assembly with a
                // domain-neutral version of Datadog.Trace.dll
                CreateAndRunAppDomain("Samples.MultiDomainHost.App.FrameworkHttpNoRedirects");

                // Next load an application that does the same thing, still does not have
                // bindingRedirect policies, but it uses the System.Net.Http NuGet package
                // instead of the built-in version.
                CreateAndRunAppDomain("Samples.MultiDomainHost.App.NuGetHttpNoRedirects");

                // Next load the application that has a bindingRedirect policy on Newtonsoft.Json.
                // This will cause a sharing violation when the domain-neutral assembly attempts
                // to call into Datadog.Trace.dll because Datadog.Trace.dll
                // can no longer be loaded shared with the bindingRedirect policy in place. This breaks
                // the consistency check of all domain-neutral assemblies only depending on other
                // domain-neutral assemblies
                CreateAndRunAppDomain("Samples.MultiDomainHost.App.NuGetJsonWithRedirects");

                // Next load the application that has a bindingRedirect policy on System.Net.Http.
                // This will cause a sharing violation when the domain-neutral assembly attempts
                // to call into Datadog.Trace.dll because Datadog.Trace.dll
                // can no longer be loaded shared with the bindingRedirect policy in place. This breaks
                // the consistency check of all domain-neutral assemblies only depending on other
                // domain-neutral assemblies
                CreateAndRunAppDomain("Samples.MultiDomainHost.App.NuGetHttpWithRedirects");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        private static void CreateAndRunAppDomain(string appName)
        {
            AppDomain domain = null;

            try
            {
                // Construct and initialize settings for a second AppDomain.
                AppDomainSetup ads = new AppDomainSetup();
                ads.ApplicationBase = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, appName);

                ads.DisallowBindingRedirects = false;
                ads.DisallowCodeDownload = true;
                ads.ConfigurationFile =
                    Path.Combine(ads.ApplicationBase, appName + ".exe.config");

                PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
                domain = System.AppDomain.CreateDomain(
                    appName,
                    System.AppDomain.CurrentDomain.Evidence,
                    ads,
                    ps);

                Console.WriteLine("**********************************************");
                Console.WriteLine($"Starting code execution in AppDomain {appName}");
                domain.ExecuteAssemblyByName(
                    appName,
                    new string[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
            finally
            {
                Console.WriteLine($"Finished code execution in AppDomain {appName}");
                Console.WriteLine("**********************************************");
                Console.WriteLine();

                if (domain != null)
                {
                    // Wait for traces to be flushed, then unload the domain
                    Thread.Sleep(2000);
                    AppDomain.Unload(domain);
                }
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
