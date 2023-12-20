// <copyright file="ExtendedLoggerFactoryProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

/// <summary>
/// Duck type for https://github.com/dotnet/extensions/blob/e7430144e8009f87ed510e7922c8c780fbb0d9ac/src/Libraries/Microsoft.Extensions.Telemetry/Logging/ExtendedLoggerFactory.cs
/// </summary>
[DuckCopy]
internal struct ExtendedLoggerFactoryProxy
{
    [DuckField(Name = "_scopeProvider")]
    public IExternalScopeProvider? ScopeProvider;
}
