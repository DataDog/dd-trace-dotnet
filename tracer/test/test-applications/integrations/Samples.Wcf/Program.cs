using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Samples.Wcf
{
    public static class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample";

        private static async Task Main(string[] args)
        {
            Binding binding;
            Uri baseAddress;
            int expectedExceptionCount;

            // Accept a Port=# argument
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? WcfPort;
            Console.WriteLine($"Port {port}");

            if (args.Length > 0 && args[0].Equals("WSHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new WSHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 0;
            }
            else if (args.Length > 0 && args[0].Equals("BasicHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new BasicHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 0;
            }
            else if (args.Length > 0 && args[0].Equals("NetTcpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new NetTcpBinding();
                ((NetTcpBinding)binding).TransferMode = TransferMode.Streamed;
                baseAddress = new Uri($"net.tcp://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 0;
            }
            else if (args.Length > 0 && args[0].Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                binding = ConfigureCustomBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 9;
            }
            else
            {
                throw new Exception("Binding type required: WSHttpBinding, BasicHttpBinding, or NetTcpBinding");
            }

            var server = Server.Startup.CreateCalculatorService(binding, baseAddress);

            try
            {
                Console.WriteLine("[Server] Starting the server.");
                server.Open();
                Console.WriteLine("[Server] The server is ready.");

                Console.WriteLine("[Client] Starting the client.");
                await Client.Startup.InvokeCalculatorService(binding, baseAddress, expectedExceptionCount);
                Console.WriteLine("[Client] The client has exited.");
            }
            catch (CommunicationException ce)
            {
                Console.WriteLine("An exception occurred: {0}", ce.Message);
                server.Abort();
                server = null;
            }
            finally
            {
                server?.Close();
            }
        }

        private static Binding ConfigureCustomBinding()
        {
            var customBinding = new CustomBinding();
            customBinding.Elements.Add(new CustomBindingElement());
            customBinding.Elements.Add(new TextMessageEncodingBindingElement(MessageVersion.Soap11, System.Text.Encoding.UTF8));
            customBinding.Elements.Add(new HttpTransportBindingElement());

            return customBinding;
        }
    }
}
