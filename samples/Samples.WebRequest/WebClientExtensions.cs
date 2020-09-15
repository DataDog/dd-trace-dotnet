using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading;

namespace Samples.WebRequest
{
    internal static class WebClientExtensions
    {
        public static void DownloadDataAsyncAndWait(this WebClient webClient, Uri uri)
        {
            DownloadDataAsyncAndWait(webClient, () => webClient.DownloadDataAsync(uri));
        }

        public static void DownloadDataAsyncAndWait(this WebClient webClient, Uri uri, object state)
        {
            DownloadDataAsyncAndWait(webClient, () => webClient.DownloadDataAsync(uri, state));
        }

        public static void DownloadFileAsyncAndWait(this WebClient webClient, Uri uri, string file)
        {
            DownloadFileAsyncAndWait(webClient, () => webClient.DownloadFileAsync(uri, file));
        }

        public static void DownloadFileAsyncAndWait(this WebClient webClient, Uri uri, string file, object state)
        {
            DownloadFileAsyncAndWait(webClient, () => webClient.DownloadFileAsync(uri, file, state));
        }

        public static void DownloadStringAsyncAndWait(this WebClient webClient, Uri uri)
        {
            DownloadStringAsyncAndWait(webClient, () => webClient.DownloadStringAsync(uri));
        }

        public static void DownloadStringAsyncAndWait(this WebClient webClient, Uri uri, object state)
        {
            DownloadStringAsyncAndWait(webClient, () => webClient.DownloadStringAsync(uri, state));
        }

        public static void OpenReadAsyncAndWait(this WebClient webClient, Uri uri)
        {
            OpenReadAsyncAndWait(webClient, () => webClient.OpenReadAsync(uri));
        }

        public static void OpenReadAsyncAndWait(this WebClient webClient, Uri uri, object state)
        {
            OpenReadAsyncAndWait(webClient, () => webClient.OpenReadAsync(uri, state));
        }

        public static void UploadDataAsyncAndWait(this WebClient webClient, Uri uri, byte[] data)
        {
            UploadDataAsyncAndWait(webClient, () => webClient.UploadDataAsync(uri, data));
        }

        public static void UploadDataAsyncAndWait(this WebClient webClient, Uri uri, string method, byte[] data)
        {
            UploadDataAsyncAndWait(webClient, () => webClient.UploadDataAsync(uri, method, data));
        }

        public static void UploadDataAsyncAndWait(this WebClient webClient, Uri uri, string method, byte[] data, object state)
        {
            UploadDataAsyncAndWait(webClient, () => webClient.UploadDataAsync(uri, method, data, state));
        }

        public static void UploadFileAsyncAndWait(this WebClient webClient, Uri uri, string file)
        {
            UploadFileAsyncAndWait(webClient, () => webClient.UploadFileAsync(uri, file));
        }

        public static void UploadFileAsyncAndWait(this WebClient webClient, Uri uri, string file, string method)
        {
            UploadFileAsyncAndWait(webClient, () => webClient.UploadFileAsync(uri, file, method));
        }

        public static void UploadFileAsyncAndWait(this WebClient webClient, Uri uri, string file, string method, object state)
        {
            UploadFileAsyncAndWait(webClient, () => webClient.UploadFileAsync(uri, file, method, state));
        }

        public static void UploadStringAsyncAndWait(this WebClient webClient, Uri uri, string data)
        {
            UploadStringAsyncAndWait(webClient, () => webClient.UploadStringAsync(uri, data));
        }

        public static void UploadStringAsyncAndWait(this WebClient webClient, Uri uri, string method, string data)
        {
            UploadStringAsyncAndWait(webClient, () => webClient.UploadStringAsync(uri, method, data));
        }

        public static void UploadStringAsyncAndWait(this WebClient webClient, Uri uri, string method, string data, object state)
        {
            UploadStringAsyncAndWait(webClient, () => webClient.UploadStringAsync(uri, method, data, state));
        }

        public static void UploadValuesAsyncAndWait(this WebClient webClient, Uri uri, NameValueCollection data)
        {
            UploadValuesAsyncAndWait(webClient, () => webClient.UploadValuesAsync(uri, data));
        }

        public static void UploadValuesAsyncAndWait(this WebClient webClient, Uri uri, string method, NameValueCollection data)
        {
            UploadValuesAsyncAndWait(webClient, () => webClient.UploadValuesAsync(uri, method, data));
        }

        public static void UploadValuesAsyncAndWait(this WebClient webClient, Uri uri, string method, NameValueCollection data, object state)
        {
            UploadValuesAsyncAndWait(webClient, () => webClient.UploadValuesAsync(uri, method, data, state));
        }

        private static void DownloadDataAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, DownloadDataCompletedEventArgs e)
            {
                webClient.DownloadDataCompleted -= Handler;
                mutex.Set();
            }

            webClient.DownloadDataCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void DownloadFileAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, EventArgs e)
            {
                webClient.DownloadFileCompleted -= Handler;
                mutex.Set();
            }

            webClient.DownloadFileCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void DownloadStringAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, DownloadStringCompletedEventArgs e)
            {
                webClient.DownloadStringCompleted -= Handler;
                mutex.Set();
            }

            webClient.DownloadStringCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void OpenReadAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, OpenReadCompletedEventArgs e)
            {
                webClient.OpenReadCompleted -= Handler;
                mutex.Set();
            }

            webClient.OpenReadCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void UploadDataAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, UploadDataCompletedEventArgs e)
            {
                webClient.UploadDataCompleted -= Handler;
                mutex.Set();
            }

            webClient.UploadDataCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void UploadFileAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, UploadFileCompletedEventArgs e)
            {
                webClient.UploadFileCompleted -= Handler;
                mutex.Set();
            }

            webClient.UploadFileCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void UploadStringAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, UploadStringCompletedEventArgs e)
            {
                webClient.UploadStringCompleted -= Handler;
                mutex.Set();
            }

            webClient.UploadStringCompleted += Handler;

            operation();

            mutex.Wait();
        }

        private static void UploadValuesAsyncAndWait(WebClient webClient, Action operation)
        {
            var mutex = new ManualResetEventSlim();

            void Handler(object s, UploadValuesCompletedEventArgs e)
            {
                webClient.UploadValuesCompleted -= Handler;
                mutex.Set();
            }

            webClient.UploadValuesCompleted += Handler;

            operation();

            mutex.Wait();
        }
    }
}
