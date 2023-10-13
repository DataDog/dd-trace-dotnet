// <copyright file="HandlerWrapperSetHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;

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
    MinimumVersion = "1.4.0",
    MaximumVersion = "1.*.*",
    IntegrationName = IntegrationName)]
public class HandlerWrapperSetHandlerIntegration
{
    private const string IntegrationName = nameof(IntegrationId.AwsLambda);
    private static readonly ILambdaExtensionRequest RequestBuilder = new LambdaRequestBuilder();
    private static readonly Async1Callbacks _callbacks = new Async1Callbacks();

    /// <summary>
    /// OnMethodBegin callback. The input Delegate handler is the customer's handler.
    /// And it's already wrapped by the Amazon.Lambda.RuntimeSupport.HandlerWrapper.
    /// Here we will try further wrap it with our DelegateWrapper class
    /// in order to make it notify the Datadog-Extension before and after the handler's each invocation.
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
    /// <param name="handler">Instance of Amazon.Lambda.RuntimeSupport.LambdaBootstrapHandler</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref Delegate handler)
    {
        handler = handler.Instrument(_callbacks);
        return CallTargetState.GetDefault();
    }

    private readonly struct Async1Callbacks : IBegin1Callbacks, IReturnCallback, IReturnAsyncCallback
    {
        public bool PreserveAsyncContext => false;

        public object OnDelegateBegin<TArg1>(object sender, ref TArg1 arg)
        {
            Serverless.Debug("DelegateWrapper Running onDelegateBegin");
            try
            {
                var proxyInstance = arg.DuckCast<IInvocationRequest>();
                if (proxyInstance != null)
                {
                    var reader = new StreamReader(proxyInstance.InputStream, Encoding.UTF8, leaveOpen: true);
                    var json = reader.ReadToEnd();
                    proxyInstance.InputStream.Seek(0, SeekOrigin.Begin);
                    var scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), json, proxyInstance.LambdaContext?.ClientContext?.Custom);
                    return new CallTargetState(scope);
                }
                else
                {
                    var scope = LambdaCommon.SendStartInvocation(new LambdaRequestBuilder(), string.Empty, null);
                    return new CallTargetState(scope);
                }
            }
            catch (Exception ex)
            {
                Serverless.Error("Could not send payload to the extension", ex);
                Console.WriteLine(ex.StackTrace);
            }

            Serverless.Debug("DelegateWrapper FINISHED Running OnDelegateBegin");
            return CallTargetState.GetDefault();
        }

        /// <inheritdoc/>
        public TReturn OnDelegateEnd<TReturn>(object sender, TReturn returnValue, Exception exception, object state)
        {
            return returnValue;
        }

        /// <inheritdoc/>
        public Task<TInnerReturn> OnDelegateEndAsync<TInnerReturn>(object sender, TInnerReturn returnValue, Exception exception, object state)
        {
            Serverless.Debug("DelegateWrapper Running onDelegateAsyncEnd");
            try
            {
                var proxyInstance = returnValue.DuckCast<IInvocationResponse>();
                if (proxyInstance != null)
                {
                    var reader = new StreamReader(proxyInstance.OutputStream, Encoding.UTF8, leaveOpen: true);
                    var json = reader.ReadToEnd();
                    proxyInstance.OutputStream.Seek(0, SeekOrigin.Begin);
                    LambdaCommon.EndInvocationSync(json, exception, ((CallTargetState)state!).Scope, RequestBuilder);
                }
                else
                {
                    LambdaCommon.EndInvocationSync(string.Empty, exception, ((CallTargetState)state!).Scope, RequestBuilder);
                }
            }
            catch (Exception ex)
            {
                Serverless.Debug(ex.StackTrace);
                LambdaCommon.EndInvocationSync(string.Empty, ex, ((CallTargetState)state!).Scope, RequestBuilder);
            }

            Serverless.Debug("DelegateWrapper FINISHED Running onDelegateAsyncEnd");
            return Task.FromResult(returnValue);
        }

        /// <inheritdoc/>
        public void OnException(object sender, Exception ex)
        {
        }
    }
}
#endif
