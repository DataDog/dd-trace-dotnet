// <copyright file="LogsTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal static class LogsTransportStrategy
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        public static IApiRequestFactory Get(DirectLogSubmissionSettings settings)
        {
#if NETCOREAPP
            Log.Information("Using {FactoryType} for log submission transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory();
#else
            Log.Information("Using {FactoryType} for log submission transport.", nameof(ApiWebRequestFactory));
            return new ApiWebRequestFactory();
#endif
        }
    }
}
