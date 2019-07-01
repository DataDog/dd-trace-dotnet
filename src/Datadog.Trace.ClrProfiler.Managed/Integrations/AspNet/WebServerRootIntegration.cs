using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration base for core web server integrations.
    /// </summary>
    public static class WebServerRootIntegration
    {
        internal const string HttpContextKey = "__Datadog.Trace.ClrProfiler.Integrations." + nameof(WebServerRootIntegration);
        private const string IntegrationName = "WebServerRoot";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(WebServerRootIntegration));

        /// <summary>
        /// Entry method for invoking the beginning of every web server request pipeline
        /// </summary>
        /// <param name="httpContext">Instance being instrumented.</param>
        /// <param name="features">Initialize features.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        [InterceptMethod(
            TargetAssembly = "Microsoft.AspNetCore.Http.Abstractions",
            TargetType = "Microsoft.AspNetCore.Http.DefaultHttpContext",
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore })]
        // [InterceptMethod(
        //    TargetAssembly = "Microsoft.AspNetCore.Http.Abstractions",
        //    TargetType = "Microsoft.AspNetCore.Http.HttpContext",
        //    TargetSignatureTypes = new[] { ClrNames.Ignore })]
        public static void Initialize(object httpContext, object features, int opCode, int mdToken)
        {
            // void Initialize(IFeatureCollection features)
            string methodDef = $"{httpContext?.GetType().FullName}.{nameof(Initialize)}()";
            MethodBase instrumentedMethod = null;

            try
            {
                instrumentedMethod = Assembly.GetCallingAssembly().ManifestModule.ResolveMethod(mdToken);
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error retrieving {methodDef}", ex);
                throw;
            }

            try
            {
                instrumentedMethod.Invoke(httpContext, new[] { features });
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error calling {methodDef}", ex);
                throw;
            }

            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                WebServerIntegrationContext.Initialize(httpContext);
            }
        }

        /// <summary>
        /// Entry method for invoking the incoming request pipeline for Microsoft.AspNetCore.Mvc.Core
        /// </summary>
        /// <param name="responseInstance">Instance being instrumented.</param>
        /// <param name="value">HttpContext.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        [InterceptMethod(
            TargetAssembly = "Microsoft.AspNetCore.Http.Abstractions",
            TargetType = "Microsoft.AspNetCore.Http.HttpResponse",
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Int32 },
            TargetMethod = "set_StatusCode")]
        public static void SetStatusCode(object responseInstance, int value, int opCode, int mdToken)
        {
            const string methodDef = "Microsoft.AspNetCore.Http.HttpResponse.set_StatusCode(int value)";
            MethodBase instrumentedMethod = null;

            try
            {
                instrumentedMethod = Assembly.GetCallingAssembly().ManifestModule.ResolveMethod(mdToken);
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error retrieving {methodDef}", ex);
                throw;
            }

            try
            {
                instrumentedMethod.Invoke(responseInstance, new object[] { value });
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error calling {methodDef}", ex);
                throw;
            }

            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled
                return;
            }

            try
            {
                if (responseInstance.TryGetPropertyValue("HttpContext", out object httpContext))
                {
                    var integration =
                        WebServerIntegrationContext.RetrieveFromHttpContext(httpContext);

                    integration?.SetStatusCode(value);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error setting status code for {nameof(WebServerIntegrationContext)}.", ex);
            }
        }
    }
}
