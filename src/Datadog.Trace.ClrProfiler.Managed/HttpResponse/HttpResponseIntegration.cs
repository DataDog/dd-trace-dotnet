using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for HttpResponse.
    /// </summary>
    public static class HttpResponseIntegration
    {
        private const string IntegrationName = "HttpResponse";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(HttpResponseIntegration));

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
                    var integration = AspNetCoreMvc2Integration.RetrieveFromHttpContext(httpContext);
                    integration?.SetStatusCode(value);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorExceptionForFilter($"Error setting status code on {nameof(AspNetCoreMvc2Integration)}.", ex);
            }
        }
    }
}
