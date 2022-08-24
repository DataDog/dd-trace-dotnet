// <copyright file="ProcessStartIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    /// <summary>
    /// System.Net.Http.HttpClientHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
   AssemblyName = "System.Diagnostics.Process",
   TypeName = "System.Diagnostics.Process",
   MethodName = "Start",
   ReturnTypeName = ClrNames.Process,
   ParameterTypeNames = new[] { ClrNames.String },
   MinimumVersion = "1.0.0",
   MaximumVersion = "7.*.*",
   IntegrationName = nameof(Configuration.IntegrationId.Process))]
    public class ProcessStartIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="filename">file name</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(ref string filename)
        {
            return CallTargetState.GetDefault();
        }
    }
}
