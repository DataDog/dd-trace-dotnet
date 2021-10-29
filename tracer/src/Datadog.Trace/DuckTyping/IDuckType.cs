// <copyright file="IDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck type interface
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDuckType
    {
        /// <summary>
        /// Gets instance
        /// </summary>
        object Instance { get; }

        /// <summary>
        /// Gets instance Type
        /// </summary>
        Type Type { get; }
    }
}
