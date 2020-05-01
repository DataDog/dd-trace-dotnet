using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Datadog.Trace;
using Datadog.Trace.Configuration;

namespace Samples.Shared
{
    public static class WebClientHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static void SendWebClientsRequest(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebClient] sending requests to {url}");

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Utf8;

                if (tracingDisabled)
                {
                    webClient.Headers.Add(HttpHeaderNames.TracingEnabled, "false");
                }

                using (Tracer.Instance.StartActive("WebClientRequest"))
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
                        webClient.DownloadDataAsync(new Uri(url));
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri)");

                        webClient.DownloadDataAsync(new Uri(url), null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadDataTaskAsync"))
                    {
                        webClient.DownloadDataTaskAsync(url).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.DownloadDataTaskAsync(String)");

                        webClient.DownloadDataTaskAsync(new Uri(url)).GetAwaiter().GetResult();
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
                        webClient.DownloadFileAsync(new Uri(url), "DownloadFileAsync.uri.txt");
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String)");

                        webClient.DownloadFileAsync(new Uri(url), "DownloadFileAsync.uri_token.txt", null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadFileTaskAsync"))
                    {
                        webClient.DownloadFileTaskAsync(url, "DownloadFileTaskAsync.string.txt").GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.DownloadFileTaskAsync(String, String)");

                        webClient.DownloadFileTaskAsync(new Uri(url), "DownloadFileTaskAsync.uri.txt").GetAwaiter().GetResult();
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
                        webClient.DownloadStringAsync(new Uri(url));
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri)");

                        webClient.DownloadStringAsync(new Uri(url), null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("DownloadStringTaskAsync"))
                    {
                        webClient.DownloadStringTaskAsync(url).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.DownloadStringTaskAsync(String)");

                        webClient.DownloadStringTaskAsync(new Uri(url)).GetAwaiter().GetResult();
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
                        webClient.OpenReadAsync(new Uri(url));
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri)");

                        webClient.OpenReadAsync(new Uri(url), null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri, Object)");
                    }

                    using (Tracer.Instance.StartActive("OpenReadTaskAsync"))
                    {
                        webClient.OpenReadTaskAsync(url).GetAwaiter().GetResult().Close();
                        Console.WriteLine("Received response for client.OpenReadTaskAsync(String)");

                        webClient.OpenReadTaskAsync(new Uri(url)).GetAwaiter().GetResult().Close();
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
                        webClient.UploadDataAsync(new Uri(url), new byte[0]);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, Byte[])");

                        webClient.UploadDataAsync(new Uri(url), "POST", new byte[0]);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[])");

                        webClient.UploadDataAsync(new Uri(url), "POST", new byte[0], null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[], Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadDataTaskAsync"))
                    {
                        webClient.UploadDataTaskAsync(url, new byte[0]).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, Byte[])");

                        webClient.UploadDataTaskAsync(new Uri(url), new byte[0]).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(Uri, Byte[])");

                        webClient.UploadDataTaskAsync(url, "POST", new byte[0]).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, String, Byte[])");

                        webClient.UploadDataTaskAsync(new Uri(url), "POST", new byte[0]).GetAwaiter().GetResult();
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
                        webClient.UploadFileAsync(new Uri(url), "UploadFile.txt");
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String)");

                        webClient.UploadFileAsync(new Uri(url), "POST", "UploadFile.txt");
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String)");

                        webClient.UploadFileAsync(new Uri(url), "POST", "UploadFile.txt", null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadFileTaskAsync"))
                    {
                        webClient.UploadFileTaskAsync(url, "UploadFile.txt").GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String)");

                        webClient.UploadFileTaskAsync(new Uri(url), "UploadFile.txt").GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(Uri, String)");

                        webClient.UploadFileTaskAsync(url, "POST", "UploadFile.txt").GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String, String)");

                        webClient.UploadFileTaskAsync(new Uri(url), "POST", "UploadFile.txt").GetAwaiter().GetResult();
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
                        webClient.UploadStringAsync(new Uri(url), requestContent);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String)");

                        webClient.UploadStringAsync(new Uri(url), "POST", requestContent);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String)");

                        webClient.UploadStringAsync(new Uri(url), "POST", requestContent, null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadStringTaskAsync"))
                    {
                        webClient.UploadStringTaskAsync(url, requestContent).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String)");

                        webClient.UploadStringTaskAsync(new Uri(url), requestContent).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(Uri, String)");

                        webClient.UploadStringTaskAsync(url, "POST", requestContent).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String, String)");

                        webClient.UploadStringTaskAsync(new Uri(url), "POST", requestContent).GetAwaiter().GetResult();
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
                        webClient.UploadValuesAsync(new Uri(url), values);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, NameValueCollection)");

                        webClient.UploadValuesAsync(new Uri(url), "POST", values);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection)");

                        webClient.UploadValuesAsync(new Uri(url), "POST", values, null);
                        while (webClient.IsBusy) ;
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection, Object)");
                    }

                    using (Tracer.Instance.StartActive("UploadValuesTaskAsync"))
                    {
                        webClient.UploadValuesTaskAsync(url, values).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, NameValueCollection)");

                        webClient.UploadValuesTaskAsync(new Uri(url), values).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, NameValueCollection)");

                        webClient.UploadValuesTaskAsync(url, "POST", values).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, String, NameValueCollection)");

                        webClient.UploadValuesTaskAsync(new Uri(url), "POST", values).GetAwaiter().GetResult();
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, String, NameValueCollection)");
                    }
                }
            }
        }
    }
}
