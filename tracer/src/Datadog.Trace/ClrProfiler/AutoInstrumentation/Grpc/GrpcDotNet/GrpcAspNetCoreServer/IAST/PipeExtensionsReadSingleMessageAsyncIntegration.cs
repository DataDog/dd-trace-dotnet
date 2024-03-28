// <copyright file="PipeExtensionsReadSingleMessageAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NET461

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer.IAST;

/// <summary>
/// System.Threading.Tasks.ValueTask`1[T] Grpc.AspNetCore.Server.Internal.PipeExtensions::ReadSingleMessageAsync[T](System.IO.Pipelines.PipeReader,Grpc.AspNetCore.Server.Internal.HttpContextServerCallContext,System.Func`2[Grpc.Core.DeserializationContext,T]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Grpc.AspNetCore.Server",
    TypeName = "Grpc.AspNetCore.Server.Internal.PipeExtensions",
    MethodName = "ReadSingleMessageAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1[!!0]",
    ParameterTypeNames = ["System.IO.Pipelines.PipeReader", "Grpc.AspNetCore.Server.Internal.HttpContextServerCallContext", "System.Func`2[Grpc.Core.DeserializationContext,!!0]"],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(Grpc))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class PipeExtensionsReadSingleMessageAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PipeExtensionsReadSingleMessageAsyncIntegration));

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (T)</typeparam>
    /// <param name="returnValue">Instance of T</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        try
        {
            TaintVisitor.Visit(returnValue, SourceType.GrpcRequestBody, 10, 1000);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error visiting the grpc server request message");
        }

        return returnValue;
    }
}
#endif
