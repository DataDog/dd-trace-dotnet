using System;
using System.Net;
using System.Net.Sockets;

namespace Datadog.Core.Tools
{
    public class PortHelper
    {
        public static PortClaim GetTcpPortClaim()
        {
            return new PortClaim(new TcpListener(IPAddress.Loopback, 0));
        }

        public class PortClaim : IDisposable
        {
            private readonly TcpListener _listener;
            private int? _port;

            public PortClaim(TcpListener listener)
            {
                _listener = listener;
                _listener.Start();
            }

            public int Port
            {
                get
                {
                    if (_port == null)
                    {
                        throw new Exception($"You must call {nameof(Unlock)} before using this port claim.");
                    }

                    return _port.Value;
                }
            }

            public PortClaim Unlock()
            {
                if (_port == null)
                {
                    _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                    _listener?.Stop();
                    // return this for fluent use, to reduce race conditions
                }

                return this;
            }

            public void Dispose()
            {
                try
                {
                    _listener?.Stop();
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
