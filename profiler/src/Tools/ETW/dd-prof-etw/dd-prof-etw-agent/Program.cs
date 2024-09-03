// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler.IntegrationTests
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            ParseCommandLine(args, out string eventsFilename, out string endpoint);
            if (eventsFilename == null)
            {
                Console.WriteLine("Missing required argument: -f <.bevents file name>");
                return;
            }

            Console.WriteLine($"Processing events in {eventsFilename}");

            if (endpoint == null)
            {
                Console.WriteLine("Missing required argument: -e <namedpipe endpoint such as \\\\.\\pipe\\DD_ETW_TEST_AGENT>");
                return;
            }

            Console.WriteLine($"Waiting for profiler registration from {endpoint}");
            AgentEtwProxy agentEtwProxy = new AgentEtwProxy(endpoint, eventsFilename);
            agentEtwProxy.ProfilerRegistered += (sender, e) =>
            {
                Console.WriteLine($"Profiler registered with PID {e.Value}");
            };

            agentEtwProxy.EventsSent += (sender, e) =>
            {
                Console.WriteLine($"Events sent: {e.Value}");
            };

            agentEtwProxy.ProfilerUnregistered += (sender, e) =>
            {
                Console.WriteLine($"Profiler unregistered with PID {e.Value}");
            };

            while (!agentEtwProxy.ProfilerHasUnregistered)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void ParseCommandLine(string[] args, out string eventsFilename, out string endpoint)
        {
            eventsFilename = null;
            endpoint = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if ("-f".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    // a filename is expected
                    i++;
                    if (i < args.Length)
                    {
                        eventsFilename = args[i];
                    }
                }
                else
                if ("-e".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    // a namedpipe endpoint is expected such as "\\\\.\\pipe\\DD_ETW_TEST_AGENT"
                    i++;
                    if (i < args.Length)
                    {
                        endpoint = args[i];
                    }
                }
            }
        }
    }
}
