// <copyright file="AspNetThreadContextIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// An ASP.NET integration to ensure that the scope initialized by the HttpModule
    /// is stored in the execution context.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AspNetThreadContextIntegration
    {
        internal const string IntegrationName = nameof(IntegrationIds.AspNet);

        private const string SystemWebAssembly = "System.Web";
        private const string ThreadContextTypeName = "System.Web.ThreadContext";
        private const string HttpContextScopeKey = "__Datadog.Trace.AspNet.TracingHttpModule-aspnet.request";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AspNetThreadContextIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="threadContext">The ThreadContext instance we are replacing.</param>
        /// <param name="setImpersonationContext">The setImpersonationContext flag.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = SystemWebAssembly,
            TargetAssembly = SystemWebAssembly,
            TargetType = ThreadContextTypeName,
            TargetMethod = "AssociateWithCurrentThread",
            TargetSignatureTypes = new[] { ClrNames.Void, ClrNames.Bool },
            TargetMinimumVersion = "4",
            TargetMaximumVersion = "4")]
        public static void AssociateWithCurrentThread(
            object threadContext,
            bool setImpersonationContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (threadContext == null) { throw new ArgumentNullException(nameof(threadContext)); }

            const string methodName = nameof(AssociateWithCurrentThread);
            Action<object, bool> associateWithCurrentThread;
            var threadContextType = threadContext.GetType();

            try
            {
                associateWithCurrentThread =
                    MethodBuilder<Action<object, bool>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(threadContextType)
                       .WithParameters(setImpersonationContext)
                       .WithNamespaceAndNameFilters(ClrNames.Void, ClrNames.Bool)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ThreadContextTypeName,
                    methodName: methodName,
                    instanceType: threadContextType.AssemblyQualifiedName);
                throw;
            }

            if (threadContext.TryDuckCast<ThreadContextStruct>(out var threadContextStruct))
            {
                var httpContext = threadContextStruct.HttpContext;
                if (httpContext.Items[HttpContextScopeKey] is Scope scope && ((IDatadogTracer)Tracer.Instance).ScopeManager is IScopeRawAccess rawAccess)
                {
                    rawAccess.Active = scope;
                }
            }

            associateWithCurrentThread(threadContext, setImpersonationContext);
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="threadContext">The ThreadContext instance we are replacing.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        [InterceptMethod(
            Integration = IntegrationName,
            CallerAssembly = SystemWebAssembly,
            TargetAssembly = SystemWebAssembly,
            TargetType = ThreadContextTypeName,
            TargetMethod = "DisassociateFromCurrentThread",
            TargetSignatureTypes = new[] { ClrNames.Void },
            TargetMinimumVersion = "4",
            TargetMaximumVersion = "4")]
        public static void DisassociateFromCurrentThread(
            object threadContext,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (threadContext == null) { throw new ArgumentNullException(nameof(threadContext)); }

            const string methodName = nameof(DisassociateFromCurrentThread);
            Action<object> disassociateFromCurrentThread;
            var threadContextType = threadContext.GetType();

            try
            {
                disassociateFromCurrentThread =
                    MethodBuilder<Action<object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(threadContextType)
                       .WithNamespaceAndNameFilters(ClrNames.Void)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: ThreadContextTypeName,
                    methodName: methodName,
                    instanceType: threadContextType.AssemblyQualifiedName);
                throw;
            }

            if (((IDatadogTracer)Tracer.Instance).ScopeManager is IScopeRawAccess rawAccess)
            {
                rawAccess.Active = null;
            }

            disassociateFromCurrentThread(threadContext);
        }

        /*
         * Ducktyping types
         */

        /// <summary>
        /// ThreadContext struct for duck typing
        /// </summary>
        [DuckCopy]
        public struct ThreadContextStruct
        {
            /// <summary>
            /// HttpContext
            /// </summary>
            public HttpContextStruct HttpContext;
        }

        /// <summary>
        /// HttpContext struct for duck typing
        /// </summary>
        [DuckCopy]
        public struct HttpContextStruct
        {
            /// <summary>
            /// Items dictionary
            /// </summary>
            public IDictionary Items;
        }
    }
}
