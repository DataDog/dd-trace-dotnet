// <copyright file="AgentEtwProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class AgentEtwProxy : IRecordDumper
    {
        // endpoint serviced by the profiler based on its PID
        private const string ProfilerNamedPipePrefix = "DD_ETW_CLIENT_";

        private readonly ITestOutputHelper _output;
        private string _agentEndPoint;
        private string _eventsFilename;
        private bool _profilerHasRegistered;
        private bool _profilerHasUnregistered;
        private bool _eventsHaveBeenSent;
        private int _pid;
        private NamedPipeClientStream _pipeClient;
        private ManualResetEvent _serverStarted;

        public AgentEtwProxy(ITestOutputHelper output, string agentEndPoint, string eventsFilename)
        {
            _agentEndPoint = agentEndPoint;
            _eventsFilename = eventsFilename;
            _profilerHasRegistered = false;
            _profilerHasUnregistered = false;
            _eventsHaveBeenSent = false;
            _pipeClient = null;
            _output = output;
            _serverStarted = new ManualResetEvent(false);
        }

        // ---------------------------------PID
        public event EventHandler<EventArgs<int>> ProfilerRegistered;

        // ---------------------------------number of events sent
        public event EventHandler<EventArgs<int>> EventsSent;

        // ---------------------------------PID
        public event EventHandler<EventArgs<int>> ProfilerUnregistered;

        public bool ProfilerHasRegistered { get => _profilerHasRegistered; }

        public bool ProfilerHasUnregistered { get => _profilerHasUnregistered; }

        public bool EventsHaveBeenSent { get => _eventsHaveBeenSent; }

        public void DumpRecord(byte[] record, int recordSize)
        {
            if (_pipeClient == null)
            {
                throw new ArgumentNullException("_pipeClient");
            }

            // Send the byte[] buffer to the server
            _pipeClient.Write(record, 0, recordSize);
            _pipeClient.Flush();

            // NOTE: this is a fire and forget call: no answer is expected from the profiler
        }

        public void StartServer()
        {
            Task.Run(() => StartServerAsync());

            _serverStarted.WaitOne();

            // let the time for WaitForConnectionAsync to start
            Thread.Sleep(300);
        }

        public async Task StartServerAsync()
        {
            Thread.CurrentThread.Name = "AgentProxy";
            try
            {
                // simulate the Agent that accepts register/unregister commands from the profiler
#pragma warning disable CA1416 // Validate platform compatibility
                using (var server = new NamedPipeServerStream(
                                            _agentEndPoint,
                                            PipeDirection.InOut,
                                            2,
                                            PipeTransmissionMode.Message,
                                            PipeOptions.WriteThrough))
                {
                    WriteLine($"NamedPipeServer is waiting for a connection on {_agentEndPoint}...");
                    _serverStarted.Set();

                    await server.WaitForConnectionAsync();

                    WriteLine("Client connected.");
                    byte[] inBuffer = new byte[256];
                    int bytesRead;
                    byte[] outBuffer = new byte[256];

                    while ((bytesRead = await server.ReadAsync(inBuffer, 0, inBuffer.Length)) > 0)
                    {
                        bool success = ProcessCommand(inBuffer);

                        // build the response based on success/failure
                        IpcHeader header = new IpcHeader
                        {
                            Size = IpcHeader.HeaderSize,
                            CommandIdOrResponseCode = success ? ResponseCodes.Success : ResponseCodes.Failure,
                        };
                        Encoding.ASCII.GetBytes(
                            IpcHeader.MagicValue,
                            MemoryMarshal.CreateSpan<byte>(ref header.Magic._element, 14));
                        MemoryMarshal.Write<IpcHeader>(outBuffer, in header);

                        server.Write(outBuffer, 0, IpcHeader.HeaderSize);
                        server.Flush();
                        server.WaitForPipeDrain();
                    }
                }
#pragma warning restore CA1416 // Validate platform compatibility
            }
            catch (Exception ex)
            {
                WriteLine($"StartServerAsync: {ex.Message}");
            }
        }

        private void OnProfilerRegistered(int pid)
        {
            WriteLine($"process {pid} has registered");
            ProfilerRegistered?.Invoke(this, new EventArgs<int>(pid));
        }

        private void OnEventsSent(int count)
        {
            EventsSent?.Invoke(this, new EventArgs<int>(count));
        }

        private void OnProfilerUnregistered(int pid)
        {
            WriteLine($"process {pid} is unregistered");
            ProfilerUnregistered?.Invoke(this, new EventArgs<int>(pid));
        }

        private void WriteLine(string line)
        {
            if (_output != null)
            {
                _output.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }

        private bool ProcessCommand(byte[] receivedData)
        {
            RegistrationCommand command = MemoryMarshal.Read<RegistrationCommand>(receivedData);

            // TODO: check header with magic number

            // handle register and unregister commands
            if (command.Header.CommandIdOrResponseCode == AgentCommands.Register)
            {
                _pid = (int)command.Pid;
                OnProfilerRegistered(_pid);
                _profilerHasRegistered = (_pid != 0);

                if (_profilerHasRegistered)
                {
                    // NOTE: we want to accept the profiler registration command before starting to send events from another thread
                    //       so we don't await this async method
                    Task.Run(StartClientAsync);
                }

                return _profilerHasRegistered;
            }
            else if (command.Header.CommandIdOrResponseCode == AgentCommands.Unregister)
            {
                OnProfilerUnregistered((int)command.Pid);
                _profilerHasUnregistered = (_pid != (int)command.Pid);

                return _profilerHasUnregistered;
            }

            return (command.Header.CommandIdOrResponseCode == AgentCommands.KeepAlive);
        }

        private async Task StartClientAsync()
        {
            // let the profiler initialize before sending events
            Thread.Sleep(500);

            string endPoint = ProfilerNamedPipePrefix + _pid.ToString();
            WriteLine($"Connecting to the profiler on {endPoint}...");
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", endPoint, PipeDirection.Out))
            {
                _pipeClient = pipeClient;

                try
                {
                    // Connect to the NamedPipe server
                    await pipeClient.ConnectAsync();
                    WriteLine("Connected to the Profiler");

                    // read the events file and send each event to the profiler
                    if (_eventsFilename != null)
                    {
                        using (FileStream fs = new FileStream(_eventsFilename, FileMode.Open, FileAccess.Read))
                        using (BinaryReader reader = new BinaryReader(fs))
                        {
                            var recordReader = new RecordReader(reader, this, null);
                            int count = 0;

                            WriteLine("Start replaying events...");

                            // each records will be processed by the DumpRecord method
                            while (fs.Position < fs.Length)
                            {
                                count++;
                                WriteLine($"Sending event {count}...");
                                recordReader.ReadRecord();
                            }

                            WriteLine($"{count} events have been replayed");
                            _eventsHaveBeenSent = true;
                            OnEventsSent(count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLine($"StartClientAsync: {ex.Message}");
                }
            }
        }
    }
}
