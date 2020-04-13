using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Samples.Wcf
{
    public static class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample";

#if NETFRAMEWORK
        // On .NET Framework, tell the runtime to load assemblies from the GAC domain-neutral.
        // In this sample, this will affect System.ServiceModel
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
#endif
        private static void Main(string[] args)
        {
            // This sample is a work in progress
            Binding binding;
            Uri baseAddress;

            if (args.Length > 0 && args[0].Equals("WSHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new WSHttpBinding();
                baseAddress = new Uri($"http://localhost:{WcfPort}/{WcfNamespace}/");
            }
            else if (args.Length > 0 && args[0].Equals("BasicHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new BasicHttpBinding();
                baseAddress = new Uri($"http://localhost:{WcfPort}/{WcfNamespace}/");
            }
            else if (args.Length > 0 && args[0].Equals("NetTcpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new NetTcpBinding();
                baseAddress = new Uri($"net.tcp://localhost:{WcfPort}/{WcfNamespace}/");
            }
            else
            {
                throw new Exception("Binding type required: WSHttpBinding, BasicHttpBinding, or NetTcpBinding");
            }

            var selfHost = new ServiceHost(typeof(CalculatorService), baseAddress);

            try
            {
                selfHost.AddServiceEndpoint(typeof(ICalculator), binding, "CalculatorService");

                if (binding is WSHttpBinding || binding is BasicHttpBinding)
                {
                    var smb = new ServiceMetadataBehavior { HttpGetEnabled = true };
                    selfHost.Description.Behaviors.Add(smb);
                }

                selfHost.Opening += (sender, eventArgs) => Console.Write("Opening... ");
                selfHost.Opened += (sender, eventArgs) => Console.WriteLine("done.");
                selfHost.Closing += (sender, eventArgs) => Console.Write("Closing... ");
                selfHost.Closed += (sender, eventArgs) => Console.WriteLine("done.");
                selfHost.Faulted += (sender, eventArgs) => Console.WriteLine("Faulted.");
                selfHost.UnknownMessageReceived += (sender, eventArgs) => Console.WriteLine($"UnknownMessageReceived: {eventArgs.Message}");

                selfHost.Open();

                Console.WriteLine("The service is ready.");
            }
            catch (CommunicationException ce)
            {
                Console.WriteLine("An exception occurred: {0}", ce.Message);
                selfHost.Abort();
                selfHost = null;
            }
            finally
            {
                // Close the ServiceHost to stop the service.
                Console.WriteLine("Press any key to exit.");
                Console.WriteLine();
                Console.ReadKey();
                selfHost?.Close();
            }
        }
    }
}
