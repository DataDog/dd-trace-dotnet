using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading.Tasks;
using ActivitySampleHelper;
using Samples.Wcf.Bindings.Custom;
using Samples.Wcf.Client;
using Samples.Wcf.Server;

namespace Samples.Wcf
{
    public static class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample/123,123"; // appending 123,123 to the namespace to validate obsfucation in LocalPath for ResourceName

        private static async Task Main(string[] args)
        {
            // Accept a Port=# argument
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? WcfPort;
            LoggingHelper.WriteLineWithDate($"Port {port}");

            if (args.Length > 0 && args[0].Equals("WebHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                await RunHttpServer(args, port);
            }
            else
            {
                await RunSoapServer(args, port);
            }
        }

        private static async Task RunSoapServer(string[] args, string port)
        {
            Binding binding;
            Uri baseAddress;
            int expectedExceptionCount;

            if (args.Length > 0 && args[0].Equals("WSHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new WSHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 2;
            }
            else if (args.Length > 0 && args[0].Equals("BasicHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new BasicHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 2;
            }
            else if (args.Length > 0 && args[0].Equals("NetTcpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new NetTcpBinding();
                ((NetTcpBinding)binding).TransferMode = TransferMode.Streamed;
                baseAddress = new Uri($"net.tcp://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 2;
            }
            else if (args.Length > 0 && args[0].Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                binding = ConfigureCustomBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
                expectedExceptionCount = 13;
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
            
            static Binding ConfigureCustomBinding()
            {
                var customBinding = new CustomBinding();
                customBinding.Elements.Add(new CustomBindingElement());
                customBinding.Elements.Add(new TextMessageEncodingBindingElement(MessageVersion.Soap11, System.Text.Encoding.UTF8));
                customBinding.Elements.Add(new HttpTransportBindingElement());

                return customBinding;
            }
        }

        private static async Task RunHttpServer(string[] args, string port)
        {
            var uri = $"http://localhost:{port}";
            var host = new WebServiceHost(typeof(HttpCalculator), new Uri(uri));
            var serviceEndpoint = host.AddServiceEndpoint(typeof(IHttpCalculator), new WebHttpBinding(), "");

            // Adds the server DispatchMessageInspector
            serviceEndpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());
            host.Open();

            using var cf = new ChannelFactory<IHttpCalculator>(new WebHttpBinding(), uri);
            cf.Endpoint.Behaviors.Add(new WebHttpBehavior());
            // Adds the ClientMessageInspector and the DispatchMessageInspector
            cf.Endpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());

            var sampleHelper = new ActivitySourceHelper("Samples.Wcf");
            using var scope = sampleHelper.CreateScope("WebClient");
            var calculator = cf.CreateChannel();

            Console.WriteLine();
            LoggingHelper.WriteLineWithDate($"[Client] Invoke: ServerSyncAddJson(1, 2)");
            var result = calculator.ServerSyncAddJson("1", "2");
            AssertResult(result);
            LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");

            Console.WriteLine();
            LoggingHelper.WriteLineWithDate($"[Client] Invoke: ServerSyncAddXml(1, 2)");
            result = calculator.ServerSyncAddXml("1", "2");
            AssertResult(result);
            LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");

            Console.WriteLine();
            LoggingHelper.WriteLineWithDate($"[Client] Invoke: ServerTaskAddPost(1, 2)");
            result = await calculator.ServerTaskAddPost(new() { Arg1 = 1, Arg2 = 2 });
            AssertResult(result);
            LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");

            Console.WriteLine();
            LoggingHelper.WriteLineWithDate($"[Client] Invoke: ServerSyncAddWrapped(1, 2)");
            result = calculator.ServerSyncAddWrapped("1", "2");
            AssertResult(result);
            LoggingHelper.WriteLineWithDate($"[Client] Result: {result}");
        }

        private static void AssertResult(double result)
        {
            if (result != 3)
            {
                throw new Exception($"Unexpected value, expected '3' but received {result}");
            }
        }
    }
}
