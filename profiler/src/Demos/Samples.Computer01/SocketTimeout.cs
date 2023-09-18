// <copyright file="SocketTimeout.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class SocketTimeout : ScenarioBase
    {
        private readonly Task _serverThreadTask;
        private readonly Task _gateThreadTask;
        private readonly ManualResetEventSlim _readyEvent;
        private readonly CancellationTokenSource _cts;
        private readonly int _port;
        private bool _hasReceiveTimedOut;
        private bool _hasSendTimedOut;

        public SocketTimeout()
        {
            _port = 4242;
            _cts = new CancellationTokenSource();
            _serverThreadTask = Task.Factory.StartNew(StartServerAsync, _cts.Token, TaskCreationOptions.LongRunning);
            _gateThreadTask = Task.Factory.StartNew(StartGateThread, _cts.Token, TaskCreationOptions.LongRunning);
            _hasReceiveTimedOut = false;
            _hasSendTimedOut = true;
            _readyEvent = new ManualResetEventSlim(false);
        }

        public override void OnProcess()
        {
            // Wait for server and gate threads to be ready
            if (!_readyEvent.IsSet)
            {
                Console.WriteLine("** Waiting for server to be ready..");
                _readyEvent.Wait(_cts.Token);
            }

            // In we are called while shutting down
            if (_cts.Token.IsCancellationRequested)
            {
                return;
            }

            ReceiveTimeout(_cts.Token).Wait(_cts.Token);
            SendTimeout(_cts.Token).Wait(_cts.Token);
        }

        private async Task ReceiveTimeout(CancellationToken token)
        {
            try
            {
                using var socket = await CreateAndConnect(token).ConfigureAwait(false);
                socket.ReceiveTimeout = 1_000;

                var buffer = new byte[42];
                socket.Receive(buffer);

                Console.WriteLine("[Error] The Receive blocking call must timeout.");
            }
            catch (SocketException se) when (se.ErrorCode == 110)
            {
                _hasReceiveTimedOut = true;
            }
            catch (Exception e)
            {
                if (!IsEventSet())
                {
                    Console.WriteLine("[Error] unknown/unexpected exception: " + e.ToString());
                }
            }
        }

        private async Task SendTimeout(CancellationToken token)
        {
            try
            {
                using var socket = await CreateAndConnect(token).ConfigureAwait(false);
                socket.SendTimeout = 1_000;

                // really big buffer to make sure the call blocks
                var buffer = new byte[8655535];
                socket.Send(buffer);

                Console.WriteLine("[Error] The Send blocking call must timeout.");
            }
            catch (SocketException e) when (e.ErrorCode == 110)
            {
                _hasReceiveTimedOut = true;
            }
            catch (Exception e)
            {
                if (!IsEventSet())
                {
                    Console.WriteLine("[Error] unknown/unexpected exception: " + e.ToString());
                }
            }
        }

        private async Task<Socket> CreateAndConnect(CancellationToken token)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ep = new IPEndPoint(IPAddress.Loopback, _port);

            try
            {
                await socket.ConnectAsync(ep, token);
            }
            catch
            {
                throw;
            }

            return socket;
        }

        private async Task StartServerAsync(object obj)
        {
            Console.WriteLine("** Start server");

            Thread.CurrentThread.Name = "DD Socket Srv";
            var token = (CancellationToken)obj;
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ep = new IPEndPoint(IPAddress.Any, _port);
            s.Bind(ep);
            s.Listen();

            _readyEvent.Set();

            // This array ensures that we keep the socket alive (avoid the GC from
            // collecting it) until the timeout is reached
            var sockets = new Socket[2];
            var idx = 0;
            while (!IsEventSet())
            {
                idx = idx++ % sockets.Length;
                try
                {
                    sockets[idx] = await s.AcceptAsync(token);
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] Unknown error when starting the server: " + e.ToString());
                    break;
                }
            }

            if (!_hasReceiveTimedOut)
            {
                Console.WriteLine("[Error] Receive operation did not timed-out");
            }

            if (!_hasSendTimedOut)
            {
                Console.WriteLine("[Error] Send operation did not timed-out");
            }

            Console.WriteLine("** Server stopped");
        }

        private void StartGateThread(object obj)
        {
            Console.WriteLine("** Starting Gate Thread");

            Thread.CurrentThread.Name = "DD Gate Thread";
            var cts = (CancellationTokenSource)obj;
            while (!IsEventSet())
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            if (!_readyEvent.IsSet)
            {
                _readyEvent.Set();
            }

            cts.Cancel();

            Console.WriteLine("** Gate Thread stopped");
        }
    }
}
#endif
