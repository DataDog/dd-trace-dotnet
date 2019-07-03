using System;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration ambient base for web server integrations.
    /// </summary>
    public static class HttpContextIntegration
    {
        private const string IntegrationName = "HttpContext";
        private static readonly ILog Log = LogProvider.GetLogger(typeof(HttpContextIntegration));

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
        public static void Initialize(object httpContext, object features, int opCode, int mdToken)
        {
            var httpContextType = httpContext.GetType();
            string methodDef = $"{httpContextType.FullName}.Initialize(IFeatureCollection features)";

            Action<object, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, object>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode)
                       .WithConcreteType(httpContextType)
                       .WithParameters(features)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error retrieving {methodDef}", ex);
                throw;
            }

            try
            {
                instrumentedMethod.Invoke(httpContext, features);
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error calling {methodDef}", ex);
                throw;
            }

            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                AspNetAmbientContext.Initialize(httpContext);
            }
        }
    }
}
