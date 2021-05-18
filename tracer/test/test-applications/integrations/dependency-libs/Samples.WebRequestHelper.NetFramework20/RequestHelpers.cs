using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Samples.WebRequest
{
    public static class RequestHelpers
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        private static readonly AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        public static void SendWebClientRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebClient] sending requests to {url}");

            using (var webClient = new WebClient())
            {
                webClient.DownloadDataCompleted += WebClient_OnEventCompleted;
                webClient.DownloadFileCompleted += WebClient_OnEventCompleted;
                webClient.DownloadStringCompleted += WebClient_OnEventCompleted;
                webClient.OpenReadCompleted += WebClient_OnEventCompleted;
                webClient.UploadDataCompleted += WebClient_OnEventCompleted;
                webClient.UploadFileCompleted += WebClient_OnEventCompleted;
                webClient.UploadStringCompleted += WebClient_OnEventCompleted;
                webClient.UploadValuesCompleted += WebClient_OnEventCompleted;

                webClient.Encoding = Utf8;

                if (tracingDisabled)
                {
                    webClient.Headers.Add("x-datadog-tracing-enabled", "false");
                }

                // WebClient
                // WebClient.DownloadData
                webClient.DownloadData(url);
                Console.WriteLine("Received response for client.DownloadData(String)");

                webClient.DownloadData(new Uri(url));
                Console.WriteLine("Received response for client.DownloadData(Uri)");

                // WebClient.DownloadDataAsync
                
                webClient.DownloadDataAsync(new Uri(url));
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadDataAsync(Uri)");

                webClient.DownloadDataAsync(new Uri(url), null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadDataAsync(Uri, Object)");

                // WebClient.DownloadFile
                webClient.DownloadFile(url, "DownloadFile.string.txt");
                Console.WriteLine("Received response for client.DownloadFile(String, String)");

                webClient.DownloadFile(new Uri(url), "DownloadFile.uri.txt");
                Console.WriteLine("Received response for client.DownloadFile(Uri, String)");

                // WebClient.DownloadFileAsync
                webClient.DownloadFileAsync(new Uri(url), "DownloadFileAsync.uri.txt");
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String)");

                webClient.DownloadFileAsync(new Uri(url), "DownloadFileAsync.uri_token.txt", null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadFileAsync(Uri, String, Object)");

                // WebClient.DownloadString
                webClient.DownloadString(url);
                Console.WriteLine("Received response for client.DownloadString(String)");

                webClient.DownloadString(new Uri(url));
                Console.WriteLine("Received response for client.DownloadString(Uri)");

                // WebClient.DownloadStringAsync
                webClient.DownloadStringAsync(new Uri(url));
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadStringAsync(Uri)");

                webClient.DownloadStringAsync(new Uri(url), null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.DownloadStringAsync(Uri, Object)");

                // WebClient.OpenRead
                webClient.OpenRead(url).Close();
                Console.WriteLine("Received response for client.OpenRead(String)");

                webClient.OpenRead(new Uri(url)).Close();
                Console.WriteLine("Received response for client.OpenRead(Uri)");

                // WebClient.OpenReadAsync
                webClient.OpenReadAsync(new Uri(url));
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.OpenReadAsync(Uri)");

                webClient.OpenReadAsync(new Uri(url), null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.OpenReadAsync(Uri, Object)");

                // WebClient.UploadData
                webClient.UploadData(url, new byte[0]);
                Console.WriteLine("Received response for client.UploadData(String, Byte[])");

                webClient.UploadData(new Uri(url), new byte[0]);
                Console.WriteLine("Received response for client.UploadData(Uri, Byte[])");

                webClient.UploadData(url, "POST", new byte[0]);
                Console.WriteLine("Received response for client.UploadData(String, String, Byte[])");

                webClient.UploadData(new Uri(url), "POST", new byte[0]);
                Console.WriteLine("Received response for client.UploadData(Uri, String, Byte[])");

                // WebClient.UploadDataAsync
                webClient.UploadDataAsync(new Uri(url), new byte[0]);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadDataAsync(Uri, Byte[])");

                webClient.UploadDataAsync(new Uri(url), "POST", new byte[0]);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[])");

                webClient.UploadDataAsync(new Uri(url), "POST", new byte[0], null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadDataAsync(Uri, String, Byte[], Object)");

                File.WriteAllText("UploadFile.txt", requestContent);

                // WebClient.UploadFile
                webClient.UploadFile(url, "UploadFile.txt");
                Console.WriteLine("Received response for client.UploadFile(String, String)");

                webClient.UploadFile(new Uri(url), "UploadFile.txt");
                Console.WriteLine("Received response for client.UploadFile(Uri, String)");

                webClient.UploadFile(url, "POST", "UploadFile.txt");
                Console.WriteLine("Received response for client.UploadFile(String, String, String)");

                webClient.UploadFile(new Uri(url), "POST", "UploadFile.txt");
                Console.WriteLine("Received response for client.UploadFile(Uri, String, String)");

                // WebClient.UploadFileAsync
                webClient.UploadFileAsync(new Uri(url), "UploadFile.txt");
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadFileAsync(Uri, String)");

                webClient.UploadFileAsync(new Uri(url), "POST", "UploadFile.txt");
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String)");

                webClient.UploadFileAsync(new Uri(url), "POST", "UploadFile.txt", null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadFileAsync(Uri, String, String, Object)");

                // WebClient.UploadString
                webClient.UploadString(url, requestContent);
                Console.WriteLine("Received response for client.UploadString(String, String)");

                webClient.UploadString(new Uri(url), requestContent);
                Console.WriteLine("Received response for client.UploadString(Uri, String)");

                webClient.UploadString(url, "POST", requestContent);
                Console.WriteLine("Received response for client.UploadString(String, String, String)");

                webClient.UploadString(new Uri(url), "POST", requestContent);
                Console.WriteLine("Received response for client.UploadString(Uri, String, String)");

                // WebClient.UploadStringAsync
                webClient.UploadStringAsync(new Uri(url), requestContent);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadStringAsync(Uri, String)");

                webClient.UploadStringAsync(new Uri(url), "POST", requestContent);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String)");

                webClient.UploadStringAsync(new Uri(url), "POST", requestContent, null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadStringAsync(Uri, String, String, Object)");

                // WebClient.UploadValues
                var values = new NameValueCollection();
                webClient.UploadValues(url, values);
                Console.WriteLine("Received response for client.UploadValues(String, NameValueCollection)");

                webClient.UploadValues(new Uri(url), values);
                Console.WriteLine("Received response for client.UploadValues(Uri, NameValueCollection)");

                webClient.UploadValues(url, "POST", values);
                Console.WriteLine("Received response for client.UploadValues(String, String, NameValueCollection)");

                webClient.UploadValues(new Uri(url), "POST", values);
                Console.WriteLine("Received response for client.UploadValues(Uri, String, NameValueCollection)");

                // WebClient.UploadValuesAsync
                webClient.UploadValuesAsync(new Uri(url), values);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadValuesAsync(Uri, NameValueCollection)");

                webClient.UploadValuesAsync(new Uri(url), "POST", values);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection)");

                webClient.UploadValuesAsync(new Uri(url), "POST", values, null);
                autoResetEvent.WaitOne();
                Console.WriteLine("Received response for client.UploadValuesAsync(Uri, String, NameValueCollection, Object)");
            }
        }

        private static void WebClient_OnEventCompleted(object sender, object eventArgs)
        {
            autoResetEvent.Set();
        }

        public static void SendWebRequestRequests(bool tracingDisabled, string url, string requestContent)
        {
            Console.WriteLine($"[WebRequest] sending requests to {url}");

            // WebRequest
            // WebRequest.GetResponse
            HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(url);
            if (tracingDisabled)
            {
                request.Headers.Add("x-datadog-tracing-enabled", "false");
            }

            request.GetResponse().Close();
            Console.WriteLine("Received response for request.GetResponse()");
        }
    }
}
