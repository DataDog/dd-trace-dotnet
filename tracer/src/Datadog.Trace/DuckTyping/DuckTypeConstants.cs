// <copyright file="DuckTypeConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Provides helper operations for duck type constants.
    /// </summary>
    internal static class DuckTypeConstants
    {
        /// <summary>
        /// Defines the duck type assembly prefix constant.
        /// </summary>
        internal const string DuckTypeAssemblyPrefix = "Datadog.DuckTypeAssembly.";

        /// <summary>
        /// Defines the duck type not visible assembly prefix constant.
        /// </summary>
        public const string DuckTypeNotVisibleAssemblyPrefix = "Datadog.DuckTypeNotVisibleAssembly.";

        /// <summary>
        /// Defines the duck type generic type assembly prefix constant.
        /// </summary>
        public const string DuckTypeGenericTypeAssemblyPrefix = "Datadog.DuckTypeGenericTypeAssembly.";
    }
}
