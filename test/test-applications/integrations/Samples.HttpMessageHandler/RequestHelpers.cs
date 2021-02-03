using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.HttpMessageHandler
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task SendHttpClientRequestsAsync(HttpClient client, bool tracingDisabled, string url, string requestContent)
        {
            // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
            Console.WriteLine($"[HttpClient] sending requests to {url}");

            if (tracingDisabled)
            {
                client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            }

            using (Tracer.Instance.StartActive("HttpClientRequestAsync"))
            {
                using (Tracer.Instance.StartActive("DeleteAsync"))
                {
                    await client.DeleteAsync(url);
                    Console.WriteLine("Received response for client.DeleteAsync(String)");

                    await client.DeleteAsync(new Uri(url));
                    Console.WriteLine("Received response for client.DeleteAsync(Uri)");

                    await client.DeleteAsync(url, CancellationToken.None);
                    Console.WriteLine("Received response for client.DeleteAsync(String, CancellationToken)");

                    await client.DeleteAsync(new Uri(url), CancellationToken.None);
                    Console.WriteLine("Received response for client.DeleteAsync(Uri, CancellationToken)");
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
                    using Stream stream1 = await client.GetStreamAsync(url);
                    Console.WriteLine("Received response for client.GetStreamAsync(String)");

                    using Stream stream2 = await client.GetStreamAsync(new Uri(url));
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
                    Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage)");

                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None);
                    Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, CancellationToken)");

                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, HttpCompletionOption)");

                    await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("ErrorSpanBelow"))
                {
                    await client.GetAsync($"{url}HttpErrorCode");
                    Console.WriteLine("Received response for client.GetAsync Error Span");
                }
            }
        }

#if NET5_0
        public static void SendHttpClientRequests(HttpClient client, bool tracingDisabled, string url, string requestContent)
        {
            // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
            Console.WriteLine($"[HttpClient] sending sync requests to {url}");

            if (tracingDisabled)
            {
                client.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            }

            using (Tracer.Instance.StartActive("HttpClientRequest"))
            {
                using (Tracer.Instance.StartActive("Send.Delete"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Delete, url));
                    Console.WriteLine("Received response for DELETE client.Send(HttpRequestMessage)");

                    client.Send(new HttpRequestMessage(HttpMethod.Delete, url), CancellationToken.None);
                    Console.WriteLine("Received response for DELETE client.Send(HttpRequestMessage, CancellationToken)");

                    client.Send(new HttpRequestMessage(HttpMethod.Delete, url), HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for DELETE client.Send(HttpRequestMessage, HttpCompletionOption)");

                    client.Send(new HttpRequestMessage(HttpMethod.Delete, url), HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for DELETE client.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("Send.Get"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Get, url));
                    Console.WriteLine("Received response for GET client.Send(HttpRequestMessage)");

                    client.Send(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None);
                    Console.WriteLine("Received response for GET client.Send(HttpRequestMessage, CancellationToken)");

                    client.Send(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for GET client.Send(HttpRequestMessage, HttpCompletionOption)");

                    client.Send(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for GET client.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("Send.Patch"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(requestContent, Utf8) });
                    Console.WriteLine("Received response for PATCH client.Send(HttpRequestMessage)");

                    client.Send(new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(requestContent, Utf8) }, CancellationToken.None);
                    Console.WriteLine("Received response for PATCH client.Send(HttpRequestMessage, CancellationToken)");

                    client.Send(new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for PATCH client.Send(HttpRequestMessage, HttpCompletionOption)");

                    client.Send(new HttpRequestMessage(HttpMethod.Patch, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for PATCH client.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("Send.Post"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(requestContent, Utf8) });
                    Console.WriteLine("Received response for POST client.Send(HttpRequestMessage)");

                    client.Send(new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(requestContent, Utf8) }, CancellationToken.None);
                    Console.WriteLine("Received response for POST client.Send(HttpRequestMessage, CancellationToken)");

                    client.Send(new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for POST client.Send(HttpRequestMessage, HttpCompletionOption)");

                    client.Send(new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for POST client.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("Send.Put"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Put, url) { Content = new StringContent(requestContent, Utf8) });
                    Console.WriteLine("Received response for PUT client.Send(HttpRequestMessage)");

                    client.Send(new HttpRequestMessage(HttpMethod.Put, url) { Content = new StringContent(requestContent, Utf8) }, CancellationToken.None);
                    Console.WriteLine("Received response for PUT client.Send(HttpRequestMessage, CancellationToken)");

                    client.Send(new HttpRequestMessage(HttpMethod.Put, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead);
                    Console.WriteLine("Received response for PUT client.Send(HttpRequestMessage, HttpCompletionOption)");

                    client.Send(new HttpRequestMessage(HttpMethod.Put, url) { Content = new StringContent(requestContent, Utf8) }, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
                    Console.WriteLine("Received response for PUT client.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                }

                using (Tracer.Instance.StartActive("ErrorSpanBelow"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Get, $"{url}HttpErrorCode"));
                    Console.WriteLine("Received response for client.Get Error Span");
                }
            }
        }
#endif
    }
}
