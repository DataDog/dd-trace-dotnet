// <copyright file="AspNetFrameworkCodeOriginHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    internal static class AspNetFrameworkCodeOriginHelper
    {
        internal static void AddSpanCodeOrigin<TActionDescriptor>(
            TActionDescriptor actionDescriptor,
            SpanCodeOrigin codeOrigin,
            string httpContextKey,
            IDatadogLogger log,
            string descriptorKind)
        {
            if (actionDescriptor is null)
            {
                return;
            }

            var httpContext = HttpContext.Current;
            if (SharedItems.TryPeekScope(httpContext, httpContextKey) is not { Root.Span: { } rootSpan })
            {
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug(
                        "Code origin is enabled but scope was not found in HttpContext (key: {HttpContextKey}, httpContextNull: {HttpContextIsNull}, itemsCount: {HttpContextItemsCount}, actionDescriptorType: {ActionDescriptorType}).",
                        httpContextKey,
                        httpContext is null,
                        httpContext?.Items?.Count ?? 0,
                        actionDescriptor.GetType());
                }

                return;
            }

            if (codeOrigin.HasCodeOrigin(rootSpan))
            {
                return;
            }

            if (!actionDescriptor.TryDuckCast<ActionDescriptorWithMethodInfo>(out var reflected)
             || reflected.MethodInfo is not { } actionMethod
             || actionMethod.DeclaringType is not { } actionType)
            {
                if (log.IsEnabled(LogEventLevel.Debug))
                {
                    log.Debug(
                        "Code origin is enabled but could not extract action from {DescriptorKind} type {ActionDescriptorType} or action MethodInfo has no DeclaringType.",
                        descriptorKind,
                        actionDescriptor.GetType());
                }

                return;
            }

            codeOrigin.SetCodeOriginForEntrySpan(rootSpan, actionType, actionMethod);
        }
    }
}
#endif
