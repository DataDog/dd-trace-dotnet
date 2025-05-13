// <copyright file="TypedDataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

internal static class TypedDataHelper<TMarkerType>
{
    private static readonly ActivatorHelper TypedDataActivator;
    private static readonly ActivatorHelper HttpActivator;

    static TypedDataHelper()
    {
        var assembly = typeof(TMarkerType).Assembly;
        TypedDataActivator = new ActivatorHelper(assembly.GetType("Microsoft.Azure.WebJobs.Script.Grpc.Messages.TypedData")!);
        HttpActivator = new ActivatorHelper(assembly.GetType("Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcHttp")!);
    }

    public static ITypedData CreateTypedData()
    {
        // Emulate new TypedData() { Http = new RpcHttp() };
        var typedData = TypedDataActivator.CreateInstance();
        var typedDataProxy = typedData.DuckCast<ITypedData>();

        var http = HttpActivator.CreateInstance();
        typedDataProxy.Http = http.DuckCast<IRpcHttp>();

        return typedDataProxy;
    }
}
#endif
