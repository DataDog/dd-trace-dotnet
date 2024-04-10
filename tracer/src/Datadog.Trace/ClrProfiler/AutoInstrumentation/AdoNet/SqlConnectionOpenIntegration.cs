// <copyright file="SqlConnectionOpenIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// System.Void System.Data.SqlConnection::Open() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Data",
        TypeName = "System.Data.SqlClient.SqlConnection",
        MethodName = "Open",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = [],
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = nameof(IntegrationId.SqlClient))
        ]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SqlConnectionOpenIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            var tracer = Tracer.Instance;

            // TODO gate for ID and env var

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.SqlClient) || !tracer.Settings.IsIntegrationEnabled(IntegrationId.AdoNet))
            {
                // integration disabled, don't create a scope, skip this span
                return CallTargetState.GetDefault();
            }

            // TODO temporarty feature flag - if not enabled then don't create this.
            if (!tracer.Settings.ExperimentalSqlClientOpenEnabled)
            {
                return CallTargetState.GetDefault();
            }

            string operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DbType.SqlServer);
            string serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DbType.SqlServer);

            var scope = tracer.StartActiveInternal(operationName, serviceName: serviceName, tags: tracer.CurrentTraceSettings.Schema.Database.CreateSqlTags());
            scope.Span.Type = SpanTypes.Db;
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.SqlClient);
            return new CallTargetState(scope);
        }

        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
