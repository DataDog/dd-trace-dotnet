using System;

namespace Sandbox.LegacySecurityPolicy
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var appDomainSetup = new AppDomainSetup
            {
                AppDomainManagerAssembly = typeof(CustomAppDomainManager).Assembly.FullName,
                AppDomainManagerType = typeof(CustomAppDomainManager).FullName
            };

            var domain = AppDomain.CreateDomain("ChildDomain", null, appDomainSetup);

            var instance = domain.CreateInstanceAndUnwrap(typeof(RemoteType).Assembly.FullName, typeof(RemoteType).FullName);

            instance.GetType().GetMethod(nameof(RemoteType.Print)).Invoke(instance, null);
        }
    }

    public class RemoteType : MarshalByRefObject
    {
        public void Print()
        {
            if (AppDomain.CurrentDomain.IsHomogenous)
            {
                Console.WriteLine("The test is not setup properly (is NetFx40_LegacySecurityPolicy enabled?");
                Environment.Exit(-1);
            }

            if (!AppDomain.CurrentDomain.IsFullyTrusted)
            {
                Console.WriteLine("The test failed, the current domain should be fully trusted");
                Environment.Exit(-2);
            }

            Console.WriteLine("OK");
        }
    }

    public class CustomAppDomainManager : AppDomainManager
    { }
}
