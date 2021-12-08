// <copyright file="SharedItems.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Web;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AspNet
{
    internal static class SharedItems
    {
        public const string HttpContextPropagatedResourceNameKey = "__Datadog.Trace.ClrProfiler.Managed.AspNetMvcIntegration-aspnet.resourcename";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SharedItems));

        internal static void PushItem<T>(HttpContext context, string key, T item)
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
            else if (existingItem is Stack<T> stack)
            {
                stack.Push(item);
            }
            else if (existingItem is T previousScope)
            {
                var newStack = new Stack<T>();
                newStack.Push(previousScope);
                newStack.Push(item);
                context.Items[key] = newStack;
            }
            else
            {
                Log.Warning("Trying to push an item in HttpContext.Items but a previous object of unhandled type is already stored there");
            }
        }

        internal static T TryPopItem<T>(HttpContext context, string key)
        {
            var item = context?.Items[key];
            if (item is T storedScope)
            {
                return storedScope;
            }
            else if (item is Stack<T> stack && stack.Count > 0)
            {
                return stack.Pop();
            }
            
            return default(T);
        }
    }
}
#endif
