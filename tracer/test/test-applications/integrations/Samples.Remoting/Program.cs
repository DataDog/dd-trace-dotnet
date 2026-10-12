using System;
using System.Collections;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels.Tcp;

namespace Samples.Remoting
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            string protocol = args.FirstOrDefault(arg => arg.StartsWith("Protocol="))?.Split('=')[1]?.ToLower() ?? "http";
            Console.WriteLine($"Protocol {protocol}");

            // "single" declares an explicit server sink chain with only one formatter (BinaryServerFormatterSinkProvider),
            // instead of the default chain that has both the SOAP and Binary formatters
            bool singleFormatter = string.Equals(args.FirstOrDefault(arg => arg.StartsWith("Formatters="))?.Split('=')[1], "single", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"Single formatter {singleFormatter}");

            string url = protocol switch
            {
                "http" => SetupHttpRemoting(port),
                "tcp" => SetupTcpRemoting(port, singleFormatter),
                "ipc" => SetupIpcRemoting(port),
                _ => throw new ArgumentException($"Protocol '{protocol}' not recognized", "Protocol"),
            };

            Console.WriteLine("Starting client calls");
            RunClient(url);

            Console.WriteLine();
            Console.WriteLine("Finished client calls");
            Console.WriteLine("Exiting...");
        }

        private static string SetupHttpRemoting(string port)
        {
            HttpChannel httpServer = new HttpChannel(int.Parse(port));
            ChannelServices.RegisterChannel(httpServer, false);

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemoteClass), "RemoteServer", WellKnownObjectMode.SingleCall);

            return $"http://localhost:{port}/RemoteServer";
        }

        private static string SetupTcpRemoting(string port, bool singleFormatter)
        {
            TcpChannel tcpServer = singleFormatter
                                       ? new TcpChannel(new Hashtable { { "port", int.Parse(port) } }, clientSinkProvider: null, serverSinkProvider: new BinaryServerFormatterSinkProvider())
                                       : new TcpChannel(int.Parse(port));
            ChannelServices.RegisterChannel(tcpServer, false);

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemoteClass), "RemoteServer", WellKnownObjectMode.SingleCall);

            return $"tcp://localhost:{port}/RemoteServer";
        }

        private static string SetupIpcRemoting(string port)
        {
            IpcChannel ipcServer = new IpcChannel($"localhost:{port}");
            ChannelServices.RegisterChannel(ipcServer, false);

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(RemoteClass), "RemoteServer", WellKnownObjectMode.SingleCall);

            return $"ipc://localhost:{port}/RemoteServer";
        }

        private static void RunClient(string url)
        {
            using (SampleHelpers.CreateScope("custom-client-span"))
            {
                RemoteClass remoteObj = (RemoteClass)Activator.GetObject(typeof(RemoteClass), url);

                Console.WriteLine();
                Console.WriteLine("Calling remoteObj.SetString(\"someString\");");
                bool result = remoteObj.SetString("someString");
                Console.WriteLine("result = " + result);

                Console.WriteLine();
                Console.WriteLine("Calling remoteObj.SetString(null);");
                try
                {
                    result = remoteObj.SetString(null);
                    Console.WriteLine("result = " + result);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception Message: {e.Message}");
                }
            }
        }
    }
}
