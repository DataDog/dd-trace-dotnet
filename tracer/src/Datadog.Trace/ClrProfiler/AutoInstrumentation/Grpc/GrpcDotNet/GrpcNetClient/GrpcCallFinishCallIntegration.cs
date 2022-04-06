// <copyright file="GrpcCallFinishCallIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    /// <summary>
    /// Grpc.Net.Client.Internal.GrpcCall calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Net.Client",
        TypeName = "Grpc.Net.Client.Internal.GrpcCall`2",
        MethodName = "FinishCall",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.HttpRequestMessage, ClrNames.Bool, ClrNames.Activity, "Grpc.Core.Status" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GrpcCallFinishCallIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TRequest">Type of the request</typeparam>
        /// <typeparam name="TActivity">Type of the activity</typeparam>
        /// <typeparam name="TStatus">Type of the status</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="requestMessage">HttpRequest message instance</param>
        /// <param name="diagnosticSourceEnabled">Whether diagnostic source is enabled</param>
        /// <param name="activity">The activity (may be null)</param>
        /// <param name="status">The status (Nullable)</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TRequest, TActivity, TStatus>(TTarget instance, in TRequest requestMessage, bool diagnosticSourceEnabled, in TActivity activity, TStatus status)
            where TStatus : IStatus
        {
            // always supported, as only applies in v2.43.0+
            GrpcDotNetClientCommon.RecordResponseMetadataAndStatus(Tracer.Instance, instance, status.StatusCode, status.Detail, status.DebugException);
            return CallTargetState.GetDefault();
        }
    }
}
#endif
