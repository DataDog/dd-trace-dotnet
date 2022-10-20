using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Samples;

namespace Samples.AWS.Lambda;

public class Function : BaseFunction
{
    // See documentation at docs/development/Serverless.md.
    // These are the handler methods which run in the lambda containers
    // and are invoked by the lambda runtime in response to a request.
    // Each handler creates a manual scope, makes a request to a "dummy"
    // API (which is traced), and returns.
    // If the handler is created "with context" then the spans created inside
    // the HandleRequest method are children of a parent (distributed) context,
    // otherwise they will be root spans
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
        await Task.Run(
            () =>
            {
                HandleRequest();
                Thread.Sleep(100);
            });
        return 10;
    }

    public async Task<int> HandlerOneParamAsync(CustomInput request)
    {
        await Task.Run(
            () =>
            {
                HandleRequest();
                Thread.Sleep(100);
            });
        return 10;
    }

    public async Task<int> HandlerTwoParamsAsync(CustomInput request, ILambdaContext context)
    {
        await Task.Run(
            () =>
            {
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

    public void HandlerDoublyNestedGenericDictionaryParam(NestedClass.InnerGeneric<string, NestedClass.InnerGeneric<string, Dictionary<string, string>>> request)
    {
        HandleRequest();
    }

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

    internal static void HandleRequest([CallerMemberName] string caller = null)
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

    private static void MakeRequest(string url)
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
