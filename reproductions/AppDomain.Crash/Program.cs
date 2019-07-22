using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using Datadog.Trace.TestHelpers;

namespace AppDomain.Crash
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting AppDomain Crash Test");

                List<Thread> workers = new List<Thread>();

                string commonFriendlyAppDomainName = "crash-dummy";
                int index = 1;

                var mainApplicationHelper = new EnvironmentHelper(
                    sampleName: "AppDomain.Crash",
                    anchorType: typeof(Program),
                    output: null,
                    samplesDirectory: "reproductions",
                    prependSamplesToAppName: false,
                    requiresProfiling: false);

                var instanceDirectory = "C:\\Github\\DataDog\\dd-trace-dotnet\\reproductions\\AppDomain.Instance\\bin\\x64\\Debug\\net452"; // instanceHelper.GetSampleApplicationOutputDirectory();
                var mainAppOutputDirectory = "C:\\Github\\DataDog\\dd-trace-dotnet\\reproductions\\AppDomain.Crash\\bin\\x64\\Debug\\net452"; // mainApplicationHelper.GetSampleApplicationOutputDirectory();
                var securityInfo = new Evidence();

                var currentAssembly = Assembly.GetExecutingAssembly();

                var instanceType = typeof(AppDomainInstanceProgram);
                var instanceName = instanceType.FullName;

                System.AppDomain previousDomain = null;
                AppDomainInstanceProgram previousProgram = null;

                var domainsToReplace = 5;

                while (domainsToReplace-- > 0)
                {
                    if (previousDomain != null)
                    {
                        System.AppDomain.Unload(previousDomain);
                    }

                    var appDomainRoot = System.AppDomain.CreateDomain(commonFriendlyAppDomainName);

                    Console.WriteLine($"Created AppDomain root for #{index} - {commonFriendlyAppDomainName}");

                    var domainInstance =
                        appDomainRoot.CreateInstanceAndUnwrap(
                            instanceType.Assembly.FullName,
                            instanceName) as AppDomainInstanceProgram;

                    Console.WriteLine($"Created AppDomain instance for #{index} - {instanceName}");

                    var argsToPass = new string[] { commonFriendlyAppDomainName, index.ToString() };

                    Console.WriteLine($"Starting instance #{index} - {instanceName}");

                    var domainWorker = new Thread(
                        () =>
                        {
                            domainInstance.Main(argsToPass);
                        });

                    domainWorker.Start();

                    workers.Add(domainWorker);

                    // Give the domain some time to enjoy life
                    Thread.Sleep(7500);

                    previousDomain = appDomainRoot;
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
    }
}
