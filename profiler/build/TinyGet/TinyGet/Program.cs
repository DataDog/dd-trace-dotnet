using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;
using TinyGet.Config;
using TinyGet.Requests;

namespace TinyGet
{
    class Program
    {
        private static readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) => Cancellation.Cancel();

            if (0 == args.Length)
            {
                PrintHelp();
            }
            else
            {
                try
                {
                    Execute(args);
                }
                catch (ApplicationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static void Execute(string[] args)
        {
            NameValueCollection settings = args.ToNameValueCollection();
            AppArguments arguments = AppArguments.Parse(settings);

            Context context = new Context(arguments, Cancellation.Token, Console.Out);
            AppHost host = new AppHost(context, new RequestSenderCreator());
            host.Run();
        }

        private static void PrintHelp()
        {
            Console.WriteLine(CommandLineArguments.HelpMessage);

            Console.WriteLine();

            string version = Assembly.GetEntryAssembly().GetName().Version.ToString();
            Console.WriteLine("Version: " + version);
        }
    }
}
