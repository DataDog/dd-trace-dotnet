using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using System.Xml;

namespace Samples.ConsoleDeadLock
{
    /// <summary>
    /// A config builder that makes outbound HTTP calls on a background thread during
    /// AppSettings initialization, reproducing the pattern used by Azure App Configuration
    /// and Azure Identity (DefaultAzureCredential).
    ///
    /// The Azure SDK internally uses Task.Run for credential acquisition (e.g., AzureCliCredential
    /// shells out to a process, ManagedIdentityCredential hits the IMDS endpoint). This means the
    /// first instrumented HTTP call originates on a ThreadPool thread, not the main thread.
    ///
    /// This is critical for the deadlock: if the instrumented call were on the main thread, the
    /// CLR would allow .cctor re-entry (same thread) and the call would fail with NullRef instead
    /// of deadlocking. But on a different thread, the CLR blocks waiting for the .cctor to complete
    /// on the main thread → deadlock.
    /// </summary>
    public class HttpConfigBuilder : ConfigurationBuilder
    {
        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            var url = Environment.GetEnvironmentVariable("CONFIGBUILDER_HTTP_URL")
                   ?? "http://127.0.0.1:9999/";

            Console.WriteLine($"[ConfigBuilder] ProcessRawXml - making HTTP call on background thread to {url}...");

            // Schedule the HTTP call on a ThreadPool thread, then block waiting for it.
            // This matches the Azure SDK pattern where DefaultAzureCredential.GetTokenAsync()
            // is called with .GetAwaiter().GetResult(), and internally the credential providers
            // use Task.Run for async operations.
            //
            // The WebRequest.GetResponse() call on the ThreadPool thread is instrumented by
            // CallTarget. The instrumentation tries to access Tracer.Instance →
            // GlobalConfigurationSource → blocked on .cctor (held by main thread) → DEADLOCK.
            var task = Task.Run(() =>
            {
                Console.WriteLine($"[ConfigBuilder] Background thread {Environment.CurrentManagedThreadId} started, about to call WebRequest.Create");
                try
                {
                    var request = WebRequest.Create(url);
                    Console.WriteLine($"[ConfigBuilder] WebRequest created on thread {Environment.CurrentManagedThreadId}, calling GetResponse...");
                    request.Timeout = 5000;
                    using (var response = request.GetResponse())
                    {
                        Console.WriteLine($"[ConfigBuilder] HTTP response on thread {Environment.CurrentManagedThreadId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ConfigBuilder] HTTP failed on thread {Environment.CurrentManagedThreadId}: {ex.GetType().Name}");
                }

                Console.WriteLine($"[ConfigBuilder] Background thread {Environment.CurrentManagedThreadId} completed");
            });

            // Block the main thread waiting for the background HTTP call.
            // Main thread holds GlobalConfigurationSource .cctor lock.
            // Background thread needs .cctor lock → DEADLOCK (without the fix).
            task.Wait();

            Console.WriteLine("[ConfigBuilder] ProcessRawXml completed.");
            return rawXml;
        }
    }
}
