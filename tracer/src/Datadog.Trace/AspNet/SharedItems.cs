// <copyright file="SharedItems.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Web;

namespace Datadog.Trace.AspNet
{
    internal static class SharedItems
    {
        public const string HttpContextPropagatedResourceNameKey = "__Datadog.Trace.ClrProfiler.Managed.AspNetMvcIntegration-aspnet.resourcename";
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

        internal static Scope? TryPeekScope(HttpContext? context, string key) => ExtractScope(context, key, Peek);

        /// <summary>
        /// Gets the scope an AppSec check should report against. With OpenTelemetry semantics the MVC
        /// and Web API integrations don't create a span of their own -- a request has a single HTTP
        /// server span -- so there is nothing under <paramref name="key"/> and the active span, which is
        /// that server span, is used instead.
        /// </summary>
        /// <param name="context">The context of the current request</param>
        /// <param name="key">The <see cref="HttpContext.Items"/> key the integration pushes its scope under</param>
        internal static Scope? TryPeekScopeOrServerScope(HttpContext? context, string key)
        {
            var scope = TryPeekScope(context, key);
            if (scope is not null)
            {
                return scope;
            }

            var tracer = Tracer.Instance;
            return tracer.Settings.OtelSemanticsEnabled ? tracer.InternalActiveScope : null;
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
    }
}
#endif
