using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DnsClient;

namespace Samples.DnsClient
{
    public static class Program
    {
        public static async Task<int> Main()
        {
            Console.WriteLine("Starting Samples.DnsClient");

            // Use well-known public resolvers so the sample is deterministic and does
            // not depend on the CI host's system-configured name servers.
            var options = new LookupClientOptions(
                new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53),
                new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53))
            {
                // Keep timeouts short so error/timeout scenarios do not stall the run.
                Timeout = TimeSpan.FromSeconds(5),
                UseCache = false,
                Retries = 1,
            };

            var lookup = new LookupClient(options);

            // --- Synchronous query paths (LookupClient.QueryInternal) ---
            RunSync("Query A", () => lookup.Query("datadoghq.com", QueryType.A));
            RunSync("Query AAAA", () => lookup.Query("datadoghq.com", QueryType.AAAA));
            RunSync("Query MX", () => lookup.Query("datadoghq.com", QueryType.MX));
            RunSync("Query TXT", () => lookup.Query("datadoghq.com", QueryType.TXT));
            RunSync("Query with DnsQuestion", () => lookup.Query(new DnsQuestion("google.com", QueryType.A, QueryClass.IN)));
            RunSync("QueryReverse", () => lookup.QueryReverse(IPAddress.Parse("8.8.8.8")));

            // Extension method (GetHostEntry) which converges on QueryInternal internally.
            try
            {
                var hostEntry = lookup.GetHostEntry("google.com");
                Console.WriteLine($"[sync ] GetHostEntry: host={hostEntry?.HostName}, addresses={hostEntry?.AddressList?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sync ] GetHostEntry: handled {ex.GetType().Name}: {ex.Message}");
            }

            // --- Asynchronous query paths (LookupClient.QueryInternalAsync) ---
            await RunAsync("QueryAsync A", () => lookup.QueryAsync("datadoghq.com", QueryType.A));
            await RunAsync("QueryAsync AAAA", () => lookup.QueryAsync("datadoghq.com", QueryType.AAAA));
            await RunAsync("QueryReverseAsync", () => lookup.QueryReverseAsync(IPAddress.Parse("1.1.1.1")));

            // Async extension method (GetHostEntryAsync) which converges on QueryInternalAsync.
            try
            {
                var hostEntry = await lookup.GetHostEntryAsync("google.com");
                Console.WriteLine($"[async] GetHostEntryAsync: host={hostEntry?.HostName}, addresses={hostEntry?.AddressList?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[async] GetHostEntryAsync: handled {ex.GetType().Name}: {ex.Message}");
            }

            // --- Error path: NXDOMAIN surfaced as a non-success response code (ThrowDnsErrors=false, the default) ---
            // The default `lookup` client does not throw on DNS errors, so the failure is returned as a
            // response with a non-success ResponseCode (NotExistentDomain) and no exception. The instrumentation
            // must still mark the span as an error.
            RunSync("Query NXDOMAIN no-throw (expect error response)", () =>
                lookup.Query("this-domain-should-not-exist-datadog-apm.invalid", QueryType.A));
            await RunAsync("QueryAsync NXDOMAIN no-throw (expect error response)", () =>
                lookup.QueryAsync("this-domain-should-not-exist-datadog-apm.invalid", QueryType.A));

            // --- Error path: NXDOMAIN surfaced as an exception ---
            var throwingOptions = new LookupClientOptions(
                new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53))
            {
                ThrowDnsErrors = true,
                Timeout = TimeSpan.FromSeconds(5),
                UseCache = false,
                Retries = 1,
            };
            var throwingLookup = new LookupClient(throwingOptions);
            RunSync("Query NXDOMAIN (expect error)", () =>
                throwingLookup.Query("this-domain-should-not-exist-datadog-apm.invalid", QueryType.A));
            await RunAsync("QueryAsync NXDOMAIN (expect error)", () =>
                throwingLookup.QueryAsync("this-domain-should-not-exist-datadog-apm.invalid", QueryType.A));

            // --- Error path: unreachable name server surfaced as a timeout/socket error ---
            var unreachableOptions = new LookupClientOptions(
                new IPEndPoint(IPAddress.Parse("192.0.2.1"), 53)) // TEST-NET-1, guaranteed non-routable
            {
                Timeout = TimeSpan.FromMilliseconds(500),
                UseCache = false,
                Retries = 0,
            };
            var unreachableLookup = new LookupClient(unreachableOptions);
            RunSync("Query unreachable server (expect error)", () =>
                unreachableLookup.Query("datadoghq.com", QueryType.A));

            Console.WriteLine("Samples.DnsClient completed");
            return 0;
        }

        private static void RunSync(string scenario, Func<IDnsQueryResponse> action)
        {
            try
            {
                var response = action();
                Console.WriteLine(
                    $"[sync ] {scenario}: code={response.Header.ResponseCode}, answers={response.Answers.Count}, server={response.NameServer}");
                foreach (var record in response.Answers.Take(3))
                {
                    Console.WriteLine($"    {record}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sync ] {scenario}: handled {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static async Task RunAsync(string scenario, Func<Task<IDnsQueryResponse>> action)
        {
            try
            {
                var response = await action();
                Console.WriteLine(
                    $"[async] {scenario}: code={response.Header.ResponseCode}, answers={response.Answers.Count}, server={response.NameServer}");
                foreach (var record in response.Answers.Take(3))
                {
                    Console.WriteLine($"    {record}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[async] {scenario}: handled {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
