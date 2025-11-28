// <copyright file="ReaderCloseNpgsqlIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Npgsql
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// [*]DataReader [Command].ExecuteReader()
    /// </summary>
    // special case for npgsql which expose a close with three params
    [InstrumentMethod(
        AssemblyName = "Npgsql",
        TypeName = "Npgsql.NpgsqlDataReader",
        MethodName = "Close",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { ClrNames.Bool, ClrNames.Bool, ClrNames.Bool, },
        MinimumVersion = "4.0.0",
        MaximumVersion = "8.*.*",
        IntegrationName = nameof(IntegrationId.Npgsql),
        InstrumentationCategory = InstrumentationCategory.Iast)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ReaderCloseNpgsqlIntegration
    {
        private static bool errorLogged = false;

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            try
            {
                if (instance is not null)
                {
                    IastModule.UnregisterDbRecord(instance);
                }
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    Log.Error(e, "Error unregistering db record from IAST module");
                    errorLogged = true;
                }
            }

            return returnValue;
        }
    }
}
