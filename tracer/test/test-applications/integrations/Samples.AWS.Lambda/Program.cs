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
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NO_PARAM_SYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_ONE_PARAM_SYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_TWO_PARAMS_SYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NO_PARAM_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_ONE_PARAM_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_TWO_PARAMS_ASYNC"));
        }
        private static async Task<string> Post(string url)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders
                  .Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("x-datadog-tracing-enabled", "false");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/2015-03-31/functions/function/invocations");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }

        private void Get(string url)
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

        public object HandlerNoParamSync()
        {
            Get("http://localhost/function/HandlerNoParamSync");
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerOneParamSync(CustomInput request)
        {
            Get("http://localhost/function/HandlerOneParamSync");
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerTwoParamsSync(CustomInput request, ILambdaContext context)
        {
            Get("http://localhost/function/HandlerTwoParamsSync");
            return new { statusCode = 200, body = "ok!" };
        }

        public async Task<int> HandlerNoParamAsync()
        {
            await Task.Run(() => {
                Get("http://localhost/function/HandlerNoParamAsync");
                Thread.Sleep(100);
            });
            return 10;
        }

        public async Task<int> HandlerOneParamAsync(CustomInput request)
        {
            await Task.Run(() => {
                Get("http://localhost/function/HandlerOneParamAsync");
                Thread.Sleep(100);
            });
            return 10;
        }

        public async Task<int> HandlerTwoParamsAsync(CustomInput request, ILambdaContext context)
        {
            await Task.Run(() => {
                Get("http://localhost/function/HandlerTwoParamsAsync");
                Thread.Sleep(100);
            });
            return 10;
        }
    }

    public class CustomInput
    {
        public string Field1 { get; set; }
        public int Field2 { get; set; }
    }
}
