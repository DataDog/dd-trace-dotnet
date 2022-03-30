using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.WebRequest
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;
        private static readonly AutoResetEvent _allDone = new(false);
        private const string TracingEnabled = "x-datadog-tracing-enabled";

        public static async Task SendWebClientRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebClient] sending requests to {url}");

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Utf8;

                if (tracingDisabled)
                {
                    webClient.Headers.Add(TracingEnabled, "false");
                }

                using (SampleHelpers.CreateScope("WebClient"))
                {
                    using (SampleHelpers.CreateScope("DownloadData"))
                    {
                        webClient.DownloadData(GetUrlForTest("DownloadData", url));
                        Console.WriteLine("Received response for client.DownloadData(String)");

                        webClient.DownloadData(new Uri(GetUrlForTest("DownloadData2", url)));
                        Console.WriteLine("Received response for client.DownloadData(Uri)");
                    }

                    using (SampleHelpers.CreateScope("DownloadDataAsync"))
                    {
                        webClient.DownloadDataAsyncAndWait(new Uri(GetUrlForTest("DownloadDataAsync", url)));
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri)");

                        webClient.DownloadDataAsyncAndWait(new Uri(GetUrlForTest("DownloadDataAsync2", url)), null);
                        Console.WriteLine("Received response for client.DownloadDataAsync(Uri, Object)");
                    }

                    using (SampleHelpers.CreateScope("DownloadDataTaskAsync"))
                    {
                        await webClient.DownloadDataTaskAsync(GetUrlForTest("DownloadDataTaskAsync", url));
                        Console.WriteLine("Received response for client.DownloadDataTaskAsync(String)");

                        await webClient.DownloadDataTaskAsync(new Uri(GetUrlForTest("DownloadDataTaskAsync2", url)));
                        Console.WriteLine("Received response for client.DownloadDataTaskAsync(Uri)");
                    }

                    using (SampleHelpers.CreateScope("DownloadFile"))
                    {
                        webClient.DownloadFile(GetUrlForTest("DownloadFile", url), "DownloadFile.string.txt");
                        Console.WriteLine("Received response for client.DownloadFile(String, String)");

                        webClient.DownloadFile(new Uri(GetUrlForTest("DownloadFile2", url)), "DownloadFile.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFile(Uri, String)");
                    }

                    using (SampleHelpers.CreateScope("DownloadFileAsync"))
                    {
                        webClient.DownloadFileAsyncAndWait(new Uri(GetUrlForTest("DownloadFileAsync", url)), "DownloadFileAsync.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String)");

                        webClient.DownloadFileAsyncAndWait(new Uri(GetUrlForTest("DownloadFileAsync2", url)), "DownloadFileAsync.uri_token.txt", null);
                        Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String, Object)");
                    }

                    using (SampleHelpers.CreateScope("DownloadFileTaskAsync"))
                    {
                        await webClient.DownloadFileTaskAsync(GetUrlForTest("DownloadFileTaskAsync", url), "DownloadFileTaskAsync.string.txt");
                        Console.WriteLine("Received response for client.DownloadFileTaskAsync(String, String)");

                        await webClient.DownloadFileTaskAsync(new Uri(GetUrlForTest("DownloadFileTaskAsync2", url)), "DownloadFileTaskAsync.uri.txt");
                        Console.WriteLine("Received response for client.DownloadFileTaskAsync(Uri, String)");
                    }

                    using (SampleHelpers.CreateScope("DownloadString"))
                    {
                        webClient.DownloadString(GetUrlForTest("DownloadString", url));
                        Console.WriteLine("Received response for client.DownloadString(String)");

                        webClient.DownloadString(new Uri(GetUrlForTest("DownloadString2", url)));
                        Console.WriteLine("Received response for client.DownloadString(Uri)");
                    }

                    using (SampleHelpers.CreateScope("DownloadStringAsync"))
                    {
                        webClient.DownloadStringAsyncAndWait(new Uri(GetUrlForTest("DownloadStringAsync", url)));
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri)");

                        webClient.DownloadStringAsyncAndWait(new Uri(GetUrlForTest("DownloadStringAsync2", url)), null);
                        Console.WriteLine("Received response for client.DownloadStringAsync(Uri, Object)");
                    }

                    using (SampleHelpers.CreateScope("DownloadStringTaskAsync"))
                    {
                        await webClient.DownloadStringTaskAsync(GetUrlForTest("DownloadStringTaskAsync", url));
                        Console.WriteLine("Received response for client.DownloadStringTaskAsync(String)");

                        await webClient.DownloadStringTaskAsync(new Uri(GetUrlForTest("DownloadStringTaskAsync2", url)));
                        Console.WriteLine("Received response for client.DownloadStringTaskAsync(Uri)");
                    }

                    using (SampleHelpers.CreateScope("OpenRead"))
                    {
                        webClient.OpenRead(GetUrlForTest("OpenRead", url)).Close();
                        Console.WriteLine("Received response for client.OpenRead(String)");

                        webClient.OpenRead(new Uri(GetUrlForTest("OpenRead2", url))).Close();
                        Console.WriteLine("Received response for client.OpenRead(Uri)");
                    }

                    using (SampleHelpers.CreateScope("OpenReadAsync"))
                    {
                        webClient.OpenReadAsyncAndWait(new Uri(GetUrlForTest("OpenReadAsync", url)));
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri)");

                        webClient.OpenReadAsyncAndWait(new Uri(GetUrlForTest("OpenReadAsync2", url)), null);
                        Console.WriteLine("Received response for client.OpenReadAsync(Uri, Object)");
                    }

                    using (SampleHelpers.CreateScope("OpenReadTaskAsync"))
                    {
                        using Stream readStream1 = await webClient.OpenReadTaskAsync(GetUrlForTest("OpenReadTaskAsync", url));
                        Console.WriteLine("Received response for client.OpenReadTaskAsync(String)");

                        using Stream readStream2 = await webClient.OpenReadTaskAsync(new Uri(GetUrlForTest("OpenReadTaskAsync2", url)));
                        Console.WriteLine("Received response for client.OpenReadTaskAsync(Uri)");
                    }

                    using (SampleHelpers.CreateScope("UploadData"))
                    {
                        webClient.UploadData(GetUrlForTest("UploadData", url), new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(String, Byte[])");

                        webClient.UploadData(new Uri(GetUrlForTest("UploadData2", url)), new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(Uri, Byte[])");

                        webClient.UploadData(GetUrlForTest("UploadData3", url), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(String, String, Byte[])");

                        webClient.UploadData(new Uri(GetUrlForTest("UploadData4", url)), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadData(Uri, String, Byte[])");
                    }

                    using (SampleHelpers.CreateScope("UploadDataAsync"))
                    {
                        webClient.UploadDataAsyncAndWait(new Uri(GetUrlForTest("UploadDataAsync", url)), new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, Byte[])");

                        webClient.UploadDataAsyncAndWait(new Uri(GetUrlForTest("UploadDataAsync2", url)), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[])");

                        webClient.UploadDataAsyncAndWait(new Uri(GetUrlForTest("UploadDataAsync3", url)), "POST", new byte[0], null);
                        Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[], Object)");
                    }

                    using (SampleHelpers.CreateScope("UploadDataTaskAsync"))
                    {
                        await webClient.UploadDataTaskAsync(GetUrlForTest("UploadDataTaskAsync", url), new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, Byte[])");

                        await webClient.UploadDataTaskAsync(new Uri(GetUrlForTest("UploadDataTaskAsync2", url)), new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(Uri, Byte[])");

                        await webClient.UploadDataTaskAsync(GetUrlForTest("UploadDataTaskAsync3", url), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(String, String, Byte[])");

                        await webClient.UploadDataTaskAsync(new Uri(GetUrlForTest("UploadDataTaskAsync4", url)), "POST", new byte[0]);
                        Console.WriteLine("Received response for client.UploadDataTaskAsync(Uri, String, Byte[])");
                    }

                    File.WriteAllText("UploadFile.txt", requestContent);

                    using (SampleHelpers.CreateScope("UploadFile"))
                    {
                        webClient.UploadFile(GetUrlForTest("UploadFile", url), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(String, String)");

                        webClient.UploadFile(new Uri(GetUrlForTest("UploadFile2", url)), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(Uri, String)");

                        webClient.UploadFile(GetUrlForTest("UploadFile3", url), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(String, String, String)");

                        webClient.UploadFile(new Uri(GetUrlForTest("UploadFile4", url)), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFile(Uri, String, String)");
                    }

                    using (SampleHelpers.CreateScope("UploadFileAsync"))
                    {
                        webClient.UploadFileAsyncAndWait(new Uri(GetUrlForTest("UploadFileAsync", url)), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String)");

                        webClient.UploadFileAsyncAndWait(new Uri(GetUrlForTest("UploadFileAsync2", url)), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String)");

                        webClient.UploadFileAsyncAndWait(new Uri(GetUrlForTest("UploadFileAsync3", url)), "POST", "UploadFile.txt", null);
                        Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String, Object)");
                    }

                    using (SampleHelpers.CreateScope("UploadFileTaskAsync"))
                    {
                        await webClient.UploadFileTaskAsync(GetUrlForTest("UploadFileTaskAsync", url), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String)");

                        await webClient.UploadFileTaskAsync(new Uri(GetUrlForTest("UploadFileTaskAsync2", url)), "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(Uri, String)");

                        await webClient.UploadFileTaskAsync(GetUrlForTest("UploadFileTaskAsync3", url), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(String, String, String)");

                        await webClient.UploadFileTaskAsync(new Uri(GetUrlForTest("UploadFileTaskAsync4", url)), "POST", "UploadFile.txt");
                        Console.WriteLine("Received response for client.UploadFileTaskAsync(Uri, String, String)");
                    }

                    using (SampleHelpers.CreateScope("UploadString"))
                    {
                        webClient.UploadString(GetUrlForTest("UploadString", url), requestContent);
                        Console.WriteLine("Received response for client.UploadString(String, String)");

                        webClient.UploadString(new Uri(GetUrlForTest("UploadString2", url)), requestContent);
                        Console.WriteLine("Received response for client.UploadString(Uri, String)");

                        webClient.UploadString(GetUrlForTest("UploadString3", url), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadString(String, String, String)");

                        webClient.UploadString(new Uri(GetUrlForTest("UploadString4", url)), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadString(Uri, String, String)");
                    }

                    using (SampleHelpers.CreateScope("UploadStringAsync"))
                    {
                        webClient.UploadStringAsyncAndWait(new Uri(GetUrlForTest("UploadStringAsync", url)), requestContent);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String)");

                        webClient.UploadStringAsyncAndWait(new Uri(GetUrlForTest("UploadStringAsync2", url)), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String)");

                        webClient.UploadStringAsyncAndWait(new Uri(GetUrlForTest("UploadStringAsync3", url)), "POST", requestContent, null);
                        Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String, Object)");
                    }

                    using (SampleHelpers.CreateScope("UploadStringTaskAsync"))
                    {
                        await webClient.UploadStringTaskAsync(GetUrlForTest("UploadStringTaskAsync", url), requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String)");

                        await webClient.UploadStringTaskAsync(new Uri(GetUrlForTest("UploadStringTaskAsync2", url)), requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(Uri, String)");

                        await webClient.UploadStringTaskAsync(GetUrlForTest("UploadStringTaskAsync3", url), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(String, String, String)");

                        await webClient.UploadStringTaskAsync(new Uri(GetUrlForTest("UploadStringTaskAsync4", url)), "POST", requestContent);
                        Console.WriteLine("Received response for client.UploadStringTaskAsync(Uri, String, String)");
                    }

                    var values = new NameValueCollection();
                    using (SampleHelpers.CreateScope("UploadValues"))
                    {
                        webClient.UploadValues(GetUrlForTest("UploadValues", url), values);
                        Console.WriteLine("Received response for client.UploadValues(String, NameValueCollection)");

                        webClient.UploadValues(new Uri(GetUrlForTest("UploadValues2", url)), values);
                        Console.WriteLine("Received response for client.UploadValues(Uri, NameValueCollection)");

                        webClient.UploadValues(GetUrlForTest("UploadValues3", url), "POST", values);
                        Console.WriteLine("Received response for client.UploadValues(String, String, NameValueCollection)");

                        webClient.UploadValues(new Uri(GetUrlForTest("UploadValues4", url)), "POST", values);
                        Console.WriteLine("Received response for client.UploadValues(Uri, String, NameValueCollection)");
                    }

                    using (SampleHelpers.CreateScope("UploadValuesAsync"))
                    {
                        webClient.UploadValuesAsyncAndWait(new Uri(GetUrlForTest("UploadValuesAsync", url)), values);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, NameValueCollection)");

                        webClient.UploadValuesAsyncAndWait(new Uri(GetUrlForTest("UploadValuesAsync2", url)), "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection)");

                        webClient.UploadValuesAsyncAndWait(new Uri(GetUrlForTest("UploadValuesAsync3", url)), "POST", values, null);
                        Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection, Object)");
                    }

                    using (SampleHelpers.CreateScope("UploadValuesTaskAsync"))
                    {
                        await webClient.UploadValuesTaskAsync(GetUrlForTest("UploadValuesTaskAsync", url), values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(new Uri(GetUrlForTest("UploadValuesTaskAsync2", url)), values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(GetUrlForTest("UploadValuesTaskAsync3", url), "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(String, String, NameValueCollection)");

                        await webClient.UploadValuesTaskAsync(new Uri(GetUrlForTest("UploadValuesTaskAsync4", url)), "POST", values);
                        Console.WriteLine("Received response for client.UploadValuesTaskAsync(Uri, String, NameValueCollection)");
                    }
                }
            }
        }

        public static async Task SendWebRequestRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebRequest] sending requests to {url}");

            using (SampleHelpers.CreateScope("WebRequest"))
            {
                using (SampleHelpers.CreateScope("GetResponse"))
                {
                    // Create separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("GetResponse", url));
                    if (tracingDisabled)
                    {
                        request.Headers.Add(TracingEnabled, "false");
                    }

                    request.GetResponse().Close();
                    Console.WriteLine("Received response for request.GetResponse()");
                }

                using (SampleHelpers.CreateScope("GetResponseAsync"))
                {
                    // Create separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("GetResponseAsync", url));
                    if (tracingDisabled)
                    {
                        request.Headers.Add(TracingEnabled, "false");
                    }

                    (await request.GetResponseAsync()).Close();
                    Console.WriteLine("Received response for request.GetResponseAsync()");
                }

                using (SampleHelpers.CreateScope("GetRequestStream"))
                {
                    GetRequestStream(tracingDisabled, url);
                }

                using (SampleHelpers.CreateScope("BeginGetRequestStream"))
                {
                    BeginGetRequestStream(tracingDisabled, url);
                }

                using (SampleHelpers.CreateScope("BeginGetResponse"))
                {
                    BeginGetResponse(tracingDisabled, url);
                }

                using (SampleHelpers.CreateScope("BeginGetResponse TaskFactoryFromAsync"))
                {
                    // Create separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("TaskFactoryFromAsync", url));
                    if (tracingDisabled)
                    {
                        request.Headers.Add(TracingEnabled, "false");
                    }

                    await Task.Factory.FromAsync(
                        beginMethod: request.BeginGetResponse,
                        endMethod: request.EndGetResponse,
                        state: request);
                }
            }

            // Try invoking those methods without a parent trace, to detect some sampling priority issues
            GetRequestStream(tracingDisabled, url);
            BeginGetRequestStream(tracingDisabled, url);

        }

        private static void BeginGetResponse(bool tracingDisabled, string url)
        {
            // Create separate request objects since .NET Core asserts only one response per request
            HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("BeginGetResponseAsync", url));
            request.Method = "POST";
            request.ContentLength = 1;
            request.AllowWriteStreamBuffering = false;

            if (tracingDisabled)
            {
                request.Headers.Add(TracingEnabled, "false");
            }

            var stream = request.GetRequestStream();
            stream.Write(new byte[1], 0, 1);

            request.BeginGetResponse(
                iar =>
                {
                    var req = (HttpWebRequest)iar.AsyncState;
                    var response = req.EndGetResponse(iar);

                    response.Close();

                    Console.WriteLine("Received response for request.Begin/EndGetResponse()");
                    _allDone.Set();
                },
                request);

            _allDone.WaitOne();
        }

        private static void BeginGetRequestStream(bool tracingDisabled, string url)
        {
            // Create separate request objects since .NET Core asserts only one response per request
            HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("BeginGetRequestStream", url));
            request.Method = "POST";
            request.ContentLength = 1;
            request.AllowWriteStreamBuffering = false;

            if (tracingDisabled)
            {
                request.Headers.Add(TracingEnabled, "false");
            }

            request.BeginGetRequestStream(
                iar =>
                {
                    var req = (HttpWebRequest)iar.AsyncState;
                    var stream = req.EndGetRequestStream(iar);
                    stream.Write(new byte[1], 0, 1);

                    request.GetResponse()
                           .Close();

                    Console.WriteLine("Received response for request.Begin/EndGetRequestStream()/GetResponse()");
                    _allDone.Set();
                },
                request);

            _allDone.WaitOne();
        }

        private static void GetRequestStream(bool tracingDisabled, string url)
        {
            // Create separate request objects since .NET Core asserts only one response per request
            HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(GetUrlForTest("GetRequestStream", url));
            request.Method = "POST";
            request.ContentLength = 1;
            request.AllowWriteStreamBuffering = false;

            if (tracingDisabled)
            {
                request.Headers.Add(TracingEnabled, "false");
            }

            var stream = request.GetRequestStream();
            stream.Write(new byte[1], 0, 1);

            request.GetResponse().Close();
            Console.WriteLine("Received response for request.GetRequestStream()/GetResponse()");
        }

        private static string GetUrlForTest(string testName, string baseUrl)
        {
            return baseUrl + "?" + testName;
        }
    }
}
