// <copyright file="SocketTimeout.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class SocketTimeout : ScenarioBase
    {
        private const int ServerPort = 4242;
        private const int OperationTimeout = 1_000;
        private readonly TimeSpan _operationErrorTimeout = TimeSpan.FromSeconds(3);
        private readonly IPEndPoint _serverEndpoint = new(IPAddress.Loopback, ServerPort);
        private readonly Task _serverThreadTask;
        private readonly ManualResetEventSlim _serverReadyEvent;

        public SocketTimeout()
        {
            _serverReadyEvent = new ManualResetEventSlim(false);
            _serverThreadTask = Task.Factory.StartNew(StartServer, TaskCreationOptions.LongRunning);
        }

        private int TimeoutErrorCode { get; } = OperatingSystem.IsWindows() ? 10060 : 110;

        public override void OnProcess()
        {
            // Wait for server and gate threads to be ready
            if (!_serverReadyEvent.IsSet)
            {
                Console.WriteLine("** Waiting for server to be ready..");
                _serverReadyEvent.Wait();
            }

            if (IsEventSet())
            {
                return;
            }

            ReceiveTimeout();
            SendTimeout();
        }

        private static Socket CreateServerSocket()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ep = new IPEndPoint(IPAddress.Any, ServerPort);
            s.Bind(ep);
            s.Listen();

            return s;
        }

        private Socket CreateAndConnectClientSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = OperationTimeout,
                ReceiveTimeout = OperationTimeout,
                SendBufferSize = 0,
                Blocking = true
            };
            socket.Connect(_serverEndpoint);

            return socket;
        }

        private void ReceiveTimeout()
        {
            EnsureOperationTimesOut(
                (socket) =>
                {
                    var buffer = new byte[42];
                    socket.Receive(buffer);
                },
                "Receive");
        }

        private void SendTimeout()
        {
            EnsureOperationTimesOut(
                (socket) =>
                {
                    // really big buffer to make sure the call blocks
                    var buffer = new byte[200_000];
                    socket.Send(buffer);
                },
                "Send");
        }

        private bool IsSocketTimeoutException(Exception e)
        {
            return e switch
            {
                AggregateException ae => IsSocketTimeoutException(ae.InnerException),
                SocketException se => se.ErrorCode == TimeoutErrorCode,
                _ => false
            };
        }

        private void EnsureOperationTimesOut(Action<Socket> action, string operationName)
        {
            try
            {
                var cts = new CancellationTokenSource(_operationErrorTimeout);
                var task = Task.Run(
                    () =>
                    {
                        using var socket = CreateAndConnectClientSocket();
                        action(socket);

                        if (!IsEventSet())
                        {
                            Console.WriteLine($"[Error] The {operationName} blocking call must timeout.");
                        }
                    });
                task.Wait(cts.Token);
            }
            catch (Exception e) when (IsSocketTimeoutException(e))
            {
                // ok case
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[Error] The {operationName} operation was cancelled because it reached the {_operationErrorTimeout.Seconds}s timeout.");
            }
            catch (Exception e)
            {
                if (!IsEventSet())
                {
                    Console.WriteLine("[Error] unknown/unexpected exception: " + e.ToString());
                }
            }
        }

        private void StartServer(object obj)
        {
            Thread.CurrentThread.Name = "DD Socket Srv";

            using var s = CreateServerSocket();

            _serverReadyEvent.Set();

            Console.WriteLine("** Server started...");

            // This is needed to keep the connection up for the duration of each operation
            // As the current test will create only one worker thread (even though there is
            // --threads on the command-line), we are safe
            Socket client;
            while (!IsEventSet())
            {
                try
                {
                    client = s.AcceptAsync().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    if (!IsEventSet())
                    {
                        Console.WriteLine("[Error] Unknown error when accepting connection: " + e.ToString());
                    }

                    break;
                }
            }
        }
    }
}
#endif
