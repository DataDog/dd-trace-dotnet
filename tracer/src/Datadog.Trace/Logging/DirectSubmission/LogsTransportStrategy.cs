// <copyright file="LogsTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging.DirectSubmission.Sink;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal static class LogsTransportStrategy
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogsTransportStrategy));

        public static IApiRequestFactory Get(ImmutableDirectLogSubmissionSettings settings)
        {
            // Still quite a long time, but we could be sending a lot of data
            var timeout = TimeSpan.FromSeconds(15);

#if NETCOREAPP
            Log.Information("Using {FactoryType} for log submission transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory(settings.IntakeUrl, LogsApiHeaderNames.DefaultHeaders, timeout: timeout);
#else
            Log.Information("Using {FactoryType} for log submission transport.", nameof(ApiWebRequestFactory));
            return new ApiWebRequestFactory(settings.IntakeUrl, LogsApiHeaderNames.DefaultHeaders, timeout: timeout);
#endif
        }
    }
}
