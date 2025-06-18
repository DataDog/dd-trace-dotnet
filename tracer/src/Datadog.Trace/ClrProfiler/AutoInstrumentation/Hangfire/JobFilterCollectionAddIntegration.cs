// <copyright file="JobFilterCollectionAddIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// System.Void Hangfire.Common.JobFilterCollection::Add(System.Object) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Hangfire.Core",
    TypeName = "Hangfire.Common.JobFilterCollection",
    MethodName = "Add",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [ClrNames.Object],
    MinimumVersion = "1.8.18",
    MaximumVersion = "1.*.*",
    IntegrationName = nameof(IntegrationId.Hangfire))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class JobFilterCollectionAddIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(JobFilterCollectionAddIntegration));
    private static readonly object _registrationLock = new object();
    private static bool _loadedClientFilter = false;
    private static bool _loadedServerFilter = false;
    private static volatile bool _filtersRegistered = false;

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref object? filter)
        where TTarget : IJobFilterCollectionProxy
    {
        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IJobFilterCollectionProxy
    {
        if (!Tracer.Instance.Settings.IsIntegrationEnabled(HangfireCommon.IntegrationId))
        {
            // integration disabled, skip injection of datadog job filter
            return CallTargetReturn.GetDefault();
        }

        if (!_loadedServerFilter)
        {
            // Try to find the Hangfire.Core assembly
            Assembly? hangfireAssembly = AppDomain.CurrentDomain
                                                  .GetAssemblies()
                                                  .FirstOrDefault(asm => asm.GetName().Name == "Hangfire.Core");

            if (hangfireAssembly == null)
            {
                throw new InvalidOperationException("Hangfire.Core assembly not loaded.");
            }

            Type? saferServerFilterType = hangfireAssembly.GetType("Hangfire.Server.IServerFilter");

            Log.Debug("Datadog Jobfilter is not added in yet, attempting to do so.");
            // Type? serverFilterType = Type.GetType("Hangfire.Server.IServerFilter, Hangfire.Core");
            if (saferServerFilterType != null)
            {
                Log.Debug("Registering filter for {FilterType}", saferServerFilterType.ToString());
                object serverFilter = DuckType.CreateReverse(saferServerFilterType, new DatadogHangfireServerFilter());
                Log.Debug("This is the ducktype using create reverse: {Proxy}", serverFilter.ToString());
                instance.AddInternal(serverFilter, null);
                Log.Debug("We added the serverFilter!");
                _loadedServerFilter = true;
            }
            else
            {
                Log.Debug("iserverfilter is null");
            }
        }

        if (!_loadedClientFilter)
        {
            Type? clientFilterType = Type.GetType("Hangfire.Client.IClientFilter, Hangfire.Core");
            if (clientFilterType != null)
            {
                Log.Debug("Registering filter for {FilterType}", clientFilterType.ToString());
                object clientFilter = DuckType.CreateReverse(clientFilterType, new DatadogHangfireClientFilter());
                instance.AddInternal(clientFilter, null);
                Log.Debug("We added the clientFilter!");
                _loadedClientFilter = true;
            }
            else
            {
                Log.Debug("iclientfilter is null");
            }
        }

        return CallTargetReturn.GetDefault();
    }
}
