// <copyright file="AspNetFrameworkEndpointCodeOrigin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    internal static class AspNetFrameworkEndpointCodeOrigin
    {
        internal static bool TryGetTypeAndMethod<TActionDescriptor>(
            TActionDescriptor actionDescriptor,
            [NotNullWhen(true)] out Type? type,
            [NotNullWhen(true)] out MethodInfo? method)
        {
            type = null;
            method = null;

            if (actionDescriptor is null)
            {
                return false;
            }

            if (!actionDescriptor.TryDuckCast<ActionDescriptorWithMethodInfo>(out var reflected)
             || reflected.MethodInfo is not { } actionMethod
             || actionMethod.DeclaringType is not { } actionType)
            {
                return false;
            }

            type = actionType;
            method = actionMethod;
            return true;
        }
    }
}
#endif
