// <copyright file="ActivityShape.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity
{
    /// <summary>
    /// The Activity duck-type shape, chosen once from the DiagnosticSource assembly version.
    /// </summary>
    internal enum ActivityShape
    {
        /// <summary>
        /// Not initialized, or DiagnosticSource is unsupported (below 4.0.4).
        /// </summary>
        None = 0,

        /// <summary>
        /// DiagnosticSource [4.0.4, 5.0): DiagnosticListener path, <see cref="DuckTypes.IActivity"/>.
        /// </summary>
        Legacy = 1,

        /// <summary>
        /// DiagnosticSource [5.0, 6.0): ActivityListener path, <see cref="DuckTypes.IActivity5"/>.
        /// </summary>
        Activity5 = 2,

        /// <summary>
        /// DiagnosticSource 6.0+: ActivityListener path, <see cref="DuckTypes.IActivity6"/>.
        /// </summary>
        Activity6 = 3,
    }
}
