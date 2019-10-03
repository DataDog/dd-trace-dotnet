using System;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration ambient base for web server integrations.
    /// </summary>
    public static class HttpContextIntegration
    {
        private const string IntegrationName = "HttpContext";
        private const string DefaultHttpContextTypeName = "Microsoft.AspNetCore.Http.DefaultHttpContext";
        private static readonly Vendoring.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(HttpContextIntegration));

        /// <summary>
        /// Entry method for invoking the beginning of every web server request pipeline
        /// </summary>
        /// <param name="httpContext">Instance being instrumented.</param>
        /// <param name="features">Initialize features.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        // [InterceptMethod(
        //     TargetAssembly = "Microsoft.AspNetCore.Http.Abstractions",
        //     TargetType = DefaultHttpContextTypeName,
        //     TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Ignore })]
        // ***************************************************************
        //  DISABLED UNTIL WE FIX SCOPING ISSUES AT HTTP CONTEXT LEVEL
        // ***************************************************************
        public static void Initialize(object httpContext, object features, int opCode, int mdToken, long moduleVersionPtr)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var httpContextType = httpContext.GetInstrumentedType(DefaultHttpContextTypeName);

            Action<object, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(Initialize))
                       .WithConcreteType(httpContextType)
                       .WithParameters(features)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DefaultHttpContextTypeName,
                    methodName: nameof(Initialize),
                    instanceType: httpContext.GetType().AssemblyQualifiedName);
                throw;
            }

            try
            {
                instrumentedMethod.Invoke(httpContext, features);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error calling {DefaultHttpContextTypeName}.{nameof(Initialize)}(...)");
                throw;
            }

            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                AspNetAmbientContext.Initialize(httpContext);
            }
        }
    }
}
