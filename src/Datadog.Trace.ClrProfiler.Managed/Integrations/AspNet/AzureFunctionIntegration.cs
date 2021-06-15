using System;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// The Azure Function integration.
    /// </summary>
    public static class AzureFunctionIntegration
    {
        private const string OperationName = "azure-function.invoke";
        private const string MinimumVersion = "3";
        private const string MaximumVersion = "3";

        private const string AssemblyName = "Microsoft.Azure.WebJobs.Host";
        private const string FunctionInvokerType = "Microsoft.Azure.WebJobs.Host.Executors.IFunctionInvoker";
        private const string MethodName = "InvokeAsync";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AzureFunction));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureFunctionIntegration));

        private static Scope CreateScope(Tracer tracer)
        {
            Scope scope = null;

            try
            {
                if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                ITags tags = null;
                scope = tracer.StartActiveWithTags(operationName: OperationName, parent: null, tags: tags);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Instrumentation wrapper for FunctionInvocationMiddleware.Invoke(HttpContext context).
        /// </summary>
        /// <param name="instance">FunctionInvocationMiddleware instance.</param>
        /// <param name="instanceParam">Parameter 1.</param>
        /// <param name="argumentsParam">Parameter 2.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AssemblyName },
            TargetMethod = MethodName,
            TargetType = FunctionInvokerType,
            TargetSignatureTypes = new[] { ClrNames.ObjectTask, ClrNames.Object, ClrNames.ObjectArray },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static object InvokeAsync(
            object instance,
            object instanceParam,
            object[] argumentsParam,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (instance == null) { throw new ArgumentNullException(nameof(instance)); }

            var instanceType = instance.GetType();
            var declaringTypeGenerics = instanceType.GetGenericArguments();
            Func<object, object, Task<object>> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, Task<object>>>
                       .Start(moduleVersionPtr, mdToken, opCode, MethodName)
                       .WithConcreteType(instanceType)
                       .WithDeclaringTypeGenerics(declaringTypeGenerics)
                       .WithParameters(instanceParam, argumentsParam)
                       .WithReturnType(typeof(Task<object>))
                       .WithNamespaceAndNameFilters(ClrNames.ObjectTask, ClrNames.Object, ClrNames.ObjectArray)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: FunctionInvokerType,
                    methodName: MethodName,
                    instanceType: instance.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = CreateScope(Tracer.Instance))
            {
                try
                {
                    return instrumentedMethod(instanceParam, argumentsParam);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
