// <copyright file="LoggingConfigurationFileLoaderLoadInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;

/// <summary>
/// NLog.Config.LoggingConfiguration NLog.Config.LoggingConfigurationFileLoader::Load(NLog.LogFactory,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "NLog",
    TypeName = "NLog.Config.LoggingConfigurationFileLoader",
    MethodName = "Load",
    ReturnTypeName = "NLog.Config.LoggingConfiguration",
    ParameterTypeNames = ["NLog.LogFactory", ClrNames.String],
    MinimumVersion = "6.0.0",
    MaximumVersion = "6.*.*",
    IntegrationName = NLogConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LoggingConfigurationFileLoaderLoadInstrumentation
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LoggingConfigurationFileLoaderLoadInstrumentation));

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        // This method is called when configuration is first loaded
        // https://github.com/NLog/NLog/blob/7e8589d938878bd35fe50cc9f93e6e30ba511dcd/src/NLog/LogFactory.cs#L235
        // If there's no configuration, then it returns null, and ActivateLoggingConfiguration is never called.
        // Given that this is where we activate logs injection and add our direct log instrumentation target,
        // we need to make sure we don't ever return null from here
        //
        // The combination of this instrumentation and the ActivateLoggingConfiguration instrumentation in NLog 6.0
        // replaces the somewhat entangled implementation of the previous NLog versions encapsulated in
        // LogFactoryGetConfigurationForLoggerInstrumentation.
        if (returnValue is not null)
        {
            // nothing to do if we have a valid configuration
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        // If we aren't doing logs injection or direct log submission, we can return null, it doesn't matter
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.LogsInjectionEnabled &&
            !tracer.TracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog))
        {
            return new CallTargetReturn<TReturn?>(returnValue);
        }

        try
        {
            var loggingConfiguration = Activator.CreateInstance<TReturn>();
            return new CallTargetReturn<TReturn?>(loggingConfiguration);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create new instance of NLog's LoggingConfiguration");
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
