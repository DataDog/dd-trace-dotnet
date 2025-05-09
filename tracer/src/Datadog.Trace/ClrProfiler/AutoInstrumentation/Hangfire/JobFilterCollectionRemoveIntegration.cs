// <copyright file="JobFilterCollectionRemoveIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// System.Void Hangfire.Common.JobFilterCollection::Remove[T]() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Hangfire.Core",
    TypeName = "Hangfire.Common.JobFilterCollection",
    MethodName = "Remove",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "1.8.18",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Hangfire))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class JobFilterCollectionRemoveIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        where TTarget : IJobFilterCollectionProxy
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IJobFilterCollectionProxy
    {
        return CallTargetReturn.GetDefault();
    }
}
