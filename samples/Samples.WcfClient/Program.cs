using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.WcfClient
{
    internal class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample";

        private static void Main(string[] args)
        {
            Binding binding;
            Uri baseAddress;

#if NETFRAMEWORK
            if (args.Length > 0 && args[0].Equals("WSHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new WSHttpBinding();
                baseAddress = new Uri($"http://localhost:{WcfPort}/{WcfNamespace}/CalculatorService");
            }
            else
#endif

            if (args.Length > 0 && args[0].Equals("BasicHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new BasicHttpBinding();
                baseAddress = new Uri($"http://localhost:{WcfPort}/{WcfNamespace}/CalculatorService");
            }
            else if (args.Length > 0 && args[0].Equals("NetTcpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new NetTcpBinding();
                baseAddress = new Uri($"net.tcp://localhost:{WcfPort}/{WcfNamespace}/CalculatorService");
            }
            else
            {
#if NETFRAMEWORK
                throw new Exception("Binding type required: WSHttpBinding, BasicHttpBinding, or NetTcpBinding");
#else
                throw new ApplicationException("Binding type required: BasicHttpBinding or NetTcpBinding");
#endif
            }

            var address = new EndpointAddress(baseAddress);

            using (var calculator = new CalculatorClient(binding, address))
            {
                double result = calculator.Add(1, 2);
                Console.WriteLine($"Result: {result}");
            }
        }
    }
}
