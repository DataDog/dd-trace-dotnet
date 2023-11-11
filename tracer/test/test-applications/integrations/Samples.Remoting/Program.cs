using System;
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

            string url = protocol switch
            {
                "http" => SetupHttpRemoting(port),
                "tcp" => SetupTcpRemoting(port),
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

        private static string SetupTcpRemoting(string port)
        {
            TcpChannel tcpServer = new TcpChannel(int.Parse(port));
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
