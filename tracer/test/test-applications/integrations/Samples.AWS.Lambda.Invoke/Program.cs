// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Newtonsoft.Json;

namespace Samples.AWS.Lambda.Invoke;

internal static class Program
{
    private static async Task Main()
    {
        using var server = WebServer.Start(out var url);
        server.RequestHandler = HandleRequest;
        using var client = new AmazonLambdaClient(new BasicAWSCredentials("test", "test"), new AmazonLambdaConfig
        {
            ServiceURL = url,
            AuthenticationRegion = "us-east-1",
            MaxErrorRetry = 0,
        });

        foreach (var request in CreateRequests())
        {
            request.Payload = "{\"message\":\"unchanged\"}";
            await client.InvokeAsync(request);
        }

#if NETFRAMEWORK
        foreach (var request in CreateRequests())
        {
            request.Payload = "{\"message\":\"unchanged\"}";
            client.Invoke(request);
        }
#endif
        try
        {
            await client.InvokeAsync(new InvokeRequest { FunctionName = "failed-invoke", Payload = "{}" });
        }
        catch (AmazonLambdaException)
        {
            Console.WriteLine("EXPECTED_ERROR:failed-invoke");
        }
    }

    private static InvokeRequest[] CreateRequests() =>
    [
        new InvokeRequest { FunctionName = "default-context" },
        new InvokeRequest { FunctionName = "existing-custom", InvocationType = InvocationType.RequestResponse, ClientContext = "{\"custom\":{\"application\":\"value\"},\"env\":{\"name\":\"example\"}}" },
        new InvokeRequest { FunctionName = "existing-env", InvocationType = InvocationType.RequestResponse, ClientContext = "{\"env\":{\"name\":\"example\"}}" },
        new InvokeRequest { FunctionName = "event", InvocationType = InvocationType.Event },
        new InvokeRequest { FunctionName = "dry-run", InvocationType = InvocationType.DryRun },
    ];

    private static void HandleRequest(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var functionName = context.Request.Url.AbsolutePath.Split('/');
        Console.WriteLine("WIRE:" + JsonConvert.SerializeObject(new
        {
            function = functionName[functionName.Length - 2],
            clientContext = context.Request.Headers["X-Amz-Client-Context"],
            payload = reader.ReadToEnd(),
        }));

        context.Response.Headers.Add("x-amzn-RequestId", "test-request-id");
        context.Response.ContentType = "application/json";
        var failed = context.Request.Url.AbsolutePath.Contains("failed-invoke");
        context.Response.StatusCode = failed ? 500 : 200;
        var bytes = Encoding.UTF8.GetBytes(failed ? "{\"message\":\"test failure\"}" : "{}");
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }
}
