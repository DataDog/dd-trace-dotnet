// <copyright file="AgentEtwProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Profiler.IntegrationTests
{
    public class AgentEtwProxy
    {
        // endpoint serviced by the profiler based on its PID
        private const string ProfilerNamedPipePrefix = "\\\\.\\pipe\\DD_ETW_CLIENT_";

        private string _agentEndPoint;
        private string _eventsFilename;
        private bool _profilerHasRegistered;
        private bool _profilerHasUnregistered;
        private int _pid;

        public AgentEtwProxy(string agentEndPoint, string eventsFilename)
        {
            _agentEndPoint = agentEndPoint;
            _eventsFilename = eventsFilename;
            _profilerHasRegistered = false;
            _profilerHasUnregistered = false;

            StartServerAsync();
        }

        public bool ProfilerHasRegistered { get => _profilerHasRegistered; }

        public bool ProfilerHasUnregistered { get => _profilerHasUnregistered; }

        private async Task StartServerAsync()
        {
            try
            {
                using (var server = new NamedPipeServerStream(_agentEndPoint, PipeDirection.InOut))
                {
                    Console.WriteLine($"NamedPipeServer is waiting for a connection on {_agentEndPoint}...");
                    await server.WaitForConnectionAsync();

                    Console.WriteLine("Client connected.");
                    byte[] inBuffer = new byte[256];
                    int bytesRead;
                    byte[] outBuffer = new byte[256];
                    int bytesWritten;

                    while ((bytesRead = await server.ReadAsync(inBuffer, 0, inBuffer.Length)) > 0)
                    {
                        bool success = await ProcessCommand(inBuffer);

                        // TODO: build the response based on success/failure
                        if (success)
                        {
                            outBuffer = Encoding.UTF8.GetBytes("Success");
                        }
                        else
                        {
                            outBuffer = Encoding.UTF8.GetBytes("Failure");
                        }

                        await server.WriteAsync(outBuffer, 0, outBuffer.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartServerAsync: {ex.Message}");
            }
        }

        private async Task<bool> ProcessCommand(byte[] receivedData)
        {
            // handle register and unregister commands
            if (!_profilerHasRegistered)
            {
                _profilerHasRegistered = true;
                await StartClientAsync();
            }

            return true;
        }

        private async Task StartClientAsync()
        {
            string endPoint = ProfilerNamedPipePrefix + _pid.ToString();
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", endPoint, PipeDirection.Out))
            {
                try
                {
                    // Connect to the NamedPipe server
                    await pipeClient.ConnectAsync();

                    // read the events file and send each event to the profiler

                    // Send the byte[] buffer to the server
                    // await pipeClient.WriteAsync(buffer, 0, buffer.Length);
                    // await pipeClient.FlushAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"StartClientAsync: {ex.Message}");
                }
            }
        }
    }
}
