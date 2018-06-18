using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;

[assembly: System.Security.SecurityCritical]
[assembly: System.Security.AllowPartiallyTrustedCallers]

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides instrumentation probes that can be injected into profiled code.
    /// </summary>
    public static class Instrumentation
    {
        private static readonly ConcurrentDictionary<string, MetadataNames> MetadataLookup = new ConcurrentDictionary<string, MetadataNames>();

        /// <summary>
        /// Called after an instrumented method is entered.
        /// </summary>
        /// <param name="integrationTypeValue">A <see cref="IntegrationType"/> tht indicated which integration is instrumenting this method.</param>
        /// <param name="moduleId">The id of the module where the instrumented method is defined.</param>
        /// <param name="methodToken">The <c>mdMemberDef</c> token of the instrumented method.</param>
        /// <param name="args">An array with all the argumetns that were passed into the instrumented method. If it is an instance method, the first arguments is <c>this</c>.</param>
        /// <returns>A <see cref="Scope"/> created to instrument the method.</returns>
        [System.Security.SecuritySafeCritical]
        public static object OnMethodEntered(int integrationTypeValue,
                                             ulong moduleId,
                                             uint methodToken,
                                             object[] args)
        {
            if (!IsProfilingEnabled())
            {
                return null;
            }

            // TODO: check if this integration type is enabled
            var integrationType = (IntegrationType)integrationTypeValue;

            MetadataNames metadataNames = MetadataLookup.GetOrAdd($"{moduleId}:{methodToken}",
                                                                  key => GetMetadataNames((IntPtr)moduleId, methodToken));

            // TODO: explicitly set upstream Scope as parent for this new Scope, but Span.Context is currently internal
            Scope scope = Tracer.Instance.StartActive("");
            Span span = scope.Span;

            // TODO: make integrations more modular in the C# side
            switch (integrationType)
            {
                case IntegrationType.Custom:
                    string operationName = $"{metadataNames.TypeName}.{metadataNames.MethodName}";
                    span.OperationName = operationName;
                    span.ResourceName = "";
                    Console.WriteLine($"Entering {operationName}()");

                    break;
                case IntegrationType.AspNetMvc5:
                    if (args == null || args.Length != 3)
                    {
                        break;
                    }

                    // [System.Web.Mvc]System.Web.Mvc.ControllerContext
                    dynamic controllerContext = args[1];

                    HttpContextBase httpContext = controllerContext.HttpContext;
                    string httpMethod = httpContext.Request.HttpMethod.ToUpperInvariant();

                    string routeTemplate = controllerContext.RouteData.Route.Url;
                    IDictionary<string, object> routeValues = controllerContext.RouteData.Values;
                    var resourceName = new StringBuilder(routeTemplate);

                    // replace all route values except "id"
                    // TODO: make this filter configurable
                    foreach (var routeValue in routeValues.Where(p => !string.Equals(p.Key, "id", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        string key = $"{{{routeValue.Key.ToLowerInvariant()}}}";
                        string value = routeValue.Value.ToString().ToLowerInvariant();
                        resourceName.Replace(key, value);
                    }

                    span.ResourceName = string.Join(" ", httpMethod, resourceName.ToString());
                    span.OperationName = "web.request";
                    span.Type = "web";
                    span.SetTag("http.method", httpMethod);
                    span.SetTag("http.url", httpContext.Request.RawUrl.ToLowerInvariant());
                    span.SetTag("http.route", routeTemplate);

                    // TODO: get response code from httpContext.Response.StatusCode
                    break;

                default:
                    // invalid integration type
                    // TODO: log this
                    break;
            }

            // the return value will be left on the stack for the duration
            // of the instrumented method and passed into OnMethodExit()
            return scope;
        }

        /// <summary>
        /// Called before an instrumented method exits.
        /// </summary>
        /// <param name="args">The <see cref="Scope"/> that was created by <see cref="OnMethodEntered"/>.</param>
        [System.Security.SecuritySafeCritical]
        public static void OnMethodExit(object args)
        {
            var scope = args as Scope;
            scope?.Close();
        }

        /// <summary>
        /// Called before an instrumented method exits.
        /// </summary>
        /// <param name="args">The <see cref="Scope"/> that was created by <see cref="OnMethodEntered"/>.</param>
        /// <param name="originalReturnValue">The value returned by the instrumented method.</param>
        /// <returns>Returns the value that was originally returned by the instrumented method.</returns>
        [System.Security.SecuritySafeCritical]
        public static object OnMethodExit(object args, object originalReturnValue)
        {
            OnMethodExit(args);
            return originalReturnValue;
        }

        /// <summary>
        /// Determines whether tracing with Datadog's profiler is enabled.
        /// </summary>
        /// <returns><c>true</c> if profiling is enabled; <c>false</c> otherwise.</returns>
        public static bool IsProfilingEnabled()
        {
            string setting = ConfigurationManager.AppSettings["Datadog.Tracing:Enabled"];
            return !string.Equals(setting, bool.FalseString, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Determines whether Datadog's profiler is currently attached.
        /// </summary>
        /// <returns><c>true</c> if the profiler is currentl attached; <c>false</c> otherwise.</returns>
        public static bool IsProfilerAttached()
        {
            return NativeMethods.IsProfilerAttached();
        }

        private static MetadataNames GetMetadataNames(IntPtr moduleId, uint methodToken)
        {
            var modulePathBuffer = new StringBuilder(512);
            var typeNameBuffer = new StringBuilder(256);
            var methodNameBuffer = new StringBuilder(256);

            NativeMethods.GetMetadataNames(moduleId,
                                           methodToken,
                                           modulePathBuffer,
                                           (ulong)modulePathBuffer.Capacity,
                                           typeNameBuffer,
                                           (ulong)typeNameBuffer.Capacity,
                                           methodNameBuffer,
                                           (ulong)methodNameBuffer.Capacity);

            string module = System.IO.Path.GetFileName(modulePathBuffer.ToString());
            string type = typeNameBuffer.ToString();
            string method = methodNameBuffer.ToString();
            return new MetadataNames(module, type, method);
        }
    }
}
