// <copyright file="StatsdData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.StatsdClient.Bufferize;
using Datadog.Trace.Vendors.StatsdClient.Transport;

// ReSharper disable once CheckNamespace
namespace Datadog.Trace.Vendors.StatsdClient
{
    internal class StatsdData
    {
        private ITransport _transport;
        private StatsBufferize _statsBufferize;

        public StatsdData(
            MetricsSender metricsSender,
            StatsBufferize statsBufferize,
            ITransport transport,
            Telemetry telemetry)
        {
            MetricsSender = metricsSender;
            Telemetry = telemetry;
            _statsBufferize = statsBufferize;
            _transport = transport;
        }

        public MetricsSender MetricsSender { get; private set; }

        public Telemetry Telemetry { get; private set; }

        public void Flush()
        {
            _statsBufferize?.Flush();
            Telemetry.Flush();
        }

        public async Task DisposeAsync()
        {
            // _statsBufferize and _telemetry must be disposed before _statsSender to make
            // sure _statsSender does not received data when it is already disposed.

            Telemetry?.Dispose();
            Telemetry = null;

            var statsBufferize = _statsBufferize;
            if (statsBufferize != null)
            {
                _statsBufferize = null;
                await statsBufferize.DisposeAsync().ConfigureAwait(false);
            }

            _transport?.Dispose();
            _transport = null;

            MetricsSender = null;
        }
    }
}
