// <copyright file="QueryInternalIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient
{
    /// <summary>
    /// DnsClient.LookupClient.QueryInternal calltarget instrumentation.
    /// This is the internal convergence point for all synchronous public query entry points.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "DnsClient",
        TypeName = "DnsClient.LookupClient",
        MethodName = "QueryInternal",
        ReturnTypeName = "DnsClient.IDnsQueryResponse",
        ParameterTypeNames = ["DnsClient.DnsQuestion", "DnsClient.DnsQuerySettings", "System.Collections.Generic.IReadOnlyCollection`1[DnsClient.NameServer]"],
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.*.*",
        IntegrationName = DnsClientCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class QueryInternalIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TQuestion, TSettings, TServers>(TTarget instance, TQuestion question, TSettings settings, TServers servers)
        {
            var scope = DnsClientCommon.CreateScope(Tracer.Instance, question, servers);
            return new CallTargetState(scope);
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (exception is null)
            {
                DnsClientCommon.PopulateResponseTags(state.Scope, returnValue);
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
