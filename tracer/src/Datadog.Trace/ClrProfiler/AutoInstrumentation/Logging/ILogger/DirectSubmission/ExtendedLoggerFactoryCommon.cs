// <copyright file="ExtendedLoggerFactoryCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

internal static class ExtendedLoggerFactoryCommon
{
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (!TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.ILogger))
        {
            return CallTargetReturn.GetDefault();
        }

        if (exception is not null)
        {
            // If there's an exception during the constructor, things aren't going to work anyway
            return CallTargetReturn.GetDefault();
        }

        var scopeProvider = state.State is { } rawScopeProvider
                                ? rawScopeProvider.DuckCast<IExternalScopeProvider>()
                                : instance.DuckCast<ExtendedLoggerFactoryProxy>().ScopeProvider;

        if (LoggerFactoryIntegrationCommon<TTarget>.TryAddDirectSubmissionLoggerProvider(instance, scopeProvider))
        {
            TracerManager.Instance.Telemetry.IntegrationGeneratedSpan(IntegrationId.ILogger);
        }

        return CallTargetReturn.GetDefault();
    }
}
