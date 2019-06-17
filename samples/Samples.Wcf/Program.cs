using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace Samples.Wcf
{
    public static class Program
    {
        private static string WcfPort = "8585";
        private static string WcfNamespace = "WcfSample";

        static void Main(string[] args)
        {
            // This sample is a work in progress
            var baseAddress = new Uri($"http://localhost:{WcfPort}/{WcfNamespace}/");
            var selfHost = new ServiceHost(typeof(CalculatorService), baseAddress);

            try
            {
                selfHost.AddServiceEndpoint(typeof(ICalculator), new WSHttpBinding(), "CalculatorService");

                ServiceMetadataBehavior smb = new ServiceMetadataBehavior
                {
                    HttpGetEnabled = true
                };

                selfHost.Description.Behaviors.Add(smb);

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
