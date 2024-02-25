// <copyright file="NopProbeStatusSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.Debugger.Sink
{
    internal class NopProbeStatusSink : ProbeStatusSink
    {
        private static readonly List<ProbeStatus> EmptyProbeStatus = new();

        public override List<ProbeStatus> GetDiagnostics()
        {
            return EmptyProbeStatus;
        }

        internal override void AddReceived(string probeId)
        {
        }

        public override void AddProbeStatus(string probeId, Status status, int probeVersion = 0, Exception exception = null, string errorMessage = null)
        {
        }

        internal override void AddInstalled(string probeId)
        {
        }

        internal override void AddError(string probeId, Exception e)
        {
        }

        internal override void AddBlocked(string probeId)
        {
        }

        public override void Remove(string probeId)
        {
        }
    }
}
