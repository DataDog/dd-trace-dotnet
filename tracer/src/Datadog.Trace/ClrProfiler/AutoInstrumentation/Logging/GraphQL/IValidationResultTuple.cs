// <copyright file="IValidationResultTuple.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable 1591

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// ValueTuple returned by DocumentValidator in GraphQL4
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IValidationResultTuple
    {
        [DuckField]
        public IValidationResult Item1 { get; }
    }
}
