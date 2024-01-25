// <copyright file="IntegrationTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry
{
    internal class IntegrationTelemetryCollector
    {
        private readonly IntegrationDetail[] _integrationsById;
        private readonly IntegrationTelemetryData[] _previousValues;

        private int _hasChangesFlag = 0;
        private bool _hasSentFirstValues = false;

        public IntegrationTelemetryCollector()
        {
            _integrationsById = new IntegrationDetail[IntegrationRegistry.Names.Length];
            _previousValues = new IntegrationTelemetryData[IntegrationRegistry.Names.Length];

            for (var i = 0; i < IntegrationRegistry.Names.Length; i++)
            {
                _integrationsById[i] = new IntegrationDetail { Name = IntegrationRegistry.Names[i] };
            }
        }

        public void RecordTracerSettings(ImmutableTracerSettings settings)
        {
            for (var i = 0; i < settings.IntegrationsInternal.Settings.Length; i++)
            {
                var integration = settings.IntegrationsInternal.Settings[i];
                if (integration.EnabledInternal == false)
                {
                    _integrationsById[i].WasExplicitlyDisabled = 1;
                }
            }

            SetHasChanges();
        }

        /// <summary>
        /// Should be called when an integration is first executed (not necessarily successfully)
        /// </summary>
        public void IntegrationRunning(IntegrationId integrationId)
        {
            ref var value = ref _integrationsById[(int)integrationId];
            if (value.WasExecuted == 1)
            {
                return;
            }

            var previousValue = Interlocked.Exchange(ref value.WasExecuted, 1);
            if (previousValue == 0)
            {
                SetHasChanges();
            }
        }

        /// <summary>
        /// Should be called when an integration successfully generates a span
        /// </summary>
        public void IntegrationGeneratedSpan(IntegrationId integrationId)
        {
            ref var value = ref _integrationsById[(int)integrationId];
            if (value.WasExecuted == 1 && value.HasGeneratedSpan == 1)
            {
                return;
            }

            var previousWasExecuted = Interlocked.Exchange(ref value.WasExecuted, 1);
            var previousHasGeneratedSpan = Interlocked.Exchange(ref value.HasGeneratedSpan, 1);

            if (previousWasExecuted == 0 || previousHasGeneratedSpan == 0)
            {
                SetHasChanges();
            }
        }

        public void IntegrationDisabledDueToError(IntegrationId integrationId, string error)
        {
            ref var value = ref _integrationsById[(int)integrationId];
            if (value.Error is not null)
            {
                return;
            }

            var previousValue = Interlocked.Exchange(ref value.Error, error);
            if (previousValue is null)
            {
                SetHasChanges();
            }
        }

        public bool HasChanges()
        {
            return _hasChangesFlag == 1;
        }

        /// <summary>
        /// Get the latest data to send to the intake.
        /// </summary>
        /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
        public ICollection<IntegrationTelemetryData>? GetData()
        {
            var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
            if (!hasChanges)
            {
                return null;
            }

            // Must only include integrations that have changed since last time.
            // If this is the first time we're sending data, we know we're sending all of them
            List<IntegrationTelemetryData> changed;

            if (!_hasSentFirstValues)
            {
                _hasSentFirstValues = true;
                changed = new(IntegrationRegistry.Names.Length);
            }
            else
            {
                changed = new();
            }

            return BuildTelemetryData(changed, includeAllValues: false);
        }

        public List<IntegrationTelemetryData> GetFullData()
            => BuildTelemetryData(new(IntegrationRegistry.Names.Length), includeAllValues: true);

        private List<IntegrationTelemetryData> BuildTelemetryData(List<IntegrationTelemetryData> integrations, bool includeAllValues)
        {
            for (var i = 0; i < _integrationsById.Length; i++)
            {
                var integration = _integrationsById[i];
                var data = new IntegrationTelemetryData(
                    name: integration.Name,
                    enabled: integration.HasGeneratedSpan > 0 && integration.WasExplicitlyDisabled == 0,
                    autoEnabled: integration.WasExecuted > 0,
                    error: integration.Error);

                if (includeAllValues)
                {
                    // don't update previous if we're dumping all values
                    integrations.Add(data);
                }
                else if (!data.Equals(_previousValues[i]))
                {
                    _previousValues[i] = data;
                    integrations.Add(data);
                }
            }

            return integrations;
        }

        private void SetHasChanges()
        {
            Interlocked.Exchange(ref _hasChangesFlag, 1);
        }

        internal struct IntegrationDetail
        {
            /// <summary>
            /// Gets or sets the integration info of the integration
            /// </summary>
            public string Name;

            /// <summary>
            /// Gets or sets a value indicating whether an integration successfully generated a span
            /// 0 = not generated, 1 = generated
            /// </summary>
            public int HasGeneratedSpan;

            /// <summary>
            /// Gets or sets a value indicating whether the integration ever executed
            /// 0 = not generated, 1 = generated
            /// </summary>
            public int WasExecuted;

            /// <summary>
            /// Gets or sets a value indicating whether the integration was disabled by a user
            /// 0 = not generated, 1 = generated
            /// </summary>
            public int WasExplicitlyDisabled;

            /// <summary>
            /// Gets or sets a value indicating whether an integration was disabled due to a fatal error
            /// </summary>
            public string? Error;
        }
    }
}
