using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Samples.AWS.Lambda
{
    public class Function : BaseFunction
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine($"Is Tracer Attached: {SampleHelpers.IsProfilerAttached()}");
            Console.WriteLine($"Tracer location: {SampleHelpers.GetTracerAssemblyLocation()}");

            // Arbitrary delay to give time for us to JIT the methods correctly to avoid flakiness
            // Re-jit is necessarily async, so there is an edge case where we call the method before
            // we've instrumented it, which can lead to flakiness. This _may_ be an issue in general for
            // serverless in general (due to short-lived apps), but so far we haven't found a workaround
            await Task.Delay(5000); 

            // See documentation at docs/development/Serverless.md
            // These methods are run by the sample app, from the Integration test container
            // Each invocations retrieves the location of a specific lambda instance from the env var
            // And makes a POST request (this request is not traced) to the lambda instance running
            // in the corresponding container. Each of the lambda instances are configured to use
            // ONE of the handler methods described in the #handler region below.

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
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NO_PARAM_VOID"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_ONE_PARAM_VOID"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_TWO_PARAMS_VOID"));

            // with context
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NO_PARAM_SYNC_WITH_CONTEXT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_ONE_PARAM_SYNC_WITH_CONTEXT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_TWO_PARAMS_SYNC_WITH_CONTEXT"));

            // base functions
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_BASE_NO_PARAM_SYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_BASE_TWO_PARAMS_SYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_BASE_ONE_PARAM_SYNC_WITH_CONTEXT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_BASE_ONE_PARAM_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_BASE_TWO_PARAMS_VOID"));

            // parameter types
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_STRUCT_PARAM"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NESTED_CLASS_PARAM"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NESTED_STRUCT_PARAM"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERIC_DICT_PARAM"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_NESTED_GENERIC_DICT_PARAM"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_DOUBLY_NESTED_GENERIC_DICT_PARAM"));

            // Throwing handlers
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_TASK"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING_WITH_CONTEXT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_WITH_CONTEXT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_THROWING_ASYNC_TASK_WITH_CONTEXT"));

            // Generic types
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_VIRTUAL"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_VIRTUAL_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_ABSTRACT"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_ABSTRACT_ASYNC"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_COMPLEX"));
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_GENERICBASE_COMPLEX_NESTED"));

            // Toplevel Statements
            Thread.Sleep(1000);
            await Post(Environment.GetEnvironmentVariable("AWS_LAMBDA_ENDPOINT_TOPLEVEL_STATEMENT"));
            
            static async Task Post(string url)
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
                await response.Content.ReadAsStringAsync();
            }
        }

        // These are the handler methods which run in the lambda containers
        // and are invoked by the lambda runtime in response to a request.
        // Each handler creates a manual scope, makes a request to a "dummy"
        // API (which is traced), and returns.
        // If the handler is created "with context" then the spans created inside
        // the HandleRequest method are children of a parent (distributed) context,
        // otherwise they will be root spans
#region Handler Methods
        public object HandlerNoParamSync()
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerOneParamSync(CustomInput request)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerTwoParamsSync(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerNoParamSyncWithContext()
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerOneParamSyncWithContext(CustomInput request)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object HandlerTwoParamsSyncWithContext(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public async Task<int> HandlerNoParamAsync()
        {
            await Task.Run(() => {
                HandleRequest();
                Thread.Sleep(100);
            });
            return 10;
        }

        public async Task<int> HandlerOneParamAsync(CustomInput request)
        {
            await Task.Run(() => {
                HandleRequest();
                Thread.Sleep(100);
            });
            return 10;
        }

        public async Task<int> HandlerTwoParamsAsync(CustomInput request, ILambdaContext context)
        {
            await Task.Run(() => {
                HandleRequest();
                Thread.Sleep(100);
            });
            return 10;
        }

        public void HandlerNoParamVoid()
        {
            HandleRequest();
        }

        public void HandlerOneParamVoid(CustomInput request)
        {
            HandleRequest();
        }

        public void HandlerTwoParamsVoid(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void HandlerStructParam(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void HandlerNestedClassParam(NestedClass request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void HandlerNestedStructParam(NestedStruct request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void HandlerGenericDictionaryParam(Dictionary<string, string> request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void HandlerNestedGenericDictionaryParam(NestedGeneric<string, string> request, ILambdaContext context)
        {
            HandleRequest();
        }
        
        public void HandlerDoublyNestedGenericDictionaryParam(NestedClass.InnerGeneric<string, NestedClass.InnerGeneric<string, Dictionary<string,string>>> request)
        {
            HandleRequest();
        }
        #endregion

        public class NestedClass
        {
            public string Field1 { get; set; }
            public int Field2 { get; set; }
            
            public class InnerGeneric<TKey, TValue> : Dictionary<TKey, TValue>
            {
            }
        }

        public struct NestedStruct
        {
            public string Field1 { get; set; }
            public int Field2 { get; set; }
        }

        public class NestedGeneric<TKey, TValue> : Dictionary<TKey, TValue>
        {
        }
    }

    public class DerivedImplementation : GenericBase<CustomInput, CustomStruct>
    {
        public override CustomStruct VirtualGenericBaseType3(CustomInput request, ILambdaContext context)
        {
            HandleRequest(request);
            return default;
        }

        public override async Task<CustomStruct> VirtualGenericBaseType4(CustomInput request, ILambdaContext context)
        {
            HandleRequest(request);
            await Task.Delay(100);
            return default;
        }

        public override CustomStruct AbstractGenericBaseType5(CustomInput request, ILambdaContext context)
        {
            HandleRequest(request);
            return default;
        }

        public override async Task<CustomStruct> AbstractGenericBaseType6(CustomInput request, ILambdaContext context)
        {
            HandleRequest(request);
            await Task.Delay(100);
            return default;
        }

        protected override CustomStruct HandleRequest(CustomInput request, [CallerMemberName] string memberName = null)
        {
            HandleRequest(memberName);
            return default;
        }

        public class NestedDerived : GenericBase<CustomInput, CustomStruct>
        {
            public override CustomStruct AbstractGenericBaseType5(CustomInput request, ILambdaContext context)
            {
                HandleRequest(request);
                return default;
            }

            public override async Task<CustomStruct> AbstractGenericBaseType6(CustomInput request, ILambdaContext context)
            {
                HandleRequest(request);
                await Task.Delay(100);
                return default;
            }

            protected override CustomStruct HandleRequest(CustomInput request, [CallerMemberName] string memberName = null)
            {
                HandleRequest(memberName);
                return default;
            }
        }
    }

    public class CustomInput
    {
        public string Field1 { get; set; }
        public int Field2 { get; set; }
    }

    public struct CustomStruct
    {
        public CustomStruct(string key1, string key2, string key3)
        {
            Key1 = key1;
            Key2 = key2;
            Key3 = key3;
        }

        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Key3 { get; set; }

        public override string ToString() => $"({Key1}, {Key2}, {Key3})";
    }

    public abstract class GenericBase<TRequest, TResponse> : BaseFunction
    {
        public TResponse GenericBaseType1(TRequest request, ILambdaContext context)
        {
            return HandleRequest(request);
        }

        public async Task<TResponse> GenericBaseType2(TRequest request, ILambdaContext context)
        {
            HandleRequest(request);
            await Task.Delay(1000);
            return default;
        }

        public virtual TResponse VirtualGenericBaseType3(TRequest request, ILambdaContext context)
        {
            return HandleRequest(request);
        }

        public virtual async Task<TResponse> VirtualGenericBaseType4(TRequest request, ILambdaContext context)
        {
            HandleRequest(request);
            await Task.Delay(1000);
            return default;
        }

        [LambdaSerializer(typeof(CustomSerializer))]
        public GenericBase<CustomInput, Function.NestedClass.InnerGeneric<TRequest, TResponse>> ComplexNestedGeneric(
            Function.NestedClass.InnerGeneric<TRequest, NestedInSameType<TRequest, Dictionary<string, Nested.DeepNested<TResponse>>>> request,
            ILambdaContext context)
        {
            HandleRequest((TRequest)default);
            return default;
        }

        public abstract TResponse AbstractGenericBaseType5(TRequest request, ILambdaContext context);

        public abstract Task<TResponse> AbstractGenericBaseType6(TRequest request, ILambdaContext context);

        protected abstract TResponse HandleRequest(TRequest request, [CallerMemberName] string memberName = null);
        
        public class NestedInSameType<TKey, TValue> : Dictionary<TKey, TValue>
        {
        }

        public class Nested
        {
            public class DeepNested<TNested> : HashSet<TNested>
            {
            }
        }
    }

    public class BaseFunction
    {
        public object BaseHandlerNoParamSync()
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object BaseHandlerTwoParamsSync(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public object BaseHandlerOneParamSyncWithContext(CustomInput request)
        {
            HandleRequest();
            return new { statusCode = 200, body = "ok!" };
        }

        public async Task<int> BaseHandlerOneParamAsync(CustomInput request)
        {
            await Task.Run(
                () =>
                {
                    HandleRequest();
                    Thread.Sleep(100);
                });
            return 10;
        }

        public void BaseHandlerTwoParamsVoid(CustomInput request, ILambdaContext context)
        {
            HandleRequest();
        }

        public void ThrowingHandler()
        {
            Throw();
        }

        public async Task ThrowingHandlerAsync()
        {
            await Task.CompletedTask;
            Throw();
        }

        public Task ThrowingHandlerAsyncTask()
        {
            Throw();
            return Task.CompletedTask; // Never called
        }

        protected void HandleRequest([CallerMemberName] string caller = null)
        {
            using var scope = SampleHelpers.CreateScope($"manual.{caller}");
            var host = Environment.GetEnvironmentVariable("DUMMY_API_HOST")!;
            var url = $"{host}/function/{caller}";

            MakeRequest(url);
        }

        private void Throw([CallerMemberName] string caller = null)
        {
            using var scope = SampleHelpers.CreateScope($"manual.{caller}");
            var url = $"http://localhost/function/{caller}"; // not a valid endopint, so will throw

            MakeRequest(url);
        }

        private void MakeRequest(string url)
        {
            Console.WriteLine("Calling url " + url);

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
    }

    public class CustomSerializer : ILambdaSerializer
    {
        public T Deserialize<T>(Stream requestStream) => default;

        public void Serialize<T>(T response, Stream responseStream)
        {
        }
    }
}
