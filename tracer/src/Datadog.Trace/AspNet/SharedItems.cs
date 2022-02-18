// <copyright file="SharedItems.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Web;

namespace Datadog.Trace.AspNet
{
    internal static class SharedItems
    {
        public const string HttpContextPropagatedResourceNameKey = "__Datadog.Trace.ClrProfiler.Managed.AspNetMvcIntegration-aspnet.resourcename";

        internal static void PushScope(HttpContext context, string key, Scope item)
        {
            if (context == null)
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

        internal static Scope TryPopScope(HttpContext context, string key)
        {
            var item = context?.Items[key];
            if (item is Scope storedScope)
            {
                return storedScope;
            }
            else if (item is Stack<Scope> stack && stack.Count > 0)
            {
                return stack.Pop();
            }

            return default(Scope);
        }

        internal static Scope TryPeakScope(HttpContext context, string key)
        {
            var item = context?.Items[key];
            if (item is Scope storedScope)
            {
                return storedScope;
            }
            else if (item is Stack<Scope> stack && stack.Count > 0)
            {
                return stack.Peek();
            }

            return default(Scope);
        }
    }
}
#endif
