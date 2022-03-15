// <copyright file="ModelBindingResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// model binding result duck type
    /// </summary>
    [DuckTyping.DuckCopy]
    internal struct ModelBindingResult
    {
        /// <summary>
        /// Gets the model associated with this context.
        /// </summary>
        public object Model;

        /// <summary>
        /// <para>
        /// Gets a value indicating whether or not the <see cref="Model"/> value has been set.
        /// </para>
        /// <para>
        /// This property can be used to distinguish between a model binder which does not find a value and
        /// the case where a model binder sets the <c>null</c> value.
        /// </para>
        /// </summary>
        public bool IsModelSet;
    }
}
#endif
