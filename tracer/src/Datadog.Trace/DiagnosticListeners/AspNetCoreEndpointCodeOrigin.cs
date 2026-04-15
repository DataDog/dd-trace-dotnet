// <copyright file="AspNetCoreEndpointCodeOrigin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners;

internal static class AspNetCoreEndpointCodeOrigin
{
    internal static bool TryGetTypeAndMethod(RouteEndpoint routeEndpoint, [NotNullWhen(true)] out Type? type, [NotNullWhen(true)] out MethodInfo? method)
    {
        type = null;
        method = null;

        // Fast path: use RequestDelegate.Method and prefer the delegate target type if available.
        var requestDelegate = routeEndpoint.RequestDelegate;
        var delegateMethod = requestDelegate?.Method;
        if (delegateMethod is not null)
        {
            var candidate = requestDelegate?.Target?.GetType() ?? delegateMethod.DeclaringType ?? delegateMethod.ReflectedType;
            if (candidate is not null)
            {
                type = candidate;
                method = delegateMethod;
                return true;
            }
        }

        // Fallback: minimal API/internal shapes (RequestDelegate.Target.handler)
        if (requestDelegate?.Target is { } requestDelegateTarget &&
            TryGetHandlerFromRequestDelegateTarget(requestDelegateTarget, out var handler) &&
            handler.Method is { } handlerMethod)
        {
            var candidate = handler.Target?.GetType() ?? handlerMethod.DeclaringType ?? handlerMethod.ReflectedType;
            if (candidate is not null)
            {
                type = candidate;
                method = handlerMethod;
                return true;
            }
        }

        // We can consider adding additional fallbacks here
        // (e.g., endpoint metadata scan and/or a carefully bounded reflection-based fallback).

        return false;
    }

    private static bool TryGetHandlerFromRequestDelegateTarget(object requestDelegateTarget, [NotNullWhen(true)] out Delegate? handler)
    {
        handler = null;

        // Common shape: field "handler"
        if (requestDelegateTarget.TryDuckCast<Target>(out var target) && target.Handler is { } h1)
        {
            handler = h1;
            return true;
        }

        return false;
    }
}
