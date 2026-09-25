// <copyright file="QueryInternalAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    /// <summary>
    /// DnsClient.LookupClient.QueryInternalAsync calltarget instrumentation.
    /// This is the internal convergence point for all asynchronous public query entry points.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "DnsClient",
        TypeName = "DnsClient.LookupClient",
        MethodName = "QueryInternalAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[DnsClient.IDnsQueryResponse]",
        ParameterTypeNames = ["DnsClient.DnsQuestion", "DnsClient.DnsQuerySettings", "System.Collections.Generic.IReadOnlyCollection`1[DnsClient.NameServer]", ClrNames.CancellationToken],
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.*.*",
        IntegrationName = DnsClientCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class QueryInternalAsyncIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TQuestion, TSettings, TServers>(TTarget instance, TQuestion question, TSettings settings, TServers servers, CancellationToken cancellationToken)
        {
            var scope = DnsClientCommon.CreateScope(Tracer.Instance, question, servers);
            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (exception is null)
            {
                DnsClientCommon.PopulateResponseTags(state.Scope, returnValue);
            }

            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
