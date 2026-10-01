using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using Ae.Dns.Server;

namespace Samples.DnsClient
{
    // Ae.Dns owns the sockets and wire protocol; this handler supplies the sample's scenarios.
    internal sealed class DnsServer : IDnsRawClient
    {
        private readonly DnsTcpServer _tcp;
        private readonly DnsUdpServer _udp;
        private readonly bool _failover;
        private readonly ConcurrentDictionary<string, int> _requests = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _listeners;

        public DnsServer(bool failover = false)
        {
            _failover = failover;
            EndPoint = new IPEndPoint(IPAddress.Loopback, WebServer.GetOpenPort());
            _tcp = new DnsTcpServer(this, new DnsTcpServerOptions { Endpoint = EndPoint });
            try
            {
                _udp = new DnsUdpServer(this, new DnsUdpServerOptions { Endpoint = EndPoint });
            }
            catch
            {
                _tcp.Dispose();
                throw;
            }

            _listeners = Task.WhenAll(_tcp.Listen(_stopping.Token), _udp.Listen(_stopping.Token));
        }

        public IPEndPoint EndPoint { get; }

        public int RequestCount(string name, bool tcp = false) => _requests.TryGetValue($"{tcp}:{name.ToLowerInvariant()}", out var count) ? count : 0;

        public void Dispose()
        {
            _stopping.Cancel();
            _tcp.Dispose();
            _udp.Dispose();
            try
            {
                _listeners.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
                // Closing a pending accept can surface this on older runtimes.
            }
            finally
            {
                _stopping.Dispose();
            }
        }

        public async Task<DnsRawClientResponse> Query(Memory<byte> buffer, DnsRawClientRequest request, CancellationToken token = default)
        {
            var query = new DnsMessage();
            var offset = 0;
            query.ReadBytes(buffer.Slice(0, request.QueryLength), ref offset);
            var name = query.Header.Host.ToString().TrimEnd('.').ToLowerInvariant();
            var tcp = request.ServerName == nameof(DnsTcpServer);
            var count = _requests.AddOrUpdate($"{tcp}:{name}", 1, (_, value) => value + 1);
            if (name.StartsWith("timeout.", StringComparison.Ordinal) || (name.StartsWith("retry.", StringComparison.Ordinal) && count == 1))
            {
                // Leave the request unanswered until shutdown so the client can time out/retry.
                await Task.Delay(Timeout.Infinite, token);
            }

            var response = new DnsMessage { Header = DnsQueryFactory.Clone(query.Header) };
            response.Header.IsQueryResponse = true;
            response.Header.RecursionAvailable = true;
            response.Header.AdditionalRecordCount = 0;
            response.Header.Truncation = name.StartsWith("truncated.", StringComparison.Ordinal) && !tcp;
            response.Header.ResponseCode = name.StartsWith("nxdomain", StringComparison.Ordinal) ? DnsResponseCode.NXDomain
                                         : name.StartsWith("servfail.", StringComparison.Ordinal) || (_failover && name.StartsWith("failover.", StringComparison.Ordinal)) ? DnsResponseCode.ServFail
                                         : DnsResponseCode.NoError;
            if (response.Header.ResponseCode == DnsResponseCode.NoError && !response.Header.Truncation && !name.StartsWith("empty.", StringComparison.Ordinal))
            {
                response.Answers =
                [
                    new DnsResourceRecord
                    {
                        Host = query.Header.Host,
                        Type = query.Header.QueryType,
                        Class = query.Header.QueryClass,
                        TimeToLive = 60,
                        Resource = query.Header.QueryType switch
                        {
                            DnsQueryType.A => new DnsIpAddressResource { IPAddress = IPAddress.Parse("192.0.2.123") },
                            DnsQueryType.AAAA => new DnsIpAddressResource { IPAddress = IPAddress.Parse("2001:db8::123") },
                            DnsQueryType.PTR => new DnsDomainResource { Entries = $"host-{query.Header.Host[0]}.dns.test" },
                            DnsQueryType.MX => new DnsMxResource { Preference = 10, Entries = "mail.dns.test" },
                            DnsQueryType.TEXT => new DnsTextResource { Entries = "test" },
                            DnsQueryType.SRV => CreateServiceRecord(),
                            _ => throw new InvalidOperationException($"Unsupported DNS record type: {query.Header.QueryType}"),
                        },
                    },
                ];
                response.Header.AnswerRecordCount = 1;
            }

            offset = 0;
            response.WriteBytes(buffer, ref offset);
            return new DnsRawClientResponse(offset, query, response);
        }

        private static DnsUnknownResource CreateServiceRecord()
        {
            // Ae.Dns 3.1 has no SRV model. Supply priority=0, weight=0, port=8080,
            // then let its domain serializer encode the target name.
            var bytes = new byte[256];
            bytes[4] = 0x1f;
            bytes[5] = 0x90;
            var offset = 6;
            new DnsDomainResource { Entries = "service.dns.test" }.WriteBytes(bytes, ref offset);
            return new DnsUnknownResource { Raw = new ReadOnlyMemory<byte>(bytes, 0, offset) };
        }
    }
}
