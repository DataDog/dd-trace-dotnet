using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.Shared
{
    public static class HttpClientHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task SendHttpClientRequestsAsync(bool tracingDisabled, string url, string requestContent)
        {
            // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
            Console.WriteLine($"[HttpClient] sending requests to {url}");

            using (var client = new HttpClient())
            {
                if (tracingDisabled)
                {
                    client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                }

                using (Tracer.Instance.StartActive("HttpClientRequestAsync"))
                {
                    using (Tracer.Instance.StartActive("DeleteAsync"))
                    {
                        await client.DeleteAsync(url);
                        Console.WriteLine("Received response for client.GetAsync(String)");

                        await client.DeleteAsync(new Uri(url));
                        Console.WriteLine("Received response for client.GetAsync(Uri)");

                        await client.DeleteAsync(url, CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(String, CancellationToken)");

                        await client.DeleteAsync(new Uri(url), CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(Uri, CancellationToken)");
                    }

                    using (Tracer.Instance.StartActive("GetAsync"))
                    {
                        await client.GetAsync(url);
                        Console.WriteLine("Received response for client.GetAsync(String)");

                        await client.GetAsync(new Uri(url));
                        Console.WriteLine("Received response for client.GetAsync(Uri)");

                        await client.GetAsync(url, CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(String, CancellationToken)");

                        await client.GetAsync(new Uri(url), CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(Uri, CancellationToken)");

                        await client.GetAsync(url, HttpCompletionOption.ResponseContentRead);
                        Console.WriteLine("Received response for client.GetAsync(String, HttpCompletionOption)");

                        await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead);
                        Console.WriteLine("Received response for client.GetAsync(Uri, HttpCompletionOption)");

                        await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(String, HttpCompletionOption, CancellationToken)");

                        await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                        Console.WriteLine("Received response for client.GetAsync(Uri, HttpCompletionOption, CancellationToken)");
                    }

                    using (Tracer.Instance.StartActive("GetByteArrayAsync"))
                    {
                        await client.GetByteArrayAsync(url);
                        Console.WriteLine("Received response for client.GetByteArrayAsync(String)");

                        await client.GetByteArrayAsync(new Uri(url));
                        Console.WriteLine("Received response for client.GetByteArrayAsync(Uri)");
                    }

                    using (Tracer.Instance.StartActive("GetStreamAsync"))
                    {
                        (await client.GetStreamAsync(url)).Close();
                        Console.WriteLine("Received response for client.GetStreamAsync(String)");

                        (await client.GetStreamAsync(new Uri(url))).Close();
                        Console.WriteLine("Received response for client.GetStreamAsync(Uri)");
                    }

                    using (Tracer.Instance.StartActive("GetStringAsync"))
                    {
                        await client.GetStringAsync(url);
                        Console.WriteLine("Received response for client.GetStringAsync(String)");

                        await client.GetStringAsync(new Uri(url));
                        Console.WriteLine("Received response for client.GetStringAsync(Uri)");
                    }

#if NETCOREAPP
                    using (Tracer.Instance.StartActive("PatchAsync"))
                    {
                        await client.PatchAsync(url, new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PatchAsync(String, HttpContent)");

                        await client.PatchAsync(new Uri(url), new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PatchAsync(Uri, HttpContent)");

                        await client.PatchAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PatchAsync(String, HttpContent, CancellationToken)");

                        await client.PatchAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PatchAsync(Uri, HttpContent, CancellationToken)");
                    }

#endif
                    using (Tracer.Instance.StartActive("PostAsync"))
                    {
                        await client.PostAsync(url, new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PostAsync(String, HttpContent)");

                        await client.PostAsync(new Uri(url), new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PostAsync(Uri, HttpContent)");

                        await client.PostAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PostAsync(String, HttpContent, CancellationToken)");

                        await client.PostAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PostAsync(Uri, HttpContent, CancellationToken)");
                    }

                    using (Tracer.Instance.StartActive("PutAsync"))
                    {
                        await client.PutAsync(url, new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PutAsync(String, HttpContent)");

                        await client.PutAsync(new Uri(url), new StringContent(requestContent, Utf8));
                        Console.WriteLine("Received response for client.PutAsync(Uri, HttpContent)");

                        await client.PutAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PutAsync(String, HttpContent, CancellationToken)");

                        await client.PutAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None);
                        Console.WriteLine("Received response for client.PutAsync(Uri, HttpContent, CancellationToken)");
                    }

                    using (Tracer.Instance.StartActive("SendAsync"))
                    {
                        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                        Console.WriteLine("Received response for client.PostAsync(HttpRequestMessage)");

                        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None);
                        Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, CancellationToken)");

                        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead);
                        Console.WriteLine("Received response for client.PostAsync(HttpRequestMessage, HttpCompletionOption)");

                        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                        Console.WriteLine("Received response for client.PostAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                    }
                }
            }
        }
    }
}
