using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;

namespace Samples.DnsClient
{
    public static class Program
    {
        public static async Task<int> Main()
        {
            using var server = new DnsServer(failover: true);
            using var secondary = new DnsServer();

            // Every run covers both instrumentations and every scenario. No public DNS is used.
            await RunQueries(server, secondary, useAsync: false);
            await RunQueries(server, secondary, useAsync: true);

            var lookup = new LookupClient(CreateOptions(server));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            // DnsClient wraps cancellation in DnsResponseException in all supported versions.
            var cancelled = await ExpectException<DnsResponseException>(() => lookup.QueryAsync("cancel.dns.test", QueryType.A, cancellationToken: cancellation.Token));
            Require(cancelled.InnerException is OperationCanceledException, "Cancellation should preserve the original exception");
            Require(server.RequestCount("cancel.dns.test") == 0, "A pre-cancelled query should not send a request");

            Console.WriteLine("Samples.DnsClient completed");
            return 0;
        }

        private static async Task RunQueries(DnsServer server, DnsServer secondary, bool useAsync)
        {
            var suffix = useAsync ? "async.dns.test" : "sync.dns.test";
            var lookup = new LookupClient(CreateOptions(server));
            var endpoints = new[] { server.EndPoint };
            var nameServers = new[] { new NameServer(server.EndPoint) };
            var queryOptions = CreateOptions(server);

            foreach (var type in new[] { QueryType.A, QueryType.AAAA, QueryType.MX, QueryType.TXT })
            {
                await Query(lookup, $"{type.ToString().ToLowerInvariant()}.{suffix}", useAsync, type);
            }

            var question = new DnsQuestion($"question.{suffix}", QueryType.A);
            CheckResponse(useAsync ? await lookup.QueryAsync(question) : lookup.Query(question));
            question = new DnsQuestion($"options.{suffix}", QueryType.A);
            CheckResponse(useAsync ? await lookup.QueryAsync(question, queryOptions) : lookup.Query(question, queryOptions));
            CheckResponse(useAsync ? await lookup.QueryServerAsync(endpoints, $"server.{suffix}", QueryType.A) : lookup.QueryServer(endpoints, $"server.{suffix}", QueryType.A));
            question = new DnsQuestion($"server-options.{suffix}", QueryType.A);
            CheckResponse(useAsync ? await lookup.QueryServerAsync(nameServers, question, queryOptions) : lookup.QueryServer(nameServers, question, queryOptions));

            // Distinct addresses make the PTR spans identifiable in snapshots.
            var addressBase = useAsync ? 10 : 0;
            IPAddress Address(int offset) => IPAddress.Parse($"192.0.2.{addressBase + offset}");
            CheckResponse(useAsync ? await lookup.QueryReverseAsync(Address(1)) : lookup.QueryReverse(Address(1)));
            CheckResponse(useAsync ? await lookup.QueryReverseAsync(Address(2), queryOptions) : lookup.QueryReverse(Address(2), queryOptions));
            CheckResponse(useAsync ? await lookup.QueryServerReverseAsync(endpoints, Address(3)) : lookup.QueryServerReverse(endpoints, Address(3)));
            CheckResponse(useAsync ? await lookup.QueryServerReverseAsync(nameServers, Address(4), queryOptions) : lookup.QueryServerReverse(nameServers, Address(4), queryOptions));

            // Host entry fans out to A + AAAA; the address overload adds a PTR query first.
            var host = useAsync ? await lookup.GetHostEntryAsync($"host.{suffix}") : lookup.GetHostEntry($"host.{suffix}");
            Require(host.AddressList.Length == 2, "GetHostEntry should resolve IPv4 and IPv6");
            host = useAsync ? await lookup.GetHostEntryAsync(Address(5)) : lookup.GetHostEntry(Address(5));
            Require(host.AddressList.Length == 2, "GetHostEntry(address) should resolve IPv4 and IPv6");
            var hostName = useAsync ? await lookup.GetHostNameAsync(Address(6)) : lookup.GetHostName(Address(6));
            Require(hostName.TrimEnd('.') == $"host-{addressBase + 6}.dns.test", "GetHostName should resolve PTR");
            var services = useAsync ? await lookup.ResolveServiceAsync(suffix, "http", "tcp") : lookup.ResolveService(suffix, "http", "tcp");
            Require(services.Length == 1 && services[0].Port == 8080, "ResolveService should resolve SRV");

            var tcpOptions = CreateOptions(server);
            tcpOptions.UseTcpOnly = true;
            await Query(new LookupClient(tcpOptions), $"tcp.{suffix}", useAsync);
            Require(server.RequestCount($"tcp.{suffix}", tcp: true) == 1 && server.RequestCount($"tcp.{suffix}") == 0, "TCP-only should not use UDP");
            await Query(lookup, $"truncated.{suffix}", useAsync);
            Require(server.RequestCount($"truncated.{suffix}") == 1 && server.RequestCount($"truncated.{suffix}", tcp: true) == 1, "Truncated UDP should fall back to TCP");

            var cacheOptions = CreateOptions(server);
            cacheOptions.UseCache = true;
            var cachedLookup = new LookupClient(cacheOptions);
            var cacheName = $"cache.{suffix}";
            await Query(cachedLookup, cacheName, useAsync);
            await Query(cachedLookup, cacheName, useAsync);
            // QueryCache was added after 1.3. It must not generate a span or a network request.
            var queryCache = typeof(LookupClient).GetMethod("QueryCache", new[] { typeof(DnsQuestion) });
            if (queryCache != null)
            {
                CheckResponse((IDnsQueryResponse)queryCache.Invoke(cachedLookup, new object[] { new DnsQuestion(cacheName, QueryType.A) }));
                Require(queryCache.Invoke(cachedLookup, new object[] { new DnsQuestion($"cache-miss.{suffix}", QueryType.A) }) == null, "QueryCache should return null for a cache miss");
                Require(server.RequestCount($"cache-miss.{suffix}") == 0, "QueryCache misses should not send a request");
            }

            Require(server.RequestCount(cacheName) == 1, "Cache hits should not send another request");

            await Query(lookup, $"nxdomain.{suffix}", useAsync, code: "NotExistentDomain", answers: 0);
            await Query(lookup, $"servfail.{suffix}", useAsync, code: "ServerFailure", answers: 0);
            await Query(lookup, $"empty.{suffix}", useAsync, answers: 0);
            var throwingOptions = CreateOptions(server);
            throwingOptions.ThrowDnsErrors = true;
            var dnsError = await ExpectException<DnsResponseException>(() => Query(new LookupClient(throwingOptions), $"nxdomain-throw.{suffix}", useAsync));
            Require(dnsError.Code == DnsResponseCode.NotExistentDomain, "ThrowDnsErrors should report NXDOMAIN");
            var timeout = await ExpectException<DnsResponseException>(() => Query(lookup, $"timeout.{suffix}", useAsync));
            Require(timeout.Code == DnsResponseCode.ConnectionTimeout, "A silent server should time out");

            var retryOptions = CreateOptions(server);
            retryOptions.Retries = 1;
            await Query(new LookupClient(retryOptions), $"retry.{suffix}", useAsync);
            Require(server.RequestCount($"retry.{suffix}") == 2, "Retry should recover after the first dropped packet");

            var failoverOptions = CreateOptions(server, secondary);
            failoverOptions.ContinueOnDnsError = true;
            var response = await Query(new LookupClient(failoverOptions), $"failover.{suffix}", useAsync);
            Require(response.NameServer.Port == secondary.EndPoint.Port, "Failover should return the answering server");
            Require(server.RequestCount($"failover.{suffix}") == 1 && secondary.RequestCount($"failover.{suffix}") == 1, "Failover should query both servers");
            Console.WriteLine($"Completed all {suffix} scenarios");
        }

        private static LookupClientOptions CreateOptions(params DnsServer[] servers) => new(servers.Select(s => s.EndPoint).ToArray())
        {
            UseCache = false,
            UseRandomNameServer = false,
            Retries = 0,
            Timeout = TimeSpan.FromSeconds(1),
            ThrowDnsErrors = false,
            ContinueOnDnsError = false,
        };

        private static async Task<IDnsQueryResponse> Query(LookupClient lookup, string name, bool useAsync, QueryType type = QueryType.A, string code = "NoError", int answers = 1)
        {
            var response = useAsync ? await lookup.QueryAsync(name, type) : lookup.Query(name, type);
            CheckResponse(response, code, answers);
            return response;
        }

        private static void CheckResponse(IDnsQueryResponse response, string code = "NoError", int answers = 1)
        {
            Require(response != null && response.Header.ResponseCode.ToString() == code && response.Answers.Count == answers, $"Expected {code} with {answers} answers");
            Console.WriteLine($"{response.Questions[0]}: {response.Header.ResponseCode}, answers={response.Answers.Count}, server={response.NameServer}");
        }

        private static async Task<T> ExpectException<T>(Func<Task> action)
            where T : Exception
        {
            try
            {
                await action();
            }
            catch (T exception)
            {
                Console.WriteLine($"Expected {exception.GetType().Name}: {exception.Message}");
                return exception;
            }

            throw new InvalidOperationException($"Expected {typeof(T).Name}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
