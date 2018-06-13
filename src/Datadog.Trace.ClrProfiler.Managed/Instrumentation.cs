using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web;

[assembly: System.Security.SecurityCritical]
[assembly: System.Security.AllowPartiallyTrustedCallers]

namespace Datadog.Trace.ClrProfiler
{
    public static class Instrumentation
    {
        private static readonly ConcurrentDictionary<string, MetadataNames> MetadataLookup = new ConcurrentDictionary<string, MetadataNames>();

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
                    if (args == null || args.Length != 2)
                    {
                        break;
                    }

                    // [System.Web.Mvc]System.Web.Mvc.ControllerContext
                    dynamic controllerContext = args[0];

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

        [System.Security.SecuritySafeCritical]
        public static void OnMethodExit(object args)
        {
            var scope = args as Scope;
            scope?.Close();
        }

        [System.Security.SecuritySafeCritical]
        public static object OnMethodExit(object args, object originalReturnValue)
        {
            OnMethodExit(args);
            return originalReturnValue;
        }

        private static bool IsProfilingEnabled()
        {
            string setting = ConfigurationManager.AppSettings["Datadog.Tracing:Enabled"];
            return !string.Equals(setting, bool.FalseString, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool IsProfilerAttached()
        {
            return NativeMethods.IsProfilerAttached();
        }

        public static Dictionary<string, string> GetProfilerSettings()
        {
            var values = new Dictionary<string, string>();
            string[] environmentVariables =
            {
                "COR_ENABLE_PROFILING",
                "COR_PROFILER",
                "COR_PROFILER_PATH",
                "DATADOG_PROFILE_PROCESSES"
            };

            foreach (string name in environmentVariables)
            {
                values[name] = Environment.GetEnvironmentVariable(name);
            }

            values["Module"] = Process.GetCurrentProcess().MainModule.ModuleName;
            values["Is64BitProcess"] = Environment.Is64BitProcess.ToString();
            values["Datadog.Tracing:Enabled"] = IsProfilingEnabled().ToString();
            // values["IsProfilerAttached"] = IsProfilerAttached().ToString();

            return values;
        }
    }
}
