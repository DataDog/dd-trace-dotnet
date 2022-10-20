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

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

Console.WriteLine("Running top-level statement handler");

var handler = async (CustomInput request, ILambdaContext context) =>
{
    // ReSharper disable once ExplicitCallerInfoArgument
    BaseFunction.HandleRequest("TopLevelStatement");
    await Task.Yield();
    return new { statusCode = 200, body = "ok!" };
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
                            .Build()
                            .RunAsync();
