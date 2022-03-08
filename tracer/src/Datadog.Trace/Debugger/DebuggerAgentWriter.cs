// <copyright file="DebuggerAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger
{
    internal class DebuggerAgentWriter
    {
        private readonly DebuggerApi _api;

        private DebuggerAgentWriter(DebuggerApi api)
        {
            _api = api;
        }

        public static DebuggerAgentWriter Create(DebuggerApi api)
        {
            return new DebuggerAgentWriter(api);
        }

        public async Task WriteSnapshot(string snapshot)
        {
            var arraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(snapshot));
            await _api.SendSnapshotsAsync(arraySegment, 1).ConfigureAwait(false);
        }
    }
}
