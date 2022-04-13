using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.HttpMessageHandler
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;
        private const string TracingEnabled = "x-datadog-tracing-enabled";

        public static void SendAsyncHttpClientRequests(HttpClient client, bool tracingDisabled, string url, string requestContent)
        {
            void Invoke()
            {
                // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
                Console.WriteLine($"[HttpClient] sending requests to {url}");

                if (tracingDisabled)
                {
                    client.DefaultRequestHeaders.Add(TracingEnabled, "false");
                }

                using (SampleHelpers.CreateScope("HttpClientRequestAsync"))
                {
                    using (SampleHelpers.CreateScope("DeleteAsync"))
                    {
                        client.DeleteAsync(url).Wait();
                        Console.WriteLine("Received response for client.DeleteAsync(String)");

                        client.DeleteAsync(new Uri(url)).Wait();
                        Console.WriteLine("Received response for client.DeleteAsync(Uri)");

                        client.DeleteAsync(url, CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.DeleteAsync(String, CancellationToken)");

                        client.DeleteAsync(new Uri(url), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.DeleteAsync(Uri, CancellationToken)");
                    }

                    using (SampleHelpers.CreateScope("GetAsync"))
                    {
                        client.GetAsync(url).Wait();
                        Console.WriteLine("Received response for client.GetAsync(String)");

                        client.GetAsync(new Uri(url)).Wait();
                        Console.WriteLine("Received response for client.GetAsync(Uri)");

                        client.GetAsync(url, CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.GetAsync(String, CancellationToken)");

                        client.GetAsync(new Uri(url), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.GetAsync(Uri, CancellationToken)");

                        client.GetAsync(url, HttpCompletionOption.ResponseContentRead).Wait();
                        Console.WriteLine("Received response for client.GetAsync(String, HttpCompletionOption)");

                        client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead).Wait();
                        Console.WriteLine("Received response for client.GetAsync(Uri, HttpCompletionOption)");

                        client.GetAsync(url, HttpCompletionOption.ResponseContentRead, CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.GetAsync(String, HttpCompletionOption, CancellationToken)");

                        client.GetAsync(new Uri(url), HttpCompletionOption.ResponseContentRead, CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.GetAsync(Uri, HttpCompletionOption, CancellationToken)");
                    }

                    using (SampleHelpers.CreateScope("GetByteArrayAsync"))
                    {
                        client.GetByteArrayAsync(url).Wait();
                        Console.WriteLine("Received response for client.GetByteArrayAsync(String)");

                        client.GetByteArrayAsync(new Uri(url)).Wait();
                        Console.WriteLine("Received response for client.GetByteArrayAsync(Uri)");
                    }

                    using (SampleHelpers.CreateScope("GetStreamAsync"))
                    {
                        using Stream stream1 = client.GetStreamAsync(url).Result;
                        Console.WriteLine("Received response for client.GetStreamAsync(String)");

                        using Stream stream2 = client.GetStreamAsync(new Uri(url)).Result;
                        Console.WriteLine("Received response for client.GetStreamAsync(Uri)");
                    }

                    using (SampleHelpers.CreateScope("GetStringAsync"))
                    {
                        client.GetStringAsync(url).Wait();
                        Console.WriteLine("Received response for client.GetStringAsync(String)");

                        client.GetStringAsync(new Uri(url)).Wait();
                        Console.WriteLine("Received response for client.GetStringAsync(Uri)");
                    }

#if NETCOREAPP
                    using (SampleHelpers.CreateScope("PatchAsync"))
                    {
                        client.PatchAsync(url, new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PatchAsync(String, HttpContent)");

                        client.PatchAsync(new Uri(url), new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PatchAsync(Uri, HttpContent)");

                        client.PatchAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PatchAsync(String, HttpContent, CancellationToken)");

                        client.PatchAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PatchAsync(Uri, HttpContent, CancellationToken)");
                    }

#endif
                    using (SampleHelpers.CreateScope("PostAsync"))
                    {
                        client.PostAsync(url, new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PostAsync(String, HttpContent)");

                        client.PostAsync(new Uri(url), new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PostAsync(Uri, HttpContent)");

                        client.PostAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PostAsync(String, HttpContent, CancellationToken)");

                        client.PostAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PostAsync(Uri, HttpContent, CancellationToken)");
                    }

                    using (SampleHelpers.CreateScope("PutAsync"))
                    {
                        client.PutAsync(url, new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PutAsync(String, HttpContent)");

                        client.PutAsync(new Uri(url), new StringContent(requestContent, Utf8)).Wait();
                        Console.WriteLine("Received response for client.PutAsync(Uri, HttpContent)");

                        client.PutAsync(url, new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PutAsync(String, HttpContent, CancellationToken)");

                        client.PutAsync(new Uri(url), new StringContent(requestContent, Utf8), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.PutAsync(Uri, HttpContent, CancellationToken)");
                    }

                    using (SampleHelpers.CreateScope("SendAsync"))
                    {
                        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)).Wait();
                        Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage)");

                        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, CancellationToken)");

                        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead).Wait();
                        Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, HttpCompletionOption)");

                        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseContentRead, CancellationToken.None).Wait();
                        Console.WriteLine("Received response for client.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
                    }

                    using (SampleHelpers.CreateScope("ErrorSpanBelow"))
                    {
                        client.GetAsync($"{url}HttpErrorCode").Wait();
                        Console.WriteLine("Received response for client.GetAsync Error Span");
                    }
                }
            }

            // Wait for the tasks in a single threaded synchronization context, to detect potential deadlocks
            var synchronizationContext = new SingleThreadedSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            synchronizationContext.Send(_ => Invoke(), null);
            SynchronizationContext.SetSynchronizationContext(null);
        }

#if NET5_0_OR_GREATER
        public static void SendHttpClientRequests(HttpClient client, bool tracingDisabled, string url, string requestContent)
        {
            // Insert a call to the Tracer.Instance to include an AssemblyRef to Datadog.Trace assembly in the final executable
            Console.WriteLine($"[HttpClient] sending sync requests to {url}");

            if (tracingDisabled)
            {
                client.DefaultRequestHeaders.Add(TracingEnabled, "false");
            }

            using (SampleHelpers.CreateScope("HttpClientRequest"))
            {
                using (SampleHelpers.CreateScope("Send.Delete"))
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

                using (SampleHelpers.CreateScope("Send.Get"))
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

                using (SampleHelpers.CreateScope("Send.Patch"))
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

                using (SampleHelpers.CreateScope("Send.Post"))
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

                using (SampleHelpers.CreateScope("Send.Put"))
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

                using (SampleHelpers.CreateScope("ErrorSpanBelow"))
                {
                    client.Send(new HttpRequestMessage(HttpMethod.Get, $"{url}HttpErrorCode"));
                    Console.WriteLine("Received response for client.Get Error Span");
                }
            }
        }
#endif

        public static async Task SendHttpMessageInvokerRequestsAsync(HttpMessageInvoker invoker, bool tracingDisabled, string url)
        {
            Console.WriteLine($"[HttpMessageInvoker] sending requests to {url}");

            var httpRequest = new HttpRequestMessage();

            if (tracingDisabled)
            {
                httpRequest.Headers.Add(TracingEnabled, "false");
            }

            httpRequest.Method = HttpMethod.Get;
            httpRequest.RequestUri = new Uri(url);

            Console.WriteLine("Received response for HttpMessageInvoker.SendAsync");
            await invoker.SendAsync(httpRequest, CancellationToken.None);
        }

        private class SingleThreadedSynchronizationContext : SynchronizationContext
        {
            private int _queuedOperations;

            public override void Post(SendOrPostCallback d, object state)
            {
                Send(d, state);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                if (Interlocked.Increment(ref _queuedOperations) > 1)
                {
                    throw new InvalidOperationException("Deadlock condition detected");
                }

                try
                {
                    d(state);
                }
                finally
                {
                    Interlocked.Decrement(ref _queuedOperations);
                }
            }
        }
    }
}
