using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace Datadog.Trace.FileProxy
{
    /// <summary>
    /// Proxy job for reading traces in Azure App Services
    /// </summary>
    public class Program
    {
        private static readonly string LogFile = Path.Combine(Environment.CurrentDirectory, "trace_proxy_log.txt");

        private static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    try
                    {
                        var client = new HttpClient();
                        var uri = new Uri("http://localhost:8126/v0.4/traces");
                        var response = client.GetAsync(uri);
                        response.Wait();
                        var readTask = response.Result.Content.ReadAsStringAsync();
                        readTask.Wait();
                        var content = readTask.Result;
                        Append($"[{DateTime.Now}] - {content}");
                    }
                    catch (Exception ex)
                    {
                        Append(ex.ToString());
                    }
                }
                finally
                {
                    Thread.Sleep(10_000);
                }
            }
        }

        private static void Append(string text)
        {
            if (!File.Exists(LogFile))
            {
                File.Create(LogFile);
            }

            File.AppendAllText(LogFile, text + Environment.NewLine);
        }
    }
}
