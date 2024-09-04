// <copyright file="AgentEtwProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Profiler.IntegrationTests
{
    public class AgentEtwProxy : IRecordDumper
    {
        // endpoint serviced by the profiler based on its PID
        private const string ProfilerNamedPipePrefix = "DD_ETW_CLIENT_";

        private string _agentEndPoint;
        private string _eventsFilename;
        private bool _profilerHasRegistered;
        private bool _profilerHasUnregistered;
        private bool _eventHaveBeenSent;
        private int _pid;
        private NamedPipeClientStream _pipeClient;

        public AgentEtwProxy(string agentEndPoint, string eventsFilename)
        {
            _agentEndPoint = agentEndPoint;
            _eventsFilename = eventsFilename;
            _profilerHasRegistered = false;
            _profilerHasUnregistered = false;
            _eventHaveBeenSent = false;
            _pipeClient = null;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            StartServerAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        // ---------------------------------PID
        public event EventHandler<EventArgs<int>> ProfilerRegistered;

        // ---------------------------------number of events sent
        public event EventHandler<EventArgs<int>> EventsSent;

        // ---------------------------------PID
        public event EventHandler<EventArgs<int>> ProfilerUnregistered;

        public bool ProfilerHasRegistered { get => _profilerHasRegistered; }

        public bool ProfilerHasUnregistered { get => _profilerHasUnregistered; }

        public bool EventHaveBeenSent { get => _eventHaveBeenSent; }

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
            Thread.Sleep(100);
        }

        private void OnProfilerRegistered(int pid)
        {
            ProfilerRegistered?.Invoke(this, new EventArgs<int>(pid));
        }

        private void OnEventsSent(int count)
        {
            EventsSent?.Invoke(this, new EventArgs<int>(count));
        }

        private void OnProfilerUnregistered(int pid)
        {
            ProfilerUnregistered?.Invoke(this, new EventArgs<int>(pid));
        }

        private async Task StartServerAsync()
        {
            try
            {
                // simulate the Agent that accepts register/unregister commands from the profiler
                using (var server = new NamedPipeServerStream(
                                            _agentEndPoint,
                                            PipeDirection.InOut,
                                            2,
                                            PipeTransmissionMode.Byte,
                                            PipeOptions.None))
                {
                    Console.WriteLine($"NamedPipeServer is waiting for a connection on {_agentEndPoint}...");
                    await server.WaitForConnectionAsync();

                    Console.WriteLine("Client connected.");
                    byte[] inBuffer = new byte[256];
                    int bytesRead;
                    byte[] outBuffer = new byte[256];

                    while ((bytesRead = await server.ReadAsync(inBuffer, 0, inBuffer.Length)) > 0)
                    {
                        bool success = await ProcessCommand(inBuffer);

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

                        await server.WriteAsync(outBuffer, 0, IpcHeader.HeaderSize);
                        await server.FlushAsync();
                        server.WaitForPipeDrain();
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
                    await StartClientAsync();
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
            string endPoint = ProfilerNamedPipePrefix + _pid.ToString();
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", endPoint, PipeDirection.Out))
            {
                _pipeClient = pipeClient;

                try
                {
                    // Connect to the NamedPipe server
                    await pipeClient.ConnectAsync();

                    // read the events file and send each event to the profiler
                    if (_eventsFilename != null)
                    {
                        using (FileStream fs = new FileStream(_eventsFilename, FileMode.Open, FileAccess.Read))
                        using (BinaryReader reader = new BinaryReader(fs))
                        {
                            var recordReader = new RecordReader(reader, this, null);
                            int count = 0;

                            // each records will be processed by the DumpRecord method
                            while (fs.Position < fs.Length)
                            {
                                count++;
                                recordReader.ReadRecord();
                            }

                            _eventHaveBeenSent = true;
                            OnEventsSent(count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"StartClientAsync: {ex.Message}");
                }
            }
        }
    }
}
