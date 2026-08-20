// <copyright file="OtelThreadContextPublisher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.LibDatadog.OtelThreadContext;

internal sealed class OtelThreadContextPublisher : IOtelThreadContextPublisher
{
    private const int SpanIdSize = sizeof(ulong);

    private static int _unsupportedLogWritten;

    private readonly IOtelThreadContextNativeMethods _nativeMethods;
    private int _disabled;

    internal OtelThreadContextPublisher(IOtelThreadContextNativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods;
    }

    public bool IsEnabled => Volatile.Read(ref _disabled) == 0;

    internal static IOtelThreadContextPublisher Disabled => NullOtelThreadContextPublisher.Instance;

    internal static IOtelThreadContextPublisher Create(TracerSettings settings)
    {
        if (!settings.OtelThreadContextEnabled)
        {
            return NullOtelThreadContextPublisher.Instance;
        }

        var framework = FrameworkDescription.Instance;
        var platformIsSupported =
            framework.OSPlatform == OSPlatformName.Linux &&
            Environment.Is64BitProcess &&
            framework.ProcessArchitecture is ProcessArchitecture.X64 or ProcessArchitecture.Arm64;
        var deploymentIsSupported = LibDatadogAvailabilityHelper.IsLibDatadogAvailable.IsAvailable;

        if (!platformIsSupported || !deploymentIsSupported)
        {
            if (Interlocked.Exchange(ref _unsupportedLogWritten, 1) == 0)
            {
                Logger.Instance.Information<bool, bool>(
                    "OpenTelemetry thread context publication was requested but is unavailable. Platform supported: {PlatformSupported}, deployment supported: {DeploymentSupported}",
                    platformIsSupported,
                    deploymentIsSupported);
            }

            return NullOtelThreadContextPublisher.Instance;
        }

        return new OtelThreadContextPublisher(OtelThreadContextNativeMethods.Instance);
    }

    internal static IOtelThreadContextPublisher Create(
        bool enabled,
        bool platformIsSupported,
        bool deploymentIsSupported,
        IOtelThreadContextNativeMethods nativeMethods)
    {
        return enabled && platformIsSupported && deploymentIsSupported
                   ? new OtelThreadContextPublisher(nativeMethods)
                   : NullOtelThreadContextPublisher.Instance;
    }

    public void Set(Span span)
    {
        if (!IsEnabled)
        {
            return;
        }

        Span<byte> traceId = stackalloc byte[TraceId.Size];
        Span<byte> spanId = stackalloc byte[SpanIdSize];
        Span<byte> localRootSpanId = stackalloc byte[SpanIdSize];

        BinaryPrimitives.WriteUInt64BigEndian(traceId, span.TraceId128.Upper);
        BinaryPrimitives.WriteUInt64BigEndian(traceId.Slice(SpanIdSize), span.TraceId128.Lower);
        BinaryPrimitives.WriteUInt64BigEndian(spanId, span.SpanId);
        BinaryPrimitives.WriteUInt64BigEndian(localRootSpanId, span.RootSpanId);

        try
        {
            _nativeMethods.Update(traceId, spanId, localRootSpanId);
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    public void Reset()
    {
        if (!IsEnabled)
        {
            return;
        }

        Span<byte> traceId = stackalloc byte[TraceId.Size];
        traceId.Clear();
        var spanId = traceId.Slice(0, SpanIdSize);

        try
        {
            _nativeMethods.Update(traceId, spanId, spanId);
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    private void Disable(Exception exception)
    {
        if (Interlocked.Exchange(ref _disabled, 1) == 0)
        {
            Logger.Instance.Warning(exception, "Unable to publish OpenTelemetry thread context. Publication is now disabled.");
        }
    }

    private sealed class Logger
    {
        public static readonly IDatadogLogger Instance = DatadogLogging.GetLoggerFor<Logger>();

        private Logger()
        {
        }
    }

    private sealed class NullOtelThreadContextPublisher : IOtelThreadContextPublisher
    {
        public static readonly NullOtelThreadContextPublisher Instance = new();

        public bool IsEnabled => false;

        public void Set(Span span)
        {
        }

        public void Reset()
        {
        }
    }
}
