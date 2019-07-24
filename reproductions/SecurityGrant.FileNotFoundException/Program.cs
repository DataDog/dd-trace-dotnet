using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Threading;
using System.Web;
using AppDomain.Instance;

namespace SecurityGrant.FileNotFoundException
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            List<Thread> threads = new List<Thread>();
            int index = 0;

            PermissionSet ps = new PermissionSet(PermissionState.Unrestricted);
            // PermissionSet ps1 = new PermissionSet(PermissionState.None);
            // ps1.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
            // ps1.AddPermission(new System.Data.SqlClient.SqlClientPermission(PermissionState.Unrestricted));
            var thread = CreateAndRunAppDomain("AppDomain1", index++, ps);
            threads.Add(thread);
            thread.Start();

            // PermissionSet ps2 = new PermissionSet(PermissionState.None);
            // ps2.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
            // ps2.AddPermission(new System.Data.SqlClient.SqlClientPermission(PermissionState.Unrestricted));
            ps.AddPermission(new RegistryPermission(PermissionState.None));
            thread = CreateAndRunAppDomain("AppDomain2", index++, ps);
            threads.Add(thread);
            thread.Start();

            while (threads.Any(t => t.IsAlive))
            {
                Thread.Sleep(1000);
            }
            Console.WriteLine("IT'S DONE");
        }

        private static Thread CreateAndRunAppDomain(string name, int index, PermissionSet grantSet)
        {
            return new Thread(
                () =>
                {
                    // Construct and initialize settings for a second AppDomain.
                    AppDomainSetup ads = new AppDomainSetup();
                    ads.ApplicationBase = System.AppDomain.CurrentDomain.BaseDirectory;

                    ads.DisallowBindingRedirects = false;
                    ads.DisallowCodeDownload = true;
                    ads.ConfigurationFile =
                        System.AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

                    System.AppDomain appDomain1 = System.AppDomain.CreateDomain(
                        name,
                        System.AppDomain.CurrentDomain.Evidence,
                        ads,
                        grantSet);
                    // appDomain1.SetData("ALLOW_LOCALDB_IN_PARTIAL_TRUST", true);
                    AppDomainInstanceProgram programInstance1 = (AppDomainInstanceProgram)appDomain1.CreateInstanceAndUnwrap(
                        typeof(AppDomainInstanceProgram).Assembly.FullName,
                        typeof(AppDomainInstanceProgram).FullName);
                    var argsToPass = new string[] { name, index.ToString() };
                    programInstance1.Main(argsToPass);
                    System.AppDomain.Unload(appDomain1);

                    Console.WriteLine($"Execution was successful from AppDomain: {name}");
                });
        }

        public class AppDomainPreProgram : MarshalByRefObject
        {
            public void Run()
            {

            }
        }
    }
}
