// <copyright file="IFunctionDescriptor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// For duck typing
    /// </summary>
    public interface IFunctionDescriptor
    {
        /// <summary>Gets the ID of the function.</summary>
        string Id { get; }

        /// <summary>Gets the fully qualified name of the function. This is 'Namespace.Class.Method' </summary>
        string FullName { get; }

        /// <summary>Gets the display name of the function. This is commonly 'Class.Method' </summary>
        string ShortName { get; }

        /// <summary>Gets the name used for logging. This is 'Method' or the value overwritten by [FunctionName] </summary>
        string LogName { get; }
    }
}
