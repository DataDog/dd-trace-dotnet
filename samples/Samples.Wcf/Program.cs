using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;

namespace Samples.Wcf
{
    public static class Program
    {
        private const string WcfPort = "8585";
        private const string WcfNamespace = "WcfSample";

        private static int timeoutMilliseconds = Timeout.Infinite;
        private static Thread inputThread;
        private static readonly AutoResetEvent startInputThreadEvent = new AutoResetEvent(initialState: false);
        private static readonly AutoResetEvent closeServerEvent = new AutoResetEvent(initialState: false);

        private static void Main(string[] args)
        {
            // This sample is a work in progress
            Binding binding;
            Uri baseAddress;

            // Accept a Port=# argument
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? WcfPort;
            Console.WriteLine($"Port {port}");

            // Accept a Timeout=# argument (in milliseconds)
            string timeoutmsString = args.FirstOrDefault(arg => arg.StartsWith("Timeout="))?.Split('=')[1] ?? null;
            if (int.TryParse(timeoutmsString, out int result))
            {
                timeoutMilliseconds = result;
            }

            Console.WriteLine($"Timeout (ms) {timeoutMilliseconds}");

            if (args.Length > 0 && args[0].Equals("WSHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new WSHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
            }
            else if (args.Length > 0 && args[0].Equals("BasicHttpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new BasicHttpBinding();
                baseAddress = new Uri($"http://localhost:{port}/{WcfNamespace}/");
            }
            else if (args.Length > 0 && args[0].Equals("NetTcpBinding", StringComparison.OrdinalIgnoreCase))
            {
                binding = new NetTcpBinding();
                baseAddress = new Uri($"net.tcp://localhost:{port}/{WcfNamespace}/");
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

                // Start listening to keyboard input on another thread
                // If a keyboard event happens, the server will be closed
                // Otherwise, wait for the specified timeout
                inputThread = new Thread(WaitForKeyboard);
                inputThread.IsBackground = true;
                inputThread.Start();
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

                startInputThreadEvent.Set();
                bool keyPressed = closeServerEvent.WaitOne(timeoutMilliseconds);
                if (!keyPressed)
                {
                    Console.WriteLine($"{timeoutMilliseconds} ms timeout reached. Closing the service.");
                }

                selfHost?.Close();
            }
        }

        private static void WaitForKeyboard()
        {
            startInputThreadEvent.WaitOne();
            Console.ReadKey(); // blocks indefinitely until a key press occurs
            closeServerEvent.Set();
        }
    }
}
