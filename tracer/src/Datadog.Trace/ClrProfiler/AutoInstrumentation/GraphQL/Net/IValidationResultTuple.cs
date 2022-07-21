// <copyright file="IValidationResultTuple.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL.Net
{
    /// <summary>
    /// ValueTuple returned by DocumentValidator in GraphQL4
    /// </summary>
    internal interface IValidationResultTuple
    {
        [DuckField]
        public IValidationResult Item1 { get; }
    }
}
