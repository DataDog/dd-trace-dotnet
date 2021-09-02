// <copyright file="BlockingMiddleware.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AppSec.AspNetCore
{
    internal static class BlockingMiddleware
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BlockingMiddleware));

        public static void ModifyApplicationBuilder(object applicationBuilderInstance)
        {
            if (applicationBuilderInstance.TryDuckCast<ApplicationBuilderDuck>(out var applicationBuilder))
            {
                InsertMiddlewares(applicationBuilder.Components);
            }
            else
            {
                Log.Error($"Couldn't create duck type from {applicationBuilderInstance?.GetType()?.FullName ?? "(null)"}");
            }
        }

        private static void InsertMiddlewares(List<Func<RequestDelegate, RequestDelegate>> components)
        {
            static async Task Middleware(HttpContext context, Func<Task> next)
            {
                if (context.Items.ContainsKey(SecurityConstants.KillKey) && context.Items[SecurityConstants.KillKey] is bool killKey && killKey)
                {
                    await BlockRequest(context);
                }
                else
                {
                    try
                    {
                        context.Items[SecurityConstants.InHttpPipeKey] = true;
                        await next.Invoke();
                    }
                    catch (BlockActionException)
                    {
                        await BlockRequest(context);
                    }
                }
            }

            RequestDelegate MiddlewareWrapper(RequestDelegate next) => context => Middleware(context, () => next(context));

            components.Insert(0, MiddlewareWrapper);
            if (components.Count > 2)
            {
                // insert 2nd to last, making a guess that the last one will be the user action
                components.Insert(components.Count - 1, MiddlewareWrapper);
            }
        }

        private static async Task BlockRequest(HttpContext context)
        {
            bool blockByThrow = true;
            if (!context.Response.HasStarted)
            {
                blockByThrow = false;
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(SecurityConstants.AttackBlockedHtml);
            }

            var callbackExists = context.Items.TryGetValue("Security", out var result);
            if (callbackExists && result is Guid callbackId)
            {
                Security.Instance.Execute(callbackId);
            }

            if (blockByThrow)
            {
                throw new PageBlockedByAppSecException();
            }
        }

        /// <summary>
        /// Application builder proxy
        /// </summary>
        [DuckCopy]
        public struct ApplicationBuilderDuck
        {
            /// <summary>
            /// The components that will make up the application http pipe
            /// </summary>
            [DuckField(Name = "_components")]
            public List<Func<RequestDelegate, RequestDelegate>> Components;
        }
    }
}
#endif
