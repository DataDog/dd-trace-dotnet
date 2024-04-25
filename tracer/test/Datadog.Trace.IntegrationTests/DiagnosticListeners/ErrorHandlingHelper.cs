// <copyright file="ErrorHandlingHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public static class ErrorHandlingHelper
    {
        public const string CustomHandlerPrefix = "/custom";
        public const string ReExecuteHandlerPrefix = "/reexecute";
        public const string ExceptionPagePrefix = "/devexeceptions";
        public const string StatusCodeReExecutePrefix = "/statuscodereexecute";

        public static void ThrowBadHttpRequestException()
        {
#if NETCOREAPP2_1
            Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException.Throw(
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.RequestRejectionReason.InvalidRequestHeader,
                Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpMethod.Get);
#elif NET5_0_OR_GREATER
            throw new Microsoft.AspNetCore.Http.BadHttpRequestException("BAD", 400);
#else
            try
            {
                var badRequest = typeof(Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException);
                var rejectReasonType = badRequest.Assembly.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.RequestRejectionReason");

                var enumValueField = rejectReasonType.GetField("InvalidRequestHeader", BindingFlags.Static | BindingFlags.Public);
                object enumValue = enumValueField.GetValue(obj: null);
                var signature = new[] { rejectReasonType };
                var method = badRequest.GetMethod("Throw", BindingFlags.Static | BindingFlags.NonPublic, Type.DefaultBinder, signature, null);
                method.Invoke(obj: null, parameters: new[] { enumValue });
            }
            catch (TargetInvocationException ex)
            {
                // we expect this to throw, so unwrap and rethrow it.
                throw ex.InnerException;
            }
#endif
            }

        public static IApplicationBuilder UseMultipleErrorHandlerPipelines(
            this IApplicationBuilder app,
            Action<IApplicationBuilder> remainingMiddleware)
        {
            app.Map(
                CustomHandlerPrefix,
                inner =>
                    inner.UseExceptionHandler(
                              new ExceptionHandlerOptions { ExceptionHandler = CustomHandler })
                         .Apply(remainingMiddleware));

            app.Map(
                ReExecuteHandlerPrefix,
                inner =>
                    inner.UseExceptionHandler("/")
                         .Apply(remainingMiddleware));

            app.Map(
                StatusCodeReExecutePrefix,
                inner =>
                    inner
                       .Use((context, next) =>
                        {
                            return next();
                        })
                       .UseStatusCodePagesWithReExecute("/")
                         .Apply(remainingMiddleware));

            app.Map(
                ExceptionPagePrefix,
                inner =>
                    inner.UseDeveloperExceptionPage()
                         .Apply(remainingMiddleware));

            remainingMiddleware(app);
            return app;
        }

        public static void AddErrorHandlerInline(IApplicationBuilder app, string path)
        {
            if (path.StartsWith(CustomHandlerPrefix))
            {
                app.UsePathBase(CustomHandlerPrefix);
                app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = CustomHandler });
            }
            else if (path.StartsWith(ReExecuteHandlerPrefix))
            {
                app.UsePathBase(ReExecuteHandlerPrefix);
                app.UseExceptionHandler("/");
            }
            else if (path.StartsWith(StatusCodeReExecutePrefix))
            {
                app.UsePathBase(StatusCodeReExecutePrefix);
                app.UseStatusCodePagesWithReExecute("/");
            }
            else if (path.StartsWith(ExceptionPagePrefix))
            {
                app.UsePathBase(ExceptionPagePrefix);
                // developer exception page added by default in .NET 6
            }
        }

        public static PathBaseCorrectorStartupFilter GetStartupFilter(string path)
        {
            return new[] { CustomHandlerPrefix, ReExecuteHandlerPrefix, StatusCodeReExecutePrefix, ExceptionPagePrefix }
                  .Where(x => path.StartsWith(x))
                  .Select(x => new PathBaseCorrectorStartupFilter(x))
                  .FirstOrDefault();
        }

        private static IApplicationBuilder Apply(
            this IApplicationBuilder app,
            Action<IApplicationBuilder> configure)
        {
            configure(app);
            return app;
        }

        private static async Task CustomHandler(HttpContext context)
        {
            var exceptionDetails = context.Features.Get<IExceptionHandlerFeature>();
            var ex = exceptionDetails?.Error;
            if (ex is InvalidOperationException)
            {
                throw new Exception("Rethrowing exception", ex);
            }

            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("An error occured in the app");
        }

        public class PathBaseCorrectorStartupFilter : IStartupFilter
        {
            private readonly string _pathBase;

            public PathBaseCorrectorStartupFilter(string pathBase)
            {
                _pathBase = pathBase;
            }

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.UsePathBase(_pathBase);
                    next(app);
                };
            }
        }
    }
}
#endif
