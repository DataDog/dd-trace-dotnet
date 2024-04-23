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

        internal static void PushScope(HttpContext context, string key, Scope item)
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

        internal static Scope? TryPopScope(HttpContext context, string key) => ExtractScope(context, key, Pop);

        internal static Scope? TryPeekScope(HttpContext context, string key) => ExtractScope(context, key, Peek);

        private static Scope? ExtractScope(HttpContext context, string key, Func<Stack<Scope>, Scope> getter)
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
