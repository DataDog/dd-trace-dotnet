// <copyright file="ReaderWriterLock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Concurrency;

internal sealed partial class ReaderWriterLock : IDisposable
{
    private const int TimeoutInMs = 4000;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ReaderWriterLock>();
}
