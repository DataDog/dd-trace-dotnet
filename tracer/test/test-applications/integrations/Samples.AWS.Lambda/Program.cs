using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Samples.AWS.Lambda
{
    public class Function
    {
        private static async Task Main(string[] args)
        {
            string functionEndpoint = Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT");
            await POSTData(functionEndpoint);
        }
        private static async Task<string> POSTData(string url)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // client.DefaultRequestHeaders.Add("x-datadog-tracing-enabled", "false");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/2015-03-31/functions/function/invocations");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        private void doGet(String url)
        {
            WebRequest request = WebRequest.Create(url);
            request.Credentials = CredentialCache.DefaultCredentials;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                reader.ReadToEnd();
            }
            response.Close();
        }
        public object Handler(CustomInput request, ILambdaContext context)
        {
            doGet("https://datadoghq.com");
            return new { statusCode = 200, body = "ok!" };
        }
    }

    public class CustomInput
    {
        public string Field1 { get; set; }
        public int Field2 { get; set; }
    }
}
