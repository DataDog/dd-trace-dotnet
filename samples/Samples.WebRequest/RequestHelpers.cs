using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.WebRequest
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task SendWebClientRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebClient] sending requests to {url}");

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Utf8;

                if (tracingDisabled)
                {
                    webClient.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                }

                using (Tracer.Instance.StartActive("WebClient"))
                {
                    using (Tracer.Instance.StartActive("DownloadData"))
                    {
                        webClient.DownloadData(url);
                        Console.WriteLine("Received response for client.DownloadData(String)");

                        webClient.DownloadData(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadData(Uri)");
                    }

                    using (Tracer.Instance.StartActive("DownloadDataAsync"))
                    {
                        webClient.DownloadDataAsyncAndWait(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri)");

                        webClient.DownloadDataAsyncAndWait(new Uri(url), null);
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadDataTaskAsync"))
                    {
                        await webClient.DownloadDataTaskAsync(url);
                        Console.WriteLine("Received response for client.DownloadDataTaskAsync(String)");

                        await webClient.DownloadDataTaskAsync(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadDataTaskAsync(Uri)");
                    }

                    using (Tracer.Instance.StartActive("DownloadFile"))
                    {
                        webClient.DownloadFile(url, "DownloadFile.string.txt");
                        Console.WriteLine("Received response for client.DownloadFile(String, String)");

                        webClient.DownloadFile(new Uri(url), "DownloadFile.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFile(Uri, String)");
                    }

                    using (Tracer.Instance.StartActive("DownloadFileAsync"))
                    {
                        webClient.DownloadFileAsyncAndWait(new Uri(url), "DownloadFileAsync.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String)");

                        webClient.DownloadFileAsyncAndWait(new Uri(url), "DownloadFileAsync.uri_token.txt", null);
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadFileTaskAsync"))
                    {
                        await webClient.DownloadFileTaskAsync(url, "DownloadFileTaskAsync.string.txt");
                        Console.WriteLine("Received response for client.DownloadFileTaskAsync(String, String)");

                        await webClient.DownloadFileTaskAsync(new Uri(url), "DownloadFileTaskAsync.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFileTaskAsync(Uri, String)");
                    }

                    using (Tracer.Instance.StartActive("DownloadString"))
                    {
                        webClient.DownloadString(url);
                        Console.WriteLine("Received response for client.DownloadString(String)");

                        webClient.DownloadString(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadString(Uri)");
                    }

                    using (Tracer.Instance.StartActive("DownloadStringAsync"))
                    {
                        webClient.DownloadStringAsyncAndWait(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri)");

                        webClient.DownloadStringAsyncAndWait(new Uri(url), null);
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadStringTaskAsync"))
                    {
                        await webClient.DownloadStringTaskAsync(url);
                        Console.WriteLine("Received response for client.DownloadStringTaskAsync(String)");

                        await webClient.DownloadStringTaskAsync(new Uri(url));
                        Console.WriteLine("Received response for client.DownloadStringTaskAsync(Uri)");
                    }

                    using (Tracer.Instance.StartActive("OpenRead"))
                    {
                        webClient.OpenRead(url).Close();
                        Console.WriteLine("Received response for client.OpenRead(String)");

                        webClient.OpenRead(new Uri(url)).Close();
                        Console.WriteLine("Received response for client.OpenRead(Uri)");
                    }

                    using (Tracer.Instance.StartActive("OpenReadAsync"))
                    {
                        webClient.OpenReadAsyncAndWait(new Uri(url));
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri)");

                        webClient.OpenReadAsyncAndWait(new Uri(url), null);
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("OpenReadTaskAsync"))
                    {
                        using Stream readStream1 = await webClient.OpenReadTaskAsync(url);
                        Console.WriteLine("Received response for client.OpenReadTaskAsync(String)");

                        using Stream readStream2 = await webClient.OpenReadTaskAsync(new Uri(url));
                        Console.WriteLine("Received response for client.OpenReadTaskAsync(Uri)");
                    }

                    using (Tracer.Instance.StartActive("UploadData"))
                    {
                        webClient.UploadData(url, new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(String, Byte[])");

                        webClient.UploadData(new Uri(url), new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(Uri, Byte[])");

                        webClient.UploadData(url, "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(String, String, Byte[])");

                        webClient.UploadData(new Uri(url), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(Uri, String, Byte[])");
                    }

                    using (Tracer.Instance.StartActive("UploadDataAsync"))
                    {
                        webClient.UploadDataAsyncAndWait(new Uri(url), new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, Byte[])");

                        webClient.UploadDataAsyncAndWait(new Uri(url), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[])");

                        webClient.UploadDataAsyncAndWait(new Uri(url), "POST", new byte[0], null);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[], Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadDataTaskAsync"))
                    {
                        await webClient.UploadDataTaskAsync(url, new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, Byte[])");

                        await webClient.UploadDataTaskAsync(new Uri(url), new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(Uri, Byte[])");

                        await webClient.UploadDataTaskAsync(url, "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, String, Byte[])");

                        await webClient.UploadDataTaskAsync(new Uri(url), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(Uri, String, Byte[])");
                    }

                    File.WriteAllText("UploadFile.txt", requestContent);

                    using (Tracer.Instance.StartActive("UploadFile"))
                    {
                        webClient.UploadFile(url, "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(String, String)");

                        webClient.UploadFile(new Uri(url), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(Uri, String)");

                        webClient.UploadFile(url, "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(String, String, String)");

                        webClient.UploadFile(new Uri(url), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(Uri, String, String)");
                    }

                    using (Tracer.Instance.StartActive("UploadFileAsync"))
                    {
                        webClient.UploadFileAsyncAndWait(new Uri(url), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String)");

                        webClient.UploadFileAsyncAndWait(new Uri(url), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String)");

                        webClient.UploadFileAsyncAndWait(new Uri(url), "POST", "UploadFile.txt", null);
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadFileTaskAsync"))
                    {
                        await webClient.UploadFileTaskAsync(url, "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String)");

                        await webClient.UploadFileTaskAsync(new Uri(url), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(Uri, String)");

                        await webClient.UploadFileTaskAsync(url, "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String, String)");

                        await webClient.UploadFileTaskAsync(new Uri(url), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(Uri, String, String)");
                    }

                    using (Tracer.Instance.StartActive("UploadString"))
                    {
                        webClient.UploadString(url, requestContent);
                        Console.WriteLine("Received response for client.UploadString(String, String)");

                        webClient.UploadString(new Uri(url), requestContent);
                        Console.WriteLine("Received response for client.UploadString(Uri, String)");

                        webClient.UploadString(url, "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadString(String, String, String)");

                        webClient.UploadString(new Uri(url), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadString(Uri, String, String)");
                    }

                    using (Tracer.Instance.StartActive("UploadStringAsync"))
                    {
                        webClient.UploadStringAsyncAndWait(new Uri(url), requestContent);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String)");

                        webClient.UploadStringAsyncAndWait(new Uri(url), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String)");

                        webClient.UploadStringAsyncAndWait(new Uri(url), "POST", requestContent, null);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadStringTaskAsync"))
                    {
                        await webClient.UploadStringTaskAsync(url, requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String)");

                        await webClient.UploadStringTaskAsync(new Uri(url), requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(Uri, String)");

                        await webClient.UploadStringTaskAsync(url, "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String, String)");

                        await webClient.UploadStringTaskAsync(new Uri(url), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(Uri, String, String)");
                    }

                    var values = new NameValueCollection();
                    using (Tracer.Instance.StartActive("UploadValues"))
                    {
                        webClient.UploadValues(url, values);
                        Console.WriteLine("Received response for client.UploadValues(String, NameValueCollection)");

                        webClient.UploadValues(new Uri(url), values);
                        Console.WriteLine("Received response for client.UploadValues(Uri, NameValueCollection)");

                        webClient.UploadValues(url, "POST", values);
                        Console.WriteLine("Received response for client.UploadValues(String, String, NameValueCollection)");

                        webClient.UploadValues(new Uri(url), "POST", values);
                        Console.WriteLine("Received response for client.UploadValues(Uri, String, NameValueCollection)");
                    }

                    using (Tracer.Instance.StartActive("UploadValuesAsync"))
                    {
                        webClient.UploadValuesAsyncAndWait(new Uri(url), values);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, NameValueCollection)");

                        webClient.UploadValuesAsyncAndWait(new Uri(url), "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection)");

                        webClient.UploadValuesAsyncAndWait(new Uri(url), "POST", values, null);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadValuesTaskAsync"))
                    {
                        await webClient.UploadValuesTaskAsync(url, values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(new Uri(url), values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(url, "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, String, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(new Uri(url), "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, String, NameValueCollection)");
                    }
                }
            }
        }

        public static async Task SendWebRequestRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebRequest] sending requests to {url}");

            using (Tracer.Instance.StartActive("WebRequest"))
            {
                using (Tracer.Instance.StartActive("GetResponse"))
                {
                    // Create two separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(url);
                    if (tracingDisabled)
                    {
                        request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                    }

                    request.GetResponse().Close();
                    Console.WriteLine("Received response for request.GetResponse()");
                }

                using (Tracer.Instance.StartActive("GetResponseAsync"))
                {
                    // Create two separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(url);
                    if (tracingDisabled)
                    {
                        request.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                    }

                    (await request.GetResponseAsync()).Close();
                    Console.WriteLine("Received response for request.GetResponseAsync()");
                }
            }
        }
    }
}
