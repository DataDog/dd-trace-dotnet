// <copyright file="AspNetCoreEndpointCodeOrigin.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.DuckTyping;
#if !NETFRAMEWORK
using Datadog.Trace.Logging;
#endif

namespace Datadog.Trace.DiagnosticListeners;

internal static class AspNetCoreEndpointCodeOrigin
{
#if !NETFRAMEWORK
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetCoreEndpointCodeOrigin));
#endif

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
            requestDelegateTarget.TryDuckCast<Target>(out var target) &&
            target.Handler is { } handler &&
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

#if !NETFRAMEWORK
    internal static bool TryGetTypeAndMethod(AspNetCoreDiagnosticObserver.BeforeActionStruct beforeAction, [NotNullWhen(true)] out Type? type, [NotNullWhen(true)] out MethodInfo? method)
    {
        try
        {
            if (beforeAction.ActionDescriptor.TryDuckCast<AspNetCoreDiagnosticObserver.ControllerActionDescriptorStruct>(out var controllerActionDescriptor))
            {
                type = controllerActionDescriptor.ControllerTypeInfo;
                method = controllerActionDescriptor.MethodInfo;
                return true;
            }

            if (beforeAction.ActionDescriptor.TryDuckCast<AspNetCoreDiagnosticObserver.CompiledPageActionDescriptorStruct>(out var compiledPageActionDescriptor))
            {
                foreach (var part in compiledPageActionDescriptor.HandlerMethods)
                {
                    if (part.TryDuckCast(out AspNetCoreDiagnosticObserver.HandlerMethodDescriptorStruct methodDesc))
                    {
                        if (string.Equals(methodDesc.HttpMethod, beforeAction.HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
                        {
                            type = compiledPageActionDescriptor.HandlerTypeInfo;
                            method = methodDesc.MethodInfo;
                            return true;
                        }

                        Log.Debug("Ignoring handler method {Method} for HTTP method {HttpMethod}", methodDesc.MethodInfo.Name, methodDesc.HttpMethod);
                    }
                }

                Log.Debug("No matching handler method found for HTTP method {HttpMethod}", beforeAction.HttpContext.Request.Method);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to extract type and method from ActionDescriptor");
        }

        type = null;
        method = null;
        return false;
    }
#endif
}
