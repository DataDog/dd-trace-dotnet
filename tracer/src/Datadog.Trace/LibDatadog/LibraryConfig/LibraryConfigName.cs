// <copyright file="LibraryConfigName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.LibDatadog.LibraryConfig;

internal enum LibraryConfigName
{
    DD_APM_TRACING_ENABLED,
    DD_RUNTIME_METRICS_ENABLED,
    DD_LOGS_INJECTION,
    DD_PROFILING_ENABLED,
    DD_DATA_STREAMS_ENABLED,
    DD_APPSEC_ENABLED,
    DD_IAST_ENABLED,
    DD_DYNAMIC_INSTRUMENTATION_ENABLED,
    DD_DATA_JOBS_ENABLED,
    DD_APPSEC_SCA_ENABLED,
    DD_TRACE_DEBUG,
    DD_SERVICE,
    DD_ENV,
    DD_VERSION,
}

internal enum LibraryConfigSource
{
    DDOG_LIBRARY_CONFIG_SOURCE_LOCAL_STABLE_CONFIG = 0,
    DDOG_LIBRARY_CONFIG_SOURCE_FLEET_STABLE_CONFIG = 1,
}
