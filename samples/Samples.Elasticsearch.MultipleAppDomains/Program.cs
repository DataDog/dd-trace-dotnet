using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using AppDomain.Instance;
using Nest;


namespace Samples.Elasticsearch.MultipleAppDomains
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            List<Thread> threads = new List<Thread>();
            int index = 0;

            PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
            System.AppDomain appDomain1 = CreateAndRunAppDomain(index++, ps);
            System.AppDomain appDomain2 = CreateAndRunAppDomain(index++, ps);
        }

        private static System.AppDomain CreateAndRunAppDomain(int index, PermissionSet grantSet)
        {
            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = System.AppDomain.CurrentDomain.BaseDirectory;

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile =
                System.AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

            string name = "AppDomain" + index;
            System.AppDomain appDomain1 = System.AppDomain.CreateDomain(
                name,
                System.AppDomain.CurrentDomain.Evidence,
                ads,
                grantSet);
            AppDomainInstanceProgram programInstance1 = (AppDomainInstanceProgram)appDomain1.CreateInstanceAndUnwrap(
                typeof(AppDomainInstanceProgram).Assembly.FullName,
                typeof(AppDomainInstanceProgram).FullName);
            var argsToPass = new string[] { name, index.ToString(), "Elasticsearch" };
            programInstance1.Main(argsToPass);

            Console.WriteLine("**********************************************");
            Console.WriteLine($"Finished executing in AppDomain {name}");
            Console.WriteLine("**********************************************");
            return appDomain1;
        }
    }
}
