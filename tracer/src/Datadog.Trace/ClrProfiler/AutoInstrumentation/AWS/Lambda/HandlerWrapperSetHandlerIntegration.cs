// <copyright file="HandlerWrapperSetHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
// using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

/// <summary>
/// Instrumenting the HandlerWrapper's GetHandlerWrapper method to wrap the Handler with Datadog wrapping methods
/// </summary>
[InstrumentMethod(
    AssemblyName = "Amazon.Lambda.RuntimeSupport",
    TypeName = "Amazon.Lambda.RuntimeSupport.HandlerWrapper",
    MethodName = "set_Handler",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "Amazon.Lambda.RuntimeSupport.LambdaBootstrapHandler" },
    MinimumVersion = "1.0.0",
    MaximumVersion = "1.*.*",
    IntegrationName = IntegrationName)]
public class HandlerWrapperSetHandlerIntegration
{
    private const string IntegrationName = nameof(IntegrationId.AwsLambda);
    private static readonly ILambdaExtensionRequest RequestBuilder = new LambdaRequestBuilder();

    /// <summary>
    /// OnMethodBegin callback. The input Delegate handler is the customer's handler wrapped by the HandlerWrapper.
    /// Here we will try further wrap it with our DelegateWrapper class in order to make it notify the Datadog-Extension
    /// before and after the handler's each invocation.
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="handler">Instance of Amazon.Lambda.RuntimeSupport.LambdaBootstrapHandler</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref Delegate handler)
    {
        Serverless.Debug("DelegateWrapper Wrapping the Handler");

        var state = CallTargetState.GetDefault();
        var wrapper = DelegateWrapper.Wrap(handler, new DelegateWrapper.WrapperCallbacks
        {
            BeforeDelegate = (sender,  arg) =>
            {
                Serverless.Debug("DelegateWrapper Running BeforeDelegate");
                // state = LambdaCommon.StartInvocationOneParameter(RequestBuilder, arg);
                try
                {
                    Console.WriteLine($"ARG_TYPE {arg.GetType()}");
                    var proxyInstance = arg.DuckCast<IInvocationRequest>();
                    Console.WriteLine($"is requestProxyInstance okay? {proxyInstance != null}");
                    Console.WriteLine($"is InputStream okay? {proxyInstance.InputStream != null}");
                    Console.WriteLine($"is LambdaContext okay? {proxyInstance.LambdaContext != null}");
                    Console.WriteLine($"is ClientContext okay? {proxyInstance.LambdaContext?.ClientContext?.Custom != null}");
                    var reader = new StreamReader(proxyInstance.InputStream, Encoding.UTF8, leaveOpen: true);
                    string json = reader.ReadToEnd();
                    proxyInstance.InputStream.Seek(0, SeekOrigin.Begin);
                    var scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), json, proxyInstance.LambdaContext?.ClientContext?.Custom);
                    state = new CallTargetState(scope);
                    // using (MemoryStream memoryStream = new MemoryStream())
                    // {
                    //     byte[] buffer = new byte[1024]; // You can adjust the buffer size as needed
                    //     int bytesRead;
                    //     while ((bytesRead = proxyInstance.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                    //     {
                    //         memoryStream.Write(buffer, 0, bytesRead);
                    //     }
                    //
                    //     var inputString = Convert.ToBase64String(memoryStream.ToArray());
                    //     var scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), inputString, proxyInstance.LambdaContext?.ClientContext?.Custom);
                    //
                    //     state = new CallTargetState(scope);
                    // }
                }
                catch (Exception ex)
                {
                    Serverless.Error("Could not send payload to the extension", ex);
                    Console.WriteLine(ex.StackTrace);
                }
            },

            AfterDelegate = (sender, arg, returnValue, exception) =>
            {
                Serverless.Debug("DelegateWrapper Running AfterDelegate");
                Console.WriteLine($"returnValue AfterDelegate {returnValue}");
                return returnValue;
            },

            AfterDelegateAsync = (sender, arg, returnValue, exception) =>
            {
                Console.WriteLine($"returnValue ASYNC {returnValue}");
                var proxyInstance = returnValue.DuckCast<IInvocationResponse>();
                Console.WriteLine($"is responseProxyInstance okay? {proxyInstance != null}");
                Console.WriteLine($"is InputStream okay? {proxyInstance.OutputStream != null}");
                var reader = new StreamReader(proxyInstance.OutputStream, Encoding.UTF8, leaveOpen: true);
                string json = reader.ReadToEnd();
                Console.WriteLine($"returnValue json {json}");
                proxyInstance.OutputStream.Seek(0, SeekOrigin.Begin);
                LambdaCommon.EndInvocationSync(json, exception, state.Scope, RequestBuilder);
            }
        });
        handler = wrapper.Handler;
        return CallTargetState.GetDefault();
    }
}
