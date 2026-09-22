using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Samples.OpenTelemetry.HttpClient
{
    public static class RequestHelpers
    {
        // This set of requests is designed to exercise the HTTP client span requirements from the
        // OpenTelemetry HTTP Semantic Conventions:
        // - standard vs. unknown request methods
        // - 3xx/4xx/5xx status-to-error mapping (which differs from the Datadog default for 5xx)
        // - url.full credential/query redaction
        public static async Task SendRequestsAsync(System.Net.Http.HttpClient client, string url)
        {
            using (SampleHelpers.CreateScope("HttpClientRequestAsync"))
            {
                // Standard method, successful response: http.request.method / url.full / server.address /
                // server.port / http.response.status_code all present, span not an error.
                await client.GetAsync($"{url}ok");
                Console.WriteLine("Received response for GET ok");

                // Sensitive query string: url.query / url.full obfuscation.
                await client.GetAsync($"{url}ok?token=SUPER-SECRET-TOKEN-VALUE");
                Console.WriteLine("Received response for GET ok?token=...");

                // Unknown/non-standard method: http.request.method must be _OTHER and the span name
                // must fall back to "HTTP" (not the raw verb).
                await client.SendAsync(new HttpRequestMessage(new HttpMethod("FOO"), $"{url}ok"));
                Console.WriteLine("Received response for FOO ok");

                // 3xx response: not an error under either Datadog or OTel semantics.
                await client.GetAsync($"{url}redirect");
                Console.WriteLine("Received response for GET redirect");

                // 4xx response: an error under both Datadog and OTel semantics.
                await client.GetAsync($"{url}client-error");
                Console.WriteLine("Received response for GET client-error");

                // 5xx response: only an error under OTel semantics (Datadog's default client error
                // range is 400-499; OTel's is 400-599).
                await client.GetAsync($"{url}server-error");
                Console.WriteLine("Received response for GET server-error");

                // Credentials embedded in the URL: url.full must redact them to
                // "https://REDACTED:REDACTED@host" (or the http equivalent).
                var urlWithCredentials = url.Replace("://", "://user:pass@");
                await client.GetAsync($"{urlWithCredentials}ok");
                Console.WriteLine("Received response for GET ok with credentials");
            }
        }
    }
}
