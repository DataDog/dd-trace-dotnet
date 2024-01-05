// <copyright file="DuckTypeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Helpers.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Helpers;

internal static class DuckTypeHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DuckTypeHelper));

    internal static object? DuckTypeOriginalMethod(Type proxyType, Type staticType)
    {
        try
        {
            var proxyResult = DuckType.GetOrCreateProxyType(proxyType, staticType);
            if (!proxyResult.Success)
            {
                Log.Warning("Failed to create proxy type for {StaticType}", staticType);
                return null;
            }

            return proxyResult.CreateInstance(null!);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to duck type original method for {StaticType}", staticType);
            return null;
        }
    }

    internal static FuncWrappers.FuncWrapper<T1, TRes>? DuckTypeOriginalCtor<T1, TRes>(string signature)
    {
        try
        {
            return new FuncWrappers.FuncWrapper<T1, TRes>(signature);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create proxy type for {Signature}", signature);
            return null;
        }
    }
}
