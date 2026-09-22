// <copyright file="SharedItems.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Web;
using Datadog.Trace.OpenTelemetry;

namespace Datadog.Trace.AspNet
{
    internal static class SharedItems
    {
        public const string HttpContextPropagatedResourceNameKey = "__Datadog.Trace.ClrProfiler.Managed.AspNetMvcIntegration-aspnet.resourcename";
        private const string HttpContextRecordedExceptionKey = "__Datadog.Trace.ClrProfiler.Managed.AspNetIntegration-recorded-exception";
        private static readonly Func<Stack<Scope>, Scope> Pop = stack => stack.Pop();
        private static readonly Func<Stack<Scope>, Scope> Peek = stack => stack.Peek();

        internal static void PushScope(HttpContext? context, string key, Scope item)
        {
            if (context is null)
            {
                return;
            }

            // Storing only the scope by default to avoid allocating a stack if no inner calls are done
            var existingItem = context.Items[key];
            if (existingItem is null)
            {
                context.Items[key] = item;
            }
            else if (existingItem is Stack<Scope> stack)
            {
                stack.Push(item);
            }
            else if (existingItem is Scope previousScope)
            {
                var newStack = new Stack<Scope>();
                newStack.Push(previousScope);
                newStack.Push(item);
                context.Items[key] = newStack;
            }
        }

        internal static Scope? TryPopScope(HttpContext? context, string key) => ExtractScope(context, key, Pop);

        internal static void MarkExceptionRecorded(HttpContext? context, ISpan span, Exception exception)
        {
            if (context is not null)
            {
                context.Items[HttpContextRecordedExceptionKey] = new RecordedException(span, exception);
            }
        }

        internal static bool IsExceptionRecorded(HttpContext? context, ISpan span, Exception exception)
            => context?.Items[HttpContextRecordedExceptionKey] is RecordedException recordedException
                && ReferenceEquals(recordedException.Span, span)
                && ReferenceEquals(recordedException.Exception, exception);

        /// <summary>
        /// Gets the scope from the HttpContext with the provided key, corresponding to the integration that created it.
        /// When using OpenTelemetry semantics, the MVC and Web API integrations don't create a span of their own if a
        /// root HTTP span exists, so there is nothing under <paramref name="key"/>. To unify all behaviors on the HTTP
        /// server span, the active root  HTTP span is returned when <paramref name="fallbackToActiveOtelHttpServerScope"/>
        /// is set to true (by default). This fallback can be explicitly disabled, if needed.
        /// </summary>
        /// <param name="context">The context of the current request</param>
        /// <param name="key">The <see cref="HttpContext.Items"/> key the integration pushes its scope under</param>
        /// <param name="fallbackToActiveOtelHttpServerScope">When OTel semantics are enabled, should the active HTTP server scope be used.</param>
        internal static Scope? TryPeekScope(HttpContext? context, string key, bool fallbackToActiveOtelHttpServerScope = true)
        {
            var scope = ExtractScope(context, key, Peek);
            if (scope is not null || !fallbackToActiveOtelHttpServerScope)
            {
                return scope;
            }

            var tracer = Tracer.Instance;
            return tracer.Settings.OtelSemanticsEnabled ? HttpSemanticConventions.GetActiveHttpServerScope(tracer) : null;
        }

        private static Scope? ExtractScope(HttpContext? context, string key, Func<Stack<Scope>, Scope> getter)
        {
            var item = context?.Items[key];
            if (item is Scope storedScope)
            {
                return storedScope;
            }
            else if (item is Stack<Scope> stack && stack.Count > 0)
            {
                return getter(stack);
            }

            return default;
        }

        private sealed class RecordedException
        {
            public RecordedException(ISpan span, Exception exception)
            {
                Span = span;
                Exception = exception;
            }

            public ISpan Span { get; }

            public Exception Exception { get; }
        }
    }
}
#endif
