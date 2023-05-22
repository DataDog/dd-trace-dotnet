// <copyright file="ITelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast.Settings;

namespace Datadog.Trace.Telemetry
{
    internal interface ITelemetryController
    {
        /// <summary>
        /// Should be called when an integration is first executed (not necessarily successfully)
        /// </summary>
        void IntegrationRunning(IntegrationId integrationId);

        /// <summary>
        /// Should be called when an integration successfully generates a span
        /// </summary>
        void IntegrationGeneratedSpan(IntegrationId integrationId);

        /// <summary>
        /// Should be called when an integration is disabled for some reason.
        /// </summary>
        void IntegrationDisabledDueToError(IntegrationId integrationId, string error);

        /// <summary>
        /// Called when a tracer is initialized to record the tracer's settings
        /// Only the first tracer registered is recorded
        /// </summary>
        void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName);

        /// <summary>
        /// Called when app sec security is enabled to record the security settings
        /// </summary>
        public void RecordSecuritySettings(SecuritySettings settings);

        /// <summary>
        /// Called when IAST security is enabled to record the IAST settings
        /// </summary>
        public void RecordIastSettings(IastSettings settings);

        /// <summary>
        /// Called to record profiler-related telemetry
        /// </summary>
        public void RecordProfilerSettings(Profiler profiler);

        /// <summary>
        /// Dispose resources for sending telemetry
        /// </summary>
        /// <param name="sendAppClosingTelemetry">True if the controller should send "app closing" telemetry before disposing</param>
        public Task DisposeAsync(bool sendAppClosingTelemetry);

        /// <summary>
        /// Dispose resources for sending telemetry
        /// </summary>
        public Task DisposeAsync();

        /// <summary>
        /// Indicates the controller can start sending telemetry
        /// </summary>
        void Start();

        /// <summary>
        /// Should be called when the status (enabled/disabled) of a product (ASM/Profiler) changed.
        /// Only used in Telemetry V2
        /// </summary>
        void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error);
    }
}
