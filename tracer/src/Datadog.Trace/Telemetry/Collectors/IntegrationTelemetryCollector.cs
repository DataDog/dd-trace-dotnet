// <copyright file="IntegrationTelemetryCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry
{
    internal class IntegrationTelemetryCollector
    {
        private readonly IntegrationDetail[] _integrationsById;

        private int _hasChangesFlag = 0;

        public IntegrationTelemetryCollector()
        {
            _integrationsById = new IntegrationDetail[IntegrationRegistry.Names.Length];

            for (var i = 0; i < IntegrationRegistry.Names.Length; i++)
            {
                _integrationsById[i] = new IntegrationDetail { Name = IntegrationRegistry.Names[i] };
            }
        }

        public void RecordTracerSettings(ImmutableTracerSettings settings)
        {
            for (var i = 0; i < settings.Integrations.Settings.Length; i++)
            {
                var integration = settings.Integrations.Settings[i];
                if (integration.Enabled == false)
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
        public ICollection<IntegrationTelemetryData> GetData()
        {
            var hasChanges = Interlocked.CompareExchange(ref _hasChangesFlag, 0, 1) == 1;
            if (!hasChanges)
            {
                return null;
            }

            return _integrationsById
                  .Select(
                       integration => new IntegrationTelemetryData(
                           name: integration.Name,
                           enabled: integration.HasGeneratedSpan > 0 && integration.WasExplicitlyDisabled == 0)
                       {
                           AutoEnabled = integration.WasExecuted > 0,
                           Error = integration.Error
                       })
                  .ToList();
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
            public string Error;
        }
    }
}
