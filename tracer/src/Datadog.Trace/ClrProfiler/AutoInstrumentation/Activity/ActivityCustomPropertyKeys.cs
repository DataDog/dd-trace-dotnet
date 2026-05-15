// <copyright file="ActivityCustomPropertyKeys.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Constants for the named objects we attach to <c>System.Diagnostics.Activity</c> via
    /// <c>SetCustomProperty</c>/<c>GetCustomProperty</c>. Defined on a non-generic type so they
    /// can be referenced without instantiating <see cref="ActivityCustomPropertyAccessor{TTarget}"/>.
    /// </summary>
    internal static class ActivityCustomPropertyKeys
    {
        internal const string Span = "__dd_span__";
        internal const string Resource = "__dd_resource__";
        internal const string InitialOpName = "__dd_initial_op__";
    }
}
