#if !NETSTANDARD2_0
using System;
using System.Web;
using Datadog.Trace.AspNet;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// Instrumentation wrapper for AspNet
    /// </summary>
    public static class AspNetIntegration
    {
        private const string IntegrationName = "AspNet";
        private const string OperationName = "aspnet.request";
        private const string MinimumVersion = "4.0";
        private const string MaximumVersion = "4";

        private const string AssemblyName = "System.Web";
        private const string BuildManagerTypeName = "System.Web.Compilation.BuildManager";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AspNetIntegration));

        /// <summary>
        /// Wrapper method used to instrument System.Web.Compilation.BuildManager.InvokePreStartInitMethods
        /// </summary>
        /// <param name="methodInfoCollection">A collection of <see cref="System.Reflection.MethodInfo">objects.</see>/></param>
        /// <param name="func">A parameter-less function that returns an <see cref="System.IDisposable">IDisposable</see>/> object.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            CallerAssembly = AssemblyName,
            TargetAssembly = AssemblyName,
            TargetType = BuildManagerTypeName,
            TargetMethod = "InvokePreStartInitMethodsCore",
            TargetSignatureTypes = new[] { ClrNames.Void, "System.Collections.Generic.ICollection`1<System.Reflection.MethodInfo>", "System.Func`1<System.IDisposable>" },
            TargetMinimumVersion = MinimumVersion,
            TargetMaximumVersion = MaximumVersion)]
        public static void InvokePreStartInitMethodsCore(
            object methodInfoCollection,
            object func,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            // The whole point of instrumenting a method so early on in the application load process
            // is to register our HttpModule.
            HttpApplication.RegisterModule(typeof(TracingHttpModule));

            Action<object, object> instrumentedMethod;
            Type concreteType = null;

            try
            {
                var module = ModuleLookup.GetByPointer(moduleVersionPtr);
                concreteType = module.GetType(BuildManagerTypeName);
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: BuildManagerTypeName,
                    methodName: nameof(InvokePreStartInitMethodsCore),
                    instanceType: null,
                    relevantArguments: new[] { concreteType?.AssemblyQualifiedName });
                throw;
            }

            try
            {
                instrumentedMethod =
                    MethodBuilder<Action<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(InvokePreStartInitMethodsCore))
                       .WithParameters(methodInfoCollection, func)
                       .WithConcreteType(concreteType)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    null,
                    methodName: nameof(InvokePreStartInitMethodsCore));
                throw;
            }

            instrumentedMethod(methodInfoCollection, func);
        }
    }
}
#endif
