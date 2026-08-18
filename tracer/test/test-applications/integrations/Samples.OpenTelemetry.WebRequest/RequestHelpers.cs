using System;
using System.Net;
using System.Threading.Tasks;

namespace Samples.OpenTelemetry.WebRequest
{
    public static class RequestHelpers
    {
        // This set of requests is designed to exercise the HTTP client span requirements from the
        // OpenTelemetry HTTP Semantic Conventions:
        // - standard vs. unknown request methods
        // - 3xx/4xx/5xx status-to-error mapping (which differs from the Datadog default for 5xx)
        // - url.full credential/query redaction
        public static async Task SendRequestsAsync(string url)
        {
            using (SampleHelpers.CreateScope("WebRequestRequestAsync"))
            {
                // Standard method, successful response: http.request.method / url.full / server.address /
                // server.port / http.response.status_code all present, span not an error.
                await GetResponseAsync($"{url}ok");
                Console.WriteLine("Received response for GET ok");

                // Sensitive query string: url.query / url.full obfuscation.
                await GetResponseAsync($"{url}ok?token=SUPER-SECRET-TOKEN-VALUE");
                Console.WriteLine("Received response for GET ok?token=...");

                // Unknown/non-standard method: http.request.method must be _OTHER and the span name
                // must fall back to "HTTP" (not the raw verb).
                await GetResponseAsync($"{url}ok", method: "FOO");
                Console.WriteLine("Received response for FOO ok");

                // 3xx response: not an error under either Datadog or OTel semantics. Auto-redirect is
                // disabled so the 3xx is observed directly on this span instead of the followed request.
                await GetResponseAsync($"{url}redirect", allowAutoRedirect: false);
                Console.WriteLine("Received response for GET redirect");

                // 4xx response: an error under both Datadog and OTel semantics.
                await GetResponseAsync($"{url}client-error");
                Console.WriteLine("Received response for GET client-error");

                // 5xx response: only an error under OTel semantics (Datadog's default client error
                // range is 400-499; OTel's is 400-599).
                await GetResponseAsync($"{url}server-error");
                Console.WriteLine("Received response for GET server-error");

                // Credentials embedded in the URL: url.full must redact them to
                // "https://REDACTED:REDACTED@host" (or the http equivalent).
                var urlWithCredentials = url.Replace("://", "://user:pass@");
                await GetResponseAsync($"{urlWithCredentials}ok");
                Console.WriteLine("Received response for GET ok with credentials");
            }
        }

        private static async Task GetResponseAsync(string url, string method = null, bool allowAutoRedirect = true)
        {
            var request = (HttpWebRequest)System.Net.WebRequest.Create(url);
            request.AllowAutoRedirect = allowAutoRedirect;

            if (method != null)
            {
                request.Method = method;
            }

            try
            {
                using var response = (HttpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                // 4xx/5xx responses surface as a WebException with the response attached rather than
                // a normal return value.
                errorResponse.Dispose();
            }
        }
    }
}
