// <copyright file="DefaultInterpolatedStringHandlerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
/*
namespace Datadog.Trace.Iast.Aspects.System.Runtime
{
    /// <summary>
    /// MongoDB.Driver.Core.WireProtocol.IWireProtocol&lt;TResult&gt; instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime",
        IntegrationName = nameof(IntegrationId.SystemRandom),
        MinimumVersion = MongoDbIntegration.Major2Minor1,
        MaximumVersion = "9.*.*",
        MethodName = "AppendFormatted",
        ParameterTypeNames = new[] { ClrNames.String },
        ReturnTypeName = ClrNames.Void,
        TypeName = "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler")]
    [InstrumentMethod(
        AssemblyName = "System.Runtime",
        IntegrationName = nameof(IntegrationId.SystemRandom),
        MinimumVersion = MongoDbIntegration.Major2Minor1,
        MaximumVersion = "9.*.*",
        MethodName = "AppendFormatted",
        ParameterTypeNames = new[] { ClrNames.String },
        ReturnTypeName = ClrNames.Void,
        TypeName = "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler&")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DefaultInterpolatedStringHandlerIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="value"> value </param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string value)
        {
            return CallTargetState.GetDefault();
        }
    }
}
*/
#endif
