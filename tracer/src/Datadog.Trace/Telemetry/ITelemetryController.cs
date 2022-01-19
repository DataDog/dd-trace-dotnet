// <copyright file="ITelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry
{
    internal interface ITelemetryController : IDisposable
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
        void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName, AzureAppServices appServicesMetadata);

        /// <summary>
        /// Called when app sec security is enabled to record the security settings
        /// </summary>
        public void RecordSecuritySettings(SecuritySettings settings);

        /// <summary>
        /// Dispose resources for sending telemetry
        /// </summary>
        /// <param name="sendAppClosingTelemetry">True if the controller should send "app closing" telemetry before disposing</param>
        public void Dispose(bool sendAppClosingTelemetry);

        /// <summary>
        /// Indicates the
        /// </summary>
        void Start();
    }
}
