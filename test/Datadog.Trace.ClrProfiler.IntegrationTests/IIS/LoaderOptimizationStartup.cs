#if NETFRAMEWORK
using System.Net;
using System.Net.Http;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS
{
    public class LoaderOptimizationStartup
    {
        private const string Url = "http://localhost:8080";

        public LoaderOptimizationStartup(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

        [Fact]
        public void ApplicationDoesNotReturnErrors()
        {
            var intervalMilliseconds = 500;
            var intervals = 5;
            var serverReady = false;
            var client = new HttpClient();

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                try
                {
                    var serverReadyResponse = client.GetAsync(Url).GetAwaiter().GetResult();
                    serverReady = serverReadyResponse.StatusCode == HttpStatusCode.OK;
                }
                catch
                {
                    // ignore
                }

                if (serverReady)
                {
                    Output.WriteLine("The server is ready.");
                    break;
                }

                Thread.Sleep(intervalMilliseconds);
            }

            // Server is ready to recieve requests
            var responseMessage = client.GetAsync(Url).GetAwaiter().GetResult();
            Assert.Equal(HttpStatusCode.OK, responseMessage.StatusCode);
        }
    }
}
#endif
