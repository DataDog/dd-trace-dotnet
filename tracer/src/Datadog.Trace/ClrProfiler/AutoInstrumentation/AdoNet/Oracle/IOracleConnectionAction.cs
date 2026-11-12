// <copyright file="IOracleConnectionAction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Oracle
{
    /// <summary>
    /// Duck type for the ODP.NET OracleConnection (managed and unmanaged drivers).
    /// </summary>
    internal interface IOracleConnectionAction
    {
        /// <summary>
        /// Sets the session ACTION (V$SESSION.ACTION). The driver sends it with the next round trip.
        /// </summary>
        string ActionName { set; }
    }
}
