using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Samples.Wcf.Bindings.Custom;

namespace Samples.Wcf
{
    public static class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample/123,123"; // appending 123,123 to the namespace to validate obsfucation in LocalPath for ResourceName

        private static async Task Main(string[] args)
        {
            Binding binding;
            Uri baseAddress;
            int expectedExceptionCount;

            // Accept a Port=# argument
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? WcfPort;
            LoggingHelper.WriteLineWithDate($"Port {port}");

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
                expectedExceptionCount = 6;
            }
            else
            {
                throw new Exception("Binding type required: WSHttpBinding, BasicHttpBinding, or NetTcpBinding");
            }

            var server = Server.Startup.CreateCalculatorService(binding, baseAddress);

            try
            {
                LoggingHelper.WriteLineWithDate("[Server] Starting the server.");
                server.Open();
                LoggingHelper.WriteLineWithDate("[Server] The server is ready.");

                LoggingHelper.WriteLineWithDate("[Client] Starting the client.");
                await Client.Startup.InvokeCalculatorService(binding, baseAddress, expectedExceptionCount);
                LoggingHelper.WriteLineWithDate("[Client] The client has exited.");
            }
            catch (CommunicationException ce)
            {
                LoggingHelper.WriteLineWithDate($"An exception occurred: {ce.Message}");
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
