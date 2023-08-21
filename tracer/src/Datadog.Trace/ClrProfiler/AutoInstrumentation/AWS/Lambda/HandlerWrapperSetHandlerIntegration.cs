// <copyright file="HandlerWrapperSetHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS;
using Datadog.Trace.Configuration;

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
    private const string IntegrationName = nameof(IntegrationId.AwsSdk);
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
                state = LambdaCommon.StartInvocationOneParameter(RequestBuilder, arg);
            },

            AfterDelegate = (sender, arg, returnValue, exception) =>
            {
                Serverless.Debug("DelegateWrapper Running AfterDelegate");
                Console.WriteLine($"returnValue AfterDelegate {returnValue}");
                LambdaCommon.EndInvocationSync(returnValue, exception, state.Scope, RequestBuilder);
                return returnValue;
            },

            AfterDelegateAsync = (sender, arg, returnValue, exception) =>
            {
                LambdaCommon.EndInvocationSync(returnValue, exception, state.Scope, RequestBuilder);
                Console.WriteLine($"returnValue ASYNC {returnValue}");
            }
        });
        handler = wrapper.Handler;
        return CallTargetState.GetDefault();
    }
}
