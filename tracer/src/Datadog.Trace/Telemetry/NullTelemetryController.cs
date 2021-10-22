// <copyright file="NullTelemetryController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Telemetry
{
    internal class NullTelemetryController : ITelemetryController
    {
        public static readonly NullTelemetryController Instance = new();

        public void IntegrationRunning(IntegrationId integrationId)
        {
        }

        public void IntegrationGeneratedSpan(IntegrationId integrationId)
        {
        }

        public void IntegrationDisabledDueToError(IntegrationId integrationId, string error)
        {
        }

        public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName, AzureAppServices appServicesMetadata)
        {
        }

        public void RecordSecuritySettings(SecuritySettings settings)
        {
        }
    }
}
