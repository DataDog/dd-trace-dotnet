// <copyright file="ReflectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Helpers.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Helpers;

internal static class ReflectionHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReflectionHelper));

    internal static object? DuckTypeOriginalMethod(Type proxyType, string staticTypeStr)
    {
        try
        {
            var staticType = Type.GetType(staticTypeStr);
            if (staticType == null)
            {
                throw new Exception($"Failed to get type for {staticTypeStr}");
            }

            var proxyResult = DuckType.GetOrCreateProxyType(proxyType, staticType);
            if (!proxyResult.Success)
            {
                throw new Exception($"Failed to create proxy type for {staticType}");
            }

            return proxyResult.CreateInstance(null!);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to duck type original method for {ProxyType}", proxyType);
            return null;
        }
    }

    internal static CtorWrappers.CtorWrapper<T1, TRes>? WrapOriginalCtor<T1, TRes>(string signature)
    {
        try
        {
            return new CtorWrappers.CtorWrapper<T1, TRes>(signature);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to create proxy type for {Signature}", signature);
            return null;
        }
    }
}
