// <copyright file="SetUserIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Manual
{
    /// <summary>
    /// LoggerFactoryScopeProvider.ForEach&lt;TState&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Datadog.DiagnosticSource",
        TypeName = "Datadog.DiagnosticSource.ActivityExtensions",
        MethodName = "SetUser",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Diagnostics.Activity", ClrNames.String, ClrNames.Bool, ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String, ClrNames.String, },
        MinimumVersion = "1.*.*",
        MaximumVersion = "1.*.*",
        IntegrationName = WebRequestCommon.IntegrationName)]
    public class SetUserIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TActivity">Type of the activity</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="activity">The Activity value.</param>
        /// <param name="id">The unique identifier associated with the users</param>
        /// <param name="propagateId">Gets or sets a value indicating whether the Id field should be propagated to other services called.</param>
        /// <param name="email">Gets or sets the user's email address</param>
        /// <param name="name">Gets or sets the user's name as displayed in the UI</param>
        /// <param name="sessionId">Gets or sets the user's session unique identifier</param>
        /// <param name="role">Gets or sets the role associated with the user</param>
        /// <param name="scope">Gets or sets the scopes or granted authorities the client currently possesses extracted from token or application security context</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TActivity>(TTarget instance, TActivity activity, string id, bool propagateId, string email, string name, string sessionId, string role, string scope)
            where TActivity : IActivity
        {
            if (DefaultActivityHandler.ActivityMappingById.TryGetValue(activity.Id, out DefaultActivityHandler.ActivityMapping value))
            {
                var userDetails = new UserDetails(id)
                {
                    PropagateId = propagateId,
                    Email = email,
                    Name = name,
                    SessionId = sessionId,
                    Role = role,
                    Scope = scope
                };
                value.Scope.Span.SetUser(userDetails);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            return CallTargetReturn.GetDefault();
        }
    }
}
